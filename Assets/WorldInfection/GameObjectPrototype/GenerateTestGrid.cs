using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class GridCellData : MonoBehaviour
{
    public Vector2Int gridPosition;
    public float infectionProgress;
}

public class GenerateTestGrid : MonoBehaviour
{
    [SerializeField] private int gridSize = 128;
    [SerializeField] private float cellSize = 1f;
    [Space]
    [SerializeField] private Material gridSurfaceMaterial;


    void Start()
    {
        InstantiateGridCells();
        GenerateGridSurface();
    }


    /// <summary>
    /// Instantiates a grid of GameObjects which each store their grid space coordinates and infection progress.
    /// </summary>
    private void InstantiateGridCells()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                GameObject cell = new GameObject("GridCell");
                cell.transform.parent = transform;
                cell.transform.position = new Vector3(x * cellSize, 0f, z * cellSize);

                GridCellData cellData = cell.AddComponent<GridCellData>();
                cellData.gridPosition = new Vector2Int(x, z);
                cellData.infectionProgress = 0f;
            }
        }
    }

    /// <summary>
    /// Generates a mesh representing the grid, with coordinates baked into the vertex color of the bottom left of each quad.
    /// </summary>
    private void GenerateGridSurface()
    {
        GameObject gridSurface = new GameObject("GridSurface");
        gridSurface.transform.parent = transform;

        MeshRenderer surfaceMeshRenderer = gridSurface.AddComponent<MeshRenderer>();
        surfaceMeshRenderer.material = gridSurfaceMaterial;
        surfaceMeshRenderer.material.SetFloat("_GridSize", (float)gridSize);

        MeshFilter surfaceMeshFilter = gridSurface.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh 
        {
            name = "gridSurfaceMesh" 
        };

        Vector3[] vertices = new Vector3[(gridSize+1)*(gridSize+1)];
        Color[] vertexGridCoords = new Color[(gridSize + 1) * (gridSize + 1)];
        for (int i = 0, x = 0; x < gridSize+1; x++)
        {
            for (int z = 0; z < gridSize+1; z++, i++)
            {
                vertices[i] = new Vector3(x*cellSize, 0, z*cellSize);
                vertexGridCoords[i] = new Color(x, z, 0);
            }
        }

        int[] triangles = new int[(gridSize * gridSize) * 6];
        for (int ti = 0, vi = 0, x = 0; x < gridSize; x++, vi++)
        {
            for (int z = 0; z < gridSize; z++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 1] = vi + 1;
                triangles[ti + 5] = triangles[ti + 2] = vi + gridSize + 1;
                triangles[ti + 4] = vi + gridSize + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = vertexGridCoords;
        mesh.triangles = triangles;

        surfaceMeshFilter.mesh = mesh;
    }
}
