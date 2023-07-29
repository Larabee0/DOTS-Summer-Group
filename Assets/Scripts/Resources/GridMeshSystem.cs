using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

[assembly: RegisterGenericJobType(typeof(ExchangeTag<GridGenerateMesh, GridGeneratingMesh>))]
[assembly: RegisterGenericJobType(typeof(ExchangeTag<GridGeneratingMesh, GridGeneratedMesh>))]

public partial class GridMeshSystem : SystemBase
{
    private EntityQuery generateGridMesh;
    private EntityQuery applyGridMesh;

    protected override void OnCreate()
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        generateGridMesh = builder.WithAll<GridGenerateMesh,GridData>().WithNone<GridGeneratingMesh, GridGeneratedMesh>().Build(EntityManager);

        EntityQueryDesc desc = new()
        {
            All = new ComponentType[] { typeof(GridSubMeshRenderers), typeof(GridGeneratedMesh), typeof(GridMeshReference), typeof(GridMaterialReference) },
            None = new ComponentType[] { typeof(GridGenerateMesh), typeof(GridGeneratingMesh) },
        };
        applyGridMesh = GetEntityQuery(desc);
    }

    protected override void OnUpdate()
    {
        var ecb = GetEntityCommandBuffer();
        if (!generateGridMesh.IsEmpty)
        {
            GenerateMesh(ecb);
        }

        if (!applyGridMesh.IsEmpty)
        {
            ApplyMesh(ecb);
        }

    }

    private void ApplyMesh(EntityCommandBuffer.ParallelWriter ecb)
    {
        Entity gridEntity = SystemAPI.GetSingletonEntity<GridData>();
        if (SystemAPI.TryGetSingletonRW(out RefRW<GridMeshStore> meshStore))
        {
            
            GridMeshReference meshReference = EntityManager.GetComponentObject<GridMeshReference>(gridEntity);
            GridMaterialReference matReferences = EntityManager.GetComponentObject<GridMaterialReference>(gridEntity);
            GridSubMeshRenderers gridRenderers = SystemAPI.GetSingleton<GridSubMeshRenderers>();
            GridData gridData = SystemAPI.GetSingleton<GridData>();
            Mesh.ApplyAndDisposeWritableMeshData(meshStore.ValueRW.Value, meshReference.Value);
            ecb.RemoveComponent<GridMeshStore>(0,gridEntity);

            Mesh[] meshes = new Mesh[1];
            meshes[0] = meshReference.Value;
            meshes[0].bounds = gridData.GetMeshBounds();
            var rendererSettings = new RenderMeshDescription(ShadowCastingMode.On, true);
            var renderMeshArray = new RenderMeshArray(matReferences.materials, meshes);

            for (int i = 0; i < 4; i++)
            {
                RenderMeshUtility.AddComponents(gridRenderers[i],
                    EntityManager, rendererSettings, renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(i, 0, (sbyte)i));
            }
        }
        ecb.RemoveComponent<GridGeneratedMesh>(0, gridEntity);

    }

    private void GenerateMesh(EntityCommandBuffer.ParallelWriter ecb)
    {

        GridData gridData = SystemAPI.GetSingleton<GridData>();
        Entity gridEntity = SystemAPI.GetSingletonEntity<GridData>();

        // mesh generation begins here with allocation of a mesh.
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        ecb.AddComponent(0, gridEntity, new GridMeshStore { Value = meshDataArray });
        Mesh.MeshData meshData = meshDataArray[0];

        PrepareMeshData(gridData, meshData,
            out int4 subMeshIndices,
            out NativeArray<float3> vertices,
            out NativeArray<float2> uvs,
            out NativeArray<uint> cellLinesIndices,
            out NativeArray<uint> chunkLinesIndices,
            out NativeArray<uint> cellTrianglesIndices,
            out NativeArray<uint> chunkTrianglesIndices);

        var generationExchange = new ExchangeTag<GridGenerateMesh, GridGeneratingMesh>() { ecb = ecb, sortKey = 0, target = gridEntity }.Schedule(Dependency);


        // only fills index data for chunks. vertex data is created by the cellJob
        var chunkJob = new CreateGeometry
        {
            coordinateMul = gridData.chunkSize,
            chunkWidth = gridData.chunkDimentions.x,
            geometryWidth = gridData.CellDimentions.x + 1,
            linesIndices = chunkLinesIndices,
            trianglesIndices = chunkTrianglesIndices
        }.ScheduleParallel(gridData.ChunkCount, 64, generationExchange);

        // fills cell index data and all vertex data.
        var cellJob = new CreateGeometryUV
        {
            UVoffset = new(1f / gridData.CellDimentions.x, 1f / gridData.CellDimentions.y),
            cellDimentions = gridData.CellDimentions,
            geometryWidth = gridData.CellDimentions.x + 1,
            cellScale = gridData.cellScale,
            cellOffset = gridData.GetCellOffset(),
            vertices = vertices,
            uvs = uvs,
            linesIndices = cellLinesIndices,
            trianglesIndices = cellTrianglesIndices
        }.ScheduleParallel(gridData.CellCount, 64, generationExchange);

        // these can both safely run at the same time
        var geometryJobs = JobHandle.CombineDependencies(chunkJob, cellJob);

        // setting the submeshes can only be done after the above jobs have finished.
        var subMeshJob = new SetSubmeshes
        {
            meshDataArray = meshDataArray,
            subMeshCounts = subMeshIndices
        }.Schedule(geometryJobs);

        Dependency = new ExchangeTag<GridGeneratingMesh, GridGeneratedMesh>() { ecb = ecb, sortKey = 0, target = gridEntity }.Schedule(subMeshJob);
    }

    private static void PrepareMeshData(GridData gridData, Mesh.MeshData meshData, out int4 subMeshIndices, out NativeArray<float3> vertices, out NativeArray<float2> uvs, out NativeArray<uint> cellLinesIndices, out NativeArray<uint> chunkLinesIndices, out NativeArray<uint> cellTrianglesIndices, out NativeArray<uint> chunkTrianglesIndices)
    {
        // creating vertex buffer attributes, only using for positon and uv0s.
        NativeArray<VertexAttributeDescriptor> discriptors = new(2, Allocator.Temp);
        discriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        discriptors[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1);

        // set vertex buffer parameters, also calculating vertex buffer length.
        meshData.SetVertexBufferParams(gridData.GetVertexCount(), discriptors);

        // subMesh index counts
        subMeshIndices = gridData.GetSubMeshIndices();

        // 4 submeshes in this mesh, 2 for the line grids and 2 for the triangles (at the Cell & Chunk scale).
        meshData.subMeshCount = 4;
        // set index buffer parameters, index count is equal to sum of subMeshIndices.
        meshData.SetIndexBufferParams(math.csum(subMeshIndices), IndexFormat.UInt32);

        //mesh now setup
        // access the vertex buffer data for native reference to allow directly writing
        // vertex data into GPU frame buffer.
        vertices = meshData.GetVertexData<float3>(0);
        uvs = meshData.GetVertexData<float2>(1);

        // access the index buffer for native reference to allow direct writing to
        // GPU frame buffer.
        // Sub arrays of the index buffer are gotten for each submesh for simplicity in jobs. - alising 1 array as 4.
        cellLinesIndices = meshData.GetIndexData<uint>().GetSubArray(0,
            subMeshIndices.x);
        chunkLinesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x,
            subMeshIndices.y);
        cellTrianglesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x + subMeshIndices.y,
            subMeshIndices.z);
        chunkTrianglesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x + subMeshIndices.y + subMeshIndices.z,
            subMeshIndices.w);
    }

    private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb.AsParallelWriter();
    }
}

