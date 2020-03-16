#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    packed_float3 Position;
    packed_float3 Normal;
    packed_float2 TextureCoordinates;
};

struct BoundingBox
{
    packed_float3 MinPoint;
    packed_float3 MaxPoint;
};

struct BoundingFrustum
{
    packed_float4 LeftPlane;
    packed_float4 RightPlane;
    packed_float4 TopPlane;
    packed_float4 BottomPlane;
    packed_float4 NearPlane;
    packed_float4 FarPlane;
};

struct GeometryPacket
{
    int VertexBufferIndex;
    int IndexBufferIndex;
};

struct GeometryInstance
{
    int GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    int MaterialIndex;
    float4x4 WorldMatrix;
    BoundingBox WorldBoundingBox;
};

struct Camera
{
    int DepthBufferTextureIndex;
    packed_float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
    float4x4 ViewProjectionMatrix;
    float4x4 ViewProjectionInverse;
    BoundingFrustum BoundingFrustum;
    int OpaqueCommandListIndex;
    int OpaqueDepthCommandListIndex;
    int TransparentCommandListIndex;
    int TransparentDepthCommandListIndex;
    bool DepthOnly;
    bool AlreadyProcessed;
    float MinDepth;
    float MaxDepth;
    int MomentShadowMapTextureIndex;
    int OcclusionDepthTextureIndex;
    int OcclusionDepthCommandListIndex;
};

struct Light
{
    packed_float3 WorldSpacePosition;
    int CameraIndexes[4];
};

struct Material
{
    int MaterialBufferIndex;
    int MaterialTextureOffset;
    bool IsTransparent;
};

struct SceneProperties
{
    int ActiveCameraIndex;
    int DebugCameraIndex;
    bool isDebugCameraActive;
};

struct ShaderParameters
{
    const device SceneProperties& SceneProperties [[id(0)]];
    device Camera* Cameras [[id(1)]];
    const device Light* Lights [[id(2)]];
    const device Material* Materials [[id(3)]];
    const device GeometryPacket* GeometryPackets [[id(4)]];
    const device GeometryInstance* GeometryInstances [[id(5)]];
    const array<const device void*, 10000> Buffers [[id(6)]];
    const array<texture2d<float>, 10000> Textures [[id(10006)]];
    const array<texturecube<float>, 10000> CubeTextures [[id(20006)]];
    const array<command_buffer, 100> IndirectCommandBuffers [[id(30006)]];
    device uint* IndirectCommandBufferCounters [[id(30106)]];
};

struct MaterialData
{
    float3 Albedo;
    float3 Normal;
    float Occlusion;
    float Roughness;
    float Metallic;
    float Alpha;
};

float3 lerp(float3 a, float3 b, float w)
{
  return a + w*(b-a);
}

texture2d<float> GetTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.Textures[materialTextureOffset + (materialTextureIndex - 1)];
}

float4 ConvertOptimizedMoments(float4 optimizedMoments)
{
    optimizedMoments[0] -= 0.035955884801f;
    return float4x4(0.2227744146f, 0.1549679261f, 0.1451988946f, 0.163127443f,
                                          0.0771972861f, 0.1394629426f, 0.2120202157f, 0.2591432266f,
                                          0.7926986636f, 0.7963415838f, 0.7258694464f, 0.6539092497f,
                                          0.0319417555f,-0.1722823173f,-0.2758014811f,-0.3376131734f) * optimizedMoments;
}

float mad(float m, float a, float b)
{
    return m * a + b;
}

float Linstep(float a, float b, float v)
{
    return saturate((v - a) / (b - a));
}

// Reduces VSM light bleedning
float ReduceLightBleeding(float pMax, float amount)
{
  // Remove the [0, amount] tail and linearly rescale (amount, 1].
   return Linstep(amount, 1.0f, pMax);
}

float4 Convert4MomentToCanonical(float4 Moments0, float MomentBias=3.0e-7f)
{
	return mix(Moments0,float4(0.0f,0.375f,0.0f,0.375f),MomentBias);
}

float Compute4MomentShadowIntensity(float4 Biased4Moments, float FragmentDepth,float DepthBias)
{
    float OutShadowIntensity = 0;

	// Use short-hands for the many formulae to come
	float4 b=Biased4Moments;
	float3 z;
	z[0]=FragmentDepth-DepthBias;

	// Compute a Cholesky factorization of the Hankel matrix B storing only non-
	// trivial entries or related products
	float L21D11=mad(-b[0],b[1],b[2]);
	float D11=mad(-b[0],b[0], b[1]);
	float InvD11=1.0f/D11;
	float L21=L21D11*InvD11;
	float SquaredDepthVariance=mad(-b[1],b[1], b[3]);
	float D22=mad(-L21D11,L21,SquaredDepthVariance);

	// Obtain a scaled inverse image of bz=(1,z[0],z[0]*z[0])^T
	float3 c=float3(1.0f,z[0],z[0]*z[0]);
	// Forward substitution to solve L*c1=bz
	c[1]-=b.x;
	c[2]-=b.y+L21*c[1];
	// Scaling to solve D*c2=c1
	c[1]*=InvD11;
	c[2]/=D22;
	// Backward substitution to solve L^T*c3=c2
	c[1]-=L21*c[2];
	c[0]-=dot(c.yz,b.xy);
	// Solve the quadratic equation c[0]+c[1]*z+c[2]*z^2 to obtain solutions z[1] 
	// and z[2]
	float InvC2=1.0f/c[2];
	float p=c[1]*InvC2;
	float q=c[0]*InvC2;
	float D=((p*p)/4.0f)-q;
	float r=sqrt(D);
	z[1]=-(p/2.0f)-r;
	z[2]=-(p/2.0f)+r;

	// Use a solution made of four deltas if the solution with three deltas is 
	// invalid
	
    if(z[1]<-1.0f || z[2]>1.0f){
		float zFree=((b[0]-b[2])*z[0]+b[3]-b[1])/(z[0]+b[2]-b[0]-b[1]*z[0]);
		float w1Factor=(z[0]>zFree)?1.0f:0.0f;
		// Construct a polynomial taking value zero at z[0] and 1, value 1 at -1 and 
		// value w1Factor at zFree. Start with a linear part and then multiply by 
		// linear factors to get the roots.
		float2 Normalizers;
		Normalizers.x=w1Factor/((zFree-z[0])*mad(zFree,zFree,-1.0f));
		Normalizers.y=0.5f/((zFree+1.0f)*(z[0]+1.0f));
		float4 Polynomial;
		Polynomial[0]=mad(zFree,Normalizers.y,Normalizers.x);
		Polynomial[1]=Normalizers.x-Normalizers.y;
		// Multiply the polynomial by (z-z[0])
		Polynomial[2]=Polynomial[1];
		Polynomial[1]=mad(Polynomial[1],-z[0],Polynomial[0]);
		Polynomial[0]*=-z[0];
		// Multiply the polynomial by (z-1)
		Polynomial[3]=Polynomial[2];
		Polynomial.yz=Polynomial.xy-Polynomial.yz;
		Polynomial[0]*=-1.0f;
		// The shadow intensity is the dot product of the coefficients of this 
		// polynomial and the power moments for the respective powers
		OutShadowIntensity=dot(Polynomial,float4(1.0f,b.xyz));
	}
	// Use the solution with three deltas
	else{
		float4 Switch=
			(z[2]<z[0])?float4(z[1],z[0],1.0f,1.0f):(
			(z[1]<z[0])?float4(z[0],z[1],0.0f,1.0f):
			float4(0.0f,0.0f,0.0f,0.0f));
		float Quotient=(Switch[0]*z[2]-b[0]*(Switch[0]+z[2])+b[1])/((z[2]-Switch[1])*(z[0]-z[1]));
		OutShadowIntensity=Switch[2]+Switch[3]*Quotient;
	}
	OutShadowIntensity=saturate(OutShadowIntensity);
    return OutShadowIntensity;
}

float ComputeMSMHamburger(float4 moments, float fragmentDepth, float depthBias, float momentBias)
{
    // Bias input data to avoid artifacts
    float4 b = mix(moments, float4(0.5f, 0.5f, 0.5f, 0.5f), momentBias);

    float3 z;
    z[0] = fragmentDepth - depthBias;

    // Compute a Cholesky factorization of the Hankel matrix B storing only non-
    // trivial entries or related products
    float L32D22 = fma(-b[0], b[1], b[2]);
    float D22 = fma(-b[0], b[0], b[1]);
    float squaredDepthVariance = fma(-b[1], b[1], b[3]);
    
    float D33D22 = dot(float2(squaredDepthVariance, -L32D22), float2(D22, L32D22));
    float InvD22 = 1.0f / D22;
    float L32 = L32D22 * InvD22;

    // Obtain a scaled inverse image of bz = (1,z[0],z[0]*z[0])^T
    float3 c = float3(1.0f, z[0], z[0] * z[0]);

    // Forward substitution to solve L*c1=bz
    c[1] -= b.x;
    c[2] -= b.y + L32 * c[1];

    // Scaling to solve D*c2=c1
    c[1] *= InvD22;
    c[2] *= D22 / D33D22;

    // Backward substitution to solve L^T*c3=c2
    c[1] -= L32 * c[2];
    c[0] -= dot(c.yz, b.xy);

    // Solve the quadratic equation c[0]+c[1]*z+c[2]*z^2 to obtain solutions
    // z[1] and z[2]
    float p = c[1] / c[2];
    float q = c[0] / c[2];
    float D = (p * p * 0.25f) - q;
    float r = sqrt(D);
    z[1] =- p * 0.5f - r;
    z[2] =- p * 0.5f + r;

    // Compute the shadow intensity by summing the appropriate weights
    float4 switchVal = (z[2] < z[0]) ? float4(z[1], z[0], 1.0f, 1.0f) :
                      ((z[1] < z[0]) ? float4(z[0], z[1], 0.0f, 1.0f) :
                      float4(0.0f,0.0f,0.0f,0.0f));
    float quotient = (switchVal[0] * z[2] - b[0] * (switchVal[0] + z[2]) + b[1])/((z[2] - switchVal[1]) * (z[0] - z[1]));
    float shadowIntensity = switchVal[2] + switchVal[3] * quotient;
    return 1 - saturate(shadowIntensity);
}

float3 GetShadowPosOffset(float nDotL, float3 normal, float shadowMapSize)
{
    float OffsetScale = 0;

    float texelSize = 2.0f / shadowMapSize;
    float nmlOffsetScale = saturate(1.0f - nDotL);
    return texelSize * OffsetScale * nmlOffsetScale * normal;
}

float SampleShadowMapMSM(Light light, Camera lightCamera, float3 normal, texture2d<float> shadowMap, float3 lightSpacePosition)
{
    float3 offset = GetShadowPosOffset(saturate(dot(lightCamera.WorldPosition, normal)), normal, 2048);
    lightSpacePosition += offset;

    float3 shadowPosition = lightSpacePosition;

    shadowPosition.xy = shadowPosition.xy * float2(0.5, -0.5) + 0.5;

    float depth = shadowPosition.z;// * 0.5 + 0.5;

    float MSMDepthBias = 0.0;
    // float MSMMomentBias = 0.05;
    float MSMMomentBias = 0.05;
    // float LightBleedingReduction = 0.99;
    // float LightBleedingReduction = 0.85;
    float LightBleedingReduction = 0.85;

    constexpr sampler depthTextureSampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, max_anisotropy(8));


    // constexpr sampler depthTextureSampler(mag_filter::nearest,
    //                                   min_filter::nearest,
    //                                   mip_filter::nearest);
                                      
    float4 moments = shadowMap.sample(depthTextureSampler, shadowPosition.xy);
    moments = ConvertOptimizedMoments(moments);
    // float result = ComputeMSMHamburger(moments, depth, MSMDepthBias, MSMMomentBias * 0.001);
    //float result = compute_msm_shadow_intensity(moments, depth, MSMMomentBias * 0.001);

    moments = Convert4MomentToCanonical(moments, MSMMomentBias * 0.001);
    float result = 1 - Compute4MomentShadowIntensity(moments, depth, MSMDepthBias);

    // if (result == 0)
    // {
    //     result = 1;
    // }

    result = ReduceLightBleeding(result, LightBleedingReduction);

    return result;
}


float ComputeLightShadow(Light light, Camera lightCamera, float3 normal, texture2d<float> shadowMap, float3 lightSpacePosition)
{
    // constexpr sampler depthTextureSampler(mag_filter::linear,
    //                                   min_filter::linear,
    //                                   mip_filter::linear, 
    //                                   address::clamp_to_border,
    //                                   border_color::opaque_white, max_anisotropy(8));

    constexpr sampler depthTextureSampler(mag_filter::nearest,
                                      min_filter::nearest,
                                      mip_filter::nearest, 
                                      address::clamp_to_border,
                                      border_color::opaque_white);
                                      
    float2 shadowUv = lightSpacePosition.xy * float2(0.5, -0.5) + 0.5;
    float shadowMapDepth = shadowMap.sample(depthTextureSampler, shadowUv).r;

    float minBias = 0.11;
    float maxBias = 0.501;

    //float bias = max(maxBias * (1.0 - dot(normal, normalize(lightCamera.WorldPosition))), 0.235);  
    float bias = max(maxBias * (1.0 - shadowMapDepth), minBias);  
    
    float lightSpaceDepth = lightSpacePosition.z - bias;
    //float lightSpaceDepth = lightSpacePosition.z - 0.06;

    return lightSpaceDepth < shadowMapDepth;// * (dot(normal, normalize(light.Camera1.WorldPosition)) > 0);
}

//--------------------------------------------------------
// Testing Normal Distribution Functions
//--------------------------------------------------------

float NormalDistributionGGX(float NdotH, float roughness)
{
    float a = NdotH * roughness;
    float k = roughness / (1.0 - NdotH * NdotH + a * a);
    return k * k * M_1_PI_F;
}

//--------------------------------------------------------
// Testing Geometric Shadowing Functions
//--------------------------------------------------------

float VisibilityFunctionSmithGGX(float NdotV, float NdotL, float roughness) 
{
    float a2 = roughness * roughness;
    float GGXV = NdotL * sqrt(NdotV * NdotV * (1.0 - a2) + a2);
    float GGXL = NdotV * sqrt(NdotL * NdotL * (1.0 - a2) + a2);
    return 0.5 / (GGXV + GGXL);
}

//--------------------------------------------------------
// Testing Fresnel Functions
//--------------------------------------------------------

float3 FresnelFunctionSchlick(float VdotH, float3 f0) 
{
    float f = pow(1.0 - VdotH, 5.0);
    return f + f0 * (1.0 - f);
}


float F_Schlick(float VoH, float f0, float f90) {
    return f0 + (f90 - f0) * pow(1.0 - VoH, 5.0);
}

float DiffuseReflectanceDisney(float NoV, float NoL, float LoH, float roughness) 
{
    float f90 = 0.5 + 2.0 * roughness * LoH * LoH;
    float lightScatter = F_Schlick(NoL, 1.0, f90);
    float viewScatter = F_Schlick(NoV, 1.0, f90);
    return lightScatter * viewScatter * M_1_PI_F;
}

float DiffuseReflectanceLambert() 
{
    return M_1_PI_F;
}

//--------------------------------------------------------
// Main BRDF function
//--------------------------------------------------------

float3 EvaluateBrdf(MaterialData materialData, float3 viewDirection, float3 lightDirection, float NdotL)
{
    float reflectance = 0.5;
    float invMetallic = 1.0 - materialData.Metallic;
    //materialData.Roughness = materialData.Roughness * materialData.Roughness;

    float3 halfDirection = normalize(viewDirection + lightDirection); 

    float NdotV =  abs(dot(materialData.Normal, viewDirection)) + 1e-5;
    float NdotH =  clamp(dot(materialData.Normal, halfDirection), 0.0 , 1.0);
    float LdotH =  clamp(dot(lightDirection, halfDirection), 0.0 , 1.0);

    float3 diffuseColor = materialData.Albedo * invMetallic;
    float3 specularColor = 0.16 * reflectance * reflectance * invMetallic + materialData.Albedo * materialData.Metallic;

    float3 specularDistribution = NormalDistributionGGX(NdotH, materialData.Roughness);
    //return specularDistribution;

    float visibilityFunction = VisibilityFunctionSmithGGX(NdotV, NdotL, materialData.Roughness);
    //return visibilityFunction;

    float3 fresnelFunction = FresnelFunctionSchlick(LdotH, specularColor);
    //return fresnelFunction;

    float3 specularReflectance = specularDistribution * visibilityFunction * fresnelFunction;
    //return specularReflectance;

    float3 diffuseReflectance = diffuseColor * DiffuseReflectanceLambert();
    //float3 diffuseReflectance = diffuseColor * DiffuseReflectanceDisney(NdotV, NdotL, LdotH, materialData.Roughness);
    //return diffuseReflectance;

    return (diffuseReflectance + specularReflectance);
}

float3 ComputeIBL(float3 viewDirection, MaterialData materialData, texturecube<float> environmentMap, texturecube<float> irradianceEnvironmentMap)
{
    constexpr sampler env_texture_sampler(mag_filter::linear,
                                        min_filter::linear,
                                        mip_filter::linear, address::repeat, max_anisotropy(8));
    
    float diffuseScale = 2.5;
    float specularScale = 3;
    float reflectance = 0.5;
    float invMetallic = 1.0 - materialData.Metallic;

    float3 diffuseColor = materialData.Albedo * invMetallic;
    float3 specularColor = 0.16 * reflectance * reflectance * invMetallic + materialData.Albedo * materialData.Metallic;

    float3 diffuseIBL = 0.0;

    if (!all(diffuseColor.xyz == 0))
    {
        float3 reflectionVector = materialData.Normal;//reflect(-viewDirection, materialData.Normal);

        float4 cubeMapSample = irradianceEnvironmentMap.sample(env_texture_sampler, reflectionVector);
        diffuseIBL = cubeMapSample.rgb * diffuseColor;
    }

    float3 reflectionVector = reflect(-viewDirection, materialData.Normal);

    float4 cubeMapSample = environmentMap.sample(env_texture_sampler, reflectionVector);
    float3 specularIBL = cubeMapSample.rgb * specularColor;

    return diffuseIBL * diffuseScale + specularIBL * specularScale;
}

float3 ComputeLightContribution(Light light, Camera lightCamera, MaterialData materialData, texture2d<float> shadowMap, texturecube<float> environmentMap, texturecube<float> irradianceEnvironmentMap, float3 lightSpacePosition, float3 viewDirection)
{
    constexpr sampler env_texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(8));

    //float3 lightColor = float3(1, 1, 1);
    // float3 lightColor = float3(10, 10, 10);
    float3 lightColor = float3(95, 91, 84);
    // float3 lightColor = float3(1000, 1000, 1000);
    float3 ambientColor = float3(0.1, 0.1, 0.1);
    float lightShadow = 1.0;
    float3 lightDirection = normalize(light.WorldSpacePosition);
    //float3 lightColor = environmentMap.sample(env_texture_sampler, lightDirection).rgb * 100;
    
    if (!is_null_texture(shadowMap))
    {
        // lightShadow = ComputeLightShadow(light, lightCamera, materialData.Normal, shadowMap, lightSpacePosition);
        // return SampleShadowMapMSM(light, lightCamera, materialData.Normal, shadowMap, lightSpacePosition).xyz;
        lightShadow = SampleShadowMapMSM(light, lightCamera, materialData.Normal, shadowMap, lightSpacePosition);
        //return lightShadow;
    }

    // float3 lightContribution = lightColor * saturate(dot(normalize(light.WorldSpacePosition), materialData.Normal));
    // return materialData.Albedo * (lightContribution * lightShadow + float3(0.2, 0.2, 0.2));

    float NdotL = clamp(dot(materialData.Normal, lightDirection), 0.0 , 1.0);
    float3 reflectance = EvaluateBrdf(materialData, viewDirection, lightDirection, NdotL);

    float3 iblColor = ComputeIBL(viewDirection, materialData, environmentMap, irradianceEnvironmentMap);

    return lightColor * lightShadow * reflectance * NdotL + iblColor;
}

float4 DebugAddCascadeColors(float4 fragmentColor, const device ShaderParameters& shaderParameters, Light light, float3 worldPosition)
{
    float4 cascadeColors[6] = 
    {
        float4(1, 0, 0, 1),
        float4(0, 1, 0, 1),
        float4(0, 0, 1, 1),
        float4(1, 1, 0, 1),
        float4(1, 0, 1, 1),
        float4(0, 1, 1, 1)
    };

    float4 cascadeColor = cascadeColors[5];

    for (int i = 0; i < 4; i++)
    {
        Camera lightCamera = shaderParameters.Cameras[light.CameraIndexes[i]];
        float4 rawPosition = ((lightCamera.ViewProjectionMatrix)) * float4(worldPosition, 1);
        float3 lightSpacePosition = rawPosition.xyz;

        if (all(lightSpacePosition.xyz < 1.0) && all(lightSpacePosition.xyz > float3(-1,-1,0)))
        {
            cascadeColor = cascadeColors[i];
            break;
        }
    }

    float alpha = 0.75;
    return cascadeColor * alpha + fragmentColor * (1 - alpha);
}

#include "Materials/SimpleMaterial.h"