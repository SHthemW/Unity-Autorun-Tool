using System;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapGuidance
    {
        public static JsonObject Get()
        {
            return JsonUtil.Obj(
                ("ok", true),
                ("schemaVersion", "1.0"),
                ("defaultOutputPath", "mcp/ui-nav-map.json"),
                ("workflow", Workflow()),
                ("requiredArrays", RequiredArrays()),
                ("autoRunActionShape", AutoRunActionShape()),
                ("generationRules", GenerationRules()),
                ("validationSteps", ValidationSteps()),
                ("example", Example()),
                ("promptTemplate", PromptTemplate())
            );
        }

        private static JsonArray Workflow()
        {
            return new JsonArray
            {
                "Read Unity UI prefabs, scene roots, and UI controller scripts.",
                "Identify views, controls, transitions, and direct routes.",
                "Write the navigation map to mcp/ui-nav-map.json.",
                "Call resolve_ui_route to verify important paths.",
                "Call run_ui_route only after Unity bridge is running and the user wants execution."
            };
        }

        private static JsonArray RequiredArrays()
        {
            return new JsonArray
            {
                "views: UI screens or panels that can be navigation targets.",
                "controls: clickable controls, usually buttons, with AutoRun action metadata.",
                "transitions: edges from one view to another through a control.",
                "routes: known or important multi-step paths; omitted routes can still be computed from transitions.",
                "unresolved: uncertain UI links or controls that need human review."
            };
        }

        private static JsonObject AutoRunActionShape()
        {
            return JsonUtil.Obj(
                ("buttonName", "Unity object name or FairyGUI component name."),
                ("buttonText", "Visible label text when known; use untitled when unavailable."),
                ("isFairyGUI", "true for FairyGUI controls, false for uGUI controls."),
                ("delay", "Seconds to wait after the click, usually 0.2 to 1.0.")
            );
        }

        private static JsonArray GenerationRules()
        {
            return new JsonArray
            {
                "Use stable lowercase dotted ids such as view.home, control.home.start, transition.home.start.to.shop.",
                "Prefer evidence from serialized prefab events, AddListener calls, FairyGUI callbacks, and UI router/window manager APIs.",
                "Set transition confidence from 0.0 to 1.0 and include source.type, source.path, and source.symbol when known.",
                "Do not invent a transition when the target view is unclear; put it in unresolved instead.",
                "For FairyGUI controls, set framework to fairygui and autoRun.isFairyGUI to true.",
                "For uGUI controls, set framework to ugui and autoRun.isFairyGUI to false.",
                "Every route step should reference transitionId and controlId.",
                "Keep generated JSON deterministic: sort views, controls, transitions, and routes by id."
            };
        }

        private static JsonArray ValidationSteps()
        {
            return new JsonArray
            {
                "Call get_nav_map_guidance before writing the file when available.",
                "After writing mcp/ui-nav-map.json, call list_ui_routes with mapPath.",
                "Call resolve_ui_route with from/to or route id for each important target.",
                "If Unity bridge is running, call run_ui_route to execute the path."
            };
        }

        private static JsonObject Example()
        {
            return JsonUtil.Obj(
                ("schemaVersion", "1.0"),
                ("generatedAt", DateTimeOffset.UtcNow.ToString("o")),
                ("project", JsonUtil.Obj(("unityProject", "example"), ("source", "prefab-and-code-analysis"))),
                ("views", new JsonArray
                {
                    JsonUtil.Obj(("id", "view.a"), ("name", "A"), ("prefabPath", "Assets/UI/A.prefab"), ("rootObjectPath", "Canvas/A"), ("framework", "ugui")),
                    JsonUtil.Obj(("id", "view.b"), ("name", "B"), ("prefabPath", "Assets/UI/B.prefab"), ("rootObjectPath", "Canvas/B"), ("framework", "ugui"))
                }),
                ("controls", new JsonArray
                {
                    JsonUtil.Obj(
                        ("id", "control.a.aa"),
                        ("viewId", "view.a"),
                        ("type", "button"),
                        ("name", "AA"),
                        ("text", "AA"),
                        ("objectPath", "Canvas/A/AA"),
                        ("framework", "ugui"),
                        ("autoRun", JsonUtil.Obj(("buttonName", "AA"), ("buttonText", "AA"), ("isFairyGUI", false), ("delay", 0.2)))
                    )
                }),
                ("transitions", new JsonArray
                {
                    JsonUtil.Obj(
                        ("id", "transition.a.aa.to.b"),
                        ("fromViewId", "view.a"),
                        ("controlId", "control.a.aa"),
                        ("toViewId", "view.b"),
                        ("kind", "button-click"),
                        ("confidence", 0.95),
                        ("source", JsonUtil.Obj(("type", "code"), ("path", "Assets/Scripts/UI/AController.cs"), ("symbol", "AController.OnAAClick")))
                    )
                }),
                ("routes", new JsonArray
                {
                    JsonUtil.Obj(
                        ("id", "route.a.to.b"),
                        ("fromViewId", "view.a"),
                        ("toViewId", "view.b"),
                        ("steps", new JsonArray { JsonUtil.Obj(("transitionId", "transition.a.aa.to.b"), ("controlId", "control.a.aa")) })
                    )
                }),
                ("unresolved", new JsonArray())
            );
        }

        private static string PromptTemplate()
        {
            return "Use the unity-autorun MCP tool get_nav_map_guidance first. "
                + "Then analyze Unity UI prefabs and related C# UI scripts to generate mcp/ui-nav-map.json. "
                + "Identify views, controls, transitions, routes, and unresolved links. "
                + "Use evidence from prefab events, AddListener calls, FairyGUI callbacks, and UI router APIs. "
                + "After writing the file, call list_ui_routes and resolve_ui_route to validate the target path. "
                + "If Unity bridge is running and execution is requested, call run_ui_route.";
        }
    }
}
