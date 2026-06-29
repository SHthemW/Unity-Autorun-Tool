#!/usr/bin/env node

const http = require("node:http");

const port = Number(process.env.UNITY_AUTORUN_PORT || 17331);

const server = http.createServer((request, response) => {
  if (request.method === "GET" && request.url === "/status") {
    writeJson(response, {
      id: "status",
      ok: true,
      code: "ok",
      message: "mock bridge is available.",
      data: { isPlaying: true, bridgeVersion: "mock" },
    });
    return;
  }

  if (request.method !== "POST" || request.url !== "/rpc") {
    response.statusCode = 404;
    writeJson(response, { ok: false, code: "not_found" });
    return;
  }

  let body = "";
  request.on("data", chunk => { body += chunk; });
  request.on("end", () => {
    const rpc = JSON.parse(body);
    writeJson(response, {
      id: rpc.id,
      ok: true,
      code: "ok",
      message: `mock ${rpc.command}`,
      data: { received: rpc },
    });
  });
});

server.listen(port, "127.0.0.1", () => {
  console.log(`mock bridge listening on ${port}`);
});

function writeJson(response, value) {
  response.setHeader("content-type", "application/json");
  response.end(JSON.stringify(value));
}
