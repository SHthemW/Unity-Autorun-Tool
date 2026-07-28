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
                ("schemaVersion", UiNavMapMetadata.SchemaVersion),
                ("generatorVersion", UiNavMapMetadata.GeneratorVersion),
                ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                ("canonicalPath", absoluteOutputPath),
                ("requiredTools", new JsonArray
                {
                    "get_nav_map_guidance",
                    "get_current_ui_nav_map",
                    "get_nav_map_summary",
                    "scan_ui_nav_sources",
                    "trace_ui_navigation_calls",
                    "get_ui_nav_candidate_coverage",
                    "finalize_ui_nav_map_generation",
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
                ("requiredReadTool", "get_current_ui_nav_map"),
                ("requiredPatchTool", "merge_ui_nav_map_patch"),
                ("requiredCoverageTool", "get_ui_nav_candidate_coverage"),
                ("requiredCompletionTool", "finalize_ui_nav_map_generation"),
                ("outputPathRules", OutputPathRules(absoluteOutputPath)),
                ("workflow", Workflow()),
                ("requiredArrays", RequiredArrays()),
                ("candidateDecisionShape", CandidateDecisionShape()),
                ("transitionShape", TransitionShape()),
                ("autoRunActionShape", AutoRunActionShape()),
                ("generationRules", GenerationRules()),
                ("incrementalPatchRules", IncrementalPatchRules()),
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
                "Call scan_ui_nav_sources before broad map work to get machine-checkable UI/view, prefab, class, source-reference, and button-binding coverage gaps.",
                "Call trace_ui_navigation_calls for deep button-handler chains across helper methods and component types. Its output is evidence only, not confirmed graph edges.",
                "For complete generation, call get_ui_nav_candidate_coverage with an empty query, review every returned candidate, copy id and candidateVersion into one candidateDecisions entry per item, and repeat with offset=0 until remaining=0.",
                "Use each candidate's decisionHint, mappedEndpoints, and serializedControls as compact review evidence. topologyStrength=strong should become a matching transition unless concrete contradictory evidence exists.",
                "Call get_current_ui_nav_map before changing the map and preserve valid existing entries.",
                "For large maps or first-time generation, do not generate or rewrite the full map in one pass.",
                "When scan_ui_nav_sources reports important missing views, call backfill_ui_nav_map_from_sources with previewOnly=true. Backfill adds only deterministic views and unresolved evidence; it never infers controls, transitions, routes, automation, or confidence.",
                "External AI must decide source view, referenced-view role, target view, transition kind, control metadata, automation, and confidence before creating a patch from trace evidence.",
                "Keep reachability separate from AutoRun executability. Missing exact click metadata, async work, branch preconditions, or missing runtime confirmation can lower automation/confidence but do not erase a code-proven navigation edge.",
                "Keep ui-nav-map.json compact and actionable: include only views, controls, transitions, routes, and concise unresolved navigation blockers needed for navigation.",
                "Do not copy broad source evidence, long code excerpts, or project-wide inventories into ui-nav-map.json.",
                "Do not stop at clickable prefab buttons. Include indirect state machine, scene loading, event, data context, lifecycle, and project-specific open/show/navigation API chains as flow transitions.",
                "Build small patches by UI module, prefab folder, scene, target view, or route family. Validate with validate_ui_nav_map_patch and persist with merge_ui_nav_map_patch.",
                "Do not write ui-nav-map.json directly with filesystem operations.",
                "Do not report generation complete until finalize_ui_nav_map_generation succeeds. It rejects unreviewed candidates and stamps the current schemaVersion, generatorVersion, mapVersion, and candidate set version.",
                "After saving, validate important paths with list_ui_routes and resolve_ui_route."
            };
        }

        private static string WorkflowBriefPrompt(string path)
        {
            return "Before UI navigation work, call get_nav_map_guidance. "
                + "Read the existing map with get_current_ui_nav_map. "
                + "For large maps or first-time generation, call get_nav_map_summary and scan_ui_nav_sources. "
                + "Use source coverage gaps to find missing UI/view, prefab, class, button-binding, and source-reference evidence. "
                + "Call trace_ui_navigation_calls for deep button-handler call chains, including cross-component calls. Treat every candidate as evidence requiring external AI review. "
                + "Use get_ui_nav_candidate_coverage with an empty query as the authoritative backlog. Review every item, copy id and candidateVersion into one candidateDecisions entry per candidate, and request the next unreviewed page with offset=0 until remaining is zero. "
                + "When decisionHint.topologyStrength is strong, create a transition whose endpoints match mappedEndpoints. Do not downgrade proven reachability merely because exact AutoRun metadata, async work, branch preconditions, or runtime confirmation are incomplete. "
                + "When important deterministic view gaps exist, call backfill_ui_nav_map_from_sources with previewOnly=true. It does not generate controls, transitions, routes, automation, or confidence. "
                + "Then work in small slices with query_nav_map_items or get_ui_nav_subgraph. "
                + "Generate incremental patches by module, prefab folder, scene, target view, or route family. "
                + "Keep ui-nav-map.json compact and actionable; do not copy broad source evidence into the map. "
                + "For startup routes, inspect and model login, auth, update, privacy, notice, tutorial, and loading gates before the main UI. "
                + "Validate each patch with validate_ui_nav_map_patch and persist it with merge_ui_nav_map_patch. "
                + "Do not write ui-nav-map.json directly. The canonical path is: " + path + ". "
                + "Call finalize_ui_nav_map_generation and require completionGatePassed=true before reporting completion. "
                + "Then validate important paths with list_ui_routes and resolve_ui_route.";
        }

        private static JsonArray Workflow()
        {
            return new JsonArray
            {
                "Use this get_nav_map_guidance response as the workflow brief before starting UI navigation map work.",
                "Read Unity UI prefabs, scene roots, UI controller scripts, and surrounding application flow code directly from the project.",
                "As the external AI, infer project-specific UI open, close, routing, event, state, and scene APIs from code evidence. Do not require or assume a manually supplied API allowlist.",
                "Call get_current_ui_nav_map before changing an existing navigation map and preserve valid entries.",
                "Call get_nav_map_summary before broad map work to understand current map size and obvious reference issues.",
                "Call scan_ui_nav_sources before broad map work. Treat coverage.uiFormIdsMissingInMap, prefabsMissingInMap, classesMissingInMap, viewClassesMissingInMap, openTargetsMissingInMap, and buttonBindingViewsMissingInMap as generation backlog.",
                "Call trace_ui_navigation_calls for deep button flows. Use knownViewNames when the project uses custom view names that source discovery cannot identify.",
                "Use get_ui_nav_candidate_coverage with query omitted as the authoritative global candidate backlog. A query-scoped result is useful for one module but cannot complete global generation.",
                "Do not convert a trace candidate directly into a transition. Decide whether the referenced view is opened, closed, queried, or unrelated, and resolve ambiguous source views from the supplied call chain and source locations.",
                "Use decisionHint as analyzer guidance rather than as a final decision. For topologyStrength=strong, author a matching transition unless source, asset, runtime, or explicit human evidence contradicts reachability.",
                "For each reviewed candidate, copy id and candidateVersion exactly, then merge one candidateDecisions item: outcome=transition with targetId, outcome=unresolved with targetId, or outcome=ignored with a concise reason.",
                "For a strong candidate that is not a transition, candidateDecisions must include nonTransitionEvidence.kind and nonTransitionEvidence.summary. Automation uncertainty, async work, branch preconditions, and absent runtime confirmation are not contradictory topology evidence.",
                "Call backfill_ui_nav_map_from_sources with previewOnly=true when deterministic source view gaps are important. Backfill intentionally returns empty controls, transitions, and routes; create those only in an external-AI-authored patch.",
                "For large or missing maps, split analysis by UI module, prefab folder, scene, target view, or route family instead of producing one full JSON object.",
                "For each slice, identify only actionable views, controls, reachability transitions, routes, and unresolved blockers needed for navigation.",
                "For application startup and scene flows, inspect state machine classes, data context keys, scene loading calls, state changes, lifecycle callbacks, events, and project-specific open/show/navigation calls.",
                "For startup flows, identify every blocking gate before the main UI, including login, auth provider selection, privacy or age gates, notices, preload/update screens, and tutorial gates. Do not model app start as reaching the main UI until these gates are represented or explicitly unresolved.",
                "Model app-driven steps as transitions with kind=flow, lifecycle, scene, event, external, or timer and automation.mode=wait when there is no clickable control.",
                "For critical flows, represent each proven step as a transition and add a route only when the ordered path is supported by code or runtime evidence.",
                "Use query_nav_map_items to page through existing views, controls, transitions, routes, or unresolved entries.",
                "Use get_ui_nav_subgraph to load only the local graph around the target view or route.",
                "Call validate_ui_nav_map_patch before persisting each patch. Resolve errors; warnings should be reviewed and either fixed or intentionally left in unresolved.",
                "Call merge_ui_nav_map_patch to persist each validated patch. It creates the canonical skeleton map when no map exists.",
                "Use save_ui_nav_map only when intentionally replacing the complete map. Do not write ui-nav-map.json with direct filesystem writes.",
                "After each candidate-decision patch, call get_ui_nav_candidate_coverage again with offset=0 so already reviewed candidates disappear from the backlog.",
                "Completion gate: call finalize_ui_nav_map_generation with the same knownViewNames used during analysis. Never say the map is complete unless completionGatePassed=true.",
                "After finalization, verify important target routes with resolve_ui_route.",
                "Call run_ui_route when resolve_ui_route reports isFullyAutoRunnable=true or isNavigationRunnable=true, Unity bridge is running, and the user wants execution."
            };
        }

        private static JsonArray OutputPathRules(string absoluteOutputPath)
        {
            return new JsonArray
            {
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
                "unresolved: concise uncertain UI links or navigation blockers that need human review.",
                "candidateDecisions: compact review ledger with exactly one outcome for every current trace candidate. This array is required for the completion gate but ignored by runtime navigation."
            };
        }

        private static JsonObject CandidateDecisionShape()
        {
            return JsonUtil.Obj(
                ("id", "The exact candidate.navigation.* id returned by get_ui_nav_candidate_coverage."),
                ("candidateVersion", "The exact candidateVersion returned with the candidate. A changed evidence version makes the old decision unreviewed."),
                ("outcome", "transition, unresolved, or ignored."),
                ("targetId", "Required for transition and unresolved outcomes. References the corresponding transition or unresolved id in the same or an earlier patch."),
                ("reason", "Required for ignored outcomes. Keep it concise and explain why the evidence does not create a navigation edge."),
                ("nonTransitionEvidence", "Required when decisionHint.topologyStrength=strong but outcome is unresolved or ignored. Object fields: kind=source-contradiction, asset-contradiction, runtime-contradiction, or human-confirmation; summary=the concrete contrary observation. Do not use missing automation metadata, async work, branch preconditions, or absent runtime confirmation.")
            );
        }

        private static JsonObject TransitionShape()
        {
            return JsonUtil.Obj(
                ("id", "Stable lowercase dotted id."),
                ("fromViewId", "Source view id."),
                ("toViewId", "Target view id."),
                ("kind", "interaction, flow, lifecycle, scene, external, timer, or inferred."),
                ("controlId", "Optional. Present only when a concrete control triggers the edge."),
                ("trigger", "Optional object describing what starts the edge, such as user-action, event, callback, state-change, scene-loaded, timer, or external-callback."),
                ("effect", "Optional object describing the observed result, usually open-view, close-view, replace-view, show-view, hide-view, or load-scene."),
                ("automation", "Optional object. Use mode=click when AutoRun can click; mode=wait when the edge is driven by app flow and automation should wait for the target view; mode=manual when human action is required."),
                ("confidence", "0.0 to 1.0 confidence based on code or asset evidence."),
                ("source", "Optional short evidence summary. Do not copy broad source text into the canonical map; candidate consumption is tracked separately in candidateDecisions.")
            );
        }

        private static JsonObject AutoRunActionShape()
        {
            return JsonUtil.Obj(
                ("buttonName", "Runtime-clickable Unity GameObject name or FairyGUI component name. For uGUI this must be the real prefab/scene object name, not a CodeBind field or C# property name."),
                ("buttonText", "Visible label text when known; use untitled when unavailable."),
                ("isFairyGUI", "true for FairyGUI controls, false for uGUI controls."),
                ("delay", "Seconds to wait after the click, usually 0.2 to 1.0."),
                ("objectPath", "Optional uGUI hierarchy suffix used to scope runtime matching. The route resolver inherits control.objectPath when omitted."),
                ("scopeRootName", "Optional uGUI source-view root used to exclude same-named buttons from other open views. The route resolver derives it from control.viewId when omitted."),
                ("matchPolicy", "unique by default. Use first-interactable for a reusable nested prefab control such as a visible list or grid item when any matching item can trigger the same navigation edge.")
            );
        }

        private static JsonArray GenerationRules()
        {
            return new JsonArray
            {
                "Use stable lowercase dotted ids such as view.home, control.home.start, transition.home.start.to.shop.",
                "Prefer evidence from serialized prefab events, AddListener calls, FairyGUI callbacks, UI router/window manager APIs, event publish/subscribe flows, lifecycle callbacks, state-machine transitions, scene loading callbacks, timers, network callbacks, and external SDK callbacks.",
                "Use transitions for reachability, not only direct button clicks. A transition can connect two views when code evidence shows the app can move from the source view to the target view through application flow.",
                "When a procedure, state machine, or startup class opens a UI form before gameplay, include that UI form as a view even if it is not a final navigation target.",
                "When a startup/login UI closes itself and fires an event or state change that leads to the main UI, model the clicked control as a transition to the next reachable view and set automation.waitForViewId plus a timeout that covers async login or preload.",
                "Do not create a direct app.start to main UI route when code shows an intermediate login, auth, update, privacy, notice, tutorial, or loading gate. Add the gate view and route through it, or add an unresolved blocker if the gate cannot be automated.",
                "Set transition.kind to interaction, flow, lifecycle, scene, external, timer, or inferred. Keep project-specific API names in source summaries rather than in kind.",
                "Set controlId only when a concrete control triggers the transition. Non-interaction transitions should omit controlId.",
                "For interaction transitions with a clickable control, include automation.mode=click or rely on the control autoRun metadata.",
                "For app-driven transitions, include automation.mode=wait and waitForViewId when automation should wait for the target view to appear.",
                "Use automation.mode=manual for transitions that require user input, platform auth, payment, or other actions AutoRun cannot perform.",
                "Set transition confidence from 0.0 to 1.0. Include only short source summaries when useful; keep detailed source evidence out of ui-nav-map.json.",
                "trace_ui_navigation_calls never assigns transition confidence or automation. The external AI must derive those fields from the call chain, prefab evidence, current map, and any required runtime evidence.",
                "Candidate decisionHint is compact analyzer guidance, not an executable edge. mappedEndpoints supplies existing view ids, referenceRoleHint classifies invocation-name evidence, and serializedControls supplies exact-prefab serialized field evidence when naming conventions allow it.",
                "Keep navigation topology separate from automation. When a single mapped source reaches a single mapped target through a direct or one-hop open/show/navigation invocation and analysis is not truncated, create the reachability transition even if the control must temporarily use automation.mode=manual or omit controlId.",
                "Do not classify a strong topology candidate as unresolved merely because the handler is async, has data or branch preconditions, lacks runtime confirmation, or lacks exact AutoRun button metadata. These affect trigger details, confidence, and executability rather than whether the edge exists.",
                "Do not use trace pagination alone as proof of completeness. get_ui_nav_candidate_coverage is authoritative because it removes candidates already recorded in candidateDecisions.",
                "Every candidate returned by get_ui_nav_candidate_coverage must receive one decision with the exact candidateVersion, including duplicate, close-only, queried, generic type-token, and unrelated references; use outcome=ignored with a reason when no graph item should be created.",
                "Do not invent a transition when the target view is unclear. Put uncertain links in unresolved unless a human has confirmed them, in which case use kind=inferred with source.type=human.",
                "For FairyGUI controls, set framework to fairygui and autoRun.isFairyGUI to true.",
                "For uGUI controls, set framework to ugui and autoRun.isFairyGUI to false.",
                "For CodeBind-backed uGUI controls, resolve the serialized field reference in the prefab and use the referenced component's GameObject name and hierarchy path. Do not use the CodeBind field/property name as control.name, objectPath, or autoRun.buttonName unless it is also the real GameObject name.",
                "When buttonBinding.serializedControls contains status=resolved, a transition decision must reference a control on the resolved source view whose name, objectPath leaf, or autoRun.buttonName matches the resolved objectName. Its evidence basis is reported explicitly and still requires external-AI judgment.",
                "For uGUI controls, objectPath must be the real prefab/scene hierarchy path and autoRun.buttonName must equal the final GameObject name in that path, because AutoRun clicks by Unity object name.",
                "When a control belongs to a reusable nested prefab rather than the source view root, preserve that prefab-relative objectPath and use autoRun.matchPolicy=first-interactable only when any active matching instance is a valid trigger. Runtime matching scopes candidates by source-view root and hierarchy suffix before selecting deterministically.",
                "For generated CodeBind files, treat properties such as LoginExButton only as hints. Confirm the serialized prefab object name, usually with separators such as Login_ExButton, before writing autoRun.buttonName.",
                "Every route step should reference transitionId. Include controlId only when the transition has one.",
                "Keep generated JSON deterministic: sort views, controls, transitions, routes, unresolved, and candidateDecisions by id.",
                "schemaVersion, generatorVersion, mapVersion, generatedAt, and generation are tool-managed. Do not invent or manually increment them in patches."
            };
        }

        private static JsonArray ValidationSteps()
        {
            return new JsonArray
            {
                "Call get_nav_map_guidance before writing the file when available.",
                "Call get_current_ui_nav_map before generating changes to an existing map.",
                "Call get_nav_map_summary and scan_ui_nav_sources before broad work or first-time generation.",
                "Call trace_ui_navigation_calls for important source views and targets, especially when a click reaches a view reference through helper methods or another component.",
                "Call get_ui_nav_candidate_coverage without a query, review all returned items, merge candidateDecisions, and repeat with offset=0 until remaining=0.",
                "Treat reviewStatus=semantic-review-required as unfinished work. Replace an endpoint-mismatched transition decision, or turn a strong topology candidate into a matching transition unless concrete nonTransitionEvidence exists.",
                "Do not consider broad generation complete while scan_ui_nav_sources reports important UI/view, prefab, class, button-binding, or source-reference coverage gaps that are neither mapped nor recorded in unresolved.",
                "Before merging a trace-derived edge, confirm its source view and referenced-view role; a call-chain reference alone is not proof that the view opens.",
                "For large or first-time maps, call validate_ui_nav_map_patch and merge_ui_nav_map_patch for each slice.",
                "Call finalize_ui_nav_map_generation and require completionGatePassed=true. candidate_review_incomplete and candidate_semantic_review_incomplete are failed completion gates, not warnings.",
                "After merge_ui_nav_map_patch or save_ui_nav_map succeeds, call list_ui_routes with mapPath set to absoluteOutputPath.",
                "Call resolve_ui_route with from/to or route id for each important target.",
                "For startup targets, resolve routes from app start and from each detected blocking gate view such as login/auth/update/loading to the target. Missing gate routes must be fixed or recorded in unresolved before execution.",
                "Check resolve_ui_route.isFullyAutoRunnable and resolve_ui_route.isNavigationRunnable before execution.",
                "If Unity bridge is running and the route is fully auto-runnable or navigation-runnable, call run_ui_route to execute the path."
            };
        }

        private static JsonArray IncrementalPatchRules()
        {
            return new JsonArray
            {
                "A nav map patch is a JSON object with any of these arrays: views, controls, transitions, routes, unresolved, candidateDecisions.",
                "Each patch should cover one small slice: one UI module, prefab folder, scene, target view, or route family.",
                "For first-time generation, do not wait until the whole project is analyzed. Merge the first reliable slice; merge later slices incrementally.",
                "Do not include unchanged existing entries in a patch unless intentionally updating them.",
                "Patch items with the same id can replace existing items, but merge_ui_nav_map_patch refuses conflicts unless allowConflicts=true.",
                "Use allowConflicts=true only when the user or strong code evidence confirms the overwrite is intentional.",
                "When coverage returns reviewStatus=outdated-decision, the new candidateVersion is strong evidence for intentionally replacing that candidateDecisions item after validation.",
                "If a relationship is uncertain, put it in unresolved rather than inventing a transition.",
                "Every decision must copy the candidateVersion from coverage output. A transition decision must reference a known transition targetId, an unresolved decision must reference a known unresolved targetId, and an ignored decision must include a concise reason.",
                "A transition decision must reference an edge whose fromViewId and toViewId match mappedEndpoints when those endpoints are resolved. A strong candidate may be unresolved or ignored only with concrete nonTransitionEvidence.",
                "After each merge, call get_nav_map_summary to check counts, version status, generation status, and obvious missing references on large maps."
            };
        }

        private static JsonObject Example()
        {
            return JsonUtil.Obj(
                ("schemaVersion", UiNavMapMetadata.SchemaVersion),
                ("generatorVersion", UiNavMapMetadata.GeneratorVersion),
                ("mapVersion", 1),
                ("generatedAt", DateTimeOffset.UtcNow.ToString("o")),
                ("generation", JsonUtil.Obj(
                    ("status", "complete"),
                    ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                    ("candidateSetVersion", "candidate-set." + UiNavMapMetadata.CandidateProtocolVersion + ".example"),
                    ("candidateCount", 2),
                    ("reviewedCandidateCount", 2),
                    ("completedAt", DateTimeOffset.UtcNow.ToString("o"))
                )),
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
                        ("source", JsonUtil.Obj(("type", "code"), ("summary", "AController opens B from AA click.")))
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
                        ("source", JsonUtil.Obj(("type", "code-flow"), ("summary", "App flow opens C after data is ready.")))
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
                ("unresolved", new JsonArray()),
                ("candidateDecisions", new JsonArray
                {
                    JsonUtil.Obj(
                        ("id", "candidate.navigation.example-a"),
                        ("candidateVersion", "candidate-evidence." + UiNavMapMetadata.CandidateProtocolVersion + ".example-a"),
                        ("outcome", "transition"),
                        ("targetId", "transition.a.aa.to.b")
                    ),
                    JsonUtil.Obj(
                        ("id", "candidate.navigation.example-b"),
                        ("candidateVersion", "candidate-evidence." + UiNavMapMetadata.CandidateProtocolVersion + ".example-b"),
                        ("outcome", "transition"),
                        ("targetId", "transition.b.flow.to.c")
                    )
                })
            );
        }

        private static string PromptTemplate()
        {
            string absoluteOutputPath = UiNavMapPaths.ResolveDefaultMapPath();
            return "Use the unity-autorun MCP tool get_nav_map_guidance first. "
                + "Read the current map with get_current_ui_nav_map before making changes. "
                + "Call get_nav_map_summary and scan_ui_nav_sources before broad work. If the map is missing or large, generate it incrementally instead of producing one full JSON object. "
                + "Use scan coverage gaps as the backlog, and call backfill_ui_nav_map_from_sources with previewOnly=true when deterministic source-backed views are missing. Backfill never creates controls, transitions, routes, automation, or confidence. "
                + "Call trace_ui_navigation_calls for deep button-handler and cross-component chains. Treat its items as evidence and make the graph decisions externally before authoring a patch. "
                + "Use get_ui_nav_candidate_coverage with no query as the authoritative backlog. For every returned item copy id and candidateVersion into exactly one candidateDecisions entry, then request offset=0 again until remaining=0. "
                + "Use decisionHint, mappedEndpoints, and serializedControls. Strong topology evidence must become a matching transition unless concrete source, asset, runtime, or user-confirmed evidence contradicts it; automation uncertainty, async work, branch preconditions, and absent runtime confirmation are not contradictions. "
                + "Analyze Unity UI prefabs, UI scripts, and surrounding application flow code in slices by module, prefab folder, scene, target view, or route family. "
                + "Do not stop at direct button clicks; include state changes, scene loading, data context, events, lifecycle, and project-specific open/show/navigation API chains as indirect flow transitions. "
                + "For startup flows, explicitly model login, auth, update, privacy, notice, tutorial, and loading gates instead of assuming app start reaches the main UI. "
                + "Use query_nav_map_items and get_ui_nav_subgraph to inspect only relevant existing entries. "
                + "For each slice, create a compact nav map patch with views, controls, transitions, routes, concise unresolved links, and candidate decisions. "
                + "Validate the patch with validate_ui_nav_map_patch, then persist it with merge_ui_nav_map_patch. "
                + "Do not write files manually. merge_ui_nav_map_patch writes the exact path: " + absoluteOutputPath + ". "
                + "Call finalize_ui_nav_map_generation and require completionGatePassed=true; it stamps schemaVersion, generatorVersion, mapVersion, and the reviewed candidate set. "
                + "After merge_ui_nav_map_patch succeeds, call list_ui_routes and resolve_ui_route to validate important target paths. "
                + "Only call run_ui_route when resolve_ui_route reports isFullyAutoRunnable=true or isNavigationRunnable=true, Unity bridge is running, and execution is requested.";
        }
    }
}
