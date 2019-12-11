#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct RenderPassParameters
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

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

vertex VertexOutput VertexMain(const device VertexInput* vertexBuffer                  [[buffer(0)]], 
                               const device RenderPassParameters& renderPassParameters [[buffer(1)]],
                               uint vertexId                                           [[vertex_id]])
{
    VertexInput input = vertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 viewProjMatrix = renderPassParameters.ProjectionMatrix * renderPassParameters.ViewMatrix;

    output.Position = viewProjMatrix * float4(input.Position, 1.0);
    output.Color = input.Color;
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

fragment PixelOutput PixelMain(VertexOutput input [[stage_in]])
{
    PixelOutput output = {};

    output.Color = float4(input.Color, 1.0);

    return output;
}

