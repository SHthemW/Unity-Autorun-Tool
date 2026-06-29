using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public sealed class UiNavMap
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
            string input = string.IsNullOrWhiteSpace(mapPath)
                ? Environment.GetEnvironmentVariable("UNITY_AUTORUN_NAV_MAP") ?? "ui-nav-map.json"
                : mapPath;
            string fullPath = System.IO.Path.GetFullPath(input);
            JsonObject map = JsonNode.Parse(File.ReadAllText(fullPath))?.AsObject()
                ?? throw new InvalidOperationException($"Invalid UI nav map: {fullPath}");
            return new UiNavMap(map, fullPath);
        }

        public JsonArray ListRoutes()
        {
            var routes = new JsonArray();
            foreach (JsonObject route in Objects("routes"))
            {
                routes.Add(JsonUtil.Obj(
                    ("id", Text(route, "id")),
                    ("fromViewId", Text(route, "fromViewId")),
                    ("toViewId", Text(route, "toViewId")),
                    ("steps", Steps(route).Count),
                    ("autoRunSteps", ResolveAutoRunSequence(route).Count)
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
                route = Objects("routes").FirstOrDefault(item => Text(item, "fromViewId") == fromViewId && Text(item, "toViewId") == toViewId);
            }

            if (route == null && fromViewId != null && toViewId != null)
            {
                route = BuildRoute(fromViewId, toViewId);
            }

            if (route == null)
            {
                throw new InvalidOperationException("Route not found. Provide --route or both --from and --to.");
            }

            JsonArray autoRunSequence = ResolveAutoRunSequence(route);
            if (autoRunSequence.Count == 0)
            {
                throw new InvalidOperationException($"Route '{Text(route, "id")}' has no AutoRun actions.");
            }

            return JsonUtil.Obj(
                ("id", Text(route, "id")),
                ("fromViewId", Text(route, "fromViewId")),
                ("toViewId", Text(route, "toViewId")),
                ("steps", CloneSteps(route)),
                ("autoRunSequence", autoRunSequence)
            );
        }

        private JsonObject BuildRoute(string fromViewId, string toViewId)
        {
            var queue = new Queue<Tuple<string, List<JsonObject>>>();
            var visited = new HashSet<string> { fromViewId };
            queue.Enqueue(Tuple.Create(fromViewId, new List<JsonObject>()));

            while (queue.Count > 0)
            {
                Tuple<string, List<JsonObject>> current = queue.Dequeue();
                if (current.Item1 == toViewId)
                {
                    var routeSteps = new JsonArray();
                    foreach (JsonObject step in current.Item2)
                    {
                        routeSteps.Add(step.DeepClone());
                    }

                    return JsonUtil.Obj(("id", $"computed.{fromViewId}.to.{toViewId}"), ("fromViewId", fromViewId), ("toViewId", toViewId), ("steps", routeSteps));
                }

                foreach (JsonObject transition in Objects("transitions").Where(item => Text(item, "fromViewId") == current.Item1))
                {
                    string next = Text(transition, "toViewId");
                    if (next == null || !visited.Add(next))
                    {
                        continue;
                    }

                    var nextSteps = new List<JsonObject>(current.Item2)
                    {
                        JsonUtil.Obj(("transitionId", Text(transition, "id")), ("controlId", Text(transition, "controlId"))),
                    };
                    queue.Enqueue(Tuple.Create(next, nextSteps));
                }
            }

            return null;
        }

        private JsonArray ResolveAutoRunSequence(JsonObject route)
        {
            if (route["autoRunSequence"] is JsonArray explicitSequence && explicitSequence.Count > 0)
            {
                return JsonUtil.CloneArray(explicitSequence);
            }

            var sequence = new JsonArray();
            foreach (JsonObject step in Steps(route))
            {
                JsonObject control = FindStepControl(step)
                    ?? throw new InvalidOperationException($"Step '{step.ToJsonString()}' has no control.");
                sequence.Add(control["autoRun"]?.DeepClone()
                    ?? throw new InvalidOperationException($"Control '{Text(control, "id")}' has no AutoRun action."));
            }

            return sequence;
        }

        private JsonObject FindStepControl(JsonObject step)
        {
            string controlId = Text(step, "controlId");
            if (controlId == null && Text(step, "transitionId") is string transitionId)
            {
                controlId = Objects("transitions").FirstOrDefault(item => Text(item, "id") == transitionId)?["controlId"]?.GetValue<string>();
            }

            return Objects("controls").FirstOrDefault(item => Text(item, "id") == controlId);
        }

        private string ResolveViewId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            JsonObject view = Objects("views").FirstOrDefault(item => Text(item, "id") == value || Text(item, "name") == value);
            return view?["id"]?.GetValue<string>() ?? value;
        }

        private JsonArray CloneSteps(JsonObject route)
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
            return obj[key]?.GetValue<string>();
        }
    }
}

