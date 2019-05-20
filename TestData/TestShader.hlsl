cbuffer CoreEngine_RenderPassConstantBuffer : register(b1)
{
    matrix ViewMatrix;
    matrix ProjectionMatrix;
};

cbuffer CoreEngine_ObjectConstantBuffer : register(b2)
{
    matrix WorldMatrix;
};


struct VertexInput
{
    float3 Position: POSITION;
    float3 Normal: TexCoord0;
};

struct VertexOutput
{
    float4 Position: SV_POSITION;
    float4 Color: COLOR;
};
 
struct ColorPixelOutput
{
	float4 Color: SV_TARGET;
};


VertexOutput VertexMain(const VertexInput input)
{
    VertexOutput output = (VertexOutput)0;

    matrix worldViewProjMatrix = mul(WorldMatrix, mul(ViewMatrix, ProjectionMatrix));

    output.Position = mul(float4(input.Position, 1), worldViewProjMatrix);
    output.Color = float4(input.Normal, 1);
    
    return output;
}

ColorPixelOutput PixelMain(const VertexOutput input)
{
    ColorPixelOutput output = (ColorPixelOutput)0;
    output.Color = input.Color;
    //output.Color = float4(1, 1, 0, 1);

    return output;
}