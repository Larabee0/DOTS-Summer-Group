using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public partial class Resource : SystemBase
{
    protected override void OnStartRunning()
    {
        new ResoursePositionJob { };
    }
    protected override void OnUpdate()
    {
        
    }
}

public partial struct ResoursePositionJob : IJobEntity
{
    public void Execute([ChunkIndexInQuery] int jobChunkIndex,  ref ResourceComponent resource) 
    { 
        resource.position = GridAuthoring.Grid.chunks[resource.chunk].centerPosition;



        if(GridAuthoring.Grid.chunks[resource.chunk].index / GridAuthoring.Grid.dimentions.y == 0)
        {
            resource.southNeighbour = GridAuthoring.Grid.chunks[resource.chunk].index + 1;
        }
        else
        {
            resource.southNeighbour = -1;
        }
    }
}