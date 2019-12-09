#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct RenderPassParameters
{
    float4x4 ProjectionMatrix;
};

struct SurfaceProperties
{
    float4x4 WorldMatrix;
    uint TextureIndex;
};

struct InputParameters
{
    const device SurfaceProperties* SurfaceProperties;
    const array<texture2d<float>, 100> Textures;
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
                               uint instanceId [[instance_id]],
                               const device RenderPassParameters& renderPassParameters    [[buffer(1)]],
                               const device InputParameters& inputParameters    [[buffer(2)]])
{
    VertexOutput output = {};

    float4x4 worldMatrix = inputParameters.SurfaceProperties[instanceId].WorldMatrix;
    float4x4 projectionMatrix = renderPassParameters.ProjectionMatrix;

    output.Position = projectionMatrix * worldMatrix * float4(input.Position.xy, 0.0, 1.0);
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

