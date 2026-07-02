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
                    "get_nav_map_summary",
                    "scan_ui_nav_sources",
                    "backfill_ui_nav_map_from_sources",
                    "query_nav_map_items",
                    "get_ui_nav_subgraph",
                    "validate_ui_nav_map_patch",
                    "merge_ui_nav_map_patch",
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
                ("complianceRules", ComplianceRules()),
                ("workflow", Workflow()),
                ("requiredArrays", RequiredArrays()),
                ("transitionShape", TransitionShape()),
                ("autoRunActionShape", AutoRunActionShape()),
                ("generationRules", GenerationRules()),
                ("incrementalPatchRules", IncrementalPatchRules()),
                ("sourceCoverageRules", SourceCoverageRules()),
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
                "For large maps or first-time generation, do not generate or rewrite the full map in one pass.",
                "Guidance cannot technically force an external AI client to comply; compliance is only accepted when required tool calls and their results prove it.",
                "If an AI client skips get_nav_map_summary, scan_ui_nav_sources, or required backfill, treat its output as incomplete even if it claims completion.",
                "MUST call get_nav_map_summary and scan_ui_nav_sources before claiming the map is complete.",
                "MUST call backfill_ui_nav_map_from_sources when scan_ui_nav_sources reports UIFormId, prefab, or OpenUIForm targets missing from the map.",
                "After backfill, use query_nav_map_items or get_ui_nav_subgraph for the relevant slice.",
                "Do not stop at clickable prefab buttons. Include indirect Procedure, scene loading, event, DataNode, lifecycle, and OpenUIForm/OpenUIFormAsync chains as flow transitions.",
                "Build small patches by UI module, prefab folder, scene, or target view. Validate with validate_ui_nav_map_patch and persist with merge_ui_nav_map_patch.",
                "Do not write ui-nav-map.json directly with filesystem operations.",
                "Prefer merge_ui_nav_map_patch for incremental work. Use save_ui_nav_map only when intentionally replacing the complete map.",
                "Do not report completion while scan_ui_nav_sources coverage shows important missing entries.",
                "After saving, validate with list_ui_routes and resolve_ui_route."
            };
        }

        private static string WorkflowBriefPrompt(string path)
        {
            return "Before UI navigation work, call get_nav_map_guidance. "
                + "Read the existing map with get_current_ui_nav_map. "
                + "For large maps or first-time generation, MUST call get_nav_map_summary and scan_ui_nav_sources. "
                + "Guidance cannot force an external AI client to comply, so treat required tool results as the acceptance record. "
                + "If required tool calls are skipped, the map is incomplete regardless of the model's textual claim. "
                + "Use scan_ui_nav_sources coverage gaps to find UIFormId entries, prefabs, classes, and OpenUIForm/Procedure/LoadScene evidence missing from the map. "
                + "If any important coverage gap exists, MUST call backfill_ui_nav_map_from_sources before manual patching. "
                + "Then work in small slices with query_nav_map_items or get_ui_nav_subgraph. "
                + "Generate incremental patches by module, prefab folder, scene, or target view. "
                + "Validate each patch with validate_ui_nav_map_patch and persist it with merge_ui_nav_map_patch. "
                + "Do not claim completion while source coverage gaps remain unexplained in the map or unresolved. "
                + "Do not write ui-nav-map.json directly. "
                + "Use merge_ui_nav_map_patch for incremental work; use save_ui_nav_map only for a deliberate full replacement. The canonical path is: " + path + ". "
                + "Then validate with list_ui_routes and resolve_ui_route.";
        }

        private static JsonArray Workflow()
        {
            return new JsonArray
            {
                "Use this get_nav_map_guidance response as the workflow brief before starting UI navigation map work.",
                "Read Unity UI prefabs, scene roots, UI controller scripts, and surrounding application flow code.",
                "Call get_current_ui_nav_map before changing an existing navigation map and preserve valid entries.",
                "MUST call get_nav_map_summary before broad map work. If the map is missing, treat this as first-time generation and still build it incrementally.",
                "MUST call scan_ui_nav_sources before broad map work. Use its coverage.uiFormIdsMissingInMap, prefabsMissingInMap, and openTargetsMissingInMap as the backlog for generation.",
                "MUST provide tool-backed evidence of completion. A plain text statement from the model is not enough.",
                "MUST call backfill_ui_nav_map_from_sources when coverage gaps are non-empty and the user expects a comprehensive map.",
                "After backfill, call get_nav_map_summary and scan_ui_nav_sources again before doing manual refinement.",
                "For large or missing maps, split analysis by UI module, prefab folder, scene, or target view instead of producing one full JSON object.",
                "For application startup and scene flows, inspect Procedure classes, DataNodeDefinition.UIFormLoadScene, LoadScene calls, ChangeState calls, and OpenUIForm/OpenUIFormAsync calls. Model them as transitions with kind=flow, lifecycle, scene, or event and automation.mode=wait when there is no clickable control.",
                "Example: if code opens UIFormLogin, stores UIFormLoadScene, loads a scene, and then opens UIFormOperation, represent UIFormLogin -> UIFormLoadScene -> UIFormOperation as indirect flow transitions and an important route.",
                "Use query_nav_map_items to page through existing views, controls, transitions, routes, or unresolved entries.",
                "Use get_ui_nav_subgraph to load only the local graph around the target view or route.",
                "For each slice, identify views, controls, reachability transitions, routes, and unresolved links, then create a patch containing only those changes.",
                "Call validate_ui_nav_map_patch before persisting each patch. Resolve errors; warnings should be reviewed and either fixed or intentionally left in unresolved.",
                "Call merge_ui_nav_map_patch to persist each validated patch. It creates the canonical skeleton map when no map exists.",
                "Use save_ui_nav_map only when intentionally replacing the complete map. Do not write ui-nav-map.json with direct filesystem writes.",
                "Completion gate: before saying the nav map is complete, verify that source coverage gaps are empty or each remaining gap has an unresolved entry with source evidence and reason.",
                "Call resolve_ui_route to verify important paths.",
                "Call run_ui_route only when resolve_ui_route reports isFullyAutoRunnable=true, Unity bridge is running, and the user wants execution."
            };
        }

        private static JsonArray ComplianceRules()
        {
            return new JsonArray
            {
                "This guidance improves compliance but cannot guarantee that every external AI client will obey it.",
                "Completion is accepted only from machine-checkable tool evidence: get_nav_map_summary, scan_ui_nav_sources, validate_ui_nav_map_patch, merge_ui_nav_map_patch, and route validation results.",
                "If required tools are not called, the output must be considered incomplete even when the model says it is done.",
                "If scan_ui_nav_sources coverage reports missing UIFormId, prefab, or OpenUIForm targets, a comprehensive map is not complete until backfill and manual patches resolve them or unresolved entries record source evidence and reasons.",
                "For this project, a map around 1-2k lines is a failure signal unless scan_ui_nav_sources proves coverage is complete.",
                "For first-time generation, the expected workflow is scan -> backfill_ui_nav_map_from_sources previewOnly=true -> backfill merge -> rescan -> incremental manual patches.",
                "The model must report the final tool counts and remaining coverage gaps instead of only summarizing its reasoning."
            };
        }

        private static JsonArray OutputPathRules(string absoluteOutputPath)
        {
            return new JsonArray
            {
                "This get_nav_map_guidance response is the required workflow brief and canonical read/write workflow.",
                "For incremental generation or large maps, persist patches by calling merge_ui_nav_map_patch. The tool writes absoluteOutputPath exactly: " + absoluteOutputPath,
                "Use save_ui_nav_map only for deliberate full-map replacement.",
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
                "Call get_nav_map_summary and scan_ui_nav_sources before broad work or first-time generation.",
                "Reject self-certified completion. Require final get_nav_map_summary and scan_ui_nav_sources results before considering the map complete.",
                "Call backfill_ui_nav_map_from_sources if scan_ui_nav_sources reports missing UIFormId, prefab, or OpenUIForm targets and the goal is comprehensive generation.",
                "Do not consider generation complete while scan_ui_nav_sources reports important UIFormId, prefab, or OpenUIForm targets missing from the map.",
                "For large or first-time maps, call validate_ui_nav_map_patch and merge_ui_nav_map_patch for each slice.",
                "After merge_ui_nav_map_patch or save_ui_nav_map succeeds, call list_ui_routes with mapPath set to absoluteOutputPath.",
                "Call resolve_ui_route with from/to or route id for each important target.",
                "Check resolve_ui_route.isFullyAutoRunnable before execution.",
                "If Unity bridge is running and the route is fully auto-runnable, call run_ui_route to execute the path."
            };
        }

        private static JsonArray IncrementalPatchRules()
        {
            return new JsonArray
            {
                "A nav map patch is a JSON object with any of these arrays: views, controls, transitions, routes, unresolved.",
                "Each patch should cover one small slice: one UI module, prefab folder, scene, target view, or route family.",
                "For first-time generation, do not wait until the whole project is analyzed. Merge the first reliable slice; merge later slices incrementally.",
                "Do not include unchanged existing entries in a patch unless intentionally updating them.",
                "Patch items with the same id can replace existing items, but merge_ui_nav_map_patch refuses conflicts unless allowConflicts=true.",
                "Use allowConflicts=true only when the user or strong code evidence confirms the overwrite is intentional.",
                "If a relationship is uncertain, put it in unresolved rather than inventing a transition.",
                "After each merge, call get_nav_map_summary to check counts and obvious missing references on large maps."
            };
        }

        private static JsonArray SourceCoverageRules()
        {
            return new JsonArray
            {
                "Use scan_ui_nav_sources to build the source backlog before editing the map.",
                "Guidance cannot force an AI client to comply, so the model must use tool results as acceptance gates and must not self-certify without them.",
                "If the model skips source scanning or backfill while coverage gaps exist, the generated map is non-compliant.",
                "backfill_ui_nav_map_from_sources is the required machine fallback when a comprehensive map is requested and source coverage gaps exist.",
                "Treat UIFormId.cs as an authoritative list of possible UI views. Every UIFormId with a prefab or class should normally have a view entry.",
                "Treat OpenUIForm/OpenUIFormAsync calls as target-view evidence even when they are not inside a UI button handler.",
                "Treat Procedure, ChangeState, LoadScene, DataNodeDefinition.UIFormLoadScene, and EventArgs flows as indirect navigation evidence.",
                "For indirect evidence, create transitions with no controlId and automation.mode=wait; include source.path, source.line or source.symbol, and source.via when multiple calls form a chain.",
                "Generate explicit important routes for startup and critical flows, such as UIFormLogin -> UIFormLoadScene -> UIFormOperation, even when route steps are all app-driven.",
                "If scan_ui_nav_sources coverage still lists missing items after backfill and manual patches, add unresolved entries with source evidence instead of ignoring them.",
                "A map around 1-2k lines for this project is suspiciously incomplete unless scan_ui_nav_sources coverage proves otherwise."
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
                + "Call get_nav_map_summary and scan_ui_nav_sources before broad work. If the map is missing or large, generate it incrementally instead of producing one full JSON object. "
                + "Required tool results are the acceptance record; do not accept self-certified completion from the model. "
                + "Use scan_ui_nav_sources coverage gaps to drive the backlog until UIFormId entries, prefabs, classes, and OpenUIForm targets are represented. "
                + "If coverage gaps are non-empty and the goal is comprehensive generation, call backfill_ui_nav_map_from_sources with previewOnly=true, then call it again to merge before manual patching. "
                + "Analyze Unity UI prefabs, UI scripts, and surrounding application flow code in slices by module, prefab folder, scene, or target view. "
                + "Do not stop at direct button clicks; include Procedure, ChangeState, LoadScene, DataNode, EventArgs, lifecycle, and OpenUIForm/OpenUIFormAsync chains as indirect flow transitions. "
                + "For startup flows, generate important routes such as UIFormLogin -> UIFormLoadScene -> UIFormOperation when code evidence supports the chain. "
                + "Use query_nav_map_items and get_ui_nav_subgraph to inspect only relevant existing entries. "
                + "For each slice, create a nav map patch with views, controls, transitions, routes, and unresolved links. "
                + "Validate the patch with validate_ui_nav_map_patch, then persist it with merge_ui_nav_map_patch. "
                + "Do not write files manually. merge_ui_nav_map_patch writes the exact path: " + absoluteOutputPath + ". "
                + "Automatically infer project-specific UI open, close, routing, event, state, and scene APIs from code evidence. "
                + "Use transitions for both interaction-driven and app-flow-driven reachability. "
                + "After merge_ui_nav_map_patch succeeds, call list_ui_routes and resolve_ui_route to validate the target path. "
                + "Only call run_ui_route when resolve_ui_route reports isFullyAutoRunnable=true, Unity bridge is running, and execution is requested.";
        }
    }
}
