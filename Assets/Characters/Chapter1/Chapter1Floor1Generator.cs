using System;
using System.Collections.Generic;
using UnityEngine;

public class Chapter1Floor1Generator : MonoBehaviour
{
    [SerializeField] private int seed = -1;
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private bool generateOnAwake = true;

    private const float LocationWidth = 190f;
    private const float LocationDepth = 180f;
    private const float LaneSpacing = 190f;
    private const float RowSpacing = 180f;

    private readonly List<Material> _materials = new List<Material>();
    private readonly List<FloorMapRoom> _rooms = new List<FloorMapRoom>();

    public int CurrentSeed { get; private set; }
    public IReadOnlyList<FloorMapNode> Nodes { get; private set; }
    public IReadOnlyList<FloorMapRoom> Rooms => _rooms;
    public Transform PlayerSpawn { get; private set; }

    private void Awake()
    {
        if (generateOnAwake)
            Generate();
    }

    [ContextMenu("Generate Floor 1")]
    public void Generate()
    {
        ClearGeneratedContent();

        CurrentSeed = randomizeSeedEachRun || seed < 0
            ? UnityEngine.Random.Range(1, int.MaxValue)
            : seed;

        List<FloorMapNode> nodes = FloorMapLayout.Generate(CurrentSeed);
        Nodes = nodes;
        var random = new System.Random(CurrentSeed);

        Material floor = CreateMaterial("Megastructure_Floor", MeralgorePalette.Floor);
        Material walls = CreateMaterial("Megastructure_Walls", MeralgorePalette.Walls);
        Material structure = CreateMaterial("Megastructure_Structure", MeralgorePalette.Structure);
        Material route = CreateMaterial("Megastructure_MainRoute", MeralgorePalette.MainRoute);
        Material props = CreateMaterial("Megastructure_Props", MeralgorePalette.Props);
        Material signal = CreateMaterial("Megastructure_Signal", MeralgorePalette.Signal);
        Material boss = CreateMaterial("Megastructure_Boss", MeralgorePalette.Boss);

        int maxRow = 0;
        foreach (FloorMapNode node in nodes)
            maxRow = Mathf.Max(maxRow, node.Row);

        float floorLength = maxRow * RowSpacing + LocationDepth + 30f;
        CreateCube(transform, "Continuous_Combat_Floor",
            new Vector3(0f, -0.5f, maxRow * RowSpacing * 0.5f),
            new Vector3(LaneSpacing * 2f + LocationWidth + 30f, 1f, floorLength), floor);

        CreateMegastructureBackdrop(transform, maxRow, walls, structure, random);

        var roomObjects = new Transform[nodes.Count];
        var roomComponents = new FloorMapRoom[nodes.Count];

        foreach (FloorMapNode node in nodes)
        {
            Vector3 center = GetPosition(node);
            GameObject roomObject = new GameObject($"Space_{node.Id + 1:00}_{node.Type}");
            roomObject.transform.SetParent(transform, false);
            roomObject.transform.localPosition = center;
            roomObjects[node.Id] = roomObject.transform;

            var room = roomObject.AddComponent<FloorMapRoom>();
            room.Initialize(node, this);
            roomComponents[node.Id] = room;
            _rooms.Add(room);

            Material routeMaterial = GetRoomMaterial(node.Type, route, signal, boss);
            CreateRoomGeometry(roomObject.transform, node, structure, routeMaterial, props, random);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            FloorMapNode node = nodes[i];
            foreach (int connectedId in node.Connections)
            {
                if (connectedId <= node.Id)
                    continue;

                FloorMapNode connectedNode = nodes[connectedId];
                Vector3 from = GetPosition(node);
                Vector3 to = GetPosition(connectedNode);
                CreateLink(from, to, node, connectedNode, roomComponents, signal);
            }
        }

        GameObject spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(roomObjects[0], false);
        spawn.transform.localPosition = new Vector3(0f, 0f, -48f);
        PlayerSpawn = spawn.transform;

        FloorRunController runController = GetComponent<FloorRunController>();
        if (runController == null)
            runController = gameObject.AddComponent<FloorRunController>();
        runController.Initialize(this);
    }

    private void ClearGeneratedContent()
    {
        if (Application.isPlaying)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            foreach (Material material in _materials)
                Destroy(material);
        }
        else
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            foreach (Material material in _materials)
                DestroyImmediate(material);
        }

        _materials.Clear();
        _rooms.Clear();
        PlayerSpawn = null;
    }

    private static Vector3 GetPosition(FloorMapNode node)
    {
        return new Vector3(node.Lane * LaneSpacing, 0f, node.Row * RowSpacing);
    }

    private void CreateRoomGeometry(
        Transform room,
        FloorMapNode node,
        Material structure,
        Material route,
        Material props,
        System.Random random)
    {
        // Encounter territories are logical trigger volumes over one continuous physical floor.
        CreateCube(room, "Territory_Route_Marker", new Vector3(0f, 0.08f, 0f),
            new Vector3(10f, 0.12f, LocationDepth - 12f), route);
        CreateOpenTerritoryArchitecture(room, node, structure, props, random);
        CreateTerritoryIdentity(room, node, route, signal: props);
    }

    private void CreateOpenTerritoryArchitecture(
        Transform room, FloorMapNode node, Material structure, Material props, System.Random random)
    {
        // Keep the central combat lane readable and mostly unobstructed.
        for (int i = 0; i < 3; i++)
        {
            float x = (float)(random.NextDouble() * 112f - 56f);
            float z = (float)(random.NextDouble() * 92f - 46f);
            if (Mathf.Abs(x) < 17f && Mathf.Abs(z) < 20f) continue;
            float width = random.Next(5, 13);
            float height = random.Next(3, 10);
            CreateCube(room, $"Combat_Cover_{i:00}", new Vector3(x, height * 0.5f, z),
                new Vector3(width, height, random.Next(4, 10)), props);
        }

        // Two elevated side routes, with long ramps rather than tiny room balconies.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 53f;
            CreateCube(room, side < 0 ? "West_Transit_Deck" : "East_Transit_Deck",
                new Vector3(x, 12f, 0f), new Vector3(28f, 1.2f, 190f), structure);
            CreateRampBetween(room, side < 0 ? "West_Access_Ramp" : "East_Access_Ramp",
                new Vector3(side * 13f, 0.4f, -72f),
                new Vector3(side * 53f, 12f, -18f), 18f, structure);
        }

        // Each territory gets a different large-scale architectural signature.
        switch (node.Id % 4)
        {
            case 0:
                CreateCube(room, "Variant_Industrial_Gantry", new Vector3(0f, 25f, 30f),
                    new Vector3(118f, 3f, 9f), structure);
                CreateCube(room, "Variant_Gantry_Hanger", new Vector3(42f, 17f, 30f),
                    new Vector3(8f, 22f, 8f), props);
                break;
            case 1:
                CreateCube(room, "Variant_Central_Transit", new Vector3(-12f, 21f, 0f),
                    new Vector3(12f, 1.2f, 112f), structure);
                CreateCube(room, "Variant_Suspended_Mass", new Vector3(35f, 42f, 18f),
                    new Vector3(34f, 8f, 30f), props);
                break;
            case 2:
                CreateCube(room, "Variant_Cross_Transit", new Vector3(0f, 16f, 26f),
                    new Vector3(112f, 1.2f, 12f), structure);
                CreateCube(room, "Variant_Transfer_Platform", new Vector3(-35f, 29f, -28f),
                    new Vector3(34f, 2f, 28f), props);
                break;
            default:
                CreateCube(room, "Variant_Overpass_A", new Vector3(-28f, 24f, 12f),
                    new Vector3(12f, 2f, 100f), structure);
                CreateCube(room, "Variant_Overpass_B", new Vector3(28f, 34f, -20f),
                    new Vector3(12f, 2f, 76f), props);
                break;
        }

        // Massive supports rise well above the playable deck.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int depth = -1; depth <= 1; depth += 2)
            {
                float x = side * 69f;
                float z = depth * 57f;
                CreateCube(room, $"Megastructure_Pylon_{side}_{depth}",
                    new Vector3(x, 34f, z), new Vector3(7f, 68f, 7f), structure);
            }
        }

        // Suspended machinery overhead. Deliberately discontinuous: never cap the whole arena.
        CreateCube(room, "Overhead_Machinery", new Vector3(-38f, 52f, 12f),
            new Vector3(52f, 6f, 20f), structure);

        if (node.Type == FloorRoomType.Boss)
        {
            CreateCube(room, "Boss_Engagement_Ring", new Vector3(0f, 0.18f, 0f),
                new Vector3(54f, 0.16f, 54f), props);
            CreateCube(room, "Boss_Reactor_Spine", new Vector3(0f, 23f, 47f),
                new Vector3(34f, 46f, 9f), structure);
        }
    }

    private static GameObject CreateRampBetween(
        Transform parent, string objectName, Vector3 start, Vector3 end, float width, Material material)
    {
        Vector3 delta = end - start;
        GameObject ramp = CreateCube(parent, objectName, (start + end) * 0.5f,
            new Vector3(width, 1.5f, delta.magnitude), material);
        ramp.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        return ramp;
    }

    private void CreateMegastructureBackdrop(
        Transform parent, int maxRow, Material walls, Material structure, System.Random random)
    {
        float runLength = maxRow * RowSpacing;

        // A continuous skyline begins far beyond the playable perimeter and follows the whole floor.
        for (int i = 0; i < 22; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float progress = i / 21f;
            float z = -240f + (runLength + 480f) * progress + random.Next(-70, 71);
            float x = side * random.Next(470, 850);
            float height = random.Next(260, 620);
            float width = random.Next(28, 76);
            GameObject tower = CreateCube(parent, $"Distant_Megastructure_Tower_{i:00}",
                new Vector3(x, height * 0.5f, z),
                new Vector3(width, height, width * random.Next(2, 5)),
                i % 4 == 0 ? walls : structure);
            tower.transform.localRotation = Quaternion.Euler(0f, random.Next(0, 180), 0f);
            DisableCollider(tower);
        }

        for (int i = 0; i < 8; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float z = -180f + (runLength + 360f) * (i / 7f);
            float x = side * random.Next(760, 1050);
            float height = random.Next(340, 720);
            GameObject shaft = CreateCube(parent, $"Distant_Giant_Shaft_{i:00}",
                new Vector3(x, height * 0.5f, z),
                new Vector3(random.Next(28, 54), height, random.Next(28, 54)), walls);
            DisableCollider(shaft);
        }

        for (int i = 0; i < 7; i++)
        {
            float progress = i / 6f;
            float z = -120f + (runLength + 240f) * progress;
            float y = random.Next(240, 520);
            GameObject bridge = CreateCube(parent, $"Distant_Transit_Span_{i:00}",
                new Vector3(0f, y, z),
                new Vector3(random.Next(1100, 1650), random.Next(10, 22), random.Next(24, 44)), structure);
            bridge.transform.localRotation = Quaternion.Euler(0f, random.Next(-5, 6), 0f);
            DisableCollider(bridge);
        }

        for (int i = 0; i < 5; i++)
        {
            float z = -200f + runLength * (i / 4f);
            GameObject truss = CreateCube(parent, $"Distant_Upper_Truss_{i:00}",
                new Vector3(0f, 520f + random.Next(-50, 51), z),
                new Vector3(1450f, random.Next(12, 24), random.Next(18, 34)), walls);
            DisableCollider(truss);
        }
    }

    private static void DisableCollider(GameObject target)
    {
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
            targetCollider.enabled = false;
    }

    private void CreateTerritoryIdentity(Transform room, FloorMapNode node, Material route, Material signal)
    {
        // A simple monumental marker makes the district legible without enclosing it.
        CreateCube(room, "District_Signature_Pillar",
            new Vector3(0f, 16f, 62f), new Vector3(8f, 32f, 5f),
            node.Type == FloorRoomType.Boss ? signal : route);
    }

    private void CreateLink(
        Vector3 from,
        Vector3 to,
        FloorMapNode fromNode,
        FloorMapNode toNode,
        FloorMapRoom[] rooms,
        Material signalMaterial)
    {
        Vector3 delta = to - from;
        Vector3 direction = delta.normalized;
        Vector3 midpoint = (from + to) * 0.5f;

        // The global floor already connects every district. Only a broad combat barrier
        // remains for encounter locking; there are no narrow walkways or guardrails.
        GameObject link = new GameObject($"Link_{fromNode.Id + 1:00}_{toNode.Id + 1:00}");
        link.transform.SetParent(transform, false);
        link.transform.localPosition = new Vector3(midpoint.x, 0.05f, midpoint.z);
        link.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

        GameObject gateObject = CreateCube(link.transform, "Combat_Gate",
            new Vector3(0f, 11f, 0f), new Vector3(220f, 22f, 1.5f), signalMaterial);
        gateObject.SetActive(false);
        FloorMapLink gate = gateObject.AddComponent<FloorMapLink>();
        rooms[fromNode.Id].AddLink(gate);
        rooms[toNode.Id].AddLink(gate);
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError($"No supported shader was found for generated material '{materialName}'.");
            return null;
        }

        var material = new Material(shader) { name = materialName };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        _materials.Add(material);
        return material;
    }

    private static Material GetRoomMaterial(
        FloorRoomType type,
        Material route,
        Material signal,
        Material boss)
    {
        switch (type)
        {
            case FloorRoomType.Shop:
            case FloorRoomType.Golden:
                return signal;
            case FloorRoomType.Cursed:
                return boss;
            case FloorRoomType.Boss:
                return boss;
            default:
                return route;
        }
    }

    private static GameObject CreateCube(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 scale,
        Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = scale;

        int groundLayer = LayerMask.NameToLayer("Ground");
        cube.layer = groundLayer >= 0 ? groundLayer : 0;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return cube;
    }

}
