#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    float4 Position [[position]];
    float3 WorldPosition;
    float3 WorldNormal;
    float2 TextureCoordinates;
    float3 ViewDirection;
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const device VertexInput* vertexBuffer [[buffer(0)]],
                               constant Camera& camera [[buffer(1)]],
                               const device GeometryInstance& geometryInstance [[buffer(2)]])
{
    const device VertexInput& input = vertexBuffer[vertexId];
    VertexOutput output = {};

    output.Position = camera.ViewProjectionMatrix * geometryInstance.WorldMatrix * float4(input.Position, 1.0);
    output.WorldPosition = (geometryInstance.WorldMatrix * float4(input.Position, 1.0)).xyz;
    output.WorldNormal = float3(normalize(geometryInstance.WorldMatrix * float4(input.Normal, 0.0)).xyz);
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
    (tmp *= ((tmp * tmp) * 1000.0));
    return clamp((alpha * tmp), 0.0010000000, 150.0);

    // return pow(alpha, 1.0) * clamp(0.3 / (1e-5 + pow(depth / 1000, 4.0)), 1e-2, 3e3);
}

[[early_fragment_tests]]
fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               const device Material& material [[buffer(0)]],
                               const device void* materialBufferData [[buffer(1)]],
                               const device ShaderParameters& shaderParameters [[buffer(2)]],
                               const device GeometryInstance& geometryInstance [[buffer(3)]],
                               const device Light& light [[buffer(4)]])
{
    PixelOutput output = {};
    
    MaterialData materialData = ProcessSimpleMaterial(input.WorldPosition, input.WorldNormal, input.ViewDirection, false, input.TextureCoordinates, materialBufferData, material.MaterialTextureOffset, shaderParameters);

    float3 lightSpacePosition;
    texture2d<float> lightShadowBuffer;

    for (int i = 0; i < 4; i++)
    {
        Camera lightCamera = shaderParameters.Cameras[light.CameraIndexes[i]];
        float4 rawPosition = lightCamera.ViewProjectionMatrix * float4(input.WorldPosition, 1);
        lightSpacePosition = rawPosition.xyz / rawPosition.w;

        if (all(lightSpacePosition.xyz < 1.0) && all(lightSpacePosition.xyz > float3(-1,-1,0)))
        {
            lightShadowBuffer = shaderParameters.Textures[lightCamera.DepthBufferTextureIndex];
            break;
        }
    }

    float4 finalColor = float4(ComputeLightContribution(light, materialData, lightShadowBuffer, shaderParameters.CubeTextures[0], shaderParameters.CubeTextures[1], lightSpacePosition, normalize(input.ViewDirection)), materialData.Alpha);   
    float3 premultipliedColor = finalColor.rgb * float3(finalColor.a);

    float d = (input.Position.z);// / input.Position.w);
    float coverage = finalColor.a;
    float w = WeightFunction(coverage, d);

    output.AccumulationColor = float4(premultipliedColor.rgb, coverage) * float4(w);
    output.CoverageColor = coverage;

    return output;
}

