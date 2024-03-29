#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinition(6)

struct ShaderParameters
{
    uint CamerasBuffer;
    uint ShowMeshlets;

    uint MeshBufferIndex;
    uint MeshInstanceBufferIndex;
    uint MeshletCount;
    uint MeshInstanceIndex;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float3 WorldPosition: POSITION0;
    float3 WorldNormal: NORMAL0;
    uint MeshletIndex : COLOR0;
};

struct Payload
{
    uint MeshletIndexes[WAVE_SIZE];
};

[[vk::push_constant]]
ConstantBuffer<ShaderParameters> parameters : register(b0);

groupshared Payload sharedPayload;

bool IsMeshletVisible(Meshlet meshlet, MeshInstance meshInstance, Camera camera)
{
    // TODO: Optimize the if branch for wave operations

    float3 cameraPosition = camera.WorldPosition;
        
    if (parameters.ShowMeshlets)
    {
        cameraPosition = -camera.WorldPosition;
    }

    BoundingSphere boundingSphere;
    boundingSphere.Center = mul(float4(meshlet.BoundingSphere.Center, 1.0), meshInstance.WorldMatrix);
    boundingSphere.Radius = meshlet.BoundingSphere.Radius * meshInstance.Scale;

    if (!IntersectFrustum(camera.BoundingFrustum, boundingSphere))
    {
        return false;
    }
    
    float4 cone = unpack_s8s32(meshlet.PackedCone) / 127.0;

    if (cone.w == 1.0)
    {
        return true;
    }

    float4 normalCone = float4(normalize(mul(float4(cone.xyz, 0), meshInstance.WorldMatrix).xyz), cone.w);
    return IntersectCone(normalCone, boundingSphere, cameraPosition);
}

[NumThreads(WAVE_SIZE, 1, 1)]
void AmplificationMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    uint meshInstanceIndex = parameters.MeshInstanceIndex;

    ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
    MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

    uint meshIndex = meshInstance.MeshIndex;
    uint meshletIndex = groupId * WAVE_SIZE + groupThreadId;
    bool isMeshletVisible = false;

    if (meshletIndex < parameters.MeshletCount)
    {
        ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
        Mesh mesh = meshBuffer.Load<Mesh>(meshIndex * sizeof(Mesh));

        ByteAddressBuffer meshletBuffer = buffers[mesh.MeshletBufferIndex];
        Meshlet meshlet = meshletBuffer.Load<Meshlet>(meshletIndex * sizeof(Meshlet));

        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        isMeshletVisible = IsMeshletVisible(meshlet, meshInstance, camera);
    }

    if (isMeshletVisible)
    {
        uint laneIndex = WavePrefixCountBits(isMeshletVisible);
        sharedPayload.MeshletIndexes[laneIndex] = meshletIndex;
    }

    uint meshletCount = WaveActiveCountBits(isMeshletVisible);
    DispatchMesh(meshletCount, 1, 1, sharedPayload);
}

[OutputTopology("triangle")]
[NumThreads(WAVE_SIZE, 1, 1)]
void MeshMain(in uint groupId: SV_GroupID, 
              in uint groupThreadId : SV_GroupThreadID, 
              in payload Payload payload, 
              out vertices VertexOutput vertices[64], 
              out indices uint3 indices[126])
{
    uint meshInstanceIndex = parameters.MeshInstanceIndex;

    ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
    MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

    uint meshIndex = meshInstance.MeshIndex;
    uint meshletIndex = payload.MeshletIndexes[groupId];

    ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
    Mesh mesh = meshBuffer.Load<Mesh>(meshIndex * sizeof(Mesh));

    ByteAddressBuffer meshletBuffer = buffers[mesh.MeshletBufferIndex];
    Meshlet meshlet = meshletBuffer.Load<Meshlet>(meshletIndex * sizeof(Meshlet));

    SetMeshOutputCounts(meshlet.VertexCount, meshlet.TriangleCount);
    
    if (groupThreadId < meshlet.VertexCount)
    {
        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        ByteAddressBuffer vertexIndicesBuffer = buffers[mesh.VertexIndicesBufferIndex];
        ByteAddressBuffer verticesBuffer = buffers[mesh.VerticesBufferIndex];

        for (uint i = groupThreadId; i < meshlet.VertexCount; i += WAVE_SIZE)
        {
            uint vertexIndex = vertexIndicesBuffer.Load<uint>((meshlet.VertexOffset + i) * sizeof(uint));
            Vertex vertex = verticesBuffer.Load<Vertex>(vertexIndex * sizeof(Vertex));
 
            float3 worldPosition = mul(float4(vertex.Position, 1), meshInstance.WorldMatrix);

            vertices[i].Position = mul(float4(worldPosition, 1), camera.ViewProjectionMatrix);
            //vertices[i].WorldNormal = mul(meshlet.BoundingSphere.Center, meshInstance.WorldInvTransposeMatrix);
            vertices[i].WorldNormal = mul(float4(vertex.Normal, 0), meshInstance.WorldMatrix).xyz;
            //vertices[i].MeshletIndex = meshInstanceIndex;
            vertices[i].MeshletIndex = meshletIndex;
        }
    }
  
    if (groupThreadId < meshlet.TriangleCount)
    {
        ByteAddressBuffer triangleIndicesBuffer = buffers[mesh.TriangleIndicesBufferIndex];

        for (uint i = groupThreadId; i < meshlet.TriangleCount; i += WAVE_SIZE)
        {
            uint packedIndex = triangleIndicesBuffer.Load<uint>((meshlet.TriangleOffset + i) * sizeof(uint));
            indices[i] = unpack_u8u32(packedIndex).xyz;
        }
    }
}

float4 PixelMain(const VertexOutput input) : SV_TARGET0
{
    if (parameters.ShowMeshlets)
    {
        uint hashResult = hash(input.MeshletIndex);
	    float3 meshletColor = float3(float(hashResult & 255), float((hashResult >> 8) & 255), float((hashResult >> 16) & 255)) / 255.0;

        return float4(meshletColor, 1);
    }

    else
    {
        return float4(normalize(input.WorldNormal) * 0.5 + 0.5, 1);
    }
}