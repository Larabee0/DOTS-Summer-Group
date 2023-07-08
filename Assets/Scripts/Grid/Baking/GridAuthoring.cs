using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
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

    private float chunkScale;
    private float3 cellOffset = float3.zero;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshRenderer textureDisplay;

    private List<Chunk> chunks = new();
    private List<Cell> cells = new();
    private Mesh gridMesh;

    private Texture2D gridTexture;

    private void Start()
    {
        gridTexture = new(dimentions.x * chunkSize, dimentions.y * chunkSize, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Clamp
        };

        for (int x = 0; x < gridTexture.width; x++)
        {
            for (int y = 0; y < gridTexture.height; y++)
            {
                gridTexture.SetPixel(x, y, UnityEngine.Random.ColorHSV());
            }
        }
        gridTexture.Apply();    
        if(textureDisplay != null)
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

    private void GenerateGridMesh()
    {
        if(gridMesh == null)
        {
            return;
        }
        gridMesh.Clear();
        gridMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        gridMesh.subMeshCount = 4;
        
        List<Vector3> vertices = new();
        List<int> cellLines = new();
        List<int> chunkLines = new();
        List<int> cellTriangles = new();
        List<int> chunkTriangles = new();
        

        for (int i = 0; i < cells.Count; i++)
        {
            float3x4 corners = cells[i].corners;
            AddGeometry(vertices, cellLines, cellTriangles, corners);
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            float3x4 corners = chunks[i].corners;
            AddGeometry(vertices, chunkLines, chunkTriangles, corners);
        }

        HashSet<Vector3> compressedVerticesSet = new(vertices);
        List<Vector3> compressedVertices = new(compressedVerticesSet);


        Dictionary<Vector3, int> vertexRemap = new(compressedVertices.Count);

        for (int i = 0; i < compressedVertices.Count; i++)
        {
            vertexRemap.Add(compressedVertices[i], i);
        }

        for (int i = 0; i < cellLines.Count; i++)
        {
            cellLines[i] = vertexRemap[vertices[cellLines[i]]];
        }
        for (int i = 0; i < chunkLines.Count; i++)
        {
            chunkLines[i] = vertexRemap[vertices[chunkLines[i]]];
        }
        for (int i = 0; i < cellTriangles.Count; i++)
        {
            cellTriangles[i] = vertexRemap[vertices[cellTriangles[i]]];
        }
        for (int i = 0; i < chunkTriangles.Count; i++)
        {
            chunkTriangles[i] = vertexRemap[vertices[chunkTriangles[i]]];
        }

        float4[] colors = new float4[compressedVertices.Count];
        for (int i = 0;i < cells.Count; i++)
        {
            Cell cur = cells[i];
            float3x4 corners = cells[i].corners;
            
            int bottomLeft = vertexRemap[corners[0]];
            int topLeft = vertexRemap[corners[1]];
            int topRight = vertexRemap[corners[2]];
            int bottomRight = vertexRemap[corners[3]];

            int cellIndex = cur.index;
            colors[bottomLeft][2] = cellIndex;
            colors[topLeft][3] = cellIndex;
            colors[topRight][0] = cellIndex;
            colors[bottomRight][1] = cellIndex;
        }

        gridMesh.SetVertices(compressedVertices);
        gridMesh.SetColors(new NativeArray<float4>(colors, Allocator.Temp));
        gridMesh.SetIndices(cellLines, MeshTopology.Lines, 0);
        gridMesh.SetIndices(chunkLines, MeshTopology.Lines, 1);
        gridMesh.SetIndices(cellTriangles, MeshTopology.Triangles, 2);
        gridMesh.SetIndices(chunkTriangles, MeshTopology.Triangles, 3);
        Debug.Log(gridMesh.indexFormat);
        Debug.LogFormat("Vertex Buffer {0}", vertices.Count);
        Debug.LogFormat("Compressed Vertices Buffer {0}", compressedVertices.Count);
        Debug.LogFormat("cellLines (0) {0}", cellLines.Count);
        Debug.LogFormat("chunkLines (1) {0}", chunkLines.Count);
        Debug.LogFormat("cellTriangles (2) {0}", cellTriangles.Count);
        Debug.LogFormat("chunkTriangles (3) {0}", chunkTriangles.Count);
    }

    private static void AddGeometry(List<Vector3> vertices, List<int> lines, List<int> triangles, float3x4 corners)
    {
        int index = vertices.Count;
        vertices.Add(corners.c0);
        vertices.Add(corners.c1);
        vertices.Add(corners.c2);
        vertices.Add(corners.c3);

        lines.Add(index);
        lines.Add(index + 1);

        lines.Add(index + 1);
        lines.Add(index + 2);

        lines.Add(index + 2);
        lines.Add(index + 3);

        lines.Add(index + 3);
        lines.Add(index);

        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);


        triangles.Add(index + 2);
        triangles.Add(index + 3);
        triangles.Add(index);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
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

            corners.c0 = bottomLeft;
            corners.c1 = new float3(bottomLeft.x, 0f, topRight.z);
            corners.c2 = topRight;
            corners.c3 = new float3(topRight.x, 0f, bottomLeft.z);
        }
    }

    //[System.Serializable]
    public class Cell
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