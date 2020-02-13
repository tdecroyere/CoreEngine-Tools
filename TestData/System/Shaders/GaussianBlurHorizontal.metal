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
    constexpr sampler texture_sampler(mag_filter::nearest,
                                      min_filter::nearest,
                                      mip_filter::nearest);

    int2 offset = int2(1, 0);
                                       
    PixelOutput output = {};

    uint2 pixel = uint2(input.TextureCoordinates.x * shaderParameters.InputTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTexture.get_height());  
    uint2 textureDim(shaderParameters.InputTexture.get_width(), shaderParameters.InputTexture.get_height());
    float3 outputColor = 0;

    int radius = 9;

    for(int i = -radius; i <= radius; ++i)
    {
        uint2 pixCoord = clamp(uint2(int2(pixel) + offset * i), uint2(0), textureDim);

        float3 sample = shaderParameters.InputTexture.read(pixCoord, 0).rgb;

        sample *= saturate((radius + 0.5f) - abs(i));
        outputColor += sample;
    }

    outputColor /= radius * 2;

    output.Color = float4(outputColor, 1);
    return output;
}