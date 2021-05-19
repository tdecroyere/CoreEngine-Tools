#include "Common.hlsl"

#define RootSignatureDef RootSignatureDefinitionWithSampler(1, "StaticSampler(s0, filter = FILTER_MIN_MAG_MIP_POINT)")

struct ShaderParameters
{
    uint SourceTextureIndex;
};

ConstantBuffer<ShaderParameters> parameters : register(b0);
SamplerState TextureSampler: register(s0);

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
};

static float4 rectangleVertices[] =
{
    float4(-1, 1, 0, 1),
    float4(1, 1, 0, 1),
    float4(-1, -1, 0, 1),
    float4(1, -1, 0, 1)
};

static float2 rectangleTextureCoordinates[] =
{
    float2(0, 0),
    float2(1, 0),
    float2(0, 1),
    float2(1, 1)
};

static uint3 rectangleIndices[] =
{
    uint3(0, 1, 2),
    uint3(2, 1, 3)
};

[OutputTopology("triangle")]
[NumThreads(128, 1, 1)]
void MeshMain(in uint groupId : SV_GroupID, in uint groupThreadId : SV_GroupThreadID, out vertices VertexOutput vertices[128], out indices uint3 indices[128])
{
    const uint meshVertexCount = 4;
    const uint meshPrimitiveCount = 2;

    SetMeshOutputCounts(meshVertexCount, meshPrimitiveCount);

    if (groupThreadId < meshVertexCount)
    {
        vertices[groupThreadId].Position = rectangleVertices[groupThreadId];
        vertices[groupThreadId].TextureCoordinates = rectangleTextureCoordinates[groupThreadId];
    }

    if (groupThreadId < meshPrimitiveCount)
    {
        indices[groupThreadId] = rectangleIndices[groupThreadId];
    }
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    Texture2D diffuseTexture = textures[parameters.SourceTextureIndex];
    output.Color = diffuseTexture.Sample(TextureSampler, input.TextureCoordinates);
    
    return output; 
}