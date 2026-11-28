using System;
using System.Collections.Generic;
using UnityEngine;

public class Chapter1Floor1Generator : MonoBehaviour
{
    [SerializeField] private int seed = -1;
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private bool generateOnAwake = true;

    private const float LocationWidth = 70f;
    private const float LocationDepth = 52f;
    private const float LaneSpacing = 84f;
    private const float RowSpacing = 72f;
    private const float DoorWidth = 14f;

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
        spawn.transform.localPosition = new Vector3(0f, 0f, -14f);
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
        CreateCube(room, "Ground_Slab", new Vector3(0f, -0.5f, 0f),
            new Vector3(LocationWidth, 1f, LocationDepth), floor);
        CreateCube(room, "Route_Inlay", new Vector3(0f, 0.08f, 0f),
            new Vector3(5f, 0.15f, LocationDepth - 6f), route);

        List<Opening> openings = GetOpenings(node, nodes);
        CreateWalls(room, openings, walls);
        CreateSupportColumns(room, structure);
        CreateUpperLevel(room, structure);
        CreateRoomDetails(room, node, props, random);
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
        float connectorLength = Mathf.Max(14f, distance - 84f);

        GameObject link = new GameObject($"Link_{fromNode.Id + 1:00}_{toNode.Id + 1:00}");
        link.transform.SetParent(transform, false);
        link.transform.localPosition = new Vector3(midpoint.x, 0.05f, midpoint.z);
        link.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

        CreateCube(link.transform, "Walkway", Vector3.zero,
            new Vector3(DoorWidth, 0.25f, connectorLength), routeMaterial);

        GameObject gateObject = CreateCube(link.transform, "Combat_Gate",
            new Vector3(0f, 4f, 0f), new Vector3(DoorWidth, 8f, 0.8f), signalMaterial);
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
