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

float4 ResolveTexturePixel(const device texture2d_ms<float>& texture, float2 textureCoordinates)
{
    uint2 pixelCoordinates = uint2(textureCoordinates.x * texture.get_width(), textureCoordinates.y * texture.get_height());  
    uint sampleCount = texture.get_num_samples();  

    float4 accumulatedColor = 0;
    
    for(uint i = 0; i < sampleCount; i++) 
    {  
        float4 sample = texture.read(pixelCoordinates, i);  
        accumulatedColor += float4(sample);
    }  

    return float4(accumulatedColor / sampleCount);
}

fragment PixelOutput PixelMain(const VertexOutput input [[stage_in]],
                               const device ShaderParameters& shaderParameters)
{
    PixelOutput output = {};

    float4 opaqueColor = ResolveTexturePixel(shaderParameters.InputTexture, input.TextureCoordinates);
    float modulation = ResolveTexturePixel(shaderParameters.InputTransparentRevealageTexture, input.TextureCoordinates).r;

    if (modulation == 1)
    {
        output.Color = opaqueColor;
        return output;
    }

    float4 transparentColor = ResolveTexturePixel(shaderParameters.InputTransparentTexture, input.TextureCoordinates);

    if (isinf(transparentColor.a))
    {
        transparentColor.a = MaxComponent(transparentColor.xyz);
    }

    if (isinf(MaxComponent(transparentColor.xyz)))
    {
        transparentColor = float4(1.0);
    }

    const float epsilon = 0.0010000000;

    // Self modulation
    transparentColor.rgb *= ((float3)(0.5) + (max(modulation, epsilon) / (float3)((2.0 * max(epsilon, modulation)))));

    output.Color = float4(opaqueColor.rgb * modulation + (transparentColor.rgb * (1 - modulation) / float3(max(transparentColor.a, 0.00001))), 1.0);
    //output.Color = half4(transparentColor.rgb, 1);
    //zoutput.Color = half4(modulation, modulation, modulation, 1);
    return output; 
}