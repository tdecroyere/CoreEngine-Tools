#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const texture2d<float> InputTexture [[id(0)]];
    const texture2d<float, access::write> OutputTexture [[id(1)]];
};

kernel void DirectTransfer(uint2 pixelCoordinates [[thread_position_in_grid]],
                           const device ShaderParameters& shaderParameters)
{
    shaderParameters.OutputTexture.write(float4(0, 0, 1, 1), pixelCoordinates);
}

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
                                       
    PixelOutput output = {};

    uint2 pixel = uint2(input.TextureCoordinates.x * shaderParameters.InputTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTexture.get_height());  

    float4 inputColor = shaderParameters.InputTexture.read(pixel, 0);
    output.Color = inputColor;
    //output.Color = float4(1, 1, 0, 0);
    return output; 
}