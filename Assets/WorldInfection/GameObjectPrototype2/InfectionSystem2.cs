using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfectionSystem2 : MonoBehaviour
{
    [SerializeField] private float infectionRate;
    [SerializeField] private float infectionThreshold;

    private InfectionGridAuthoring infectionGrid;
    private int gridSize;

    private void Start()
    {
        infectionGrid = GetComponent<InfectionGridAuthoring>();
        gridSize = infectionGrid.dimentions.x * infectionGrid.chunkSize;
    }

    void Update()
    {
        SimpleInfectionSpread(ref infectionGrid.worldInfection, gridSize, infectionRate, infectionThreshold);
        infectionGrid.UpdateInfectionInTextures();
    }

    /// <summary>
    /// If a cell is fully infected, increments any directly adjacent cells by infectionRate.
    /// </summary>
    private void SimpleInfectionSpread(ref float[] worldInfection, int gridSize, float infectionRate, float infectionThreshold)
    {

        for (int i = 0; i < worldInfection.Length; i++)
        {
            if (worldInfection[i] < infectionThreshold) { continue; }

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
}
