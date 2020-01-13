#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    float4 Position [[position]];
    float3 WorldPosition;
    float3 WorldNormal;
    float3 Normal;
    float2 TextureCoordinates;
    float3 ViewDirection;
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
    output.WorldPosition = (worldMatrix * float4(input.Position, 1.0)).xyz;
    output.WorldNormal = float3(normalize(worldMatrix * float4(input.Normal, 0.0)).xyz);
    output.Normal = input.Normal;
    output.TextureCoordinates = input.TextureCoordinates;
    output.ViewDirection = camera.WorldPosition - output.WorldPosition;

    return output;
}

struct PixelOutput
{
    float4 AccumulationColor [[color(0)]];
    float4 CoverageColor [[color(1)]];
};

float WeightFunction(float alpha, float depth)
{
    float tmp = (1.0 - (depth * 0.99));
    (tmp *= ((tmp * tmp) * 10000.0));
    return clamp((alpha * tmp), 0.0010000000, 150.0);
}

[[early_fragment_tests]]
fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               const device void* material [[buffer(0)]],
                               const device int& materialTextureOffset [[buffer(1)]],
                               const device ShaderParameters& shaderParameters [[buffer(2)]],
                               const device GeometryInstance& geometryInstance [[buffer(3)]])
{
    PixelOutput output = {};
    
    MaterialData materialData = ProcessSimpleMaterial(input.WorldPosition, input.Normal, input.WorldNormal, input.ViewDirection, false, input.TextureCoordinates, material, materialTextureOffset, shaderParameters);

    if (geometryInstance.IsTransparent == 1)
    {
        float4 finalColor = float4(materialData.Albedo.rgb * ComputeLightContribution(materialData.Normal), materialData.Albedo.a);
        float3 premultipliedColor = finalColor.rgb * float3(finalColor.a);

        float d = (input.Position.z);// / input.Position.w);
        float coverage = finalColor.a;
        float w = WeightFunction(coverage, d);

        output.AccumulationColor = float4(premultipliedColor.rgb, coverage) * float4(w);
        output.CoverageColor = coverage;
    }

    return output;
}

