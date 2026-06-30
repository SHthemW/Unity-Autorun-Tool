using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

public sealed class AutoRunBridgeServer
{
    public const string Version = "0.1.0";
    public const int DefaultPort = 17331;

    private readonly object _lock = new();
    private HttpListener _listener;
    private Thread _thread;
    private AutoRunBridgeDispatcher _dispatcher;

    public bool IsRunning => _listener != null && _listener.IsListening;

    public void Start(int port = DefaultPort)
    {
        if (IsRunning)
        {
            return;
        }

        _dispatcher = new AutoRunBridgeDispatcher();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _thread = new Thread(ListenLoop) { IsBackground = true };
        _thread.Start();
        EditorApplication.update += _dispatcher.Pump;
        Debug.Log($"AutoRun MCP bridge started on http://127.0.0.1:{port}/");
    }

    public void Stop()
    {
        bool wasRunning = false;
        lock (_lock)
        {
            if (_listener == null)
            {
                return;
            }

            wasRunning = _listener.IsListening;
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }

        if (_dispatcher != null)
        {
            EditorApplication.update -= _dispatcher.Pump;
            _dispatcher = null;
        }

        if (wasRunning)
        {
            Debug.Log("AutoRun MCP bridge stopped.");
        }
    }

    public AutoRunBridgeResponse Enqueue(string requestJson)
    {
        if (!IsRunning || _dispatcher == null)
        {
            return AutoRunBridgeResponses.Fail(null, "bridge_not_running", "AutoRun MCP bridge is not running.");
        }

        return _dispatcher.Enqueue(requestJson);
    }

    private void ListenLoop()
    {
        while (IsRunning)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleContext(context));
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void HandleContext(HttpListenerContext context)
    {
        if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/status")
        {
            WriteJson(context, _dispatcher.Enqueue("{\"id\":\"status\",\"command\":\"status\"}"));
            return;
        }

        if (context.Request.HttpMethod != "POST" || context.Request.Url.AbsolutePath != "/rpc")
        {
            context.Response.StatusCode = 404;
            WriteJson(context, AutoRunBridgeResponses.Fail(null, "not_found", "Endpoint not found."));
            return;
        }

        string body;
        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
        {
            body = reader.ReadToEnd();
        }

        AutoRunBridgeResponse response = _dispatcher.Enqueue(body);
        WriteJson(context, response);
    }

    private static void WriteJson(HttpListenerContext context, AutoRunBridgeResponse response)
    {
        string json = JsonUtility.ToJson(response);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }
}
