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
            if (route["isFullyAutoRunnable"]?.GetValue<bool>() != true)
            {
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "route_not_fully_autorunnable"),
                    ("message", "Route contains app-driven or manual transitions. Use resolve_ui_route and navigate_ui for app-driven wait steps."),
                    ("route", route)
                );
            }

            JsonNode result = await _bridge.CallUnityAsync("run_sequence", JsonUtil.Obj(("actions", route["autoRunSequence"]?.DeepClone())));
            AddRouteInfo(result, map, route, false);
            return result;
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

        private async Task<JsonArray> ListOpenViewsAsync()
        {
            JsonNode result = await _bridge.CallUnityAsync("list_open_views");
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
