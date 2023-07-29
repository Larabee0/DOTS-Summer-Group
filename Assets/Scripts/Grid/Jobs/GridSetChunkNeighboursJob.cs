using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct GridSetChunkNeighboursJob : IJobFor
{
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public DynamicBuffer<ChunkReferenceBuffer> chunkBuffer;
    [ReadOnly,NativeDisableContainerSafetyRestriction]
    public ComponentLookup<TileNeighboursEntities> tileNeighbourEntities;

    public GridData gridData;
    [NativeDisableContainerSafetyRestriction]
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute(int index)
    {
        int2 coordinate = new(index % gridData.chunkDimentions.x, index / gridData.chunkDimentions.x);
        Entity chunkEntity = chunkBuffer[index].entity;
        TileNeighboursEntities tileNeighbours = tileNeighbourEntities[chunkEntity];
        for (SqrDirection d = SqrDirection.N; d <= SqrDirection.NW; d++)
        {
            int neighbourIndex = GridExtensions.GetIndexInDirection(d, coordinate, gridData.chunkDimentions);
            if (neighbourIndex >= 0)
            {
                tileNeighbours[d] = chunkBuffer[neighbourIndex].entity;
            }
        }
        int sortKey = gridData.GetVertexCount() ^ gridData.CellCount;
        ecb.SetComponent(sortKey, chunkEntity, tileNeighbours);
    }
}
