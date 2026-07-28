using System;
using System.Text;

public static class NavMapHtmlRenderer
{
    public static string Render(NavMapGraph graph, string sourcePath)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, sourcePath);
        builder.AppendLine("<div class=\"wrap\"><svg viewBox=\"0 0 " + graph.Width + " " + graph.Height + "\" width=\"" + graph.Width + "\" height=\"" + graph.Height + "\">");
        builder.AppendLine("<defs><marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#6b7280\"/></marker></defs>");
        RenderIslands(builder, graph);
        RenderEdges(builder, graph);
        RenderNodes(builder, graph);
        builder.AppendLine("</svg></div>");
        builder.AppendLine("<div class=\"legend\">Nodes are views. Arrows are button-click transitions. Generate Gen/ui-nav-map.json to replace the example graph.</div>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, string sourcePath)
    {
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>Unity AutoRun Nav Map</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#202124;background:#f7f8fa}");
        builder.AppendLine(".meta{margin-bottom:16px;color:#5f6368}.wrap{overflow:auto;background:white;border:1px solid #dfe3e8;border-radius:8px;padding:16px;max-height:78vh}");
        builder.AppendLine("svg{display:block;max-width:none}.node{fill:#eef6ff;stroke:#2f6f9f;stroke-width:2}.label{font-size:18px;font-weight:600;fill:#1f2933}.path{font-size:14px;fill:#536471}");
        builder.AppendLine(".edge{stroke:#6b7280;stroke-width:2;fill:none;marker-end:url(#arrow)}.edgeText{font-size:14px;fill:#374151}");
        builder.AppendLine(".island{fill:#fbfcff;stroke:#c8d1dc;stroke-dasharray:6 4}.legend{font-size:12px;color:#5f6368;margin-top:12px}");
        builder.AppendLine("</style></head><body>");
        builder.AppendLine("<h1>Unity AutoRun Nav Map</h1>");
        builder.AppendLine("<div class=\"meta\">Source: " + Html(sourcePath) + "</div>");
    }

    private static void RenderIslands(StringBuilder builder, NavMapGraph graph)
    {
        foreach (NavMapIsland island in graph.Islands)
        {
            builder.AppendLine("<rect class=\"island\" x=\"" + island.X + "\" y=\"40\" width=\"" + island.Width + "\" height=\"" + island.Height + "\" rx=\"8\"/>");
            builder.AppendLine("<text class=\"path\" x=\"" + (island.X + 12) + "\" y=\"62\">" + Html(island.Name) + "</text>");
        }
    }

    private static void RenderEdges(StringBuilder builder, NavMapGraph graph)
    {
        foreach (NavMapEdge edge in graph.Edges)
        {
            NavMapNode from;
            NavMapNode to;
            if (!graph.NodeById.TryGetValue(edge.FromViewId, out from) || !graph.NodeById.TryGetValue(edge.ToViewId, out to))
            {
                continue;
            }

            EdgePoints points = GetEdgePoints(from, to);
            int x1 = points.X1;
            int y1 = points.Y1;
            int x2 = points.X2;
            int y2 = points.Y2;
            int midX = (x1 + x2) / 2;
            int midY = (y1 + y2) / 2;
            builder.AppendLine("<path class=\"edge\" d=\"" + points.Path + "\"/>");
            builder.AppendLine("<text class=\"edgeText\" x=\"" + midX + "\" y=\"" + (midY - 6) + "\" text-anchor=\"middle\">" + Html(edge.Label) + "</text>");
        }
    }

    private static EdgePoints GetEdgePoints(NavMapNode from, NavMapNode to)
    {
        if (from.Column < to.Column)
        {
            int x1 = from.X + NavMapLayout.NodeWidth;
            int y1 = from.Y + NavMapLayout.NodeHeight / 2;
            int x2 = to.X;
            int y2 = to.Y + NavMapLayout.NodeHeight / 2;
            return new EdgePoints(x1, y1, x2, y2, "M" + x1 + " " + y1 + " C" + (x1 + 90) + " " + y1 + "," + (x2 - 90) + " " + y2 + "," + x2 + " " + y2);
        }

        if (from.Column > to.Column)
        {
            int x1 = from.X;
            int y1 = from.Y + NavMapLayout.NodeHeight / 2;
            int x2 = to.X + NavMapLayout.NodeWidth;
            int y2 = to.Y + NavMapLayout.NodeHeight / 2;
            return new EdgePoints(x1, y1, x2, y2, "M" + x1 + " " + y1 + " C" + (x1 - 90) + " " + y1 + "," + (x2 + 90) + " " + y2 + "," + x2 + " " + y2);
        }

        int sameColumnOffset = from.Row <= to.Row ? NavMapLayout.NodeWidth - 55 : 55;
        int x = from.X + sameColumnOffset;
        int y1Bottom = from.Y + (from.Row <= to.Row ? NavMapLayout.NodeHeight : 0);
        int y2Top = to.Y + (from.Row <= to.Row ? 0 : NavMapLayout.NodeHeight);
        return new EdgePoints(x, y1Bottom, x, y2Top, "M" + x + " " + y1Bottom + " L" + x + " " + y2Top);
    }

    private static void RenderNodes(StringBuilder builder, NavMapGraph graph)
    {
        foreach (NavMapNode node in graph.Views)
        {
            builder.AppendLine("<rect class=\"node\" x=\"" + node.X + "\" y=\"" + node.Y + "\" width=\"" + NavMapLayout.NodeWidth + "\" height=\"" + NavMapLayout.NodeHeight + "\" rx=\"8\"/>");
            builder.AppendLine("<text class=\"label\" x=\"" + (node.X + 16) + "\" y=\"" + (node.Y + 36) + "\">" + Html(ShortText(node.Name, 28)) + "</text>");
            builder.AppendLine("<text class=\"path\" x=\"" + (node.X + 16) + "\" y=\"" + (node.Y + 62) + "\">" + Html(ShortText(node.Id, 34)) + "</text>");
            builder.AppendLine("<text class=\"path\" x=\"" + (node.X + 16) + "\" y=\"" + (node.Y + 82) + "\">" + Html(ShortText(node.PrefabPath, 38)) + "</text>");
        }
    }

    private static string ShortText(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength - 3) + "...";
    }

    private static string Html(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private sealed class EdgePoints
    {
        public readonly int X1;
        public readonly int Y1;
        public readonly int X2;
        public readonly int Y2;
        public readonly string Path;

        public EdgePoints(int x1, int y1, int x2, int y2, string path)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Path = path;
        }
    }
}
