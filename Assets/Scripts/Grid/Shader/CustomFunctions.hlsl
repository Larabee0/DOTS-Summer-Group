// Used by Water and Water Shore shader graphs.
void CalculateCoordindates_float (
	float4 vertexColors,
	float width,
	out float2 coord0,
	out float2 coord1,
	out float2 coord2,
	out float2 coord3
) {
    int index0 = vertexColors.x;
    int index1 = vertexColors.y;
    int index2 = vertexColors.z;
    int index3 = vertexColors.w;
    int w = width;
    int y0 = index0 / w;
    int x0 = index0 % w;
    
    int y1 = index1 / w;
    int x1 = index1 % w;
    
    int y2 = index2 / w;
    int x2 = index2 % w;
    
    int y3 = index3 / w;
    int x3 = index3 % w;
    
    coord0 = float2(x0, y0);
    coord1 = float2(x1, y1);
    coord2 = float2(x2, y3);
    coord3 = float2(x3, y3);

}

void OverScanCoordindate_float(
	float4 vertexColors,
	float width,
	out float2 coord
)
{
    int index0 = vertexColors.x;
    int index1 = vertexColors.y;
    int index2 = vertexColors.z;
    int index3 = vertexColors.w;
    int w = width;
    int y0 = index0 / w;
    int x0 = index0 % w;
    
    int y1 = index1 / w;
    int x1 = index1 % w;
    
    int y2 = index2 / w;
    int x2 = index2 % w;
    
    int y3 = index3 / w;
    int x3 = index3 % w;
    
    float2 coord0 = float2(x0, y0);
    float2 coord1 = float2(x1, y1);
    float2 coord2 = float2(x2, y3);
    float2 coord3 = float2(x3, y3);
    
    coord = (coord0 + coord1 + coord2 + coord3) / 4;
    

}