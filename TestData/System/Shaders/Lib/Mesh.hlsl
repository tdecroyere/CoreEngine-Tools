// TODO: Compress Vertex attributes to save bandwidth
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
    uint VerticesBufferIndex;
    uint VertexIndicesBufferIndex;
    uint TriangleIndicesBufferIndex;
    uint MeshletBufferIndex;
    BoundingBox BoundingBox;
};

struct Meshlet
{
    uint PackedCone;
    BoundingSphere BoundingSphere;
    uint VertexCount;
    uint VertexOffset;
    uint TriangleCount;
    uint TriangleOffset;
};

struct MeshInstance
{
    uint MeshIndex;
    float Scale; // TODO: For the moment we support only uniform scaling
    float4x3 WorldMatrix;
    float3x3 WorldInvTransposeMatrix;
};

struct Camera
{
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ViewProjectionMatrix;
    BoundingFrustum BoundingFrustum;
};
