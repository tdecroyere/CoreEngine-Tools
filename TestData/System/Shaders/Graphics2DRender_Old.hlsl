#define RootSignatureDef \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants=1, b0)," \
    "SRV(t0, flags = DATA_STATIC), " \
    "DescriptorTable(SRV(t1, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE)), " \
    "StaticSampler(s0, filter = FILTER_MIN_MAG_MIP_POINT)"

#pragma pack_matrix(row_major)

struct ShaderParameters
{
    uint RectangleCount;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
    nointerpolation uint InstanceId: TEXCOORD1;
    nointerpolation bool IsOpaque: TEXCOORD2;
};

struct RectangleSurface
{
    float4x4 WorldViewProjMatrix;
    float2 TextureMinPoint;
    float2 TextureMaxPoint;
    uint TextureIndex;
    bool IsOpaque;
    uint2 Padding;
};

ConstantBuffer<ShaderParameters> parameters : register(b0);
StructuredBuffer<RectangleSurface> RectangleSurfaces: register(t0);
Texture2D SurfaceTextures[100]: register(t1);
SamplerState TextureSampler: register(s0);

static float4 rectangleVertices[] =
{
    float4(0, 0, 0, 1),
    float4(1, 0, 0, 1),
    float4(0, 1, 0, 1),
    float4(1, 1, 0, 1)
};

static uint3 rectangleIndices[] =
{
    uint3(0, 1, 2),
    uint3(2, 1, 3)
};

static float4 rectangleTextureCoordinatesWeights[] =
{
    float4(1, 1, 0, 0),
    float4(0, 1, 1, 0),
    float4(1, 0, 0, 1),
    float4(0, 0, 1, 1)
};

[OutputTopology("triangle")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId : SV_GroupID, in uint groupThreadId : SV_GroupThreadID, out vertices VertexOutput vertices[128], out indices uint3 indices[128])
{
    const uint surfaceVertexCount = 4;
    const uint surfacePrimitiveCount = 2;
    const uint maxSurfaceCount = 32;

    uint currentSurfaceCount = min(maxSurfaceCount, parameters.RectangleCount - groupId * maxSurfaceCount);
    uint meshVertexCount = currentSurfaceCount * surfaceVertexCount;
    uint meshPrimitiveCount = currentSurfaceCount * surfacePrimitiveCount;

    SetMeshOutputCounts(meshVertexCount, meshPrimitiveCount);

    uint vertexId = groupThreadId % surfaceVertexCount;
    uint vertexOffset = groupThreadId / surfaceVertexCount;
    uint vertexInstanceId = groupId * maxSurfaceCount + vertexOffset;

    if (vertexInstanceId < parameters.RectangleCount)
    {
        RectangleSurface rectangle = RectangleSurfaces[vertexInstanceId];

        float2 minPoint = rectangle.TextureMinPoint;
        float2 maxPoint = rectangle.TextureMaxPoint;

        float4 rectangleTextureCoordinates = rectangleTextureCoordinatesWeights[vertexId];

        vertices[groupThreadId].Position = mul(rectangleVertices[vertexId], rectangle.WorldViewProjMatrix);
        vertices[groupThreadId].TextureCoordinates = float2(minPoint.x * rectangleTextureCoordinates.x + maxPoint.x * rectangleTextureCoordinates.z, minPoint.y * rectangleTextureCoordinates.y + maxPoint.y * rectangleTextureCoordinates.w);
        vertices[groupThreadId].InstanceId = vertexInstanceId;
        vertices[groupThreadId].IsOpaque = rectangle.IsOpaque;
    }
  
    uint primitiveId = groupThreadId % surfacePrimitiveCount;
    uint primitiveOffset = groupThreadId / surfacePrimitiveCount;
    uint primitiveInstanceId = groupId * maxSurfaceCount + primitiveOffset;

    if (primitiveInstanceId < parameters.RectangleCount)
    {
        indices[groupThreadId] = rectangleIndices[primitiveId] + primitiveOffset * surfaceVertexCount;
    }
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    int textureIndex = RectangleSurfaces[input.InstanceId].TextureIndex;
    Texture2D diffuseTexture = SurfaceTextures[textureIndex];

    float4 textureColor = diffuseTexture.Sample(TextureSampler, input.TextureCoordinates);

    if (!input.IsOpaque)
    {
        if (textureColor.a == 0)
        {
            discard;
        }
        
        output.Color = textureColor;
    }

    else
    {
        output.Color = float4(textureColor.rgb, 1);
    }

    return output; 
}