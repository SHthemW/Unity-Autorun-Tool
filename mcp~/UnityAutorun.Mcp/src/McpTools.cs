using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class McpTools
    {
        public static JsonArray All()
        {
            return new JsonArray
            {
                Tool("unity_status", "Check the Unity AutoRun bridge status."),
                Tool("unity_play", "Request Unity Editor to enter Play Mode."),
                Tool("unity_stop", "Request Unity Editor to exit Play Mode."),
                Tool("list_buttons", "List current Unity UI buttons.", ("framework", Enum("ugui", "fairygui", "all"))),
                Tool("click_button", "Click a Unity UI button by name or text.", ("name", "string"), ("text", "string"), ("framework", Enum("ugui", "fairygui"))),
                Tool("run_sequence", "Run a sequence of AutoRun button actions.", ("actions", "array")),
                Tool("list_ui_routes", "List routes in a UI navigation map file.", ("mapPath", "string")),
                Tool("resolve_ui_route", "Resolve a UI route from a navigation map without executing it.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
                Tool("run_ui_route", "Resolve a UI route and execute it through AutoRun.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
            };
        }

        private static JsonObject Tool(string name, string description, params (string Name, object Schema)[] properties)
        {
            var props = new JsonObject();
            foreach ((string propName, object schema) in properties)
            {
                props[propName] = schema is JsonNode node ? node : JsonUtil.Obj(("type", schema.ToString()));
            }

            return JsonUtil.Obj(
                ("name", name),
                ("description", description),
                ("inputSchema", JsonUtil.Obj(("type", "object"), ("properties", props)))
            );
        }

        private static JsonObject Enum(params string[] values)
        {
            var array = new JsonArray();
            foreach (string value in values)
            {
                array.Add(value);
            }

            return JsonUtil.Obj(("type", "string"), ("enum", array));
        }
    }
}

