using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public static class CliApp
    {
        public static async Task<int> RunAsync(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
            {
                PrintUsage();
                return 0;
            }

            var bridge = new BridgeClient();
            JsonNode result = null;
            string command = args[0];
            if (command == "status")
            {
                result = await bridge.GetStatusAsync();
            }
            else if (command == "play" || command == "stop")
            {
                result = await bridge.CallUnityAsync(command);
            }
            else if (command == "list-buttons")
            {
                result = await bridge.CallUnityAsync("list_buttons", JsonUtil.Obj(("framework", Read(args, "--framework", "all"))));
            }
            else if (command == "click")
            {
                result = await bridge.CallUnityAsync("click_button", ClickPayload(args));
            }
            else if (command == "run-sequence")
            {
                result = await bridge.CallUnityAsync("run_sequence", JsonUtil.Obj(("actions", ReadActions(args))));
            }
            else if (command == "nav-guidance")
            {
                result = UiNavMapGuidance.Get();
            }
            else if (command == "scan-nav-sources")
            {
                result = UiNavMapSourceScanner.Scan(JsonUtil.Obj(
                    ("mapPath", Read(args, "--map")),
                    ("kind", Read(args, "--kind", "all")),
                    ("query", Read(args, "--query", "")),
                    ("offset", ReadInt(args, "--offset", 0)),
                    ("limit", ReadInt(args, "--limit", 50)),
                    ("includeText", ReadBool(args, "--include-text", true))
                ));
            }
            else if (command == "backfill-nav-map")
            {
                result = UiNavMapSourceScanner.Backfill(JsonUtil.Obj(
                    ("mapPath", Read(args, "--map")),
                    ("previewOnly", ReadBool(args, "--preview", true)),
                    ("includeEvidenceBacklog", ReadBool(args, "--include-evidence-backlog", true))
                ));
            }
            else if (command == "routes")
            {
                result = Routes(args);
            }
            else if (command == "route")
            {
                result = Route(args);
            }
            else if (command == "run-route")
            {
                result = await RunRouteAsync(args, bridge);
            }
            else if (command == "navigate-ui")
            {
                result = await NavigateUiAsync(args, bridge);
            }

            if (result == null)
            {
                PrintUsage();
                return 1;
            }

            Console.WriteLine(JsonUtil.Pretty(result));
            return result["ok"]?.GetValue<bool>() == false ? 2 : 0;
        }

        private static JsonObject ClickPayload(string[] args)
        {
            return JsonUtil.Obj(
                ("name", Read(args, "--name")),
                ("text", Read(args, "--text")),
                ("framework", Read(args, "--framework", "ugui"))
            );
        }

        private static JsonArray ReadActions(string[] args)
        {
            string jsonFile = Read(args, "--json-file");
            string json = jsonFile != null ? File.ReadAllText(jsonFile) : Read(args, "--json", "[]") ?? "[]";
            return JsonNode.Parse(json)?.AsArray() ?? new JsonArray();
        }

        private static JsonObject Routes(string[] args)
        {
            UiNavMap map = UiNavMap.Load(Read(args, "--map"));
            return JsonUtil.Obj(("ok", true), ("path", map.Path), ("routes", map.ListRoutes()));
        }

        private static JsonObject Route(string[] args)
        {
            UiNavMap map = UiNavMap.Load(Read(args, "--map"));
            return JsonUtil.Obj(("ok", true), ("path", map.Path), ("route", Resolve(map, args)));
        }

        private static async Task<JsonNode> RunRouteAsync(string[] args, BridgeClient bridge)
        {
            UiNavMap map = UiNavMap.Load(Read(args, "--map"));
            JsonObject route = Resolve(map, args);
            if (route["isFullyAutoRunnable"]?.GetValue<bool>() != true)
            {
                if (route["isNavigationRunnable"]?.GetValue<bool>() == true)
                {
                    JsonNode navigationResult = await bridge.CallUnityAsync("navigate_route", JsonUtil.Obj(
                        ("routeId", route["id"]?.DeepClone()),
                        ("targetViewId", route["toViewId"]?.DeepClone()),
                        ("navigationSteps", route["navigationSteps"]?.DeepClone() ?? new JsonArray())
                    ));
                    if (navigationResult != null)
                    {
                        navigationResult["route"] = JsonUtil.Obj(("path", map.Path), ("id", route["id"]?.DeepClone()), ("fromViewId", route["fromViewId"]?.DeepClone()), ("toViewId", route["toViewId"]?.DeepClone()), ("steps", route["steps"]?.DeepClone()));
                    }

                    return navigationResult;
                }

                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "route_not_runnable"),
                    ("message", "Route contains manual or unsupported transitions. Use route/resolve_ui_route to inspect navigationSteps."),
                    ("route", route)
                );
            }

            JsonNode result = await bridge.CallUnityAsync("run_sequence", JsonUtil.Obj(("actions", route["autoRunSequence"]?.DeepClone())));
            if (result != null)
            {
                result["route"] = JsonUtil.Obj(("path", map.Path), ("id", route["id"]?.DeepClone()), ("fromViewId", route["fromViewId"]?.DeepClone()), ("toViewId", route["toViewId"]?.DeepClone()));
            }
            return result;
        }

        private static async Task<JsonNode> NavigateUiAsync(string[] args, BridgeClient bridge)
        {
            UiNavMap map = UiNavMap.Load(Read(args, "--map"));
            JsonArray openViews = await ListOpenViewsAsync(bridge);
            JsonObject route = map.ResolveBestRoute(Read(args, "--from"), Read(args, "--to"), openViews.Select(item => item?.GetValue<string>()));
            JsonNode result = await bridge.CallUnityAsync("navigate_route", JsonUtil.Obj(
                ("routeId", route["id"]?.DeepClone()),
                ("targetViewId", route["toViewId"]?.DeepClone()),
                ("navigationSteps", route["navigationSteps"]?.DeepClone() ?? new JsonArray())
            ));
            if (result != null)
            {
                result["route"] = JsonUtil.Obj(
                    ("path", map.Path),
                    ("id", route["id"]?.DeepClone()),
                    ("fromViewId", route["fromViewId"]?.DeepClone()),
                    ("toViewId", route["toViewId"]?.DeepClone()),
                    ("steps", route["steps"]?.DeepClone())
                );
            }

            return result;
        }

        private static async Task<JsonArray> ListOpenViewsAsync(BridgeClient bridge)
        {
            JsonNode result = await bridge.CallUnityAsync("list_open_views");
            return result?["data"]?["openViews"]?.AsArray() ?? new JsonArray();
        }

        private static JsonObject Resolve(UiNavMap map, string[] args)
        {
            return map.ResolveRoute(Read(args, "--route"), Read(args, "--from"), Read(args, "--to"));
        }

        private static string Read(string[] args, string option, string fallback = null)
        {
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private static int ReadInt(string[] args, string option, int fallback)
        {
            int index = Array.IndexOf(args, option);
            if (index < 0 || index + 1 >= args.Length)
            {
                return fallback;
            }

            int value;
            return int.TryParse(args[index + 1], out value) ? value : fallback;
        }

        private static bool ReadBool(string[] args, string option, bool fallback)
        {
            int index = Array.IndexOf(args, option);
            if (index < 0)
            {
                return fallback;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return true;
            }

            bool value;
            return bool.TryParse(args[index + 1], out value) ? value : fallback;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Unity AutoRun MCP\n\n"
                + "Usage:\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- help\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- status\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- play\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- stop\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- list-buttons [--framework ugui|fairygui|all]\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- click --name ButtonName [--text Text] [--framework ugui|fairygui]\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- run-sequence --json-file sequence.json\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- nav-guidance\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- scan-nav-sources [--map mcp/ui-nav-map.json]\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- backfill-nav-map [--map mcp/ui-nav-map.json] [--preview true|false]\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- routes --map mcp/ui-nav-map.example.json\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- route --map mcp/ui-nav-map.example.json --from A --to C\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- run-route --map mcp/ui-nav-map.example.json --from A --to C\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- navigate-ui --map mcp/ui-nav-map.json --to TargetView [--from StartView]\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- mcp\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- mock-bridge");
        }
    }
}


