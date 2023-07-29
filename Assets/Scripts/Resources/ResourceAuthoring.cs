using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ResourceAuthoring : MonoBehaviour
{
    public int chunk;
    public Vector3 position;
    public char type;

    public int northNeighbour;
    public int southNeighbour;
    public int eastNeighbour;
    public int westNeighbour;
    public int northEastNeighbour;
    public int northWestNeighbour;
    public int southEastNeighbour;
    public int southWestNeighbour;
}

public class ResourceBaker : Baker<ResourceAuthoring>
{
    public override void Bake(ResourceAuthoring authoring)
    {
        AddComponent(new ResourceComponent
        {
            chunk = authoring.chunk,
            position = authoring.position,
            type = authoring.type,
            northNeighbour = authoring.northNeighbour,
            southNeighbour = authoring.southNeighbour,
            eastNeighbour = authoring.eastNeighbour,
            westNeighbour = authoring.westNeighbour,
            northEastNeighbour = authoring.northEastNeighbour,
            northWestNeighbour = authoring.southWestNeighbour,
            southEastNeighbour = authoring.southEastNeighbour,
            southWestNeighbour = authoring.southWestNeighbour,
        });
    }
}
