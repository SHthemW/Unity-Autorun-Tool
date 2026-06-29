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
- `list_ui_routes`
- `resolve_ui_route`
- `run_ui_route`

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

