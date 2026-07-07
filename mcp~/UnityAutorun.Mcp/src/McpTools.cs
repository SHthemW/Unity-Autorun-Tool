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
                Tool("get_nav_map_guidance", "Get the UI navigation map schema, generation rules, and prompt guidance for AI prefab/code analysis."),
                Tool("get_current_ui_nav_map", "Read the current canonical UI navigation map from Unity-Autorun-Tool/mcp/ui-nav-map.json."),
                Tool("save_ui_nav_map", "Save a generated UI navigation map to the canonical Unity-Autorun-Tool/mcp/ui-nav-map.json path. Use this instead of writing the file manually.", ("map", "object"), ("mapJson", "string")),
                Tool("get_nav_map_summary", "Return compact counts, missing references, isolated views, and high-degree views without returning the full nav map.", ("mapPath", "string")),
                Tool("scan_ui_nav_sources", "Scan Unity Assets for UIForm ids, prefabs, classes, OpenUIForm calls, procedure flows, and map coverage gaps. Use before generating large nav maps.", ("mapPath", "string"), ("kind", "string"), ("query", "string"), ("offset", "number"), ("limit", "number"), ("includeText", "boolean")),
                Tool("backfill_ui_nav_map_from_sources", "Generate and merge a source-derived baseline nav map from UIForm ids, prefabs, classes, OpenUIForm calls, procedure flows, and evidence backlog.", ("mapPath", "string"), ("previewOnly", "boolean"), ("includeEvidenceBacklog", "boolean")),
                Tool("query_nav_map_items", "Page through one nav map section by text query. Use section views, controls, transitions, routes, or unresolved.", ("mapPath", "string"), ("section", Enum("views", "controls", "transitions", "routes", "unresolved")), ("query", "string"), ("offset", "number"), ("limit", "number")),
                Tool("get_ui_nav_subgraph", "Return a local nav map subgraph around a view, from/to target, or query. Use this instead of reading a huge full map.", ("mapPath", "string"), ("viewId", "string"), ("from", "string"), ("to", "string"), ("query", "string"), ("depth", "number")),
                Tool("validate_ui_nav_map_patch", "Validate an incremental nav map patch against the current canonical map before merging.", ("mapPath", "string"), ("patch", "object"), ("patchJson", "string")),
                Tool("merge_ui_nav_map_patch", "Merge an incremental nav map patch into the canonical map. Creates a skeleton map when no map exists. Refuses conflicts unless allowConflicts=true.", ("mapPath", "string"), ("patch", "object"), ("patchJson", "string"), ("allowConflicts", "boolean")),
                Tool("list_ui_routes", "List routes in a UI navigation map file.", ("mapPath", "string")),
                Tool("resolve_ui_route", "Resolve a UI route from a navigation map without executing it.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
                Tool("run_ui_route", "Resolve a UI route and execute it through AutoRun. Click-only routes use run_sequence; click/wait navigation routes use navigate_route.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
                Tool("navigate_ui", "Navigate to a target UI view through the Unity bridge using the nav map. If from is omitted, active Unity views are used as route candidates.", ("mapPath", "string"), ("from", "string"), ("to", "string")),
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

