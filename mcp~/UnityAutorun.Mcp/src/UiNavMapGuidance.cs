using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapGuidance
    {
        private const string ContractVersion = "1.2";

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
                    ("reachabilityIndependentFromAutomation", true),
                    ("reusableControlDoesNotImplyEquivalentInstances", true),
                    ("repeatedControlImmediateEdgeIndependentFromDownstreamEligibility", true),
                    ("repeatedControlRecoveryOwnedByRuntime", true)
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
                    ("nestedReusableClickRequiresExplicitMatchPolicy", true),
                    ("repeatedMatchPolicyRequiresImmediateEdgeEvidence", true),
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
                ("automation", "Optional object. Use mode=click when AutoRun can click the immediate control; mode=wait when app flow drives the edge and automation should wait for the target view; mode=manual only when the immediate action itself requires unsupported human input."),
                ("manualReason", "Required inside automation when mode=manual is used for a nested reusable control. Cite a concrete limitation of automating the immediate action. A later route step being data-dependent is not a manual-action reason because repeated-control recovery is handled at runtime."),
                ("automationPreconditions", "Judge the immediate transition separately from later route eligibility. For repeated instances that share a handler and reliably open the same immediate target, use mode=click with matchPolicy=first-interactable. AutoRun owns visible-instance enumeration, scrolling, branch switching, dismiss-and-retry recovery, and later precondition search. Size downstream timeouts for that search; do not use mode=manual merely because a later control may be absent for some items."),
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
                ("matchPolicy", "Required explicitly for potentially repeated nested-prefab controls; otherwise unique is the default. Use first-interactable when source or prefab evidence shows that matching instances share the handler and reach the same immediate target. Use unique only with evidence that the complete runtime selector has exactly one match. Runtime navigation, not static map generation, searches repeated instances for later route eligibility."),
                ("matchPolicyEvidence", "Required non-empty string for either match policy on a potentially repeated nested-prefab control. For first-interactable, cite the shared handler and same immediate target using source, prefab, or runtime evidence. It does not need to prove which item satisfies later route steps or describe a project-specific dismiss path because AutoRun owns that recovery. For unique, cite evidence for exactly one runtime match. This field documents the external AI decision and is ignored by runtime selection."),
                ("asyncAvailability", "Controls created after network, data, or virtualized-list loading may use a transition automation timeout sized for the expected latency. Do not treat temporary absence or failure to infer a ScrollRect as evidence that the control can never appear.")
            );
        }
    }
}
