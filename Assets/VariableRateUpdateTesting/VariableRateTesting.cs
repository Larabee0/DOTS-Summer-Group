using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(CustomRateGroup))]
public partial class VariableRateTesting : SystemBase
{
    private int pointCount = 2500;
    private Unity.Mathematics.Random random;
    private float3 minPos = new float3(-100,-100, -100);
    private float3 maxPos = new float3(100, 100,100);

    protected override void OnStartRunning()
    {
        random.InitState((uint)System.DateTime.Now.Millisecond);
    }

    protected override void OnUpdate()
    {
        var pointArray = new NativeArray<float3>(pointCount,
            Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (var i = 0; i < pointCount; i++)
        {
            pointArray[i] = random.NextFloat3(minPos, maxPos);
        }

        // Schedule returns a dependency
        JobHandle expensiveDependency = new ExpensiveJob
        {
            pointArray = pointArray,
        }.Schedule();

        // point array should be disposed off after ExpensiveJob has run
        pointArray.Dispose(expensiveDependency);
        // we want PostExpensiveJobJob to run after expensiveJob has run
        new PostExpensiveJobJob().Schedule(expensiveDependency);
    }
}

[BurstCompile]
public struct ExpensiveJob : IJob
{
    public NativeArray<float3> pointArray;
    public void Execute()
    {
        Debug.Log("Begin calculating distances");
        
        var pointCount = pointArray.Length;
        var results = new NativeArray<float>(pointCount * pointCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0, r = 0; i < pointCount; i++)
        {
            for (int j = 0; j < pointCount; j++, r++)
            {
                results[r] = math.distance(pointArray[i], pointArray[j]);
            }
        }
    }
}

[BurstCompile]
public struct PostExpensiveJobJob : IJob
{
    public void Execute()
    {
        Debug.Log("End calculating distances");
    }
}

public partial class CustomRateGroup : ComponentSystemGroup
{
    public CustomRateGroup()
    {
        RateManager = new RateUtils.VariableRateManager(5000, true);
    }
}