using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public sealed partial class UiNavMap
    {
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
                    return BuildComputedRoute(fromViewId, toViewId, current.Item2);
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
                        BuildStep(transition),
                    };
                    queue.Enqueue(Tuple.Create(next, nextSteps));
                }
            }

            return null;
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

        private JsonArray ResolveAutoRunSequence(JsonObject route)
        {
            if (route["autoRunSequence"] is JsonArray explicitSequence && explicitSequence.Count > 0)
            {
                return JsonUtil.CloneArray(explicitSequence);
            }

            var sequence = new JsonArray();
            foreach (JsonObject step in Steps(route))
            {
                JsonNode autoRun = ResolveStepAutoRun(step);
                if (autoRun != null)
                {
                    sequence.Add(autoRun.DeepClone());
                }
            }

            return sequence;
        }

        private JsonArray ResolveAutomationSequence(JsonObject route)
        {
            var sequence = new JsonArray();
            foreach (JsonObject step in Steps(route))
            {
                sequence.Add(ResolveStepAutomation(step));
            }

            return sequence;
        }

        private JsonObject ResolveStepAutomation(JsonObject step)
        {
            JsonObject transition = FindStepTransition(step);
            JsonObject automation = transition?["automation"] as JsonObject;
            JsonNode autoRun = ResolveStepAutoRun(step);
            string mode = Text(automation, "mode") ?? (autoRun != null ? "click" : "wait");

            return JsonUtil.Obj(
                ("transitionId", Text(step, "transitionId")),
                ("fromViewId", Text(transition, "fromViewId")),
                ("toViewId", Text(transition, "toViewId")),
                ("kind", Text(transition, "kind") ?? "interaction"),
                ("mode", mode),
                ("isAutoRunnable", autoRun != null),
                ("automation", automation?.DeepClone())
            );
        }

        private bool IsFullyAutoRunnable(JsonObject route, JsonArray autoRunSequence)
        {
            return Steps(route).Count > 0 && Steps(route).Count == autoRunSequence.Count;
        }

        private JsonNode ResolveStepAutoRun(JsonObject step)
        {
            JsonObject transition = FindStepTransition(step);
            JsonObject automation = transition?["automation"] as JsonObject;
            if (automation?["autoRun"] != null)
            {
                return automation["autoRun"];
            }

            JsonObject control = FindStepControl(step);
            return control?["autoRun"];
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

        private JsonObject FindStepTransition(JsonObject step)
        {
            string transitionId = Text(step, "transitionId");
            return transitionId == null ? null : Objects("transitions").FirstOrDefault(item => Text(item, "id") == transitionId);
        }

        private static JsonObject BuildStep(JsonObject transition)
        {
            var step = JsonUtil.Obj(("transitionId", Text(transition, "id")));
            string controlId = Text(transition, "controlId");
            if (controlId != null)
            {
                step["controlId"] = controlId;
            }

            return step;
        }
    }
}
