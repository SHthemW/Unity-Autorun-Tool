# Unity AutoRun MCP

Start the Unity bridge from `Window/Auto Run MCP Bridge/Start`, then use the CLI or MCP server.

## CLI

```powershell
dotnet run --project mcp~/UnityAutorun.Mcp -- help
dotnet run --project mcp~/UnityAutorun.Mcp -- status
dotnet run --project mcp~/UnityAutorun.Mcp -- list-buttons --framework all
dotnet run --project mcp~/UnityAutorun.Mcp -- click --name StartButton --framework ugui
dotnet run --project mcp~/UnityAutorun.Mcp -- run-sequence --json-file sequence.json
dotnet run --project mcp~/UnityAutorun.Mcp -- routes --map mcp/ui-nav-map.example.json
dotnet run --project mcp~/UnityAutorun.Mcp -- route --map mcp/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- run-route --map mcp/ui-nav-map.example.json --from A --to C
dotnet run --project mcp~/UnityAutorun.Mcp -- mock-bridge
```

Environment variables:

- `UNITY_AUTORUN_HOST`: bridge host, default `127.0.0.1`
- `UNITY_AUTORUN_PORT`: bridge port, default `17331`

## MCP Server

Publish the server first, then use `dotnet mcp~/UnityAutorun.Mcp/bin/Release/net8.0/publish/UnityAutorun.Mcp.dll mcp` as a stdio MCP server command.

The installed MCP configuration starts the current published binary only. It does not build on each MCP connection.

## One-click install

Open `Window/Auto Run Window`, press `Publish MCP`, select the MCP install target folder, then press `Install MCP`.

- Press `Publish MCP` to publish the current MCP server version into `mcp~/UnityAutorun.Mcp/bin/Release/net8.0/publish/`.
- Select a `.codex` folder to update `config.toml` with a `[mcp_servers.unity_autorun]` entry.
- Select a `.claude` folder to update `.mcp.json` with a `unity-autorun` entry under `mcpServers`.
- Press `Preview Nav Map` to render `mcp/ui-nav-map.json` as `mcp/ui-nav-map.preview.html` and open it in the browser.

Installing an MCP means registering a server command with the AI client. The protocol is common, but each client stores the server configuration in its own format.

For a release binary:

```powershell
dotnet publish mcp~/UnityAutorun.Mcp -c Release
```

The publish output is also archived under `mcp~/UnityAutorun.Mcp/bin/Release-Archives/`.

The server exposes:

- `unity_status`
- `unity_play`
- `unity_stop`
- `list_buttons`
- `click_button`
- `run_sequence`
- `get_nav_map_guidance`
- `get_current_ui_nav_map`
- `save_ui_nav_map`
- `get_nav_map_summary`
- `scan_ui_nav_sources`
- `backfill_ui_nav_map_from_sources`
- `query_nav_map_items`
- `get_ui_nav_subgraph`
- `validate_ui_nav_map_patch`
- `merge_ui_nav_map_patch`
- `list_ui_routes`
- `resolve_ui_route`
- `run_ui_route`

## Prompting AI to build a nav map

Use this prompt after the MCP server is installed:

```text
Use the unity-autorun MCP tool get_nav_map_guidance first.
Guidance cannot force an external AI client to comply, so required MCP tool results are the acceptance record.
Do not accept self-certified completion from the model.
Read the current map with get_current_ui_nav_map before making changes.
Call get_nav_map_summary and scan_ui_nav_sources before broad work.
If the map is missing or large, generate it incrementally instead of producing one full JSON object.
Analyze this Unity project's UI prefabs and related C# UI scripts in slices by module, prefab folder, scene, or target view.
Use scan_ui_nav_sources coverage gaps as the backlog until UIFormId entries, prefabs, classes, and OpenUIForm targets are represented.
If coverage gaps are non-empty and the goal is a comprehensive map, call backfill_ui_nav_map_from_sources with previewOnly=true, then call it again to merge before manual patching.
Use query_nav_map_items and get_ui_nav_subgraph to inspect only relevant existing entries.
For each slice, generate a UI navigation map patch with views, controls, transitions, routes, and unresolved links.
Validate each patch with validate_ui_nav_map_patch, then save it with merge_ui_nav_map_patch.
Do not write `ui-nav-map.json` directly with filesystem operations.
Use evidence from prefab events, AddListener calls, FairyGUI callbacks, and UI router/window manager APIs.
Do not stop at direct button clicks; include Procedure, ChangeState, LoadScene, DataNode, EventArgs, lifecycle, and OpenUIForm/OpenUIFormAsync chains as indirect flow transitions.
For startup flows, generate important routes such as UIFormLogin -> UIFormLoadScene -> UIFormOperation when code evidence supports the chain.
For CodeBind-backed uGUI controls, resolve serialized prefab references and use the real GameObject name/path for objectPath and autoRun.buttonName, not the C# field or property name.
After merge_ui_nav_map_patch succeeds, call list_ui_routes and resolve_ui_route to validate the route from <start view> to <target view>.
Do not claim the map is complete while scan_ui_nav_sources still reports important missing UIFormId, prefab, or OpenUIForm targets; add unresolved entries with evidence for anything that cannot be resolved.
For this project, a map around 1-2k lines is suspiciously incomplete unless scan_ui_nav_sources proves coverage is complete.
Report final get_nav_map_summary counts and scan_ui_nav_sources coverage gaps.
If Unity is open and the AutoRun bridge is running, call run_ui_route to navigate to <target view>.
```

For bug investigation:

```text
I need to debug <target view>.
Use get_nav_map_guidance, read the current map with get_current_ui_nav_map, inspect the local graph with get_ui_nav_subgraph, save updates with validate_ui_nav_map_patch and merge_ui_nav_map_patch, resolve the route to <target view>, then use run_ui_route if the Unity bridge is running.
Do not invent uncertain transitions; put them in unresolved with source evidence.
```

## UI Navigation Map

AI agents can generate a `ui-nav-map.json` file from prefab and code analysis.

The CLI and MCP server can then resolve routes from that map and convert them into AutoRun actions.

Use `mcp/ui-nav-map.example.json` as the reference shape.

## Unity Bridge Protocol

The Unity Editor bridge listens on `http://127.0.0.1:17331/` after it is started.

```powershell
Invoke-RestMethod http://127.0.0.1:17331/status
Invoke-RestMethod http://127.0.0.1:17331/rpc `
  -Method Post `
  -ContentType application/json `
  -Body '{"id":"1","command":"click_button","payload":{"name":"StartButton","framework":"ugui"}}'
```

