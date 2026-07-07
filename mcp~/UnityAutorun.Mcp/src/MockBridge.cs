using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public static class MockBridge
    {
        public static async Task RunAsync()
        {
            int port = int.TryParse(Environment.GetEnvironmentVariable("UNITY_AUTORUN_PORT"), out int value) ? value : 17331;
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                Console.WriteLine($"mock bridge listening on {port}");

                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleAsync(context));
                }
            }
        }

        private static async Task HandleAsync(HttpListenerContext context)
        {
            if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath == "/status")
            {
                await WriteAsync(context, JsonUtil.Obj(
                    ("id", "status"),
                    ("ok", true),
                    ("code", "ok"),
                    ("message", "mock bridge is available."),
                    ("data", JsonUtil.Obj(("isPlaying", true), ("bridgeVersion", "mock")))
                ));
                return;
            }

            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/rpc")
            {
                context.Response.StatusCode = 404;
                await WriteAsync(context, JsonUtil.Obj(("ok", false), ("code", "not_found")));
                return;
            }

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                JsonNode request = JsonNode.Parse(await reader.ReadToEndAsync());
                await WriteAsync(context, JsonUtil.Obj(
                    ("id", request?["id"]?.DeepClone()),
                    ("ok", true),
                    ("code", "ok"),
                    ("message", $"mock {request?["command"]?.GetValue<string>()}"),
                    ("data", JsonUtil.Obj(("received", request?.DeepClone())))
                ));
            }
        }

        private static async Task WriteAsync(HttpListenerContext context, JsonNode response)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(response.ToJsonString());
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }
    }
}

