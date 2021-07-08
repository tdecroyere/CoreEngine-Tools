#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinition(6)

struct ShaderParameters
{
    uint CamerasBuffer;
    uint ShowMeshlets;
    uint MeshBufferIndex;
    uint MeshInstanceBufferIndex;
    uint CommandBufferIndex;
    uint MeshInstanceCount;
};

// TODO a macro system to generate the struct with the correct
// amount of parameters?
struct DispatchMeshIndirectParam
{
    uint CamerasBuffer;
    uint ShowMeshlets;

    uint MeshBufferIndex;
    uint MeshInstanceBufferIndex;
    uint MeshletCount;
    uint MeshInstanceIndex;

    uint ThreadGroupCount;
    uint Reserved1;
    uint Reserved2;
};

[[vk::push_constant]]
ConstantBuffer<ShaderParameters> parameters : register(b0);

[NumThreads(WAVE_SIZE, 1, 1)]
void ComputeMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    uint meshInstanceIndex = groupId * WAVE_SIZE + groupThreadId;

    // TODO: Process only mesh instance for now
    if (meshInstanceIndex > 0)
    {
        return;
    }

    ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
    MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

    ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
    Mesh mesh = meshBuffer.Load<Mesh>(meshInstance.MeshIndex * sizeof(Mesh));

    RWByteAddressBuffer commandBuffer = rwBuffers[parameters.CommandBufferIndex];

    uint commandIndex = 0;

    DispatchMeshIndirectParam command = (DispatchMeshIndirectParam)0;

    command.CamerasBuffer = parameters.CamerasBuffer;
    command.ShowMeshlets = parameters.ShowMeshlets;

    command.MeshBufferIndex = parameters.MeshBufferIndex;
    command.MeshInstanceBufferIndex = parameters.MeshInstanceBufferIndex;
    command.MeshletCount = mesh.MeshletCount;
    command.MeshInstanceIndex = meshInstanceIndex;

    command.ThreadGroupCount = (uint)ceil((float)mesh.MeshletCount / WAVE_SIZE);
    command.Reserved1 = 1; // TODO: Put that value to 0 for vulkan
    command.Reserved2 = 1;

    commandBuffer.Store<DispatchMeshIndirectParam>(commandIndex, command);
}