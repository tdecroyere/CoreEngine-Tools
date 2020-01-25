#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;
using namespace simd;

constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(8));


struct SimpleMaterial
{
    float4 DiffuseColor;
    int DiffuseTexture;
    int NormalTexture;
    int BumpTexture;
    int SpecularTexture;
};

float3 ResolveNormalFromSurfaceGradient(float3 nrmBaseNormal, float3 surfGrad)
{
    float resolveSign = 1;
// resolve sign +/-1. Should only be -1 for double sided
// materials when viewing the back-face and the mode is // set to flip.
    return normalize(nrmBaseNormal - resolveSign*surfGrad); 
}

// input: vM is channels .xy of a tangent space normal in [-1;1] // out: convert vM to a derivative
float2 TspaceNormalToDerivative(float2 vM) 
{
    const float fS = 1.0/(128*128); 
    float2 vMsq = vM*vM;
    const float mz_sq = 1-vMsq.x-vMsq.y;
    const float maxcompxy_sq = fS*max(vMsq.x,vMsq.y);
    const float z_inv = rsqrt( max(mz_sq,maxcompxy_sq) );

    return -z_inv * float2(vM.x, -vM.y);
}

float2 NoParallax(texture2d<float> normalTexture, sampler texture_sampler, float3 texDir3D, float2 texCoord )
{
    float bumpScale = 0.005;

    float lod = normalTexture.calculate_clamped_lod(texture_sampler, texCoord);

    float height = 1.0;
    float numSteps = mix(10, 5, texDir3D.z);

    if (lod > 1)
    {
        numSteps = 1;
    }

    // TODO: Use depth info to decrement the number of steps
    float step = 1.0 / numSteps;
    float2 delta = float2(-texDir3D.x, texDir3D.y) * bumpScale / (texDir3D.z * numSteps);

    float2 offsetCoord = texCoord;
    float bump = normalTexture.sample(texture_sampler, offsetCoord).r;
    
    while (bump < height) {
        height -= step;
        offsetCoord += delta;
        bump = normalTexture.sample(texture_sampler, offsetCoord).r;
    }

    //Move one step back to the position before we hit terrain
    float2 oldOffset = offsetCoord - delta;
    float oldHeight = height + step;
    float oldBump = normalTexture.sample(texture_sampler, oldOffset).r;

    float oldDistToTerrain = oldBump - oldHeight;
    float currentDistToTerrain = bump - height;

    float weight = currentDistToTerrain / (currentDistToTerrain - oldDistToTerrain);
    
    offsetCoord = oldOffset * weight + offsetCoord * (1 - weight);

    return offsetCoord;
}

MaterialData ProcessSimpleMaterial(float3 position, float3 worldNormal, float3 viewDirection, bool depthOnly, float2 textureCoordinates, const device void* material, int materialTextureOffset, const device ShaderParameters& shaderParameters)
{
    MaterialData materialData = {};

    const device SimpleMaterial& simpleMaterial = *((const device SimpleMaterial*)material);

    texture2d<float> diffuseTexture = GetTexture(shaderParameters, materialTextureOffset, simpleMaterial.DiffuseTexture);
    texture2d<float> normalTexture = GetTexture(shaderParameters, materialTextureOffset, simpleMaterial.NormalTexture);
    texture2d<float> specularTexture = GetTexture(shaderParameters, materialTextureOffset, simpleMaterial.SpecularTexture);
    texture2d<float> bumpTexture = GetTexture(shaderParameters, materialTextureOffset, simpleMaterial.BumpTexture);

    materialData.Normal = worldNormal;
    materialData.Albedo = (simpleMaterial.DiffuseColor.a > 0) ? simpleMaterial.DiffuseColor.rgb : float3(1, 1, 1);
    materialData.Alpha = (simpleMaterial.DiffuseColor.a > 0) ? simpleMaterial.DiffuseColor.a : 1.0;

    if (!depthOnly && simpleMaterial.NormalTexture > 0 && !(worldNormal.x == 0 && worldNormal.y == 0 && worldNormal.z == 0))
    {
        if (simpleMaterial.BumpTexture > 0)
        {
            float3  N            = normalize(worldNormal);
            float3  dp1          = dfdx( -viewDirection.xyz );
            float3  dp2          = dfdy( -viewDirection.xyz );
            float2  duv1         = dfdx( textureCoordinates );
            float2  duv2         = dfdy( textureCoordinates );

            float3 T = normalize(dp1 * duv2.y - dp2 * duv1.y);
            float3 B = -normalize(cross(N, T));

            float3x3 tbnMat = float3x3(T, B, N);

            float3 texDir3D = normalize( transpose(tbnMat) * -viewDirection );

            textureCoordinates = NoParallax(bumpTexture, texture_sampler, texDir3D, textureCoordinates);
        }

        float2 textureColor = normalTexture.sample(texture_sampler, textureCoordinates).rg * 2.0 - 1.0;
        float3 nrmBaseNormal = normalize(worldNormal);
        float3 surfaceGradient = float3(TspaceNormalToDerivative(textureColor), 0);
        materialData.Normal = ResolveNormalFromSurfaceGradient(nrmBaseNormal, surfaceGradient);
    }

    if (simpleMaterial.DiffuseTexture > 0)
    {
        float4 textureDiffuseColor = diffuseTexture.sample(texture_sampler, textureCoordinates);
        float lod = diffuseTexture.calculate_clamped_lod(texture_sampler, textureCoordinates);
        
        if (lod > 0)
        {
            if (textureDiffuseColor.a < 0.5)
                textureDiffuseColor.a = 0;
            
            if (textureDiffuseColor.a > 0)
                textureDiffuseColor.a = 1;
        }

        if (textureDiffuseColor.a == 1)
        {
            materialData.Albedo = textureDiffuseColor.rgb;
        }

        else
        {
            materialData.Albedo = textureDiffuseColor.rgb;
            materialData.Alpha = textureDiffuseColor.a;
        }
    }

    if (!depthOnly && simpleMaterial.SpecularTexture > 0)
    {
        float4 specularColor = specularTexture.sample(texture_sampler, textureCoordinates);

        materialData.Occlusion = specularColor.r;
        materialData.Roughness = specularColor.g;
        materialData.Metallic = specularColor.b;
    }

    return materialData;
}