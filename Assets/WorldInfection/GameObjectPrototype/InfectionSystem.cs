using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WorldInfectionData : MonoBehaviour
{
    public float[] infectionProgresses;
}

public class InfectionSystem : MonoBehaviour
{
    [SerializeField] private int gridSize;
    private GridCellData[] cells;

    private MeshRenderer gridSurfaceMeshRenderer;

    private WorldInfectionData worldInfectionData;
    public Texture2D infectionTexture;

    private void Start()
    {
        cells = FindObjectsOfType<GridCellData>();
        gridSize = (int)Mathf.Sqrt(cells.Length);
        
        worldInfectionData = gameObject.AddComponent<WorldInfectionData>();

        worldInfectionData.infectionProgresses = new float[gridSize * gridSize];
        infectionTexture = new Texture2D(gridSize, gridSize);

        gridSurfaceMeshRenderer = GameObject.Find("GridSurface").GetComponent<MeshRenderer>();
        gridSurfaceMeshRenderer.material.SetTexture("_InfectionTexture", infectionTexture);
    }

    private void Update()
    {
        UpdateInfectionTexture();
        UpdateCellsInfectionProgress();
    }

    /// <summary>
    /// Copies new values from WorldInfectionData into each cell's infection progress.
    /// </summary>
    private void UpdateCellsInfectionProgress()
    {
        foreach (GridCellData cell in cells)
        {
            int i = (cell.gridPosition.y*gridSize) + cell.gridPosition.x;
            cell.infectionProgress = worldInfectionData.infectionProgresses[i];
        }
    }

    /// <summary>
    /// Copies new values from WorldInfectionData into the infectionTexture.
    /// </summary>
    private void UpdateInfectionTexture()
    {
        for (int x = 0; x < infectionTexture.width; x++)
        {
            for (int y = 0; y < infectionTexture.height; y++)
            {
                int i = (y * gridSize) + x;
                infectionTexture.SetPixel(x, y, new Color(worldInfectionData.infectionProgresses[i],0,0,1));
            }
        }
        infectionTexture.Apply();
    }
}
