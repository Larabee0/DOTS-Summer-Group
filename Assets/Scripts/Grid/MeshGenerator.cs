using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[BurstCompile]
public struct CreateGeometry : IJobFor
{
    public int coordinateMul;
    public int chunkWidth;
    public int geometryWidth;

    // disabling ContainerSafetyRestriction allows out of order write access to arrays
    // and also allows memory aliasing of index buffers
    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<uint> linesIndices;
    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<uint> trianglesIndices;

    public void Execute(int chunkIndex)
    {
        int2 coordinate = new(chunkIndex % chunkWidth, chunkIndex / chunkWidth);

        // get index buffer start positions for this chunk
        int indexLine = chunkIndex * 8; // each chunk gets 8 indices in the line buffer
        int indexTri = chunkIndex * 6; // each chunk gets 6 indices in the triangle buffer

        // convert chunk coordinates to cell coordinate space
        coordinate *= coordinateMul;


        // calculate vertex indices for this chunk's corners (conversion of cell space corindates to mesh space
        // then conversion of coordinates to indices.
        uint4 indicies = new()
        {
            x = (uint)(coordinate.y * geometryWidth + coordinate.x),
            y = (uint)((coordinate + new int2(0, coordinateMul)).y * geometryWidth + (coordinate + new int2(0, coordinateMul)).x),
            z = (uint)((coordinate + coordinateMul).y * geometryWidth + (coordinate + coordinateMul).x),
            w = (uint)((coordinate + new int2(coordinateMul, 0)).y * geometryWidth + (coordinate + new int2(coordinateMul, 0)).x)
        };

        // write index data for lines
        linesIndices[indexLine + 0] = indicies.x;
        linesIndices[indexLine + 1] = indicies.y;

        linesIndices[indexLine + 2] = indicies.y;
        linesIndices[indexLine + 3] = indicies.z;

        linesIndices[indexLine + 4] = indicies.z;
        linesIndices[indexLine + 5] = indicies.w;

        linesIndices[indexLine + 6] = indicies.w;
        linesIndices[indexLine + 7] = indicies.x;

        // write index data for triangles
        trianglesIndices[indexTri + 0] = indicies.x;
        trianglesIndices[indexTri + 1] = indicies.z;
        trianglesIndices[indexTri + 2] = indicies.w;

        trianglesIndices[indexTri + 3] = indicies.y;
        trianglesIndices[indexTri + 4] = indicies.z;
        trianglesIndices[indexTri + 5] = indicies.x;
    }
}

[BurstCompile]
public struct CreateGeometryUV : IJobFor
{
    public float cellScale;
    public float3 cellOffset;
    public float2 UVoffset;
    public int2 cellDimentions;
    public int geometryWidth;

    // disabling ContainerSafetyRestriction allows out of order write access to arrays
    // and also allows memory aliasing of index buffers
    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<float3> vertices;
    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<float2> uvs;

    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<uint> linesIndices;
    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<uint> trianglesIndices;

    public void Execute(int cellIndex)
    {
        int2 coordinate = new(cellIndex % cellDimentions.x, cellIndex / cellDimentions.x);


        // get index buffer start positions for this cell
        int indexLine = cellIndex * 8; // each cell gets 8 indices in the line buffer
        int indexTri = cellIndex * 6; // each cell gets 6 indices in the triangle buffer

        // remap cell corrindates to UV coordinates
        float2 cellCentreUV = math.remap(float2.zero, (float2)cellDimentions, float2.zero, 1f, (float2)coordinate);

        // calculate vertex indices for this cell (the four corners of the square)
        uint4 indicies = new()
        {
            x = (uint)(coordinate.y * geometryWidth + coordinate.x),
            y = (uint)((coordinate + new int2(0, 1)).y * geometryWidth + (coordinate + new int2(0, 1)).x),
            z = (uint)((coordinate + 1).y * geometryWidth + (coordinate + 1).x),
            w = (uint)((coordinate + new int2(1, 0)).y * geometryWidth + (coordinate + new int2(1, 0)).x)
        };

        // only passing the diagonals to the job halves the memory needed to start the job

        float halfScale = cellScale / 2f;
        float3 centerPosition = cellOffset + new float3(coordinate.x * cellScale, 0, coordinate.y * cellScale);
        float3x2 diagnonal = new()
        {
            c0 = centerPosition - halfScale,
            c1 = centerPosition + halfScale
        };
        diagnonal.c0.y = diagnonal.c1.y = 0;

        float3x4 corners = new() // calculating the corners from the diagonals is easy.
        {
            c0 = diagnonal.c0,
            c1 = new float3(diagnonal.c0.x, 0f, diagnonal.c1.z),
            c2 = diagnonal.c1,
            c3 = new float3(diagnonal.c1.x, 0f, diagnonal.c0.z)
        };

        // set the corner positons for each vertex in the cell
        for (int i = 0; i < 4; i++)
        {
            vertices[(int)indicies[i]] = corners[i];
        }

        // set uv coordinates for each vertex in the cell
        uvs[(int)indicies.x] = cellCentreUV; // 0,0
        uvs[(int)indicies.y] = math.min(new float2(1), new float2(cellCentreUV.x, cellCentreUV.y + UVoffset.y)); //0, 1
        uvs[(int)indicies.z] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y + UVoffset.y)); //1, 1
        uvs[(int)indicies.w] = math.min(new float2(1), new float2(cellCentreUV.x + UVoffset.x, cellCentreUV.y)); //1, 0

        // write index data for lines
        linesIndices[indexLine + 0] = indicies.x;
        linesIndices[indexLine + 1] = indicies.y;

        linesIndices[indexLine + 2] = indicies.y;
        linesIndices[indexLine + 3] = indicies.z;

        linesIndices[indexLine + 4] = indicies.z;
        linesIndices[indexLine + 5] = indicies.w;

        linesIndices[indexLine + 6] = indicies.w;
        linesIndices[indexLine + 7] = indicies.x;

        // write index data for triangles
        trianglesIndices[indexTri + 0] = indicies.x;
        trianglesIndices[indexTri + 1] = indicies.z;
        trianglesIndices[indexTri + 2] = indicies.w;

        trianglesIndices[indexTri + 3] = indicies.y;
        trianglesIndices[indexTri + 4] = indicies.z;
        trianglesIndices[indexTri + 5] = indicies.x;
    }
}

[BurstCompile]
public struct SetSubmeshes : IJob
{
    public Mesh.MeshDataArray meshDataArray;
    public int4 subMeshCounts;

    public void Execute()
    {
        Mesh.MeshData meshData = meshDataArray[0];

        // instructs the mesh the bounds of the index buffer each submesh occupies. starting from index 0 to sum of IndexBuffer length.
        // Sub mesh descriptor takes a start index in the index buffer then a length value - the length of that sub meshes index buffer.
        // for sub mesh 0 we start at 0. For sub mesh 1 we start where sub mesh 0 ended aka subMeshCounts.x.
        // we add the length of each submesh on to the next descriptors start position to cover the whole buffer.
        meshData.SetSubMesh(0, new SubMeshDescriptor(0,
            subMeshCounts.x, MeshTopology.Lines), MeshUpdateFlags.DontValidateIndices);

        meshData.SetSubMesh(1, new SubMeshDescriptor(subMeshCounts.x,
            subMeshCounts.y, MeshTopology.Lines), MeshUpdateFlags.DontValidateIndices);

        meshData.SetSubMesh(2, new SubMeshDescriptor(subMeshCounts.x + subMeshCounts.y,
            subMeshCounts.z, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices);

        meshData.SetSubMesh(3, new SubMeshDescriptor(subMeshCounts.x + subMeshCounts.y + subMeshCounts.z,
            subMeshCounts.w, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
    }
}