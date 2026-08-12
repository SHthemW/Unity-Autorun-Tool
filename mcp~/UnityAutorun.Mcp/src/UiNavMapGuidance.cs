using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapGuidance
    {
        private const string ContractVersion = "1.0";

        public static JsonObject Get()
        {
            string absoluteOutputPath = UiNavMapPaths.ResolveDefaultMapPath();
            return JsonUtil.Obj(
                ("ok", true),
                ("contractVersion", ContractVersion),
                ("schemaVersion", UiNavMapMetadata.SchemaVersion),
                ("generatorVersion", UiNavMapMetadata.GeneratorVersion),
                ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                ("canonicalPath", absoluteOutputPath),
                ("repoRelativeOutputPath", UiNavMapPaths.DefaultRelativePath),
                ("requiredTools", RequiredTools()),
                ("toolRoles", JsonUtil.Obj(
                    ("read", "get_current_ui_nav_map"),
                    ("summary", "get_nav_map_summary"),
                    ("scan", "scan_ui_nav_sources"),
                    ("trace", "trace_ui_navigation_calls"),
                    ("coverage", "get_ui_nav_candidate_coverage"),
                    ("backfill", "backfill_ui_nav_map_from_sources"),
                    ("query", "query_nav_map_items"),
                    ("subgraph", "get_ui_nav_subgraph"),
                    ("validatePatch", "validate_ui_nav_map_patch"),
                    ("mergePatch", "merge_ui_nav_map_patch"),
                    ("fullReplacement", "save_ui_nav_map"),
                    ("finalize", "finalize_ui_nav_map_generation"),
                    ("listRoutes", "list_ui_routes"),
                    ("resolveRoute", "resolve_ui_route")
                )),
                ("pathContract", JsonUtil.Obj(
                    ("canonicalPath", absoluteOutputPath),
                    ("repoRelativePath", UiNavMapPaths.DefaultRelativePath),
                    ("directFileWritesAllowed", false),
                    ("useCanonicalPathForMapTools", true)
                )),
                ("mapContract", JsonUtil.Obj(
                    ("requiredArrays", RequiredArrays()),
                    ("toolManagedFields", new JsonArray
                    {
                        "schemaVersion",
                        "generatorVersion",
                        "mapVersion",
                        "generatedAt",
                        "generation"
                    }),
                    ("candidateDecisionShape", CandidateDecisionShape()),
                    ("transitionShape", TransitionShape()),
                    ("autoRunActionShape", AutoRunActionShape())
                )),
                ("analysisContract", JsonUtil.Obj(
                    ("traceCandidatesAreConfirmedEdges", false),
                    ("externalDecisionRequired", true),
                    ("backfillInfersControlsTransitionsRoutesOrAutomation", false),
                    ("reachabilityIndependentFromAutomation", true)
                )),
                ("candidateReviewContract", JsonUtil.Obj(
                    ("coverageTool", "get_ui_nav_candidate_coverage"),
                    ("globalQueryMustBeEmpty", true),
                    ("restartOffsetAfterEachMerge", 0),
                    ("copyExactId", true),
                    ("copyExactCandidateVersion", true),
                    ("allowedOutcomes", new JsonArray
                    {
                        "transition",
                        "unresolved",
                        "ignored"
                    }),
                    ("strongTopologyRequiresMatchingTransitionUnlessContradicted", true),
                    ("strongNonTransitionRequiresEvidence", true),
                    ("insufficientContradictions", new JsonArray
                    {
                        "automation uncertainty",
                        "async work",
                        "branch preconditions",
                        "missing runtime confirmation"
                    })
                )),
                ("patchContract", JsonUtil.Obj(
                    ("readBeforeMutation", true),
                    ("validateTool", "validate_ui_nav_map_patch"),
                    ("mergeTool", "merge_ui_nav_map_patch"),
                    ("preferIncrementalSlices", true),
                    ("preserveUnrelatedValidEntries", true),
                    ("allowConflictsOnlyForConfirmedReplacement", true)
                )),
                ("completionContract", JsonUtil.Obj(
                    ("tool", "finalize_ui_nav_map_generation"),
                    ("globalCandidateCoverageRequired", true),
                    ("sameKnownViewNamesRequired", true),
                    ("successField", "completionGatePassed"),
                    ("successValue", true),
                    ("routeValidationTools", new JsonArray
                    {
                        "list_ui_routes",
                        "resolve_ui_route"
                    })
                ))
            );
        }

        private static JsonArray RequiredTools()
        {
            return new JsonArray
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
    }
}
