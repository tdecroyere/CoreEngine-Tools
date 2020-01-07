#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    float3 Position;
    float3 Color;
};

struct VertexOutput
{
    float4 Position [[position]];
    float3 Color;
};

struct RenderPassParameters
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct ShaderParameters
{
    const device VertexInput* VertexBuffer                  [[id(0)]];
    const device RenderPassParameters& RenderPassParameters [[id(1)]];
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const device ShaderParameters& parameters)
{
    VertexInput input = parameters.VertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 viewProjMatrix = parameters.RenderPassParameters.ProjectionMatrix * parameters.RenderPassParameters.ViewMatrix;

    output.Position = viewProjMatrix * float4(input.Position, 1.0);
    output.Color = input.Color;
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

[[early_fragment_tests]]
fragment PixelOutput PixelMain(VertexOutput input [[stage_in]])
{
    PixelOutput output = {};

    output.Color = float4(input.Color, 1.0);

    return output;
}

