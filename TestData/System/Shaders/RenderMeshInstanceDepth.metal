#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

vertex float4 VertexMain(const uint vertexId [[vertex_id]],
                         const device VertexInput* vertexBuffer [[buffer(0)]],
                         constant Camera& camera [[buffer(1)]],
                         const device GeometryInstance& geometryInstance [[buffer(2)]])
{
    return camera.ViewProjectionMatrix * geometryInstance.WorldMatrix * float4(vertexBuffer[vertexId].Position, 1.0);
}