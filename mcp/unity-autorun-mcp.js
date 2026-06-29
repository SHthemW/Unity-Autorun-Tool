#!/usr/bin/env node

const { callUnity, getStatus } = require("./unity-bridge-client");
const { tools } = require("./mcp-tools");
const { listRoutes, loadNavMap, resolveRoute } = require("./ui-nav-map");

let inputBuffer = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", chunk => {
  inputBuffer += chunk;
  readMessages();
});

function readMessages() {
  while (inputBuffer.length > 0) {
    if (inputBuffer.startsWith("Content-Length:")) {
      const headerEnd = inputBuffer.indexOf("\r\n\r\n");
      if (headerEnd < 0) {
        return;
      }

      const header = inputBuffer.slice(0, headerEnd);
      const match = header.match(/Content-Length:\s*(\d+)/i);
      if (!match) {
        inputBuffer = "";
        writeError(null, -32700, "Missing Content-Length.");
        return;
      }

      const length = Number(match[1]);
      const bodyStart = headerEnd + 4;
      const bodyEnd = bodyStart + length;
      if (inputBuffer.length < bodyEnd) {
        return;
      }

      handleMessage(inputBuffer.slice(bodyStart, bodyEnd));
      inputBuffer = inputBuffer.slice(bodyEnd);
      continue;
    }

    const newlineIndex = inputBuffer.indexOf("\n");
    if (newlineIndex < 0) {
      return;
    }

    const line = inputBuffer.slice(0, newlineIndex).trim();
    inputBuffer = inputBuffer.slice(newlineIndex + 1);
    if (line) {
      handleMessage(line);
    }
  }
}

async function handleMessage(line) {
  let request;
  try {
    request = JSON.parse(line);
  } catch (error) {
    writeError(null, -32700, error.message);
    return;
  }

  try {
    if (request.method === "initialize") {
      writeResult(request.id, {
        protocolVersion: "2024-11-05",
        capabilities: { tools: {} },
        serverInfo: { name: "unity-autorun", version: "0.1.0" },
      });
    } else if (request.method === "notifications/initialized") {
      return;
    } else if (request.method === "tools/list") {
      writeResult(request.id, { tools });
    } else if (request.method === "tools/call") {
      writeResult(request.id, await callTool(request.params || {}));
    } else {
      writeError(request.id, -32601, `Method not found: ${request.method}`);
    }
  } catch (error) {
    writeError(request.id, -32000, error.message);
  }
}

async function callTool(params) {
  const args = params.arguments || {};
  const toolName = params.name;
  let result;
  if (toolName === "unity_status") {
    result = await getStatus();
  } else if (toolName === "unity_play") {
    result = await callUnity("play");
  } else if (toolName === "unity_stop") {
    result = await callUnity("stop");
  } else if (toolName === "list_buttons") {
    result = await callUnity("list_buttons", {
      framework: args.framework || "all",
    });
  } else if (toolName === "click_button") {
    result = await callUnity("click_button", {
      name: args.name,
      text: args.text,
      framework: args.framework || "ugui",
    });
  } else if (toolName === "run_sequence") {
    result = await callUnity("run_sequence", {
      actions: args.actions || [],
    });
  } else if (toolName === "list_ui_routes") {
    const { map, path } = loadNavMap(args.mapPath);
    result = { ok: true, path, routes: listRoutes(map) };
  } else if (toolName === "resolve_ui_route") {
    const { map, path } = loadNavMap(args.mapPath);
    result = { ok: true, path, route: resolveRoute(map, args) };
  } else if (toolName === "run_ui_route") {
    const { map, path } = loadNavMap(args.mapPath);
    const route = resolveRoute(map, args);
    result = await callUnity("run_sequence", { actions: route.autoRunSequence });
    result.route = { path, id: route.id, fromViewId: route.fromViewId, toViewId: route.toViewId };
  } else {
    throw new Error(`Unknown tool: ${toolName}`);
  }

  return {
    isError: result.ok === false,
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
  };
}

function writeResult(id, result) {
  process.stdout.write(JSON.stringify({ jsonrpc: "2.0", id, result }) + "\n");
}

function writeError(id, code, message) {
  process.stdout.write(JSON.stringify({
    jsonrpc: "2.0",
    id,
    error: { code, message },
  }) + "\n");
}
