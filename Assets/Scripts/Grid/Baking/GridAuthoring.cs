using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class GridAuthoring : MonoBehaviour
{
    [Tooltip("Chunk grid dimentions")]
    public int2 dimentions = new(64,64);
    [Tooltip("Main Grid Scale"),Min(0.001f)]
    public float cellScale = 1f;
    [Tooltip("chunk size square it to get cells per chunl"), Min(1)]
    public int chunkSize = 3;
    [Tooltip("Cell Pixel Dimentions")]
    [SerializeField, Min(1)] private int pixelsPerCell = 16;


    private int2 cellDimentions;
    private float chunkScale;
    private float3 cellOffset = float3.zero;
    private int tWidth = 128;
    private int tHeight = 128;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshRenderer textureDisplay;

    private List<Chunk> chunks = new();
    private List<Cell> cells = new();
    private Mesh gridMesh;

    private Texture2D gridTexture;

    private void Start()
    {
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
            GenerateGridMeshJobs();
        }
    }

    private void InitialiseGridTexture()
    {
        if (gridTexture != null)
        {
            gridTexture.Reinitialize(dimentions.x * chunkSize * pixelsPerCell, dimentions.y * chunkSize * pixelsPerCell);

            gridTexture.filterMode = FilterMode.Point;
            gridTexture.wrapModeU = TextureWrapMode.Mirror;
            gridTexture.wrapModeV = TextureWrapMode.Mirror;
        }
        else
        {
            gridTexture = new(dimentions.x * chunkSize * pixelsPerCell, dimentions.y * chunkSize * pixelsPerCell, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Mirror,
                wrapModeV = TextureWrapMode.Mirror
            };
        }
        tWidth = gridTexture.width;
        tHeight = gridTexture.height;

        for (int x = 0; x < gridTexture.width; x += pixelsPerCell)
        {
            for (int y = 0; y < gridTexture.height; y += pixelsPerCell)
            {
                Color colour = UnityEngine.Random.ColorHSV();
                for (int z = 0; z < pixelsPerCell; z++)
                {
                    for (int w = 0; w < pixelsPerCell; w++)
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
        // data prep - all the data the mesh generator needs.
        NativeArray<float3x2> cellDiagonals = new(cells.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = new(cells.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> chunkCoordinates = new(chunks.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        
        // this takes ages cos serial and accessing managed collections
        for (int i = 0; i < cells.Count; i++)
        {
            cellDiagonals[i] = cells[i].diagonals;
            cellCoordinates[i] = cells[i].coordinate;
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            chunkCoordinates[i] = chunks[i].coordinate;
        }

        // mesh generation begins here with allocation of a mesh.
        float startTime = Time.realtimeSinceStartup;
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];
        
        // creating vertex buffer attributes, only using for positon and uv0s.
        NativeArray<VertexAttributeDescriptor> discriptors = new(2, Allocator.Temp);
        discriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        discriptors[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1);

        // calculate cell dimentions from chunk dimentions and chunk size
        cellDimentions = new(dimentions.x * chunkSize, dimentions.y * chunkSize);

        // set vertex buffer parameters, also calculating vertex buffer length.
        meshData.SetVertexBufferParams((cellDimentions.x + 1) * (cellDimentions.y + 1), discriptors);


        // subMesh index counts
        int4 subMeshIndices = new()
        {
            x = cellDimentions.x * cellDimentions.y * 8,
            y = dimentions.x * dimentions.y * 8,
            z = cellDimentions.x * cellDimentions.y * 6,
            w = dimentions.x * dimentions.y * 6
        };

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
            coordinateMul = chunkSize,
            chunkWidth = dimentions.x,
            geometryWidth = cellDimentions.x + 1,
            coordinates = chunkCoordinates,
            linesIndices = chunkLinesIndices,
            trianglesIndices = chunkTrianglesIndices
        }.ScheduleParallel(chunks.Count,64,new JobHandle());

        // fills cell index data and all vertex data.
        var cellJob = new CreateGeometryUV
        {
            UVoffset = new(1f / cellDimentions.x, 1f / cellDimentions.y),
            cellDimentions = cellDimentions,
            geometryWidth = cellDimentions.x + 1,
            coordinates = cellCoordinates,
            diagonals = cellDiagonals,
            vertices = vertices,
            uvs = uvs,
            linesIndices = cellLinesIndices,
            trianglesIndices = cellTrianglesIndices
        }.ScheduleParallel(cells.Count, 64, new JobHandle());

        // these can both safely run at the same time
        var geometryJobs = JobHandle.CombineDependencies(chunkJob, cellJob);

        // setting the submeshes can only be done after the above jobs have finished.
        new SetSubmeshes
        {
            meshDataArray =meshDataArray,
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
        // recalaute bounds (Should be moved inside jobs)
        gridMesh.RecalculateBounds();
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
        cellDimentions = new(dimentions.x * chunkSize, dimentions.y * chunkSize);

        NativeArray<float3> vertices = new((cellDimentions.x + 1) * (cellDimentions.y + 1), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> uvs = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> cellLinesIndices = new(cellDimentions.x * cellDimentions.y * 8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> chunkLinesIndices = new(dimentions.x * dimentions.y * 8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> cellTrianglesIndices = new(cellDimentions.x * cellDimentions.y * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> chunkTrianglesIndices = new(dimentions.x * dimentions.y * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);


        for (int i = 0; i < cells.Count; i++)
        {
            float3x4 corners = cells[i].Corners;
            AddGeometry(vertices,uvs, cellLinesIndices, cellTrianglesIndices, new float3x2(corners.c0, corners.c2), cells[i].coordinate,cellDimentions.x,1);
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            float3x4 corners = chunks[i].Corners;
            AddGeometry(vertices, uvs, chunkLinesIndices, chunkTrianglesIndices, new float3x2(corners.c0, corners.c2), chunks[i].coordinate,dimentions.x, chunkSize);
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

        float2 cellCentreUV = math.remap(float2.zero, (float2)cellDimentions, float2.zero, 1f, (float2)coordinate);
        float2 UVoffset = new(1f / cellDimentions.x, 1f / cellDimentions.y);
        int index = coordinate.y * width + coordinate.x;
        coordinate *= coordinateMul;

        int4 indicies = new()
        {
            x = coordinate.y * (cellDimentions.x + 1) + coordinate.x,
            y = (coordinate + new int2(0, coordinateMul)).y * (cellDimentions.x + 1) + (coordinate + new int2(0, coordinateMul)).x,
            z = (coordinate + coordinateMul).y * (cellDimentions.x + 1) + (coordinate + coordinateMul).x,
            w = (coordinate + new int2(coordinateMul, 0)).y * (cellDimentions.x + 1) + (coordinate + new int2(coordinateMul, 0)).x
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

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitialiseGridTexture();
            GenerateGrid();
            GenerateGridMeshJobs();
        }
    }

    private void GenerateGrid()
    {
        chunks.Clear();
        chunkScale =  chunkSize * cellScale;
        for (int i = 0, x = 0; x < dimentions.x; x++)
        {
            for (int z = 0; z < dimentions.y; z++, i++)
            {
                chunks.Add(new Chunk(new float3(x * chunkScale, 0, z * chunkScale), chunkScale, i, new int2(x, z)));
            }
        }

        cellOffset = chunks[0].Corners.c0 + (cellScale /2);
        cellOffset.y = 0;
        cells.Clear();
        for (int i = 0; i < chunks.Count; i++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    cells.Add(new Cell(chunks[i]));
                    chunks[i].virtualSubGrid.Add(cells[^1]);
                }
            }
        }

        int cellCountX = dimentions.x * chunkSize;
        int cellCountZ = dimentions.y * chunkSize;
        //int chunkSize = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;

        for (int gridZ = 0, cellIndex = 0; gridZ < cellCountZ; gridZ++)
        {
            for (int gridX = 0; gridX < cellCountX; gridX++, cellIndex++)
            {
                int chunkX = gridX / chunkSize;
                int chunkZ = gridZ / chunkSize;

                // cells are stored sorted by chunk Index at this part of initilisation.
                int chunkIndex = chunkX + chunkZ * dimentions.x;

                // compute index of current cell in main Grid cellBuffer
                int localX = gridX - chunkX * chunkSize;
                int localZ = gridZ - chunkZ * chunkSize;
                int cellBufferIndex = localX + localZ * chunkSize + (chunkIndex * chunkSize * chunkSize);

                // set cellIndex in HexCellReference buffer
                Cell cell = cells[cellBufferIndex];
                cell.index = cellIndex;
                //cells[cellBufferIndex] = cell;
                    
                cell.coordinate = new int2(gridX, gridZ);
                cell.centerPosition = cellOffset+ new float3(gridX * cellScale, 0, gridZ * cellScale) ;
                cell.UpdateCorners(cellScale);
            }
        }
    }

    private void OnDrawGizmos()
    {
        return;
        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk cur = chunks[i];
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(cur.centerPosition, 0.2f);

            Gizmos.color = Color.red;
            for (int s = 0; s < 4; s++)
            {
                Gizmos.DrawSphere(cur.Corners[s], 0.15f);
            }
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cur = cells[i];
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(cur.centerPosition,0.1f);

            Gizmos.color = Color.yellow;
            for (int s = 0; s < 4; s++)
            {
                Gizmos.DrawSphere(cur.Corners[s], 0.05f);
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

        public Cell (Chunk chunk)
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


    /*
    public void ConstructGridMesh()
    {
        gridMesh.Clear();
        List<Vector3> Vertices = new(data.dimentions.x * 2 + data.dimentions.y * 2 + 4);
        List<int> Triangles = new(data.dimentions.x * 2 + data.dimentions.y * 2 + 4);
        Vector2 a;
        Vector2 b;

        for (int x = 0; x < data.dimentions.x; x++)
        {
            a = this[new int2(x, 0)].WorldBottomLeft;
            b = this[new int2(x, data.dimentions.y - 1)].WorldTopLeft;
            Vertices.Add(new Vector3(a.x, a.y));
            Vertices.Add(new Vector3(b.x, b.y));
            Triangles.Add(Triangles.Count);
            Triangles.Add(Triangles.Count);
            if (x == data.dimentions.x - 1)
            {
                a = this[new int2(x, 0)].WorldBottomRight;
                b = this[new int2(x, data.dimentions.y - 1)].WorldTopRight;
                Vertices.Add(new Vector3(a.x, a.y));
                Vertices.Add(new Vector3(b.x, b.y));
                Triangles.Add(Triangles.Count);
                Triangles.Add(Triangles.Count);
            }
        }

        for (int y = 0; y < data.dimentions.y; y++)
        {
            a = this[new int2(0, y)].WorldBottomLeft;
            b = this[new int2(data.dimentions.x - 1, y)].WorldBottomRight;
            Vertices.Add(new Vector3(a.x, a.y));
            Vertices.Add(new Vector3(b.x, b.y));
            Triangles.Add(Triangles.Count);
            Triangles.Add(Triangles.Count);
            if (y == data.dimentions.y - 1)
            {
                a = this[new int2(0, y)].WorldTopLeft;
                b = this[new int2(data.dimentions.x - 1, y)].WorldTopRight;
                Vertices.Add(new Vector3(a.x, a.y));
                Vertices.Add(new Vector3(b.x, b.y));
                Triangles.Add(Triangles.Count);
                Triangles.Add(Triangles.Count);
            }
        }
        gridMesh.SetVertices(Vertices);
        gridMesh.SetIndices(Triangles, MeshTopology.Lines, 0);
    }
    */
}


public class GridBaker : Baker<GridAuthoring>
{
    public override void Bake(GridAuthoring authoring)
    {
        
    }
}