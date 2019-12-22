#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct VertexInput
{
    float3 Position;
    float3 Normal;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position [[position]];
    float3 WorldNormal;
    float3 Normal;
    float2 TextureCoordinates;
};

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct GeometryInstance
{
    uint GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    float4x4 WorldMatrix;
};

struct Camera
{
    float4x4 ViewMatrix;
    float4x4 ProjectionMatrix;
};

struct SceneProperties
{
    Camera ActiveCamera;
    Camera DebugCamera;
    bool isDebugCameraActive;
};

struct ShaderParameters
{
    const device SceneProperties& SceneProperties [[id(0)]];
    const device GeometryPacket* GeometryPackets [[id(1)]];
    const device GeometryInstance* GeometryInstances [[id(2)]];
    command_buffer CommandBuffer [[id(3)]];
    const array<const device VertexInput*, 10000> VertexBuffers [[id(4)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10004)]];
    const array<const device void*, 10000> MaterialData [[id(20004)]];
    const array<texture2d<float>, 10000> MaterialTextures [[id(30004)]];
    const device int* MaterialTextureOffsets [[id(40004)]];
};

struct SimpleMaterial
{
    float4 DiffuseColor;
    int DiffuseTexture;
    int NormalTexture;
};

vertex VertexOutput VertexMain(const uint vertexId [[vertex_id]],
                               const uint instanceId [[instance_id]],
                               const device VertexInput* vertexBuffer [[buffer(0)]],
                               const device Camera& camera [[buffer(1)]],
                               const device GeometryInstance& geometryInstance [[buffer(2)]])
{
    VertexInput input = vertexBuffer[vertexId];
    VertexOutput output = {};

    float4x4 worldMatrix = geometryInstance.WorldMatrix;
    float4x4 worldViewProjMatrix = (camera.ProjectionMatrix * camera.ViewMatrix) * worldMatrix;

    output.Position = worldViewProjMatrix * float4(input.Position, 1.0);
    output.WorldNormal = normalize(worldMatrix * float4(input.Normal, 0.0)).xyz;
    output.Normal = input.Normal;
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

struct PixelOutput
{
    float4 Color [[color(0)]];
};

float4 ComputeLightContribution(float3 worldNormal)
{
    float light = saturate(dot(normalize(float3(-0.5, 0.5, 0.5)), worldNormal));
    return float4(light, light, light, 1.0) + float4(0.1, 0.1, 0.1, 1.0);
}

texture2d<float> GetMaterialTexture(const device ShaderParameters& shaderParameters, int materialTextureOffset, int materialTextureIndex)
{
    return shaderParameters.MaterialTextures[materialTextureOffset + (materialTextureIndex - 1)];
}

float3 ResolveNormalFromSurfaceGradient(float3 nrmBaseNormal, float3 surfGrad)
{
    float resolveSign = 1;
// resolve sign +/-1. Should only be -1 for double sided
// materials when viewing the back-face and the mode is // set to flip.
    return normalize(nrmBaseNormal - resolveSign*surfGrad); 
}

// input: vM is channels .xy of a tangent space normal in [-1;1] // out: convert vM to a derivative
float2 TspaceNormalToDerivative(float2 vM) 
{
    const float fS = 1.0/(128*128); 
    float2 vMsq = vM*vM;
    const float mz_sq = 1-vMsq.x-vMsq.y;
    const float maxcompxy_sq = fS*max(vMsq.x,vMsq.y);
    const float z_inv = rsqrt( max(mz_sq,maxcompxy_sq) );

    return -z_inv * float2(vM.x, -vM.y);
}

fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               const device SimpleMaterial& material [[buffer(0)]],
                               const device int& materialTextureOffset [[buffer(1)]],
                               const device ShaderParameters& shaderParameters [[buffer(2)]],
                               const device GeometryInstance& geometryInstance [[buffer(3)]])
{
    PixelOutput output = {};

    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat);

    float3 normalVector = input.WorldNormal;
    float4 diffuseColor = material.DiffuseColor.a > 0 ? material.DiffuseColor : float4(1, 1, 1, 1);

    if (material.NormalTexture > 0)
    {
        texture2d<float> normalTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, material.NormalTexture);
        float4 textureColor = normalTexture.sample(texture_sampler, input.TextureCoordinates);

        if (textureColor.a > 0)
        {
            float3 nrmBaseNormal = normalize(input.WorldNormal);
            float3 surfaceGradient = float3(TspaceNormalToDerivative(textureColor.xy), 0);
            normalVector = ResolveNormalFromSurfaceGradient(nrmBaseNormal, surfaceGradient);

            //output.Color = float4((normalVector * 0.5) + float3(0.5), 1.0);
            //output.Color = float4((nrmBaseNormal * 0.5) + float3(0.5), 1.0);
        }
    }

    if (material.DiffuseTexture > 0)
    {
        texture2d<float> diffuseTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, material.DiffuseTexture);
        diffuseColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);
    }

    if (diffuseColor.a == 0)
    {
        discard_fragment();
    }

    output.Color = diffuseColor * ComputeLightContribution(normalVector);

    return output;
}

