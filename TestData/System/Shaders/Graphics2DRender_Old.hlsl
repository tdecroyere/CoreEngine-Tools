#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinitionWithSampler(2, "StaticSampler(s0, space = 2, filter = FILTER_MIN_MAG_MIP_POINT)")

struct ShaderParameters
{
    uint RectangleCount;
    uint RectangleSurfacesBuffer;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
    nointerpolation uint InstanceId: TEXCOORD1;
    nointerpolation uint IsOpaque: TEXCOORD2;
};

struct RectangleSurface
{
    float4x4 WorldViewProjMatrix;
    float2 TextureMinPoint;
    float2 TextureMaxPoint;
    uint TextureIndex;
    uint IsOpaque;
};

[[vk::push_constant]]
ConstantBuffer<ShaderParameters> parameters : register(b0);
SamplerState TextureSampler: register(s0, space2);

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
[NumThreads(32, 1, 1)]
void MeshMain(in uint groupId : SV_GroupID, in uint groupThreadId : SV_GroupThreadID, out vertices VertexOutput vertices[128], out indices uint3 indices[128])
{
    const uint surfaceVertexCount = 4;
    const uint surfacePrimitiveCount = 2;
    const uint maxSurfaceCount = 16;

    uint currentSurfaceCount = min(maxSurfaceCount, parameters.RectangleCount - groupId * maxSurfaceCount);
    uint meshVertexCount = currentSurfaceCount * surfaceVertexCount;
    uint meshPrimitiveCount = currentSurfaceCount * surfacePrimitiveCount;

    SetMeshOutputCounts(meshVertexCount, meshPrimitiveCount);

    uint currentGroupThreadId = groupThreadId * 2;

    uint vertexId = currentGroupThreadId % surfaceVertexCount;
    uint vertexOffset = currentGroupThreadId / surfaceVertexCount;
    uint vertexInstanceId = groupId * maxSurfaceCount + vertexOffset;

    if (vertexInstanceId < parameters.RectangleCount)
    {
        ByteAddressBuffer rectangleSurfaces = buffers[parameters.RectangleSurfacesBuffer];
        RectangleSurface rectangle = rectangleSurfaces.Load<RectangleSurface>(vertexInstanceId * sizeof(RectangleSurface));

        float2 minPoint = rectangle.TextureMinPoint;
        float2 maxPoint = rectangle.TextureMaxPoint;

        float4 rectangleTextureCoordinates = rectangleTextureCoordinatesWeights[vertexId];

        vertices[currentGroupThreadId].Position = mul(rectangleVertices[vertexId], rectangle.WorldViewProjMatrix);
        vertices[currentGroupThreadId].TextureCoordinates = float2(minPoint.x * rectangleTextureCoordinates.x + maxPoint.x * rectangleTextureCoordinates.z, minPoint.y * rectangleTextureCoordinates.y + maxPoint.y * rectangleTextureCoordinates.w);
        vertices[currentGroupThreadId].InstanceId = vertexInstanceId;
        vertices[currentGroupThreadId].IsOpaque = rectangle.IsOpaque;

        rectangleTextureCoordinates = rectangleTextureCoordinatesWeights[vertexId + 1];

        vertices[currentGroupThreadId + 1].Position = mul(rectangleVertices[vertexId + 1], rectangle.WorldViewProjMatrix);
        vertices[currentGroupThreadId + 1].TextureCoordinates = float2(minPoint.x * rectangleTextureCoordinates.x + maxPoint.x * rectangleTextureCoordinates.z, minPoint.y * rectangleTextureCoordinates.y + maxPoint.y * rectangleTextureCoordinates.w);
        vertices[currentGroupThreadId + 1].InstanceId = vertexInstanceId;
        vertices[currentGroupThreadId + 1].IsOpaque = rectangle.IsOpaque;
    }
  
    uint primitiveId = currentGroupThreadId % surfacePrimitiveCount;
    uint primitiveOffset = currentGroupThreadId / surfacePrimitiveCount;
    uint primitiveInstanceId = groupId * maxSurfaceCount + primitiveOffset;

    if (primitiveInstanceId < parameters.RectangleCount)
    {
        indices[currentGroupThreadId] = rectangleIndices[primitiveId] + primitiveOffset * surfaceVertexCount;
        indices[currentGroupThreadId + 1] = rectangleIndices[primitiveId + 1] + primitiveOffset * surfaceVertexCount;
    }
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    ByteAddressBuffer rectangleSurfaces = buffers[parameters.RectangleSurfacesBuffer];
    RectangleSurface rectangle = rectangleSurfaces.Load<RectangleSurface>(input.InstanceId * sizeof(RectangleSurface));

    int textureIndex = rectangle.TextureIndex;
    Texture2D diffuseTexture = textures[textureIndex];

    float4 textureColor = diffuseTexture.Sample(TextureSampler, input.TextureCoordinates);

    if (input.IsOpaque == 0)
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