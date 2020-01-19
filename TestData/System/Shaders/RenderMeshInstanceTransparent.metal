#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    uint InstanceId [[flat]];
    float4 Position [[position]];
    float3 WorldPosition;
    float3 WorldNormal;
    float3 ModelViewPosition;
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
    float4x4 viewProjectionMatrix = camera.ViewProjectionMatrix;

    output.InstanceId = instanceId;
    output.Position = viewProjectionMatrix * worldMatrix * float4(input.Position, 1.0);
    output.WorldPosition = (worldMatrix * float4(input.Position, 1.0)).xyz;
    output.ModelViewPosition = (camera.ViewMatrix * worldMatrix * float4(input.Position, 1.0)).xyz;
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
    
    MaterialData materialData = ProcessSimpleMaterial(input.WorldPosition, input.Normal, input.WorldNormal, input.ViewDirection, false, input.TextureCoordinates, materialBufferData, material.MaterialTextureOffset, shaderParameters);

    float3 lightSpacePosition;
    texture2d<float> lightShadowBuffer;

    for (int i = 0; i < 3; i++)
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

    float4 finalColor = float4(materialData.Albedo.rgb * ComputeLightContribution(light, lightShadowBuffer, lightSpacePosition, materialData.Normal), materialData.Albedo.a);
    float3 premultipliedColor = finalColor.rgb * float3(finalColor.a);

    float d = (input.Position.z);// / input.Position.w);
    float coverage = finalColor.a;
    float w = WeightFunction(coverage, d);

    output.AccumulationColor = float4(premultipliedColor.rgb, coverage) * float4(w);
    output.CoverageColor = coverage;

    return output;
}

