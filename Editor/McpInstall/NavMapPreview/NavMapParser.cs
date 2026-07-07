using UnityEngine;

public static class NavMapParser
{
    public static NavMapGraph Parse(string json)
    {
        var graph = new NavMapGraph();
        NavMapDocument document = JsonUtility.FromJson<NavMapDocument>(json);
        ReadViews(graph, document);
        ReadEdges(graph, document);
        NavMapLayout.Apply(graph);
        return graph;
    }

    private static void ReadViews(NavMapGraph graph, NavMapDocument document)
    {
        if (document == null || document.views == null)
        {
            return;
        }

        foreach (NavMapView view in document.views)
        {
            if (view == null || string.IsNullOrEmpty(view.id))
            {
                continue;
            }

            var node = new NavMapNode
            {
                Id = view.id,
                Name = view.name,
                PrefabPath = view.prefabPath
            };
            graph.Views.Add(node);
            graph.NodeById[view.id] = node;
        }
    }

    private static void ReadEdges(NavMapGraph graph, NavMapDocument document)
    {
        if (document == null || document.transitions == null)
        {
            return;
        }

        foreach (NavMapTransition transition in document.transitions)
        {
            if (transition == null)
            {
                continue;
            }

            var edge = new NavMapEdge
            {
                FromViewId = transition.fromViewId,
                ToViewId = transition.toViewId,
                ControlId = transition.controlId,
                Kind = transition.kind
            };
            edge.Label = BuildEdgeLabel(edge);
            graph.Edges.Add(edge);
        }
    }

    private static string BuildEdgeLabel(NavMapEdge edge)
    {
        if (!string.IsNullOrEmpty(edge.ControlId))
        {
            return edge.ControlId;
        }

        if (!string.IsNullOrEmpty(edge.Kind))
        {
            return edge.Kind;
        }

        return "transition";
    }
}
