using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(ExchangeTag<GridUninitialised, GridCellsUnset>))]
[assembly: RegisterGenericJobType(typeof(RemoveCompJob<GridCellsUnset>))]
[assembly: RegisterGenericJobType(typeof(SortJob<ChunkReferenceBuffer, ChunkBufferSorter>))]
[assembly: RegisterGenericJobType(typeof(SortJob<CellReferenceBuffer, CellBufferSorter>))]


[BurstCompile]
public partial struct GridCreatorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

        foreach ((GridData gridData, Entity grid) in SystemAPI.Query<GridData>().WithEntityAccess().WithAll<GridUninitialised>())
        {
            var chunkJob = new GridInitialiseChunksJob
            {
                chunkPrefab = SystemAPI.GetSingleton<EntityPrefab<ChunkTag>>(),
                gridData = gridData,
                gridEntity = grid,
                ecb = ecb
            }.ScheduleParallel(gridData.ChunkCount, 64, state.Dependency);

            var cellJob = new GridInitialiseCellsJob
            {
                cellPrefab = SystemAPI.GetSingleton<EntityPrefab<CellTag>>(),
                gridData = gridData,
                gridEntity = grid,
                ecb = ecb
            }.ScheduleParallel(gridData.CellCount, 64, state.Dependency);

            var spawnJob = JobHandle.CombineDependencies(chunkJob, cellJob);

            state.Dependency = new ExchangeTag<GridUninitialised, GridCellsUnset>()
            {
                sortKey = gridData.pixelsPerCell ^ gridData.GetVertexCount(),
                target = grid,
                ecb = ecb
            }.Schedule(spawnJob);
        }

        foreach ((GridData gridData, DynamicBuffer<ChunkReferenceBuffer> chunkBuffer,
            DynamicBuffer<CellReferenceBuffer> cellBuffer,
            Entity grid) in SystemAPI.Query<GridData, DynamicBuffer<ChunkReferenceBuffer>, DynamicBuffer<CellReferenceBuffer>>().WithEntityAccess().WithAll<GridCellsUnset>().WithNone<GridUninitialised>())
        {
            // these throw an error despite the generic jobs being registered
            //var sortChunks = chunkBuffer.AsNativeArray().SortJob(new ChunkBufferSorter()).Schedule(state.Dependency);
            //var sortCells = cellBuffer.AsNativeArray().SortJob(new CellBufferSorter()).Schedule(state.Dependency);
            //var sortJobs = JobHandle.CombineDependencies(sortCells, sortChunks);
            var sortJobs = state.Dependency;
            chunkBuffer.AsNativeArray().Sort(new ChunkBufferSorter());
            cellBuffer.AsNativeArray().Sort(new CellBufferSorter());

            ComponentLookup<TileNeighboursEntities> TileNeighboursEntities = SystemAPI.GetComponentLookup<TileNeighboursEntities>(true);
            var chunkNeighbourJob = new GridSetChunkNeighboursJob
            {
                tileNeighbourEntities = TileNeighboursEntities,
                chunkBuffer = chunkBuffer,
                gridData = gridData,
                ecb = ecb
            }.ScheduleParallel(gridData.ChunkCount, 64, sortJobs);

            var cellSetJob = new GridSetCellsJob()
            {
                tileNeighbourEntities = TileNeighboursEntities,
                cellBuffer = cellBuffer,
                chunkBuffer = chunkBuffer,
                gridData = gridData,
                ecb = ecb
            }.ScheduleParallel(gridData.CellCount, 64, sortJobs);
            var setJobs = JobHandle.CombineDependencies(chunkNeighbourJob, cellSetJob);

            state.Dependency = new RemoveCompJob<GridCellsUnset>()
            {
                sortKey = gridData.pixelsPerCell ^ gridData.GetVertexCount(),
                target = grid,
                ecb = ecb
            }.Schedule(setJobs);
        }

        state.Dependency = new GridSortChunkCellBufferJob() { ecb = ecb }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}

[BurstCompile]
public struct ExchangeTag<T,S> : IJob where T : struct, IComponentData where S : struct, IComponentData
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public Entity target;
    public int sortKey;
    public void Execute()
    {
        ecb.RemoveComponent<T>(sortKey, target);
        ecb.AddComponent(sortKey, target, ComponentType.ReadWrite<S>());
    }
}


[BurstCompile]
public struct RemoveCompJob<T> : IJob where T : struct, IComponentData
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public Entity target;
    public int sortKey;
    public void Execute()
    {
        ecb.RemoveComponent<T>(sortKey, target);
    }
}