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

Use `dotnet run --project mcp~/UnityAutorun.Mcp -- mcp` as a stdio MCP server command.

## One-click install

Open `Window/Auto Run Window`, select the MCP install target folder, then press `Install MCP`.

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
- `list_ui_routes`
- `resolve_ui_route`
- `run_ui_route`

## Prompting AI to build a nav map

Use this prompt after the MCP server is installed:

```text
Use the unity-autorun MCP tool get_nav_map_guidance first.
Then analyze this Unity project's UI prefabs and related C# UI scripts.
Generate mcp/ui-nav-map.json with views, controls, transitions, routes, and unresolved links.
Use evidence from prefab events, AddListener calls, FairyGUI callbacks, and UI router/window manager APIs.
After writing the file, call list_ui_routes and resolve_ui_route to validate the route from <start view> to <target view>.
If Unity is open and the AutoRun bridge is running, call run_ui_route to navigate to <target view>.
```

For bug investigation:

```text
I need to debug <target view>.
Use get_nav_map_guidance, generate or update mcp/ui-nav-map.json, resolve the route to <target view>, then use run_ui_route if the Unity bridge is running.
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

