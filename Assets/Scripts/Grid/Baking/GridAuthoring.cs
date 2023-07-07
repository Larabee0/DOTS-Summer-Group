using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using static UnityEngine.UI.GridLayoutGroup;

public class GridAuthoring : MonoBehaviour
{
    [Tooltip("Chunk grid dimentions")]
    public float3 cellOffset = float3.zero;
    public int2 dimentions = new(64,64);
    [Tooltip("Main Grid Scale"),Min(1)]
    public float cellScale = 1f;
    private float chunkScale;
    [Tooltip("chunk size square it to get cells per chunl"), Min(1)]
    public int chunkSize = 3;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    [SerializeField] private List<Chunk> chunks = new();
    [SerializeField] private List<Cell> cells = new();
    private Mesh gridMesh;

    private void Start()
    {
        if (meshFilter != null && meshRenderer != null)
        {
            GenerateGrid();
            meshFilter.mesh = gridMesh = new Mesh() { name = "Grid Mesh", subMeshCount = 1 };
            // GenerateGridMesh();
        }
    }

    private void GenerateGridMesh()
    {
        gridMesh.Clear();
        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Color> indices = new();

        for (int i = 0, x = 0; x < dimentions.x; x++)
        {
            for (int z = 0; z < dimentions.y; z++, i++)
            {
                vertices.Add(new Vector3(x * cellScale, 0, z * cellScale));
                triangles.Add(i);


            }
        }
        gridMesh.SetVertices(vertices);
        gridMesh.SetIndices(triangles, MeshTopology.Points, 0);
    }

    private void GenerateGrid()
    {
        chunkScale = cellScale + chunkSize;
        for (int i = 0, x = 0; x < dimentions.x; x++)
        {
            for (int z = 0; z < dimentions.y; z++, i++)
            {
                chunks.Add(new Chunk(new float3(x * chunkScale, 0, z * chunkScale), chunkScale, i, new int2(x, z)));
            }
        }

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
                cell.centerPosition = new float3(gridX * cellScale, 0, gridZ * cellScale)- cellOffset;
                cell.UpdateCorners(cellScale);
            }
        }
    }

    private void OnDrawGizmos()
    {
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

    [System.Serializable]
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
            corners.c2 = new float3(topRight.x, 0f, bottomLeft.z);
            corners.c3 = topRight;
        }

        public void AddCell(Cell cell)
        {
            cell.chunkIndex = index;
            cell.chunkCoordinate = coordinate;
            virtualSubGrid.Add(cell);
        }
    }

    [System.Serializable]
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
            corners.c2 = new float3(topRight.x, 0f, bottomLeft.z);
            corners.c3 = topRight;
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
            corners.c2 = new float3(topRight.x, 0f, bottomLeft.z);
            corners.c3 = topRight;
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