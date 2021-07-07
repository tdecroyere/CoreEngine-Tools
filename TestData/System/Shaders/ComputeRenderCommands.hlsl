#include "CoreEngine.hlsl"

#define RootSignatureDef RootSignatureDefinition(2)

struct ShaderParameters
{
    uint MeshBufferIndex;
    uint MeshInstanceBufferIndex;
};

[[vk::push_constant]]
ConstantBuffer<ShaderParameters> parameters : register(b0);

[NumThreads(WAVE_SIZE, 1, 1)]
void ComputeMain(in uint groupId: SV_GroupID, in uint groupThreadId: SV_GroupThreadID)
{

}