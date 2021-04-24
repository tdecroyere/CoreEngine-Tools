#define RootSignatureDef \
    "RootFlags(0), " \
    "DescriptorTable(SRV(t1, flags = DATA_STATIC)), " \
    "DescriptorTable(UAV(u1, flags = DATA_VOLATILE))"

Texture2D InputTexture: register(t1);
RWTexture2D<float4> OutputTexture: register(u1);

float3 ToneMapACES(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;

    return saturate((x * (a * x + b)) / ( x * ( c * x + d) + e));
}

[numthreads(8, 8, 1)]
void ToneMap(uint2 pixelCoordinates: SV_DispatchThreadID)
{                
    // TODO: Check if we can output linear values here or do we need to convert to srgb

    float exposure = 0.15;

    float3 inputSample = InputTexture[pixelCoordinates].rgb;

    inputSample = ToneMapACES(inputSample * exposure);
    //OutputTexture[pixelCoordinates] = float4(0.0f, 0.215f, 1.0f, 1.0);
    OutputTexture[pixelCoordinates] = float4(inputSample, 1.0);
}