#define RootSignatureDef \
    "RootFlags(0), " \
    "SRV(t0, flags = DATA_STATIC), " \
    "SRV(t1, flags = DATA_STATIC), " \
    "SRV(t2, flags = DATA_STATIC), " \
    "DescriptorTable(SRV(t3, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE)), " \
    "StaticSampler(s0," \
                 "filter = FILTER_MIN_MAG_MIP_LINEAR)"

#pragma pack_matrix(row_major)

struct VertexInput
{
    float2 Position;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
    nointerpolation uint InstanceId: TEXCOORD1;
    nointerpolation bool IsOpaque: TEXCOORD2;
};

struct RenderPass
{
    float4x4 ProjectionMatrix;
};

struct RectangleSurface
{
    float4x4 WorldMatrix;
    float2 TextureMinPoint;
    float2 TextureMaxPoint;
    int TextureIndex;
    bool IsOpaque;

    // TODO: Find a common solution for alignment issues
    int Reserved4;
    int Reserved5;
};

StructuredBuffer<VertexInput> VertexBuffer: register(t0);
StructuredBuffer<RenderPass> RenderPassParameters: register(t1);
StructuredBuffer<RectangleSurface> RectangleSurfaces: register(t2);
Texture2D SurfaceTextures[100]: register(t3);
SamplerState TextureSampler: register(s0);

VertexOutput VertexMain(const uint vertexId: SV_VertexID, const uint instanceId: SV_InstanceID)
{
    VertexOutput output = (VertexOutput)0;

    VertexInput input = VertexBuffer[vertexId];

    float4x4 worldMatrix = RectangleSurfaces[instanceId].WorldMatrix;
    float4x4 projectionMatrix = RenderPassParameters[0].ProjectionMatrix;

    output.Position = mul(mul(float4(input.Position, 0.0, 1.0), worldMatrix), projectionMatrix);
    output.InstanceId = instanceId;

    float2 minPoint = RectangleSurfaces[instanceId].TextureMinPoint;
    float2 maxPoint = RectangleSurfaces[instanceId].TextureMaxPoint;

    if ((vertexId) % 4 == 0)
    {
        output.TextureCoordinates = float2(minPoint.x, minPoint.y);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.TextureCoordinates = float2(maxPoint.x, minPoint.y);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.TextureCoordinates = float2(minPoint.x, maxPoint.y);
    }

    else if ((vertexId) % 4 == 3)
    {
        output.TextureCoordinates = float2(maxPoint.x, maxPoint.y);
    }

    output.IsOpaque = RectangleSurfaces[instanceId].IsOpaque;
    
    return output;
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