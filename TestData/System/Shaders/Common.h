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
    float3 MinPoint;
    float3 MaxPoint;
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

struct SceneProperties
{
    Camera ActiveCamera;
    Camera DebugCamera;
    bool isDebugCameraActive;
};

struct ShaderParameters
{
    const device SceneProperties& SceneProperties [[id(0)]];
    const device GeometryPacket* GeometryPackets [[id(1)]];
    const device GeometryInstance* GeometryInstances [[id(2)]];
    const array<const device VertexInput*, 10000> VertexBuffers [[id(3)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10003)]];
    const array<const device void*, 10000> MaterialData [[id(20003)]];
    const array<texture2d<float>, 10000> MaterialTextures [[id(30003)]];
    const device int* MaterialTextureOffsets [[id(40003)]];
    command_buffer OpaqueCommandBuffer [[id(40004)]];
};

struct MaterialData
{
    float4 Albedo;
    float3 Normal;
};

texture2d<float> GetMaterialTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.MaterialTextures[materialTextureOffset + (materialTextureIndex - 1)];
}

float3 ComputeLightContribution(float3 worldNormal)
{
    // float3 light = float3(3, 3, 3) * saturate(dot(normalize(float3(-0.5, 0.5, 0.5)), worldNormal));
    float3 light = float3(1, 1, 1) * saturate(dot(normalize(float3(-0.5, 0.5, 0.5)), worldNormal));
    return light + float3(0.1, 0.1, 0.1);
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

#include "Materials/SimpleMaterial.h"