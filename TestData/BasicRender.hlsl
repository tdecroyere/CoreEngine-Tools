struct VertexInput
{
    float3 Position;
    float3 Normal;
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
    uint GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
};

struct Mesh
{
    int GeometryInstancesCount;
    GeometryInstance GeometryInstancesIndex[100];
};

struct MeshInstance
{
    uint MeshIndex;
    float4x4 WorldMatrix;
    BoundingBox GeometryInstancesWorldBoundingBox[100];
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
    int MeshInstanceCount;
};

SceneProperties SceneProperties;
StructuredBuffer<GeometryPacket> GeometryPackets;
StructuredBuffer<GeometryInstance> GeometryInstances;
StructuredBuffer<Mesh> Meshes;
StructuredBuffer<MeshInstance> MeshInstances;
StructuredBuffer<VertexInput> VertexBuffers[1000];
StructuredBuffer<uint> IndexBuffers[1000];

void DrawMeshInstances()
{
    for (int i = 0; i < SceneProperties.MeshInstanceCount; i++)
    {
        MeshInstance meshInstance = MeshInstances[i];
        Mesh mesh = Meshes[meshInstance.MeshIndex];

        for (int j = 0; j < mesh.GeometryInstancesCount; j++)
        {
            GeometryInstance geometryInstance = GeometryInstances[j];
            GeometryPacket geometryPacket = GeometryPackets[geometryInstance.GeometryPacketIndex];
        }
    }
}