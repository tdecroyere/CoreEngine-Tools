#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinition(9)

struct ShaderParameters
{
    uint CamerasBuffer;
    uint ShowMeshlets;
    uint MeshBufferIndex;
    uint MeshInstanceBufferIndex;
    uint MeshInstanceVisibilityBufferIndex;
    uint CommandBufferIndex;
    uint MeshInstanceCount;
    uint IsPostPass;
    uint IsOcclusionCullingEnabled;
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
void ComputeMain(in uint threadId: SV_DispatchThreadId)
{
    uint meshInstanceIndex = threadId;
    bool isMeshInstanceVisible = false;

    ByteAddressBuffer meshInstanceBuffer = buffers[parameters.MeshInstanceBufferIndex];
    MeshInstance meshInstance = meshInstanceBuffer.Load<MeshInstance>(meshInstanceIndex * sizeof(MeshInstance));

    RWByteAddressBuffer meshInstanceVisibilityBuffer = rwBuffers[parameters.MeshInstanceVisibilityBufferIndex];
    uint meshInstanceVisibility = meshInstanceVisibilityBuffer.Load<uint>(meshInstanceIndex * sizeof(uint));

    if (parameters.IsPostPass == 0 && meshInstanceVisibility == 0)
    {
        return;
    }

    if (meshInstanceIndex < parameters.MeshInstanceCount)
    {
        ByteAddressBuffer cameras = buffers[parameters.CamerasBuffer];
        Camera camera = cameras.Load<Camera>(0);

        isMeshInstanceVisible = Intersect(camera.BoundingFrustum, meshInstance.WorldBoundingBox);
    }

    if (parameters.IsPostPass == 1)
    {
        if (parameters.IsOcclusionCullingEnabled == 1)
        {
            isMeshInstanceVisible = meshInstanceIndex < 100;
        }

        meshInstanceVisibilityBuffer.Store<uint>(meshInstanceIndex * sizeof(uint), isMeshInstanceVisible ? 1 : 0);
        isMeshInstanceVisible = isMeshInstanceVisible && meshInstanceVisibility == 0;
    }

    uint visibleMeshInstanceCount = WaveActiveCountBits(isMeshInstanceVisible);
    
    if (visibleMeshInstanceCount == 0)
    {
        return;
    }

    RWByteAddressBuffer commandBuffer = rwBuffers[parameters.CommandBufferIndex];
    uint commandOffset = 0;

    if (WaveIsFirstLane())
    {
        uint sizeInBytes = 0;
        commandBuffer.GetDimensions(sizeInBytes);

        commandBuffer.InterlockedAdd(sizeInBytes - sizeof(uint), visibleMeshInstanceCount, commandOffset);
    }

    commandOffset = WaveReadLaneFirst(commandOffset);
    
    if (isMeshInstanceVisible && (parameters.IsPostPass == 0 || (parameters.IsPostPass == 1 && meshInstanceVisibility == 0)))
    {
        uint laneIndex = WavePrefixCountBits(isMeshInstanceVisible);
        uint commandIndex = commandOffset + laneIndex;
        DispatchMeshIndirectParam command = (DispatchMeshIndirectParam)0;

        // TODO: Pack the draw calls and sort them by mesh type
        ByteAddressBuffer meshBuffer = buffers[parameters.MeshBufferIndex];
        Mesh mesh = meshBuffer.Load<Mesh>(meshInstance.MeshIndex * sizeof(Mesh));

        command.CamerasBuffer = parameters.CamerasBuffer;
        command.ShowMeshlets = parameters.ShowMeshlets;

        command.MeshBufferIndex = parameters.MeshBufferIndex;
        command.MeshInstanceBufferIndex = parameters.MeshInstanceBufferIndex;
        command.MeshletCount = mesh.MeshletCount;
        command.MeshInstanceIndex = meshInstanceIndex;

        command.ThreadGroupCount = (uint)ceil((float)mesh.MeshletCount / WAVE_SIZE);

    #ifdef VULKAN
        command.Reserved1 = 0;
        command.Reserved2 = 0;
    #else
        command.Reserved1 = 1;
        command.Reserved2 = 1;
    #endif

        commandBuffer.Store<DispatchMeshIndirectParam>(commandIndex * sizeof(DispatchMeshIndirectParam), command);
    }
}