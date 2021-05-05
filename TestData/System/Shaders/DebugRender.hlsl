#define RootSignatureDef \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants=3, b0)," \
    "SRV(t0, flags = DATA_STATIC), " \
    "SRV(t1, flags = DATA_STATIC)," \
    "UAV(u0, flags = DATA_VOLATILE)"

#pragma pack_matrix(row_major)

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
    uint DebugPrimitiveCount;
    uint InstanceOffset;
    DebugPrimitiveType DebugPrimitiveType;
};

struct DebugPrimitive
{
    float4 Parameter1;
    float4 Parameter2;
    float3 Color;
    DebugPrimitiveType PrimitiveType;
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
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct Meshlet
{
    float3 Vertices[90];
    uint2 Primitives[90];
    float3 Color;
};

struct Payload
{
    uint VertexCount;
    uint PrimitiveCount;
    uint MaxDebugPrimitiveCountPerGroup;
    uint DebugPrimitiveCount;
    uint InstanceOffset;
};

ConstantBuffer<ShaderParameters> parameters : register(b0);
StructuredBuffer<DebugPrimitive> debugPrimitives: register(t0);
StructuredBuffer<RenderPass> RenderPassParameters: register(t1);

RWStructuredBuffer<Meshlet> meshletBuffer: register(u0);

Meshlet InitLine(DebugPrimitive debugPrimitive)
{
    Meshlet meshlet = (Meshlet)0;
    meshlet.Vertices[0] = debugPrimitive.Parameter1.xyz;
    meshlet.Vertices[1] = debugPrimitive.Parameter2.xyz;

    meshlet.Primitives[0] = uint2(0, 1);
    meshlet.Color = debugPrimitive.Color;

    return meshlet;
}

[NumThreads(64, 1, 1)]
void AmplificationMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{
    const uint maxDebugPrimitiveCount = 64;

    Payload payload = (Payload)0;
    payload.DebugPrimitiveCount = min(maxDebugPrimitiveCount, parameters.DebugPrimitiveCount - groupId * maxDebugPrimitiveCount);
    payload.InstanceOffset = groupId * maxDebugPrimitiveCount;

    if (parameters.DebugPrimitiveType == DebugPrimitiveType::Line)
    {
        payload.VertexCount = 2;
        payload.PrimitiveCount = 1;
        payload.MaxDebugPrimitiveCountPerGroup = 64;
    }

    else if (parameters.DebugPrimitiveType == DebugPrimitiveType::Cube)
    {
        payload.VertexCount = 8;
        payload.PrimitiveCount = 12;
        payload.MaxDebugPrimitiveCountPerGroup = 10;
    }

    else if (parameters.DebugPrimitiveType == DebugPrimitiveType::Sphere)
    {
        const uint steps = 30;

        payload.VertexCount = steps * 3;
        payload.PrimitiveCount = steps * 3;
        payload.MaxDebugPrimitiveCountPerGroup = 1;
    }

    // TODO: Do frustum culling per primitive type
    Meshlet meshlet = (Meshlet)0;

    uint debugPrimitiveIndex = parameters.InstanceOffset + groupId * maxDebugPrimitiveCount + groupThreadId;
    DebugPrimitive debugPrimitive = debugPrimitives[debugPrimitiveIndex];

    if (parameters.DebugPrimitiveType == DebugPrimitiveType::Line)
    {
        meshlet.Vertices[0] = debugPrimitive.Parameter1.xyz;
        meshlet.Vertices[1] = debugPrimitive.Parameter2.xyz;
        meshlet.Primitives[0] = uint2(0, 1);
        meshlet.Color = debugPrimitive.Color;
    }

    else if (parameters.DebugPrimitiveType == DebugPrimitiveType::Cube)
    {
        float3 minPoint = debugPrimitive.Parameter1.xyz;
        float3 maxPoint = debugPrimitive.Parameter2.xyz;

        float xSize = maxPoint.x - minPoint.x;
        float ySize = maxPoint.y - minPoint.y;
        float zSize = maxPoint.z - minPoint.z;

        meshlet.Vertices[0] = minPoint;
        meshlet.Vertices[1] = minPoint + float3(0, 0, zSize);
        meshlet.Vertices[2] = minPoint + float3(xSize, 0, 0);
        meshlet.Vertices[3] = minPoint + float3(xSize, 0, zSize);
        meshlet.Vertices[4] = minPoint + float3(0, ySize, 0);
        meshlet.Vertices[5] = minPoint + float3(0, ySize, zSize);
        meshlet.Vertices[6] = minPoint + float3(xSize, ySize, 0);
        meshlet.Vertices[7] = minPoint + float3(xSize, ySize, zSize);

        meshlet.Primitives[0] = uint2(0, 1);
        meshlet.Primitives[1] = uint2(0, 2);
        meshlet.Primitives[2] = uint2(1, 3);
        meshlet.Primitives[3] = uint2(2, 3);

        meshlet.Primitives[4] = uint2(4, 5);
        meshlet.Primitives[5] = uint2(4, 6);
        meshlet.Primitives[6] = uint2(5, 7);
        meshlet.Primitives[7] = uint2(6, 7);

        meshlet.Primitives[8] = uint2(0, 4);
        meshlet.Primitives[9] = uint2(2, 6);
        meshlet.Primitives[10] = uint2(3, 7);
        meshlet.Primitives[11] = uint2(1, 5);

        meshlet.Color = debugPrimitive.Color;
    }

    else if (parameters.DebugPrimitiveType == DebugPrimitiveType::Sphere)
    {
        float3 position = debugPrimitive.Parameter1.xyz;
        float radius = debugPrimitive.Parameter2.x;
        const uint steps = 30;

        float stepAngle = (360.0 * PI / 180.0) / steps;

        uint currentVertexCount = 0;
        uint currentPrimitiveCount = 0;

        // TODO: optimize the same calculations with waves?
        [unroll]
        for (uint i = 0; i < steps; i++)
        {
            meshlet.Primitives[currentPrimitiveCount++] = uint2(i, ((i + 1) % steps));
            meshlet.Vertices[currentVertexCount++] = position + float3(0, -radius * cos(i * stepAngle), radius * sin(i * stepAngle));
        }

        uint vertexOffset = currentVertexCount;

        [unroll]
        for (i = 0; i < steps; i++)
        {
            meshlet.Primitives[currentPrimitiveCount++] = uint2(vertexOffset + i, vertexOffset + ((i + 1) % steps));
            meshlet.Vertices[currentVertexCount++] = position + float3(-radius * cos(i * stepAngle), radius * sin(i * stepAngle), 0);
        }

        vertexOffset = currentVertexCount;

        [unroll]
        for (i = 0; i < steps; i++)
        {
            meshlet.Primitives[currentPrimitiveCount++] = uint2(vertexOffset + i, vertexOffset + ((i + 1) % steps));
            meshlet.Vertices[currentVertexCount++] = position + float3(-radius * cos(i * stepAngle), 0, radius * sin(i * stepAngle));
        }

        meshlet.Color = debugPrimitive.Color;
    }

    meshletBuffer[debugPrimitiveIndex] = meshlet;

    GroupMemoryBarrierWithGroupSync();
    
    DispatchMesh(ceil((float)payload.DebugPrimitiveCount / payload.MaxDebugPrimitiveCountPerGroup), 1, 1, payload);
}

[OutputTopology("line")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId: SV_GroupID, 
              in uint groupThreadId: SV_GroupThreadID, 
              in payload Payload payload, 
              out vertices VertexOutput vertices[128], 
              out indices uint2 indices[128])
{
    uint currentDebugPrimitiveCount = min(payload.MaxDebugPrimitiveCountPerGroup, payload.DebugPrimitiveCount - groupId * payload.MaxDebugPrimitiveCountPerGroup);
    uint meshVertexCount = currentDebugPrimitiveCount * payload.VertexCount;
    uint meshPrimitiveCount = currentDebugPrimitiveCount * payload.PrimitiveCount;

    SetMeshOutputCounts(meshVertexCount, meshPrimitiveCount);

    uint baseInstanceId = payload.InstanceOffset + groupId * payload.MaxDebugPrimitiveCountPerGroup;

    uint vertexId = groupThreadId % payload.VertexCount;
    uint vertexOffset = groupThreadId / payload.VertexCount;
    uint vertexInstanceId = baseInstanceId + vertexOffset;

    if (vertexInstanceId < parameters.DebugPrimitiveCount)
    {
        Meshlet meshlet = meshletBuffer[parameters.InstanceOffset + vertexInstanceId];
        float3 worldPosition = meshlet.Vertices[vertexId];

        vertices[groupThreadId].Position = mul(mul(float4(worldPosition, 1), RenderPassParameters[0].ViewMatrix), RenderPassParameters[0].ProjectionMatrix);
        vertices[groupThreadId].Color = float4(meshlet.Color, 1);
    }

    uint primitiveId = groupThreadId % payload.PrimitiveCount;
    uint primitiveOffset = groupThreadId / payload.PrimitiveCount;
    uint primitiveInstanceId = baseInstanceId + primitiveOffset;

    if (primitiveInstanceId < parameters.DebugPrimitiveCount)
    {
        Meshlet meshlet = meshletBuffer[parameters.InstanceOffset + primitiveInstanceId];
        indices[groupThreadId] = meshlet.Primitives[primitiveId] + primitiveOffset * payload.VertexCount;
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