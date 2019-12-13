#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

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

struct RenderPassParameters
{
    float4x4 ProjectionMatrix;
};

struct RectangleSurface
{
    float4x4 WorldMatrix;
    int TextureIndex;
};

struct ShaderParameters
{
    const device VertexInput* VertexBuffer                  [[id(0)]];
    const device RenderPassParameters& RenderPassParameters [[id(1)]];
    const device RectangleSurface* RectangleSurfaces        [[id(2)]];
    array<texture2d<float>, 100> SurfaceTextures            [[id(3)]];
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const uint instanceId [[instance_id]],
                               const device ShaderParameters& parameters)
{
    VertexInput input = parameters.VertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 worldMatrix = parameters.RectangleSurfaces[instanceId].WorldMatrix;
    float4x4 projectionMatrix = parameters.RenderPassParameters.ProjectionMatrix;

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
                               const device ShaderParameters& shaderParameters)
{
    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear);
                                       
    PixelOutput output = {};

    int textureIndex = shaderParameters.RectangleSurfaces[input.InstanceId].TextureIndex;
    texture2d<float> diffuseTexture = shaderParameters.SurfaceTextures[textureIndex];

    float4 textureColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);
    output.Color = textureColor;

    return output; 
}