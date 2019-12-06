#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

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

struct ArgumentBuffer
{
    const device RenderPassParameters* renderPassParameters;
    const device ObjectProperties* objectProperties;
    const device VertexShaderParameters* vertexShaderParameters;
};

struct VertexInput
{
    float3 Position [[attribute(0)]];
    float3 Normal   [[attribute(1)]];
};

struct VertexOutput
{
    float4 Position     [[position]];
    float3 WorldNormal;
};

vertex VertexOutput VertexMain(VertexInput input                            [[stage_in]], 
                               const device ArgumentBuffer& argumentBuffer  [[buffer(1)]], 
                               uint instanceId                              [[instance_id]])
{
    VertexOutput output = {};

    uint objectPropertyIndex = argumentBuffer.vertexShaderParameters[instanceId].objectPropertyIndex;

    float4x4 worldMatrix = argumentBuffer.objectProperties[objectPropertyIndex].WorldMatrix;
    float4x4 worldViewProjMatrix = (argumentBuffer.renderPassParameters->ProjectionMatrix * argumentBuffer.renderPassParameters->ViewMatrix) * worldMatrix;

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

