using System;
using System.Collections.Generic;
using UnityEngine;

public class Chapter1Floor1Generator : MonoBehaviour
{
    [SerializeField] private int seed = -1;
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private bool generateOnAwake = true;

    private const float LocationWidth = 150f;
    private const float LocationDepth = 130f;
    private const float LaneSpacing = 190f;
    private const float RowSpacing = 180f;
    private const float DoorWidth = 28f;

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
            CreateRoomGeometry(roomObject.transform, node, nodes, floor, walls, structure, routeMaterial, props, random);
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
                CreateLink(from, to, node, connectedNode, roomComponents, route, signal);
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
        List<FloorMapNode> nodes,
        Material floor,
        Material walls,
        Material structure,
        Material route,
        Material props,
        System.Random random)
    {
        // A territory is an open combat district, not an enclosed room.
        CreateCube(room, "Territory_Ground", new Vector3(0f, -0.5f, 0f),
            new Vector3(LocationWidth, 1f, LocationDepth), floor);
        CreateCube(room, "Route_Inlay", new Vector3(0f, 0.08f, 0f),
            new Vector3(7f, 0.12f, LocationDepth - 8f), route);

        CreateOpenTerritoryArchitecture(room, node, structure, props, random);
        CreateMegastructureBackdrop(room, node, walls, structure, random);
        CreateTerritoryIdentity(room, node, route, signal: props);
    }

    private void CreateOpenTerritoryArchitecture(
        Transform room, FloorMapNode node, Material structure, Material props, System.Random random)
    {
        // Keep the central combat lane readable and mostly unobstructed.
        for (int i = 0; i < 10; i++)
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
                new Vector3(x, 12f, 0f), new Vector3(24f, 1.2f, 104f), structure);
            CreateCube(room, side < 0 ? "West_Deck_Spine" : "East_Deck_Spine",
                new Vector3(side * 67f, 14f, 0f), new Vector3(2f, 4f, 108f), props);

            GameObject ramp = CreateCube(room, side < 0 ? "West_Access_Ramp" : "East_Access_Ramp",
                new Vector3(side * 34f, 5.7f, -37f), new Vector3(13f, 1f, 42f), structure);
            ramp.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
        }

        // Broken cross-bridges create alternate movement lines and sightline changes.
        CreateCube(room, "Cross_Transit_Bridge_A", new Vector3(0f, 18f, 34f),
            new Vector3(76f, 1.1f, 10f), structure);
        CreateCube(room, "Cross_Transit_Bridge_B", new Vector3(0f, 9f, -53f),
            new Vector3(52f, 1.1f, 8f), structure);

        // Massive supports rise well above the playable deck.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int depth = -1; depth <= 1; depth += 2)
            {
                float x = side * 69f;
                float z = depth * 57f;
                CreateCube(room, $"Megastructure_Pylon_{side}_{depth}",
                    new Vector3(x, 34f, z), new Vector3(7f, 68f, 7f), structure);
                CreateCube(room, $"Pylon_Collar_{side}_{depth}",
                    new Vector3(x, 19f, z), new Vector3(13f, 3f, 13f), props);
            }
        }

        // Suspended machinery overhead. Deliberately discontinuous: never cap the whole arena.
        CreateCube(room, "Overhead_Machinery_A", new Vector3(-34f, 48f, 5f),
            new Vector3(38f, 5f, 16f), structure);
        CreateCube(room, "Overhead_Machinery_B", new Vector3(42f, 57f, -28f),
            new Vector3(30f, 4f, 22f), props);

        if (node.Type == FloorRoomType.Boss)
        {
            CreateCube(room, "Boss_Engagement_Ring", new Vector3(0f, 0.18f, 0f),
                new Vector3(54f, 0.16f, 54f), props);
            CreateCube(room, "Boss_Reactor_Spine", new Vector3(0f, 23f, 47f),
                new Vector3(34f, 46f, 9f), structure);
        }
    }

    private void CreateMegastructureBackdrop(
        Transform room, FloorMapNode node, Material walls, Material structure, System.Random random)
    {
        // Local skyline silhouettes give every territory its own distant industrial profile.
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f + random.Next(-9, 10);
            float radius = random.Next(105, 175);
            float radians = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(radians) * radius;
            float z = Mathf.Sin(radians) * radius;
            float height = random.Next(75, 190);
            float width = random.Next(12, 30);
            GameObject tower = CreateCube(room, $"Distant_Structure_{i:00}",
                new Vector3(x, height * 0.5f, z),
                new Vector3(width, height, width * random.Next(1, 3)), i % 3 == 0 ? walls : structure);
            tower.transform.localRotation = Quaternion.Euler(0f, random.Next(0, 180), 0f);
            Collider towerCollider = tower.GetComponent<Collider>();
            if (towerCollider != null) towerCollider.enabled = false;
        }

        // Huge suspended spans visible above and beyond the playable battlefield.
        GameObject span = CreateCube(room, "Distant_Horizontal_Span",
            new Vector3(random.Next(-35, 36), 105f, 145f),
            new Vector3(190f, 12f, 18f), walls);
        GameObject shaft = CreateCube(room, "Distant_Vertical_Shaft",
            new Vector3(random.Next(-35, 36), 90f, -145f),
            new Vector3(24f, 180f, 24f), structure);
        Collider spanCollider = span.GetComponent<Collider>();
        Collider shaftCollider = shaft.GetComponent<Collider>();
        if (spanCollider != null) spanCollider.enabled = false;
        if (shaftCollider != null) shaftCollider.enabled = false;
    }

    private void CreateTerritoryIdentity(Transform room, FloorMapNode node, Material route, Material signal)
    {
        // A simple monumental marker makes the district legible without enclosing it.
        CreateCube(room, "District_Signature_Pillar",
            new Vector3(0f, 16f, 62f), new Vector3(8f, 32f, 5f),
            node.Type == FloorRoomType.Boss ? signal : route);
    }

    private static List<Opening> GetOpenings(FloorMapNode node, List<FloorMapNode> nodes)
    {
        var openings = new List<Opening>();
        foreach (int connectedId in node.Connections)
        {
            FloorMapNode connected = nodes[connectedId];
            float dx = (connected.Lane - node.Lane) * LaneSpacing;
            float dz = (connected.Row - node.Row) * RowSpacing;
            float xTime = Mathf.Abs(dx) < 0.001f ? float.PositiveInfinity : 35f / Mathf.Abs(dx);
            float zTime = Mathf.Abs(dz) < 0.001f ? float.PositiveInfinity : 26f / Mathf.Abs(dz);

            if (xTime < zTime)
            {
                openings.Add(new Opening(
                    dx < 0f ? OpeningSide.West : OpeningSide.East,
                    Mathf.Clamp(dz * xTime, -LocationDepth * 0.5f + DoorWidth, LocationDepth * 0.5f - DoorWidth)));
            }
            else
            {
                openings.Add(new Opening(
                    dz < 0f ? OpeningSide.South : OpeningSide.North,
                    Mathf.Clamp(dx * zTime, -LocationWidth * 0.5f + DoorWidth, LocationWidth * 0.5f - DoorWidth)));
            }
        }

        return openings;
    }

    private void CreateWalls(Transform room, List<Opening> openings, Material material)
    {
        CreateWallSegments(room, OpeningSide.North, LocationWidth, openings, material);
        CreateWallSegments(room, OpeningSide.South, LocationWidth, openings, material);
        CreateWallSegments(room, OpeningSide.East, LocationDepth, openings, material);
        CreateWallSegments(room, OpeningSide.West, LocationDepth, openings, material);
    }

    private void CreateWallSegments(
        Transform room,
        OpeningSide side,
        float wallLength,
        List<Opening> openings,
        Material material)
    {
        var centers = new List<float>();
        foreach (Opening opening in openings)
        {
            if (opening.Side == side)
                centers.Add(opening.Offset);
        }
        centers.Sort();

        float cursor = -wallLength * 0.5f;
        float wallPosition = side == OpeningSide.North
            ? LocationDepth * 0.5f
            : side == OpeningSide.South
                ? -LocationDepth * 0.5f
                : side == OpeningSide.East
                    ? LocationWidth * 0.5f
                    : -LocationWidth * 0.5f;

        foreach (float center in centers)
        {
            float openingStart = Mathf.Max(cursor, center - DoorWidth * 0.5f);
            CreateWallSegment(room, side, cursor, openingStart, wallPosition, material);
            cursor = Mathf.Max(cursor, center + DoorWidth * 0.5f);
        }

        CreateWallSegment(room, side, cursor, wallLength * 0.5f, wallPosition, material);
    }

    private void CreateWallSegment(
        Transform room,
        OpeningSide side,
        float start,
        float end,
        float wallPosition,
        Material material)
    {
        float length = end - start;
        if (length <= 0.1f)
            return;

        Vector3 position;
        Vector3 scale;
        if (side == OpeningSide.North || side == OpeningSide.South)
        {
            position = new Vector3((start + end) * 0.5f, 4f, wallPosition);
            scale = new Vector3(length, 8f, 3f);
        }
        else
        {
            position = new Vector3(wallPosition, 4f, (start + end) * 0.5f);
            scale = new Vector3(3f, 8f, length);
        }

        CreateCube(room, $"Boundary_{side}_{start:F0}", position, scale, material);
    }

    private void CreateSupportColumns(Transform room, Material material)
    {
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                CreateCube(room, $"Support_{x}_{z}", new Vector3(x * 29f, 4f, z * 20f),
                    new Vector3(2.5f, 8f, 2.5f), material);
            }
        }
    }

    private void CreateUpperLevel(Transform room, Material material)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 27f;
            CreateCube(room, side < 0 ? "West_Upper_Platform" : "East_Upper_Platform",
                new Vector3(x, 8.35f, 0f),
                new Vector3(12f, 0.7f, LocationDepth - 6f), material);

            CreateCube(room, side < 0 ? "West_Upper_Rail" : "East_Upper_Rail",
                new Vector3(side * 20.5f, 10f, 0f),
                new Vector3(0.35f, 2.6f, LocationDepth - 6f), material);
        }

        var ramp = CreateCube(room, "Upper_Ramp", new Vector3(-23f, 4.1f, -5f),
            new Vector3(6f, 0.6f, 28f), material);
        ramp.transform.localRotation = Quaternion.Euler(-17f, 0f, 0f);
    }

    private void CreateRoomDetails(Transform room, FloorMapNode node, Material material, System.Random random)
    {
        if (node.Type == FloorRoomType.Boss)
        {
            CreateCube(room, "Boss_Platform", new Vector3(0f, 0.25f, 5f),
                new Vector3(18f, 0.5f, 18f), CreateMaterial("Boss_Platform", MeralgorePalette.Boss));
        }

        for (int i = 0; i < 3; i++)
        {
            float x = random.Next(0, 2) == 0 ? -1f : 1f;
            float z = random.Next(-16, 17);
            float size = random.Next(3, 7);
            CreateCube(room, $"Industrial_Block_{i}", new Vector3(x * random.Next(16, 25), size, z),
                new Vector3(size, size * 2f, size), material);
        }
    }

    private void CreateLink(
        Vector3 from,
        Vector3 to,
        FloorMapNode fromNode,
        FloorMapNode toNode,
        FloorMapRoom[] rooms,
        Material routeMaterial,
        Material signalMaterial)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        Vector3 direction = delta.normalized;
        Vector3 midpoint = (from + to) * 0.5f;
        float edgeDistance = Mathf.Min(
            Mathf.Abs(direction.x) < 0.001f ? float.PositiveInfinity : (LocationWidth * 0.5f) / Mathf.Abs(direction.x),
            Mathf.Abs(direction.z) < 0.001f ? float.PositiveInfinity : (LocationDepth * 0.5f) / Mathf.Abs(direction.z));
        float connectorLength = Mathf.Max(8f, distance - edgeDistance * 2f);

        GameObject link = new GameObject($"Link_{fromNode.Id + 1:00}_{toNode.Id + 1:00}");
        link.transform.SetParent(transform, false);
        link.transform.localPosition = new Vector3(midpoint.x, 0.05f, midpoint.z);
        link.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

        CreateCube(link.transform, "Walkway", Vector3.zero,
            new Vector3(DoorWidth, 0.6f, connectorLength), routeMaterial);

        GameObject gateObject = CreateCube(link.transform, "Combat_Gate",
            new Vector3(0f, 5f, 0f), new Vector3(DoorWidth, 10f, 1.2f), signalMaterial);
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

    private enum OpeningSide
    {
        North,
        South,
        East,
        West
    }

    private readonly struct Opening
    {
        public readonly OpeningSide Side;
        public readonly float Offset;

        public Opening(OpeningSide side, float offset)
        {
            Side = side;
            Offset = offset;
        }
    }
}
