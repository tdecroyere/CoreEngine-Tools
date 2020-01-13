#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;
using namespace simd;

struct SimpleMaterial
{
    float4 DiffuseColor;
    int DiffuseTexture;
    int NormalTexture;
    int BumpTexture;
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

MaterialData ProcessSimpleMaterial(float3 position, float3 normal, float3 worldNormal, float3 viewDirection, bool depthOnly, float2 textureCoordinates, const device void* material, int materialTextureOffset, const device ShaderParameters& shaderParameters)
{
    MaterialData materialData = {};

    const device SimpleMaterial& simpleMaterial = *((const device SimpleMaterial*)material);

    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(8));

    materialData.Normal = worldNormal;
    materialData.Albedo = simpleMaterial.DiffuseColor.a > 0 ? float4(simpleMaterial.DiffuseColor) : float4(1, 1, 1, 1);

    if (!depthOnly && simpleMaterial.NormalTexture > 0 && !(worldNormal.x == 0 && worldNormal.y == 0 && worldNormal.z == 0))
    {
        texture2d<float> normalTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.NormalTexture);

        if (simpleMaterial.BumpTexture > 0)
        {
            float3  N            = normalize(normal);
            float3  dp1          = dfdx( -viewDirection.xyz );
            float3  dp2          = dfdy( -viewDirection.xyz );
            float2  duv1         = dfdx( textureCoordinates );
            float2  duv2         = dfdy( textureCoordinates );

            float3 T = normalize(dp1 * duv2.y - dp2 * duv1.y);
            float3 B = -normalize(cross(N, T));

            float3x3 tbnMat = float3x3(T, B, N);

            float3 texDir3D = normalize( transpose(tbnMat) * -viewDirection );

            texture2d<float> bumpTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.BumpTexture);
            textureCoordinates = NoParallax(bumpTexture, texture_sampler, texDir3D, textureCoordinates);
        }

        float2 textureColor = normalTexture.sample(texture_sampler, textureCoordinates).rg * 2.0 - 1.0;
        float3 nrmBaseNormal = normalize(normal);
        float3 surfaceGradient = float3(TspaceNormalToDerivative(textureColor), 0);
        materialData.Normal = ResolveNormalFromSurfaceGradient(nrmBaseNormal, surfaceGradient);
    }

    if (simpleMaterial.DiffuseTexture > 0)
    {
        texture2d<float> diffuseTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.DiffuseTexture);
        float4 textureDiffuseColor = diffuseTexture.sample(texture_sampler, textureCoordinates);

        if (textureDiffuseColor.a == 1)
        {
            materialData.Albedo = float4(textureDiffuseColor.rgb, materialData.Albedo.a);
        }

        else
        {
            materialData.Albedo = textureDiffuseColor;
        }
    }

    return materialData;
}