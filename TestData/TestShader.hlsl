#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct type_CoreEngine_RenderPassConstantBuffer
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct type_CoreEngine_ObjectConstantBuffer
{
    float4x4 WorldMatrix;
};

struct VertexMain_out
{
    float4 out_var_COLOR [[user(locn0)]];
    float4 gl_Position [[position]];
};

struct VertexMain_in
{
    float4 in_var_POSITION [[attribute(0)]];
    float4 in_var_TexCoord0 [[attribute(1)]];
};

vertex VertexMain_out VertexMain(VertexMain_in in [[stage_in]], constant type_CoreEngine_RenderPassConstantBuffer& CoreEngine_RenderPassConstantBuffer [[buffer(1)]], constant type_CoreEngine_ObjectConstantBuffer& CoreEngine_ObjectConstantBuffer [[buffer(2)]])
{
    VertexMain_out out = {};
    out.gl_Position = ((transpose(CoreEngine_RenderPassConstantBuffer.ProjectionMatrix) * transpose(CoreEngine_RenderPassConstantBuffer.ViewMatrix)) * transpose(CoreEngine_ObjectConstantBuffer.WorldMatrix)) * in.in_var_POSITION;
    out.out_var_COLOR = in.in_var_TexCoord0;
    return out;
}






struct PixelMain_out
{
    float4 out_var_SV_TARGET [[color(0)]];
};

struct PixelMain_in
{
    float4 in_var_COLOR [[user(locn0)]];
};

fragment PixelMain_out PixelMain(PixelMain_in in [[stage_in]], float4 gl_FragCoord [[position]])
{
    PixelMain_out out = {};
    out.out_var_SV_TARGET = float4(1, 1, 0, 1);
    return out;
}




// #include <metal_stdlib>
// #include <simd/simd.h>

// using namespace metal;

// struct VertexInput
// {
//     float4 Position;
//     float4 Color;
// };

// struct VertexOutput
// {
//     float4 Position [[position]];
//     float4 Color;
// };

// struct CoreEngine_RenderPassConstantBuffer {
//     float4x4 ViewMatrix;
//     float4x4 ProjectionMatrix;
// };

// struct CoreEngine_ObjectConstantBuffer
// {
//     float4x4 WorldMatrix;
// };

// vertex VertexOutput VertexMain(uint vertexID [[vertex_id]], 
//                                 constant VertexInput* input [[buffer(0)]], 
//                                 constant CoreEngine_RenderPassConstantBuffer* renderPassParametersPointer [[buffer(1)]],
//                                 constant CoreEngine_ObjectConstantBuffer* objectParametersPointer [[buffer(2)]])
// {
//     VertexOutput output;

//     CoreEngine_RenderPassConstantBuffer renderPassParameters = *renderPassParametersPointer;
//     CoreEngine_ObjectConstantBuffer objectParameters = *objectParametersPointer;
    
//     float4x4 worldViewProjMatrix = objectParameters.WorldMatrix * renderPassParameters.ViewMatrix * renderPassParameters.ProjectionMatrix;

//     output.Position = input[vertexID].Position * worldViewProjMatrix;
//     output.Color = input[vertexID].Color;
    
//     return output;
// }

// fragment float4 PixelMain(VertexOutput input [[stage_in]])
// {
//     return float4(1, 1, 0, 1);
//     //return input.Color;
// }