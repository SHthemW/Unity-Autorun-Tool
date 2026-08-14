# Unity Autorun Workflows

Use this reference to select a compact, reliable MCP workflow. Tool schemas returned by the MCP server remain the source of truth for arguments.

## Contents

- [Mode routing](#mode-routing)
- [Fast compatibility decision](#fast-compatibility-decision)
- [Tool routing](#tool-routing)
- [Start and open a target view](#start-and-open-a-target-view)
- [Inspect and self-test runtime UI](#inspect-and-self-test-runtime-ui)
- [`gen-nav`: Generate a complete navigation map](#gen-nav-generate-a-complete-navigation-map)
- [`gen-nav`: Repair or extend an existing map](#gen-nav-repair-or-extend-an-existing-map)
- [Author repeated and asynchronous controls](#author-repeated-and-asynchronous-controls)
- [Diagnose failures](#diagnose-failures)

## Mode routing

| Invocation | Mode | Behavior |
| --- | --- | --- |
| `$unity-autorun <runtime goal>` | Runtime | Quickly judge compatibility, then navigate, inspect, interact, and self-test when supported. |
| `$unity-autorun` with a runtime request in surrounding text | Runtime | Treat the surrounding request as the goal. |
| `$unity-autorun gen-nav` | Navigation-map | Generate or comprehensively update the canonical map. |
| `$unity-autorun gen-nav <scope>` | Navigation-map | Generate, repair, audit, or update only the named scope while preserving unrelated valid data. |

Do not create another skill or look for an MCP tool named `gen-nav`. It is a routing argument for this skill.

If the user requests map generation or maintenance without the `gen-nav` argument, direct them to `$unity-autorun gen-nav [scope]` instead of silently entering navigation-map mode.

## Fast compatibility decision

Classify the runtime request as `compatible`, `partially compatible`, or `incompatible/currently unavailable` before starting a long workflow.

| Requested capability | Compatible evidence | Incompatible boundary |
| --- | --- | --- |
| Reach a target view | It is already open, or a mapped route resolves as navigation-runnable. | The target is not open and no runnable mapped route exists. |
| Click through UI | uGUI button automation, or FairyGUI buttons when the optional helper is installed. | Arbitrary typing, drag, hover, gesture, keyboard, gamepad, or unsupported custom action. |
| Read or assert values | Supported uGUI components or optional TMP components are present. | FairyGUI state, UI Toolkit, custom-rendered state, or non-UI game state. |
| Review UI presentation | Active, enabled, visible, selectable, interactable, text/value, selection, sprite/texture name, fill amount, and hierarchy path are sufficient. | Pixel layout, overlap, color, font, screenshot, or animation-quality judgment is required. |

Choose zero to three relevant probes; do not run every probe mechanically:

1. Call `unity_status` once when connectivity or Play Mode matters.
2. If the target may already be open, use `is_ui_view_open`.
3. If navigation is required, use `resolve_ui_route`; never read the full map merely to decide feasibility.
4. Use a filtered `list_buttons` query only when button framework or availability is the deciding factor.
5. If runtime UI is available and the framework is not already known to be unsupported, use one narrow `get_ui_state` query.
6. Do not call `get_nav_map_guidance`, scan sources, or mutate the map in runtime mode.

When the request needs values, assertions, or a structured presentation review, a runnable route proves only navigation compatibility. Confirm UIState compatibility after opening the target by finding the required uGUI or TMP controls with a narrow `get_ui_state` query.

For a compatible request, proceed without asking for another confirmation. For a partially compatible request, complete the supported portion and identify the omitted portion. For an incompatible or unavailable request, stop with one or two sentences such as `Unity Autorun cannot complete this because <specific boundary or returned error>.` Include the smallest corrective action only when one exists. A missing or stale route may be followed by `Run $unity-autorun gen-nav <target scope> first.`

## Tool routing

| Goal | Primary tools | Rule |
| --- | --- | --- |
| Check connectivity | `unity_status` | Use `get_unity_bridge_port` only when endpoint diagnostics are needed. |
| Start or stop Play Mode | `unity_play`, `unity_stop` | Change editor state only when the user requested it or it is required by an authorized navigation. |
| Start and open a view | `start_ui_navigation`, `get_ui_navigation_status` | Prefer this tracked flow across Play Mode and startup gates. |
| Navigate while already playing | `navigate_ui` | Use only when startup handling is unnecessary. |
| Check one view | `is_ui_view_open` | Pass the mapped view id when available. |
| Inspect values | `get_ui_state` | Filter by identity, scope, and component type; keep `limit` small. |
| Assert a value | `wait_for_ui_state` | Prefer one bounded asynchronous assertion over repeated snapshots. |
| Click a control | `click_button` | Supply the correct framework and the most stable known selector. |
| Run fixed button steps | `run_sequence` | Use only for a known ordered sequence, not dynamic navigation. |
| Generate or repair a map | `$unity-autorun gen-nav` and navigation-map tools | Request `get_nav_map_guidance`, then follow this workflow using its live version/path/schema contract. |

## Start and open a target view

1. Complete the fast compatibility decision.
2. Call `start_ui_navigation` with `targetViewId` or `to` and normally leave `ensurePlayMode=true`.
3. Save the returned `navigationId`.
4. Call `get_ui_navigation_status` with that id.
5. Repeat only while `terminal=false`.
6. On success, verify the target or a user-visible landmark, then perform the requested state review or assertion.
7. On failure, report `navigationPhase`, `resultCode`, and `resultMessage`; inspect a local map subgraph only when the error points to route resolution.

Do not replace this workflow with repeated `unity_play`, `list_buttons`, and `click_button` calls. The tracked navigation owns Play Mode transitions, startup gates, route resolution, and per-step waits.

## Inspect and self-test runtime UI

### Read current state

Call `get_ui_state` with a target-oriented query. Useful filters include:

- `query`: GameObject name, hierarchy path, component type, or current property value.
- `scope`: containing view or hierarchy path.
- `types`: such as `Text`, `TMP_Text`, `InputField`, `TMP_InputField`, `Toggle`, `Slider`, `Dropdown`, `Button`, or `Image`.
- `exact`: enable when the identifier is stable and unique.
- `limit`: keep small and increase only when `truncated=true` requires it.

Read the component's `primaryProperty` and its typed `{name, valueType, value}` properties. Use the full hierarchy `path` to disambiguate repeated names.

### Assert asynchronous state

Call `wait_for_ui_state` with a target `query`, a `property`, a comparison, and an expected string value. `property=value` selects the component's `primaryProperty`. Metadata properties `active`, `enabled`, `visible`, `selectable`, and `interactable` can also be asserted.

Use comparisons such as `exists`, `notExists`, `equals`, `contains`, `greaterThan`, or `lessThanOrEqual`. Set `timeoutMs` to the expected UI latency and normally keep `pollMs` near its default.

On timeout, use the returned last-observed elements and `uiStateActual` to explain the mismatch. Do not immediately dump every UI element.

### Suggested self-test sequence

1. Confirm runtime compatibility.
2. Navigate to the target view with tracked navigation when needed.
3. Verify the view or a stable landmark.
4. Read the relevant pre-action state.
5. Perform the requested direct interaction.
6. Wait for the expected resulting state.
7. Report the selector, before/after values, hierarchy path, and assertion outcome.

### Review observable presentation

Actively inspect the controls relevant to the user's review instead of returning only a navigation success:

- Confirm key elements are active and visible.
- Confirm controls expected to be usable are selectable and interactable.
- Inspect text, placeholders, selected options, toggle state, slider or scrollbar values, scroll position, sprite or texture names, and image fill amount when relevant.
- Treat empty or unexpected values as findings only when the user supplied an expectation or the intended invariant is unambiguous.
- State that pixel layout, overlap, colors, fonts, screenshots, and animation smoothness were not reviewed when those qualities matter to the request.

## `gen-nav`: Generate a complete navigation map

The compact live contract is mandatory for current version, path, shape, candidate-review, patch, and completion semantics. This skill supplies the stable workflow directly.

1. Call `get_nav_map_guidance` once to load the compact live contract.
2. Call `get_current_ui_nav_map`; preserve valid existing entries. Use `save_ui_nav_map` only when the canonical file does not exist.
3. Call `get_nav_map_summary` and `scan_ui_nav_sources` before broad analysis.
4. Use `trace_ui_navigation_calls` for deep button handlers and cross-component chains. Treat candidates as evidence, not confirmed edges.
5. When deterministic views are missing, preview `backfill_ui_nav_map_from_sources`; do not expect it to infer controls, transitions, routes, automation, or confidence.
6. Work in small slices using `query_nav_map_items` and `get_ui_nav_subgraph`.
7. Author one compact patch per module, prefab folder, scene, target view, or route family.
8. Call `validate_ui_nav_map_patch`, correct errors, then call `merge_ui_nav_map_patch`. Patches preserve omitted fields recursively, explicit `null` removes a field, and a semantic no-op returns `writePerformed=false` without changing map versions or timestamps.
9. Call `get_ui_nav_candidate_coverage` without `query` as the authoritative backlog. Keep automatically carried-forward decisions, review every returned item, copy its exact `id` and `candidateVersion` into one decision, merge only those decisions, then request `offset=0` again until `remaining=0`.
10. Call `finalize_ui_nav_map_generation` with the same known view names used during analysis. Repeating finalization on an already-current map must return `writePerformed=false`.
11. Require `completionGatePassed=true`, then validate important paths with `list_ui_routes` and `resolve_ui_route`.

Keep reachability separate from AutoRun executability. Async work, branch prerequisites, or missing exact click metadata may lower automation confidence without disproving a source-backed navigation edge.

For nested-prefab or virtualized-list controls, explicitly review `matchPolicy` against the immediate transition before merging the candidate decision. Use `first-interactable` when source or prefab evidence shows that the instances share the handler and reach the same immediate target. Hierarchy nesting alone is insufficient, but static generation does not need to identify which instance satisfies later route steps because the navigation runtime owns that search.

## `gen-nav`: Repair or extend an existing map

1. Request `get_nav_map_guidance` and apply its live contract fields.
2. Load only the affected map summary, items, or subgraph.
3. Re-scan or trace the relevant source slice.
4. Preserve unrelated valid map entries.
5. Validate and merge a focused patch rather than replacing the complete map. Do not call `save_ui_nav_map` when the map already exists, and do not resubmit unchanged existing entries.
6. Re-run global candidate coverage and finalization because source changes can invalidate prior candidate versions.

When a user reports a generated-map defect, improve the analysis evidence or generation rules that caused it. Do not hand-edit the generated navigation map as a one-off workaround.

## Author repeated and asynchronous controls

Treat control multiplicity, control availability, and route eligibility as separate questions:

1. A nested-prefab path proves only that multiple runtime instances are possible. Set `matchPolicy` explicitly for the candidate.
2. Use `unique` only with non-empty `matchPolicyEvidence` showing that the complete runtime selector has exactly one match in the source view.
3. Use `first-interactable` with non-empty `matchPolicyEvidence` citing source, prefab, or runtime evidence that matching instances share the handler and reach the same immediate target. This immediate-edge evidence is sufficient even when item data affects later route steps.
4. Let AutoRun enumerate visible matches, scroll virtualized lists, switch generic branches, dismiss an ineligible target, and retry when downstream eligibility varies. Prefer an explicit category or tab route step when a stable one is known, but do not require static identification of the eligible item or a project-specific dismiss path.
5. Size the downstream `automation.timeout` for expected network, data-binding, virtualized-list, and repeated-branch search latency. Temporary absence of a cell, or failure to infer its scroll container before cells exist, is not proof that the control is unsupported.
6. Keep the source-backed transition even when immediate automation is genuinely unavailable. Use `automation.mode=manual` only for a concrete unsupported limitation of the immediate action, record it in `automation.manualReason`, and never use later item eligibility alone as that reason.

Before finalization, resolve at least one important route containing each reviewed repeated control and inspect the returned navigation steps. Confirm that the repeated immediate edge is `click`, keeps the explicit `matchPolicy`, and that data-dependent downstream steps carry an adequate timeout.

## Diagnose failures

- Bridge unavailable: ask the user to start it from `Window > Auto Run MCP Bridge > Start`, then use `unity_status`.
- MCP binary stale: publish the MCP server from the Auto Run window and reconnect the AI client.
- View not mapped: in runtime mode, stop and suggest `$unity-autorun gen-nav <target scope>`; in `gen-nav` mode, inspect source coverage and the local map subgraph before a focused patch.
- Route not runnable: in runtime mode, report the manual, unsupported, or unresolved transition from `resolve_ui_route`; use the contract-governed repair workflow only in `gen-nav` mode.
- Control ambiguous: narrow by framework, exact name or text, view scope, and hierarchy path evidence.
- UI assertion timed out: report the last observed property value and path before changing selectors or timeouts.
