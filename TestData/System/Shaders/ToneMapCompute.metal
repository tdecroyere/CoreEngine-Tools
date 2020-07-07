#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const texture2d<float, access::read> InputTexture [[id(0)]];
    const texture2d<float, access::write> OutputTexture [[id(1)]];
};

float3 ToneMapACES(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;

    return saturate((x * (a * x + b)) / ( x * ( c * x + d) + e));
}

kernel void ToneMap(uint2 pixelCoordinates [[thread_position_in_grid]],
                    const device ShaderParameters& shaderParameters)
{                
    float exposure = 0.03;

    float3 sample = shaderParameters.InputTexture.read(pixelCoordinates).rgb;

    sample = ToneMapACES(sample * exposure);

    // TODO: Do we need to convert from linear space to SRGB?
    shaderParameters.OutputTexture.write(float4(sample, 1.0), pixelCoordinates);
}