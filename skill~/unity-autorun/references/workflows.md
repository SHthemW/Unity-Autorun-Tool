# Unity Autorun Workflows

Use this reference to select a compact, reliable MCP workflow. Tool schemas returned by the MCP server remain the source of truth for arguments.

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
| Generate or repair a map | Navigation-map tools | Apply `get_nav_map_guidance` first and follow its live rules. |

## Start and open a target view

1. Call `start_ui_navigation` with `targetViewId` or `to` and normally leave `ensurePlayMode=true`.
2. Save the returned `navigationId`.
3. Call `get_ui_navigation_status` with that id.
4. Repeat only while `terminal=false`.
5. On success, optionally verify a user-visible value with `get_ui_state` or `wait_for_ui_state`.
6. On failure, report `navigationPhase`, `resultCode`, and `resultMessage`; inspect a local map subgraph only when the error points to route resolution.

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

1. Navigate to the target view with tracked navigation.
2. Verify the view or a stable landmark.
3. Perform the requested direct interaction.
4. Wait for the expected resulting value.
5. Report the selector, observed value, and assertion outcome.

## Generate a complete navigation map

The live `get_nav_map_guidance` response is mandatory and overrides this summary.

1. Call `get_nav_map_guidance` and apply its prompts and rules.
2. Call `get_current_ui_nav_map`; preserve valid existing entries.
3. Call `get_nav_map_summary` and `scan_ui_nav_sources` before broad analysis.
4. Use `trace_ui_navigation_calls` for deep button handlers and cross-component chains. Treat candidates as evidence, not confirmed edges.
5. When deterministic views are missing, preview `backfill_ui_nav_map_from_sources`; do not expect it to infer controls, transitions, routes, automation, or confidence.
6. Work in small slices using `query_nav_map_items` and `get_ui_nav_subgraph`.
7. Author one compact patch per module, prefab folder, scene, target view, or route family.
8. Call `validate_ui_nav_map_patch`, correct errors, then call `merge_ui_nav_map_patch`.
9. Call `get_ui_nav_candidate_coverage` without `query` as the authoritative backlog. Review every item, copy its exact `id` and `candidateVersion` into one decision, merge it, then request `offset=0` again until `remaining=0`.
10. Call `finalize_ui_nav_map_generation` with the same known view names used during analysis.
11. Require `completionGatePassed=true`, then validate important paths with `list_ui_routes` and `resolve_ui_route`.

Keep reachability separate from AutoRun executability. Async work, branch prerequisites, or missing exact click metadata may lower automation confidence without disproving a source-backed navigation edge.

## Repair or extend an existing map

1. Apply `get_nav_map_guidance` first.
2. Load only the affected map summary, items, or subgraph.
3. Re-scan or trace the relevant source slice.
4. Preserve unrelated valid map entries.
5. Validate and merge a focused patch rather than replacing the complete map.
6. Re-run global candidate coverage and finalization because source changes can invalidate prior candidate versions.

When a user reports a generated-map defect, improve the analysis evidence or generation rules that caused it. Do not hand-edit the generated navigation map as a one-off workaround.

## Diagnose failures

- Bridge unavailable: ask the user to start it from `Window > Auto Run MCP Bridge > Start`, then use `unity_status`.
- MCP binary stale: publish the MCP server from the Auto Run window and reconnect the AI client.
- View not mapped: inspect source coverage and the local map subgraph, then use the guidance-governed patch workflow.
- Route not runnable: inspect `resolve_ui_route` output for manual, unsupported, or unresolved transitions.
- Control ambiguous: narrow by framework, exact name or text, view scope, and hierarchy path evidence.
- UI assertion timed out: report the last observed property value and path before changing selectors or timeouts.
