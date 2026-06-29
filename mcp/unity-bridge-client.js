const DEFAULT_HOST = "127.0.0.1";
const DEFAULT_PORT = 17331;

function getBridgeUrl(pathname) {
  const host = process.env.UNITY_AUTORUN_HOST || DEFAULT_HOST;
  const port = process.env.UNITY_AUTORUN_PORT || DEFAULT_PORT;
  return `http://${host}:${port}${pathname}`;
}

async function callUnity(command, payload = {}) {
  return withBridgeRetry(async () => {
    const response = await fetch(getBridgeUrl("/rpc"), {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id: `cli-${Date.now()}`,
        command,
        payload,
      }),
    });

    return parseBridgeResponse(response);
  });
}

async function getStatus() {
  return withBridgeRetry(async () => parseBridgeResponse(await fetch(getBridgeUrl("/status"))));
}

async function parseBridgeResponse(response) {
  if (!response.ok) {
    throw new Error(`Bridge HTTP ${response.status}: ${await response.text()}`);
  }

  return response.json();
}

async function withBridgeRetry(operation) {
  const attempts = Number(process.env.UNITY_AUTORUN_RETRIES || 10);
  const delayMs = Number(process.env.UNITY_AUTORUN_RETRY_DELAY_MS || 500);
  let lastError;

  for (let i = 0; i < attempts; i++) {
    try {
      return await operation();
    } catch (error) {
      lastError = error;
      if (i < attempts - 1) {
        await delay(delayMs);
      }
    }
  }

  throw new Error([
    `Unity AutoRun bridge is not reachable at ${getBridgeUrl("")}.`,
    "Start it from Unity: Window/Auto Run MCP Bridge/Start.",
    `Last error: ${lastError.message}`,
  ].join(" "));
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

module.exports = {
  callUnity,
  getStatus,
};
