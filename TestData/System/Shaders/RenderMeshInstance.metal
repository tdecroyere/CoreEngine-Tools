#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    float3 Position;
    float3 Normal;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position [[position]];
    float3 WorldNormal;
    float2 TextureCoordinates;
};

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct GeometryInstance
{
    uint GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    float4x4 WorldMatrix;
};

struct Camera
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
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
    command_buffer CommandBuffer [[id(3)]];
    const array<const device VertexInput*, 10000> VertexBuffers [[id(4)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10004)]];
    const array<const device void*, 10000> MaterialData [[id(20004)]];
    const array<texture2d<float>, 10000> MaterialTextures [[id(30004)]];
    const device int* MaterialTextureOffsets [[id(40004)]];
};

struct SimpleMaterial
{
    float4 DiffuseColor;
    int DiffuseTexture;
    int NormalTexture;
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
    float4x4 worldViewProjMatrix = (camera.ProjectionMatrix * camera.ViewMatrix) * worldMatrix;

    output.Position = worldViewProjMatrix * float4(input.Position, 1.0);
    output.WorldNormal = normalize(worldMatrix * float4(input.Normal, 0.0)).xyz;
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

float4 ComputeLightContribution(float3 worldNormal)
{
    float light = saturate(dot(normalize(float3(-0.5, 0.5, 0.5)), worldNormal));
    return float4(light, light, light, 1.0) + float4(0.2, 0.2, 0.2, 1.0);
}

texture2d<float> GetMaterialTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.MaterialTextures[materialTextureOffset + (materialTextureIndex - 1)];
}

fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               const device SimpleMaterial& material [[buffer(0)]],
                               const device int& materialTextureOffset [[buffer(1)]],
                               const device ShaderParameters& shaderParameters [[buffer(2)]])
{
    PixelOutput output = {};

    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat);

    if (material.NormalTexture > 0)
    {
        texture2d<float> normalTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, material.NormalTexture);
        float4 textureColor = normalTexture.sample(texture_sampler, input.TextureCoordinates);

        output.Color = textureColor * ComputeLightContribution(input.WorldNormal);
    }

    else if (material.DiffuseTexture > 0)
    {
        texture2d<float> diffuseTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, material.DiffuseTexture);
        float4 textureColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);

        output.Color = textureColor * ComputeLightContribution(input.WorldNormal);
    }

    else if (material.DiffuseColor.a == 0)
    {
        output.Color = float4((input.WorldNormal * 0.5) + float3(0.5), 1.0);
    }

    else
    {
        output.Color = float4(material.DiffuseColor.xyz, 1) * ComputeLightContribution(input.WorldNormal);
    }
    
    return output;
}

