using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class McpTools
    {
        public static JsonArray All()
        {
            return new JsonArray
            {
                Tool("get_unity_bridge_port", "Get the current Unity bridge endpoint for diagnostics. Other bridge tools resolve this endpoint automatically."),
                Tool("unity_status", "Check the Unity AutoRun bridge status."),
                Tool("unity_play", "Request Unity Editor to enter Play Mode."),
                Tool("unity_stop", "Request Unity Editor to exit Play Mode."),
                Tool("list_buttons", "List current Unity UI buttons with optional server-side filtering and bounded output.", ("framework", Enum("ugui", "fairygui", "all")), ("query", "string"), ("exact", "boolean"), ("limit", "number"), ("namesOnly", "boolean")),
                Tool("is_ui_view_open", "Check one target UI view without returning the complete runtime hierarchy.", ("targetViewId", "string"), ("query", "string")),
                Tool("get_ui_state", "Read current runtime uGUI and optional TextMeshPro values. Returns active elements by default with stable hierarchy paths, typed property values, visibility, and interactability. Password fields are masked unless includeSensitive=true.", ("query", "string"), ("scope", "string"), ("exact", "boolean"), ("types", StringArray()), ("limit", "number"), ("includeInactive", "boolean"), ("includeSensitive", "boolean")),
                Tool("wait_for_ui_state", "Wait until a filtered runtime uGUI or TextMeshPro property satisfies a comparison. Use property=value for the element's primary property; active, enabled, visible, selectable, and interactable metadata can also be asserted. This is the preferred assertion tool for asynchronous UI self-tests.", ("query", "string"), ("scope", "string"), ("exact", "boolean"), ("types", StringArray()), ("property", "string"), ("comparison", Enum("exists", "notExists", "equals", "notEquals", "contains", "notContains", "startsWith", "endsWith", "greaterThan", "greaterThanOrEqual", "lessThan", "lessThanOrEqual", "isEmpty", "isNotEmpty")), ("expected", "string"), ("timeoutMs", "number"), ("pollMs", "number"), ("limit", "number"), ("includeInactive", "boolean"), ("includeSensitive", "boolean")),
                Tool("click_button", "Click a Unity UI button by name or text.", ("name", "string"), ("text", "string"), ("framework", Enum("ugui", "fairygui"))),
                Tool("run_sequence", "Run a sequence of AutoRun button actions.", ("actions", "array")),
                Tool("get_nav_map_guidance", "Get the current compact UI navigation-map version, path, schema, analysis, patch, and completion contract for the Unity Autorun skill."),
                Tool("get_current_ui_nav_map", "Return a compact summary of the canonical UI navigation map. Set full=true only when the complete map is explicitly required.", ("full", "boolean")),
                Tool("save_ui_nav_map", "Create the canonical Unity project navigation map under ProjectSettings when it does not exist. Full replacement of an existing map is refused; read it and use incremental patch tools instead.", ("map", "object"), ("mapJson", "string")),
                Tool("get_nav_map_summary", "Return compact counts, semantic hash, missing references, isolated views, and high-degree views without returning the full nav map.", ("mapPath", "string")),
                Tool("scan_ui_nav_sources", "Scan Unity Assets for UI/view ids, prefabs, classes, source references, coverage gaps, and the count of traceable button call-chain candidates.", ("mapPath", "string"), ("kind", "string"), ("query", "string"), ("offset", "number"), ("limit", "number"), ("includeText", "boolean"), ("knownViewNames", StringArray())),
                Tool("trace_ui_navigation_calls", "Trace button handlers through a bounded local and cross-type call graph. Returns evidence-only candidates with decision hints, mapped endpoints, and serialized control evidence when resolvable; external AI must decide and submit a patch.", ("mapPath", "string"), ("query", "string"), ("offset", "number"), ("limit", "number"), ("knownViewNames", StringArray())),
                Tool("get_ui_nav_candidate_coverage", "Return navigation candidates whose decisions are missing, outdated, or semantically inconsistent with strong topology evidence. Review and merge every page before finalizing generation.", ("mapPath", "string"), ("query", "string"), ("offset", "number"), ("limit", "number"), ("knownViewNames", StringArray())),
                Tool("finalize_ui_nav_map_generation", "Fail unless every current navigation candidate has a current and semantically consistent transition, unresolved, or ignored decision. Already-current maps are returned without rewriting timestamps or file bytes.", ("mapPath", "string"), ("limit", "number"), ("knownViewNames", StringArray())),
                Tool("backfill_ui_nav_map_from_sources", "Preview or merge deterministic source-discovered views and unresolved evidence. This tool never infers controls, transitions, routes, automation, or confidence.", ("mapPath", "string"), ("previewOnly", "boolean"), ("includeEvidenceBacklog", "boolean")),
                Tool("query_nav_map_items", "Page through one nav map section by text query. Use section views, controls, transitions, routes, unresolved, or candidateDecisions.", ("mapPath", "string"), ("section", Enum("views", "controls", "transitions", "routes", "unresolved", "candidateDecisions")), ("query", "string"), ("offset", "number"), ("limit", "number")),
                Tool("get_ui_nav_subgraph", "Return a local nav map subgraph around a view, from/to target, or query. Use this instead of reading a huge full map.", ("mapPath", "string"), ("viewId", "string"), ("from", "string"), ("to", "string"), ("query", "string"), ("depth", "number")),
                Tool("validate_ui_nav_map_patch", "Validate an incremental nav map patch against the current canonical map before merging.", ("mapPath", "string"), ("patch", "object"), ("patchJson", "string")),
                Tool("merge_ui_nav_map_patch", "Merge an incremental RFC 7396-style nav map patch. Unspecified fields are preserved, explicit null removes a field, semantic no-ops do not rewrite the file, and conflicts are refused unless allowConflicts=true.", ("mapPath", "string"), ("patch", "object"), ("patchJson", "string"), ("allowConflicts", "boolean")),
                Tool("list_ui_routes", "List routes in a UI navigation map file.", ("mapPath", "string")),
                Tool("resolve_ui_route", "Resolve a UI route from a navigation map without executing it.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
                Tool("run_ui_route", "Resolve and execute a UI route with per-step target-view waits.", ("mapPath", "string"), ("route", "string"), ("from", "string"), ("to", "string")),
                Tool("navigate_ui", "Navigate from currently active mapped views while Unity is already in Play Mode. Prefer start_ui_navigation when Play Mode or startup/login gates may be required.", ("mapPath", "string"), ("from", "string"), ("to", "string")),
                Tool("start_ui_navigation", "Primary tool for requests such as start the game and open a UI view. Starts a tracked navigation that survives Play Mode transitions and returns immediately.", ("targetViewId", "string"), ("to", "string"), ("navigationId", "string"), ("ensurePlayMode", "boolean")),
                Tool("get_ui_navigation_status", "Wait for progress on a tracked UI navigation and return compact status. Call again only when terminal=false.", ("navigationId", "string"), ("waitMs", "number"), ("pollMs", "number")),
                Tool("cancel_ui_navigation", "Cancel a tracked UI navigation.", ("navigationId", "string")),
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

        private static JsonObject StringArray()
        {
            return JsonUtil.Obj(
                ("type", "array"),
                ("items", JsonUtil.Obj(("type", "string")))
            );
        }
    }
}

