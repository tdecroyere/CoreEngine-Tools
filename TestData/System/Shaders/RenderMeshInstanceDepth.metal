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
    float2 TextureCoordinates;
};

struct BoundingBox
{
    float3 MinPoint;
    float3 MaxPoint;
};

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
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
    const array<const device VertexInput*, 10000> VertexBuffers [[id(3)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10003)]];
    const array<const device void*, 10000> MaterialData [[id(20003)]];
    const array<texture2d<float>, 10000> MaterialTextures [[id(30003)]];
    const device int* MaterialTextureOffsets [[id(40003)]];
    command_buffer OpaqueCommandBuffer [[id(40004)]];
    command_buffer TransparentCommandBuffer [[id(40005)]];
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
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

texture2d<float> GetMaterialTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.MaterialTextures[materialTextureOffset + (materialTextureIndex - 1)];
}

fragment void PixelMain(VertexOutput input [[stage_in]],
                        const device void* material [[buffer(0)]],
                        const device int& materialTextureOffset [[buffer(1)]],
                        const device ShaderParameters& shaderParameters [[buffer(2)]],
                        const device GeometryInstance& geometryInstance [[buffer(3)]])
{
    const device SimpleMaterial& simpleMaterial = *((const device SimpleMaterial*)material);

    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(4));

    float4 albedo = simpleMaterial.DiffuseColor.a > 0 ? simpleMaterial.DiffuseColor : float4(1, 1, 1, 1);

    if (simpleMaterial.DiffuseTexture > 0)
    {
        texture2d<float> diffuseTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.DiffuseTexture);
        float4 textureDiffuseColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);

        if (textureDiffuseColor.a == 1)
        {
            albedo = float4(textureDiffuseColor.rgb, albedo.a);
        }

        else
        {
            albedo = textureDiffuseColor;
        }
    }

    if (geometryInstance.IsTransparent == 1 && albedo.a < 1.0)
    {
        discard_fragment();
    }
}

