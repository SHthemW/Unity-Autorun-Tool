using System;
using System.IO;
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
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "route_not_fully_autorunnable"),
                    ("message", "Route contains app-driven or manual transitions. Use route/resolve_ui_route and advance/wait for those steps outside AutoRun."),
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

        private static JsonObject Resolve(UiNavMap map, string[] args)
        {
            return map.ResolveRoute(Read(args, "--route"), Read(args, "--from"), Read(args, "--to"));
        }

        private static string Read(string[] args, string option, string fallback = null)
        {
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
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
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- routes --map mcp/ui-nav-map.example.json\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- route --map mcp/ui-nav-map.example.json --from A --to C\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- run-route --map mcp/ui-nav-map.example.json --from A --to C\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- mcp\n"
                + "  dotnet run --project mcp~/UnityAutorun.Mcp -- mock-bridge");
        }
    }
}


