#define RootSignatureDef \
    "RootFlags(CBV_SRV_UAV_HEAP_DIRECTLY_INDEXED), " \
    "RootConstants(num32BitConstants=3, b0)"
    // "StaticSampler(s0, filter = FILTER_MIN_MAG_MIP_POINT)"

#pragma pack_matrix(row_major)

struct ShaderParameters
{
    uint VertexBufferIndex;
    uint RectangleSurfacesIndex;
    uint RenderPassIndex;
};

struct VertexInput
{
    float2 Position;
    float2 TextureCoordinates;
};

struct VertexOutput
{
    float4 Position: SV_Position;
    float2 TextureCoordinates: TEXCOORD0;
    nointerpolation uint InstanceId: TEXCOORD1;
    nointerpolation bool IsOpaque: TEXCOORD2;
};

struct RenderPass
{
    float4x4 ProjectionMatrix;
};

struct RectangleSurface
{
    float4 Color;
    // float4x4 WorldMatrix;
    // float2 TextureMinPoint;
    // float2 TextureMaxPoint;
    // int TextureIndex;
    // bool IsOpaque;

    // TODO: Find a common solution for alignment issues
    // int Reserved4;
    // int Reserved5;
};

ConstantBuffer<ShaderParameters> parameters : register(b0);
// SamplerState TextureSampler: register(s0);

float4x4 CreateOrthographicMatrixOffCenter(float minPlaneX, float maxPlaneX, float minPlaneY, float maxPlaneY, float minPlaneZ, float maxPlaneZ)
{
    float4x4 result = (float4x4)0;

    result._11 = 2.0 / (maxPlaneX - minPlaneX);
    result._22 = 2.0f / (maxPlaneY - minPlaneY);
    result._33 = 1.0f / (maxPlaneZ - minPlaneZ);

    //result._41 = (minPlaneX + maxPlaneX) / (minPlaneX - maxPlaneX);
    //result._42 = (minPlaneY + maxPlaneY) / (minPlaneY - maxPlaneY);
    // result._43 = minPlaneZ / (minPlaneZ - maxPlaneZ);
    result._44 = 1.0f;

    return result;
}

float4x4 CreateScale(float scale)
{
    float4x4 result = (float4x4)0;

    result._11 = scale;
    result._22 = scale;
    result._33 = 1.0f;

    result._44 = 1.0f;

    return result;
}

float4x4 CreateTranslation(float x, float y, float z)
{
    float4x4 result = (float4x4)0;

    result._11 = 1.0;
    result._22 = 1.0;
    result._33 = 1.0f;

    result._41 = x;
    result._42 = y;
    result._43 = z;
    result._44 = 1.0f;

    return result;
}

VertexOutput VertexMain(const uint vertexId: SV_VertexID, const uint instanceId: SV_InstanceID)
{
    VertexOutput output = (VertexOutput)0;

    // StructuredBuffer<VertexInput> vertexBuffer = ResourceDescriptorHeap[parameters.VertexBufferIndex];
    // VertexInput input = vertexBuffer[vertexId];

    // StructuredBuffer<RenderPass> renderPassParameters = ResourceDescriptorHeap[parameters.RenderPassIndex];

    // StructuredBuffer<RectangleSurface> rectangleSurfaces = ResourceDescriptorHeap[parameters.RectangleSurfacesIndex];
    // RectangleSurface rectangleSurface = rectangleSurfaces[instanceId];

    float4x4 worldMatrix = worldMatrix = instanceId == 0 ? CreateScale(100) : CreateScale(50);//rectangleSurface.WorldMatrix;
    float4x4 projectionMatrix = CreateOrthographicMatrixOffCenter(0, 1280, 0, 720, 0, 1);//renderPassParameters[0].ProjectionMatrix;

    // output.Position = mul(mul(float4(input.Position, 0.0, 1.0), worldMatrix), projectionMatrix);
    // output.Position = mul(float4(input.Position, 0.0, 1.0), projectionMatrix);

    float2 minPoint = 0;//rectangleSurface.TextureMinPoint;
    float2 maxPoint = 1;//rectangleSurface.TextureMaxPoint;

    if (vertexId == 0)
    {
        output.Position = mul(mul(float4(0, 1, 0, 1), worldMatrix), projectionMatrix);
        output.TextureCoordinates = float2(minPoint.x, minPoint.y);
    }

    else if (vertexId == 1)
    {
        output.Position = mul(mul(float4(1, 1, 0, 1), worldMatrix), projectionMatrix);
        output.TextureCoordinates = float2(maxPoint.x, minPoint.y);
    }

    else if (vertexId == 2)
    {
        output.Position = mul(mul(float4(0, 0, 0, 1), worldMatrix), projectionMatrix);
        output.TextureCoordinates = float2(minPoint.x, maxPoint.y);
    }

    else if (vertexId == 3)
    {
        output.Position = mul(mul(float4(1, 0, 0, 1), worldMatrix), projectionMatrix);
        output.TextureCoordinates = float2(maxPoint.x, maxPoint.y);
    }

    output.InstanceId = instanceId;
    // output.IsOpaque = rectangleSurface.IsOpaque;
    
    return output;
}

struct PixelOutput
{
    float4 Color: SV_TARGET0;
};

PixelOutput PixelMain(const VertexOutput input)
{
    PixelOutput output = (PixelOutput)0;

    // uint index = parameters.RectangleSurfacesIndex;
    // StructuredBuffer<RectangleSurface> rectangleSurfaces = ResourceDescriptorHeap[NonUniformResourceIndex(index)];
    // RectangleSurface rectangle = rectangleSurfaces[0];
    ByteAddressBuffer rectangleSurfaces = ResourceDescriptorHeap[parameters.RectangleSurfacesIndex];
    RectangleSurface rectangle = rectangleSurfaces.Load<RectangleSurface>(32);
    // // float4 color = rectangleSurfaces.Load4(0);
    output.Color = rectangle.Color;
    // output.Color = color;

    // if (input.InstanceId > 0)
    // {
    //     output.Color = float4(0, 0, 1, 1);
    // }

    // else
    // {
    //     output.Color = float4(1, 0, 0, 1);
    // }
    return output;

    // StructuredBuffer<RectangleSurface> rectangleSurfaces = ResourceDescriptorHeap[parameters.RectangleSurfacesIndex];
    // int textureIndex = rectangleSurfaces[input.InstanceId].TextureIndex;

    // Texture2D diffuseTexture = ResourceDescriptorHeap[NonUniformResourceIndex(textureIndex)];
    // float4 textureColor = diffuseTexture.Sample(TextureSampler, input.TextureCoordinates);

    // if (!input.IsOpaque)
    // {
    //     if (textureColor.a == 0)
    //     {
    //         discard;
    //     }
        
    //     output.Color = textureColor;
    // }

    // else
    // {
    //     output.Color = float4(textureColor.rgb, 1);
    // }

    // return output; 
}