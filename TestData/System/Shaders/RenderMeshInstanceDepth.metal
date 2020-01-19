#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    float4 Position [[position]];
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const device VertexInput* vertexBuffer [[buffer(0)]],
                               const device Camera& camera [[buffer(1)]],
                               const device GeometryInstance& geometryInstance [[buffer(2)]])
{
    VertexInput input = vertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 worldMatrix = geometryInstance.WorldMatrix;
    float4x4 viewProjectionMatrix = camera.ViewProjectionMatrix;
    float4x4 worldViewProjectionMatrix = viewProjectionMatrix * worldMatrix;

    output.Position = worldViewProjectionMatrix * float4(input.Position, 1.0);

    return output;
}
