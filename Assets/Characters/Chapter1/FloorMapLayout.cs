using System;
using System.Collections.Generic;

public enum FloorRoomType
{
    Start,
    Combat,
    Shop,
    Golden,
    Cursed,
    Boss
}

[Serializable]
public sealed class FloorMapNode
{
    public int Id;
    public int Row;
    public int Lane;
    public FloorRoomType Type;
    public readonly List<int> Connections = new List<int>();

    public FloorMapNode(int id, int row, int lane, FloorRoomType type)
    {
        Id = id;
        Row = row;
        Lane = lane;
        Type = type;
    }

    public bool IsMainRoute => Lane == 0;
}

public static class FloorMapLayout
{
    public const int RoomCount = 20;
    public const int RowCount = 8;

    public static List<FloorMapNode> Generate(int seed)
    {
        var random = new Random(seed);
        var nodes = new List<FloorMapNode>(RoomCount);
        var grid = new FloorMapNode[RowCount, 3];

        for (int row = 0; row < RowCount; row++)
        {
            for (int laneIndex = 0; laneIndex < 3; laneIndex++)
            {
                int lane = laneIndex - 1;
                if ((row == 0 || row == RowCount - 1) && lane != 0)
                    continue;

                FloorRoomType type = row == 0
                    ? FloorRoomType.Start
                    : row == RowCount - 1 && lane == 0
                        ? FloorRoomType.Boss
                        : FloorRoomType.Combat;
                var node = new FloorMapNode(nodes.Count, row, lane, type);
                nodes.Add(node);
                grid[row, laneIndex] = node;
            }
        }

        for (int row = 0; row < RowCount - 1; row++)
        {
            Connect(grid[row, 1], grid[row + 1, 1]);
        }

        for (int row = 1; row < RowCount - 1; row++)
        {
            for (int laneIndex = 0; laneIndex < 3; laneIndex += 2)
            {
                FloorMapNode node = grid[row, laneIndex];
                FloorMapNode previous = grid[row - 1, laneIndex];
                FloorMapNode next = grid[row + 1, laneIndex];
                Connect(previous, node);
                Connect(node, next);
            }
        }

        Connect(grid[0, 1], grid[1, 0]);
        Connect(grid[0, 1], grid[1, 2]);
        Connect(grid[6, 0], grid[7, 1]);
        Connect(grid[6, 2], grid[7, 1]);
        Connect(grid[2, 1], grid[2, 0]);
        Connect(grid[5, 1], grid[5, 2]);

        AssignSpecialRooms(nodes, random);
        return nodes;
    }

    private static void AssignSpecialRooms(List<FloorMapNode> nodes, Random random)
    {
        var branchNodes = new List<FloorMapNode>();
        foreach (FloorMapNode node in nodes)
        {
            if (!node.IsMainRoute)
                branchNodes.Add(node);
        }

        AssignRandomRoomType(branchNodes, FloorRoomType.Shop, random);
        AssignRandomRoomType(branchNodes, FloorRoomType.Golden, random);
        AssignRandomRoomType(branchNodes, FloorRoomType.Cursed, random);
    }

    private static void AssignRandomRoomType(
        List<FloorMapNode> candidates,
        FloorRoomType type,
        Random random)
    {
        int index = random.Next(candidates.Count);
        candidates[index].Type = type;
        candidates.RemoveAt(index);
    }

    private static void Connect(FloorMapNode from, FloorMapNode to)
    {
        if (from == null || to == null)
            return;

        from.Connections.Add(to.Id);
        to.Connections.Add(from.Id);
    }
}
