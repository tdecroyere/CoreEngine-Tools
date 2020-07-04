#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

// TODO: Currently it doesn't support ms output. The solution would be to resolve and post process at the same time
// It also avoid having to load/store multiple passes
struct ShaderParameters
{
    const texture2d_ms<float, access::read> OpaqueTexture [[id(0)]];
    const texture2d_ms<float, access::read> TransparentTexture [[id(1)]];
    const texture2d_ms<float, access::read> TransparentRevealageTexture [[id(2)]];
    const texture2d<float, access::write> OutputTexture [[id(3)]];
};

float MaxComponent(float3 v)
{
    return max(max(v.x, v.y), v.z);
};

float MinComponent(float3 v)
{
    return min(min(v.x, v.y), v.z);
};


float4 ResolveTexturePixel(const device texture2d_ms<float>& texture, uint2 pixelCoordinates)
{
    uint sampleCount = texture.get_num_samples();  

    float4 accumulatedColor = 0;
    
    for(uint i = 0; i < sampleCount; i++) 
    {  
        float4 sample = texture.read(pixelCoordinates, i);  
        accumulatedColor += float4(sample);
    }  

    return float4(accumulatedColor / sampleCount);
}

kernel void Resolve(uint2 pixelCoordinates [[thread_position_in_grid]],
                    const device ShaderParameters& shaderParameters)
{            
    float4 opaqueColor = ResolveTexturePixel(shaderParameters.OpaqueTexture, pixelCoordinates);
    float modulation = ResolveTexturePixel(shaderParameters.TransparentRevealageTexture, pixelCoordinates).r;

    if (modulation == 1)
    {
        shaderParameters.OutputTexture.write(opaqueColor, pixelCoordinates);
        return;
    }

    float4 transparentColor = ResolveTexturePixel(shaderParameters.TransparentTexture, pixelCoordinates);

    if (isinf(transparentColor.a))
    {
        transparentColor.a = MaxComponent(transparentColor.xyz);
    }

    if (isinf(MaxComponent(transparentColor.xyz)))
    {
        transparentColor = float4(1.0);
    }

    const float epsilon = 0.0010000000;

    // Self modulation
    transparentColor.rgb *= ((float3)(0.5) + (max(modulation, epsilon) / (float3)((2.0 * max(epsilon, modulation)))));

    shaderParameters.OutputTexture.write(float4(opaqueColor.rgb * modulation + (transparentColor.rgb * (1 - modulation) / float3(max(transparentColor.a, 0.00001))), 1.0), pixelCoordinates);
}