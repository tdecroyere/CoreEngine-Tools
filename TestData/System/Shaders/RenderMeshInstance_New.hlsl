#include "Common.hlsl"

#define RootSignatureDef RootSignatureDefinition(3)
#define WAVE_SIZE 32

struct ShaderParameters
{
    uint MeshInstancesBuffer;
    uint CamerasBuffer;

    uint MeshInstanceCount;

    // uint MeshletBuffer;
    // uint MeshBuffer;
    // 
};

// NEW STRUCTURE
struct Vertex
{
    float3 Position;
    float3 Normal;
    float2 TextureCoordinates;
};

struct Meshlet
{
    // TODO: Use the old fields for now
    uint VertexBufferIndex;
    uint IndexBufferIndex;
    uint StartIndex;
    uint VertexCount;
    uint IndexCount;

    // uint VertexCount;
    // uint VertexOffset;
    // uint PrimitiveCount;
    // uint PrimitiveOffset;
};

// TODO: Get rid of that and only use MeshletInstance?
struct Mesh
{
    uint MeshletCount;
    uint MeshletOffset;
};

struct MeshInstance
{
    float4x4 WorldMatrix;
    float4x4 WorldInvTransposeMatrix;
    uint MeshIndex;
    // BoundingBox WorldBoundingBox;
    // TODO: Material
};

// StructuredBuffer<MeshInstance> MeshInstances;
// StructuredBuffer<Meshlets> Meshlets;
// StructuredBuffer<uint3> PrimitiveIndices;
// StructuredBuffer<uint> VertexIndices;
// StructuredBuffer<Vertex> Vertices;


// OLD STRUCTURE
struct BoundingFrustum
{
    float4 LeftPlane;
    float4 RightPlane;
    float4 TopPlane;
    float4 BottomPlane;
    float4 NearPlane;
    float4 FarPlane;
};

struct Camera
{
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ViewProjectionMatrix;
    BoundingFrustum BoundingFrustum;
};

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct BoundingBox
{
    float3 MinPoint;
    float3 MaxPoint;
};

struct GeometryInstance
{
    uint GeometryPacketIndex;
    uint StartIndex;
    uint VertexCount;
    uint IndexCount;
    // uint MaterialIndex;
    float4x4 WorldMatrix;
    float4x4 WorldInvTransposeMatrix;
    BoundingBox WorldBoundingBox;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float3 WorldPosition: POSITION0;
    float3 WorldNormal: NORMAL0;
};

ConstantBuffer<ShaderParameters> parameters : register(b0);

[OutputTopology("triangle")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId : SV_GroupID, in uint groupThreadId : SV_GroupThreadID, out vertices VertexOutput vertices[128], out indices uint3 indices[128])
{
    const uint instanceCount = 1;

    ByteAddressBuffer geometryInstances = buffers[parameters.GeometryInstancesBuffer];
    GeometryInstance geometryInstance = geometryInstances.Load<GeometryInstance>(groupId * sizeof(GeometryInstance));

    ByteAddressBuffer geometryPackets = buffers[parameters.GeometryPacketsBuffer];
    GeometryPacket geometryPacket = geometryPackets.Load<GeometryPacket>(geometryInstance.GeometryPacketIndex * sizeof(GeometryPacket));

    uint indexCount = geometryInstance.IndexCount;

    const uint vertexCount = indexCount;
    const uint primitiveCount = indexCount / 3;

    SetMeshOutputCounts(vertexCount, primitiveCount);

    if (groupThreadId < vertexCount)
    {
        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        float4x4 worldViewProjMatrix = mul(geometryInstance.WorldMatrix, camera.ViewProjectionMatrix);

        ByteAddressBuffer indexBuffer = buffers[geometryPacket.IndexBufferIndex];
        uint index = indexBuffer.Load<uint>((geometryInstance.StartIndex + groupThreadId) * sizeof(uint));

        ByteAddressBuffer vertexBuffer = buffers[geometryPacket.VertexBufferIndex];
        Vertex vertex = vertexBuffer.Load<Vertex>(index * sizeof(Vertex));

        vertices[groupThreadId].Position = mul(float4(vertex.Position, 1), worldViewProjMatrix);
        vertices[groupThreadId].WorldNormal = mul(float4(vertex.Normal, 0), geometryInstance.WorldInvTransposeMatrix).xyz;
    }
  
    if (groupThreadId < primitiveCount)
    {
        // ByteAddressBuffer indexBuffer = buffers[geometryPacket.IndexBufferIndex];
        // indices[groupThreadId] = indexBuffer.Load<uint3>(groupThreadId * sizeof(uint3));
        indices[groupThreadId] = uint3(0, 1, 2) + groupThreadId * 3;
    }
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    output.Color = float4(normalize(input.WorldNormal) * 0.5 + 0.5, 1);

    return output; 
}