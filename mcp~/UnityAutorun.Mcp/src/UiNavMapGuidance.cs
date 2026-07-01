using System;
using System.IO;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapGuidance
    {
        public static JsonObject Get()
        {
            string absoluteOutputPath = UiNavMapPaths.ResolveDefaultMapPath();
            return JsonUtil.Obj(
                ("ok", true),
                ("schemaVersion", "1.0"),
                ("canonicalPath", absoluteOutputPath),
                ("requiredTools", new JsonArray
                {
                    "get_nav_map_guidance",
                    "get_current_ui_nav_map",
                    "save_ui_nav_map",
                    "list_ui_routes",
                    "resolve_ui_route"
                }),
                ("workflowBriefRules", WorkflowBriefRules()),
                ("workflowBriefPrompt", WorkflowBriefPrompt(absoluteOutputPath)),
                ("defaultOutputPath", absoluteOutputPath),
                ("repoRelativeOutputPath", UiNavMapPaths.DefaultRelativePath),
                ("absoluteOutputPath", absoluteOutputPath),
                ("outputDirectory", Path.GetDirectoryName(absoluteOutputPath)),
                ("requiredSaveTool", "save_ui_nav_map"),
                ("requiredReadTool", "get_current_ui_nav_map"),
                ("outputPathRules", OutputPathRules(absoluteOutputPath)),
                ("workflow", Workflow()),
                ("requiredArrays", RequiredArrays()),
                ("transitionShape", TransitionShape()),
                ("autoRunActionShape", AutoRunActionShape()),
                ("generationRules", GenerationRules()),
                ("validationSteps", ValidationSteps()),
                ("example", Example()),
                ("promptTemplate", PromptTemplate())
            );
        }

        private static JsonArray WorkflowBriefRules()
        {
            return new JsonArray
            {
                "Call get_nav_map_guidance before starting UI navigation map work.",
                "Use this response for the workflow brief, schema, and detailed generation rules.",
                "Call get_current_ui_nav_map before changing the map; preserve valid existing entries.",
                "Do not write ui-nav-map.json directly with filesystem operations.",
                "Persist the complete map only by calling save_ui_nav_map.",
                "After saving, validate with list_ui_routes and resolve_ui_route."
            };
        }

        private static string WorkflowBriefPrompt(string path)
        {
            return "Before UI navigation work, call get_nav_map_guidance. "
                + "Read the existing map with get_current_ui_nav_map. "
                + "Generate or update the complete navigation map object in memory. "
                + "Do not write ui-nav-map.json directly. "
                + "Persist only by calling save_ui_nav_map; it writes the canonical path: " + path + ". "
                + "Then validate with list_ui_routes and resolve_ui_route.";
        }

        private static JsonArray Workflow()
        {
            return new JsonArray
            {
                "Use this get_nav_map_guidance response as the workflow brief before starting UI navigation map work.",
                "Read Unity UI prefabs, scene roots, UI controller scripts, and surrounding application flow code.",
                "Call get_current_ui_nav_map before changing an existing navigation map and preserve valid entries.",
                "Identify views, controls, reachability transitions, and direct routes.",
                "Call save_ui_nav_map with the complete generated map. Do not write ui-nav-map.json with direct filesystem writes.",
                "Call resolve_ui_route to verify important paths.",
                "Call run_ui_route only when resolve_ui_route reports isFullyAutoRunnable=true, Unity bridge is running, and the user wants execution."
            };
        }

        private static JsonArray OutputPathRules(string absoluteOutputPath)
        {
            return new JsonArray
            {
                "This get_nav_map_guidance response is the required workflow brief and canonical read/write workflow.",
                "Always save the generated navigation map by calling save_ui_nav_map. The tool writes absoluteOutputPath exactly: " + absoluteOutputPath,
                "Do not create or edit ui-nav-map.json with direct filesystem writes. Do not write ui-nav-map.json to the Unity project root, the MCP project folder, the current shell directory, or a temporary working directory.",
                "When calling list_ui_routes, resolve_ui_route, run_ui_route, or navigate_ui, pass mapPath as absoluteOutputPath unless the user explicitly requests another file."
            };
        }

        private static JsonArray RequiredArrays()
        {
            return new JsonArray
            {
                "views: UI screens or panels that can be navigation targets.",
                "controls: clickable controls, usually buttons, with AutoRun action metadata.",
                "transitions: reachability edges from one view to another. Edges may be caused by user interaction, events, lifecycle callbacks, state machines, scene loading, timers, external SDK callbacks, or manually confirmed inference.",
                "routes: known or important multi-step paths; omitted routes can still be computed from transitions.",
                "unresolved: uncertain UI links or controls that need human review."
            };
        }

        private static JsonObject TransitionShape()
        {
            return JsonUtil.Obj(
                ("id", "Stable lowercase dotted id."),
                ("fromViewId", "Source view id."),
                ("toViewId", "Target view id."),
                ("kind", "interaction, flow, lifecycle, scene, external, timer, or inferred."),
                ("controlId", "Optional. Present only when a concrete UI control triggers the edge."),
                ("trigger", "Optional object describing what starts the edge, such as user-action, event, callback, state-change, scene-loaded, timer, or external-callback."),
                ("effect", "Optional object describing the observed result, usually open-view, close-view, replace-view, show-view, hide-view, or load-scene."),
                ("automation", "Optional object. Use mode=click when AutoRun can click; mode=wait when the edge is driven by app flow and automation should wait for the target view; mode=manual when human action is required."),
                ("confidence", "0.0 to 1.0 confidence based on code or asset evidence."),
                ("source", "Evidence object with source.type, source.path, source.symbol, and optional source.via chain.")
            );
        }

        private static JsonObject AutoRunActionShape()
        {
            return JsonUtil.Obj(
                ("buttonName", "Runtime-clickable Unity GameObject name or FairyGUI component name. For uGUI this must be the real prefab/scene object name, not a CodeBind field or C# property name."),
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
                "Prefer evidence from serialized prefab events, AddListener calls, FairyGUI callbacks, UI router/window manager APIs, event publish/subscribe flows, lifecycle callbacks, state-machine transitions, scene loading callbacks, timers, network callbacks, and external SDK callbacks.",
                "Infer project-specific UI open, close, routing, event, state, and scene APIs from the code being analyzed. Do not require or assume a manually supplied API allowlist.",
                "Use transitions for reachability, not only direct button clicks. A transition can connect two views when code evidence shows the app can move from the source view to the target view through application flow.",
                "Set transition.kind to interaction, flow, lifecycle, scene, external, timer, or inferred. Keep project-specific names in source.symbol or source.via rather than in kind.",
                "Set controlId only when a concrete control triggers the transition. Non-interaction transitions should omit controlId.",
                "For interaction transitions with a clickable control, include automation.mode=click or rely on the control autoRun metadata.",
                "For app-driven transitions, include automation.mode=wait and waitForViewId when automation should wait for the target view to appear.",
                "Use automation.mode=manual for transitions that require user input, platform auth, payment, or other actions AutoRun cannot perform.",
                "Set transition confidence from 0.0 to 1.0 and include source.type, source.path, and source.symbol when known.",
                "Do not invent a transition when the target view is unclear. Put uncertain links in unresolved unless a human has confirmed them, in which case use kind=inferred with source.type=human.",
                "For FairyGUI controls, set framework to fairygui and autoRun.isFairyGUI to true.",
                "For uGUI controls, set framework to ugui and autoRun.isFairyGUI to false.",
                "For CodeBind-backed uGUI controls, resolve the serialized field reference in the prefab and use the referenced component's GameObject name and hierarchy path. Do not use the CodeBind field/property name as control.name, objectPath, or autoRun.buttonName unless it is also the real GameObject name.",
                "For uGUI controls, objectPath must be the real prefab/scene hierarchy path and autoRun.buttonName must equal the final GameObject name in that path, because AutoRun clicks by Unity object name.",
                "Every route step should reference transitionId. Include controlId only when the transition has one.",
                "Keep generated JSON deterministic: sort views, controls, transitions, and routes by id."
            };
        }

        private static JsonArray ValidationSteps()
        {
            return new JsonArray
            {
                "Call get_nav_map_guidance before writing the file when available.",
                "Call get_current_ui_nav_map before generating changes to an existing map.",
                "After generating the map content, call save_ui_nav_map before route validation.",
                "After save_ui_nav_map succeeds, call list_ui_routes with mapPath set to absoluteOutputPath.",
                "Call resolve_ui_route with from/to or route id for each important target.",
                "Check resolve_ui_route.isFullyAutoRunnable before execution.",
                "If Unity bridge is running and the route is fully auto-runnable, call run_ui_route to execute the path."
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
                        ("kind", "interaction"),
                        ("trigger", JsonUtil.Obj(("type", "user-action"), ("action", "click"), ("controlId", "control.a.aa"))),
                        ("effect", JsonUtil.Obj(("type", "open-view"), ("targetViewId", "view.b"))),
                        ("automation", JsonUtil.Obj(("mode", "click"))),
                        ("confidence", 0.95),
                        ("source", JsonUtil.Obj(("type", "code"), ("path", "Assets/Scripts/UI/AController.cs"), ("symbol", "AController.OnAAClick")))
                    ),
                    JsonUtil.Obj(
                        ("id", "transition.b.flow.to.c"),
                        ("fromViewId", "view.b"),
                        ("toViewId", "view.c"),
                        ("kind", "flow"),
                        ("trigger", JsonUtil.Obj(("type", "event"), ("name", "DataReady"))),
                        ("effect", JsonUtil.Obj(("type", "open-view"), ("targetViewId", "view.c"))),
                        ("automation", JsonUtil.Obj(("mode", "wait"), ("waitForViewId", "view.c"), ("timeout", 10))),
                        ("confidence", 0.85),
                        ("source", JsonUtil.Obj(("type", "code-flow"), ("path", "Assets/Scripts/AppFlow.cs"), ("symbol", "OpenCWhenDataReady")))
                    )
                }),
                ("routes", new JsonArray
                {
                    JsonUtil.Obj(
                        ("id", "route.a.to.c"),
                        ("fromViewId", "view.a"),
                        ("toViewId", "view.c"),
                        ("steps", new JsonArray
                        {
                            JsonUtil.Obj(("transitionId", "transition.a.aa.to.b"), ("controlId", "control.a.aa")),
                            JsonUtil.Obj(("transitionId", "transition.b.flow.to.c"))
                        })
                    )
                }),
                ("unresolved", new JsonArray())
            );
        }

        private static string PromptTemplate()
        {
            string absoluteOutputPath = UiNavMapPaths.ResolveDefaultMapPath();
            return "Use the unity-autorun MCP tool get_nav_map_guidance first. "
                + "Read the current map with get_current_ui_nav_map before making changes. "
                + "Then analyze Unity UI prefabs, UI scripts, and surrounding application flow code to generate the navigation map content. "
                + "Save the result by calling save_ui_nav_map; do not write files manually. "
                + "save_ui_nav_map will write the exact path: " + absoluteOutputPath + ". "
                + "Identify views, controls, reachability transitions, routes, and unresolved links. "
                + "Automatically infer project-specific UI open, close, routing, event, state, and scene APIs from code evidence. "
                + "Use transitions for both interaction-driven and app-flow-driven reachability. "
                + "After save_ui_nav_map succeeds, call list_ui_routes and resolve_ui_route to validate the target path. "
                + "Only call run_ui_route when resolve_ui_route reports isFullyAutoRunnable=true, Unity bridge is running, and execution is requested.";
        }
    }
}
