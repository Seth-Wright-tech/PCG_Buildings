using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building_Mesh_Gen : MonoBehaviour
{
    public float cellSize = 3f;
    public float floorHeight = 3f;
    
    public enum WindowType { BigWindows, TallWindows }
    public enum DoorType { SingleDoor, DoubleDoor }
    public enum RoofType { Gable, Flat }
    
    public WindowType windowType = WindowType.BigWindows;
    public DoorType doorType = DoorType.SingleDoor;
    public RoofType roofType = RoofType.Flat;
    // How far a roof will extend into an adjacent roof to simulate a connected look (in cell units)
    public float roofConnectionOverlap = 0.5f;
    
    private Vector2Int doorCell;
    private int doorDirection;

    // Separate lists for each material
    private List<Vector3> wallVerts = new List<Vector3>();
    private List<int> wallTris = new List<int>();
    private List<Vector3> wallNorms = new List<Vector3>();
    private List<Vector2> wallUVs = new List<Vector2>();
    
    private List<Vector3> windowVerts = new List<Vector3>();
    private List<int> windowTris = new List<int>();
    private List<Vector3> windowNorms = new List<Vector3>();
    private List<Vector2> windowUVs = new List<Vector2>();
    
    private List<Vector3> doorVerts = new List<Vector3>();
    private List<int> doorTris = new List<int>();
    private List<Vector3> doorNorms = new List<Vector3>();
    private List<Vector2> doorUVs = new List<Vector2>();

    // Roof/trim lists (separate submesh)
    private List<Vector3> roofVerts = new List<Vector3>();
    private List<int> roofTris = new List<int>();
    private List<Vector3> roofNorms = new List<Vector3>();
    private List<Vector2> roofUVs = new List<Vector2>();

    public Mesh GenerateBuildingMesh(bool[,] footprint, int[,] heights, Vector2Int doorPosition, int doorDir)
    {
        doorCell = doorPosition;
        doorDirection = doorDir;
        
        // Clear all lists
        wallVerts.Clear(); wallTris.Clear(); wallNorms.Clear(); wallUVs.Clear();
        windowVerts.Clear(); windowTris.Clear(); windowNorms.Clear(); windowUVs.Clear();
        doorVerts.Clear(); doorTris.Clear(); doorNorms.Clear(); doorUVs.Clear();

        int width = footprint.GetLength(0);
        int depth = footprint.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                if (!footprint[x, y]) continue;

                float topH = heights[x, y] * floorHeight;
                Vector3 basePos = new Vector3(x * cellSize, 0f, y * cellSize);

                Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
                Vector3[] dirNormals = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dirs[d].x;
                    int ny = y + dirs[d].y;
                    bool neighborInside = nx >= 0 && ny >= 0 && nx < width && ny < depth && footprint[nx, ny];
                    float neighborTop = neighborInside ? heights[nx, ny] * floorHeight : 0f;

                    if (!neighborInside || neighborTop < topH - 1e-5f)
                    {
                        float wallBottom = neighborTop;
                        float wallHeight = topH - wallBottom;
                        Vector3 wallBasePos = new Vector3(basePos.x, wallBottom, basePos.z);
                        
                        bool isDoor = (x == doorCell.x && y == doorCell.y && d == doorDirection);
                        
                        AddWallForDirection(wallBasePos, wallHeight, d, dirNormals[d], isDoor);
                    }
                }

                Vector3 topBL = basePos + new Vector3(0f, topH, 0f);
                Vector3 topBR = basePos + new Vector3(cellSize, topH, 0f);
                Vector3 topTR = basePos + new Vector3(cellSize, topH, cellSize);
                Vector3 topTL = basePos + new Vector3(0f, topH, cellSize);
                
                // Only add flat top if using flat roof (goes to roof submesh)
                if (roofType == RoofType.Flat)
                {
                    AddQuad(roofVerts, roofTris, roofNorms, roofUVs, topBL, topBR, topTR, topTL, Vector3.up);
                }
            }
        }

        // Add roof after all walls are generated
        if (roofType == RoofType.Gable)
        {
            AddGableRoof(footprint, heights);
        }
        else
        {
            AddFlatRoof(footprint, heights);
        }

        // Combine all submeshes into one mesh
        Mesh mesh = new Mesh();
            
        // Combine all vertices
        List<Vector3> allVerts = new List<Vector3>();
        allVerts.AddRange(wallVerts);
        int windowVertOffset = allVerts.Count;
        allVerts.AddRange(windowVerts);
        int doorVertOffset = allVerts.Count;
        allVerts.AddRange(doorVerts);
        int roofVertOffset = allVerts.Count;
        allVerts.AddRange(roofVerts);
            
        // Combine all normals
        List<Vector3> allNorms = new List<Vector3>();
        allNorms.AddRange(wallNorms);
        allNorms.AddRange(windowNorms);
        allNorms.AddRange(doorNorms);
        allNorms.AddRange(roofNorms);
            
        // Combine all UVs
        List<Vector2> allUVs = new List<Vector2>();
        allUVs.AddRange(wallUVs);
        allUVs.AddRange(windowUVs);
        allUVs.AddRange(doorUVs);
        allUVs.AddRange(roofUVs);
            
        mesh.SetVertices(allVerts);
        mesh.SetNormals(allNorms);
        mesh.SetUVs(0, allUVs);
        
        // Set submeshes
        mesh.subMeshCount = 4;
        
        // Submesh 0: Walls
        mesh.SetTriangles(wallTris, 0);
        
        // Submesh 1: Windows (offset indices)
        List<int> offsetWindowTris = new List<int>();
        foreach (int tri in windowTris)
        {
            offsetWindowTris.Add(tri + windowVertOffset);
        }
        mesh.SetTriangles(offsetWindowTris, 1);
        
        // Submesh 2: Doors (offset indices)
        List<int> offsetDoorTris = new List<int>();
        foreach (int tri in doorTris)
        {
            offsetDoorTris.Add(tri + doorVertOffset);
        }
        mesh.SetTriangles(offsetDoorTris, 2);

        // Submesh 3: Roof/trim (offset indices)
        List<int> offsetRoofTris = new List<int>();
        foreach (int tri in roofTris)
        {
            offsetRoofTris.Add(tri + roofVertOffset);
        }
        mesh.SetTriangles(offsetRoofTris, 3);
        
        mesh.RecalculateBounds();
        return mesh;
    }

    void AddWallForDirection(Vector3 basePos, float h, int dirIndex, Vector3 normal, bool isDoor)
    {
        float x0 = basePos.x;
        float x1 = basePos.x + cellSize;
        float z0 = basePos.z;
        float z1 = basePos.z + cellSize;

        float yBase = basePos.y;
        int floors = Mathf.CeilToInt(h / floorHeight);

        float frameHorizontal, frameVertical, depth;
        
        if (windowType == WindowType.BigWindows)
        {
            frameHorizontal = 0.3f;
            frameVertical = 0.3f;
            depth = 0.15f;
        }
        else
        {
            frameHorizontal = 0.8f;
            frameVertical = 0.2f;
            depth = 0.15f;
        }

        for (int i = 0; i < floors; i++)
        {
            float y0 = yBase + i * floorHeight;
            float y1 = Mathf.Min(yBase + (i + 1) * floorHeight, yBase + h);

            Vector3 bl, br, tr, tl;

            if (dirIndex == 0)
            {
                bl = new Vector3(x0, y0, z1);
                br = new Vector3(x1, y0, z1);
                tr = new Vector3(x1, y1, z1);
                tl = new Vector3(x0, y1, z1);
            } 
            else if (dirIndex == 1)
            {
                bl = new Vector3(x1, y0, z1);
                br = new Vector3(x1, y0, z0);
                tr = new Vector3(x1, y1, z0);
                tl = new Vector3(x1, y1, z1);
            } 
            else if (dirIndex == 2)
            {
                bl = new Vector3(x1, y0, z0);
                br = new Vector3(x0, y0, z0);
                tr = new Vector3(x0, y1, z0);
                tl = new Vector3(x1, y1, z0);
            } 
            else
            {
                bl = new Vector3(x0, y0, z0);
                br = new Vector3(x0, y0, z1);
                tr = new Vector3(x0, y1, z1);
                tl = new Vector3(x0, y1, z0);
            }

            if (isDoor && i == 0)
            {
                AddDoor(bl, br, tr, tl, normal);
            }
            else
            {
                Vector3 rightDir = (br - bl).normalized;
                Vector3 upDir = (tl - bl).normalized;
                
                Vector3 insetBL = bl + rightDir * frameHorizontal + upDir * frameVertical - normal * depth;
                Vector3 insetBR = br - rightDir * frameHorizontal + upDir * frameVertical - normal * depth;
                Vector3 insetTR = tr - rightDir * frameHorizontal - upDir * frameVertical - normal * depth;
                Vector3 insetTL = tl + rightDir * frameHorizontal - upDir * frameVertical - normal * depth;

                Vector3 frameBL = bl + rightDir * frameHorizontal + upDir * frameVertical;
                Vector3 frameBR = br - rightDir * frameHorizontal + upDir * frameVertical;
                Vector3 frameTR = tr - rightDir * frameHorizontal - upDir * frameVertical;
                Vector3 frameTL = tl + rightDir * frameHorizontal - upDir * frameVertical;

                // Wall frame goes to wall mesh
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, bl, frameBL, frameTL, tl, normal);
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBR, br, tr, frameTR, normal);
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTL, frameTR, tr, tl, normal);
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, bl, br, frameBR, frameBL, normal);

                // Window glass goes to window mesh
                AddQuad(windowVerts, windowTris, windowNorms, windowUVs, insetBL, insetBR, insetTR, insetTL, normal);

                // Recess walls go to wall mesh
                Vector3 sideNormal;
                
                sideNormal = Vector3.Cross(frameBR - frameBL, insetBL - frameBL).normalized;
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBL, frameBR, insetBR, insetBL, sideNormal);
                
                sideNormal = Vector3.Cross(frameTR - frameBR, insetBR - frameBR).normalized;
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBR, frameTR, insetTR, insetBR, sideNormal);
                
                sideNormal = Vector3.Cross(frameTL - frameTR, insetTR - frameTR).normalized;
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTR, frameTL, insetTL, insetTR, sideNormal);
                
                sideNormal = Vector3.Cross(frameBL - frameTL, insetTL - frameTL).normalized;
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTL, frameBL, insetBL, insetTL, sideNormal);
            }
        }
    }

    void AddDoor(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 normal)
    {
        float depth = 0.1f;
        Vector3 rightDir = (br - bl).normalized;
        Vector3 upDir = (tl - bl).normalized;
        
        float doorHeight = 2.2f;
        float frameThickness = 0.15f;
        
        float doorWidth, doorOffset;
        
        if (doorType == DoorType.SingleDoor)
        {
            doorWidth = 1.0f;
            doorOffset = (cellSize - doorWidth) * 0.5f;
        }
        else
        {
            doorWidth = 2.0f;
            doorOffset = (cellSize - doorWidth) * 0.5f;
        }
        
        Vector3 doorBL = bl + rightDir * doorOffset;
        Vector3 doorBR = bl + rightDir * (doorOffset + doorWidth);
        Vector3 doorTR = bl + rightDir * (doorOffset + doorWidth) + upDir * doorHeight;
        Vector3 doorTL = bl + rightDir * doorOffset + upDir * doorHeight;
        
        Vector3 frameBL = doorBL + rightDir * frameThickness;
        Vector3 frameBR = doorBR - rightDir * frameThickness;
        Vector3 frameTR = doorTR - rightDir * frameThickness - upDir * frameThickness;
        Vector3 frameTL = doorTL + rightDir * frameThickness - upDir * frameThickness;
        
        Vector3 recessBL = frameBL - normal * depth;
        Vector3 recessBR = frameBR - normal * depth;
        Vector3 recessTR = frameTR - normal * depth;
        Vector3 recessTL = frameTL - normal * depth;
        
        // Wall sections go to wall mesh
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, bl, doorBL, doorTL, tl, normal);
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, doorBR, br, tr, doorTR, normal);
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, doorTL, doorTR, tr, tl, normal);
        
        // Door frame goes to wall mesh
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, doorBL, frameBL, frameTL, doorTL, normal);
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBR, doorBR, doorTR, frameTR, normal);
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTL, frameTR, doorTR, doorTL, normal);
        
        // Door surface goes to door mesh
        AddQuad(doorVerts, doorTris, doorNorms, doorUVs, recessBL, recessBR, recessTR, recessTL, normal);
        
        // Recess walls go to wall mesh
        Vector3 sideNormal;
        
        sideNormal = Vector3.Cross(frameBR - frameBL, recessBL - frameBL).normalized;
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBL, frameBR, recessBR, recessBL, sideNormal);
        
        sideNormal = Vector3.Cross(frameTR - frameBR, recessBR - frameBR).normalized;
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameBR, frameTR, recessTR, recessBR, sideNormal);
        
        sideNormal = Vector3.Cross(frameTL - frameTR, recessTR - frameTR).normalized;
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTR, frameTL, recessTL, recessTR, sideNormal);
        
        sideNormal = Vector3.Cross(frameBL - frameTL, recessTL - frameTL).normalized;
        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, frameTL, frameBL, recessBL, recessTL, sideNormal);
        
        // Door divider goes to wall mesh
        if (doorType == DoorType.DoubleDoor)
        {
            float dividerWidth = 0.1f;
            float centerX = doorOffset + doorWidth * 0.5f;
            
            Vector3 divBL = bl + rightDir * (centerX - dividerWidth * 0.5f);
            Vector3 divBR = bl + rightDir * (centerX + dividerWidth * 0.5f);
            Vector3 divTR = bl + rightDir * (centerX + dividerWidth * 0.5f) + upDir * doorHeight;
            Vector3 divTL = bl + rightDir * (centerX - dividerWidth * 0.5f) + upDir * doorHeight;
            
            AddQuad(wallVerts, wallTris, wallNorms, wallUVs, divBL, divBR, divTR, divTL, normal);
        }
    }

    void AddQuad(List<Vector3> v, List<int> t, List<Vector3> n, List<Vector2> uv, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 desiredNormal)
    {
        int start = v.Count;
        v.Add(bl); v.Add(br); v.Add(tr); v.Add(tl);

        Vector3 triNormal = Vector3.Cross(br - bl, tr - bl);
        bool flip = Vector3.Dot(triNormal, desiredNormal) < 0f;

        n.Add(desiredNormal); n.Add(desiredNormal); n.Add(desiredNormal); n.Add(desiredNormal);

        Vector3 right = (br - bl);
        Vector3 up = (tl - bl);
        float width = right.magnitude;
        float height = up.magnitude;
        
        float uvScale = 0.5f;
        
        uv.Add(new Vector2(0, 0));
        uv.Add(new Vector2(width * uvScale, 0));
        uv.Add(new Vector2(width * uvScale, height * uvScale));
        uv.Add(new Vector2(0, height * uvScale));

        if (!flip)
        {
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
            t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }
        else
        {
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }
    }
    void CreateSimpleGableForRegion(int minX, int maxX, int minY, int maxY, float baseHeight, float roofPitch, float regionWidth, float regionDepth, bool[,] footprint, float connectionOverlap, int[,] heights)
    {
        float overhang = 0.3f;
        float centerX = (minX + maxX + 1) * 0.5f * cellSize;
        float centerY = (minY + maxY + 1) * 0.5f * cellSize;
        
        // We'll extend the ridge/eaves toward neighboring footprint cells if present, using connectionOverlap
        if (regionWidth > regionDepth)
        {
            float roofHeight = baseHeight + regionDepth * 0.5f * roofPitch;
            float startX = (minX * cellSize) - overhang;
            float endX = ((maxX + 1) * cellSize) + overhang;
            bool hasStartConnection = false;
            bool hasEndConnection = false;
            float startConnectionHeight = roofHeight;
            float endConnectionHeight = roofHeight;

            // Check for connection toward negative X; only if neighbor cells are same integer height
            int myH = Mathf.RoundToInt(baseHeight / floorHeight);
            if (minX - 1 >= 0)
            {
                for (int yy = minY; yy <= maxY; yy++)
                {
                    if (!footprint[minX - 1, yy]) continue;
                    if (heights[minX - 1, yy] != myH) continue; // require same height
                    hasStartConnection = true;
                    float extensionDist = connectionOverlap * cellSize;
                    startX -= extensionDist;

                    // Calculate neighbor's roof center and dimensions
                    float neighborCenterZ = (yy + 0.5f) * cellSize;
                    float distFromNeighborRidge = Mathf.Abs(centerY - neighborCenterZ);
                    startConnectionHeight = baseHeight + (regionDepth * 0.5f - distFromNeighborRidge) * roofPitch;
                    break;
                }
            }

            // Check for connection toward positive X
                if (maxX + 1 < footprint.GetLength(0))
            {
                for (int yy = minY; yy <= maxY; yy++)
                {
                    if (!footprint[maxX + 1, yy]) continue;
                    if (heights[maxX + 1, yy] != myH) continue; // require same height
                    hasEndConnection = true;
                    float extensionDist = connectionOverlap * cellSize;
                    endX += extensionDist;

                    float neighborCenterZ = (yy + 0.5f) * cellSize;
                    float distFromNeighborRidge = Mathf.Abs(centerY - neighborCenterZ);
                    endConnectionHeight = baseHeight + (regionDepth * 0.5f - distFromNeighborRidge) * roofPitch;
                    break;
                }
            }

            float overlapDist = connectionOverlap * cellSize;
            // compute slope-based adjustment so the extension lies on our roof plane
            float slopeHeightAdjust = overlapDist * roofPitch;

            float ridgeStartY = hasStartConnection ? Mathf.Min(startConnectionHeight, roofHeight) : roofHeight;
            float ridgeEndY = hasEndConnection ? Mathf.Min(endConnectionHeight, roofHeight) : roofHeight;

            // project the overlap along our roof slope (avoid floating intersections)
            if (hasStartConnection) ridgeStartY = Mathf.Max(baseHeight, ridgeStartY - slopeHeightAdjust);
            if (hasEndConnection) ridgeEndY = Mathf.Max(baseHeight, ridgeEndY - slopeHeightAdjust);

            Vector3 ridgeStart = new Vector3(startX, ridgeStartY, centerY);
            Vector3 ridgeEnd = new Vector3(endX, ridgeEndY, centerY);
            Vector3 ridgeStartBase = new Vector3((minX * cellSize) - overhang, roofHeight, centerY);
            Vector3 ridgeEndBase = new Vector3(((maxX + 1) * cellSize) + overhang, roofHeight, centerY);
            
            Vector3 eaveFront = new Vector3((minX * cellSize) - overhang, baseHeight, (minY * cellSize) - overhang);
            Vector3 eaveBack = new Vector3((minX * cellSize) - overhang, baseHeight, ((maxY + 1) * cellSize) + overhang);
            Vector3 eaveFrontEnd = new Vector3(((maxX + 1) * cellSize) + overhang, baseHeight, (minY * cellSize) - overhang);
            Vector3 eaveBackEnd = new Vector3(((maxX + 1) * cellSize) + overhang, baseHeight, ((maxY + 1) * cellSize) + overhang);
            
            // Main roof slopes
            if (hasStartConnection)
            {
                AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveFront, ridgeStartBase, ridgeEnd, eaveFrontEnd, Vector3.up);
                AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveBackEnd, ridgeEnd, ridgeStartBase, eaveBack, Vector3.up);
                
                // Hip connection triangles at start
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveFront, ridgeStartBase, ridgeStart, Vector3.up);
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, ridgeStart, ridgeStartBase, eaveBack, Vector3.up);
            }
            else
            {
                AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveFront, ridgeStart, ridgeEnd, eaveFrontEnd, Vector3.up);
                AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveBackEnd, ridgeEnd, ridgeStart, eaveBack, Vector3.up);
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveFront, ridgeStart, eaveBack, Vector3.left);
            }
            
            if (hasEndConnection)
            {
                // Hip connection triangles at end
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, ridgeEnd, ridgeEndBase, eaveFrontEnd, Vector3.up);
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveBackEnd, ridgeEndBase, ridgeEnd, Vector3.up);
            }
            else
            {
                AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveBackEnd, ridgeEnd, eaveFrontEnd, Vector3.right);
            }
        }
        else
        {
            float roofHeight = baseHeight + regionWidth * 0.5f * roofPitch;
            float startZ = (minY * cellSize) - overhang;
            float endZ = ((maxY + 1) * cellSize) + overhang;
            bool hasStartConnection = false;
            bool hasEndConnection = false;
            float startConnectionHeight = roofHeight;
            float endConnectionHeight = roofHeight;

            // Check for connection toward negative Y; require same integer height
            int myHn = Mathf.RoundToInt(baseHeight / floorHeight);
            if (minY - 1 >= 0)
            {
                for (int xx = minX; xx <= maxX; xx++)
                {
                    if (!footprint[xx, minY - 1]) continue;
                    if (heights[xx, minY - 1] != myHn) continue; // require same height
                    hasStartConnection = true;
                    float extensionDist = connectionOverlap * cellSize;
                    startZ -= extensionDist;

                    float neighborCenterX = (xx + 0.5f) * cellSize;
                    float distFromNeighborRidge = Mathf.Abs(centerX - neighborCenterX);
                    startConnectionHeight = baseHeight + (regionWidth * 0.5f - distFromNeighborRidge) * roofPitch;
                    break;
                }
            }

            // Check for connection toward positive Y; require same integer height
            if (maxY + 1 < footprint.GetLength(1))
            {
                for (int xx = minX; xx <= maxX; xx++)
                {
                    if (!footprint[xx, maxY + 1]) continue;
                    if (heights[xx, maxY + 1] != myHn) continue; // require same height
                    hasEndConnection = true;
                    float extensionDist = connectionOverlap * cellSize;
                    endZ += extensionDist;

                    float neighborCenterX = (xx + 0.5f) * cellSize;
                    float distFromNeighborRidge = Mathf.Abs(centerX - neighborCenterX);
                    endConnectionHeight = baseHeight + (regionWidth * 0.5f - distFromNeighborRidge) * roofPitch;
                    break;
                }
            }

            float overlapDist2 = connectionOverlap * cellSize;
            float slopeHeightAdjust2 = overlapDist2 * roofPitch;

            float ridgeStartY2 = hasStartConnection ? Mathf.Min(startConnectionHeight, roofHeight) : roofHeight;
            float ridgeEndY2 = hasEndConnection ? Mathf.Min(endConnectionHeight, roofHeight) : roofHeight;
            if (hasStartConnection) ridgeStartY2 = Mathf.Max(baseHeight, ridgeStartY2 - slopeHeightAdjust2);
            if (hasEndConnection) ridgeEndY2 = Mathf.Max(baseHeight, ridgeEndY2 - slopeHeightAdjust2);

            Vector3 ridgeStart = new Vector3(centerX, ridgeStartY2, startZ);
            Vector3 ridgeEnd = new Vector3(centerX, ridgeEndY2, endZ);
            Vector3 ridgeStartBase = new Vector3(centerX, roofHeight, (minY * cellSize) - overhang);
            Vector3 ridgeEndBase = new Vector3(centerX, roofHeight, ((maxY + 1) * cellSize) + overhang);
            
            Vector3 eaveLeft = new Vector3((minX * cellSize) - overhang, baseHeight, (minY * cellSize) - overhang);
            Vector3 eaveRight = new Vector3(((maxX + 1) * cellSize) + overhang, baseHeight, (minY * cellSize) - overhang);
            Vector3 eaveLeftEnd = new Vector3((minX * cellSize) - overhang, baseHeight, ((maxY + 1) * cellSize) + overhang);
            Vector3 eaveRightEnd = new Vector3(((maxX + 1) * cellSize) + overhang, baseHeight, ((maxY + 1) * cellSize) + overhang);
            
            // Main roof slopes
                if (hasStartConnection)
                {
                    AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveLeft, eaveLeftEnd, ridgeEnd, ridgeStartBase, Vector3.up);
                    AddQuad(roofVerts, roofTris, roofNorms, roofUVs, ridgeStartBase, ridgeEnd, eaveRightEnd, eaveRight, Vector3.up);
                    
                    // Hip connection triangles at start
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveLeft, ridgeStartBase, ridgeStart, Vector3.up);
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, ridgeStart, ridgeStartBase, eaveRight, Vector3.up);
                }
                else
                {
                    AddQuad(roofVerts, roofTris, roofNorms, roofUVs, eaveLeft, eaveLeftEnd, ridgeEnd, ridgeStart, Vector3.up);
                    AddQuad(roofVerts, roofTris, roofNorms, roofUVs, ridgeStart, ridgeEnd, eaveRightEnd, eaveRight, Vector3.up);
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveLeft, ridgeStart, eaveRight, Vector3.back);
                }
                
                if (hasEndConnection)
                {
                    // Hip connection triangles at end
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, ridgeEnd, ridgeEndBase, eaveLeftEnd, Vector3.up);
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveRightEnd, ridgeEndBase, ridgeEnd, Vector3.up);
                }
                else
                {
                    AddTriangle(roofVerts, roofTris, roofNorms, roofUVs, eaveRightEnd, ridgeEnd, eaveLeftEnd, Vector3.forward);
                }
        }
    }
    void AddGableRoof(bool[,] footprint, int[,] heights)
    {
        int width = footprint.GetLength(0);
        int depth = footprint.GetLength(1);
        
        float roofPitch = 0.5f;
        
        bool[,] processed = new bool[width, depth];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                if (!footprint[x, y] || processed[x, y]) continue;
                
                int minX = x, maxX = x, minY = y, maxY = y;
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                processed[x, y] = true;
                
                List<Vector2Int> region = new List<Vector2Int>();
                region.Add(new Vector2Int(x, y));
                
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    
                    Vector2Int[] neighbors = {
                        new Vector2Int(current.x + 1, current.y),
                        new Vector2Int(current.x - 1, current.y),
                        new Vector2Int(current.x, current.y + 1),
                        new Vector2Int(current.x, current.y - 1)
                    };
                    
                    foreach (Vector2Int n in neighbors)
                    {
                        if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < depth &&
                            footprint[n.x, n.y] && !processed[n.x, n.y])
                        {
                            processed[n.x, n.y] = true;
                            queue.Enqueue(n);
                            region.Add(n);
                            minX = Mathf.Min(minX, n.x);
                            maxX = Mathf.Max(maxX, n.x);
                            minY = Mathf.Min(minY, n.y);
                            maxY = Mathf.Max(maxY, n.y);
                        }
                    }
                }
                
                float maxHeight = 0;
                foreach (Vector2Int cell in region)
                {
                    maxHeight = Mathf.Max(maxHeight, heights[cell.x, cell.y] * floorHeight);
                }
                
                // Greedy rectangle carving by height (no BFS): process heights from tall to short and carve
                // maximal same-height rectangles directly from the region bounds.
                // Build a local occupancy map for region cells keyed by their height value.
                int regionW = maxX - minX + 1;
                int regionH = maxY - minY + 1;
                int[,] localHeight = new int[regionW, regionH];
                bool[,] used = new bool[regionW, regionH];
                int maxHval = 0;
                for (int lx = 0; lx < regionW; lx++)
                {
                    for (int ly = 0; ly < regionH; ly++)
                    {
                        Vector2Int c = new Vector2Int(minX + lx, minY + ly);
                        if (region.Contains(c))
                        {
                            localHeight[lx, ly] = heights[c.x, c.y];
                            maxHval = Mathf.Max(maxHval, localHeight[lx, ly]);
                        }
                        else
                        {
                            localHeight[lx, ly] = -1;
                            used[lx, ly] = true; // mark out-of-region as used
                        }
                    }
                }

                // iterate height values high-to-low
                for (int hval = maxHval; hval >= 0; hval--)
                {
                    for (int lx = 0; lx < regionW; lx++)
                    {
                        for (int ly = 0; ly < regionH; ly++)
                        {
                            if (used[lx, ly]) continue;
                            if (localHeight[lx, ly] != hval) continue;

                            // expand maximal rectangle from (lx,ly)
                            int rx = lx;
                            while (rx + 1 < regionW && !used[rx + 1, ly] && localHeight[rx + 1, ly] == hval) rx++;

                            int ry = ly;
                            bool canExpandY = true;
                            while (canExpandY)
                            {
                                for (int xx = lx; xx <= rx; xx++)
                                {
                                    if (ry + 1 >= regionH || used[xx, ry + 1] || localHeight[xx, ry + 1] != hval)
                                    {
                                        canExpandY = false;
                                        break;
                                    }
                                }
                                if (canExpandY) ry++;
                            }

                            // now [lx..rx] x [ly..ry] is a maximal rectangle for this start
                            int gminx = minX + lx;
                            int gmaxx = minX + rx;
                            int gminy = minY + ly;
                            int gmaxy = minY + ry;
                            float baseH = hval * floorHeight;
                            CreateSimpleGableForRegion(gminx, gmaxx, gminy, gmaxy, baseH, roofPitch,
                            (gmaxx - gminx + 1) * cellSize, (gmaxy - gminy + 1) * cellSize, footprint, roofConnectionOverlap, heights);

                            // mark used
                            for (int ux = lx; ux <= rx; ux++)
                                for (int uy = ly; uy <= ry; uy++)
                                    used[ux, uy] = true;
                        }
                    }
                }
            }
        }
    }

    void AddFlatRoof(bool[,] footprint, int[,] heights)
    {
        int width = footprint.GetLength(0);
        int depth = footprint.GetLength(1);
        
        float ridgeHeight = 0.6f;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                if (!footprint[x, y]) continue;
                
                float topH = heights[x, y] * floorHeight;
                Vector3 basePos = new Vector3(x * cellSize, topH, y * cellSize);
                
                Vector3 topBL = basePos;
                Vector3 topBR = basePos + new Vector3(cellSize, 0, 0);
                Vector3 topTR = basePos + new Vector3(cellSize, 0, cellSize);
                Vector3 topTL = basePos + new Vector3(0, 0, cellSize);
                AddQuad(wallVerts, wallTris, wallNorms, wallUVs, topBL, topBR, topTR, topTL, Vector3.up);
            }
        }
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                if (!footprint[x, y]) continue;
                
                float topH = heights[x, y] * floorHeight;
                Vector3 basePos = new Vector3(x * cellSize, topH, y * cellSize);
                
                Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
                
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dirs[d].x;
                    int ny = y + dirs[d].y;
                    bool neighborInside = nx >= 0 && ny >= 0 && nx < width && ny < depth && footprint[nx, ny];
                    
                    if (!neighborInside || (neighborInside && heights[nx, ny] < heights[x, y]))
                    {
                        Vector3 ridgeBL, ridgeBR, ridgeTR, ridgeTL;
                        Vector3 normal;
                        
                        if (d == 0)
                        {
                            ridgeBL = basePos + new Vector3(0, 0, cellSize);
                            ridgeBR = basePos + new Vector3(cellSize, 0, cellSize);
                            ridgeTR = basePos + new Vector3(cellSize, ridgeHeight, cellSize);
                            ridgeTL = basePos + new Vector3(0, ridgeHeight, cellSize);
                            normal = Vector3.forward;
                        }
                        else if (d == 1)
                        {
                            ridgeBL = basePos + new Vector3(cellSize, 0, cellSize);
                            ridgeBR = basePos + new Vector3(cellSize, 0, 0);
                            ridgeTR = basePos + new Vector3(cellSize, ridgeHeight, 0);
                            ridgeTL = basePos + new Vector3(cellSize, ridgeHeight, cellSize);
                            normal = Vector3.right;
                        }
                        else if (d == 2)
                        {
                            ridgeBL = basePos + new Vector3(cellSize, 0, 0);
                            ridgeBR = basePos + new Vector3(0, 0, 0);
                            ridgeTR = basePos + new Vector3(0, ridgeHeight, 0);
                            ridgeTL = basePos + new Vector3(cellSize, ridgeHeight, 0);
                            normal = Vector3.back;
                        }
                        else
                        {
                            ridgeBL = basePos + new Vector3(0, 0, 0);
                            ridgeBR = basePos + new Vector3(0, 0, cellSize);
                            ridgeTR = basePos + new Vector3(0, ridgeHeight, cellSize);
                            ridgeTL = basePos + new Vector3(0, ridgeHeight, 0);
                            normal = Vector3.left;
                        }
                        
                        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, ridgeBL, ridgeBR, ridgeTR, ridgeTL, normal);
                        AddQuad(wallVerts, wallTris, wallNorms, wallUVs, ridgeTL, ridgeTR, ridgeBR, ridgeBL, Vector3.up);
                    }
                }
            }
        }
    }

    void AddTriangle(List<Vector3> v, List<int> t, List<Vector3> n, List<Vector2> uv, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal)
    {
        int start = v.Count;
        v.Add(v1); v.Add(v2); v.Add(v3);
        
        n.Add(normal); n.Add(normal); n.Add(normal);
        
        uv.Add(new Vector2(0, 0));
        uv.Add(new Vector2(0.5f, 1));
        uv.Add(new Vector2(1, 0));
        
        Vector3 triNormal = Vector3.Cross(v2 - v1, v3 - v1);
        bool flip = Vector3.Dot(triNormal, normal) < 0f;
        
        if (!flip)
        {
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
        }
        else
        {
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
        }
    }
}