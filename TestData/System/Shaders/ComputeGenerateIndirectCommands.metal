#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

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

    const device Camera& camera = parameters.Cameras[cameraIndex];
    BoundingFrustum cameraFrustum = camera.BoundingFrustum;
    BoundingBox worldBoundingBox = geometryInstance.WorldBoundingBox;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        if (!camera.DepthOnly)
        {
            command_buffer opaqueCommandBuffer = parameters.IndirectCommandBuffers[camera.OpaqueCommandListIndex];
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
        }

        else
        {
            command_buffer transparentDepthCommandBuffer = parameters.IndirectCommandBuffers[camera.TransparentDepthCommandListIndex];
            EncodeDrawCommand(transparentDepthCommandBuffer, geometryInstanceIndex, parameters, vertexBuffer, indexBuffer, camera, geometryInstance, testLight, true, true);
        }
    }
}