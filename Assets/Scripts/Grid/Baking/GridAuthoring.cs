using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public class GridAuthoring : MonoBehaviour
{
    public GridData gridData;

    [Tooltip("Only for GameObject mode")]
    [SerializeField] private bool useJobs;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshRenderer textureDisplay;

    private readonly List<Chunk> chunks = new();
    private readonly List<Cell> cells = new();
    private Mesh gridMesh;

    private Texture2D gridTexture;

    public static GridAuthoring Grid;

    private void Start()
    {
        Grid = this;

        InitialiseGridTexture();

        if (textureDisplay != null)
        {
            textureDisplay.material.mainTexture = gridTexture;
        }
        if (meshFilter != null && meshRenderer != null)
        {
            meshRenderer.materials[2].SetTexture("_GridColours", gridTexture);
            GenerateGrid();
            meshFilter.mesh = gridMesh = new Mesh() { name = "Grid Mesh", subMeshCount = 2 };
            if (useJobs)
            {
                GenerateGridMeshJobs();
            }
            else
            {
                GenerateGridMesh();
            }
        }
    }

    private void InitialiseGridTexture()
    {
        if (gridTexture != null)
        {
            gridTexture.Reinitialize(gridData.TextureDimentions.x , gridData.TextureDimentions.y );

            gridTexture.filterMode = FilterMode.Point;
            gridTexture.wrapModeU = TextureWrapMode.Mirror;
            gridTexture.wrapModeV = TextureWrapMode.Mirror;
        }
        else
        {
            gridTexture = new(gridData.TextureDimentions.x, gridData.TextureDimentions.y, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Mirror,
                wrapModeV = TextureWrapMode.Mirror
            };
        }

        for (int x = 0; x < gridTexture.width; x += gridData.pixelsPerCell)
        {
            for (int y = 0; y < gridTexture.height; y += gridData.pixelsPerCell)
            {
                Color colour = UnityEngine.Random.ColorHSV();
                for (int z = 0; z < gridData.pixelsPerCell; z++)
                {
                    for (int w = 0; w < gridData.pixelsPerCell; w++)
                    {
                        gridTexture.SetPixel(x + z, y + w, colour);
                        colour = UnityEngine.Random.ColorHSV();
                    }
                }
            }
        }
        gridTexture.Apply();
    }

    private void GenerateGridMeshJobs()
    {
        if (gridMesh == null)
        {
            return;
        }

        // mesh generation begins here with allocation of a mesh.
        float startTime = Time.realtimeSinceStartup;
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        // creating vertex buffer attributes, only using for positon and uv0s.
        NativeArray<VertexAttributeDescriptor> discriptors = new(2, Allocator.Temp);
        discriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        discriptors[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1);

        // set vertex buffer parameters, also calculating vertex buffer length.
        meshData.SetVertexBufferParams(gridData.GetVertexCount(), discriptors);


        // subMesh index counts
        int4 subMeshIndices = gridData.GetSubMeshIndices();

        // 4 submeshes in this mesh, 2 for the line grids and 2 for the triangles (at the Cell & Chunk scale).
        meshData.subMeshCount = 4;
        // set index buffer parameters, index count is equal to sum of subMeshIndices.
        meshData.SetIndexBufferParams(math.csum(subMeshIndices), IndexFormat.UInt32);

        //mesh now setup
        // access the vertex buffer data for native reference to allow directly writing
        // vertex data into GPU frame buffer.
        NativeArray<float3> vertices = meshData.GetVertexData<float3>(0);
        NativeArray<float2> uvs = meshData.GetVertexData<float2>(1);

        // access the index buffer for native reference to allow direct writing to
        // GPU frame buffer.
        // Sub arrays of the index buffer are gotten for each submesh for simplicity in jobs. - alising 1 array as 4.
        NativeArray<uint> cellLinesIndices = meshData.GetIndexData<uint>().GetSubArray(0,
            subMeshIndices.x);

        NativeArray<uint> chunkLinesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x,
            subMeshIndices.y);

        NativeArray<uint> cellTrianglesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x + subMeshIndices.y,
            subMeshIndices.z);

        NativeArray<uint> chunkTrianglesIndices = meshData.GetIndexData<uint>().GetSubArray(subMeshIndices.x + subMeshIndices.y + subMeshIndices.z,
            subMeshIndices.w);

        // only fills index data for chunks. vertex data is created by the cellJob
        var chunkJob = new CreateGeometry
        {
            coordinateMul = gridData.chunkSize,
            chunkWidth = gridData.chunkDimentions.x,
            geometryWidth = gridData.CellDimentions.x + 1,
            linesIndices = chunkLinesIndices,
            trianglesIndices = chunkTrianglesIndices
        }.ScheduleParallel(gridData.ChunkCount, 64, new JobHandle());

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
        }.ScheduleParallel(gridData.CellCount, 64, new JobHandle());

        // these can both safely run at the same time
        var geometryJobs = JobHandle.CombineDependencies(chunkJob, cellJob);

        // setting the submeshes can only be done after the above jobs have finished.
        new SetSubmeshes
        {
            meshDataArray = meshDataArray,
            subMeshCounts = subMeshIndices
        }.Schedule(geometryJobs).Complete();

        // log mesh info before arrays are disposed
        Debug.LogFormat("Vertex Buffer {0}", vertices.Length);
        Debug.LogFormat("cellLines (0) {0}", cellLinesIndices.Length);
        Debug.LogFormat("chunkLines (1) {0}", chunkLinesIndices.Length);
        Debug.LogFormat("cellTriangles (2) {0}", cellTrianglesIndices.Length);
        Debug.LogFormat("chunkTriangles (3) {0}", chunkTrianglesIndices.Length);

        // clear current mesh
        gridMesh.Clear();
        // apply meshDataArray to the current mesh reference.
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, gridMesh);
        
        // because this is flat grid, the bound calculations are simple.
        gridMesh.bounds = gridData.GetMeshBounds();
        

        Debug.LogFormat("Mesh Time = {0}ms", (Time.realtimeSinceStartup - startTime) * 1000f);
    }

    private void GenerateGridMesh()
    {
        if (gridMesh == null)
        {
            return;
        }
        gridMesh.Clear();
        gridMesh.indexFormat = IndexFormat.UInt32;
        gridMesh.subMeshCount = 4;
        
        NativeArray<float3> vertices = new((gridData.CellDimentions.x + 1) * (gridData.CellDimentions.y + 1), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> uvs = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> cellLinesIndices = new(gridData.CellDimentions.x * gridData.CellDimentions.y * 8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> chunkLinesIndices = new(gridData.chunkDimentions.x * gridData.chunkDimentions.y * 8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> cellTrianglesIndices = new(gridData.CellDimentions.x * gridData.CellDimentions.y * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> chunkTrianglesIndices = new(gridData.chunkDimentions.x * gridData.chunkDimentions.y * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);


        for (int i = 0; i < cells.Count; i++)
        {
            float3x4 corners = cells[i].Corners;
            AddGeometry(vertices,uvs, cellLinesIndices, cellTrianglesIndices, new float3x2(corners.c0, corners.c2), cells[i].coordinate,gridData.CellDimentions.x,1);
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            float3x4 corners = chunks[i].Corners;
            AddGeometry(vertices, uvs, chunkLinesIndices, chunkTrianglesIndices, new float3x2(corners.c0, corners.c2), chunks[i].coordinate,gridData.chunkDimentions.x, gridData.chunkSize);
        }
        
        gridMesh.SetVertices(vertices);
        gridMesh.SetUVs(0, uvs);
        gridMesh.SetIndices(cellLinesIndices, MeshTopology.Lines, 0);
        gridMesh.SetIndices(chunkLinesIndices, MeshTopology.Lines, 1);
        gridMesh.SetIndices(cellTrianglesIndices, MeshTopology.Triangles, 2);
        gridMesh.SetIndices(chunkTrianglesIndices, MeshTopology.Triangles, 3);
        Debug.Log(gridMesh.indexFormat);
        Debug.LogFormat("Vertex Buffer {0}", vertices.Length);
        Debug.LogFormat("cellLines (0) {0}", cellLinesIndices.Length);
        Debug.LogFormat("chunkLines (1) {0}", chunkLinesIndices.Length);
        Debug.LogFormat("cellTriangles (2) {0}", cellTrianglesIndices.Length);
        Debug.LogFormat("chunkTriangles (3) {0}", chunkTrianglesIndices.Length);
    }

    private unsafe void AddGeometry(NativeArray<float3> vertexMap,NativeArray<float2> uvs, NativeArray<int> lines, NativeArray<int> triangles, float3x2 diagnonals, int2 coordinate, int width,int coordinateMul)
    {
        float3x4 corners = new()
        {
            c0 = diagnonals.c0,
            c1 = new float3(diagnonals.c0.x, 0f, diagnonals.c1.z),
            c2 = diagnonals.c1,
            c3 = new float3(diagnonals.c1.x, 0f, diagnonals.c0.z)
        };

        float2 cellCentreUV = math.remap(float2.zero, (float2)gridData.CellDimentions, float2.zero, 1f, (float2)coordinate);
        float2 UVoffset = new(1f / gridData.CellDimentions.x, 1f / gridData.CellDimentions.y);
        int index = coordinate.y * width + coordinate.x;
        coordinate *= coordinateMul;

        int4 indicies = new()
        {
            x = coordinate.y * (gridData.CellDimentions.x + 1) + coordinate.x,
            y = (coordinate + new int2(0, coordinateMul)).y * (gridData.CellDimentions.x + 1) + (coordinate + new int2(0, coordinateMul)).x,
            z = (coordinate + coordinateMul).y * (gridData.CellDimentions.x + 1) + (coordinate + coordinateMul).x,
            w = (coordinate + new int2(coordinateMul, 0)).y * (gridData.CellDimentions.x + 1) + (coordinate + new int2(coordinateMul, 0)).x
        };

        for (int i = 0; i < 4; i++)
        {
            vertexMap[indicies[i]] = corners[i];
        }

        uvs[indicies.x] = cellCentreUV; // 0,0
        uvs[indicies.y] = math.min(new float2(1), new float2(cellCentreUV.x, cellCentreUV.y + UVoffset.y)); //0, 1
        uvs[indicies.z] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y + UVoffset.y)); //1, 1
        uvs[indicies.w] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y)); //1, 0

        int indexLine = index * 8;
        int indexTri = index * 6;

        lines[indexLine + 0] = indicies.x;
        lines[indexLine + 1] = indicies.y;

        lines[indexLine + 2] = indicies.y;
        lines[indexLine + 3] = indicies.z;

        lines[indexLine + 4] = indicies.z;
        lines[indexLine + 5] = indicies.w;

        lines[indexLine + 6] = indicies.w;
        lines[indexLine + 7] = indicies.x;


        triangles[indexTri + 0] = indicies.x;
        triangles[indexTri + 1] = indicies.z;
        triangles[indexTri + 2] = indicies.w;

        triangles[indexTri + 3] = indicies.y;
        triangles[indexTri + 4] = indicies.z;
        triangles[indexTri + 5] = indicies.x;
    }

    // private void OnValidate()
    // {
    //     if (Application.isPlaying)
    //     {
    //         InitialiseGridTexture();
    //         GenerateGrid();
    //         if (useJobs)
    //         {
    //             GenerateGridMeshJobs();
    //         }
    //         else
    //         {
    //             GenerateGridMesh();
    //         }
    //     }
    // }

    private void GenerateGrid()
    {
        chunks.Clear();
        for (int i = 0, x = 0; x < gridData.chunkDimentions.x; x++)
        {
            for (int z = 0; z < gridData.chunkDimentions.y; z++, i++)
            {
                chunks.Add(new Chunk(new float3(x * gridData.ChunkScale, 0, z * gridData.ChunkScale), gridData.ChunkScale, i, new int2(x, z)));
            }
        }

        float3 cellOffset = gridData.GetCellOffset();
        cells.Clear();
        for (int i = 0; i < chunks.Count; i++)
        {
            for (int x = 0; x < gridData.chunkSize; x++)
            {
                for (int z = 0; z < gridData.chunkSize; z++)
                {
                    cells.Add(new Cell(chunks[i]));
                    chunks[i].virtualSubGrid.Add(cells[^1]);
                }
            }
        }

        int cellCountX = gridData.chunkDimentions.x * gridData.chunkSize;
        int cellCountZ = gridData.chunkDimentions.y * gridData.chunkSize;
        //int gridData.chunkSize = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;

        for (int gridZ = 0, cellIndex = 0; gridZ < cellCountZ; gridZ++)
        {
            for (int gridX = 0; gridX < cellCountX; gridX++, cellIndex++)
            {
                int chunkX = gridX / gridData.chunkSize;
                int chunkZ = gridZ / gridData.chunkSize;

                // cells are stored sorted by chunk Index at this part of initilisation.
                int chunkIndex = chunkX + chunkZ * gridData.chunkDimentions.x;

                // compute index of current cell in main Grid cellBuffer
                int localX = gridX - chunkX * gridData.chunkSize;
                int localZ = gridZ - chunkZ * gridData.chunkSize;
                int cellBufferIndex = localX + localZ * gridData.chunkSize + (chunkIndex * gridData.chunkSize * gridData.chunkSize);

                // set cellIndex in HexCellReference buffer
                Cell cell = cells[cellBufferIndex];
                cell.index = cellIndex;
                //cells[cellBufferIndex] = cell;
                    
                cell.coordinate = new int2(gridX, gridZ);
                cell.centerPosition = cellOffset+ new float3(gridX * gridData.cellScale, 0, gridZ * gridData.cellScale) ;
                cell.UpdateCorners(gridData.cellScale);
            }
        }
    }

    //[System.Serializable]
    public class Chunk
    {
        public int index;
        public int2 coordinate;
        public Vector3 centerPosition;
        public float3x2 diagonals;
        public float3x4 Corners => new()
        {
            c0 = diagonals.c0, // 0, 0
            c1 = new float3(diagonals.c0.x, 0f, diagonals.c1.z),//0,1
            c2 = diagonals.c1, // 1,1
            c3 = new float3(diagonals.c1.x, 0f, diagonals.c0.z),//1,0
        };
        public List<Cell> virtualSubGrid = new();

        public Chunk(float3 centerPosition, float scale, int index,int2 coordinate)
        {
            this.index = index;
            this.centerPosition = centerPosition;
            this.coordinate = coordinate;
            float halfScale = scale / 2f;

            float3 bottomLeft = centerPosition - halfScale;
            float3 topRight = centerPosition + halfScale;
            bottomLeft.y = topRight.y = 0;
            diagonals = new(bottomLeft, topRight);
        }
    }

    //[System.Serializable]
    public class Cell : IComparable<Cell>
    {
        public int2 coordinate;
        public int chunkIndex;
        public int2 chunkCoordinate;
        public float3 centerPosition;
        public int index;


        public float3x2 diagonals;
        public float3x4 Corners => new()
        {
            c0 = diagonals.c0, // 0, 0
            c1 = new float3(diagonals.c0.x, 0f, diagonals.c1.z),//0,1
            c2 = diagonals.c1, // 1,1
            c3 = new float3(diagonals.c1.x, 0f, diagonals.c0.z),//1,0
        };


        public Cell(float3 centerPosition, float scale, int index, int2 coordinate)
        {
            this.index = index;
            this.centerPosition = centerPosition;
            this.coordinate = coordinate;

            float halfScale = scale / 2f;

            float3 bottomLeft = centerPosition - halfScale;
            float3 topRight = centerPosition + halfScale;
            bottomLeft.y = topRight.y = 0;
            diagonals = new(bottomLeft, topRight);
        }

        public Cell(Chunk chunk)
        {
            chunkIndex = chunk.index;
            chunkCoordinate = chunk.coordinate;
        }
        public void UpdateCorners(float scale)
        {
            float halfScale = scale / 2f;

            float3 bottomLeft = centerPosition - halfScale;
            float3 topRight = centerPosition + halfScale;
            bottomLeft.y = topRight.y = 0;
            diagonals = new(bottomLeft, topRight);
        }

        public int CompareTo(Cell other)
        {
            return index.CompareTo(other.index);
        }
    }
}


public class GridBaker : Baker<GridAuthoring>
{
    public override void Bake(GridAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent<GridTag>(entity);
        AddComponent(entity, authoring.gridData);
        AddComponent<CellReferenceBuffer>(entity);
        AddComponent<ChunkReferenceBuffer>(entity);
        AddComponent<GridUninitialised>(entity);
        AddComponent<GridGenerateMesh>(entity);
        AddComponent(entity, new GridSubMeshRenderers
        {
            mesh0 = CreateAdditionalEntity(TransformUsageFlags.Renderable, false, "SubMesh0"),
            mesh1 = CreateAdditionalEntity(TransformUsageFlags.Renderable, false, "SubMesh1"),
            mesh2 = CreateAdditionalEntity(TransformUsageFlags.Renderable, false, "SubMesh2"),
            mesh3 = CreateAdditionalEntity(TransformUsageFlags.Renderable, false, "SubMesh3")
        });
        AddComponentObject(entity, new GridMeshReference() { Value = new Mesh() { name = "Grid Mesh" } });
        if (Application.isPlaying)
        {
            AddComponentObject(entity, new GridMaterialReference { materials = authoring.GetComponent<MeshRenderer>().materials });
        }
        else
        {
            AddComponentObject(entity, new GridMaterialReference { materials = authoring.GetComponent<MeshRenderer>().sharedMaterials });
        }
        
    }
}