#define RootSignatureDef "RootFlags(ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT), " \
              "CBV(b0, space = 1), " \
              "SRV(t1, space = 1), " \
              "SRV(t2, space = 1)"

//#include "TestSurfaceShader.hlsl"
                             
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

struct CoreEngine_RenderPassParameters
{
    matrix ViewMatrix;
    matrix ProjectionMatrix;
}; 

struct MaterialParameters
{
    float3 Color;
};

struct LightParameters
{
    float3 Position;
    float3 Color;
};

// TODO: Make a system to avoid copying instance parameters of static objects every frames
struct InstanceParameters
{
    matrix WorldMatrix;
};

struct VertexShaderParameters
{
    uint objectPropertyIndex;
};

// TODO: Hide the definition of register spaces
ConstantBuffer<CoreEngine_RenderPassParameters> renderPassParameters : register(b0, space1);
StructuredBuffer<InstanceParameters> instanceParameters : register(t1, space1);
StructuredBuffer<VertexShaderParameters> vertexShaderParameters : register(t2, space1);

VertexOutput VertexMain(const VertexInput input, uint instanceId: SV_InstanceID) 
{
    VertexOutput output = (VertexOutput)0; 

    int objectPropertyIndex = vertexShaderParameters[instanceId].objectPropertyIndex;

    matrix worldMatrix = instanceParameters[objectPropertyIndex].WorldMatrix;
    matrix worldViewProjMatrix = mul(worldMatrix, mul(renderPassParameters.ViewMatrix, renderPassParameters.ProjectionMatrix));

    output.Position = mul(float4(input.Position, 1), worldViewProjMatrix);
    output.Color = normalize(mul(float4(input.Normal, 0), worldMatrix));
    
    return output;
}

ColorPixelOutput PixelMain(const VertexOutput input)
{
    ColorPixelOutput output = (ColorPixelOutput)0;
 
    //output.Color = SurfaceShader(input.Color);
    output.Color = input.Color * 0.5 + 0.5;
    //output.Color = float4(1, 1, 0, 1);

    return output;
}