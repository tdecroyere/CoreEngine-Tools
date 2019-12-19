#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

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
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct GeometryInstance
{
    uint GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    float4x4 WorldMatrix;
    BoundingBox WorldBoundingBox;
};

struct Camera
{
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
    const array<const device VertexInput*, 1000> VertexBuffers [[id(5)]];
    const array<const device uint*, 1000> IndexBuffers [[id(1005)]];
    command_buffer CommandBuffer [[id(2005)]];
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
    if (!Intersect(frustum.LeftPlane, box))
    {
        return false;
    }

    if (!Intersect(frustum.RightPlane, box))
    {
        return false;
    }

    if (!Intersect(frustum.TopPlane, box))
    {
        return false;
    }

    if (!Intersect(frustum.BottomPlane, box))
    {
        return false;
    }

    if (!Intersect(frustum.NearPlane, box))
    {
        return false;
    }

    if (!Intersect(frustum.FarPlane, box))
    {
        return false;
    }

    return true;
}


kernel void DrawMeshInstances(uint geometryInstanceIndex [[thread_position_in_grid]],
                              const device ShaderParameters& parameters)
{
    const device GeometryInstance& geometryInstance = parameters.GeometryInstances[geometryInstanceIndex];
    GeometryPacket geometryPacket = parameters.GeometryPackets[geometryInstance.GeometryPacketIndex];

    const device VertexInput* vertexBuffer = parameters.VertexBuffers[geometryPacket.VertexBufferIndex];
    const device uint* indexBuffer = parameters.IndexBuffers[geometryPacket.IndexBufferIndex] + geometryInstance.StartIndex;

    BoundingFrustum cameraFrustum = parameters.SceneProperties.ActiveCamera.BoundingFrustum;
    BoundingBox worldBoundingBox = geometryInstance.WorldBoundingBox;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        render_command commandList = render_command(parameters.CommandBuffer, geometryInstanceIndex);

        commandList.set_vertex_buffer(vertexBuffer, 0);

        if (!parameters.SceneProperties.isDebugCameraActive)
        {
            commandList.set_vertex_buffer(&parameters.SceneProperties.ActiveCamera, 1);
        }

        else
        {
            commandList.set_vertex_buffer(&parameters.SceneProperties.DebugCamera, 1);
        }

        commandList.set_vertex_buffer(&geometryInstance, 2);
        commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
    }
}