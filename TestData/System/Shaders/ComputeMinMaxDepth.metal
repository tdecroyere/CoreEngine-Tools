#include <metal_stdlib>
#include <simd/simd.h>

#include "Common.h"

using namespace metal;

struct InputParameters
{
    const depth2d_ms<float> InputTexture [[id(0)]];
    device Camera* Cameras [[id(1)]];
    device float2* WorkingBuffer [[id(2)]];
};

float ConvertDepthSampleToLinear(float depthSample, float nearPlane, float farPlane)
{
    float depthRange = farPlane - nearPlane;
    float worldSpaceDepthSample = 2.0 * nearPlane * farPlane / (farPlane + nearPlane - depthSample * depthRange);
    return saturate((worldSpaceDepthSample - nearPlane) / depthRange);
}

kernel void ComputeMinMaxDepthInitial(uint2 threadPosition [[thread_position_in_grid]],
                                      uint2 threadPositionInGroup [[thread_position_in_threadgroup]],
                                      uint2 groupPosition [[threadgroup_position_in_grid]],
                                      uint2 groupsCount [[threadgroups_per_grid]],
                                      const device InputParameters& parameters)
{
    device Camera& camera = parameters.Cameras[0];

    float minValue = MAXFLOAT;
    float maxValue = 0.0;

    threadgroup atomic_uint atomicMinZ;
    threadgroup atomic_uint atomicMaxZ;
    
    atomic_store_explicit(&atomicMinZ, as_type<uint>(minValue), metal::memory_order_relaxed);
    atomic_store_explicit(&atomicMaxZ, as_type<uint>(maxValue), metal::memory_order_relaxed);

    threadgroup_barrier(mem_flags::mem_threadgroup);

    uint sampleCount = parameters.InputTexture.get_num_samples();  
    
    for (uint i = 0; i < sampleCount; i++) 
    {  
        float sample = parameters.InputTexture.read(threadPosition, i);  

        if (sample < 1.0)
        {
            float nearClip = camera.MinDepth;
            float farClip = camera.MaxDepth;

            sample = ConvertDepthSampleToLinear(sample, nearClip, farClip);

            atomic_fetch_min_explicit(&atomicMinZ, as_type<uint>(sample), memory_order_relaxed);
            atomic_fetch_max_explicit(&atomicMaxZ, as_type<uint>(sample), memory_order_relaxed);
        }
    }  

    threadgroup_barrier(mem_flags::mem_threadgroup);

    if (threadPositionInGroup.x == 0 && threadPositionInGroup.y == 0)
    {
        device float2& workingPointer = parameters.WorkingBuffer[groupPosition.y * groupsCount.x + groupPosition.x];
        
        workingPointer.x = as_type<float>(atomic_load_explicit(&atomicMinZ, memory_order_relaxed));
        workingPointer.y = as_type<float>(atomic_load_explicit(&atomicMaxZ, memory_order_relaxed));
    }
}

kernel void ComputeMinMaxDepthStep(uint2 threadPosition [[thread_position_in_grid]],
                                   uint2 threadPositionInGroup [[thread_position_in_threadgroup]],
                                   uint2 groupPosition [[threadgroup_position_in_grid]],
                                   uint2 groupsCount [[threadgroups_per_grid]],
                                   uint2 threadsPerGrid [[threads_per_grid]],
                                   const device InputParameters& parameters)
{
    float minValue = MAXFLOAT;
    float maxValue = 0.0;

    threadgroup atomic_uint atomicMinZ;
    threadgroup atomic_uint atomicMaxZ;

    if (threadPositionInGroup.x == 0)
    {
        atomic_store_explicit(&atomicMinZ, as_type<uint>(minValue), metal::memory_order_relaxed);
        atomic_store_explicit(&atomicMaxZ, as_type<uint>(maxValue), metal::memory_order_relaxed);
    }
    
    threadgroup_barrier(mem_flags::mem_threadgroup);

    float2 workingData = parameters.WorkingBuffer[threadPosition.x];

    atomic_fetch_min_explicit(&atomicMinZ, as_type<uint>(workingData.x), memory_order_relaxed);
    atomic_fetch_max_explicit(&atomicMaxZ, as_type<uint>(workingData.y), memory_order_relaxed);

    threadgroup_barrier(mem_flags::mem_threadgroup);

    if (threadPositionInGroup.x == 0)
    {
        if (groupsCount.x > 1)
        {
            device float2& destWorkingPointer = parameters.WorkingBuffer[groupPosition.x];
            
            destWorkingPointer.x = as_type<float>(atomic_load_explicit(&atomicMinZ, memory_order_relaxed));
            destWorkingPointer.y = as_type<float>(atomic_load_explicit(&atomicMaxZ, memory_order_relaxed));
        }

        else
        {
            device Camera& camera = parameters.Cameras[0];

            camera.MinDepth = as_type<float>(atomic_load_explicit(&atomicMinZ, memory_order_relaxed));
            camera.MaxDepth = as_type<float>(atomic_load_explicit(&atomicMaxZ, memory_order_relaxed));
        }
    }
}