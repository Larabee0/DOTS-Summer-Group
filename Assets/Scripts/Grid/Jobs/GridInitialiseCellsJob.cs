using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using JetBrains.Annotations;

[BurstCompile]
public struct GridInitialiseCellsJob : IJobFor
{
    [NativeDisableContainerSafetyRestriction]
    public EntityCommandBuffer.ParallelWriter ecb;
    public Entity gridEntity;
    public GridData gridData;
    public EntityPrefab<CellTag> cellPrefab;

    public void Execute(int index)
    {
        int2 coordinate = new(index % gridData.CellDimentions.x, index / gridData.CellDimentions.x);

        int2 chunkCoordinate = coordinate / gridData.chunkSize;
        int chunkIndex = chunkCoordinate.y * gridData.chunkDimentions.x + chunkCoordinate.x;

        float3 center = new(coordinate.x * gridData.cellScale, 0, coordinate.y * gridData.cellScale);
        float halfScale = gridData.cellScale / 2f;
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
            scale = gridData.cellScale
        };
        var chunkNeighbourIndices = ChunkNeighboursIndices.Null;
        var neighbourIndices = TileNeighboursIndices.Null;
        for (SqrDirection d = 0; d <= SqrDirection.NW; d++)
        {
            int2 neighbourCoordinate = GridExtensions.GetCoordinateInDirection(d, coordinate);
            int2 neighbourChunkCoordinate = neighbourCoordinate / gridData.chunkSize;
            int neighbourChunkIndex = neighbourChunkCoordinate.y * gridData.chunkDimentions.x + neighbourChunkCoordinate.x;
            chunkNeighbourIndices[d] = neighbourChunkIndex> gridData.ChunkCount || neighbourChunkIndex < 0 ? -1 : neighbourChunkIndex;
            neighbourIndices[d] = GridExtensions.GetIndexFromCoordinate(neighbourCoordinate, gridData.CellDimentions);
        }

        int sortKey = gridData.GetVertexCount() ^ gridData.CellCount;
        // at this point the entity doesn't exist thus can only be used in the scope of 
        // this command buffer
        Entity newCell = ecb.Instantiate(sortKey, cellPrefab);
        ecb.SetComponent(sortKey, newCell, abstractData);
        ecb.SetComponent(sortKey, newCell, positionData);
        ecb.SetComponent(sortKey, newCell, neighbourIndices);
        ecb.SetComponent(sortKey, newCell, chunkNeighbourIndices);
        ecb.SetComponent(sortKey, newCell, new ChunkParentReference() { index = chunkIndex });
        ecb.SetComponent(sortKey, newCell, new GridReference() { Value = gridEntity });
        ecb.AppendToBuffer(sortKey, gridEntity, new CellReferenceBuffer { entity = newCell, index = index });
    }


}
