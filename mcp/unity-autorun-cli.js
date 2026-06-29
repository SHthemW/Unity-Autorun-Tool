#!/usr/bin/env node

const { callUnity, getStatus } = require("./unity-bridge-client");
const { listRoutes, loadNavMap, resolveRoute } = require("./ui-nav-map");
const fs = require("node:fs");

function readOption(args, name, fallback = undefined) {
  const index = args.indexOf(name);
  if (index < 0 || index + 1 >= args.length) {
    return fallback;
  }

  return args[index + 1];
}

function hasFlag(args, name) {
  return args.includes(name);
}

function printUsage() {
  console.log([
    "Unity AutoRun CLI",
    "",
    "Usage:",
    "  node mcp/unity-autorun-cli.js help",
    "  node mcp/unity-autorun-cli.js status",
    "  node mcp/unity-autorun-cli.js play",
    "  node mcp/unity-autorun-cli.js stop",
    "  node mcp/unity-autorun-cli.js list-buttons [--framework ugui|fairygui|all]",
    "  node mcp/unity-autorun-cli.js click --name ButtonName [--text Text] [--framework ugui|fairygui]",
    "  node mcp/unity-autorun-cli.js run-sequence --json '[{\"buttonName\":\"Start\"}]'",
    "  node mcp/unity-autorun-cli.js run-sequence --json-file sequence.json",
    "  node mcp/unity-autorun-cli.js routes --map ui-nav-map.json",
    "  node mcp/unity-autorun-cli.js route --map ui-nav-map.json --from A --to C",
    "  node mcp/unity-autorun-cli.js run-route --map ui-nav-map.json --from A --to C",
    "",
    "Commands:",
    "  help          Show this help.",
    "  status        Check whether the Unity bridge is reachable.",
    "  play          Request Unity Editor to enter Play Mode.",
    "  stop          Request Unity Editor to exit Play Mode.",
    "  list-buttons  List UGUI/FairyGUI buttons in the current scene.",
    "  click         Click one button by name or text.",
    "  run-sequence  Run a JSON array of AutoRunParam actions.",
    "  routes        List routes in a UI navigation map.",
    "  route         Resolve one route and print its AutoRun sequence.",
    "  run-route     Resolve one route and execute its AutoRun sequence.",
    "",
    "Environment:",
    "  UNITY_AUTORUN_HOST  Bridge host, default 127.0.0.1",
    "  UNITY_AUTORUN_PORT  Bridge port, default 17331",
  ].join("\n"));
}

async function main() {
  const args = process.argv.slice(2);
  const command = args[0];

  if (!command || command === "help" || hasFlag(args, "--help") || hasFlag(args, "-h")) {
    printUsage();
    return;
  }

  let result;
  if (command === "status") {
    result = await getStatus();
  } else if (command === "play" || command === "stop") {
    result = await callUnity(command);
  } else if (command === "list-buttons") {
    result = await callUnity("list_buttons", {
      framework: readOption(args, "--framework", "all"),
    });
  } else if (command === "click") {
    result = await callUnity("click_button", {
      name: readOption(args, "--name"),
      text: readOption(args, "--text"),
      framework: readOption(args, "--framework", "ugui"),
    });
  } else if (command === "run-sequence") {
    const jsonFile = readOption(args, "--json-file");
    const rawJson = jsonFile ? fs.readFileSync(jsonFile, "utf8") : readOption(args, "--json", "[]");
    result = await callUnity("run_sequence", {
      actions: JSON.parse(rawJson),
    });
  } else if (command === "routes") {
    const { map, path } = loadNavMap(readOption(args, "--map"));
    result = { ok: true, path, routes: listRoutes(map) };
  } else if (command === "route") {
    const { map, path } = loadNavMap(readOption(args, "--map"));
    result = {
      ok: true,
      path,
      route: resolveRoute(map, readRouteOptions(args)),
    };
  } else if (command === "run-route") {
    const { map, path } = loadNavMap(readOption(args, "--map"));
    const route = resolveRoute(map, readRouteOptions(args));
    result = await callUnity("run_sequence", {
      actions: route.autoRunSequence,
    });
    result.route = { path, id: route.id, fromViewId: route.fromViewId, toViewId: route.toViewId };
  } else {
    printUsage();
    process.exitCode = 1;
    return;
  }

  console.log(JSON.stringify(result, null, 2));
  if (result && result.ok === false) {
    process.exitCode = 2;
  }
}

function readRouteOptions(args) {
  return {
    route: readOption(args, "--route"),
    from: readOption(args, "--from"),
    to: readOption(args, "--to"),
  };
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});
