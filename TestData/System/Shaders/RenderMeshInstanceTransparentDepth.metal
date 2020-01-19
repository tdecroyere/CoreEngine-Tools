#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
    bool DepthOnly [[flat]];
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
    output.TextureCoordinates = input.TextureCoordinates;
    output.DepthOnly = camera.DepthOnly;

    return output;
}

fragment void PixelMain(VertexOutput input [[stage_in]],
                        const device Material& material [[buffer(0)]],
                        const device void* materialBufferData [[buffer(1)]],
                        const device ShaderParameters& shaderParameters [[buffer(2)]],
                        const device GeometryInstance& geometryInstance [[buffer(3)]])
{

    MaterialData materialData = ProcessSimpleMaterial(input.Position.xyz, float3(0), float3(0), float3(0), true, input.TextureCoordinates, materialBufferData, material.MaterialTextureOffset, shaderParameters);

    if ((input.DepthOnly && materialData.Albedo.a == 0.0) || (!input.DepthOnly && materialData.Albedo.a < 1.0))
    {
        discard_fragment();
    }
}

