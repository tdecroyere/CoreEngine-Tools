#define RootSignatureDefinitionWithSampler(numParameters, samplerDefinition) \
    "RootFlags(0), " \
    "RootConstants(num32BitConstants="#numParameters", b0)," \
    "DescriptorTable(SRV(t0, space = 0, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    "DescriptorTable(SRV(t0, space = 1, numDescriptors = unbounded, flags = DESCRIPTORS_VOLATILE))," \
    samplerDefinition

#define RootSignatureDefinition(numParameters) RootSignatureDefinitionWithSampler(numParameters, "")

static const float PI = 3.14159265f;

ByteAddressBuffer buffers[]: register(t0, space0);
Texture2D textures[]: register(t0, space1);

// TODO: Enable library compilation and linkage