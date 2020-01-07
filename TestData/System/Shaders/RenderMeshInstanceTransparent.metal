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

struct BoundingBox
{
    float3 MinPoint;
    float3 MaxPoint;
};

struct GeometryPacket
{
    uint VertexBufferIndex;
    uint IndexBufferIndex;
};

struct GeometryInstance
{
    int GeometryPacketIndex;
    int StartIndex;
    int IndexCount;
    int MaterialIndex;
    int IsTransparent;
    float4x4 WorldMatrix;
    BoundingBox WorldBoundingBox;
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
    const array<const device VertexInput*, 10000> VertexBuffers [[id(3)]];
    const array<const device uint*, 10000> IndexBuffers [[id(10003)]];
    const array<const device void*, 10000> MaterialData [[id(20003)]];
    const array<texture2d<float>, 10000> MaterialTextures [[id(30003)]];
    const device int* MaterialTextureOffsets [[id(40003)]];
    command_buffer OpaqueCommandBuffer [[id(40004)]];
    command_buffer TransparentCommandBuffer [[id(40005)]];
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
    float4 Color2 [[color(1)]];
};

float3 ComputeLightContribution(float3 worldNormal)
{
    float light = 3 * saturate(dot(normalize(float3(-0.5, 0.5, 0.5)), worldNormal));
    return float3(light, light, light) + float3(0.1, 0.1, 0.1);
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

float WeightFunction(float alpha, float depth)
{
    float tmp = (1.0 - (depth * 0.99));
    (tmp *= ((tmp * tmp) * 100000.0));
    return clamp((alpha * tmp), 10.000000, 100.0);
}

[[early_fragment_tests]]
fragment PixelOutput PixelMain(VertexOutput input [[stage_in]],
                               const device void* material [[buffer(0)]],
                               const device int& materialTextureOffset [[buffer(1)]],
                               const device ShaderParameters& shaderParameters [[buffer(2)]],
                               const device GeometryInstance& geometryInstance [[buffer(3)]])
{
    PixelOutput output = {};

    const device SimpleMaterial& simpleMaterial = *((const device SimpleMaterial*)material);

    constexpr sampler texture_sampler(mag_filter::linear,
                                      min_filter::linear,
                                      mip_filter::linear, address::repeat, max_anisotropy(4));

    float3 normalVector = input.WorldNormal;
    float4 diffuseColor = simpleMaterial.DiffuseColor.a > 0 ? simpleMaterial.DiffuseColor : float4(1, 1, 1, 1);

    if (simpleMaterial.NormalTexture > 0)
    {
        texture2d<float> normalTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.NormalTexture);
        float4 textureColor = normalTexture.sample(texture_sampler, input.TextureCoordinates);

        if (textureColor.a > 0)
        {
            float3 nrmBaseNormal = normalize(input.WorldNormal);
            float3 surfaceGradient = float3(TspaceNormalToDerivative(textureColor.xy), 0);
            normalVector = ResolveNormalFromSurfaceGradient(nrmBaseNormal, surfaceGradient);

            //output.Color = float4((normalVector * 0.5) + float3(0.5), 1.0);
            //output.Color = float4((nrmBaseNormal * 0.5) + float3(0.5), 1.0);
            //return output;
        }
    }

    if (simpleMaterial.DiffuseTexture > 0)
    {
        texture2d<float> diffuseTexture = GetMaterialTexture(shaderParameters, materialTextureOffset, simpleMaterial.DiffuseTexture);
        float4 textureDiffuseColor = diffuseTexture.sample(texture_sampler, input.TextureCoordinates);

        if (textureDiffuseColor.a == 1)
        {
            diffuseColor = float4(textureDiffuseColor.rgb, diffuseColor.a);
        }

        else
        {
            diffuseColor = textureDiffuseColor;
        }
    }

    if (geometryInstance.IsTransparent == 0 || diffuseColor.a == 1.0 || diffuseColor.a == 0.0)
    {
        discard_fragment();
    }
    
    float4 finalColor = float4(diffuseColor.rgb * ComputeLightContribution(normalVector), diffuseColor.a);
    float3 premultipliedColor = finalColor.rgb * float3(finalColor.a);

    float d = (input.Position.z);// / input.Position.w);
    float coverage = finalColor.a;
    float w = WeightFunction(coverage, d);

    output.Color = float4(premultipliedColor.rgb, coverage) * float4(w);
    output.Color2 = coverage;
    return output;
}

