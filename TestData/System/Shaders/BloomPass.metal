#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const texture2d<float> InputTexture [[id(0)]];
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

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device ShaderParameters& shaderParameters)
{
    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear);

                                       
    PixelOutput output = {};
    float exposure = 0.1;

    float3 sample = shaderParameters.InputTexture.sample(texture_sampler, input.TextureCoordinates).rgb;

    sample *= exposure;

    if(dot(sample, 0.333f) <= 0.25f)
    {
        output.Color = 0;
    }

    else
    {
        output.Color = float4(sample, 1);
    }

    return output;
}