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
    BoundingFrustum BoundingFrustum;
    int OpaqueCommandListIndex;
    int OpaqueDepthCommandListIndex;
    int TransparentCommandListIndex;
    int TransparentDepthCommandListIndex;
    bool DepthOnly;
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
    const device Camera* Cameras [[id(1)]];
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

float ComputeLightShadow(Light light, float3 normal, texture2d<float> shadowMap, float3 lightSpacePosition)
{
    constexpr sampler depthTextureSampler(mag_filter::nearest,
                                      min_filter::nearest,
                                      mip_filter::nearest, 
                                      address::clamp_to_border,
                                      border_color::opaque_white);
                                      
    float2 shadowUv = lightSpacePosition.xy * float2(0.5, -0.5) + 0.5;
    float shadowMapDepth = shadowMap.sample(depthTextureSampler, shadowUv).r;

    float minBias = 0.06;
    float maxBias = 0.501;

    //float bias = max(maxBias * (1.0 - dot(normal, normalize(light.Camera1.WorldPosition))), minBias);  
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
    float diffuseScale = 2.5;
    float specularScale = 3;
    float reflectance = 0.5;
    float invMetallic = 1.0 - materialData.Metallic;

    constexpr sampler env_texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(8));

    float3 reflectionVector = materialData.Normal;//reflect(-viewDirection, materialData.Normal);

    float4 cubeMapSample = irradianceEnvironmentMap.sample(env_texture_sampler, reflectionVector);
    float3 diffuseIBL = cubeMapSample.rgb * materialData.Albedo * invMetallic;;

    reflectionVector = reflect(-viewDirection, materialData.Normal);

    cubeMapSample = environmentMap.sample(env_texture_sampler, reflectionVector);
    float3 specularColor = 0.16 * reflectance * reflectance * invMetallic + materialData.Albedo * materialData.Metallic;
    float3 specularIBL = cubeMapSample.rgb * specularColor;

    return diffuseIBL * diffuseScale + specularIBL * specularScale;
}

float3 ComputeLightContribution(Light light, MaterialData materialData, texture2d<float> shadowMap, texturecube<float> environmentMap, texturecube<float> irradianceEnvironmentMap, float3 lightSpacePosition, float3 viewDirection)
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
        lightShadow = ComputeLightShadow(light, materialData.Normal, shadowMap, lightSpacePosition);
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
    float4 cascadeColors[4] = 
    {
        float4(1, 0, 0, 1),
        float4(0, 1, 0, 1),
        float4(0, 0, 1, 1),
        float4(1, 1, 0, 1)
    };

    float4 cascadeColor = cascadeColors[3];

    for (int i = 0; i < 4; i++)
    {
        Camera lightCamera = shaderParameters.Cameras[light.CameraIndexes[i]];
        float4 rawPosition = ((lightCamera.ProjectionMatrix * lightCamera.ViewMatrix)) * float4(worldPosition, 1);
        float3 lightSpacePosition = rawPosition.xyz / rawPosition.w;

        if (all(lightSpacePosition.xyz < 1.0) && all(lightSpacePosition.xyz > float3(-1,-1,0)))
        {
            cascadeColor = cascadeColors[i];
            break;
        }
    }

    float alpha = 0.1;
    return cascadeColor * alpha + fragmentColor * (1 - alpha);
}

#include "Materials/SimpleMaterial.h"