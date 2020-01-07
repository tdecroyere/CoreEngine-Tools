#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ShaderParameters
{
    const texture2d_ms<float> InputTexture [[id(0)]];
    const texture2d_ms<float> InputTransparentTexture [[id(1)]];
    const texture2d_ms<float> InputTransparentRevealageTexture [[id(2)]];
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

float MaxComponent(float3 v)
{
    return max(max(v.x, v.y), v.z);
};

float MinComponent(float3 v)
{
    return min(min(v.x, v.y), v.z);
};

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device ShaderParameters& shaderParameters)
{
    PixelOutput output = {};

    uint2 pixelCoordinates = uint2(input.TextureCoordinates.x * shaderParameters.InputTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTexture.get_height());  
    uint sampleCount = shaderParameters.InputTexture.get_num_samples();  

    float4 opaqueColor = 0;
    
    for(uint i = 0; i < sampleCount; i++) {  
        float4 sample = shaderParameters.InputTexture.read(pixelCoordinates, i);  
        opaqueColor += sample;  
    }  

    opaqueColor /= sampleCount;

    pixelCoordinates = uint2(input.TextureCoordinates.x * shaderParameters.InputTransparentRevealageTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTransparentRevealageTexture.get_height());  
    sampleCount = shaderParameters.InputTransparentRevealageTexture.get_num_samples();  

    float transparentRevealageColor = 0;
    
    for(uint i = 0; i < sampleCount; i++) {  
        float4 sample = shaderParameters.InputTransparentRevealageTexture.read(pixelCoordinates, i);  
        transparentRevealageColor += sample.r;  
    }  

    transparentRevealageColor /= sampleCount;

    float modulation = transparentRevealageColor;

    if (modulation == 1.0)
    {
        output.Color = float4(opaqueColor.rgb, 1.0);
        return output;
    }

    pixelCoordinates = uint2(input.TextureCoordinates.x * shaderParameters.InputTransparentTexture.get_width(), input.TextureCoordinates.y * shaderParameters.InputTransparentTexture.get_height());  
    sampleCount = shaderParameters.InputTransparentTexture.get_num_samples();  

    float4 transparentColor = 0;
    
    for(uint i = 0; i < sampleCount; i++) {  
        float4 sample = shaderParameters.InputTransparentTexture.read(pixelCoordinates, i);  
        transparentColor += sample;  
    }  

    transparentColor /= sampleCount;

    if (isinf(transparentColor.a))
    {
        transparentColor.a = MaxComponent(transparentColor.xyz);
    }

    if (isinf(MaxComponent(transparentColor.xyz)))
    {
        transparentColor = (float4)(1.0);
    }

    const float epsilon = 0.0010000000;

    // Self modulation
    transparentColor.rgb *= ((float3)(0.5) + (max(modulation, epsilon) / (float3)((2.0 * max(epsilon, modulation)))));

    output.Color = float4(opaqueColor.rgb * modulation + (transparentColor.rgb * (1 - modulation) / float3(max(transparentColor.a, 0.00001))), 1.0);
    //output.Color = float4(transparentColor.rgb / float3(max(transparentColor.a, 0.000010000000)), 1);
    //output.Color = float4(transparentRevealageColor, transparentRevealageColor, transparentRevealageColor, 1);
    return output; 
}