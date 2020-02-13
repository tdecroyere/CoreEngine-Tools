#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const texture2d<float> InputTexture [[id(0)]];
    const texture2d<float> BloomTexture [[id(1)]];
};

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const device ShaderParameters& shaderParameters)
{
    VertexOutput output = {};

    if ((vertexId) % 4 == 0)
    {
        output.Position = float4(-1, 1, 0, 1);
        output.TextureCoordinates = float2(0, 0);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.Position = float4(1, 1, 0, 1);
        output.TextureCoordinates = float2(1, 0);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.Position = float4(-1, -1, 0, 1);
        output.TextureCoordinates = float2(0, 1);
    }

    else if ((vertexId) % 4 == 3)
    {
        output.Position = float4(1, -1, 0, 1);
        output.TextureCoordinates = float2(1, 1);
    }
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

float3 ToneMapACES(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x*(a*x+b))/(x*(c*x+d)+e));
}

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device ShaderParameters& shaderParameters)
{
    constexpr sampler texture_sampler(mag_filter::nearest,
                                      min_filter::nearest,
                                      mip_filter::nearest);

    constexpr sampler texture_samplerLinear(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear);

                                       
    PixelOutput output = {};
    float exposure = 0.08;

    float3 sample = shaderParameters.InputTexture.sample(texture_sampler, input.TextureCoordinates).rgb;
    float3 bloomSample = shaderParameters.BloomTexture.sample(texture_samplerLinear, input.TextureCoordinates).rgb;

    //bloomSample *= 0.3;
    bloomSample *= 0.0;

    sample = ToneMapACES(sample * exposure);
    output.Color = float4(sample + bloomSample, 1);

    return output;
}