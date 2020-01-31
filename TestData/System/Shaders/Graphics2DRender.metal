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
    bool IsOpaque [[flat]];
};

struct RenderPassParameters
{
    float4x4 ProjectionMatrix;
};

struct RectangleSurface
{
    float4x4 WorldMatrix;
    float2 TextureMinPoint;
    float2 TextureMaxPoint;
    int TextureIndex;
    bool IsOpaque;
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
    output.InstanceId = instanceId;

    float2 minPoint = parameters.RectangleSurfaces[instanceId].TextureMinPoint;
    float2 maxPoint = parameters.RectangleSurfaces[instanceId].TextureMaxPoint;

    if ((vertexId) % 4 == 0)
    {
        output.TextureCoordinates = float2(minPoint.x, minPoint.y);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.TextureCoordinates = float2(maxPoint.x, minPoint.y);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.TextureCoordinates = float2(minPoint.x, maxPoint.y);
    }

    else if ((vertexId) % 4 == 3)
    {
        output.TextureCoordinates = float2(maxPoint.x, maxPoint.y);
    }

    output.IsOpaque = parameters.RectangleSurfaces[instanceId].IsOpaque;
    
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

    if (!input.IsOpaque)
    {
        if (textureColor.a == 0)
        {
            discard_fragment();
        }
        
        output.Color = textureColor;
    }

    else
    {
        output.Color = float4(textureColor.rgb, 1);
    }

    return output; 
}