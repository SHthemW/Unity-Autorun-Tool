using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public sealed partial class McpServer
    {
        private readonly BridgeClient _bridge = new BridgeClient();
        private string _buffer = "";

        public async Task RunAsync()
        {
            using (var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8))
            {
                char[] chunk = new char[4096];
                int read;
                while ((read = await reader.ReadAsync(chunk, 0, chunk.Length)) > 0)
                {
                    _buffer += new string(chunk, 0, read);
                    await ReadMessagesAsync();
                }
            }
        }

        private async Task ReadMessagesAsync()
        {
            while (_buffer.Length > 0)
            {
                if (_buffer.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int headerEnd = _buffer.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEnd < 0)
                    {
                        return;
                    }

                    string header = _buffer.Substring(0, headerEnd);
                    int length = int.Parse(header.Split(':')[1].Trim());
                    int bodyStart = headerEnd + 4;
                    int bodyEnd = bodyStart + length;
                    if (_buffer.Length < bodyEnd)
                    {
                        return;
                    }

                    await HandleMessageAsync(_buffer.Substring(bodyStart, length));
                    _buffer = _buffer.Substring(bodyEnd);
                    continue;
                }

                int newline = _buffer.IndexOf('\n');
                if (newline < 0)
                {
                    return;
                }

                string line = _buffer.Substring(0, newline).Trim();
                _buffer = _buffer.Substring(newline + 1);
                if (line.Length > 0)
                {
                    await HandleMessageAsync(line);
                }
            }
        }

        private async Task HandleMessageAsync(string line)
        {
            JsonObject request;
            try
            {
                request = JsonNode.Parse(line)?.AsObject();
            }
            catch (Exception ex)
            {
                WriteError(null, -32700, ex.Message);
                return;
            }

            try
            {
                string method = request?["method"]?.GetValue<string>();
                JsonNode id = request?["id"]?.DeepClone();
                if (method == "initialize")
                {
                    WriteResult(id, JsonUtil.Obj(
                        ("protocolVersion", "2024-11-05"),
                        ("capabilities", JsonUtil.Obj(("tools", new JsonObject()))),
                        ("serverInfo", JsonUtil.Obj(("name", "unity-autorun"), ("version", UiNavMapMetadata.GeneratorVersion))),
                        ("instructions", "For requests to start Unity and open a UI view, prefer start_ui_navigation followed by get_ui_navigation_status. Bridge tools resolve the dynamically published endpoint automatically; get_unity_bridge_port is diagnostic only.")
                    ));
                }
                else if (method == "notifications/initialized")
                {
                    return;
                }
                else if (method == "tools/list")
                {
                    WriteResult(id, JsonUtil.Obj(("tools", McpTools.All())));
                }
                else if (method == "tools/call")
                {
                    WriteResult(id, await CallToolAsync(request?["params"]?.AsObject() ?? new JsonObject()));
                }
                else
                {
                    WriteError(id, -32601, $"Method not found: {method}");
                }
            }
            catch (Exception ex)
            {
                WriteError(request?["id"]?.DeepClone(), -32000, ex.Message);
            }
        }

        private async Task<JsonObject> CallToolAsync(JsonObject parameters)
        {
            string name = parameters["name"]?.GetValue<string>() ?? "";
            JsonObject args = parameters["arguments"]?.AsObject() ?? new JsonObject();
            JsonNode result = await DispatchToolAsync(name, args);
            bool isError = result?["ok"]?.GetValue<bool>() == false;
            return JsonUtil.Obj(("isError", isError), ("content", new JsonArray { JsonUtil.Obj(("type", "text"), ("text", JsonUtil.Pretty(result))) }));
        }

        private async Task<JsonNode> DispatchToolAsync(string name, JsonObject args)
        {
            if (name == "get_unity_bridge_port") return _bridge.GetEndpointInfo();
            if (name == "unity_status") return await _bridge.GetStatusAsync();
            if (name == "unity_play") return await _bridge.CallUnityAsync("play");
            if (name == "unity_stop") return await _bridge.CallUnityAsync("stop");
            if (name == "list_buttons") return await _bridge.CallUnityAsync("list_buttons", JsonUtil.Obj(
                ("framework", Text(args, "framework", "all")),
                ("query", Text(args, "query")),
                ("exact", Bool(args, "exact", false)),
                ("limit", Int(args, "limit", 100)),
                ("namesOnly", Bool(args, "namesOnly", false))
            ));
            if (name == "is_ui_view_open") return await _bridge.CallUnityAsync("is_ui_view_open", JsonUtil.Obj(
                ("targetViewId", Text(args, "targetViewId")),
                ("query", Text(args, "query"))
            ));
            if (name == "click_button") return await _bridge.CallUnityAsync("click_button", JsonUtil.Obj(("name", Text(args, "name")), ("text", Text(args, "text")), ("framework", Text(args, "framework", "ugui"))));
            if (name == "run_sequence") return await _bridge.CallUnityAsync("run_sequence", JsonUtil.Obj(("actions", args["actions"]?.DeepClone() ?? new JsonArray())));
            if (name == "get_nav_map_guidance") return UiNavMapGuidance.Get();
            if (name == "get_current_ui_nav_map") return UiNavMapReader.GetCurrent(args);
            if (name == "save_ui_nav_map") return UiNavMapWriter.Save(args);
            if (name == "get_nav_map_summary") return UiNavMapPatchTools.GetSummary(args);
            if (name == "scan_ui_nav_sources") return UiNavMapSourceScanner.Scan(args);
            if (name == "trace_ui_navigation_calls") return UiNavMapSourceScanner.TraceNavigationCalls(args);
            if (name == "get_ui_nav_candidate_coverage") return UiNavMapSourceScanner.GetCandidateCoverage(args);
            if (name == "finalize_ui_nav_map_generation") return UiNavMapSourceScanner.FinalizeGeneration(args);
            if (name == "backfill_ui_nav_map_from_sources") return UiNavMapSourceScanner.Backfill(args);
            if (name == "query_nav_map_items") return UiNavMapPatchTools.QueryItems(args);
            if (name == "get_ui_nav_subgraph") return UiNavMapPatchTools.GetSubgraph(args);
            if (name == "validate_ui_nav_map_patch") return UiNavMapPatchTools.ValidatePatch(args);
            if (name == "merge_ui_nav_map_patch") return UiNavMapPatchTools.MergePatch(args);
            if (name == "list_ui_routes") return ListRoutes(args);
            if (name == "resolve_ui_route") return ResolveRoute(args);
            if (name == "run_ui_route") return await RunRouteAsync(args);
            if (name == "navigate_ui") return await NavigateUiAsync(args);
            if (name == "start_ui_navigation") return await StartUiNavigationAsync(args);
            if (name == "get_ui_navigation_status") return await GetUiNavigationStatusAsync(args);
            if (name == "cancel_ui_navigation") return await _bridge.CallUnityAsync("cancel_ui_navigation", JsonUtil.Obj(
                ("navigationId", Text(args, "navigationId"))
            ));
            throw new InvalidOperationException($"Unknown tool: {name}");
        }

        private static string Text(JsonObject args, string key, string fallback = null)
        {
            return args[key]?.GetValue<string>() ?? fallback;
        }

        private static bool Bool(JsonObject args, string key, bool fallback)
        {
            return args[key]?.GetValue<bool>() ?? fallback;
        }

        private static int Int(JsonObject args, string key, int fallback)
        {
            return args[key]?.GetValue<int>() ?? fallback;
        }

        private static void WriteResult(JsonNode id, JsonNode result)
        {
            Console.WriteLine(JsonUtil.Obj(("jsonrpc", "2.0"), ("id", id), ("result", result)).ToJsonString());
        }

        private static void WriteError(JsonNode id, int code, string message)
        {
            Console.WriteLine(JsonUtil.Obj(("jsonrpc", "2.0"), ("id", id), ("error", JsonUtil.Obj(("code", code), ("message", message)))).ToJsonString());
        }
    }
}

