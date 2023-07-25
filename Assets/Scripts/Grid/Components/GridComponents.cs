using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 36 bytes
/// Used to store World Position Data for the given tile the component is attached to.
/// </summary>
public struct TilePositionData : IComponentData
{
    public float3 center;
    public float3x2 diagonals;

    public float3x4 Corners => new()
    {
        c0 = diagonals.c0, // 0, 0 bottom left
        c1 = new float3(diagonals.c0.x, 0f, diagonals.c1.z),//0,1 top left
        c2 = diagonals.c1, // 1,1 top right
        c3 = new float3(diagonals.c1.x, 0f, diagonals.c0.z),//1,0 bottom right
    };
    public float3 Bottomleft => diagonals.c0;
    public float3 TopLeft => new(diagonals.c0.x, 0f, diagonals.c1.z);
    public float3 BottomRight => diagonals.c1;
    public float3 TopRight => new(diagonals.c1.x, 0f, diagonals.c0.z);
}

/// <summary>
/// 16 bytes
/// 
/// </summary>
public struct TileAbstractData : IComponentData
{
    public int2 coordinate;
    public int index;
    public float scale;
}

/// <summary>
/// 32 bytes
/// </summary>
public struct TileNeighboursIndices : IComponentData
{
    public static TileNeighboursIndices Null => new()
    { 
        N = -1,
        NE = -1,
        E = -1,
        SE = -1,
        S = -1,
        SW = -1,
        W = -1,
        NW = -1
    };

    public int N;
    public int NE;
    public int E;
    public int SE;
    public int S;
    public int SW;
    public int W;
    public int NW;

    public int this[SqrDirection d]
    {
        set
        {
            switch (d)
            {
                case SqrDirection.N:
                    N = value;
                    break;
                case SqrDirection.NE:
                    NE = value;
                    break;
                case SqrDirection.E:
                    E = value;
                    break;
                case SqrDirection.SE:
                    SE = value;
                    break;
                case SqrDirection.S:
                    S = value;
                    break;
                case SqrDirection.SW:
                    SW = value;
                    break;
                case SqrDirection.W:
                    W = value;
                    break;
                case SqrDirection.NW:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            SqrDirection.N => N,
            SqrDirection.NE => NE,
            SqrDirection.E => E,
            SqrDirection.SE => SE,
            SqrDirection.S => S,
            SqrDirection.SW => SW,
            SqrDirection.W => W,
            SqrDirection.NW => NW,
            _ => -1
        };
    }

    public int this[int d]
    {

        set
        {
            switch (d)
            {
                case 0:
                    N = value;
                    break;
                case 1:
                    NE = value;
                    break;
                case 2:
                    E = value;
                    break;
                case 3:
                    SE = value;
                    break;
                case 4:
                    S = value;
                    break;
                case 5:
                    SW = value;
                    break;
                case 6:
                    W = value;
                    break;
                case 7:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            0 => N,
            1 => NE,
            2 => E,
            3 => SE,
            4 => S,
            5 => SW,
            6 => W,
            7 => NW,
            _ => -1
        };
    }
}

/// <summary>
/// 64 bytes
/// </summary>
public struct TileNeighboursEntities : IComponentData
{
    public Entity N;
    public Entity NE;
    public Entity E;
    public Entity SE;
    public Entity S;
    public Entity SW;
    public Entity W;
    public Entity NW;

    public Entity this[SqrDirection d]
    {
        set
        {
            switch (d)
            {
                case SqrDirection.N:
                    N = value;
                    break;
                case SqrDirection.NE:
                    NE = value;
                    break;
                case SqrDirection.E:
                    E = value;
                    break;
                case SqrDirection.SE:
                    SE = value;
                    break;
                case SqrDirection.S:
                    S = value;
                    break;
                case SqrDirection.SW:
                    SW = value;
                    break;
                case SqrDirection.W:
                    W = value;
                    break;
                case SqrDirection.NW:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            SqrDirection.N => N,
            SqrDirection.NE => NE,
            SqrDirection.E => E,
            SqrDirection.SE => SE,
            SqrDirection.S => S,
            SqrDirection.SW => SW,
            SqrDirection.W => W,
            SqrDirection.NW => NW,
            _ => Entity.Null
        };
    }

    public Entity this[int d]
    {
        set
        {
            switch (d)
            {
                case 0:
                    N = value;
                    break;
                case 1:
                    NE = value;
                    break;
                case 2:
                    E = value;
                    break;
                case 3:
                    SE = value;
                    break;
                case 4:
                    S = value;
                    break;
                case 5:
                    SW = value;
                    break;
                case 6:
                    W = value;
                    break;
                case 7:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            0 => N,
            1 => NE,
            2 => E,
            3 => SE,
            4 => S,
            5 => SW,
            6 => W,
            7 => NW,
            _ => Entity.Null
        };
    }
}

/// <summary>
/// 32 bytes
/// </summary>
public struct ChunkNeighboursIndices : IComponentData
{
    public static ChunkNeighboursIndices Null => new()
    {
        N = -1,
        NE = -1,
        E = -1,
        SE = -1,
        S = -1,
        SW = -1,
        W = -1,
        NW = -1
    };
    public int N;
    public int NE;
    public int E;
    public int SE;
    public int S;
    public int SW;
    public int W;
    public int NW;

    public int this[SqrDirection d]
    {
        set
        {
            switch (d)
            {
                case SqrDirection.N:
                    N = value;
                    break;
                case SqrDirection.NE:
                    NE = value;
                    break;
                case SqrDirection.E:
                    E = value;
                    break;
                case SqrDirection.SE:
                    SE = value;
                    break;
                case SqrDirection.S:
                    S = value;
                    break;
                case SqrDirection.SW:
                    SW = value;
                    break;
                case SqrDirection.W:
                    W = value;
                    break;
                case SqrDirection.NW:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            SqrDirection.N => N,
            SqrDirection.NE => NE,
            SqrDirection.E => E,
            SqrDirection.SE => SE,
            SqrDirection.S => S,
            SqrDirection.SW => SW,
            SqrDirection.W => W,
            SqrDirection.NW => NW,
            _ => -1
        };
    }

    public int this[int d]
    {

        set
        {
            switch (d)
            {
                case 0:
                    N = value;
                    break;
                case 1:
                    NE = value;
                    break;
                case 2:
                    E = value;
                    break;
                case 3:
                    SE = value;
                    break;
                case 4:
                    S = value;
                    break;
                case 5:
                    SW = value;
                    break;
                case 6:
                    W = value;
                    break;
                case 7:
                    NW = value;
                    break;
            }
        }
        get => d switch
        {
            0 => N,
            1 => NE,
            2 => E,
            3 => SE,
            4 => S,
            5 => SW,
            6 => W,
            7 => NW,
            _ => -1
        };
    }
}

/// <summary>
/// 4 bytes
/// </summary>
public struct ChunkParentReference : IComponentData
{
    public int index;
}

/// <summary>
/// 12 bytes
/// </summary>
public struct CellReferenceBuffer : IBufferElementData
{
    public int index;
    public Entity entity;
}

/// <summary>
/// 12 bytes
/// </summary>
public struct ChunkReferenceBuffer : IBufferElementData
{
    public int index;
    public Entity entity;
}

/// <summary>
/// 20 bytes
/// </summary>
[Serializable]
public struct GridData : IComponentData
{
    [Tooltip("Cell Pixel Dimentions")]
    [SerializeField, Min(1)] public int pixelsPerCell;
    [Tooltip("chunk size square it to get cells per chunl"), Min(1)]
    public int chunkSize;
    [Tooltip("Main Grid Scale"), Min(0.001f)]
    public float cellScale;
    [Tooltip("Chunk grid dimentions")]
    public int2 chunkDimentions;
    public int2 CellDimentions => chunkDimentions * chunkSize;
    public float ChunkScale => chunkSize * cellScale;
    public int ChunkCount => chunkDimentions.x * chunkDimentions.y;
    public int CellCount => chunkDimentions.x * chunkSize * chunkDimentions.y * chunkSize;
    public int2 TextureDimentions => CellDimentions * pixelsPerCell;
}

/// <summary>
/// 8 bytes
/// </summary>
public struct GridReference : IComponentData
{
    public Entity Value;
}

/// <summary>
/// 1 byte
/// </summary>
public struct CellTag : IComponentData { }

/// <summary>
/// 1 byte
/// </summary>
public struct ChunkTag : IComponentData { }

/// <summary>
/// 1 byte
/// </summary>
public struct GridTag : IComponentData { }

public struct GridUninitialised : IComponentData { }
public struct GridCellsUnset : IComponentData { }
public struct SortChunkCellBuffer : IComponentData { }


public struct CellBufferSorter : IComparer<CellReferenceBuffer>
{
    public int Compare(CellReferenceBuffer x, CellReferenceBuffer y)
    {
        return x.index.CompareTo(y.index);
    }
}

public struct ChunkBufferSorter : IComparer<ChunkReferenceBuffer>
{
    public int Compare(ChunkReferenceBuffer x, ChunkReferenceBuffer y)
    {
        return x.index.CompareTo(y.index);
    }
}