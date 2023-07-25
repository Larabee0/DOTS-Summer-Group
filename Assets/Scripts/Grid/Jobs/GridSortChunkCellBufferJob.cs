using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile,WithAll(typeof(SortChunkCellBuffer))]
public partial struct GridSortChunkCellBufferJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity chunkEntity, ref DynamicBuffer<CellReferenceBuffer> cellBuffer)
    {
        cellBuffer.AsNativeArray().Sort(new CellBufferSorter());
        ecb.RemoveComponent<SortChunkCellBuffer>(jobChunkIndex, chunkEntity);
    }
}
