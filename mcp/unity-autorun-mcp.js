#!/usr/bin/env node

const { callUnity, getStatus } = require("./unity-bridge-client");

const tools = [
  {
    name: "unity_status",
    description: "Check the Unity AutoRun bridge status.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "unity_play",
    description: "Request Unity Editor to enter Play Mode.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "unity_stop",
    description: "Request Unity Editor to exit Play Mode.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "list_buttons",
    description: "List current Unity UI buttons.",
    inputSchema: {
      type: "object",
      properties: {
        framework: { type: "string", enum: ["ugui", "fairygui", "all"] },
      },
    },
  },
  {
    name: "click_button",
    description: "Click a Unity UI button by name or text.",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string" },
        text: { type: "string" },
        framework: { type: "string", enum: ["ugui", "fairygui"] },
      },
      required: ["name"],
    },
  },
  {
    name: "run_sequence",
    description: "Run a sequence of AutoRun button actions.",
    inputSchema: {
      type: "object",
      properties: {
        actions: { type: "array", items: { type: "object" } },
      },
      required: ["actions"],
    },
  },
];

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
