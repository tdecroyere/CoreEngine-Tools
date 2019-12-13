#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    float3 Position;
    float3 Normal;
};

struct VertexOutput
{
    float4 Position [[position]];
    float3 WorldNormal;
};

struct RenderPassParameters
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct ObjectProperties
{
    float4x4 WorldMatrix;
};

struct VertexShaderParameters
{
    uint objectPropertyIndex;
};

struct ShaderParameters
{
    const device VertexInput* VertexBuffer                  [[id(0)]];
    const device RenderPassParameters& RenderPassParameters [[id(1)]];
    const device ObjectProperties* ObjectProperties         [[id(2)]];
    const device VertexShaderParameters* VertexShaderParameters [[id(3)]];
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const uint instanceId [[instance_id]],
                               const device ShaderParameters& parameters)
{
    VertexInput input = parameters.VertexBuffer[vertexId];
    VertexOutput output = {};

    uint objectPropertyIndex = parameters.VertexShaderParameters[instanceId].objectPropertyIndex;

    float4x4 worldMatrix = parameters.ObjectProperties[objectPropertyIndex].WorldMatrix;
    float4x4 worldViewProjMatrix = (parameters.RenderPassParameters.ProjectionMatrix * parameters.RenderPassParameters.ViewMatrix) * worldMatrix;

    output.Position = worldViewProjMatrix * float4(input.Position, 1.0);
    output.WorldNormal = normalize(worldMatrix * float4(input.Normal, 0.0)).xyz;
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

fragment PixelOutput PixelMain(VertexOutput input [[stage_in]])
{
    PixelOutput output = {};

    output.Color = float4((input.WorldNormal * 0.5) + float3(0.5), 1.0);
    
    // float light = dot(normalize(float3(1, 1, 1)), input.WorldNormal);
    // output.Color = float4(light, light, light, 1.0);
    
    return output;
}

