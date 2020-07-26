#define RootSignatureDef \
    "RootFlags(0), " \
    "SRV(t0, flags = DATA_STATIC), " \
    "SRV(t1, flags = DATA_STATIC)"

#pragma pack_matrix(row_major)

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

StructuredBuffer<VertexInput> VertexBuffer: register(t0);
StructuredBuffer<RenderPass> RenderPassParameters: register(t1);

VertexOutput VertexMain(const uint vertexId: SV_VertexID, const uint instanceId: SV_InstanceID)
{
    VertexInput input = VertexBuffer[vertexId];
    VertexOutput output = (VertexOutput)0;

    float4x4 viewProjMatrix = mul(RenderPassParameters[0].ViewMatrix, RenderPassParameters[0].ProjectionMatrix);

    output.Position = mul(float4(input.Position.xyz, 1.0), viewProjMatrix);
    output.Color = input.Color;
    
    return output;
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