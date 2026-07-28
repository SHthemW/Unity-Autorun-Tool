using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public sealed partial class UiNavMap
    {
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

        private JsonArray ResolveNavigationSteps(JsonObject route)
        {
            var sequence = new JsonArray();
            foreach (JsonObject step in Steps(route))
            {
                sequence.Add(ResolveNavigationStep(step));
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

        private JsonObject ResolveNavigationStep(JsonObject step)
        {
            JsonObject transition = FindStepTransition(step);
            JsonObject automation = transition?["automation"] as JsonObject;
            JsonNode autoRun = ResolveStepAutoRun(step);
            string mode = Text(automation, "mode") ?? (autoRun != null ? "click" : "wait");
            string waitForViewId = Text(automation, "waitForViewId") ?? Text(transition, "toViewId");
            var result = JsonUtil.Obj(
                ("transitionId", Text(step, "transitionId")),
                ("fromViewId", Text(transition, "fromViewId")),
                ("toViewId", Text(transition, "toViewId")),
                ("kind", Text(transition, "kind") ?? "interaction"),
                ("mode", mode),
                ("isAutoRunnable", autoRun != null),
                ("waitForViewId", ResolveViewRuntimeToken(waitForViewId))
            );

            if (automation?["timeout"] != null)
            {
                result["timeout"] = automation["timeout"].DeepClone();
            }

            if (autoRun != null)
            {
                result["action"] = autoRun.DeepClone();
            }

            return result;
        }

        private string ResolveViewRuntimeToken(string viewIdOrName)
        {
            string viewId = ResolveViewId(viewIdOrName);
            JsonObject view = Objects("views").FirstOrDefault(item => Text(item, "id") == viewId);
            if (view == null)
            {
                return viewIdOrName;
            }

            string rootObjectPath = Text(view, "rootObjectPath");
            if (!string.IsNullOrEmpty(rootObjectPath))
            {
                return rootObjectPath;
            }

            return Text(view, "name") ?? Text(view, "id");
        }

        private bool IsFullyAutoRunnable(JsonObject route, JsonArray autoRunSequence)
        {
            return Steps(route).Count > 0 && Steps(route).Count == autoRunSequence.Count;
        }

        private static bool IsNavigationRunnable(JsonArray navigationSteps)
        {
            if (navigationSteps == null || navigationSteps.Count == 0)
            {
                return false;
            }

            foreach (JsonObject step in navigationSteps.OfType<JsonObject>())
            {
                string mode = Text(step, "mode");
                bool supported = mode == "wait" || (mode == "click" && step["action"] != null);
                if (!supported)
                {
                    return false;
                }
            }

            return true;
        }

        private JsonNode ResolveStepAutoRun(JsonObject step)
        {
            JsonObject transition = FindStepTransition(step);
            JsonObject automation = transition?["automation"] as JsonObject;
            JsonObject control = FindStepControl(step);
            if (automation?["autoRun"] != null)
            {
                JsonNode autoRun = NormalizeAutoRun(automation["autoRun"], control);
                return IsDefaultAction(autoRun) ? null : autoRun;
            }

            JsonNode controlAutoRun = NormalizeAutoRun(control?["autoRun"], control);
            return IsDefaultAction(controlAutoRun) ? null : controlAutoRun;
        }

        private static JsonNode NormalizeAutoRun(JsonNode autoRun, JsonObject control)
        {
            if (autoRun == null)
            {
                return CreateAutoRunFromControl(control);
            }

            string objectPathButtonName = GetObjectPathLeaf(Text(control, "objectPath"));
            if (!IsDefaultAction(autoRun) && !IsFairyGUIControl(control) && !string.IsNullOrEmpty(objectPathButtonName))
            {
                return CloneAutoRunWithButtonName(autoRun, objectPathButtonName);
            }

            if (control == null || !IsDefaultAction(autoRun))
            {
                return autoRun;
            }

            JsonObject fallback = CreateAutoRunFromControl(control);
            if (fallback == null)
            {
                return autoRun;
            }

            if (autoRun["delay"] != null)
            {
                fallback["delay"] = autoRun["delay"].DeepClone();
            }

            if (autoRun["isTest"] != null)
            {
                fallback["isTest"] = autoRun["isTest"].DeepClone();
            }

            return fallback;
        }

        private static JsonNode CloneAutoRunWithButtonName(JsonNode autoRun, string buttonName)
        {
            JsonObject clone = autoRun.DeepClone().AsObject();
            clone["buttonName"] = buttonName;
            return clone;
        }

        private static bool IsFairyGUIControl(JsonObject control)
        {
            return string.Equals(Text(control, "framework"), "fairygui", StringComparison.OrdinalIgnoreCase);
        }

        private static JsonObject CreateAutoRunFromControl(JsonObject control)
        {
            string buttonName = ResolveAutoRunButtonName(control);
            if (buttonName == null)
            {
                return null;
            }

            return JsonUtil.Obj(
                ("buttonName", buttonName),
                ("buttonText", Text(control, "text") ?? "untitled"),
                ("isFairyGUI", string.Equals(Text(control, "framework"), "fairygui", StringComparison.OrdinalIgnoreCase))
            );
        }

        private static string ResolveAutoRunButtonName(JsonObject control)
        {
            string pathLeaf = GetObjectPathLeaf(Text(control, "objectPath"));
            return string.IsNullOrEmpty(pathLeaf) ? Text(control, "name") : pathLeaf;
        }

        private static string GetObjectPathLeaf(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return null;
            }

            string normalized = objectPath.Replace('\\', '/').Trim('/');
            int slashIndex = normalized.LastIndexOf('/');
            return slashIndex >= 0 ? normalized.Substring(slashIndex + 1) : normalized;
        }

        private static bool IsDefaultAction(JsonNode autoRun)
        {
            string buttonName = autoRun?["buttonName"]?.GetValue<string>();
            return string.IsNullOrEmpty(buttonName) || buttonName == "unnamed";
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
