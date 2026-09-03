using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed partial class NavigationAutoRunMap
{
    private const string SupportedSchemaVersion = "2.0";
    private const string SupportedGeneratorVersion = "2.1";
    private const string SupportedCandidateProtocolVersion = "1.3";

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
        string path = McpInstallConfig.ResolveNavigationMapPath();
        NavigationMapDocument document = ParseDocument(File.ReadAllText(path), path);
        if (document == null)
        {
            throw new InvalidOperationException("Invalid nav map: " + path);
        }

        ValidateMapVersion(document, path);
        ValidateAutoRunSelectors(document, path);
        return new NavigationAutoRunMap(document, path);
    }

    public static string GetDefaultMapPath()
    {
        return McpInstallConfig.ResolveNavigationMapPath();
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
            RuntimeToken = ResolveViewRuntimeToken(view.id),
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

    private static void ValidateMapVersion(NavigationMapDocument document, string path)
    {
        bool valid = string.Equals(document.schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal)
            && string.Equals(document.generatorVersion, SupportedGeneratorVersion, StringComparison.Ordinal)
            && document.mapVersion > 0
            && document.generation != null
            && string.Equals(document.generation.status, "complete", StringComparison.Ordinal)
            && string.Equals(
                document.generation.candidateProtocolVersion,
                SupportedCandidateProtocolVersion,
                StringComparison.Ordinal);
        if (valid)
        {
            return;
        }

        throw new InvalidOperationException(
            "Navigation map is stale or incomplete: "
            + path
            + ". Expected schemaVersion="
            + SupportedSchemaVersion
            + ", generatorVersion="
            + SupportedGeneratorVersion
            + ", mapVersion>0, generation.status=complete, and candidateProtocolVersion="
            + SupportedCandidateProtocolVersion
            + ". Regenerate and finalize the map with the current MCP server.");
    }

    private static void ValidateAutoRunSelectors(
        NavigationMapDocument document,
        string path)
    {
        var views = (document.views ?? new NavigationMapView[0])
            .Where(view => view != null && !string.IsNullOrEmpty(view.id))
            .GroupBy(view => view.id)
            .ToDictionary(group => group.Key, group => group.First());
        var controls = (document.controls ?? new NavigationMapControl[0])
            .Where(control => control != null && !string.IsNullOrEmpty(control.id))
            .GroupBy(control => control.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (NavigationMapControl control in document.controls ?? new NavigationMapControl[0])
        {
            NavigationMapAutoRun autoRun = control?.autoRun;
            if (autoRun == null || string.IsNullOrWhiteSpace(autoRun.matchPolicy))
            {
                continue;
            }

            ValidateMatchPolicy(
                autoRun.matchPolicy,
                path,
                "control '" + control.id + "'",
                "matchPolicy");
        }

        foreach (NavigationMapTransition transition in document.transitions ?? new NavigationMapTransition[0])
        {
            NavigationMapAutoRun autoRun = transition?.automation?.autoRun;
            if (autoRun != null && !string.IsNullOrWhiteSpace(autoRun.matchPolicy))
            {
                ValidateMatchPolicy(
                    autoRun.matchPolicy,
                    path,
                    "transition '" + transition.id + "'",
                    "automation.autoRun.matchPolicy");
            }

            if (transition == null
                || !controls.TryGetValue(transition.controlId ?? string.Empty, out NavigationMapControl control)
                || !views.TryGetValue(control.viewId ?? string.Empty, out NavigationMapView view)
                || !IsPotentiallyRepeatedNestedControl(control, view)
                || !UsesClickAutomation(transition, control))
            {
                continue;
            }

            NavigationMapAutoRun effectiveAutoRun = transition.automation?.autoRun ?? control.autoRun;
            if (effectiveAutoRun == null
                || string.IsNullOrWhiteSpace(effectiveAutoRun.matchPolicy))
            {
                throw InvalidAutoRunSelector(
                    path,
                    "transition '"
                        + transition.id
                        + "' using nested control '"
                        + control.id
                        + "'",
                    "the effective AutoRun selector must declare matchPolicy explicitly; "
                        + "the field was missing or could not be read.");
            }
        }
    }

    private static void ValidateMatchPolicy(
        string matchPolicy,
        string path,
        string owner,
        string fieldName)
    {
        if (string.Equals(
                matchPolicy,
                AutoRunParam.MATCH_UNIQUE,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                matchPolicy,
                AutoRunParam.MATCH_FIRST_INTERACTABLE,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw InvalidAutoRunSelector(
            path,
            owner,
            fieldName
                + " must be 'unique' or 'first-interactable', but was '"
                + matchPolicy
                + "'.");
    }

    private static bool IsPotentiallyRepeatedNestedControl(
        NavigationMapControl control,
        NavigationMapView view)
    {
        if (control == null
            || view == null
            || !string.Equals(
                control.source?.type,
                "serialized-control",
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(control.objectPath)
            || string.IsNullOrWhiteSpace(view.rootObjectPath))
        {
            return false;
        }

        string controlPath = control.objectPath.Replace('\\', '/').Trim('/');
        string viewPath = view.rootObjectPath.Replace('\\', '/').Trim('/');
        if (controlPath.StartsWith(viewPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int separator = controlPath.IndexOf('/');
        if (separator <= 0)
        {
            return false;
        }

        string ownerRoot = controlPath.Substring(0, separator);
        string viewRoot = GetObjectPathLeaf(viewPath);
        return NormalizeViewToken(ownerRoot) != NormalizeViewToken(viewRoot);
    }

    private static bool UsesClickAutomation(
        NavigationMapTransition transition,
        NavigationMapControl control)
    {
        string mode = transition.automation?.mode;
        if (!string.IsNullOrWhiteSpace(mode))
        {
            return string.Equals(mode, "click", StringComparison.OrdinalIgnoreCase);
        }

        return transition.automation?.autoRun != null || control.autoRun != null;
    }

    private static InvalidOperationException InvalidAutoRunSelector(
        string path,
        string owner,
        string detail)
    {
        return new InvalidOperationException(
            "Invalid navigation map AutoRun selector in "
            + path
            + " for "
            + owner
            + ": "
            + detail
            + " Regenerate and finalize the map with the current MCP server.");
    }
}
