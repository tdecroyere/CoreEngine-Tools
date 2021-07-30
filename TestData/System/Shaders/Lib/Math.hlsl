static const float PI = 3.14159265f;
static const float FloatMinValue = -3.40282347E+38F;
static const float FloatMaxValue = 3.40282347E+38F;

struct BoundingFrustum
{
    float4 LeftPlane;
    float4 RightPlane;
    float4 TopPlane;
    float4 BottomPlane;
    float4 NearPlane;
    float4 FarPlane;
};

struct BoundingBox
{
    float3 MinPoint;
    float3 MaxPoint;
};

struct BoundingBox2D
{
    float2 MinPoint;
    float2 MaxPoint;
};

struct BoundingSphere
{
    float3 Center;
    float Radius;
};

bool IntersectCone(float4 normalCone, BoundingSphere boundingSphere, float3 cameraPosition)
{
    float3 viewVector = boundingSphere.Center - cameraPosition;

    return dot(viewVector, normalCone.xyz) < normalCone.w * length(viewVector) + boundingSphere.Radius;
}

bool IntersectPlane(float4 plane, BoundingSphere sphere)
{
    return (dot(plane, float4(sphere.Center, 1)) < sphere.Radius);
}

bool Intersect(float4 plane, BoundingBox box)
{
    if (dot(plane, float4(box.MinPoint.x, box.MinPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MinPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MaxPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MaxPoint.y, box.MinPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MinPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MinPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MinPoint.x, box.MaxPoint.y, box.MaxPoint.z, 1)) <= 0) return true;
    if (dot(plane, float4(box.MaxPoint.x, box.MaxPoint.y, box.MaxPoint.z, 1)) <= 0) return true;

    return false;
}

bool IntersectFrustum(BoundingFrustum frustum, BoundingSphere sphere)
{
    if (!IntersectPlane(frustum.LeftPlane, sphere)) return false;
    if (!IntersectPlane(frustum.RightPlane, sphere)) return false;
    if (!IntersectPlane(frustum.TopPlane, sphere)) return false;
    if (!IntersectPlane(frustum.BottomPlane, sphere)) return false;
    if (!IntersectPlane(frustum.NearPlane, sphere)) return false;

    return true;
}

// TODO: Be carreful with that method because each lane in the wave
// will go to each tests even if the cull return false directly
bool Intersect(BoundingFrustum frustum, BoundingBox box)
{
    if (!Intersect(frustum.LeftPlane, box)) return false;
    if (!Intersect(frustum.RightPlane, box)) return false;
    if (!Intersect(frustum.TopPlane, box)) return false;
    if (!Intersect(frustum.BottomPlane, box)) return false;
    if (!Intersect(frustum.NearPlane, box)) return false;

    //if (!isnan(frustum.FarPlane.w) && !Intersect(frustum.FarPlane, box)) return false;

    return true;
}

static float3 boundingBoxOffsets[] =
{
    float3(0, 0, 0),
    float3(1, 0, 0),
    float3(0, 1, 0),
    float3(1, 1, 0),
    float3(0, 0, 1),
    float3(1, 0, 1),
    float3(0, 1, 1),
    float3(1, 1, 1)
};

BoundingBox TransformBoundingBox(BoundingBox boundingBox, float4x3 worldMatrix)
{
    BoundingBox result = (BoundingBox)0;

    float3 boundingBoxSize = boundingBox.MaxPoint - boundingBox.MinPoint;

    float3 minPoint = float3(FloatMaxValue, FloatMaxValue, FloatMaxValue);
    float3 maxPoint = float3(FloatMinValue, FloatMinValue, FloatMinValue);

    [unroll]
    for (uint i = 0; i < 8; i++)
    {
        float3 sourcePoint = boundingBox.MinPoint + boundingBoxOffsets[i] * boundingBoxSize;
        float3 transformedPoint = mul(float4(sourcePoint, 1), worldMatrix);

        minPoint = min(transformedPoint, minPoint);
        maxPoint = max(transformedPoint, maxPoint);
    }

    result.MinPoint = minPoint;
    result.MaxPoint = maxPoint;

    return result;
}

bool ProjectBoundingBox(BoundingBox boundingBox, float4x4 worldViewProjMatrix, out BoundingBox2D result, out float depth)
{
    float4x4 offsetMatrix = float4x4(0.5f, 0.0f, 0.0f, 0.0f,
                                        0.0f, -0.5f, 0.0f, 0.0f,
                                        0.0f, 0.0f, 1.0f, 0.0f,
                                        0.5f, 0.5f, 0.0f, 1.0f);

    float4x4 transformMatrix = mul(worldViewProjMatrix, offsetMatrix);
    float3 boundingBoxSize = boundingBox.MaxPoint - boundingBox.MinPoint;

    float2 minPoint = float2(FloatMaxValue, FloatMaxValue);
    float2 maxPoint = float2(FloatMinValue, FloatMinValue);

    depth = 0.0;

    [unroll]
    for (uint i = 0; i < 8; i++)
    {
        float3 sourcePoint = boundingBox.MinPoint + boundingBoxOffsets[i] * boundingBoxSize;

        float4 projectedPoint = mul(float4(sourcePoint, 1.0f), transformMatrix);
        projectedPoint /= projectedPoint.w;

        if (projectedPoint.z <= 0.0)
        {
            return false;
        }

        minPoint = (projectedPoint.z > 0.0f) ? min(projectedPoint.xy, minPoint) : minPoint;
        maxPoint = (projectedPoint.z > 0.0f) ? max(projectedPoint.xy, maxPoint) : maxPoint;

        depth = max(projectedPoint.z, depth);
    }

    result.MinPoint = minPoint;
    result.MaxPoint = maxPoint;

    return true;
}

float4x4 CreateScaleTranslation(float3 scale, float3 translation)
{
    return float4x4(scale.x, 0, 0, 0,
                    0, scale.y, 0, 0,
                    0, 0, scale.z, 0,
                    translation.x, translation.y, translation.z, 1);
}

uint hash(uint a)
{
   a = (a+0x7ed55d16) + (a<<12);
   a = (a^0xc761c23c) ^ (a>>19);
   a = (a+0x165667b1) + (a<<5);
   a = (a+0xd3a2646c) ^ (a<<9);
   a = (a+0xfd7046c5) + (a<<3);
   a = (a^0xb55a4f09) ^ (a>>16);

   return a;
}