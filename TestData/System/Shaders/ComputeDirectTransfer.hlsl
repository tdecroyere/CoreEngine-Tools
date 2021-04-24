#define RootSignatureDef \
    "RootFlags(0), " \
    "DescriptorTable(SRV(t0, numDescriptors = 1, flags = DESCRIPTORS_VOLATILE))," \
    "StaticSampler(s0," \
                 "filter = FILTER_MIN_MAG_MIP_POINT)"

Texture2D InputTexture: register(t0);
SamplerState TextureSampler: register(s0);

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
};

VertexOutput VertexMain(const uint vertexId: SV_VertexID)
{
    VertexOutput output = (VertexOutput)0;

    if ((vertexId) % 4 == 0)
    {
        output.Position = float4(-1, 1, 0, 1);
        output.TextureCoordinates = float2(0, 0);
    }

    else if ((vertexId) % 4 == 1)
    {
        output.Position = float4(1, 1, 0, 1);
        output.TextureCoordinates = float2(1, 0);
    }

    else if ((vertexId) % 4 == 2)
    {
        output.Position = float4(-1, -1, 0, 1);
        output.TextureCoordinates = float2(0, 1);
    }

    else if ((vertexId) % 4 == 3)
    {
        output.Position = float4(1, -1, 0, 1);
        output.TextureCoordinates = float2(1, 1);
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

    float4 inputColor = InputTexture.Sample(TextureSampler, input.TextureCoordinates);

    // int width;
    // int height;

    // InputTexture.GetDimensions(width, height);

    // uint2 pixel = uint2(input.TextureCoordinates.x * width, input.TextureCoordinates.y * height);  

    // float4 inputColor = InputTexture[pixel];
    output.Color = inputColor;
    return output; 
}