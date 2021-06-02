#include "Common.hlsl"

#define RootSignatureDef RootSignatureDefinition(6)
#define WAVE_SIZE 32

struct ShaderParameters
{
    uint CamerasBuffer;
    uint ShowMeshlets;

    uint MeshBufferIndex;
    uint MeshletBufferIndex;
    uint MeshInstanceBufferIndex;
    uint MeshInstanceCount;
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

struct BoundingBox
{
    float3 MinPoint;
    float3 MaxPoint;
};

bool Intersect(float4 plane, BoundingBox box)
{
    if (dot(plane, float4(box.MinPoint.x, box.MinPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MinPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MaxPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MaxPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MinPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MinPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MaxPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MaxPoint.y, box.MaxPoint.z, 1)) <= 0) return true;

    return false;
}

bool Intersect(BoundingFrustum frustum, BoundingBox box)
{
    if (!Intersect(frustum.LeftPlane, box)) return false;
    if (!Intersect(frustum.RightPlane, box)) return false;
    if (!Intersect(frustum.TopPlane, box)) return false;
    if (!Intersect(frustum.BottomPlane, box)) return false;
    if (!Intersect(frustum.NearPlane, box)) return false;
    if (!Intersect(frustum.FarPlane, box)) return false;

    return true;
}

struct Vertex
{
    float3 Position;
    float3 Normal;
    float2 TextureCoordinates;
};


// TODO: Get rid of that and only use MeshletInstance?
struct Mesh
{
    uint MeshletCount;
    uint MeshletOffset;
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

struct MeshInstance
{
    uint MeshIndex;
    float4x3 WorldMatrix;
    float3x3 WorldInvTransposeMatrix;
    BoundingBox WorldBoundingBox;
    // TODO: Material
};

struct Camera
{
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ViewProjectionMatrix;
    BoundingFrustum BoundingFrustum;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float3 WorldPosition: POSITION0;
    float3 WorldNormal: NORMAL0;
    uint   MeshletIndex : COLOR0;
};

struct Payload
{
    uint MeshCount;
    uint MeshInstanceList[WAVE_SIZE];
    uint MeshInstanceCounts[WAVE_SIZE];
    uint MeshMapping[WAVE_SIZE];
    uint MeshInstanceOffsets[WAVE_SIZE + 1];
    uint GroupOffsets[WAVE_SIZE + 1];
};

ConstantBuffer<ShaderParameters> parameters : register(b0);

groupshared Payload sharedPayload;

groupshared uint sharedMeshInstanceList[WAVE_SIZE];
groupshared uint sharedMeshInstanceCounts[WAVE_SIZE];
groupshared uint sharedMeshMapping[WAVE_SIZE];
groupshared uint sharedMeshInstanceOffsets[WAVE_SIZE + 1];
groupshared uint sharedGroupOffsets[WAVE_SIZE + 1];

[NumThreads(WAVE_SIZE, 1, 1)]
void AmplificationMain(uint threadId : SV_DispatchThreadID, in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    // TODO: Replace WAVE_SIZE with getLaneCount()

    // Zero out groupshared memory which requires it.
    sharedMeshInstanceList[groupThreadId] = 0;

    if (groupThreadId == 0)
    {
        sharedMeshInstanceOffsets[0] = 0;
        sharedGroupOffsets[0] = 0;
    }

    if (groupThreadId < WAVE_SIZE)
    {
        sharedMeshInstanceCounts[groupThreadId] = 0;
        sharedMeshMapping[groupThreadId] = 0;
    }

    uint meshInstanceIndex = threadId;

    uint meshCount = 0;
    uint meshIndex = WAVE_SIZE;

    if (meshInstanceIndex < parameters.MeshInstanceCount)
    {
        ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
        MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        uint meshInstanceOffset = 0;

        // TODO: Implement meshinstance culling
        if (Intersect(camera.BoundingFrustum, meshInstance.WorldBoundingBox))
        {
            uint absoluteMeshIndex = meshInstance.MeshIndex;

            // Construct an orderer local list of mesh mapping
            bool meshInstanceProcessed = false;

            while (meshInstanceOffset < WAVE_SIZE && !meshInstanceProcessed)
            {
                uint minMeshIndex = WaveActiveMin(absoluteMeshIndex);

                if (WaveIsFirstLane())
                {
                    sharedMeshMapping[meshInstanceOffset] = minMeshIndex;
                }

                if (minMeshIndex == absoluteMeshIndex)
                {
                    meshIndex = meshInstanceOffset;
                    meshInstanceProcessed = true;
                }
                
                meshInstanceOffset++;
            }
        }

        meshCount = WaveActiveMax(meshInstanceOffset);
    }

    // TODO: For now we use directly the mesh index but later
    // We need a mapping table with local mesh index and the real one
    uint meshLaneOffset = 0;

    for (uint i = 0; i < meshCount; i++)
    {
        bool meshMatch = (meshIndex == i);

        if (meshMatch)
        {
            meshLaneOffset = WavePrefixCountBits(meshMatch);
        }

        sharedMeshInstanceCounts[i] = WaveActiveCountBits(meshMatch);
    }

    // Compute instance and mesh offsets separatly
    if (groupThreadId < meshCount)
    {
        uint meshInstanceCount = sharedMeshInstanceCounts[groupThreadId];

        ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
        Mesh mesh = meshBuffer.Load<Mesh>(sharedMeshMapping[groupThreadId] * sizeof(Mesh));

        sharedMeshInstanceOffsets[groupThreadId + 1] = meshInstanceCount;

        // TODO: Implement packing

        sharedGroupOffsets[groupThreadId + 1] = mesh.MeshletCount * meshInstanceCount;
    }

    // Transform the local offsets to absolute offsets (accumulate individual counts from left)
    if (groupThreadId <= meshCount)
    {
        uint instanceCount = sharedMeshInstanceOffsets[groupThreadId];
        sharedMeshInstanceOffsets[groupThreadId] = instanceCount + WavePrefixSum(instanceCount);

        uint meshletCount = sharedGroupOffsets[groupThreadId];
        sharedGroupOffsets[groupThreadId] = meshletCount + WavePrefixSum(meshletCount);
    }

    if (meshIndex != WAVE_SIZE)
    {
        uint startMeshInstanceOffset = sharedMeshInstanceOffsets[meshIndex];
        sharedMeshInstanceList[startMeshInstanceOffset + meshLaneOffset] = meshInstanceIndex;
    }

    // Copy groupshared memory to payload
    // TODO: Can't we directly fill the payload shared struct?
    // Needs perf measurement
    
    if (groupThreadId == 0)
    {
        sharedPayload.MeshCount = meshCount;
    }

    sharedPayload.MeshInstanceList[groupThreadId] = sharedMeshInstanceList[groupThreadId];

    if (groupThreadId < meshCount)
    {
        sharedPayload.MeshInstanceCounts[groupThreadId] = sharedMeshInstanceCounts[groupThreadId];
        sharedPayload.MeshMapping[groupThreadId] = sharedMeshMapping[groupThreadId];
    }

    if (groupThreadId <= meshCount)
    {
        sharedPayload.GroupOffsets[groupThreadId] = sharedGroupOffsets[groupThreadId];
        sharedPayload.MeshInstanceOffsets[groupThreadId] = sharedMeshInstanceOffsets[groupThreadId];
    }

    DispatchMesh(sharedGroupOffsets[meshCount], 1, 1, sharedPayload);
}

[OutputTopology("triangle")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId : SV_GroupID, 
              in uint groupThreadId : SV_GroupThreadID, 
              in payload Payload payload, 
              out vertices VertexOutput vertices[128], 
              out indices uint3 indices[128])
{
    uint meshCount = payload.MeshCount;

    // Find the LOD to which this threadgroup is assigned.
    // Each wave does this independently to avoid groupshared memory & sync.
    uint offsetCheck = 0;
    uint laneIndex = groupThreadId % WaveGetLaneCount();

    if (laneIndex < meshCount)
    {
        offsetCheck = WaveActiveCountBits(groupId >= payload.GroupOffsets[laneIndex]) - 1;
    }

    uint meshIndex = WaveReadLaneFirst(offsetCheck);
    uint mappedMeshIndex = payload.MeshMapping[meshIndex];

    // Load our LOD meshlet offset & LOD instance count
    uint meshOffset = payload.GroupOffsets[meshIndex];
    uint meshInstanceCount = payload.MeshInstanceCounts[meshIndex];

    // Calculate and load our meshlet.
    uint meshletIndex = (groupId - meshOffset) / meshInstanceCount;

    ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
    Mesh mesh = meshBuffer.Load<Mesh>(mappedMeshIndex * sizeof(Mesh));

    ByteAddressBuffer meshletBuffer = buffers[parameters.MeshletBufferIndex];
    Meshlet meshlet = meshletBuffer.Load<Meshlet>((mesh.MeshletOffset + meshletIndex) * sizeof(Meshlet));

    uint indexCount = meshlet.IndexCount;

    const uint vertexCount = indexCount;
    const uint primitiveCount = indexCount / 3;

    SetMeshOutputCounts(vertexCount, primitiveCount);

    if (groupThreadId < vertexCount)
    {
        uint lodInstance = (groupId - meshOffset) % meshInstanceCount;           // Instance index into this LOD level's instances
        uint meshInstanceOffset = payload.MeshInstanceOffsets[meshIndex] + lodInstance;  // Instance index into the payload instance list
        uint meshInstanceIndex = payload.MeshInstanceList[meshInstanceOffset];

        ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
        MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        ByteAddressBuffer indexBuffer = buffers[meshlet.IndexBufferIndex];
        uint index = indexBuffer.Load<uint>((meshlet.StartIndex + groupThreadId) * sizeof(uint));

        ByteAddressBuffer vertexBuffer = buffers[meshlet.VertexBufferIndex];
        Vertex vertex = vertexBuffer.Load<Vertex>(index * sizeof(Vertex));

        float3 worldPosition = mul(float4(vertex.Position, 1), meshInstance.WorldMatrix);

        vertices[groupThreadId].Position = mul(float4(worldPosition, 1), camera.ViewProjectionMatrix);
        vertices[groupThreadId].WorldNormal = mul(vertex.Normal, meshInstance.WorldInvTransposeMatrix);
        vertices[groupThreadId].MeshletIndex = meshletIndex;
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

uint hash(uint a)
{
   a = (a+0x7ed55d16) + (a<<12);
   a = (a^0xc761c23c) ^ (a>>19);
   a = (a+0x165667b1) + (a<<5);
   a = (a+0xd3a2646c) ^ (a<<9);
   a = (a+0xfd7046c5) + (a<<3);
   a = (a^0xb55a4f09) ^ (a>>16);

   return a;
}

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    if (parameters.ShowMeshlets)
    {
        uint hashResult = hash(input.MeshletIndex);
	    float3 meshletColor = float3(float(hashResult & 255), float((hashResult >> 8) & 255), float((hashResult >> 16) & 255)) / 255.0;

        output.Color = float4(meshletColor, 1);
    }

    else
    {
        output.Color = float4(normalize(input.WorldNormal) * 0.5 + 0.5, 1);
    }

    return output; 
}