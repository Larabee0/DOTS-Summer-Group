using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class InfectionSystem : MonoBehaviour
{
    [SerializeField] private int gridSize;
    private GridCellData[] cells;
    private MeshRenderer gridSurfaceMeshRenderer;

    // Infection Data
    public Texture2D infectionTexture;
    public float[] worldInfection;

    private void Start()
    {
        cells = FindObjectsOfType<GridCellData>();
        gridSize = (int)Mathf.Sqrt(cells.Length);

        worldInfection = new float[gridSize * gridSize];
        infectionTexture = new Texture2D(gridSize, gridSize);
        infectionTexture.wrapMode = TextureWrapMode.Clamp;

        gridSurfaceMeshRenderer = GameObject.Find("GridSurface").GetComponent<MeshRenderer>();
        gridSurfaceMeshRenderer.material.SetTexture("_InfectionTexture", infectionTexture);

        PlaceInfectionStartPoint(new Vector2Int(23,40));
    }

    private void Update()
    {
        UpdateInfectionTexture();
        UpdateCellsInfectionProgress();

        SimpleInfectionSpread(1f);
        //SimpleRandomInfectionSpread(1f);
    }

    private void PlaceInfectionStartPoint(Vector2Int position)
    {
        int i = (position.y*gridSize) + position.x;
        if (i > worldInfection.Length || i < 0) { Debug.Log("Invalid infection start point!"); return; }

        worldInfection[i] = 1;
    }

    /// <summary>
    /// If a cell is fully infected, increments any directly adjacent cells by infectionRate.
    /// </summary>
    private void SimpleInfectionSpread(float infectionRate)
    {
        for (int i = 0; i < worldInfection.Length; i++)
        {
            if (worldInfection[i] < 1) { continue; }

            // Check if cells are grid edges
            bool topEdge, bottomEdge, leftEdge, rightEdge;
            topEdge = bottomEdge = leftEdge = rightEdge = false;

            if ((i + gridSize) >= worldInfection.Length) { topEdge = true; }
            else if ((i - gridSize) < 0) { bottomEdge = true; }
            if ((i % gridSize) == 0) { leftEdge = true; }
            if (((i + 1) % gridSize) == 0) { rightEdge = true; }

            // Top
            if (!topEdge)
            {
                worldInfection[i + gridSize] += infectionRate * Time.deltaTime;
            }

            // Down
            if (!bottomEdge)
            {
                worldInfection[i - gridSize] += infectionRate * Time.deltaTime;
            }

            // Left
            if (!leftEdge)
            {
                worldInfection[i - 1] += infectionRate * Time.deltaTime;
            }

            // Right
            if (!rightEdge)
            {
                worldInfection[i + 1] += infectionRate * Time.deltaTime;
            }

            // Top-Left
            if (!topEdge && !leftEdge)
            {
                worldInfection[i + gridSize - 1] += infectionRate * Time.deltaTime;
            }

            // Top-Right
            if (!topEdge && !rightEdge)
            {
                worldInfection[i + gridSize + 1] += infectionRate * Time.deltaTime;
            }

            // Bottom-Left
            if (!bottomEdge && !leftEdge)
            {
                worldInfection[i - gridSize - 1] += infectionRate * Time.deltaTime;
            }

            // Bottom-Right
            if (!bottomEdge && !rightEdge)
            {
                worldInfection[i - gridSize + 1] += infectionRate * Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// If a cell is fully infected, increments a random directly adjacent cell by infectionRate.
    /// </summary>
    private void SimpleRandomInfectionSpread(float infectionRate)
    {
        for (int i = 0; i < worldInfection.Length; i++)
        {
            if (worldInfection[i] < 1) { continue; }

            // Check if cells are grid edges
            bool topEdge, bottomEdge, leftEdge, rightEdge;
            topEdge = bottomEdge = leftEdge = rightEdge = false;

            if ((i + gridSize) >= worldInfection.Length) { topEdge = true; }
            else if ((i - gridSize) < 0) { bottomEdge = true; }
            if ((i % gridSize) == 0) { leftEdge = true; }
            if (((i + 1) % gridSize) == 0) { rightEdge = true; }

            int direction = UnityEngine.Random.Range(0, 8);

            // Top
            if (!topEdge && direction == 0)
            {
                worldInfection[i + gridSize] += infectionRate * Time.deltaTime;
            }

            // Down
            if (!bottomEdge && direction == 1)
            {
                worldInfection[i - gridSize] += infectionRate * Time.deltaTime;
            }

            // Left
            if (!leftEdge && direction == 2)
            {
                worldInfection[i - 1] += infectionRate * Time.deltaTime;
            }

            // Right
            if (!rightEdge && direction == 3)
            {
                worldInfection[i + 1] += infectionRate * Time.deltaTime;
            }

            // Top-Left
            if (!topEdge && !leftEdge && direction == 4)
            {
                worldInfection[i + gridSize - 1] += infectionRate * Time.deltaTime;
            }

            // Top-Right
            if (!topEdge && !rightEdge && direction == 5)
            {
                worldInfection[i + gridSize + 1] += infectionRate * Time.deltaTime;
            }

            // Bottom-Left
            if (!bottomEdge && !leftEdge && direction == 6)
            {
                worldInfection[i - gridSize - 1] += infectionRate * Time.deltaTime;
            }

            // Bottom-Right
            if (!bottomEdge && !rightEdge && direction == 7)
            {
                worldInfection[i - gridSize + 1] += infectionRate * Time.deltaTime;
            }
        }
    }


    /// <summary>
    /// Copies new values from worldInfection into each cell's infection progress.
    /// </summary>
    private void UpdateCellsInfectionProgress()
    {
        foreach (GridCellData cell in cells)
        {
            int i = (cell.gridPosition.y * gridSize) + cell.gridPosition.x;
            cell.infectionProgress = worldInfection[i];
        }
    }

    /// <summary>
    /// Copies new values from worldInfection into the infectionTexture.
    /// </summary>
    private void UpdateInfectionTexture()
    {
        for (int x = 0; x < infectionTexture.width; x++)
        {
            for (int y = 0; y < infectionTexture.height; y++)
            {
                int i = (y * gridSize) + x;
                infectionTexture.SetPixel(x, y, new Color(worldInfection[i],0,0,1));
            }
        }
        infectionTexture.Apply();
    }
}
