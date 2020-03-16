#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

float XSize(BoundingBox boundingBox)
{
    return boundingBox.MaxPoint.x - boundingBox.MinPoint.x;
}

float YSize(BoundingBox boundingBox)
{
    return boundingBox.MaxPoint.y - boundingBox.MinPoint.y;
}

float ZSize(BoundingBox boundingBox)
{
    return boundingBox.MaxPoint.z - boundingBox.MinPoint.z;
}

void AddPointToBoundingBox(thread BoundingBox& boundingBox, float4 point)
{
    point.xyz /= point.w;

    float minX = (point.x < boundingBox.MinPoint.x) ? point.x : boundingBox.MinPoint.x;
    float minY = (point.y < boundingBox.MinPoint.y) ? point.y : boundingBox.MinPoint.y;
    float minZ = (point.z < boundingBox.MinPoint.z) ? point.z : boundingBox.MinPoint.z;

    float maxX = (point.x > boundingBox.MaxPoint.x) ? point.x : boundingBox.MaxPoint.x;
    float maxY = (point.y > boundingBox.MaxPoint.y) ? point.y : boundingBox.MaxPoint.y;
    float maxZ = (point.z > boundingBox.MaxPoint.z) ? point.z : boundingBox.MaxPoint.z;

    boundingBox.MinPoint = float3(minX, minY, minZ);
    boundingBox.MaxPoint = float3(maxX, maxY, maxZ);
}

BoundingBox CreateTransformedBoundingBox(BoundingBox boundingBox, float4x4 matrix)
{
    float4 pointList[8];

    pointList[0] = matrix * float4((boundingBox.MinPoint + float3(0, 0, 0)), 1);
    pointList[1] = matrix * float4((boundingBox.MinPoint + float3(XSize(boundingBox), 0, 0)), 1);
    pointList[2] = matrix * float4((boundingBox.MinPoint + float3(0, YSize(boundingBox), 0)), 1);
    pointList[3] = matrix * float4((boundingBox.MinPoint + float3(XSize(boundingBox), YSize(boundingBox), 0)), 1);
    pointList[4] = matrix * float4((boundingBox.MinPoint + float3(0, 0, ZSize(boundingBox))), 1);
    pointList[5] = matrix * float4((boundingBox.MinPoint + float3(XSize(boundingBox), 0, ZSize(boundingBox))), 1);
    pointList[6] = matrix * float4((boundingBox.MinPoint + float3(0, YSize(boundingBox), ZSize(boundingBox))), 1);
    pointList[7] = matrix * float4((boundingBox.MinPoint + float3(XSize(boundingBox), YSize(boundingBox), ZSize(boundingBox))), 1);

    BoundingBox result = {};

    result.MinPoint = float3(MAXFLOAT, MAXFLOAT, MAXFLOAT);
    result.MaxPoint = float3(-MAXFLOAT, -MAXFLOAT, -MAXFLOAT);

    for (int i = 0; i < 8; i++)
    {
        AddPointToBoundingBox(result, pointList[i]);
    }

    return result;
}

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

__attribute__((always_inline))
void EncodeDrawCommand(command_buffer indirectCommandBuffer, 
                       int geometryInstanceIndex, 
                       const device ShaderParameters& parameters,
                       const device VertexInput* vertexBuffer, 
                       const device uint* indexBuffer, 
                       const device Camera& camera, 
                       const device GeometryInstance& geometryInstance,
                       const device Light& testLight,
                       bool isTransparent,
                       bool depthOnly)
{
    render_command commandList = render_command(indirectCommandBuffer, geometryInstanceIndex);

    commandList.set_vertex_buffer(vertexBuffer, 0);
    commandList.set_vertex_buffer(&camera, 1);
    commandList.set_vertex_buffer(&geometryInstance, 2);

    if (!(isTransparent == false && depthOnly == true))
    {
        if (geometryInstance.MaterialIndex != - 1)
        {
            const device Material& material = parameters.Materials[geometryInstance.MaterialIndex];
            const device void* materialData = parameters.Buffers[material.MaterialBufferIndex];

            commandList.set_fragment_buffer(&material, 0);
            commandList.set_fragment_buffer(materialData, 1);
        }

        commandList.set_fragment_buffer(&parameters, 2);
        commandList.set_fragment_buffer(&geometryInstance, 3);
        commandList.set_fragment_buffer(&testLight, 4);
    }

    commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
}

kernel void GenerateIndirectCommands(uint2 threadPosition [[thread_position_in_grid]],
                                     uint2 threadPositionInGroup [[thread_position_in_threadgroup]],
                                     const device ShaderParameters& parameters)
{
    uint geometryInstanceIndex = threadPosition.x;
    uint cameraIndex = threadPosition.y;

    const device GeometryInstance& geometryInstance = parameters.GeometryInstances[geometryInstanceIndex];
    GeometryPacket geometryPacket = parameters.GeometryPackets[geometryInstance.GeometryPacketIndex];
    Material material = parameters.Materials[geometryInstance.MaterialIndex];

    const device VertexInput* vertexBuffer = (const device VertexInput*)parameters.Buffers[geometryPacket.VertexBufferIndex];
    const device uint* indexBuffer = (const device uint*)parameters.Buffers[geometryPacket.IndexBufferIndex] + geometryInstance.StartIndex;

    const device Light& testLight = parameters.Lights[0];

    device Camera& camera = parameters.Cameras[cameraIndex];

    if (camera.AlreadyProcessed)
    {
        return;
    }

    // threadgroup atomic_uint geometryInstanceCount;

    // if (threadPositionInGroup.x == 0)
    // {
    //     atomic_store_explicit(&geometryInstanceCount, 0, metal::memory_order_relaxed);
    // }
    
    // threadgroup_barrier(mem_flags::mem_threadgroup);

    BoundingFrustum cameraFrustum = camera.BoundingFrustum;
    BoundingBox worldBoundingBox = geometryInstance.WorldBoundingBox;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        if (!camera.DepthOnly)
        {
            command_buffer opaqueCommandBuffer = parameters.IndirectCommandBuffers[camera.OpaqueCommandListIndex];
            
            device atomic_uint* commandBufferCounter = (device atomic_uint*)&parameters.IndirectCommandBufferCounters[camera.OpaqueCommandListIndex];
            atomic_fetch_add_explicit(commandBufferCounter, 1, metal::memory_order_relaxed);

            //atomic_fetch_add_explicit(&geometryInstanceCount, 1, metal::memory_order_relaxed);

            EncodeDrawCommand(opaqueCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, false, false);

            if (material.IsTransparent)
            {
                command_buffer transparentCommandBuffer = parameters.IndirectCommandBuffers[camera.TransparentCommandListIndex];
                EncodeDrawCommand(transparentCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, true, false);
            }
        }

        if (material.IsTransparent == false)
        {
            command_buffer opaqueDepthCommandBuffer = parameters.IndirectCommandBuffers[camera.OpaqueDepthCommandListIndex];
            EncodeDrawCommand(opaqueDepthCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, false, true);

            BoundingBox screenSpaceBoundingBox = CreateTransformedBoundingBox(worldBoundingBox, camera.ViewProjectionMatrix);
            float minLength = 2.0;

            // if (screenSpaceBoundingBox.MinPoint.z > 0.2)
            // {
            //     minLength = 0.5;
            // }

            // if (XSize(worldBoundingBox) > minLength || YSize(worldBoundingBox) > minLength)
            if (length(worldBoundingBox.MaxPoint - worldBoundingBox.MinPoint) > minLength)
            {
                command_buffer occlusionDepthCommandBuffer = parameters.IndirectCommandBuffers[camera.OcclusionDepthCommandListIndex];
                EncodeDrawCommand(occlusionDepthCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, false, true);
            }
        }

        else
        {
            command_buffer transparentDepthCommandBuffer = parameters.IndirectCommandBuffers[camera.TransparentDepthCommandListIndex];
            EncodeDrawCommand(transparentDepthCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, true, true);
        }
    }

    // threadgroup_barrier(mem_flags::mem_threadgroup);

    // if (threadPositionInGroup.x == 0)
    // {
    //     device atomic_uint* commandBufferCounter = (device atomic_uint*)&parameters.IndirectCommandBufferCounters[camera.OpaqueCommandListIndex];
    //     atomic_fetch_add_explicit(commandBufferCounter, atomic_load_explicit(&geometryInstanceCount, memory_order_relaxed), metal::memory_order_relaxed);
    // }

    camera.AlreadyProcessed = true;
}