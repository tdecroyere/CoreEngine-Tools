#include "Common.hlsl"

#define RootSignatureDef RootSignatureDefinition(11)
#define WAVE_SIZE 32

enum DebugPrimitiveType
{
    Line = 0,
    Cube = 1,
    Sphere = 2,
    BoundingFrustum = 3
};

struct ShaderParameters
{
    uint TotalDebugPrimitiveCount;
    uint InstanceOffset;
    uint VertexBufferOffset;
    uint IndexBufferOffset;
    uint MaxDebugPrimitiveCountPerGroup;
    uint VertexCount;
    uint PrimitiveCount;
    
    uint DebugPrimitivesBuffer;
    uint CameraBuffer;
    uint VertexBuffer;
    uint IndexBuffer;
};

struct DebugPrimitive
{
    float4 Parameter1;
    float4 Parameter2;
    float4 Parameter3;
    float4 Parameter4;
    float4 ColorAndPrimitiveType;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    nointerpolation float4 Color: TEXCOORD0;
};

struct Camera
{
    float3 WorldPosition;
    float4x4 ViewMatrix;
    float4x4 ViewProjectionMatrix;
};

struct Payload
{
    uint VertexCount;
    uint PrimitiveCount;
    uint TotalDebugPrimitiveCount;
    uint MaxDebugPrimitiveCountPerGroup;

    float4x4 WorldViewProjMatrices[WAVE_SIZE];
    float3 Colors[WAVE_SIZE];
};

ConstantBuffer<ShaderParameters> parameters : register(b0);

groupshared Payload sharedPayload;

[NumThreads(WAVE_SIZE, 1, 1)]
void AmplificationMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    const uint maxDebugPrimitiveCount = WAVE_SIZE;

    uint totalDebugPrimitiveCount = min(maxDebugPrimitiveCount, parameters.TotalDebugPrimitiveCount - groupId * maxDebugPrimitiveCount);
    uint vertexCount = parameters.VertexCount;
    uint primitiveCount = parameters.PrimitiveCount;
    uint maxDebugPrimitiveCountPerGroup = parameters.MaxDebugPrimitiveCountPerGroup;

    uint debugPrimitiveIndex = parameters.InstanceOffset + groupId * maxDebugPrimitiveCount + groupThreadId;

    ByteAddressBuffer debugPrimitives = buffers[parameters.DebugPrimitivesBuffer];
    DebugPrimitive debugPrimitive = debugPrimitives.Load<DebugPrimitive>(debugPrimitiveIndex * sizeof(DebugPrimitive));

    ByteAddressBuffer cameraBuffer = buffers[parameters.CameraBuffer];
    
    float4x4 worldMatrix = (float4x4)0;
    DebugPrimitiveType debugPrimitiveType = (DebugPrimitiveType)debugPrimitive.ColorAndPrimitiveType.w;
    
    switch(debugPrimitiveType)
    {
        case DebugPrimitiveType::Line:
            break;

        case DebugPrimitiveType::Cube:
            worldMatrix = CreateScaleTranslation(debugPrimitive.Parameter1.xyz, debugPrimitive.Parameter2.xyz);
            break;

        case DebugPrimitiveType::Sphere:
            worldMatrix = CreateScaleTranslation((debugPrimitive.Parameter2.x * 2.0f).xxx, debugPrimitive.Parameter1.xyz);
            break;

        case DebugPrimitiveType::BoundingFrustum:
            worldMatrix = float4x4(debugPrimitive.Parameter1,
                                   debugPrimitive.Parameter2,
                                   debugPrimitive.Parameter3,
                                   debugPrimitive.Parameter4);
            break;
    }

    sharedPayload.TotalDebugPrimitiveCount = totalDebugPrimitiveCount;
    sharedPayload.MaxDebugPrimitiveCountPerGroup = maxDebugPrimitiveCountPerGroup;
    sharedPayload.VertexCount = vertexCount;
    sharedPayload.PrimitiveCount = primitiveCount;
    sharedPayload.WorldViewProjMatrices[groupThreadId] = mul(worldMatrix, cameraBuffer.Load<Camera>(0).ViewProjectionMatrix);
    sharedPayload.Colors[groupThreadId] = debugPrimitive.ColorAndPrimitiveType.rgb;

    DispatchMesh(ceil((float)totalDebugPrimitiveCount / maxDebugPrimitiveCountPerGroup), 1, 1, sharedPayload);
}

[OutputTopology("line")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId: SV_GroupID, 
              in uint groupThreadId: SV_GroupThreadID, 
              in payload Payload payload, 
              out vertices VertexOutput vertices[256], 
              out indices uint2 indices[256])
{
    uint currentDebugPrimitiveCount = min(payload.MaxDebugPrimitiveCountPerGroup, payload.TotalDebugPrimitiveCount - groupId * payload.MaxDebugPrimitiveCountPerGroup);
    uint meshVertexCount = currentDebugPrimitiveCount * payload.VertexCount;
    uint meshPrimitiveCount = currentDebugPrimitiveCount * payload.PrimitiveCount;

    SetMeshOutputCounts(meshVertexCount, meshPrimitiveCount);

    uint baseInstanceId = groupId * payload.MaxDebugPrimitiveCountPerGroup;
    uint currentGroupThreadId = groupThreadId * 2;

    uint vertexId = currentGroupThreadId % payload.VertexCount;
    uint vertexOffset = currentGroupThreadId / payload.VertexCount;
    uint vertexInstanceId = baseInstanceId + vertexOffset;

    if (vertexInstanceId < payload.TotalDebugPrimitiveCount)
    {
        ByteAddressBuffer vertexBuffer = buffers[parameters.VertexBuffer];

        float4x4 worldViewProjMatrix = payload.WorldViewProjMatrices[vertexInstanceId];
        float3 vertex = vertexBuffer.Load<float3>((parameters.VertexBufferOffset + vertexId) * sizeof(float3));

        vertices[currentGroupThreadId].Position = mul(float4(vertex, 1), worldViewProjMatrix);
        vertices[currentGroupThreadId].Color = float4(payload.Colors[vertexInstanceId], 1);

        vertex = vertexBuffer.Load<float3>((parameters.VertexBufferOffset + vertexId + 1) * sizeof(float3));

        vertices[currentGroupThreadId + 1].Position = mul(float4(vertex, 1), worldViewProjMatrix);
        vertices[currentGroupThreadId + 1].Color = float4(payload.Colors[vertexInstanceId], 1);
    }

    uint primitiveId = currentGroupThreadId % payload.PrimitiveCount;
    uint primitiveOffset = currentGroupThreadId / payload.PrimitiveCount;
    uint primitiveInstanceId = baseInstanceId + primitiveOffset;

    if (primitiveInstanceId < payload.TotalDebugPrimitiveCount)
    {
        ByteAddressBuffer indexBuffer = buffers[parameters.IndexBuffer];

        indices[currentGroupThreadId] = indexBuffer.Load<uint2>((parameters.IndexBufferOffset + primitiveId) * sizeof(uint2)) + primitiveOffset * payload.VertexCount;
        indices[currentGroupThreadId + 1] = indexBuffer.Load<uint2>((parameters.IndexBufferOffset + primitiveId + 1) * sizeof(uint2)) + primitiveOffset * payload.VertexCount;
    }
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

[earlydepthstencil]
PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    output.Color = float4(input.Color.xyz, 1.0);

    return output; 
}