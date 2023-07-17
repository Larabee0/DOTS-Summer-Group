using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

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
            GenerateGridMesh();
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

    private void GenerateGridMesh()
    {
        if (gridMesh == null)
        {
            return;
        }
        gridMesh.Clear();
        gridMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        gridMesh.subMeshCount = 4;
        cellDimentions = new(dimentions.x * chunkSize, dimentions.y * chunkSize);

        NativeParallelHashMap<float3,int> vertexMap = new((cellDimentions.x + 1) * (cellDimentions.y + 1), Allocator.Temp);
        NativeList<float3> cellLines = new(cellDimentions.x * cellDimentions.y * 8, Allocator.Temp);
        NativeList<float3> chunkLines = new(dimentions.x * dimentions.y * 8, Allocator.Temp);
        NativeList<float3> cellTriangles = new(cellDimentions.x * cellDimentions.y * 6, Allocator.Temp);
        NativeList<float3> chunkTriangles = new(dimentions.x*dimentions.y*6,Allocator.Temp);

        for (int i = 0; i < cells.Count; i++)
        {
            float3x4 corners = cells[i].corners;
            //AddGeometry(vertexMap, cellLines, cellTriangles, corners);
            AddGeometry(vertexMap, cellLines, cellTriangles, corners, cells[i].coordinate);
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            float3x4 corners = chunks[i].corners;
            AddGeometry(vertexMap, chunkLines, chunkTriangles, corners, cells[i].coordinate);
        }

        NativeArray<int> cellLinesIndices = new(cellDimentions.x * cellDimentions.y * 8, Allocator.Temp);
        NativeArray<int> chunkLinesIndices = new(dimentions.x * dimentions.y * 8, Allocator.Temp);
        NativeArray<int> cellTrianglesIndices = new(cellDimentions.x * cellDimentions.y * 6, Allocator.Temp);
        NativeArray<int> chunkTrianglesIndices = new(dimentions.x * dimentions.y * 6, Allocator.Temp);

        for (int i = 0; i < cellLinesIndices.Length; i++)
        {
            cellLinesIndices[i] = vertexMap[cellLines[i]];
        }

        for (int i = 0; i < chunkLinesIndices.Length; i++)
        {
            chunkLinesIndices[i] = vertexMap[chunkLines[i]];
        }

        for (int i = 0; i < cellTrianglesIndices.Length; i++)
        {
            cellTrianglesIndices[i] = vertexMap[cellTriangles[i]];
        }

        for (int i = 0; i < chunkTrianglesIndices.Length; i++)
        {
            chunkTrianglesIndices[i] = vertexMap[chunkTriangles[i]];
        }

        cells.Sort();
        NativeArray<float3> vertices = vertexMap.GetKeyArray(Allocator.Temp);
        NativeArray<float2> uvs = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        float2 UVoffset = new(1f / cellDimentions.x, 1f / cellDimentions.y);

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cur = cells[i];
            float3x4 corners = cells[i].corners;

            int bottomLeft = vertexMap[corners[0]];
            int topLeft = vertexMap[corners[1]];
            int topRight = vertexMap[corners[2]];
            int bottomRight = vertexMap[corners[3]];

            float2 cellCentreUV = math.remap(float2.zero, (float2)cellDimentions, float2.zero, 1f, (float2)cur.coordinate);
            uvs[bottomLeft] = cellCentreUV; // 0,0
            uvs[topLeft] = math.min(new float2(1), new float2(cellCentreUV.x, cellCentreUV.y + UVoffset.y)); //0, 1
            uvs[topRight] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y + UVoffset.y)); //1, 1
            uvs[bottomRight] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y)); //1, 0
        }

        gridMesh.SetVertices(vertices);
        gridMesh.SetUVs(0, uvs);
        gridMesh.SetIndices(cellLinesIndices, MeshTopology.Lines, 0);
        gridMesh.SetIndices(chunkLinesIndices, MeshTopology.Lines, 1);
        gridMesh.SetIndices(cellTrianglesIndices, MeshTopology.Triangles, 2);
        gridMesh.SetIndices(chunkTrianglesIndices, MeshTopology.Triangles, 3);
        Debug.Log(gridMesh.indexFormat);
        Debug.LogFormat("Vertex Buffer {0}", vertices.Length);
        Debug.LogFormat("cellLines (0) {0}", cellLines.Length);
        Debug.LogFormat("chunkLines (1) {0}", chunkLines.Length);
        Debug.LogFormat("cellTriangles (2) {0}", cellTriangles.Length);
        Debug.LogFormat("chunkTriangles (3) {0}", chunkTriangles.Length);
    }
    /*
    private void AddGeometry(NativeParallelHashMap<float3, int> vertexMap, NativeList<int> lines, NativeList<int> triangles, float3x4 corners)
    {
        int4 indicies = int4.zero;
        
        for (int i = 0; i < 4; i++)
        {
            MapVertex(ref indicies, vertexMap, corners, i);
        }

        lines.AddNoResize(indicies.x);
        lines.AddNoResize(indicies.y);

        lines.AddNoResize(indicies.y);
        lines.AddNoResize(indicies.z);

        lines.AddNoResize(indicies.z);
        lines.AddNoResize(indicies.w);

        lines.AddNoResize(indicies.w);
        lines.AddNoResize(indicies.x);

        triangles.AddNoResize(indicies.x);
        triangles.AddNoResize(indicies.z);
        triangles.AddNoResize(indicies.w);


        triangles.AddNoResize(indicies.y);
        triangles.AddNoResize(indicies.z);
        triangles.AddNoResize(indicies.x);
    }
    */
    private  void MapVertex(ref int4 indicies, NativeParallelHashMap<float3, int> vertexMap, float3x4 corners, int index, int vertexIndex)
    {
        int vertexCount = (cellDimentions.x + 1) * (cellDimentions.y + 1);
        vertexMap.TryAdd(corners[index],vertexCount-1- vertexIndex);
        return;
        if (vertexMap.TryAdd(corners[index],index))
        {
            //indicies[index] = value;
        }
        else
        {
            vertexCount = (cellDimentions.x + 1) * (cellDimentions.y + 1);
            indicies[index] = vertexCount - 1 - vertexMap.Count();
            vertexMap.Add(corners[index], indicies[index]);
        }

    }

    private void AddGeometry(NativeParallelHashMap<float3,int> vertexMap, NativeList<float3> lines, NativeList<float3> triangles, float3x4 corners, int2 cellCoordinates)
    {
        int4 indices = new int4(0);

        MapVertex(ref indices, vertexMap, corners, 0,cellCoordinates.y * cellDimentions.x + cellCoordinates.x);
        MapVertex(ref indices, vertexMap, corners, 1,(cellCoordinates.y+1) * cellDimentions.x + cellCoordinates.x);
        MapVertex(ref indices, vertexMap, corners, 2, (cellCoordinates.y + 1) * cellDimentions.x + (cellCoordinates.x+1));
        MapVertex(ref indices, vertexMap, corners, 3, cellCoordinates.y * cellDimentions.x + (cellCoordinates.x + 1));

        int4 indicies = new(0, 1, 2, 3);
        lines.AddNoResize(corners[indicies.x]);
        lines.AddNoResize(corners[indicies.y]);

        lines.AddNoResize(corners[indicies.y]);
        lines.AddNoResize(corners[indicies.z]);

        lines.AddNoResize(corners[indicies.z]);
        lines.AddNoResize(corners[indicies.w]);

        lines.AddNoResize(corners[indicies.w]);
        lines.AddNoResize(corners[indicies.x]);

        triangles.AddNoResize(corners[indicies.x]);
        triangles.AddNoResize(corners[indicies.z]);
        triangles.AddNoResize(corners[indicies.w]);


        triangles.AddNoResize(corners[indicies.y]);
        triangles.AddNoResize(corners[indicies.z]);
        triangles.AddNoResize(corners[indicies.x]);
    }

    /*
    private void MapVertex(ref int4 indicies, NativeParallelHashMap<float3, int> vertexMap, float3x4 corners, int index)
    {
        if (vertexMap.Add(corners[index], out int value))
        {
            indicies[index] = value;
        }
        else
        {
            int vertexCount = (cellDimentions.x + 1) * (cellDimentions.y + 1);
            indicies[index] = vertexCount - 1 - vertexMap.Count();
            vertexMap.Add(corners[index], indicies[index]);
        }

    }
    */
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitialiseGridTexture();
            GenerateGrid();
            GenerateGridMesh();
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

        cellOffset = chunks[0].corners.c0 + (cellScale /2);
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
                Gizmos.DrawSphere(cur.corners[s], 0.15f);
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
                Gizmos.DrawSphere(cur.corners[s], 0.05f);
            }
        }
    }

    //[System.Serializable]
    public class Chunk
    {
        public int index;
        public int2 coordinate;
        public Vector3 centerPosition;
        public float3x4 corners;
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

            corners.c0 = bottomLeft; // 0, 0
            corners.c1 = new float3(bottomLeft.x, 0f, topRight.z);//0,1
            corners.c2 = topRight; // 1,1
            corners.c3 = new float3(topRight.x, 0f, bottomLeft.z);//1,0
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

        public float3x4 corners;


        public Cell(float3 centerPosition, float scale, int index, int2 coordinate)
        {
            this.index = index;
            this.centerPosition = centerPosition;
            this.coordinate = coordinate;

            float halfScale = scale / 2f;

            float3 bottomLeft = centerPosition - halfScale;
            float3 topRight = centerPosition + halfScale;
            bottomLeft.y = topRight.y = 0;

            corners.c0 = bottomLeft;
            corners.c1 = new float3(bottomLeft.x, 0f, topRight.z);
            corners.c2 = topRight;
            corners.c3 = new float3(topRight.x, 0f, bottomLeft.z);
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

            corners.c0 = bottomLeft;
            corners.c1 = new float3(bottomLeft.x, 0f, topRight.z);
            corners.c2 = topRight;
            corners.c3 = new float3(topRight.x, 0f, bottomLeft.z);
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