using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building_Generator : MonoBehaviour
{   
    public int gridSize = 10;
    public int seed = 0;
    int[,] heightGrid;
    int minHeight = 3;
    int maxHeight = 8;
    bool[,] footprint;
    Building_Mesh_Gen.RoofType currentRoofType;
    Building_Mesh_Gen meshGenInstance;

    void Start()
    {
        Random.InitState(seed);
        footprint = GenerateFootprint(Random.Range(0, 8));
        heightGrid = GenerateHeightGrid(footprint);
        
        // Find a random cell on the perimeter for the door
        (Vector2Int doorCell, int doorDir) = FindDoorLocation(footprint);
        
    Building_Mesh_Gen meshGen = gameObject.AddComponent<Building_Mesh_Gen>();
    meshGenInstance = meshGen;
        
        // Randomly choose window type
        if (Random.value > 0.5f)
        {
            meshGen.windowType = Building_Mesh_Gen.WindowType.BigWindows;
        }
        else
        {
            meshGen.windowType = Building_Mesh_Gen.WindowType.TallWindows;
        }

        // Randomly choose door type
        if (Random.value > 0.5f)
        {
            meshGen.doorType = Building_Mesh_Gen.DoorType.SingleDoor;
        }
        else
        {
            meshGen.doorType = Building_Mesh_Gen.DoorType.DoubleDoor;
        }
        
        if (Random.value > 0.5f)
        {
            meshGen.roofType = Building_Mesh_Gen.RoofType.Gable;
        }
        else
        {
            meshGen.roofType = Building_Mesh_Gen.RoofType.Flat;
        }
        
        currentRoofType = meshGen.roofType;
        
        Mesh buildingMesh = meshGen.GenerateBuildingMesh(footprint, heightGrid, doorCell, doorDir);
        
        // Add MeshFilter if it doesn't exist
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.mesh = buildingMesh;
        
        // Add MeshRenderer if it doesn't exist (so you can see it)
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        // Set up materials array for the 4 submeshes (roof, walls, windows, doors)
        Material[] materials = new Material[4];
        
        // Load random wall material from wall_m folder (4 options)
        int randomWall = Random.Range(1, 5); // 1, 2, 3, or 4
        Material wallMat = Resources.Load<Material>("material/wall_m/wall_" + randomWall);
        if (wallMat != null)
        {
            materials[0] = wallMat;
        }
        else
        {
            Debug.LogError("Could not find wall material at: material/wall_m/wall_" + randomWall);
            materials[0] = new Material(Shader.Find("Standard"));
        }
        
        // Load window material and set emission tiling based on window type
        Material windowMat = Resources.Load<Material>("material/window_m/window");
        if (windowMat != null)
        {
            // Create instance so we don't modify the original
            materials[1] = new Material(windowMat);
            
            // Set emission tiling based on window type
            if (meshGen.windowType == Building_Mesh_Gen.WindowType.BigWindows)
            {
                materials[1].SetTextureScale("_MainTex", new Vector2(0.7f, 0.7f));
            }
            else // TallWindows
            {
                materials[1].SetTextureScale("_MainTex", new Vector2(1.2f, 0.7f));
            }
        }
        else
        {
            Debug.LogError("Could not find window material at: material/window_m/window");
            materials[1] = new Material(Shader.Find("Standard"));
        }
        
        // Load door material and set emission tiling based on door type
        Material doorMat = Resources.Load<Material>("material/door_m/door");
        if (doorMat != null)
        {
            // Create instance so we don't modify the original
            materials[2] = new Material(doorMat);

            // Set emission tiling based on door type
            if (meshGen.doorType == Building_Mesh_Gen.DoorType.SingleDoor)
            {
                materials[2].SetTextureScale("_MainTex", new Vector2(2.6f, 1f));
            }
            else // DoubleDoor
            {
                materials[2].SetTextureScale("_MainTex", new Vector2(2.2f, 1f));
            }
        }
        else
        {
            Debug.LogError("Could not find door material at: material/door_m/door");
            materials[2] = new Material(Shader.Find("Standard"));
        }

        // Load roof material
        Material roofMat = Resources.Load<Material>("material/roof_m/roof");
        if (roofMat != null)
        {
            materials[3] = roofMat;
        }
        else
        {
            Debug.LogError("Could not find roof material at: material/roof_m/roof");
            materials[3] = new Material(Shader.Find("Standard"));
        }
        
        // If roof is flat, prefer using wall_3 as the roof/trim material so it matches walls' 3rd variant
        if (meshGen.roofType == Building_Mesh_Gen.RoofType.Flat)
        {
            Material wall3 = Resources.Load<Material>("material/wall_m/wall_3");
            if (wall3 != null)
            {
                materials[3] = wall3;
            }
            else
            {
                // keep previously loaded roofMat (or Standard) if wall_3 is missing
                Debug.LogWarning("wall_3 material not found; keeping default roof material");
            }
        }
        
        meshRenderer.materials = materials;
        
    // Place prefabs
    PlacePrefabs(doorCell, doorDir);
    }

    void PlacePrefabs(Vector2Int doorCell, int doorDir)
    {
        float cellSize = 3f;
        float floorHeight = 3f;
        if (meshGenInstance != null)
        {
            cellSize = meshGenInstance.cellSize;
            floorHeight = meshGenInstance.floorHeight;
        }
        
        // Place antenna on flat roof
        if (currentRoofType == Building_Mesh_Gen.RoofType.Flat)
        {
            GameObject antennaPrefab = Resources.Load<GameObject>("Prefab/antenna");
            if (antennaPrefab != null)
            {
                // Find a random cell that has the building footprint
                List<Vector2Int> roofCells = new List<Vector2Int>();
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        if (footprint[x, y])
                        {
                            roofCells.Add(new Vector2Int(x, y));
                        }
                    }
                }
                
                if (roofCells.Count > 0)
                {
                    Vector2Int randomCell = roofCells[Random.Range(0, roofCells.Count)];
                    float height = heightGrid[randomCell.x, randomCell.y] * floorHeight;
                    Vector3 antennaPos = new Vector3(
                        (randomCell.x + 0.5f) * cellSize,
                        height,
                        (randomCell.y + 0.5f) * cellSize
                    );
                    
                    GameObject antenna = Instantiate(antennaPrefab);
                    antenna.transform.SetParent(transform, false);
                    antenna.transform.localPosition = antennaPos + Vector3.up * 1.2f;
                    antenna.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                Debug.LogError("Could not find antenna prefab at: Prefab/antenna");
            }
        }
        
        // Place Spider-Man on a random wall
        GameObject spidermanPrefab = Resources.Load<GameObject>("Prefab/SpiderMan");
        if (spidermanPrefab != null)
        {
            // Find all exterior walls (excluding door)
            List<(Vector2Int cell, int direction, int floor)> wallPositions = new List<(Vector2Int, int, int)>();
            
            Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
            
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (!footprint[x, y]) continue;
                    
                    int floors = heightGrid[x, y];
                    
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dirs[d].x;
                        int ny = y + dirs[d].y;
                        bool isExterior = nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize || !footprint[nx, ny];
                        
                        // Skip if this is the door location
                        if (x == doorCell.x && y == doorCell.y && d == doorDir)
                            continue;
                        
                        if (isExterior)
                        {
                            // Add all floors for this wall
                            for (int floor = 0; floor < floors; floor++)
                            {
                                wallPositions.Add((new Vector2Int(x, y), d, floor));
                            }
                        }
                    }
                }
            }
            
            if (wallPositions.Count > 0)
            {
                var randomWall = wallPositions[Random.Range(0, wallPositions.Count)];
                Vector2Int cell = randomWall.cell;
                int direction = randomWall.direction;
                int floor = randomWall.floor;
                
                // Calculate position and rotation
                Vector3 basePos = new Vector3(cell.x * cellSize, floor * floorHeight, cell.y * cellSize);
                Vector3 spidermanPos = basePos;
                Quaternion spidermanRot = Quaternion.identity;
                
                // Position based on wall direction
                if (direction == 0)
                {
                    spidermanPos = new Vector3((cell.x + 0.5f) * cellSize, floor * floorHeight + floorHeight * 0.5f, (cell.y + 1) * cellSize + 0.01f);
                    spidermanRot = Quaternion.Euler(0, 180, 0);
                }
                else if (direction == 1)
                {
                    spidermanPos = new Vector3((cell.x + 1) * cellSize + 0.01f, floor * floorHeight + floorHeight * 0.5f, (cell.y + 0.5f) * cellSize);
                    spidermanRot = Quaternion.Euler(0, 270, 0);
                }
                else if (direction == 2)
                {
                    spidermanPos = new Vector3((cell.x + 0.5f) * cellSize, floor * floorHeight + floorHeight * 0.5f, cell.y * cellSize - 0.01f);
                    spidermanRot = Quaternion.Euler(0, 0, 0);
                }
                else
                {
                    spidermanPos = new Vector3(cell.x * cellSize - 0.01f, floor * floorHeight + floorHeight * 0.5f, (cell.y + 0.5f) * cellSize);
                    spidermanRot = Quaternion.Euler(0, 90, 0);
                }
                
                GameObject spiderman = Instantiate(spidermanPrefab);
                spiderman.transform.SetParent(transform, false);
                spiderman.transform.localPosition = spidermanPos;
                spiderman.transform.localRotation = spidermanRot * Quaternion.Euler(0f, -90f, 0f);
            }
        }
        else
        {
            Debug.LogError("Could not find Spider-Man prefab at: Prefab/SpiderMan");
        }
    }

    (Vector2Int, int) FindDoorLocation(bool[,] footprint)
    {
        List<(Vector2Int cell, int direction)> possibleDoors = new List<(Vector2Int, int)>();
        
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
        
        // Find all cells that have at least one exterior wall
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (!footprint[x, y]) continue;
                
                // Check each direction
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dirs[d].x;
                    int ny = y + dirs[d].y;
                    
                    // If neighbor is outside the grid or is not part of footprint, this is an exterior wall
                    bool isExterior = nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize || !footprint[nx, ny];
                    
                    if (isExterior)
                    {
                        possibleDoors.Add((new Vector2Int(x, y), d));
                    }
                }
            }
        }
        
        // Pick a random door location from possible locations
        if (possibleDoors.Count > 0)
        {
            int randomIndex = Random.Range(0, possibleDoors.Count);
            return possibleDoors[randomIndex];
        }
        
        // Fallback (shouldn't happen with valid footprints)
        return (new Vector2Int(0, 0), 0);
    }

    bool[,] GenerateFootprint(int type)
    {
        bool[,] grid = new bool[gridSize, gridSize];

        switch (type)
        {
            case 0: return MakeRectangle(grid, 6, 6);
            case 1: return MakeRectangle(grid, 8, 4);
            case 2: return MakeLShape(grid);
            case 3: return MakeUShape(grid);
            case 4: return MakeTShape(grid);
            case 5: return MakeCShape(grid);
            case 6: return MakePlusShape(grid);
            case 7: return MakeSmallLShape(grid);
            default: return MakeRectangle(grid, 6, 6);
        }
    }

    int[,] GenerateHeightGrid(bool[,] footprint)
    {
        int[,] heights = new int[gridSize, gridSize];

        // Base height for entire building
        int baseHeight = Random.Range(minHeight, maxHeight + 1);

        // Number of smooth regions (1–2)
        int regions = Random.Range(1, 2);

        // Initialize everything to base height
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                if (footprint[x, y])
                    heights[x, y] = baseHeight;

        // Add 1–2 areas with slightly different heights
        for (int r = 0; r < regions; r++)
        {
            int regionCenterX = Random.Range(2, gridSize - 2);
            int regionCenterY = Random.Range(2, gridSize - 2);
            int delta = Random.Range(-2, 4);
            float radius = Random.Range(2f, 5f);

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (!footprint[x, y]) continue;

                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(regionCenterX, regionCenterY));
                    if (dist < radius)
                    {
                        int newHeight = Mathf.Clamp(baseHeight + delta, minHeight, maxHeight);
                        heights[x, y] = newHeight;
                    }
                }
            }
        }

        return heights;
    }

    bool[,] MakeRectangle(bool[,] g, int w, int h)
    {
        int x0 = (gridSize - w) / 2;
        int y0 = (gridSize - h) / 2;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                g[x, y] = true;
        return g;
    }

    bool[,] MakeLShape(bool[,] g)
    {
        for (int y = 2; y < 8; y++)
            for (int x = 2; x < 4; x++)
                g[x, y] = true;

        for (int y = 6; y < 8; y++)
            for (int x = 2; x < 8; x++)
                g[x, y] = true;

        return g;
    }

    bool[,] MakeSmallLShape(bool[,] g)
    {
        for (int y = 4; y < 9; y++)
            for (int x = 5; x < 7; x++)
                g[x, y] = true;

        for (int y = 7; y < 9; y++)
            for (int x = 5; x < 9; x++)
                g[x, y] = true;

        return g;
    }

    bool[,] MakeUShape(bool[,] g)
    {
        for (int y = 2; y < 8; y++)
        {
            g[2, y] = true;
            g[7, y] = true;
        }
        for (int x = 2; x < 8; x++)
            g[x, 2] = true;
        return g;
    }

    bool[,] MakeTShape(bool[,] g)
    {
        for (int x = 2; x < 8; x++)
            for (int y = 2; y < 4; y++)
                g[x, y] = true;

        for (int y = 2; y < 8; y++)
            for (int x = 4; x < 6; x++)
                g[x, y] = true;

        return g;
    }

    bool[,] MakeCShape(bool[,] g)
    {
        for (int y = 2; y < 8; y++)
            g[2, y] = true;

        for (int x = 2; x < 8; x++)
        {
            g[x, 2] = true;
            g[x, 7] = true;
        }

        return g;
    }

    bool[,] MakePlusShape(bool[,] g)
    {
        for (int y = 3; y < 7; y++)
            for (int x = 4; x < 6; x++)
                g[x, y] = true;

        for (int x = 3; x < 7; x++)
            for (int y = 4; y < 6; y++)
                g[x, y] = true;

        return g;
    }

    void Update()
    {
        
    }
}