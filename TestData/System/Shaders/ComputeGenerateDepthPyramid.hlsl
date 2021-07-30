#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinitionWithSampler(4, "StaticSampler(s0, space = 4, filter = FILTER_MINIMUM_MIN_MAG_LINEAR_MIP_POINT, addressU = TEXTURE_ADDRESS_CLAMP, addressV = TEXTURE_ADDRESS_CLAMP)")

struct ShaderParameters
{
    uint DepthBufferIndex;
    uint DepthPyramidBufferIndex;
    uint DepthPyramidBufferWidth;
    uint DepthPyramidBufferHeight;
};

[[vk::push_constant]]
ConstantBuffer<ShaderParameters> parameters : register(b0);
SamplerState TextureSampler: register(s0, space4);

[NumThreads(8, 8, 1)]
void ComputeMain(in uint2 threadId: SV_DispatchThreadId)
{
    Texture2D depthBuffer = textures[parameters.DepthBufferIndex];
    float minDepthValue = depthBuffer.SampleLevel(TextureSampler, (threadId + 0.5) / float2(parameters.DepthPyramidBufferWidth, parameters.DepthPyramidBufferHeight), 0).r;
    
    RWTexture2D<float4> depthPyramidBuffer = rwTextures[parameters.DepthPyramidBufferIndex];
    depthPyramidBuffer[threadId] = float4(minDepthValue, 0, 0, 1);
}
