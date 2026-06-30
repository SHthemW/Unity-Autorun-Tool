using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public sealed partial class UiNavMap
    {
        private const int UnsupportedNavigationStepCost = 10000;

        private JsonObject ResolveBestRouteObject(string fromViewId, string toViewId)
        {
            var candidates = Objects("routes")
                .Where(item => Text(item, "fromViewId") == fromViewId && Text(item, "toViewId") == toViewId)
                .ToList();

            JsonObject computed = BuildRoute(fromViewId, toViewId);
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

        private JsonObject BuildRoute(string fromViewId, string toViewId)
        {
            var open = new List<RouteSearchNode>
            {
                new RouteSearchNode(fromViewId, new List<JsonObject>(), 0),
            };
            var bestCosts = new Dictionary<string, int>
            {
                [fromViewId] = 0,
            };

            while (open.Count > 0)
            {
                RouteSearchNode current = open.OrderBy(node => node.Cost).ThenBy(node => node.Steps.Count).First();
                open.Remove(current);
                if (current.ViewId == toViewId)
                {
                    return BuildComputedRoute(fromViewId, toViewId, current.Steps);
                }

                foreach (JsonObject transition in Objects("transitions").Where(item => Text(item, "fromViewId") == current.ViewId))
                {
                    string next = Text(transition, "toViewId");
                    if (next == null)
                    {
                        continue;
                    }

                    JsonObject nextStep = BuildStep(transition);
                    int nextCost = current.Cost + NavigationStepCost(nextStep);
                    if (bestCosts.TryGetValue(next, out int bestCost) && bestCost <= nextCost)
                    {
                        continue;
                    }

                    bestCosts[next] = nextCost;
                    var nextSteps = new List<JsonObject>(current.Steps)
                    {
                        nextStep,
                    };
                    open.Add(new RouteSearchNode(next, nextSteps, nextCost));
                }
            }

            return null;
        }

        private int RouteCost(JsonObject route)
        {
            return Steps(route).Sum(NavigationStepCost);
        }

        private static bool IsComputedRoute(JsonObject route)
        {
            return Text(route, "id")?.StartsWith("computed.") == true;
        }

        private int NavigationStepCost(JsonObject step)
        {
            JsonObject navigationStep = ResolveNavigationStep(step);
            string mode = Text(navigationStep, "mode");
            bool supported = mode == "wait" || (mode == "click" && navigationStep["action"] != null);
            return supported ? 1 : UnsupportedNavigationStepCost;
        }

        private static JsonObject BuildComputedRoute(string fromViewId, string toViewId, List<JsonObject> steps)
        {
            var routeSteps = new JsonArray();
            foreach (JsonObject step in steps)
            {
                routeSteps.Add(step.DeepClone());
            }

            return JsonUtil.Obj(("id", $"computed.{fromViewId}.to.{toViewId}"), ("fromViewId", fromViewId), ("toViewId", toViewId), ("steps", routeSteps));
        }

        private sealed class RouteSearchNode
        {
            public RouteSearchNode(string viewId, List<JsonObject> steps, int cost)
            {
                ViewId = viewId;
                Steps = steps;
                Cost = cost;
            }

            public string ViewId { get; }
            public List<JsonObject> Steps { get; }
            public int Cost { get; }
        }
    }
}
