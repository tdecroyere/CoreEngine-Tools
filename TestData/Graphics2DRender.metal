#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct RenderPassParameters
{
    float4x4 ProjectionMatrix;
};

struct VertexInput
{
    float3 Position             [[attribute(0)]];
    float3 TextureCoordinates   [[attribute(1)]];
};

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
};

vertex VertexOutput VertexMain(VertexInput input [[stage_in]], 
                               const device RenderPassParameters& renderPassParameters    [[buffer(1)]])
{
    VertexOutput output = {};

    float4x4 viewProjMatrix = renderPassParameters.ProjectionMatrix;

    output.Position = viewProjMatrix * float4(input.Position.xy, 0.0, 1.0);
    output.TextureCoordinates = input.TextureCoordinates.xy;
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               texture2d<float> colorTexture [[ texture(1) ]])
{
    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear);
                                       
    PixelOutput output = {};

    float4 textureColor = colorTexture.sample(texture_sampler, input.TextureCoordinates);
    output.Color = textureColor;

    return output; 
}

