using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class NavigationAutoRunMap
{
    private const string NavMapPath = "mcp/ui-nav-map.json";
    private const string ExampleNavMapPath = "mcp/ui-nav-map.example.json";

    private readonly NavigationMapDocument _document;
    private readonly Dictionary<string, NavigationMapView> _views;
    private readonly Dictionary<string, NavigationMapControl> _controls;
    private readonly Dictionary<string, NavigationMapTransition> _transitions;

    private NavigationAutoRunMap(NavigationMapDocument document, string path)
    {
        _document = document;
        Path = path;
        _views = (document.views ?? new NavigationMapView[0])
            .Where(view => view != null && !string.IsNullOrEmpty(view.id))
            .GroupBy(view => view.id)
            .ToDictionary(group => group.Key, group => group.First());
        _controls = (document.controls ?? new NavigationMapControl[0])
            .Where(control => control != null && !string.IsNullOrEmpty(control.id))
            .GroupBy(control => control.id)
            .ToDictionary(group => group.Key, group => group.First());
        _transitions = (document.transitions ?? new NavigationMapTransition[0])
            .Where(transition => transition != null && !string.IsNullOrEmpty(transition.id))
            .GroupBy(transition => transition.id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public string Path { get; private set; }

    public static NavigationAutoRunMap LoadDefault()
    {
        string root = McpInstallConfig.GetToolRootDirectory();
        string path = ResolveMapPath(root);
        NavigationMapDocument document = JsonUtility.FromJson<NavigationMapDocument>(File.ReadAllText(path));
        if (document == null)
        {
            throw new InvalidOperationException("Invalid nav map: " + path);
        }

        return new NavigationAutoRunMap(document, path);
    }

    public static string GetDefaultMapPath()
    {
        return ResolveMapPath(McpInstallConfig.GetToolRootDirectory());
    }

    public List<NavigationAutoRunOption> ListNavigableTargets()
    {
        var targetIds = new HashSet<string>();
        foreach (NavigationMapRoute route in Routes())
        {
            AddTarget(targetIds, route.toViewId);
        }

        foreach (NavigationMapTransition transition in Transitions())
        {
            AddTarget(targetIds, transition.toViewId);
        }

        return targetIds
            .Select(ToOption)
            .Where(option => option != null)
            .OrderBy(option => option.DisplayName)
            .ToList();
    }

    public NavigationAutoRunPlan ResolveToTarget(string targetViewId, IEnumerable<string> openViews)
    {
        string toViewId = ResolveViewId(targetViewId);
        if (string.IsNullOrEmpty(toViewId))
        {
            throw new InvalidOperationException("Target view is required.");
        }

        var plans = new List<NavigationAutoRunPlan>();
        foreach (string openView in openViews ?? Enumerable.Empty<string>())
        {
            string fromViewId = ResolveOpenViewId(openView);
            if (fromViewId == null || !_views.ContainsKey(fromViewId))
            {
                continue;
            }

            if (fromViewId == toViewId)
            {
                return new NavigationAutoRunPlan { RouteId = "already." + toViewId, FromViewId = fromViewId, ToViewId = toViewId };
            }

            NavigationMapRoute route = TryResolveRoute(fromViewId, toViewId);
            if (route != null)
            {
                plans.Add(BuildPlan(route));
            }
        }

        foreach (string inferredViewId in InferOpenViewIdsFromVisibleControls())
        {
            if (inferredViewId == toViewId)
            {
                return new NavigationAutoRunPlan { RouteId = "already." + toViewId, FromViewId = inferredViewId, ToViewId = toViewId };
            }

            NavigationMapRoute route = TryResolveRoute(inferredViewId, toViewId);
            if (route != null)
            {
                plans.Add(BuildPlan(route));
            }
        }

        foreach (string startupViewId in StartupViewIds())
        {
            if (startupViewId == toViewId)
            {
                continue;
            }

            NavigationMapRoute route = TryResolveRoute(startupViewId, toViewId);
            if (route != null)
            {
                plans.Add(BuildPlan(route));
            }
        }

        NavigationAutoRunPlan plan = SelectBestPlan(plans);
        if (plan != null)
        {
            return plan;
        }

        throw new InvalidOperationException("Route not found from current active views to " + toViewId + ".");
    }

    private IEnumerable<string> InferOpenViewIdsFromVisibleControls()
    {
        var viewIds = new HashSet<string>();
        foreach (NavigationMapControl control in _controls.Values)
        {
            if (control == null || string.IsNullOrEmpty(control.viewId))
            {
                continue;
            }

            AutoRunParam action = NormalizeAutoRun(control.autoRun, control);
            if (IsDefaultAction(action) || !AutoRunButtonService.HasButton(action))
            {
                continue;
            }

            AddTarget(viewIds, control.viewId);
        }

        return viewIds;
    }

    private NavigationAutoRunPlan BuildPlan(NavigationMapRoute route)
    {
        var plan = new NavigationAutoRunPlan
        {
            RouteId = route.id,
            FromViewId = route.fromViewId,
            ToViewId = route.toViewId,
        };
        foreach (NavigationMapRouteStep step in Steps(route))
        {
            plan.Steps.Add(ResolveNavigationStep(step));
        }

        return plan;
    }

    private NavigationAutoRunOption ToOption(string viewId)
    {
        if (!_views.TryGetValue(viewId, out NavigationMapView view))
        {
            return null;
        }

        string name = string.IsNullOrEmpty(view.name) ? view.id : view.name;
        return new NavigationAutoRunOption
        {
            ViewId = view.id,
            Name = name,
            DisplayName = name + " (" + view.id + ")",
        };
    }

    private static void AddTarget(HashSet<string> targetIds, string viewId)
    {
        if (!string.IsNullOrEmpty(viewId))
        {
            targetIds.Add(viewId);
        }
    }

    private IEnumerable<string> StartupViewIds()
    {
        var startupIds = new HashSet<string>();
        foreach (NavigationMapTransition transition in Transitions())
        {
            if (IsStartupTransition(transition))
            {
                AddTarget(startupIds, transition.fromViewId);
            }
        }

        foreach (NavigationMapRoute route in Routes())
        {
            if (IsStartupViewId(route.fromViewId))
            {
                AddTarget(startupIds, route.fromViewId);
            }
        }

        foreach (string viewId in _views.Keys)
        {
            if (IsStartupViewId(viewId))
            {
                AddTarget(startupIds, viewId);
            }
        }

        return startupIds;
    }

    private static bool IsStartupTransition(NavigationMapTransition transition)
    {
        return transition != null
            && (IsStartupViewId(transition.fromViewId)
                || (string.Equals(transition.kind, "lifecycle", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(transition.automation?.mode, "wait", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsStartupViewId(string viewId)
    {
        if (string.IsNullOrEmpty(viewId))
        {
            return false;
        }

        string token = NormalizeViewToken(viewId);
        return token == "appstart" || token == "start" || token == "startup";
    }

    private static NavigationAutoRunPlan SelectBestPlan(IEnumerable<NavigationAutoRunPlan> plans)
    {
        return plans
            .Where(plan => plan != null)
            .GroupBy(plan => plan.RouteId)
            .Select(group => group.First())
            .OrderBy(plan => IsReadyToStart(plan) ? 0 : 1)
            .ThenBy(plan => plan.Steps.Count)
            .FirstOrDefault();
    }

    private static bool IsReadyToStart(NavigationAutoRunPlan plan)
    {
        if (plan == null || plan.Steps.Count == 0)
        {
            return true;
        }

        AutoRunNavStep firstStep = plan.Steps[0];
        if (firstStep.mode == "wait")
        {
            return true;
        }

        return firstStep.mode == "click"
            && firstStep.action != null
            && AutoRunButtonService.HasButton(firstStep.action);
    }

    private static string ResolveMapPath(string root)
    {
        string mapPath = System.IO.Path.Combine(root, NavMapPath);
        if (File.Exists(mapPath))
        {
            return mapPath;
        }

        string examplePath = System.IO.Path.Combine(root, ExampleNavMapPath);
        if (File.Exists(examplePath))
        {
            return examplePath;
        }

        throw new FileNotFoundException("Cannot find mcp/ui-nav-map.json or mcp/ui-nav-map.example.json.");
    }
}
