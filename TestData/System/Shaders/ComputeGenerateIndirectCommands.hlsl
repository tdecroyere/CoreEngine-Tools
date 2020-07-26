#define RootSignatureDef \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants=2, b0)," \
    "SRV(t1, flags = DATA_VOLATILE)," \
    "DescriptorTable(" \
    "                UAV(u1, numDescriptors = 1, flags = DATA_VOLATILE))"

cbuffer RootConstants : register(b0)
{
    uint2 cameraBufferAddress;
};

struct D3D12_DRAW_ARGUMENTS
{
    uint VertexCountPerInstance;
    uint InstanceCount;
    uint StartVertexLocation;
    uint StartInstanceLocation;
};

struct IndirectCommand
{
    uint2 cbvAddress;
    D3D12_DRAW_ARGUMENTS drawArguments;
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

struct Camera
{
    int DepthBufferTextureIndex;
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
    float4x4 ViewProjectionMatrix;
    float4x4 ViewProjectionInverse;
    BoundingFrustum BoundingFrustum;
    int OpaqueCommandListIndex;
    int OpaqueDepthCommandListIndex;
    int TransparentCommandListIndex;
    int TransparentDepthCommandListIndex;
    bool DepthOnly;
    bool AlreadyProcessed;
    float MinDepth;
    float MaxDepth;
    int MomentShadowMapTextureIndex;
    int OcclusionDepthTextureIndex;
    int OcclusionDepthCommandListIndex;
};

StructuredBuffer<Camera> Cameras: register(t1);
AppendStructuredBuffer<IndirectCommand> IndirectCommandBuffers: register(u1);

[numthreads(32, 32, 1)]
void GenerateIndirectCommands(uint2 pixelCoordinates: SV_DispatchThreadID)
{                
    IndirectCommand command = (IndirectCommand)0;
    command.cbvAddress = cameraBufferAddress;
    command.drawArguments.VertexCountPerInstance = 3;
    command.drawArguments.InstanceCount = 1;
    
    IndirectCommandBuffers.Append(command);
}