#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    float4 Position;
    float4 Color;
};

struct VertexOutput
{
    float4 Position [[position]];
    float4 Color;
};

struct CoreEngine_RenderPassConstantBuffer {
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct CoreEngine_ObjectConstantBuffer
{
    float4x4 WorldMatrix;
};

vertex VertexOutput VertexMain(uint vertexID [[vertex_id]], 
                                constant VertexInput* input [[buffer(0)]], 
                                constant CoreEngine_RenderPassConstantBuffer* renderPassParametersPointer [[buffer(1)]],
                                constant CoreEngine_ObjectConstantBuffer* objectParametersPointer [[buffer(2)]])
{
    VertexOutput output;

    CoreEngine_RenderPassConstantBuffer renderPassParameters = *renderPassParametersPointer;
    CoreEngine_ObjectConstantBuffer objectParameters = *objectParametersPointer;
    
    float4x4 worldViewProjMatrix = objectParameters.WorldMatrix * renderPassParameters.ViewMatrix * renderPassParameters.ProjectionMatrix;

    output.Position = input[vertexID].Position * worldViewProjMatrix;
    output.Color = input[vertexID].Color;
    
    return output;
}

fragment float4 PixelMain(VertexOutput input [[stage_in]])
{
    return input.Color;
}