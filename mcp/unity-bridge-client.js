const DEFAULT_HOST = "127.0.0.1";
const DEFAULT_PORT = 17331;

function getBridgeUrl(pathname) {
  const host = process.env.UNITY_AUTORUN_HOST || DEFAULT_HOST;
  const port = process.env.UNITY_AUTORUN_PORT || DEFAULT_PORT;
  return `http://${host}:${port}${pathname}`;
}

async function callUnity(command, payload = {}) {
  const response = await fetch(getBridgeUrl("/rpc"), {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      id: `cli-${Date.now()}`,
      command,
      payload,
    }),
  });

  if (!response.ok) {
    throw new Error(`Bridge HTTP ${response.status}: ${await response.text()}`);
  }

  return response.json();
}

async function getStatus() {
  const response = await fetch(getBridgeUrl("/status"));
  if (!response.ok) {
    throw new Error(`Bridge HTTP ${response.status}: ${await response.text()}`);
  }

  return response.json();
}

module.exports = {
  callUnity,
  getStatus,
};
