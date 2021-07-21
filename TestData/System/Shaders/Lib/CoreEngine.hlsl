#define RootSignatureDefinitionWithSampler(numParameters, samplerDefinition) \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants="#numParameters", b0)," \
    "DescriptorTable(SRV(t0, space = 0, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    "DescriptorTable(SRV(t0, space = 1, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    "DescriptorTable(UAV(u0, space = 2, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    "DescriptorTable(UAV(u0, space = 3, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    samplerDefinition

#define RootSignatureDefinition(numParameters) RootSignatureDefinitionWithSampler(numParameters, "")

ByteAddressBuffer buffers[]: register(t0, space0);
Texture2D textures[]: register(t0, space1);
RWByteAddressBuffer rwBuffers[]: register(u0, space2);
RWTexture2D<float4> rwTextures[]: register(u0, space3);

#define WAVE_SIZE 32

// TODO: Enable library compilation and linkage

#include "Math.hlsl"
#include "Mesh.hlsl"
