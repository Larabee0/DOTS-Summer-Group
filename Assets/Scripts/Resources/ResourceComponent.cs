using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct ResourceComponent : IComponentData
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
