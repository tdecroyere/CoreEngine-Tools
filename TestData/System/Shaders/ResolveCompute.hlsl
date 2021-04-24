#define RootSignatureDef \
    "RootFlags(0), " \
    "DescriptorTable(SRV(t1, flags = DATA_VOLATILE)), " \
    "DescriptorTable(SRV(t2, flags = DATA_VOLATILE)), " \
    "DescriptorTable(SRV(t3, flags = DATA_VOLATILE)), " \
    "DescriptorTable(UAV(u1, flags = DATA_VOLATILE))"

Texture2D OpaqueTexture: register(t1);
Texture2D TransparentTexture: register(t2);
Texture2D TransparentRevealageTexture: register(t3);
RWTexture2D<float4> OutputTexture: register(u1);

[numthreads(8, 8, 1)]
void Resolve(uint2 pixelCoordinates: SV_DispatchThreadID)
{                
    float3 inputSample = OpaqueTexture[pixelCoordinates].rgb;
    OutputTexture[pixelCoordinates] = float4(inputSample, 1.0);
}