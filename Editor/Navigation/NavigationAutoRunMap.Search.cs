using System.Collections.Generic;
using System.Linq;

public sealed partial class NavigationAutoRunMap
{
    private const int UnsupportedNavigationStepCost = 10000;

    private NavigationMapRoute ResolveBestRouteObject(string fromViewId, string toViewId)
    {
        var candidates = Routes()
            .Where(item => item.fromViewId == fromViewId && item.toViewId == toViewId)
            .ToList();

        NavigationMapRoute computed = BuildRoute(fromViewId, toViewId);
        if (computed != null)
        {
            candidates.Add(computed);
        }

        return candidates
            .OrderBy(RouteCost)
            .ThenBy(IsComputedRoute)
            .ThenBy(route => Steps(route).Count)
            .FirstOrDefault();
    }

    private NavigationMapRoute BuildRoute(string fromViewId, string toViewId)
    {
        var open = new List<RouteSearchNode>
        {
            new RouteSearchNode(fromViewId, new List<NavigationMapRouteStep>(), 0),
        };
        var bestCosts = new Dictionary<string, int> { [fromViewId] = 0 };

        while (open.Count > 0)
        {
            RouteSearchNode current = open.OrderBy(node => node.Cost).ThenBy(node => node.Steps.Count).First();
            open.Remove(current);
            if (current.ViewId == toViewId)
            {
                return BuildComputedRoute(fromViewId, toViewId, current.Steps);
            }

            foreach (NavigationMapTransition transition in Transitions().Where(item => item.fromViewId == current.ViewId))
            {
                if (string.IsNullOrEmpty(transition.toViewId))
                {
                    continue;
                }

                NavigationMapRouteStep nextStep = BuildStep(transition);
                int nextCost = current.Cost + NavigationStepCost(nextStep);
                if (bestCosts.TryGetValue(transition.toViewId, out int bestCost) && bestCost <= nextCost)
                {
                    continue;
                }

                bestCosts[transition.toViewId] = nextCost;
                var nextSteps = new List<NavigationMapRouteStep>(current.Steps) { nextStep };
                open.Add(new RouteSearchNode(transition.toViewId, nextSteps, nextCost));
            }
        }

        return null;
    }

    private int RouteCost(NavigationMapRoute route)
    {
        return Steps(route).Sum(NavigationStepCost);
    }

    private int NavigationStepCost(NavigationMapRouteStep step)
    {
        AutoRunNavStep navigationStep = ResolveNavigationStep(step);
        bool supported = navigationStep.mode == "wait" || (navigationStep.mode == "click" && navigationStep.action != null);
        return supported ? 1 : UnsupportedNavigationStepCost;
    }

    private static bool IsComputedRoute(NavigationMapRoute route)
    {
        return route.id != null && route.id.StartsWith("computed.");
    }

    private static NavigationMapRoute BuildComputedRoute(string fromViewId, string toViewId, List<NavigationMapRouteStep> steps)
    {
        return new NavigationMapRoute
        {
            id = "computed." + fromViewId + ".to." + toViewId,
            fromViewId = fromViewId,
            toViewId = toViewId,
            steps = steps.ToArray(),
        };
    }

    private static NavigationMapRouteStep BuildStep(NavigationMapTransition transition)
    {
        return new NavigationMapRouteStep
        {
            transitionId = transition.id,
            controlId = transition.controlId,
        };
    }

    private sealed class RouteSearchNode
    {
        public RouteSearchNode(string viewId, List<NavigationMapRouteStep> steps, int cost)
        {
            ViewId = viewId;
            Steps = steps;
            Cost = cost;
        }

        public string ViewId { get; }
        public List<NavigationMapRouteStep> Steps { get; }
        public int Cost { get; }
    }
}
