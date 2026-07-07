using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public sealed partial class UiNavMap
    {
        private readonly JsonObject _map;

        private UiNavMap(JsonObject map, string path)
        {
            _map = map;
            Path = path;
        }

        public string Path { get; private set; }

        public static UiNavMap Load(string mapPath)
        {
            string fullPath = UiNavMapPaths.ResolveMapPath(mapPath);
            JsonObject map = JsonNode.Parse(File.ReadAllText(fullPath))?.AsObject()
                ?? throw new InvalidOperationException($"Invalid UI nav map: {fullPath}");
            return new UiNavMap(map, fullPath);
        }

        public JsonArray ListRoutes()
        {
            var routes = new JsonArray();
            foreach (JsonObject route in Objects("routes"))
            {
                JsonArray autoRunSequence = ResolveAutoRunSequence(route);
                JsonArray automationSequence = ResolveAutomationSequence(route);
                JsonArray navigationSteps = ResolveNavigationSteps(route);
                routes.Add(JsonUtil.Obj(
                    ("id", Text(route, "id")),
                    ("fromViewId", Text(route, "fromViewId")),
                    ("toViewId", Text(route, "toViewId")),
                    ("steps", Steps(route).Count),
                    ("autoRunSteps", autoRunSequence.Count),
                    ("automationSteps", automationSequence.Count),
                    ("navigationSteps", navigationSteps.Count),
                    ("isFullyAutoRunnable", IsFullyAutoRunnable(route, autoRunSequence)),
                    ("isNavigationRunnable", IsNavigationRunnable(navigationSteps))
                ));
            }

            return routes;
        }

        public JsonObject ResolveRoute(string routeId, string from, string to)
        {
            string fromViewId = ResolveViewId(from);
            string toViewId = ResolveViewId(to);
            JsonObject route = null;

            if (!string.IsNullOrWhiteSpace(routeId))
            {
                route = Objects("routes").FirstOrDefault(item => Text(item, "id") == routeId);
            }
            else if (fromViewId != null && toViewId != null)
            {
                route = ResolveBestRouteObject(fromViewId, toViewId);
            }

            if (route == null)
            {
                throw new InvalidOperationException("Route not found. Provide --route or both --from and --to.");
            }

            JsonArray autoRunSequence = ResolveAutoRunSequence(route);
            JsonArray automationSequence = ResolveAutomationSequence(route);
            JsonArray navigationSteps = ResolveNavigationSteps(route);

            return JsonUtil.Obj(
                ("id", Text(route, "id")),
                ("fromViewId", Text(route, "fromViewId")),
                ("toViewId", Text(route, "toViewId")),
                ("steps", CloneSteps(route)),
                ("automationSequence", automationSequence),
                ("isFullyAutoRunnable", IsFullyAutoRunnable(route, autoRunSequence)),
                ("isNavigationRunnable", IsNavigationRunnable(navigationSteps)),
                ("navigationSteps", navigationSteps),
                ("autoRunSequence", autoRunSequence)
            );
        }

        public JsonObject ResolveBestRoute(string from, string to, IEnumerable<string> openViews)
        {
            if (!string.IsNullOrWhiteSpace(from))
            {
                return ResolveRoute(null, from, to);
            }

            foreach (string openView in openViews ?? Enumerable.Empty<string>())
            {
                string fromViewId = ResolveViewId(openView);
                string toViewId = ResolveViewId(to);
                if (fromViewId == null || toViewId == null || fromViewId == toViewId)
                {
                    continue;
                }

                JsonObject route = TryResolveRoute(fromViewId, toViewId);
                if (route != null)
                {
                    return route;
                }
            }

            return ResolveRoute(null, from, to);
        }

        private string ResolveViewId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = NormalizeViewToken(value);
            JsonObject view = Objects("views").FirstOrDefault(item =>
                Text(item, "id") == value
                || Text(item, "name") == value
                || IsViewTokenMatch(NormalizeViewToken(Text(item, "id")), normalized)
                || IsViewTokenMatch(NormalizeViewToken(Text(item, "name")), normalized));
            return view?["id"]?.GetValue<string>() ?? value;
        }

        private static string NormalizeViewToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string token = value.ToLowerInvariant();
            if (token.StartsWith("view."))
            {
                token = token.Substring("view.".Length);
            }

            return token
                .Replace(".", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);
        }

        private static bool IsViewTokenMatch(string candidate, string target)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
            {
                return false;
            }

            return candidate == target
                || (target.Length >= 4 && candidate.EndsWith(target))
                || (candidate.Length >= 4 && target.EndsWith(candidate));
        }

        private JsonObject TryResolveRoute(string fromViewId, string toViewId)
        {
            try
            {
                return ResolveRoute(null, fromViewId, toViewId);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static JsonArray CloneSteps(JsonObject route)
        {
            return JsonUtil.CloneArray(route["steps"]?.AsArray() ?? new JsonArray());
        }

        private static IReadOnlyList<JsonObject> Steps(JsonObject route)
        {
            return route["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        }

        private IEnumerable<JsonObject> Objects(string key)
        {
            return _map[key]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();
        }

        private static string Text(JsonObject obj, string key)
        {
            return obj?[key]?.GetValue<string>();
        }
    }
}

