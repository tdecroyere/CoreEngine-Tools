#define RootSignatureDef \
    "RootFlags(0)," \
    "SRV(t0, flags = DATA_STATIC), " \

    // "RootFlags(0), " \
    // "SRV(t0, flags = DATA_STATIC), " \
    // "SRV(t1, flags = DATA_STATIC), " \
    // "SRV(t2, flags = DATA_STATIC), " \
    // "DescriptorTable(SRV(t3, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE)), " \
    // "StaticSampler(s0," \
    //              "filter = FILTER_MIN_MAG_MIP_LINEAR)"

#pragma pack_matrix(row_major)

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

struct VertexInput
{
    float3 Position;
    float3 Normal;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float3 WorldPosition: TEXCOORD0;
    float3 WorldNormal: TEXCOORD1;
    float2 TextureCoordinates: TEXCOORD2;
    float3 ViewDirection: TEXCOORD3;
};

StructuredBuffer<Camera> Cameras: register(t0);

VertexOutput VertexMain(const uint vertexId: SV_VertexID, const uint instanceId: SV_InstanceID)
{
    VertexOutput output = (VertexOutput)0;
    float4x4 viewProjMatrix = Cameras[0].ViewProjectionMatrix;

    if ((vertexId) % 4 == 0)
    {
        
        output.Position = mul(float4(-1, 1, 0, 1), viewProjMatrix);
        output.TextureCoordinates = float2(0, 0);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.Position = mul(float4(1, 1, 0, 1), viewProjMatrix);
        output.TextureCoordinates = float2(1, 0);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.Position = mul(float4(-1, -1, 0, 1), viewProjMatrix);
        output.TextureCoordinates = float2(0, 1);
    }

    return output;
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    output.Color = float4(1, 1, 0, 1);

    return output; 
}