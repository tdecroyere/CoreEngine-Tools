#define RootSignatureDef \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants=5, b0)," \
    "SRV(t0, flags = DATA_STATIC), " \
    "SRV(t1, flags = DATA_STATIC)," \
    "SRV(t2, flags = DATA_STATIC)," \
    "SRV(t3, flags = DATA_STATIC)"

#pragma pack_matrix(row_major)
#define WAVE_SIZE 32

static const float PI = 3.14159265f;

enum DebugPrimitiveType : uint
{
    Line,
    Cube,
    Sphere,
    Frustrum
};

struct ShaderParameters
{
    uint TotalDebugPrimitiveCount;
    uint InstanceOffset;
    uint VertexBufferOffset;
    uint IndexBufferOffset;
    DebugPrimitiveType DebugPrimitiveType;
};

struct DebugPrimitive
{
    float4x4 WorldMatrix;
    float4 Color;
};

struct VertexInput
{
    float4 Position;
    float4 Color;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    nointerpolation float4 Color: TEXCOORD0;
};

struct RenderPass
{
    float4x4 ViewProjMatrix;
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
StructuredBuffer<DebugPrimitive> debugPrimitives: register(t0);
StructuredBuffer<RenderPass> RenderPassParameters: register(t1);

StructuredBuffer<float3> vertexBuffer: register(t2);
StructuredBuffer<uint2> indexBuffer: register(t3);

groupshared Payload sharedPayload;

[NumThreads(WAVE_SIZE, 1, 1)]
void AmplificationMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    const uint maxDebugPrimitiveCount = WAVE_SIZE;

    uint totalDebugPrimitiveCount = min(maxDebugPrimitiveCount, parameters.TotalDebugPrimitiveCount - groupId * maxDebugPrimitiveCount);
    uint vertexCount;
    uint primitiveCount;
    uint maxDebugPrimitiveCountPerGroup;

    switch (parameters.DebugPrimitiveType)
    {
        case DebugPrimitiveType::Cube:
            vertexCount = 8;
            primitiveCount = 12;
            maxDebugPrimitiveCountPerGroup = 20;
            break;

        case DebugPrimitiveType::Sphere:
            vertexCount = 90;
            primitiveCount = 90;
            maxDebugPrimitiveCountPerGroup = 2;
            break;

        default:
            vertexCount = 2;
            primitiveCount = 1;
            maxDebugPrimitiveCountPerGroup = 64;
            break;
    }
  
    uint debugPrimitiveIndex = parameters.InstanceOffset + groupId * maxDebugPrimitiveCount + groupThreadId;
    DebugPrimitive debugPrimitive = debugPrimitives[debugPrimitiveIndex];
    float4x4 worldMatrix = debugPrimitive.WorldMatrix;

    sharedPayload.TotalDebugPrimitiveCount = totalDebugPrimitiveCount;
    sharedPayload.MaxDebugPrimitiveCountPerGroup = maxDebugPrimitiveCountPerGroup;
    sharedPayload.VertexCount = vertexCount;
    sharedPayload.PrimitiveCount = primitiveCount;
    sharedPayload.WorldViewProjMatrices[groupThreadId] = mul(worldMatrix, RenderPassParameters[0].ViewProjMatrix);
    sharedPayload.Colors[groupThreadId] = debugPrimitive.Color.rgb;

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
        float4x4 worldViewProjMatrix = payload.WorldViewProjMatrices[vertexInstanceId];
        float3 vertex = vertexBuffer[parameters.VertexBufferOffset + vertexId];

        vertices[currentGroupThreadId].Position = mul(float4(vertex, 1), worldViewProjMatrix);
        vertices[currentGroupThreadId].Color = float4(payload.Colors[vertexInstanceId], 1);

        vertex = vertexBuffer[parameters.VertexBufferOffset + vertexId + 1];

        vertices[currentGroupThreadId + 1].Position = mul(float4(vertex, 1), worldViewProjMatrix);
        vertices[currentGroupThreadId + 1].Color = float4(payload.Colors[vertexInstanceId], 1);
    }

    uint primitiveId = currentGroupThreadId % payload.PrimitiveCount;
    uint primitiveOffset = currentGroupThreadId / payload.PrimitiveCount;
    uint primitiveInstanceId = baseInstanceId + primitiveOffset;

    if (primitiveInstanceId < payload.TotalDebugPrimitiveCount)
    {
        indices[currentGroupThreadId] = indexBuffer[parameters.IndexBufferOffset + primitiveId] + primitiveOffset * payload.VertexCount;
        indices[currentGroupThreadId + 1] = indexBuffer[parameters.IndexBufferOffset + primitiveId + 1] + primitiveOffset * payload.VertexCount;
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