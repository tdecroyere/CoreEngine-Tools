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
    float4 OpaqueColor [[color(0)]];
};

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
    Camera lightCamera;

    for (int i = 0; i < 4; i++)
    {
        lightCamera = shaderParameters.Cameras[light.CameraIndexes[i]];
        lightSpacePosition = (lightCamera.ViewProjectionMatrix * float4(input.WorldPosition, 1)).xyz;

        if (all(lightSpacePosition.xyz < 1.0) && all(lightSpacePosition.xyz > float3(-1,-1,0)))
        {
            lightShadowBuffer = shaderParameters.Textures[lightCamera.MomentShadowMapTextureIndex];
            break;
        }
    }

    float3 outputColor = ComputeLightContribution(light, lightCamera, materialData, lightShadowBuffer, shaderParameters.CubeTextures[0], shaderParameters.CubeTextures[1], lightSpacePosition, normalize(input.ViewDirection));

    output.OpaqueColor = float4(outputColor, 1);

    // TODO: Move all debug overlay to a debug shader
    //output.OpaqueColor = DebugAddCascadeColors(output.OpaqueColor, shaderParameters, light, input.WorldPosition);

    //output.OpaqueColor = float4(materialData.Occlusion,materialData.Occlusion,materialData.Occlusion, 1);
    //output.OpaqueColor = float4(materialData.Normal * 0.5 + 0.5, 1);
    //output.OpaqueColor = float4(materialData.Roughness, materialData.Roughness, materialData.Roughness, 1);
    //output.OpaqueColor = float4(input.TextureCoordinates.rgr, 1);

    return output;
}

