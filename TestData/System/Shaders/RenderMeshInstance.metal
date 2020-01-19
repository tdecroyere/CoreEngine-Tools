#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct VertexOutput
{
    uint InstanceId [[flat]];
    float4 Position [[position]];
    float3 WorldPosition;
    float3 ModelViewPosition;
    float3 WorldNormal;
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

    // output.OpaqueColor = float4(0, 1, 0, 1);
    // return output;
    
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

    float3 outputColor = materialData.Albedo.rgb * ComputeLightContribution(light, lightShadowBuffer, lightSpacePosition, materialData.Normal);
    //float3 outputColor = ComputeLightContribution(materialData.Normal);
    //float exposure = 1;//0.05;
    //outputColor = ToneMapACES(outputColor * exposure);


    output.OpaqueColor = float4(outputColor, 1);

    // TODO: Move all debug overlay to a debug shader
    output.OpaqueColor = DebugAddCascadeColors(output.OpaqueColor, shaderParameters, light, input.WorldPosition);

    //output.OpaqueColor = float4(materialData.Normal * 0.5 + 0.5, 1);
    //output.OpaqueColor = float4(input.LightPosition.zzz, 1);
    //output.OpaqueColor = float4(lightPosition2.zzz, 1);

    return output;
}

