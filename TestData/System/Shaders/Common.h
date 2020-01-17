#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    packed_float3 Position;
    packed_float3 Normal;
    packed_float2 TextureCoordinates;
};

struct BoundingBox
{
    packed_float3 MinPoint;
    packed_float3 MaxPoint;
};

struct BoundingFrustum
{
    float4 LeftPlane;
    float4 RightPlane;
    float4 TopPlane;
    float4 BottomPlane;
    float4 NearPlane;
    float4 FarPlane;
};

struct GeometryPacket
{
    int VertexBufferIndex;
    int IndexBufferIndex;
};

struct GeometryInstance
{
    int GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    int MaterialIndex;
    int IsTransparent;
    float4x4 WorldMatrix;
    BoundingBox WorldBoundingBox;
};

struct Camera
{
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
    BoundingFrustum BoundingFrustum;
};

struct Light
{
    float3 WorldSpacePosition;
    int CameraIndexes[5];
    int CommandListIndexes[5];
    int ShadowMapTextureIndexes[5];
    int CascadeCountToRemove;
};

struct SceneProperties
{
    int ActiveCameraIndex;
    int DebugCameraIndex;
    bool isDebugCameraActive;
};

struct ShaderParameters
{
    const device SceneProperties& SceneProperties [[id(0)]];
    const device Camera* Cameras [[id(1)]];
    const device Light* Lights [[id(2)]];
    const device GeometryPacket* GeometryPackets [[id(3)]];
    const device GeometryInstance* GeometryInstances [[id(4)]];
    const array<const device VertexInput*, 10000> VertexBuffers [[id(5)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10005)]];
    const array<const device void*, 10000> MaterialData [[id(20005)]];
    const array<texture2d<float>, 10000> Textures [[id(30005)]];
    const device int* MaterialTextureOffsets [[id(40005)]];
    command_buffer CameraCommandBuffer [[id(40006)]];
    const array<command_buffer, 100> LightCommandBuffers [[id(40007)]];
};

struct MaterialData
{
    float4 Albedo;
    float3 Normal;
};

texture2d<float> GetTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.Textures[materialTextureOffset + (materialTextureIndex - 1)];
}

float ComputeLightShadow(Light light, float3 normal, texture2d<float> shadowMap, float3 lightSpacePosition)
{
    constexpr sampler depthTextureSampler(mag_filter::nearest,
                                      min_filter::nearest,
                                      mip_filter::nearest, 
                                      address::clamp_to_border,
                                      border_color::opaque_white);
                                      
    float2 shadowUv = lightSpacePosition.xy * float2(0.5, -0.5) + 0.5;
    float shadowMapDepth = shadowMap.sample(depthTextureSampler, shadowUv).r;

    float minBias = 0.06;
    float maxBias = 0.6;

    //float bias = max(maxBias * (1.0 - dot(normal, normalize(light.Camera1.WorldPosition))), minBias);  
    float bias = max(maxBias * (1.0 - shadowMapDepth), minBias);  
    
    float lightSpaceDepth = lightSpacePosition.z - bias;
    //float lightSpaceDepth = lightSpacePosition.z - 0.8;

    return lightSpaceDepth < shadowMapDepth;// * (dot(normal, normalize(light.Camera1.WorldPosition)) > 0);
}

float3 ComputeLightContribution(Light light, texture2d<float> shadowMap, float3 lightSpacePosition, float3 worldNormal)
{
    float3 lightColor = float3(1, 1, 1);
    float lightShadow = 1.0;
    
    if (!is_null_texture(shadowMap))
    {
        lightShadow = ComputeLightShadow(light, worldNormal, shadowMap, lightSpacePosition);
    }

    float3 lightContribution = lightColor * saturate(dot(normalize(light.WorldSpacePosition), worldNormal));
    //return lightShadow;
    return lightContribution * lightShadow + float3(0.1, 0.1, 0.1);
}

// static float3 ToneMapACES(float3 x)
// {
//     float a = 2.51f;
//     float b = 0.03f;
//     float c = 2.43f;
//     float d = 0.59f;
//     float e = 0.14f;
//     return saturate((x*(a*x+b))/(x*(c*x+d)+e));
// }


float4 DebugAddCascadeColors(float4 fragmentColor, const device ShaderParameters& shaderParameters, Light light, float3 worldPosition)
{
    float4 cascadeColors[4] = 
    {
        float4(1, 0, 0, 1),
        float4(0, 1, 0, 1),
        float4(0, 0, 1, 1),
        float4(1, 1, 0, 1)
    };

    float4 cascadeColor = cascadeColors[3];

    for (int i = 0; i < 4; i++)
    {
        Camera lightCamera = shaderParameters.Cameras[light.CameraIndexes[i]];
        float4 rawPosition = ((lightCamera.ProjectionMatrix * lightCamera.ViewMatrix)) * float4(worldPosition, 1);
        float3 lightSpacePosition = rawPosition.xyz / rawPosition.w;

        if (all(lightSpacePosition.xyz < 1.0) && all(lightSpacePosition.xyz > float3(-1,-1,0)))
        {
            cascadeColor = cascadeColors[i];
            break;
        }
    }

    float alpha = 0.1;
    return cascadeColor * alpha + fragmentColor * (1 - alpha);
}

#include "Materials/SimpleMaterial.h"