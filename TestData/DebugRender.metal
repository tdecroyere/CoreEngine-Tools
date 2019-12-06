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
    float3 Position [[attribute(0)]];
    float3 Color    [[attribute(1)]];
};

struct VertexOutput
{
    float4 Position [[position]];
    float3 Color;
};

vertex VertexOutput VertexMain(VertexInput input                                            [[stage_in]], 
                               const device RenderPassParameters& renderPassParameters    [[buffer(1)]])
{
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

