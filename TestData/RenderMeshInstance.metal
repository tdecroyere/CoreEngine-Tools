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

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct GeometryInstance
{
    uint GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    float4x4 WorldMatrix;
};

struct Camera
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const uint instanceId [[instance_id]],
                               const device VertexInput* vertexBuffer [[buffer(0)]],
                               const device Camera& camera [[buffer(1)]],
                               const device GeometryInstance& geometryInstance [[buffer(2)]])
{
    VertexInput input = vertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 worldMatrix = geometryInstance.WorldMatrix;
    float4x4 worldViewProjMatrix = (camera.ProjectionMatrix * camera.ViewMatrix) * worldMatrix;

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

