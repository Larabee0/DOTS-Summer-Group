using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class InfectionGridAuthoring : MonoBehaviour
{
    [Tooltip("Chunk grid dimentions")]
    public int2 dimentions = new(64,64);
    [Tooltip("Main Grid Scale"),Min(0.001f)]
    public float cellScale = 1f;
    [Tooltip("chunk size square it to get cells per chunl"), Min(1)]
    public int chunkSize = 3;

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

    //INFECTION STUFF
    public float[] worldInfection;

    private void Start()
    {
        InitialiseWorldInfection(new int2[] 
        {
            new int2(0,0),
            new int2(1,0),
            new int2(1,1),
            new int2(0,1),
            new int2(1,2),
            new int2(2,2),
        });

        InitialiseGridTexture();

        if (textureDisplay != null)
        {
            textureDisplay.material.mainTexture = gridTexture;
        }
        if (meshFilter != null && meshRenderer != null)
        {
            meshRenderer.materials[2].SetTexture("_InfectionTexture", gridTexture);
            GenerateGrid();
            meshFilter.mesh = gridMesh = new Mesh() { name = "Grid Mesh", subMeshCount = 2 };
            GenerateGridMesh();
        }
    }

    private void UpdateInfectionShaderProperties() 
    {
        meshRenderer.materials[2].SetVector("_Dimentions", new Vector4(dimentions.x, dimentions.y, 0, 0));
        meshRenderer.materials[2].SetFloat("_ChunkSize", chunkSize);
    }

    private void InitialiseWorldInfection(int2[] infectionSpawnCoordinates)
    {
        if (worldInfection != null)
        {
            Array.Resize(ref worldInfection, dimentions.x * chunkSize * dimentions.y * chunkSize);
        }
        else
        {
            worldInfection = new float[dimentions.x * chunkSize * dimentions.y * chunkSize];
        }

        // Create starting infected cells
        foreach (int2 coordinate in infectionSpawnCoordinates)
        {
            if (coordinate.x > (dimentions.x * chunkSize) || coordinate.y > (dimentions.y * chunkSize)) 
            { 
              Debug.Log("Infection spawnpoint out of bounds"); 
              continue; 
            }
            worldInfection[(coordinate.y * dimentions.x * chunkSize) + coordinate.x] = 1f;
        }
    }

    private void InitializeWorldInfection()
    {
        if (worldInfection != null)
        {
            Array.Resize(ref worldInfection, dimentions.x * chunkSize * dimentions.y * chunkSize);
        }
        else
        {
            worldInfection = new float[dimentions.x * chunkSize * dimentions.y * chunkSize];
        }
    }

    private void InitialiseGridTexture()
    {
        if (gridTexture != null)
        {
            gridTexture.Reinitialize(dimentions.x * chunkSize, dimentions.y * chunkSize);

            gridTexture.filterMode = FilterMode.Point;
            gridTexture.wrapModeU = TextureWrapMode.Mirror;
            gridTexture.wrapModeV = TextureWrapMode.Mirror;
        }
        else
        {
            gridTexture = new(dimentions.x * chunkSize, dimentions.y * chunkSize, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Mirror,
                wrapModeV = TextureWrapMode.Mirror
            };
        }
        tWidth = gridTexture.width;
        tHeight = gridTexture.height;

        for (int x = 0; x < gridTexture.width; x += 1)
        {
            for (int y = 0; y < gridTexture.height; y += 1)
            {
                Color infectionColor = new Color(worldInfection[(y * dimentions.x * chunkSize) + x], 0, 0);
                gridTexture.SetPixel(x, y, infectionColor);
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

        Dictionary<Vector3, int> vertexMap = new();
        List<int> cellLines = new();
        List<int> chunkLines = new();
        List<int> cellTriangles = new();
        List<int> chunkTriangles = new();


        for (int i = 0; i < cells.Count; i++)
        {
            float3x4 corners = cells[i].corners;
            AddGeometry(vertexMap, cellLines, cellTriangles, corners);
        }
        for (int i = 0; i < chunks.Count; i++)
        {
            float3x4 corners = chunks[i].corners;
            AddGeometry(vertexMap, chunkLines, chunkTriangles, corners);
        }


        List<Vector3> vertices = new(vertexMap.Keys);
        cells.Sort();
        NativeArray<float2> uvs = new(vertices.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        float2 UVoffset = new(1f / (dimentions.x * chunkSize), 1f / (dimentions.y * chunkSize));
        float2 cellDimentions = new(dimentions.x * chunkSize, dimentions.y * chunkSize);
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cur = cells[i];
            float3x4 corners = cells[i].corners;

            int bottomLeft = vertexMap[corners[0]];
            int topLeft = vertexMap[corners[1]];
            int topRight = vertexMap[corners[2]];
            int bottomRight = vertexMap[corners[3]];

            float2 cellCentreUV = math.remap(float2.zero, cellDimentions, float2.zero, 1f, (float2)cur.coordinate);
            uvs[bottomLeft] = cellCentreUV;
            uvs[topLeft] = math.min(new float2(1), new float2(cellCentreUV.x, cellCentreUV.y + UVoffset.y));
            uvs[topRight] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y + UVoffset.y)); //1, 1
            uvs[bottomRight] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y)); //1, 0
        }

        gridMesh.SetVertices(vertices);
        gridMesh.SetUVs(0, uvs);
        gridMesh.SetIndices(cellLines, MeshTopology.Lines, 0);
        gridMesh.SetIndices(chunkLines, MeshTopology.Lines, 1);
        gridMesh.SetIndices(cellTriangles, MeshTopology.Triangles, 2);
        gridMesh.SetIndices(chunkTriangles, MeshTopology.Triangles, 3);
        Debug.Log(gridMesh.indexFormat);
        Debug.LogFormat("Vertex Buffer {0}", vertices.Count);
        Debug.LogFormat("cellLines (0) {0}", cellLines.Count);
        Debug.LogFormat("chunkLines (1) {0}", chunkLines.Count);
        Debug.LogFormat("cellTriangles (2) {0}", cellTriangles.Count);
        Debug.LogFormat("chunkTriangles (3) {0}", chunkTriangles.Count);
    }

    private void AddGeometry(Dictionary<Vector3, int> vertexMap, List<int> lines, List<int> triangles, float3x4 corners)
    {
        int4 indicies = int4.zero;
        
        for (int i = 0; i < 4; i++)
        {
            MapVertex(ref indicies, vertexMap, corners, i);
        }

        lines.Add(indicies.x);
        lines.Add(indicies.y);

        lines.Add(indicies.y);
        lines.Add(indicies.z);

        lines.Add(indicies.z);
        lines.Add(indicies.w);

        lines.Add(indicies.w);
        lines.Add(indicies.x);

        triangles.Add(indicies.x);
        triangles.Add(indicies.z);
        triangles.Add(indicies.w);


        triangles.Add(indicies.y);
        triangles.Add(indicies.z);
        triangles.Add(indicies.x);
    }

    private static void MapVertex(ref int4 indicies, Dictionary<Vector3, int> vertexMap, float3x4 corners, int index)
    {
        if (vertexMap.TryGetValue(corners[index], out int value))
        {
            indicies[index] = value;
        }
        else
        {
            indicies[index] = vertexMap.Count;
            vertexMap.Add(corners[index], indicies[index]);
        }

    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitializeWorldInfection();
            InitialiseGridTexture();
            UpdateInfectionShaderProperties();

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


public class InfectionGridBaker : Baker<InfectionGridAuthoring>
{
    public override void Bake(InfectionGridAuthoring authoring)
    {
        
    }
}