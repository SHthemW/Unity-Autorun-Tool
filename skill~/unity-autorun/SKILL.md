---
name: unity-autorun
description: Operate, navigate, inspect, and self-test Unity UI through the Unity Autorun MCP server, and analyze, generate, repair, or validate UI navigation maps. Use when a user asks to start or stop Unity Play Mode, open a target UI view, click Unity UI controls, read uGUI or TextMeshPro values, verify an asynchronous UI state, test a UI flow, diagnose the Unity bridge, or build and maintain Gen/ui-nav-map.json with Unity Autorun.
---

# Unity Autorun

Use the `unity-autorun` MCP server as the primary interface for Unity UI work. Prefer its structured operations over manual editor interaction, broad log reading, or ad hoc filesystem edits.

## Route the request

1. Classify the task as runtime navigation, direct interaction, UI inspection/assertion, navigation-map work, or diagnostics.
2. Read [references/workflows.md](references/workflows.md) when selecting a multi-tool sequence, generating a navigation map, or handling a failure.
3. Use bounded queries and return only evidence relevant to the user's requested target.

## Apply the guidance gate

For any request that creates, repairs, audits, extends, or validates a UI navigation map:

1. Call `get_nav_map_guidance` before map analysis or mutation.
2. Apply the returned `workflowBriefPrompt` and `promptTemplate` as task instructions.
3. Follow the returned workflow, output-path, generation, patch, and validation rules. Treat the live response as authoritative when it differs from this skill.
4. Read the existing map with `get_current_ui_nav_map` before changing it.
5. Never write `Gen/ui-nav-map.json` directly. Validate and merge incremental patches through the MCP tools.
6. Never report map generation complete until `finalize_ui_nav_map_generation` returns `completionGatePassed=true`.

For runtime-only navigation or self-tests, do not load the large navigation-generation guidance response. Use the runtime workflows below so simple operations stay fast and token-efficient.

## Prefer high-level runtime tools

- To start the game and open a view, call `start_ui_navigation` once, then call `get_ui_navigation_status` until `terminal=true`.
- Use `navigate_ui` only when Unity is already in Play Mode and startup or login gates are not needed.
- Use `resolve_ui_route` before `run_ui_route` when explicitly running a named or computed route.
- Use `get_ui_state` with `query`, `scope`, `types`, and a small `limit` to inspect current uGUI or TextMeshPro values.
- Use `wait_for_ui_state` for asynchronous assertions instead of repeatedly polling broad UI snapshots.
- Use `click_button` for one direct interaction and `run_sequence` only for an intentionally ordered button sequence.
- Use `is_ui_view_open` for one target view; do not enumerate an entire runtime hierarchy to answer a yes/no question.
- Use `get_unity_bridge_port` only for endpoint diagnostics. Other bridge tools resolve the endpoint automatically.

## Verify outcomes

After navigation or interaction, verify the requested result with the smallest suitable tool: navigation terminal state, `is_ui_view_open`, `get_ui_state`, or `wait_for_ui_state`. Report the observed value and relevant hierarchy path when available.

Treat masked password or PIN values as unreadable unless the user explicitly authorizes `includeSensitive=true`. Prefer `hasValue` for credential-field self-tests.

If the MCP server, bridge, navigation map, or target control is unavailable, report the exact returned error and the smallest corrective action. Do not invent success or hide unresolved navigation evidence.
