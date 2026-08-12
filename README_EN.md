# Unity Autorun Tool

[中文（默认）](./README.md) | [English](./README_EN.md)

Unity Autorun Tool is a Unity Editor extension for automating UI startup flows, test navigation, and AI-assisted UI operation. It can click UGUI and FairyGUI buttons from editor presets, expose the same operations through a local HTTP bridge, and provide a .NET 8 MCP server so AI clients can inspect and drive Unity UI flows through structured tools.

## Quick Start

Open `Window > Auto Run Window` in Unity to see the real program interface shown below.

<p align="center">
  <img src="./Documentation~/images/auto-run-window.png" alt="Unity Auto Run program interface" width="33%">
</p>

The upper `MCP` area connects AI clients and reports the Bridge state. The lower `Navigation AutoRun` area searches, selects, and runs a target from the navigation map.

### Quick Installation

The recommended method is [Unity Package Manager with a Git URL](https://docs.unity3d.com/2021.3/Documentation/Manual/upm-ui-giturl.html). Make sure [Git](https://git-scm.com/downloads) is installed before using this method.

1. Open `Window > Package Manager` in Unity.
2. Press `+` in the upper-left corner and choose `Add package from git URL...`.
3. Enter the following Git URL, then press `Add`:

```text
https://github.com/SHthemW/Unity-Autorun-Tool.git
```

4. Wait for installation and script compilation to finish, then open `Window > Auto Run Window`.

For a pinned version or manual installation, see the [full installation instructions](#installation).

### Connect an AI client

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and open `Window > Auto Run Window`.
2. Press `Publish MCP` in the upper `MCP` area to build the current MCP server.
3. Press `Select`, choose the project's `.codex` or `.claude` folder, and press `Install MCP`.
4. Reconnect the AI client. The connection is ready when `Bridge` reports `Running` and the client appears under `Processes`.
5. For first use, ask the AI to build the navigation map:

```text
Analyze this Unity project's UI navigation and generate a complete AutoRun navigation map.
```

The AI uses the MCP tools to scan source code, review candidates, and create `Gen/ui-nav-map.json`. AutoRun only accepts a map after its finalization checks pass.

### Navigate with AI

After the map is ready, ask for the target directly:

```text
Run the game and open the inventory view.
```

The AI starts one asynchronous navigation task and polls it until Unity enters Play Mode, passes intermediate views, and reaches the target. You normally do not need to press Unity's Play button or have the AI repeatedly read the full map.

For direct editor operation, type a query in the lower `Navigation AutoRun` area, select the target, and press `Go!`. If Unity is not in Play Mode, the tool enters Play Mode and resumes the route after startup.

## Features

- Direct AI-driven Unity startup and navigation to a requested UI view.
- UI navigation map support through `Gen/ui-nav-map.json`.
- Route resolution and execution for click and wait based UI transitions.
- Asynchronous tracked UI navigation that survives Play Mode transitions and exposes compact progress polling.
- Navigation map tools for guidance, source scanning, patch validation, merging, summaries, subgraphs, and HTML preview.
- .NET 8 CLI and MCP server for Codex, Claude, or other MCP-capable clients.
- Local HTTP bridge on `127.0.0.1:17331` for external tooling.
- UGUI button discovery and clicking by GameObject name or button text.
- Optional FairyGUI support when FairyGUI is installed in the project.
- Delayed action sequences for editor Play Mode startup and shutdown workflows.
- In-window console with Debug, Info, Warning, and Error filtering.
- Manual AutoRun presets remain available for fixed Go and Stop sequences that do not use AI.

## Requirements

- [Unity 2021.3 or newer](https://unity.com/download).
- UGUI for built-in Unity button automation.
- [FairyGUI](https://www.fairygui.com/download) only if you want to automate FairyGUI buttons.
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) for the CLI and MCP server.

## Installation

The recommended installation method is [Unity Package Manager with a Git URL](https://docs.unity3d.com/2021.3/Documentation/Manual/upm-ui-giturl.html). This method requires [Git](https://git-scm.com/downloads) to be installed.

1. Open `Window > Package Manager`.
2. Press `+`.
3. Choose `Add package from git URL...`.
4. Enter:

```text
https://github.com/SHthemW/Unity-Autorun-Tool.git
```

For reproducible installs, use a version tag:

```text
https://github.com/SHthemW/Unity-Autorun-Tool.git#v0.1.0
```

Manual installation is also supported. Copy or clone this repository into a Unity project under:

```text
Assets/Editor/Unity-Autorun-Tool
```

Open Unity and use:

```text
Window > Auto Run Window
```

## MCP And Bridge Panel

The `MCP` panel in `Window > Auto Run Window` groups bridge controls, detected MCP processes, install controls, and helper tools.

- The panel header shows the current Unity AutoRun MCP version, and the `Processes` table reports the version of the MCP server binary connected to each AI client.
- `Start` / `Stop` controls the local Unity bridge.
- `Publish MCP` stops MCP child processes that point at the current published DLL, waits for the file to unlock, runs `dotnet publish`, and checks whether AI clients reconnect automatically.
- `Install MCP` updates a selected `.codex/config.toml` or `.claude/.mcp.json`.
- `Open Root` opens this tool folder.
- `Preview Nav Map` uses the bundled Viz.js / Graphviz layout engine to render `Gen/ui-nav-map.json` as an interactive `Gen/ui-nav-map.preview.html`.

You can also start or stop the bridge from:

```text
Window > Auto Run MCP Bridge > Start
Window > Auto Run MCP Bridge > Stop
```

The bridge listens at:

```text
http://127.0.0.1:17331/
```

If port `17331` is occupied, the bridge automatically tries each following port until one is available. The Auto Run window displays the actual URL in use.

The selected endpoint is published under the current Unity project's `Library` directory. MCP configurations do not store a port; AI clients call `get_unity_bridge_port` to obtain the endpoint dynamically.

## CLI

Run the CLI from the tool root. You can open `Window > Auto Run Window`, press `Open Root` in the `MCP` panel, and then launch a terminal from the opened directory. This works for both UPM Git URL installs and manual `Assets/Editor` installs.

```powershell
dotnet run --project mcp~/UnityAutorun.Mcp -- help
dotnet run --project mcp~/UnityAutorun.Mcp -- bridge-port
dotnet run --project mcp~/UnityAutorun.Mcp -- status
dotnet run --project mcp~/UnityAutorun.Mcp -- play
dotnet run --project mcp~/UnityAutorun.Mcp -- stop
dotnet run --project mcp~/UnityAutorun.Mcp -- list-buttons --framework all
dotnet run --project mcp~/UnityAutorun.Mcp -- click --name StartButton --framework ugui
dotnet run --project mcp~/UnityAutorun.Mcp -- run-sequence --json-file sequence.json
dotnet run --project mcp~/UnityAutorun.Mcp -- nav-guidance
dotnet run --project mcp~/UnityAutorun.Mcp -- scan-nav-sources --map Gen/ui-nav-map.json
dotnet run --project mcp~/UnityAutorun.Mcp -- backfill-nav-map --map Gen/ui-nav-map.json --preview true
dotnet run --project mcp~/UnityAutorun.Mcp -- routes --map Gen/ui-nav-map.example.json
dotnet run --project mcp~/UnityAutorun.Mcp -- route --map Gen/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- run-route --map Gen/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- navigate-ui --map Gen/ui-nav-map.json --to TargetView
dotnet run --project mcp~/UnityAutorun.Mcp -- mock-bridge
```

Environment variables:

| Variable | Default | Meaning |
| --- | --- | --- |
| `UNITY_AUTORUN_PROJECT_ROOT` | auto-detected by install | Project whose dynamically published bridge endpoint is used. |
| `UNITY_AUTORUN_TOOL_ROOT` | auto-detected by install | Tool root used by MCP nav-map operations. |

## MCP Server

Publish the server:

```powershell
dotnet publish mcp~/UnityAutorun.Mcp -c Release
```

Release publish output is also archived as a zip under:

```text
mcp~/UnityAutorun.Mcp/bin/Release-Archives/
```

### Publishing while MCP is running

The editor `Publish MCP` action matches this tool's MCP child processes by their full binary path. It terminates only those processes, not unrelated `dotnet.exe` instances, and starts publishing only after the processes exit and the published DLL is unlocked.

When MCP processes were running before publication, the tool waits up to 5 seconds after a successful publish for Codex, Claude, or another client to restart the expected number of MCP processes. A complete restart records the new PIDs. A partial or missing restart does not turn a successful publish into a failure; instead, the Auto Run Console records a Warning and the editor displays a reconnect prompt.

Unity cannot recreate an AI client's stdio connection. If the client does not restart MCP automatically, restart or reconnect the affected client session.

Running `dotnet publish` directly in a terminal does not use this process-management flow. Stop the affected MCP client connections first when the published DLL is in use, or use the editor `Publish MCP` action.

The editor `Install MCP` flow registers this command for the selected client:

```powershell
dotnet mcp~/UnityAutorun.Mcp/bin/Release/net8.0/publish/UnityAutorun.Mcp.dll mcp
```

Available MCP tools:

- `get_unity_bridge_port`
- `unity_status`
- `unity_play`
- `unity_stop`
- `list_buttons`
- `is_ui_view_open`
- `click_button`
- `run_sequence`
- `get_nav_map_guidance`
- `get_current_ui_nav_map`
- `save_ui_nav_map`
- `get_nav_map_summary`
- `scan_ui_nav_sources`
- `trace_ui_navigation_calls`
- `get_ui_nav_candidate_coverage`
- `finalize_ui_nav_map_generation`
- `backfill_ui_nav_map_from_sources`
- `query_nav_map_items`
- `get_ui_nav_subgraph`
- `validate_ui_nav_map_patch`
- `merge_ui_nav_map_patch`
- `list_ui_routes`
- `resolve_ui_route`
- `run_ui_route`
- `navigate_ui`
- `start_ui_navigation`
- `get_ui_navigation_status`
- `cancel_ui_navigation`

For requests such as "start the game and open a UI view", call `start_ui_navigation` once, then long-poll `get_ui_navigation_status` until `terminal=true`. Unity handles startup, login gates, route resolution, and per-step waits internally, so the AI does not need to repeatedly call `list_buttons`, read the full navigation map, or inspect broad logs.

Bridge tools resolve the dynamically published project endpoint automatically. `get_unity_bridge_port` is diagnostic only and is not a prerequisite for other bridge tools. `get_current_ui_nav_map` returns a summary by default; pass `full=true` only when the complete file is explicitly required.

For navigation-map generation, `scan_ui_nav_sources` reports source coverage and `trace_ui_navigation_calls` returns bounded button call-chain evidence across helper methods and component types. Each candidate includes compact `decisionHint` data, mapped endpoint ids, and serialized control evidence when the owner-type prefab can be resolved. Trace candidates are intentionally not executable edges: the external AI must decide the source view, referenced-view role, transition kind, control metadata, automation, and confidence, then validate and merge an incremental patch. Reachability and AutoRun executability are separate: missing exact click metadata, async work, branch preconditions, or absent runtime confirmation must not erase a code-proven edge. `backfill_ui_nav_map_from_sources` only adds deterministic source-discovered views and unresolved evidence; it never infers controls, transitions, or routes.

Complete generation must use `get_ui_nav_candidate_coverage` without a `query` as the candidate backlog. For every returned candidate, the external AI copies the exact `id` and `candidateVersion` into one `candidateDecisions` item, merges the patch, and requests `offset=0` again until `remaining=0`. Changed source or serialized-control evidence invalidates the old candidate version and returns it to the backlog. A `semantic-review-required` item must be replaced when it points at mismatched transition endpoints, fails to use resolved serialized-control identity, or downgrades strong topology evidence without concrete `nonTransitionEvidence`. The AI must then call `finalize_ui_nav_map_generation`; the tool returns `candidate_review_incomplete` or `candidate_semantic_review_incomplete` and leaves the map unfinished while any candidate remains incomplete.

## HTTP Bridge

Start the bridge first, then use the URL returned by `get_unity_bridge_port` or displayed in the Auto Run window:

```powershell
$bridgeUrl = "<dynamically published bridge URL>"
Invoke-RestMethod "${bridgeUrl}status"
Invoke-RestMethod "${bridgeUrl}rpc" `
  -Method Post `
  -ContentType application/json `
  -Body '{"id":"1","command":"click_button","payload":{"name":"StartButton","framework":"ugui"}}'
```

Bridge commands include:

- `status`
- `play`
- `stop`
- `list_buttons`
- `list_open_views`
- `is_ui_view_open`
- `click_button`
- `run_sequence`
- `navigate_route`
- `cancel_navigation`
- `start_ui_navigation`
- `get_ui_navigation_status`
- `cancel_ui_navigation`

## UI Navigation Map

The navigation system uses `Gen/ui-nav-map.json`. If that file does not exist, the editor falls back to `Gen/ui-nav-map.example.json`.

The map describes:

- `views`: UI screens, panels, or view roots.
- `controls`: buttons and related UI controls.
- `transitions`: how one view reaches another.
- `routes`: reusable paths across transitions.
- `unresolved`: known gaps that need manual analysis.
- `candidateDecisions`: compact review results for every static call-chain candidate. This ledger is used only for generation completeness and is ignored by runtime navigation.

The map carries `schemaVersion`, `generatorVersion`, and an incrementing `mapVersion`. Every write refreshes these fields and marks candidate coverage as `review-required`; only successful finalization restores `generation.status=complete`. The editor and MCP route loader reject maps whose format version, generator version, or completion state is stale.

`Preview Nav Map` uses the Graphviz `dot` engine to lay out views that participate in valid transitions and groups parallel transitions between the same pair of views into one edge. Views without a valid transition remain in the sidebar instead of widening the canvas. The preview supports search by name, id, or prefab path, one-click neighborhood focus, hover edge labels, panning, zooming, and fit-to-view. All rendering dependencies are bundled for offline use; the generated HTML does not access an external CDN.

The editor `Navigation AutoRun` panel loads the map, lists navigable target views, filters them by search text, and runs a route with `Go!`. If Unity is not already in Play Mode, it stores the pending target, enters Play Mode, and continues after Play Mode starts.

Route execution supports:

- `click` steps backed by UGUI or FairyGUI button actions.
- UGUI navigation clicks verified through the active `EventSystem`: the selected button must be interactable and own the top pointer raycast before pointer-down, pointer-up, and pointer-click events are dispatched.
- `wait` steps that wait for a view to become active, foreground, and stable.
- Per-control `delay` values as post-click settle time before the next route step starts.
- cancellation through the editor panel or bridge command.

Navigation never invokes inactive-hierarchy UGUI controls. A covered target is not treated as reached merely because its GameObject exists; the route keeps waiting and reports the blocking pointer target on timeout.

`start_ui_navigation` immediately returns a `navigationId` and stores the pending target in the editor session, allowing it to continue after entering Play Mode or reloading the script domain. `get_ui_navigation_status` returns `navigationStatus`, `navigationPhase`, `terminal`, `elapsedMilliseconds`, and compact target-view matches.

For AI-assisted map generation, first ask the MCP server for `get_nav_map_guidance`, then use the scan, coverage, query, validate, merge, and finalize tools instead of writing `ui-nav-map.json` directly. Do not report generation complete until the finalization gate succeeds.

## Console

The Auto Run Window includes a local console for tool messages. Entries are grouped by:

- `Debug`
- `Info`
- `Warning`
- `Error`

The toggles only filter visibility. `Clear` removes current entries. Messages are not mirrored to the Unity Console.

## Manual AutoRun

AI with `Navigation AutoRun` is the recommended workflow. The `Manual AutoRun` panel remains available for fixed button sequences that do not use an AI client.

Open `Window > Auto Run Window`, then use the lower `Manual AutoRun` panel.

1. Press `First use? Press me to create an autorun action config :)`.
2. Press `Save config` after the initial file is created.
3. Create a preset with `Then, press me to create a new action preset`, or use `+` next to the preset dropdown after at least one preset exists.
4. Add actions under `Action - Go` or `Action - Stop`.
5. Fill the action fields.
6. Press `Save config`.
7. Use `Go!` instead of the Unity Play button when you want startup automation.
8. Use `Stop` when you want Stop actions to run before leaving Play Mode.

Action fields:

| Field | Meaning |
| --- | --- |
| `name` | Target button GameObject name. UGUI lookup also normalizes names by ignoring `_`, `*`, trailing `GameObject`, and trailing `Button`. |
| `text` | Optional text match for UGUI buttons when names are ambiguous. Leave the default for FairyGUI unless your FairyGUI helper supports text filtering. |
| `delay` | Delay before this action runs. |
| `FGUI` | Use FairyGUI clicking instead of UGUI clicking. Disabled automatically when FairyGUI is not installed. |

`Go` actions run after entering Play Mode. `Stop` actions run before the editor exits Play Mode.

Configuration is stored under the Unity editor process base directory rather than inside the project:

```text
AutorunToolData/config.xml
```

This keeps local presets out of normal project version control. The tool creates a runtime `AutoRunHandler` GameObject and marks it as `DontDestroyOnLoad`. If you start Play Mode with Unity's native Play button after using `Go!`, the existing handler can remain until removed or reset.

## Project Layout

```text
.
|-- AutoRun*.cs                  # Shared runtime/editor automation models and handler
|-- package.json                 # Unity Package Manager metadata for Git URL installation
|-- Bridge/                      # HTTP bridge models, dispatcher, sequence, and navigation execution
|-- Editor/                      # Unity Editor window, menus, MCP install, processes, nav AutoRun UI
|-- Services/                    # UGUI/FairyGUI button and active-view discovery
|-- Util/                        # XML and optional FairyGUI helpers
|-- Gen/                         # Generated nav-map files and the tracked example map
|-- mcp~/UnityAutorun.Mcp/       # .NET 8 CLI and MCP server source
```

## Troubleshooting

- If `Install MCP` fails, press `Publish MCP` first and confirm the selected folder is named `.codex` or `.claude`.
- If CLI bridge calls fail, start the bridge in Unity and use `bridge-port` or the MCP tool `get_unity_bridge_port` to inspect the current endpoint.
- If a UGUI button is not found, check the runtime GameObject name, normalized name, and optional text field.
- If a route cannot run, inspect it with `route`, `resolve_ui_route`, or `get_ui_nav_subgraph` and check unsupported or unresolved transitions.
- If `Navigation AutoRun` shows no targets, create or merge a real `Gen/ui-nav-map.json`, or start from the example map to verify the workflow.
