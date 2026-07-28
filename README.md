# Unity Autorun Tool

[English](./README.md) | [中文](./README_CN.md)

Unity Autorun Tool is a Unity Editor extension for automating UI startup flows, test navigation, and AI-assisted UI operation. It can click UGUI and FairyGUI buttons from editor presets, expose the same operations through a local HTTP bridge, and provide a .NET 8 MCP server so AI clients can inspect and drive Unity UI flows through structured tools.

## Features

- Manual AutoRun panel for reusable Go and Stop action presets.
- UGUI button discovery and clicking by GameObject name or button text.
- Optional FairyGUI support when FairyGUI is installed in the project.
- Delayed action sequences for editor Play Mode startup and shutdown workflows.
- Local HTTP bridge on `127.0.0.1:17331` for external tooling.
- .NET 8 CLI and MCP server for Codex, Claude, or other MCP-capable clients.
- UI navigation map support through `mcp/ui-nav-map.json`.
- Route resolution and execution for click and wait based UI transitions.
- Asynchronous tracked UI navigation that survives Play Mode transitions and exposes compact progress polling.
- Navigation map tools for guidance, source scanning, patch validation, merging, summaries, subgraphs, and HTML preview.
- In-window console with Debug, Info, Warning, and Error filtering.

## Requirements

- Unity 2021.3 or newer.
- UGUI for built-in Unity button automation.
- FairyGUI only if you want to automate FairyGUI buttons.
- .NET 8 SDK for the CLI and MCP server.

## Installation

The recommended installation method is Unity Package Manager with a Git URL.

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

The extension stores manual AutoRun configuration outside the Unity project, under the Unity editor process base directory:

```text
AutorunToolData/config.xml
```

That keeps local automation presets out of normal project version control.

## Manual AutoRun

Open `Window > Auto Run Window`, then use the `Manual AutoRun` panel.

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

The tool creates a runtime `AutoRunHandler` GameObject and marks it as `DontDestroyOnLoad`. If you start Play Mode with Unity's native Play button after using `Go!`, the existing handler can still be present until removed or reset.

## MCP And Bridge Panel

The `MCP` panel in `Window > Auto Run Window` groups bridge controls, detected MCP processes, install controls, and helper tools.

- `Start` / `Stop` controls the local Unity bridge.
- `Publish MCP` stops MCP child processes that point at the current published DLL, waits for the file to unlock, runs `dotnet publish`, and checks whether AI clients reconnect automatically.
- `Install MCP` updates a selected `.codex/config.toml` or `.claude/.mcp.json`.
- `Open Terminal` opens a terminal at the tool root.
- `Open Root` opens this tool folder.
- `Preview Nav Map` renders `mcp/ui-nav-map.json` as `mcp/ui-nav-map.preview.html`.

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

Run the CLI from the tool root. The easiest way is to open `Window > Auto Run Window`, then press `Open Terminal` in the `MCP` panel. This works for both UPM Git URL installs and manual `Assets/Editor` installs.

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
dotnet run --project mcp~/UnityAutorun.Mcp -- scan-nav-sources --map mcp/ui-nav-map.json
dotnet run --project mcp~/UnityAutorun.Mcp -- backfill-nav-map --map mcp/ui-nav-map.json --preview true
dotnet run --project mcp~/UnityAutorun.Mcp -- routes --map mcp/ui-nav-map.example.json
dotnet run --project mcp~/UnityAutorun.Mcp -- route --map mcp/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- run-route --map mcp/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- navigate-ui --map mcp/ui-nav-map.json --to TargetView
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

For navigation-map generation, `scan_ui_nav_sources` reports source coverage and `trace_ui_navigation_calls` returns bounded button call-chain evidence across helper methods and component types. Trace candidates are intentionally not executable edges: the external AI must decide the source view, referenced-view role, transition kind, control metadata, automation, and confidence, then validate and merge an incremental patch. `backfill_ui_nav_map_from_sources` only adds deterministic source-discovered views and unresolved evidence; it never infers controls, transitions, or routes.

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

The navigation system uses `mcp/ui-nav-map.json`. If that file does not exist, the editor falls back to `mcp/ui-nav-map.example.json`.

The map describes:

- `views`: UI screens, panels, or view roots.
- `controls`: buttons and related UI controls.
- `transitions`: how one view reaches another.
- `routes`: reusable paths across transitions.
- `unresolved`: known gaps that need manual analysis.

The editor `Navigation AutoRun` panel loads the map, lists navigable target views, filters them by search text, and runs a route with `Go!`. If Unity is not already in Play Mode, it stores the pending target, enters Play Mode, and continues after Play Mode starts.

Route execution supports:

- `click` steps backed by UGUI or FairyGUI button actions.
- `wait` steps that wait for a view to appear.
- cancellation through the editor panel or bridge command.

`start_ui_navigation` immediately returns a `navigationId` and stores the pending target in the editor session, allowing it to continue after entering Play Mode or reloading the script domain. `get_ui_navigation_status` returns `navigationStatus`, `navigationPhase`, `terminal`, `elapsedMilliseconds`, and compact target-view matches.

For AI-assisted map generation, first ask the MCP server for `get_nav_map_guidance`, then use the scan, query, validate, and merge tools instead of writing `ui-nav-map.json` directly.

## Console

The Auto Run Window includes a local console for tool messages. Entries are grouped by:

- `Debug`
- `Info`
- `Warning`
- `Error`

The toggles only filter visibility. `Clear` removes current entries. Messages are not mirrored to the Unity Console.

## Project Layout

```text
.
|-- AutoRun*.cs                  # Shared runtime/editor automation models and handler
|-- package.json                 # Unity Package Manager metadata for Git URL installation
|-- Bridge/                      # HTTP bridge models, dispatcher, sequence, and navigation execution
|-- Editor/                      # Unity Editor window, menus, MCP install, processes, nav AutoRun UI
|-- Services/                    # UGUI/FairyGUI button and active-view discovery
|-- Util/                        # XML and optional FairyGUI helpers
|-- mcp/                         # Public nav-map files and MCP user docs
|-- mcp~/UnityAutorun.Mcp/       # .NET 8 CLI and MCP server source
```

## Troubleshooting

- If `Install MCP` fails, press `Publish MCP` first and confirm the selected folder is named `.codex` or `.claude`.
- If CLI bridge calls fail, start the bridge in Unity and use `bridge-port` or the MCP tool `get_unity_bridge_port` to inspect the current endpoint.
- If a UGUI button is not found, check the runtime GameObject name, normalized name, and optional text field.
- If a route cannot run, inspect it with `route`, `resolve_ui_route`, or `get_ui_nav_subgraph` and check unsupported or unresolved transitions.
- If `Navigation AutoRun` shows no targets, create or merge a real `mcp/ui-nav-map.json`, or start from the example map to verify the workflow.
