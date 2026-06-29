# Unity AutoRun MCP

Start the Unity bridge from `Window/Auto Run MCP Bridge/Start`, then use the CLI or MCP server.

## CLI

```powershell
node mcp/unity-autorun-cli.js help
node mcp/unity-autorun-cli.js status
node mcp/unity-autorun-cli.js list-buttons --framework all
node mcp/unity-autorun-cli.js click --name StartButton --framework ugui
node mcp/unity-autorun-cli.js run-sequence --json-file sequence.json
node mcp/unity-autorun-cli.js routes --map mcp/ui-nav-map.example.json
node mcp/unity-autorun-cli.js route --map mcp/ui-nav-map.example.json --from A --to C
node mcp/unity-autorun-cli.js run-route --map mcp/ui-nav-map.example.json --from A --to C
```

Environment variables:

- `UNITY_AUTORUN_HOST`: bridge host, default `127.0.0.1`
- `UNITY_AUTORUN_PORT`: bridge port, default `17331`

## MCP Server

Use `node mcp/unity-autorun-mcp.js` as a stdio MCP server command.

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
