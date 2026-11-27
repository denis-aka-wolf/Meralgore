using UnityEngine;

public class Chapter1Floor1Generator : MonoBehaviour
{
    [SerializeField] private int seed = 1471;
    [SerializeField] private bool generateOnAwake = true;

    private const float LocationWidth = 70f;
    private const float LocationDepth = 52f;

    private readonly System.Collections.Generic.List<Material> _materials =
        new System.Collections.Generic.List<Material>();

    private void Awake()
    {
        if (generateOnAwake)
            Generate();
    }

    [ContextMenu("Generate Floor 1")]
    public void Generate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        _materials.Clear();
        Random.InitState(seed);

        Material concrete = CreateMaterial("Megastructure_Concrete", new Color(0.19f, 0.20f, 0.20f));
        Material darkConcrete = CreateMaterial("Megastructure_DarkConcrete", new Color(0.11f, 0.12f, 0.12f));
        Material mainRoute = CreateMaterial("MainRoute", new Color(0.30f, 0.48f, 0.47f));
        Material branchRoute = CreateMaterial("BranchRoute", new Color(0.35f, 0.31f, 0.24f));
        Material hazard = CreateMaterial("Industrial_Hazard", new Color(0.70f, 0.45f, 0.08f));
        Material boss = CreateMaterial("Boss_Route", new Color(0.52f, 0.12f, 0.10f));
        Material upper = CreateMaterial("Upper_Structure", new Color(0.27f, 0.29f, 0.28f));

        Location[] locations =
        {
            new Location("01_Start_Plaza", 0f, 0f, true, false, false),
            new Location("02_Foundry_Avenue", 0f, 72f, true, false, false),
            new Location("03_Crushing_Hall", 0f, 144f, true, false, false),
            new Location("04_Central_Conveyor", 0f, 216f, true, false, false),
            new Location("05_Rail_Depot", 0f, 288f, true, false, false),
            new Location("06_Power_Rotunda", 0f, 360f, true, false, false),
            new Location("07_Lower_Furnace", 0f, 432f, true, false, false),
            new Location("08_Structural_Junction", 0f, 504f, true, false, false),
            new Location("09_Boss_Approach", 0f, 576f, true, false, false),
            new Location("10_Boss_Chamber", 0f, 648f, false, true, false),
            new Location("11_West_Loading_Bay", -78f, 72f, false, false, true),
            new Location("12_West_Pump_Hall", -78f, 144f, false, false, true),
            new Location("13_West_Cooling_Yard", -78f, 216f, false, false, true),
            new Location("14_West_Storage_Gallery", -78f, 288f, false, false, true),
            new Location("15_East_Smelter_Bay", 78f, 72f, false, false, true),
            new Location("16_East_Service_Yard", 78f, 144f, false, false, true),
            new Location("17_East_Turbine_Hall", 78f, 216f, false, false, true),
            new Location("18_East_Overpass", 78f, 288f, false, false, true),
            new Location("19_Secret_Riser", -78f, 360f, false, false, true),
            new Location("20_Elevated_Reserve", 78f, 360f, false, false, true)
        };

        foreach (Location location in locations)
        {
            Material routeMaterial = location.Boss
                ? boss
                : location.MainRoute
                    ? mainRoute
                    : branchRoute;

            CreateMegastructureLocation(location.Name, location.X, location.Z, routeMaterial,
                concrete, darkConcrete, upper, location.Name == "01_Start_Plaza", location.Boss);
        }

        CreateMainRoute(mainRoute, hazard);
        CreateIndustrialBranches(branchRoute, hazard);
        CreateBossWarning(boss);
    }

    private void CreateMegastructureLocation(
        string name,
        float x,
        float z,
        Material routeMaterial,
        Material concrete,
        Material darkConcrete,
        Material upper,
        bool isStart,
        bool isBoss)
    {
        GameObject location = new GameObject(name);
        location.transform.SetParent(transform, false);
        location.transform.position = new Vector3(x, 0f, z);

        CreateCube(location.transform, "Ground_Slab", Vector3.zero,
            new Vector3(LocationWidth, 0.8f, LocationDepth), darkConcrete);

        CreateIndustrialGround(location.transform, routeMaterial, hazard: name.Contains("Furnace"));

        float halfWidth = LocationWidth * 0.5f;
        float halfDepth = LocationDepth * 0.5f;
        CreateCube(location.transform, "West_Pier", new Vector3(-halfWidth, 4f, 0f),
            new Vector3(3f, 8f, LocationDepth), concrete);
        CreateCube(location.transform, "East_Pier", new Vector3(halfWidth, 4f, 0f),
            new Vector3(3f, 8f, LocationDepth), concrete);

        if (!isStart)
        {
            CreateGatedWall(location.transform, "South_Border", new Vector3(0f, 4f, -halfDepth),
                concrete, routeMaterial);
        }
        if (!isBoss)
        {
            CreateGatedWall(location.transform, "North_Border", new Vector3(0f, 4f, halfDepth),
                concrete, routeMaterial);
        }

        CreateSupportColumns(location.transform, concrete);
        CreateUpperLevel(location.transform, upper);

        if (isStart)
            CreateSpawnMarker(location.transform, routeMaterial);
        if (isBoss)
            CreateBossArenaFeatures(location.transform, routeMaterial);
    }

    private void CreateIndustrialGround(Transform location, Material routeMaterial, bool hazard)
    {
        CreateCube(location, "Route_Inlay", new Vector3(0f, 0.48f, 0f),
            new Vector3(7f, 0.14f, LocationDepth - 8f), routeMaterial);

        float[] xPositions = { -23f, 23f };
        foreach (float x in xPositions)
        {
            CreateCube(location, $"Machine_Base_{x}", new Vector3(x, 1.2f, -8f),
                new Vector3(10f, 2.4f, 12f), routeMaterial);

            for (int i = -2; i <= 2; i++)
            {
                CreateCube(location, $"Load_Block_{x}_{i}",
                    new Vector3(x, 4.2f + Random.Range(0f, 1.5f), i * 7f),
                    new Vector3(8f, 7f, 4f), routeMaterial);
            }
        }

        if (hazard)
        {
            for (int i = -2; i <= 2; i++)
            {
                CreateCube(location, $"Hazard_Mark_{i}",
                    new Vector3(i * 6f, 0.58f, 15f),
                    new Vector3(4f, 0.12f, 1.2f), routeMaterial);
            }
        }
    }

    private void CreateGatedWall(Transform location, string name, Vector3 position,
        Material concrete, Material routeMaterial)
    {
        CreateCube(location, name + "_Left", new Vector3(position.x - 22f, position.y, position.z),
            new Vector3(26f, 8f, 3f), concrete);
        CreateCube(location, name + "_Right", new Vector3(position.x + 22f, position.y, position.z),
            new Vector3(26f, 8f, 3f), concrete);
        CreateCube(location, name + "_Gate", new Vector3(0f, 1.1f, position.z),
            new Vector3(12f, 2.2f, 3.4f), routeMaterial);
    }

    private void CreateSupportColumns(Transform location, Material concrete)
    {
        for (int x = -24; x <= 24; x += 24)
        {
            for (int z = -16; z <= 16; z += 16)
            {
                CreateCube(location, $"Support_{x}_{z}", new Vector3(x, 5f, z),
                    new Vector3(2f, 10f, 2f), concrete);
            }
        }
    }

    private void CreateUpperLevel(Transform location, Material upper)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 27f;
            CreateCube(location, side < 0 ? "West_Upper_Platform" : "East_Upper_Platform",
                new Vector3(x, 8.5f, 0f),
                new Vector3(12f, 0.7f, LocationDepth - 6f), upper);

            CreateCube(location, side < 0 ? "West_Upper_Rail" : "East_Upper_Rail",
                new Vector3(side * 21f, 10f, 0f),
                new Vector3(0.35f, 2.2f, LocationDepth - 6f), upper);
        }

        CreateUpperStairs(location, upper);
    }

    private void CreateUpperStairs(Transform location, Material upper)
    {
        for (int i = 0; i < 10; i++)
        {
            CreateCube(location, $"Upper_Stairs_{i + 1}",
                new Vector3(-17f, 1f + i * 0.75f, -18f + i * 1.3f),
                new Vector3(5f, 0.7f, 1.8f), upper);
        }
    }

    private void CreateSpawnMarker(Transform location, Material routeMaterial)
    {
        CreateCube(location, "PlayerSpawn", new Vector3(0f, 0.65f, -17f),
            new Vector3(4f, 0.3f, 4f), routeMaterial);
    }

    private void CreateBossArenaFeatures(Transform location, Material boss)
    {
        CreateCube(location, "Boss_Pedestal", new Vector3(0f, 1.5f, 4f),
            new Vector3(14f, 3f, 14f), boss);

        for (int i = -2; i <= 2; i++)
        {
            CreateCube(location, $"Boss_Monolith_{i}",
                new Vector3(i * 13f, 5f, 17f),
                new Vector3(3f, 10f, 3f), boss);
        }
    }

    private void CreateMainRoute(Material mainRoute, Material hazard)
    {
        for (float z = 36f; z < 612f; z += 72f)
        {
            CreateCube(transform, $"Main_Forward_Link_{z}", new Vector3(0f, 0.75f, z),
                new Vector3(12f, 0.3f, 24f), mainRoute);
        }

        for (float z = 72f; z <= 576f; z += 72f)
        {
            CreateCube(transform, $"Forward_Arrow_{z}_L", new Vector3(-5f, 0.68f, z + 8f),
                new Vector3(1f, 0.12f, 8f), hazard);
            CreateCube(transform, $"Forward_Arrow_{z}_R", new Vector3(5f, 0.68f, z + 8f),
                new Vector3(1f, 0.12f, 8f), hazard);
        }
    }

    private void CreateIndustrialBranches(Material branchRoute, Material hazard)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 39f;
            for (float z = 72f; z <= 360f; z += 72f)
            {
                CreateCube(transform, $"Branch_Link_{side}_{z}", new Vector3(x, 0.7f, z),
                    new Vector3(20f, 0.3f, 12f), branchRoute);
            }
        }
    }

    private void CreateBossWarning(Material boss)
    {
        for (int i = -3; i <= 3; i++)
        {
            CreateCube(transform, $"Boss_Warning_Barrier_{i}",
                new Vector3(i * 9f, 4f, 615f),
                new Vector3(5f, 8f, 1f), boss);
        }
    }

    private Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        _materials.Add(material);
        return material;
    }

    private static void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = scale;
        cube.layer = 3;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private readonly struct Location
    {
        public readonly string Name;
        public readonly float X;
        public readonly float Z;
        public readonly bool MainRoute;
        public readonly bool Boss;
        public readonly bool Branch;
        public readonly bool Start;

        public Location(string name, float x, float z, bool mainRoute, bool boss, bool branch)
        {
            Name = name;
            X = x;
            Z = z;
            MainRoute = mainRoute;
            Boss = boss;
            Branch = branch;
            Start = name == "01_Start_Plaza";
        }
    }
}
