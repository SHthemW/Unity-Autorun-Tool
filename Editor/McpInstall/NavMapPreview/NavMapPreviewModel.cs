using System;
using System.Collections.Generic;

[Serializable]
internal sealed class NavMapPreviewModel
{
    public string sourcePath;
    public NavMapPreviewView[] views;
    public NavMapPreviewEdge[] edges;
    public int transitionCount;
    public int validTransitionCount;
    public int invalidTransitionCount;
    public int connectedViewCount;
    public int unconnectedViewCount;

    public static NavMapPreviewModel Create(NavMapGraph graph, string sourcePath)
    {
        if (graph == null)
        {
            throw new ArgumentNullException("graph");
        }

        var viewIds = new HashSet<string>(StringComparer.Ordinal);
        var uniqueViews = new List<NavMapNode>();
        foreach (NavMapNode view in graph.Views)
        {
            if (view == null || string.IsNullOrEmpty(view.Id) || !viewIds.Add(view.Id))
            {
                continue;
            }

            uniqueViews.Add(view);
        }

        var connectedViewIds = new HashSet<string>(StringComparer.Ordinal);
        var edgeGroups = BuildEdgeGroups(
            graph,
            viewIds,
            connectedViewIds,
            out int validTransitionCount,
            out int invalidTransitionCount);
        var previewViews = new NavMapPreviewView[uniqueViews.Count];
        int connectedViewCount = 0;
        for (int i = 0; i < uniqueViews.Count; i++)
        {
            NavMapNode view = uniqueViews[i];
            bool connected = connectedViewIds.Contains(view.Id);
            if (connected)
            {
                connectedViewCount++;
            }

            previewViews[i] = new NavMapPreviewView
            {
                domId = "nav-node-" + i,
                id = view.Id,
                name = view.Name,
                prefabPath = view.PrefabPath,
                connected = connected
            };
        }

        return new NavMapPreviewModel
        {
            sourcePath = sourcePath,
            views = previewViews,
            edges = edgeGroups,
            transitionCount = graph.Edges.Count,
            validTransitionCount = validTransitionCount,
            invalidTransitionCount = invalidTransitionCount,
            connectedViewCount = connectedViewCount,
            unconnectedViewCount = previewViews.Length - connectedViewCount
        };
    }

    private static NavMapPreviewEdge[] BuildEdgeGroups(
        NavMapGraph graph,
        HashSet<string> viewIds,
        HashSet<string> connectedViewIds,
        out int validTransitionCount,
        out int invalidTransitionCount)
    {
        var groupsBySource =
            new Dictionary<string, Dictionary<string, EdgeGroupBuilder>>(StringComparer.Ordinal);
        var orderedGroups = new List<EdgeGroupBuilder>();
        validTransitionCount = 0;
        invalidTransitionCount = 0;

        foreach (NavMapEdge transition in graph.Edges)
        {
            if (!IsValidTransition(transition, viewIds))
            {
                invalidTransitionCount++;
                continue;
            }

            validTransitionCount++;
            connectedViewIds.Add(transition.FromViewId);
            connectedViewIds.Add(transition.ToViewId);

            Dictionary<string, EdgeGroupBuilder> groupsByTarget;
            if (!groupsBySource.TryGetValue(transition.FromViewId, out groupsByTarget))
            {
                groupsByTarget =
                    new Dictionary<string, EdgeGroupBuilder>(StringComparer.Ordinal);
                groupsBySource.Add(transition.FromViewId, groupsByTarget);
            }

            EdgeGroupBuilder group;
            if (!groupsByTarget.TryGetValue(transition.ToViewId, out group))
            {
                group = new EdgeGroupBuilder(
                    "nav-edge-" + orderedGroups.Count,
                    transition.FromViewId,
                    transition.ToViewId);
                groupsByTarget.Add(transition.ToViewId, group);
                orderedGroups.Add(group);
            }

            group.Add(transition);
        }

        var result = new NavMapPreviewEdge[orderedGroups.Count];
        for (int i = 0; i < orderedGroups.Count; i++)
        {
            result[i] = orderedGroups[i].Build();
        }

        return result;
    }

    private static bool IsValidTransition(NavMapEdge transition, HashSet<string> viewIds)
    {
        return transition != null
            && !string.IsNullOrEmpty(transition.FromViewId)
            && !string.IsNullOrEmpty(transition.ToViewId)
            && viewIds.Contains(transition.FromViewId)
            && viewIds.Contains(transition.ToViewId);
    }

    private sealed class EdgeGroupBuilder
    {
        private const int EdgeLabelMaxLength = 32;
        private readonly string _domId;
        private readonly string _fromViewId;
        private readonly string _toViewId;
        private readonly List<NavMapPreviewTransition> _transitions =
            new List<NavMapPreviewTransition>();

        public EdgeGroupBuilder(string domId, string fromViewId, string toViewId)
        {
            _domId = domId;
            _fromViewId = fromViewId;
            _toViewId = toViewId;
        }

        public void Add(NavMapEdge transition)
        {
            _transitions.Add(new NavMapPreviewTransition
            {
                controlId = transition.ControlId,
                kind = transition.Kind,
                label = transition.Label
            });
        }

        public NavMapPreviewEdge Build()
        {
            return new NavMapPreviewEdge
            {
                domId = _domId,
                fromViewId = _fromViewId,
                toViewId = _toViewId,
                label = BuildLabel(),
                transitionCount = _transitions.Count,
                transitions = _transitions.ToArray()
            };
        }

        private string BuildLabel()
        {
            if (_transitions.Count > 1)
            {
                return _transitions.Count + " transitions";
            }

            string label = _transitions.Count == 0 ? null : _transitions[0].label;
            if (string.IsNullOrEmpty(label))
            {
                return "transition";
            }

            return label.Length <= EdgeLabelMaxLength
                ? label
                : label.Substring(0, EdgeLabelMaxLength - 3) + "...";
        }
    }
}

[Serializable]
internal sealed class NavMapPreviewView
{
    public string domId;
    public string id;
    public string name;
    public string prefabPath;
    public bool connected;
}

[Serializable]
internal sealed class NavMapPreviewEdge
{
    public string domId;
    public string fromViewId;
    public string toViewId;
    public string label;
    public int transitionCount;
    public NavMapPreviewTransition[] transitions;
}

[Serializable]
internal sealed class NavMapPreviewTransition
{
    public string controlId;
    public string kind;
    public string label;
}
