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

kernel void DrawMeshInstances(uint geometryInstanceIndex [[thread_position_in_grid]],
                              const device ShaderParameters& parameters)
{
    const device GeometryInstance& geometryInstance = parameters.GeometryInstances[geometryInstanceIndex];
    GeometryPacket geometryPacket = parameters.GeometryPackets[geometryInstance.GeometryPacketIndex];

    const device VertexInput* vertexBuffer = parameters.VertexBuffers[geometryPacket.VertexBufferIndex];
    const device uint* indexBuffer = parameters.IndexBuffers[geometryPacket.IndexBufferIndex] + geometryInstance.StartIndex;

    const device Light& testLight = parameters.Lights[0];

    const device Camera& activeCamera = parameters.Cameras[parameters.SceneProperties.ActiveCameraIndex];
    BoundingFrustum cameraFrustum = activeCamera.BoundingFrustum;
    BoundingBox worldBoundingBox = geometryInstance.WorldBoundingBox;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        render_command commandList = render_command(parameters.CameraCommandBuffer, geometryInstanceIndex);

        commandList.set_vertex_buffer(vertexBuffer, 0);
        commandList.set_vertex_buffer(&parameters, 1);

        if (!parameters.SceneProperties.isDebugCameraActive)
        {
            commandList.set_vertex_buffer(&activeCamera, 2);
        }

        else
        {
            commandList.set_vertex_buffer(&parameters.Cameras[parameters.SceneProperties.DebugCameraIndex], 2);
        }

        commandList.set_vertex_buffer(&geometryInstance, 3);
        commandList.set_vertex_buffer(&testLight, 4);

        if (geometryInstance.MaterialIndex != - 1)
        {
            const device void* materialData = parameters.MaterialData[geometryInstance.MaterialIndex];
            const device int& materialTextureOffset = parameters.MaterialTextureOffsets[geometryInstance.MaterialIndex];

            commandList.set_fragment_buffer(materialData, 0);
            commandList.set_fragment_buffer(&materialTextureOffset, 1);
        }

        commandList.set_fragment_buffer(&parameters, 2);
        commandList.set_fragment_buffer(&geometryInstance, 3);
        commandList.set_fragment_buffer(&testLight, 4);

        commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
    }

    // Light
    const device Camera& lightCamera = parameters.Cameras[testLight.CameraIndexes[0]];
    cameraFrustum = lightCamera.BoundingFrustum;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        render_command commandList = render_command(parameters.LightCommandBuffers[0], geometryInstanceIndex);

        commandList.set_vertex_buffer(vertexBuffer, 0);
        commandList.set_vertex_buffer(&parameters, 1);
        commandList.set_vertex_buffer(&lightCamera, 2);
        commandList.set_vertex_buffer(&geometryInstance, 3);

        if (geometryInstance.MaterialIndex != - 1)
        {
            const device void* materialData = parameters.MaterialData[geometryInstance.MaterialIndex];
            const device int& materialTextureOffset = parameters.MaterialTextureOffsets[geometryInstance.MaterialIndex];

            commandList.set_fragment_buffer(materialData, 0);
            commandList.set_fragment_buffer(&materialTextureOffset, 1);
            commandList.set_fragment_buffer(&parameters, 2);
            
        }
        
        commandList.set_fragment_buffer(&geometryInstance, 3);
        commandList.set_fragment_buffer(&testLight, 4);
        
        commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
    }

    const device Camera& lightCamera2 = parameters.Cameras[testLight.CameraIndexes[1]];
    cameraFrustum = lightCamera2.BoundingFrustum;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        render_command commandList = render_command(parameters.LightCommandBuffers[1], geometryInstanceIndex);

        commandList.set_vertex_buffer(vertexBuffer, 0);
        commandList.set_vertex_buffer(&parameters, 1);
        commandList.set_vertex_buffer(&lightCamera2, 2);
        commandList.set_vertex_buffer(&geometryInstance, 3);

        if (geometryInstance.MaterialIndex != - 1)
        {
            const device void* materialData = parameters.MaterialData[geometryInstance.MaterialIndex];
            const device int& materialTextureOffset = parameters.MaterialTextureOffsets[geometryInstance.MaterialIndex];

            commandList.set_fragment_buffer(materialData, 0);
            commandList.set_fragment_buffer(&materialTextureOffset, 1);
            commandList.set_fragment_buffer(&parameters, 2);
            
        }
        
        commandList.set_fragment_buffer(&geometryInstance, 3);
        commandList.set_fragment_buffer(&testLight, 4);
        
        commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
    }

    const device Camera& lightCamera3 = parameters.Cameras[testLight.CameraIndexes[2]];
    cameraFrustum = lightCamera3.BoundingFrustum;

    if (Intersect(cameraFrustum, worldBoundingBox))
    {
        render_command commandList = render_command(parameters.LightCommandBuffers[2], geometryInstanceIndex);

        commandList.set_vertex_buffer(vertexBuffer, 0);
        commandList.set_vertex_buffer(&parameters, 1);
        commandList.set_vertex_buffer(&lightCamera3, 2);
        commandList.set_vertex_buffer(&geometryInstance, 3);

        if (geometryInstance.MaterialIndex != - 1)
        {
            const device void* materialData = parameters.MaterialData[geometryInstance.MaterialIndex];
            const device int& materialTextureOffset = parameters.MaterialTextureOffsets[geometryInstance.MaterialIndex];

            commandList.set_fragment_buffer(materialData, 0);
            commandList.set_fragment_buffer(&materialTextureOffset, 1);
            commandList.set_fragment_buffer(&parameters, 2);
            
        }
        
        commandList.set_fragment_buffer(&geometryInstance, 3);
        commandList.set_fragment_buffer(&testLight, 4);
        
        commandList.draw_indexed_primitives(primitive_type::triangle, geometryInstance.IndexCount, indexBuffer, 1, 0, geometryInstanceIndex);
    }
}