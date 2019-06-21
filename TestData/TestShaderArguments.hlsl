struct VertexInput
{
    float3 Position: POSITION;
    float3 Normal: TexCoord0;
};

struct VertexOutput
{
    float4 Position: SV_POSITION;
    float4 Color: TexCoord0;
};
 
struct ColorPixelOutput
{
	float4 Color: SV_TARGET;
};

struct VertexShaderParameters
{
    matrix WorldMatrix;
};

cbuffer CoreEngine_RenderPassConstantBuffer : register(b0, space1)
{
    matrix ViewMatrix;
    matrix ProjectionMatrix;
};

StructuredBuffer<VertexShaderParameters> vertexShaderParameters : register(t1, space1);

VertexOutput VertexMain(const VertexInput input, uint instanceId: SV_InstanceID) 
{
    VertexOutput output = (VertexOutput)0; 

    matrix worldMatrix = vertexShaderParameters[instanceId].WorldMatrix;
    matrix worldViewProjMatrix = mul(worldMatrix, mul(ViewMatrix, ProjectionMatrix));

    output.Position = mul(float4(input.Position, 1), worldViewProjMatrix);
    output.Color = normalize(mul(float4(input.Normal, 0), worldMatrix));
    
    return output;
}

ColorPixelOutput PixelMain(const VertexOutput input)
{
    ColorPixelOutput output = (ColorPixelOutput)0;
    output.Color = input.Color * 0.5 + 0.5;
    //output.Color = float4(1, 1, 0, 1);

    return output;
}