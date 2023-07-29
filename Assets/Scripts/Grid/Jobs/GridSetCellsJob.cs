using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public struct GridSetCellsJob : IJobFor
{
    public GridData gridData;

    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public DynamicBuffer<ChunkReferenceBuffer> chunkBuffer;
    [ReadOnly]
    public DynamicBuffer<CellReferenceBuffer> cellBuffer;

    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public ComponentLookup<TileNeighboursEntities> tileNeighbourEntities;

    [NativeDisableContainerSafetyRestriction]
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute(int index)
    {
        int2 coordinate = new(index % gridData.CellDimentions.x, index / gridData.CellDimentions.x);

        int2 chunkCoordinate = coordinate / gridData.chunkSize;
        int chunkIndex = chunkCoordinate.y * gridData.chunkDimentions.x + chunkCoordinate.x;

        Entity cellEntity = cellBuffer[index].entity;

        TileNeighboursEntities tileNeighbours = tileNeighbourEntities[cellEntity];
        for (SqrDirection d = SqrDirection.N; d <= SqrDirection.NW; d++)
        {
            int neighbourIndex = GridExtensions.GetIndexInDirection(d, coordinate, gridData.CellDimentions);
            if (neighbourIndex >= 0)
            {
                tileNeighbours[d] = cellBuffer[neighbourIndex].entity;
            }
        }

        int sortKey = gridData.GetVertexCount() ^ gridData.CellCount;
        Entity chunkEntity = chunkBuffer[chunkIndex].entity;
        ecb.SetComponent(sortKey, cellEntity, tileNeighbours);
        ecb.AddComponent(sortKey, cellEntity, new Parent() { Value = chunkEntity });
        ecb.AppendToBuffer(sortKey, chunkEntity, new CellReferenceBuffer { index = index, entity = cellEntity });
    }
}
