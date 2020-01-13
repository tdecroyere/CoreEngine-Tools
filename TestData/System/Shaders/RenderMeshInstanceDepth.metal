#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
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
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

fragment void PixelMain(VertexOutput input [[stage_in]],
                        const device void* material [[buffer(0)]],
                        const device int& materialTextureOffset [[buffer(1)]],
                        const device ShaderParameters& shaderParameters [[buffer(2)]],
                        const device GeometryInstance& geometryInstance [[buffer(3)]])
{
    MaterialData materialData = ProcessSimpleMaterial(input.Position.xyz, float3(0), float3(0), float3(0), true, input.TextureCoordinates, material, materialTextureOffset, shaderParameters);

    if (geometryInstance.IsTransparent == 1 && materialData.Albedo.a < 1.0)
    {
        discard_fragment();
    }
}

