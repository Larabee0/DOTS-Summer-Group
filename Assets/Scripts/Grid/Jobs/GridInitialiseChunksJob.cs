using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public struct GridInitialiseChunksJob : IJobFor
{
    [NativeDisableContainerSafetyRestriction]
    public EntityCommandBuffer.ParallelWriter ecb;
    public Entity gridEntity;
    public GridData gridData;
    public EntityPrefab<ChunkTag> chunkPrefab;

    public void Execute(int index)
    {
        int2 coordinate = new(index % gridData.chunkDimentions.x, index / gridData.chunkDimentions.x);

        float3 center = new(coordinate.x * gridData.ChunkScale, 0, coordinate.y * gridData.ChunkScale);
        float halfScale = gridData.ChunkScale / 2f;
        float3 bottomLeft = center - halfScale;
        float3 topRight = center + halfScale;
        bottomLeft.y = topRight.y = 0;
        var positionData = new TilePositionData()
        {
            center = center,
            diagonals = new(bottomLeft, topRight)
        };

        var abstractData = new TileAbstractData()
        {
            coordinate = coordinate,
            index = index,
            scale = gridData.ChunkScale
        };

        var neighbourIndices = TileNeighboursIndices.Null;
        for (SqrDirection d = 0; d <= SqrDirection.NW; d++)
        {
            neighbourIndices[d] = GridExtensions.GetIndexInDirection(d, coordinate, gridData.chunkDimentions);
        }

        int sortKey = gridData.GetVertexCount() ^ gridData.ChunkCount;

        // at this point the entity doesn't exist thus can only be used in the scope of 
        // this command buffer
        Entity newChunk = ecb.Instantiate(sortKey, chunkPrefab);
        ecb.SetComponent(sortKey, newChunk, abstractData);
        ecb.SetComponent(sortKey, newChunk, positionData);
        ecb.SetComponent(sortKey, newChunk, neighbourIndices);
        ecb.AddComponent(sortKey, newChunk, new Parent { Value = gridEntity });
        ecb.AppendToBuffer(sortKey, gridEntity, new ChunkReferenceBuffer { entity = newChunk, index = index });
    }
}
