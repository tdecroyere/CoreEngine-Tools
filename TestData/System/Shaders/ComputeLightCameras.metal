#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct InputParameters
{
    const device Light* Lights [[id(0)]];
    device Camera* Cameras [[id(1)]];
};

float4x4 OrthographicProjection(float minPlaneX, float maxPlaneX, float minPlaneY, float maxPlaneY, float minPlaneZ, float maxPlaneZ)
{
    float4x4 result = float4x4();

    result[0][0] = 2.0f / (maxPlaneX - minPlaneX);
    result[1][1] = -2.0f / (maxPlaneY - minPlaneY);
    result[2][2] = 1.0f / (maxPlaneZ - minPlaneZ);

    result[3][0] = (minPlaneX + maxPlaneX) / (minPlaneX - maxPlaneX);
    result[3][1] = -(minPlaneY + maxPlaneY) / (minPlaneY - maxPlaneY);
    result[3][2] = minPlaneZ / (minPlaneZ - maxPlaneZ);
    result[3][3] = 1.0f;

    return result;
}

float4x4 CreateLookAtMatrix(float3 cameraPosition, float3 cameraTarget, float3 cameraUpVector)
{
    float3 zAxis = normalize(cameraTarget - cameraPosition);
    float3 xAxis = normalize(cross(cameraUpVector, zAxis));
    float3 yAxis = normalize(cross(zAxis, xAxis));

    float4x4 inv = float4x4(float4(xAxis.x, xAxis.y, xAxis.z, 0.0f),
                            float4(yAxis.x, yAxis.y, yAxis.z, 0.0f),
                            float4(zAxis.x, zAxis.y, zAxis.z, 0.0f),
                            float4(0.0f, 0.0f, 0.0f, 1.0f));

    inv[3][0] = -dot(xAxis, cameraPosition);
    inv[3][1] = -dot(yAxis, cameraPosition);
    inv[3][2] = -dot(zAxis, cameraPosition);
    return inv;
}


BoundingFrustum ExtractBoundingFrustum(float4x4 matrix)
{
    BoundingFrustum result;

    // Left plane
    float a = matrix[0][3] + matrix[0][0];
    float b = matrix[1][3] + matrix[1][0];
    float c = matrix[2][3] + matrix[2][0];
    float d = matrix[3][3] + matrix[3][0];

    result.LeftPlane = normalize(float4(-a, -b, -c, -d));

    // Right clipping plane
    a = matrix[0][3] - matrix[0][0]; 
    b = matrix[1][3] - matrix[1][0]; 
    c = matrix[2][3] - matrix[2][0]; 
    d = matrix[3][3] - matrix[3][0];

    result.RightPlane = normalize(float4(-a, -b, -c, -d));

    // Top clipping plane
    a = matrix[0][3] - matrix[0][1]; 
    b = matrix[1][3] - matrix[1][1]; 
    c = matrix[2][3] - matrix[2][1]; 
    d = matrix[3][3] - matrix[3][1];

    result.TopPlane = normalize(float4(-a, -b, -c, -d));

    // Bottom clipping plane
    a = matrix[0][3] + matrix[0][1]; 
    b = matrix[1][3] + matrix[1][1]; 
    c = matrix[2][3] + matrix[2][1]; 
    d = matrix[3][3] + matrix[3][1];

    result.BottomPlane = normalize(float4(-a, -b, -c, -d));

    // Near clipping plane
    a = matrix[0][2]; 
    b = matrix[1][2]; 
    c = matrix[2][2]; 
    d = matrix[3][2];

    result.NearPlane = normalize(float4(-a, -b, -c, -d));

    // Far clipping plane
    a = matrix[0][3] - matrix[0][2]; 
    b = matrix[1][3] - matrix[1][2]; 
    c = matrix[2][3] - matrix[2][2]; 
    d = matrix[3][3] - matrix[3][2];

    result.FarPlane = normalize(float4(-a, -b, -c, -d));

    return result;
}

kernel void ComputeLightCameras(uint2 threadPosition [[thread_position_in_grid]],
                               const device InputParameters& parameters)
{
    device Camera& mainCamera = parameters.Cameras[0];
    const device Light& light = parameters.Lights[0];

    device Camera& lightCamera1 = parameters.Cameras[light.CameraIndexes[threadPosition.y]];
    float3 lightDirection = lightCamera1.WorldPosition;
    int cascadeIdx = threadPosition.y;

    float cascadeSplits[5] = { 0.0, 0.0, 0.0, 0.0, 0.0 };
    float MinDistance = mainCamera.MinDepth;
    float MaxDistance = mainCamera.MaxDepth;

    // Compute ranges based on the main camera depth
    float lambda = 1.0f;
    int NumCascades = 5;

    float nearClip = 0.1;
    float farClip = 1000.0;
    float clipRange = farClip - nearClip;

    float minZ = nearClip + MinDistance * clipRange;
    float maxZ = nearClip + MaxDistance * clipRange;

    float range = maxZ - minZ;
    float ratio = maxZ / minZ;

    for(uint i = 0; i < NumCascades; ++i)
    {
        float p = (i + 1) / float(NumCascades);
        float logScale = minZ * pow(abs(ratio), p);
        float uniformScale = minZ + range * p;
        float d = lambda * (logScale - uniformScale) + uniformScale;
        cascadeSplits[i] = (d - nearClip) / clipRange;
    }

    // Get the 8 points of the view frustum in world space
    float3 frustumCornersWS[8] =
    {
        float3(-1.0f,  1.0f, 0.0f),
        float3( 1.0f,  1.0f, 0.0f),
        float3( 1.0f, -1.0f, 0.0f),
        float3(-1.0f, -1.0f, 0.0f),
        float3(-1.0f,  1.0f, 1.0f),
        float3( 1.0f,  1.0f, 1.0f),
        float3( 1.0f, -1.0f, 1.0f),
        float3(-1.0f, -1.0f, 1.0f),
    };

    float prevSplitDist = (cascadeIdx == 0) ? MinDistance : cascadeSplits[cascadeIdx - 1];
    float splitDist = cascadeSplits[cascadeIdx];

    for (uint i = 0; i < 8; ++i)
    {
        float4 corner = mainCamera.ViewProjectionInverse * float4(frustumCornersWS[i], 1.0f);
        frustumCornersWS[i] = corner.xyz / corner.w;
    }

    // Get the corners of the current cascade slice of the view frustum
    for (int i = 0; i < 4; ++i)
    {
        float3 cornerRay = frustumCornersWS[i + 4] - frustumCornersWS[i];
        float3 nearCornerRay = cornerRay * prevSplitDist;
        float3 farCornerRay = cornerRay * splitDist;
        frustumCornersWS[i + 4] = frustumCornersWS[i] + farCornerRay;
        frustumCornersWS[i] = frustumCornersWS[i] + nearCornerRay;
    }

    // Calculate the centroid of the view frustum slice
    float3 frustumCenter = 0.0f;
    
    for (int i = 0; i < 8; ++i)
        frustumCenter += frustumCornersWS[i];

    frustumCenter /= 8.0f;
    frustumCenter = (lightCamera1.ViewMatrix * float4(frustumCenter, 1.0f)).xyz;

    float3 minExtents;
    float3 maxExtents;
 
    // Calculate the radius of a bounding sphere surrounding the frustum corners
    // float sphereRadius = 0.0f;

    // for(uint i = 0; i < 8; ++i)
    // {
    //     // float3 corner = frustumCornersWS[i];
    //     float3 corner = (lightCamera1.ViewMatrix * float4(frustumCornersWS[i], 1.0f)).xyz;
    //     float dist = length(corner - frustumCenter);
    //     sphereRadius = max(sphereRadius, dist);
    // }

    // sphereRadius = ceil(sphereRadius * 16.0f) / 16.0f;

    // maxExtents = frustumCenter;
    // minExtents = frustumCenter;

    // maxExtents += sphereRadius;
    // minExtents += - sphereRadius;


    // Calculate an AABB around the frustum corners
    float3 mins = float3(MAXFLOAT, MAXFLOAT, MAXFLOAT);
    float3 maxes = float3(-MAXFLOAT, -MAXFLOAT, -MAXFLOAT);
    
    for (int i = 0; i < 8; ++i)
    {
        float3 corner = (lightCamera1.ViewMatrix * float4(frustumCornersWS[i], 1.0f)).xyz;
        mins = min(mins, corner);
        maxes = max(maxes, corner);
    }

    minExtents = mins;
    maxExtents = maxes;


    float shadowMapSize = 2048.0f;
    // float maxHeight = 25.0f;

    // float3 cascadeExtents = maxExtents - minExtents;

    // float3 lightPosition = float3(frustumCenter.x, maxHeight * 10, frustumCenter.z) + lightDirection;
    // float3 lightTarget = frustumCenter;
    // float3 lightUp = float3(0.0f, 1.0f, 0.0f);

    // lightCamera1.ViewMatrix = CreateLookAtMatrix(lightPosition, lightTarget, lightUp);

    lightCamera1.ProjectionMatrix = OrthographicProjection(minExtents.x, maxExtents.x, maxExtents.y, minExtents.y, 80, 240);

    // Create the rounding matrix, by projecting the world-space origin and determining
    // the fractional offset in texel space
    // float4 transformedOrigin = lightCamera1.ProjectionMatrix * lightCamera1.ViewMatrix * float4(0.0, 0.0, 0.0, 1.0);

    // float3 shadowOrigin = transformedOrigin.xyz * (shadowMapSize / 2.0f);
    // float3 roundedOrigin = round(shadowOrigin);
    // float3 roundOffset = (roundedOrigin - shadowOrigin) * (2.0f / shadowMapSize);

    // lightCamera1.ProjectionMatrix[3][0] += roundOffset.x;
    // lightCamera1.ProjectionMatrix[3][1] += roundOffset.y;

    lightCamera1.ViewProjectionMatrix = lightCamera1.ProjectionMatrix * lightCamera1.ViewMatrix;
    lightCamera1.BoundingFrustum = ExtractBoundingFrustum(lightCamera1.ViewProjectionMatrix);
}