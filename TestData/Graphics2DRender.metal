#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct RenderPassParameters
{
    float4x4 ProjectionMatrix;
};

struct RectangleSurface
{
    float4x4 WorldMatrix;
    texture2d<float> Texture;
};

struct VertexInput
{
    float2 Position;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
    uint InstanceId [[flat]];
};

vertex VertexOutput VertexMain(const device VertexInput* vertexBuffer                  [[buffer(0)]], 
                               const device RenderPassParameters& renderPassParameters [[buffer(1)]],
                               const device RectangleSurface* rectangleSurfaces        [[buffer(2)]],
                               const uint vertexId                                     [[vertex_id]],
                               const uint instanceId                                   [[instance_id]])
{
    VertexInput input = vertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 worldMatrix = rectangleSurfaces[instanceId].WorldMatrix;
    float4x4 projectionMatrix = renderPassParameters.ProjectionMatrix;

    output.Position = projectionMatrix * worldMatrix * float4(input.Position, 0.0, 1.0);
    output.TextureCoordinates = input.TextureCoordinates;
    output.InstanceId = instanceId;
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device RectangleSurface* rectangleSurfaces [[buffer(1)]])
{
    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear);
                                       
    PixelOutput output = {};

    texture2d<float> diffuseTexture = rectangleSurfaces[input.InstanceId].Texture;

    float4 textureColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);
    output.Color = textureColor;

    return output; 
}

