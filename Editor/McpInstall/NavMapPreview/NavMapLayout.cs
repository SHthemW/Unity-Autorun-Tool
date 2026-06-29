using System;
using System.Collections.Generic;

public static class NavMapLayout
{
    public const int NodeWidth = 280;
    public const int NodeHeight = 100;
    public const int ColumnGap = 420;
    public const int IslandGap = 180;
    public const int RowGap = 170;
    public const int StartX = 80;
    public const int StartY = 120;

    public static void Apply(NavMapGraph graph)
    {
        List<List<NavMapNode>> groups = BuildGroups(graph);
        int currentX = StartX;
        for (int i = 0; i < groups.Count; i++)
        {
            currentX = LayoutGroup(graph, groups[i], i, currentX);
        }

        graph.Width = Math.Max(1200, currentX + 120);
        graph.Height = Math.Max(720, CalculateGraphHeight(graph));
    }

    private static int LayoutGroup(NavMapGraph graph, List<NavMapNode> group, int islandIndex, int baseX)
    {
        Dictionary<string, int> columns = AssignColumns(graph, group);
        var rowsByColumn = new Dictionary<int, int>();
        int maxColumn = 0;
        int maxRows = 1;

        foreach (NavMapNode node in group)
        {
            int column = columns[node.Id];
            int row = rowsByColumn.ContainsKey(column) ? rowsByColumn[column] : 0;
            rowsByColumn[column] = row + 1;
            maxColumn = Math.Max(maxColumn, column);
            maxRows = Math.Max(maxRows, row + 1);

            node.Column = column;
            node.Row = row;
            node.X = baseX + column * ColumnGap;
            node.Y = StartY + row * RowGap;
        }

        int islandWidth = maxColumn * ColumnGap + NodeWidth + 70;
        int islandHeight = 110 + maxRows * RowGap;
        graph.Islands.Add(new NavMapIsland
        {
            Name = "Island " + (islandIndex + 1),
            X = baseX - 35,
            Width = islandWidth,
            Height = islandHeight
        });

        return baseX + islandWidth + IslandGap;
    }

    private static Dictionary<string, int> AssignColumns(NavMapGraph graph, List<NavMapNode> group)
    {
        var groupIds = new HashSet<string>();
        foreach (NavMapNode node in group)
        {
            groupIds.Add(node.Id);
        }

        var incoming = new Dictionary<string, int>();
        foreach (NavMapNode node in group)
        {
            incoming[node.Id] = 0;
        }

        foreach (NavMapEdge edge in graph.Edges)
        {
            if (groupIds.Contains(edge.FromViewId) && groupIds.Contains(edge.ToViewId))
            {
                incoming[edge.ToViewId] = incoming[edge.ToViewId] + 1;
            }
        }

        var columns = new Dictionary<string, int>();
        var queue = new Queue<string>();
        foreach (NavMapNode node in group)
        {
            if (incoming[node.Id] == 0)
            {
                queue.Enqueue(node.Id);
                columns[node.Id] = 0;
            }
        }

        if (queue.Count == 0 && group.Count > 0)
        {
            queue.Enqueue(group[0].Id);
            columns[group[0].Id] = 0;
        }

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (NavMapEdge edge in graph.Edges)
            {
                if (edge.FromViewId != current || !groupIds.Contains(edge.ToViewId))
                {
                    continue;
                }

                if (!columns.ContainsKey(edge.ToViewId))
                {
                    columns[edge.ToViewId] = columns[current] + 1;
                    queue.Enqueue(edge.ToViewId);
                }
            }
        }

        foreach (NavMapNode node in group)
        {
            if (!columns.ContainsKey(node.Id))
            {
                columns[node.Id] = 0;
            }
        }

        return columns;
    }

    private static int CalculateGraphHeight(NavMapGraph graph)
    {
        int maxBottom = StartY + NodeHeight;
        foreach (NavMapNode node in graph.Views)
        {
            maxBottom = Math.Max(maxBottom, node.Y + NodeHeight);
        }

        return maxBottom + 160;
    }

    private static List<List<NavMapNode>> BuildGroups(NavMapGraph graph)
    {
        var result = new List<List<NavMapNode>>();
        var visited = new HashSet<string>();
        foreach (NavMapNode view in graph.Views)
        {
            if (visited.Contains(view.Id))
            {
                continue;
            }

            result.Add(CollectGroup(graph, view.Id, visited));
        }

        return result;
    }

    private static List<NavMapNode> CollectGroup(NavMapGraph graph, string startId, HashSet<string> visited)
    {
        var group = new List<NavMapNode>();
        var queue = new Queue<string>();
        queue.Enqueue(startId);
        visited.Add(startId);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            group.Add(graph.NodeById[current]);
            foreach (string next in Neighbors(graph, current))
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return group;
    }

    private static IEnumerable<string> Neighbors(NavMapGraph graph, string viewId)
    {
        foreach (NavMapEdge edge in graph.Edges)
        {
            if (edge.FromViewId == viewId && graph.NodeById.ContainsKey(edge.ToViewId))
            {
                yield return edge.ToViewId;
            }

            if (edge.ToViewId == viewId && graph.NodeById.ContainsKey(edge.FromViewId))
            {
                yield return edge.FromViewId;
            }
        }
    }
}
