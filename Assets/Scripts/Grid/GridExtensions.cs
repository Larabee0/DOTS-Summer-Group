using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public static class GridExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetVertexCount(this GridData grid)
    {
        int2 cellDimentions = grid.CellDimentions;
        return (cellDimentions.x + 1) * (cellDimentions.y + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int4 GetSubMeshIndices(this GridData grid)
    {
        int2 cellDimentions = grid.CellDimentions;
        return new()
        {
            x = cellDimentions.x * cellDimentions.y * 8,
            y = grid.chunkDimentions.x * grid.chunkDimentions.y * 8,
            z = cellDimentions.x * cellDimentions.y * 6,
            w = grid.chunkDimentions.x * grid.chunkDimentions.y * 6
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 GetUVOffset(this GridData grid)
    {
        int2 cellDimentions = grid.CellDimentions;
        return new(1f / cellDimentions.x, 1f / cellDimentions.y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetCellOffset(this GridData grid)
    {
        float3 bottomLeftCentre = new(grid.ChunkScale, 0, grid.ChunkScale);
        return bottomLeftCentre - (bottomLeftCentre / 2) + (new float3(grid.cellScale, 0, grid.cellScale) / 2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bounds GetMeshBounds(this GridData grid) => new()
    {
        max = new float3(grid.chunkDimentions.x * grid.ChunkScale, 0, grid.chunkDimentions.y * grid.ChunkScale) + (grid.cellScale / 2f),
        min = new float3(grid.ChunkScale, 0, grid.ChunkScale) - (grid.cellScale / 2f),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AABB GetAABB(this GridData grid) => GetMeshBounds(grid).ToAABB();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 GetDirectionCoordinateOffset(SqrDirection direction) => direction switch
    {
        SqrDirection.N => new int2(0, 1),
        SqrDirection.NE => new int2(1, 1),
        SqrDirection.E => new int2(1, 0),
        SqrDirection.SE => new int2(1, -1),
        SqrDirection.S => new int2(0, -1),
        SqrDirection.SW => new int2(-1, -1),
        SqrDirection.W => new int2(-1, 0),
        SqrDirection.NW => new int2(-1, 1),
        _ => new int2(0, 0),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndexFromCoordinate(int2 coordiante, int2 dimentions)
    {
        if (coordiante.x >= 0 && coordiante.x < dimentions.x && coordiante.y >= 0 && coordiante.y < dimentions.y)
        {
            return coordiante.y * dimentions.x + coordiante.x;
        }
        return -1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 GetCoordinateInDirection(SqrDirection direction, int2 origin)
    {
        return origin + GetDirectionCoordinateOffset(direction);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndexInDirection(SqrDirection direction, int2 origin, int2 dimentions)
    {
        return GetIndexFromCoordinate(GetCoordinateInDirection(direction, origin), dimentions);
    }
}

public enum SqrDirection : byte
{
    N,
    NE,
    E,
    SE,
    S,
    SW,
    W,
    NW
}
