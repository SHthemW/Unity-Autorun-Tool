---
name: unity-autorun
description: Quickly assess whether a requested Unity UI task and its controls are compatible with Unity Autorun, then navigate, inspect uGUI or TextMeshPro state, self-test behavior, and review observable UI presentation when supported; otherwise report a brief incompatibility reason. Use for Unity Play Mode, target-view navigation, button interaction, UI value checks, asynchronous assertions, and UI behavior reviews. Use the explicit `$unity-autorun gen-nav` form to generate, update, repair, audit, or validate Gen/ui-nav-map.json.
---

# Unity Autorun

Use the `unity-autorun` MCP server as the primary interface for supported Unity UI work. Prefer its structured operations over manual editor interaction, broad log reading, or ad hoc filesystem edits.

## Select the mode

- If the first argument after `$unity-autorun` is `gen-nav`, enter navigation-map mode. Treat any following text as the requested generation or update scope.
- Otherwise enter runtime mode. Treat all text after `$unity-autorun` as the runtime goal.
- If map generation or maintenance is requested without `gen-nav`, ask the user to invoke `$unity-autorun gen-nav [scope]`; do not silently switch modes.
- `gen-nav` is a skill mode, not an MCP tool name.
- Read [references/workflows.md](references/workflows.md) for the compatibility matrix and exact multi-tool workflows.

## Runtime mode: decide quickly

Before doing expensive work, determine whether Autorun can produce the evidence and actions needed by the request. Use zero to three of the smallest targeted probes needed; do not load navigation-map guidance, scan broad source trees, or read the full map.

Autorun is compatible when the required parts of the request fit these capabilities:

- Tracked navigation to a currently open or mapped target through runnable uGUI or optional FairyGUI button routes.
- Direct uGUI or optional FairyGUI button clicks and known ordered button sequences.
- Runtime state inspection and assertions for uGUI `Text`, `InputField`, `Toggle`, `Slider`, `Dropdown`, `Scrollbar`, `ScrollRect`, `Button`, `Image`, and `RawImage`, plus optional `TMP_Text`, `TMP_InputField`, and `TMP_Dropdown`.
- Observable presentation checks based on active, enabled, visible, selectable, interactable, text/value, selected state, sprite/texture name, fill amount, and hierarchy path.

Treat these as incompatible unless another available tool explicitly covers them:

- FairyGUI value/state inspection, UI Toolkit controls, custom-rendered UI, and non-UI game state not surfaced through supported uGUI or TMP components.
- Arbitrary typing, dragging, hovering, gestures, or keyboard/gamepad input when the route cannot express the action as supported click/wait automation.
- Pixel-level appearance, layout, overlap, animation smoothness, color, typography, or screenshot review. UIState is structured state, not visual capture.
- A required target with no open view and no runnable mapped route.

If compatibility is uncertain, use bounded probes such as `unity_status`, `resolve_ui_route`, `is_ui_view_open`, a filtered `list_buttons`, or a narrow `get_ui_state`. When state inspection is required, navigation compatibility alone is insufficient: confirm supported controls with `get_ui_state` after the target opens. If the core request is incompatible or currently unavailable, stop and tell the user the shortest concrete reason. Suggest `$unity-autorun gen-nav` only when a missing or stale navigation map is the cause. If only part is unsupported, continue with the supported part and state the limitation.

## Runtime mode: act when compatible

Do not stop after describing a plan. Actively use the tools within the user's requested scope:

1. For a target view, call `start_ui_navigation` once and poll `get_ui_navigation_status` only until `terminal=true`. Use `navigate_ui` only when Unity is already playing and startup gates are unnecessary.
2. Verify the reached view with `is_ui_view_open` or a narrow landmark query.
3. Read relevant UI values with a bounded `get_ui_state` query.
4. Perform requested direct button interactions, then use `wait_for_ui_state` for asynchronous outcomes.
5. Review the observable presentation fields supported by UIState and report concrete values, paths, and failed assertions. Do not invent business expectations or claim pixel-level visual approval.

## Verify outcomes

Use the smallest suitable evidence: navigation terminal state, `is_ui_view_open`, `get_ui_state`, or `wait_for_ui_state`. Report the compatibility verdict, actions completed, observed values, relevant hierarchy paths, assertion results, and any review boundary.

Treat masked password or PIN values as unreadable unless the user explicitly authorizes `includeSensitive=true`. Prefer `hasValue` for credential-field self-tests.

If the MCP server, bridge, navigation map, or target control is unavailable, report the exact returned error and the smallest corrective action. Do not invent success or hide unresolved navigation evidence.

## `gen-nav` mode

For `$unity-autorun gen-nav [scope]`, generate or update the navigation map instead of running the runtime compatibility gate:

1. Call `get_nav_map_guidance` once to load the compact live contract.
2. Treat the returned versions, canonical path, shapes, analysis contract, candidate-review contract, patch contract, and completion contract as authoritative for the connected MCP version.
3. Read the current map, then use summary and source scanning to define the requested work slices while preserving unrelated valid entries.
4. Trace relevant call chains. Treat candidates as evidence, decide graph semantics externally, keep proven reachability separate from automation support, and explicitly review repeated-control match policy against the immediate edge. Leave repeated-item enumeration and data-dependent downstream eligibility recovery to the navigation runtime.
5. Create small patches, validate each patch, and merge it through MCP tools. `save_ui_nav_map` is creation-only and must never replace an existing map. Existing items use RFC 7396 merge semantics: omitted fields are preserved and explicit `null` removes a field. Never write `Gen/ui-nav-map.json` directly.
6. Query global candidate coverage without a filter. Reuse carried-forward decisions, review every returned backlog item, copy its exact `id` and `candidateVersion`, merge only those decisions, and restart at `offset=0` until `remaining=0`.
7. Finalize with the same known view names used during analysis and require `completionGatePassed=true`. Treat `writePerformed=false` as the expected idempotent result when the map is already current.
8. Validate important paths with route listing and resolution tools before reporting completion.
