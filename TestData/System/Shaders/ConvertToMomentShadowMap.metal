#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const depth2d_ms<float> InputTexture [[id(0)]];
};

struct VertexOutput
{
    float4 Position [[position]];
    float2 TextureCoordinates;
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const device ShaderParameters& shaderParameters)
{
    VertexOutput output = {};

    if ((vertexId) % 4 == 0)
    {
        output.Position = float4(-1, 1, 0, 1);
        output.TextureCoordinates = float2(0, 0);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.Position = float4(1, 1, 0, 1);
        output.TextureCoordinates = float2(1, 0);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.Position = float4(-1, -1, 0, 1);
        output.TextureCoordinates = float2(0, 1);
    }

    else if ((vertexId) % 4 == 3)
    {
        output.Position = float4(1, -1, 0, 1);
        output.TextureCoordinates = float2(1, 1);
    }
    
    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

float4 GetOptimizedMoments(float depth)
{
    float square = depth * depth;
    float4 moments = float4(depth, square, square * depth, square * square);
    float4 optimized = float4x4(-2.07224649f, 13.7948857237f,  0.105877704f,   9.7924062118f,
                                              32.23703778f,  -59.4683975703f, -1.9077466311f, -33.7652110555f,
                                             -68.571074599f,  82.0359750338f,  9.3496555107f,  47.9456096605f,
                                              39.3703274134f,-35.364903257f,  -6.6543490743f, -23.9728048165f) * moments;
    optimized[0] += 0.035955884801f;
    return optimized;
}

// float ConvertDepthSampleToLinear(float depthSample, float nearPlane, float farPlane)
// {
//     float depthRange = farPlane - nearPlane;
//     float worldSpaceDepthSample = 2.0 * nearPlane * farPlane / (farPlane + nearPlane - depthSample * depthRange);
//     return ((worldSpaceDepthSample - nearPlane) / depthRange);
// }

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device ShaderParameters& shaderParameters)
{                                      
    PixelOutput output = {};

    uint2 pixel = uint2(input.TextureCoordinates.x * shaderParameters.InputTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTexture.get_height());  

    uint sampleCount = shaderParameters.InputTexture.get_num_samples();  
    float4 moments = 0.0;

    for (uint i = 0; i < sampleCount; i++) 
    {  
        float depth = shaderParameters.InputTexture.read(pixel, i);  
        depth = depth * 0.5 + 0.5;
        moments += GetOptimizedMoments(depth);
        //float square = depth * depth;
        //moments += float4(depth, square, square * depth, square * square);
    } 

    moments /= sampleCount;

    output.Color = moments;
    return output;
}