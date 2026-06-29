using System.Collections.Generic;
using System;

public sealed class NavMapGraph
{
    public readonly List<NavMapNode> Views = new List<NavMapNode>();
    public readonly List<NavMapEdge> Edges = new List<NavMapEdge>();
    public readonly List<NavMapIsland> Islands = new List<NavMapIsland>();
    public readonly Dictionary<string, NavMapNode> NodeById = new Dictionary<string, NavMapNode>();
    public int Width = 1200;
    public int Height = 720;
}

public sealed class NavMapNode
{
    public string Id;
    public string Name;
    public string PrefabPath;
    public int X;
    public int Y;
    public int Column;
    public int Row;
}

public sealed class NavMapEdge
{
    public string FromViewId;
    public string ToViewId;
    public string ControlId;
    public string Kind;
    public string Label;
}

public sealed class NavMapIsland
{
    public string Name;
    public int X;
    public int Width;
    public int Height;
}

[Serializable]
public sealed class NavMapDocument
{
    public NavMapView[] views;
    public NavMapTransition[] transitions;
}

[Serializable]
public sealed class NavMapView
{
    public string id;
    public string name;
    public string prefabPath;
}

[Serializable]
public sealed class NavMapTransition
{
    public string fromViewId;
    public string toViewId;
    public string controlId;
    public string kind;
}
