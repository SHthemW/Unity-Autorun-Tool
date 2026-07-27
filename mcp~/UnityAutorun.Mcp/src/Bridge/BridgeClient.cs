using System;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public sealed class BridgeClient
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly BridgeEndpointProvider _endpointProvider = new BridgeEndpointProvider();
        private readonly int _retries = int.TryParse(Environment.GetEnvironmentVariable("UNITY_AUTORUN_RETRIES"), out int retries) ? retries : 10;
        private readonly int _retryDelayMs = int.TryParse(Environment.GetEnvironmentVariable("UNITY_AUTORUN_RETRY_DELAY_MS"), out int delay) ? delay : 500;

        public JsonObject GetEndpointInfo()
        {
            return _endpointProvider.GetInfo();
        }

        public Task<JsonNode> GetStatusAsync()
        {
            return WithRetryAsync(async endpoint =>
                await ParseResponseAsync(await Http.GetAsync($"{endpoint.BaseUrl}/status")));
        }

        public Task<JsonNode> CallUnityAsync(string command, JsonObject payload = null)
        {
            return WithRetryAsync(async endpoint =>
            {
                var request = JsonUtil.Obj(
                    ("id", $"cli-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"),
                    ("command", command),
                    ("payload", payload ?? new JsonObject())
                );
                using (var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"))
                {
                    return await ParseResponseAsync(await Http.PostAsync($"{endpoint.BaseUrl}/rpc", content));
                }
            });
        }

        private async Task<JsonNode> ParseResponseAsync(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Bridge HTTP {(int)response.StatusCode}: {body}");
            }

            return JsonNode.Parse(body);
        }

        private async Task<JsonNode> WithRetryAsync(Func<BridgeEndpoint, Task<JsonNode>> operation)
        {
            Exception lastError = null;
            string lastUrl = null;
            for (int i = 0; i < _retries; i++)
            {
                try
                {
                    BridgeEndpoint endpoint = _endpointProvider.GetRequired();
                    lastUrl = endpoint.Url;
                    return await operation(endpoint);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (i < _retries - 1)
                    {
                        await Task.Delay(_retryDelayMs);
                    }
                }
            }

            string message = lastError != null ? lastError.Message : "unknown";
            string endpointText = lastUrl != null ? $" at {lastUrl}" : "";
            throw new InvalidOperationException(
                $"Unity AutoRun bridge is not reachable{endpointText}. "
                + $"Start it from Unity: Window/Auto Run MCP Bridge/Start. Last error: {message}"
            );
        }
    }
}

