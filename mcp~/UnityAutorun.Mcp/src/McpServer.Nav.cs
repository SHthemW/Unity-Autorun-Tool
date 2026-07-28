using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public sealed partial class McpServer
    {
        private static JsonObject ListRoutes(JsonObject args)
        {
            UiNavMap map = UiNavMap.Load(Text(args, "mapPath"));
            return JsonUtil.Obj(("ok", true), ("path", map.Path), ("routes", map.ListRoutes()));
        }

        private static JsonObject ResolveRoute(JsonObject args)
        {
            UiNavMap map = UiNavMap.Load(Text(args, "mapPath"));
            return JsonUtil.Obj(("ok", true), ("path", map.Path), ("route", Resolve(map, args)));
        }

        private async Task<JsonNode> RunRouteAsync(JsonObject args)
        {
            UiNavMap map = UiNavMap.Load(Text(args, "mapPath"));
            JsonObject route = Resolve(map, args);
            if (route["isNavigationRunnable"]?.GetValue<bool>() != true)
            {
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "route_not_runnable"),
                    ("message", "Route contains manual or unsupported transitions. Use resolve_ui_route to inspect navigationSteps."),
                    ("route", route)
                );
            }

            JsonNode navigationResult = await _bridge.CallUnityAsync("navigate_route", JsonUtil.Obj(
                ("routeId", route["id"]?.DeepClone()),
                ("targetViewId", route["toViewId"]?.DeepClone()),
                ("navigationSteps", route["navigationSteps"]?.DeepClone() ?? new JsonArray())
            ));
            AddRouteInfo(navigationResult, map, route, true);
            return navigationResult;
        }

        private async Task<JsonNode> NavigateUiAsync(JsonObject args)
        {
            UiNavMap map = UiNavMap.Load(Text(args, "mapPath"));
            JsonArray openViews = await ListOpenViewsAsync();
            JsonObject route = map.ResolveBestRoute(Text(args, "from"), Text(args, "to"), openViews.Select(item => item?.GetValue<string>()));
            JsonNode result = await _bridge.CallUnityAsync("navigate_route", JsonUtil.Obj(
                ("routeId", route["id"]?.DeepClone()),
                ("targetViewId", route["toViewId"]?.DeepClone()),
                ("navigationSteps", route["navigationSteps"]?.DeepClone() ?? new JsonArray())
            ));
            AddRouteInfo(result, map, route, true);
            return result;
        }

        private async Task<JsonNode> StartUiNavigationAsync(JsonObject args)
        {
            return await _bridge.CallUnityAsync("start_ui_navigation", JsonUtil.Obj(
                ("targetViewId", Text(args, "targetViewId") ?? Text(args, "to")),
                ("navigationId", Text(args, "navigationId")),
                ("ensurePlayMode", Bool(args, "ensurePlayMode", true))
            ));
        }

        private async Task<JsonNode> GetUiNavigationStatusAsync(JsonObject args)
        {
            int waitMilliseconds = Math.Max(0, Math.Min(25000, Int(args, "waitMs", 20000)));
            int pollMilliseconds = Math.Max(100, Math.Min(2000, Int(args, "pollMs", 500)));
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(waitMilliseconds);
            JsonNode result;
            do
            {
                result = await _bridge.CallUnityAsync("get_ui_navigation_status", JsonUtil.Obj(
                    ("navigationId", Text(args, "navigationId"))
                ));
                if (IsTerminalNavigationStatus(result) || DateTimeOffset.UtcNow >= deadline)
                {
                    return result;
                }

                int remaining = (int)Math.Max(0, (deadline - DateTimeOffset.UtcNow).TotalMilliseconds);
                await Task.Delay(Math.Min(pollMilliseconds, remaining));
            }
            while (true);
        }

        private static bool IsTerminalNavigationStatus(JsonNode result)
        {
            return result == null
                || result["ok"]?.GetValue<bool>() == false
                || result["data"]?["terminal"]?.GetValue<bool>() == true;
        }

        private async Task<JsonArray> ListOpenViewsAsync()
        {
            JsonNode result = await _bridge.CallUnityAsync("list_open_views", JsonUtil.Obj(("limit", 10000)));
            return result?["data"]?["openViews"]?.AsArray() ?? new JsonArray();
        }

        private static JsonObject Resolve(UiNavMap map, JsonObject args)
        {
            return map.ResolveRoute(Text(args, "route"), Text(args, "from"), Text(args, "to"));
        }

        private static void AddRouteInfo(JsonNode result, UiNavMap map, JsonObject route, bool includeSteps)
        {
            if (result == null)
            {
                return;
            }

            var info = JsonUtil.Obj(
                ("path", map.Path),
                ("id", route["id"]?.DeepClone()),
                ("fromViewId", route["fromViewId"]?.DeepClone()),
                ("toViewId", route["toViewId"]?.DeepClone())
            );
            if (includeSteps)
            {
                info["steps"] = route["steps"]?.DeepClone();
            }

            result["route"] = info;
        }
    }
}
