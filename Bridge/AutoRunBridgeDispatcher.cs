using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

public sealed partial class AutoRunBridgeDispatcher
{
    private readonly Queue<AutoRunBridgeJob> _jobs = new();
    private readonly object _lock = new();

    public AutoRunBridgeResponse ExecuteStatus(string id)
    {
        return AutoRunBridgeResponses.Success(id, "AutoRun bridge is available.", new AutoRunBridgeData
        {
            isPlaying = EditorApplication.isPlaying,
            unityVersion = Application.unityVersion,
            bridgeVersion = AutoRunBridgeServer.Version,
        });
    }

    public AutoRunBridgeResponse Enqueue(string requestJson)
    {
        AutoRunBridgeRequest request;
        try
        {
            request = JsonUtility.FromJson<AutoRunBridgeRequest>(requestJson);
        }
        catch (Exception ex)
        {
            return AutoRunBridgeResponses.Fail(null, "invalid_json", ex.Message);
        }

        if (request == null || string.IsNullOrEmpty(request.command))
        {
            return AutoRunBridgeResponses.Fail(null, "invalid_request", "Request command is required.");
        }

        var job = new AutoRunBridgeJob(request);
        lock (_lock)
        {
            _jobs.Enqueue(job);
        }

        if (!job.WaitHandle.WaitOne(TimeSpan.FromSeconds(30)))
        {
            return AutoRunBridgeResponses.Fail(request.id, "timeout", "Unity bridge request timed out.");
        }

        return job.Response;
    }

    public void Pump()
    {
        if (_navigationJob != null)
        {
            PumpNavigation();
        }

        if (_sequenceJob != null)
        {
            PumpSequence();
        }

        AutoRunBridgeJob job = null;
        lock (_lock)
        {
            if (_jobs.Count > 0)
            {
                job = _jobs.Dequeue();
            }
        }

        if (job == null)
        {
            return;
        }

        bool shouldComplete = true;
        try
        {
            job.Response = Execute(job.Request, job, out shouldComplete);
        }
        catch (Exception ex)
        {
            job.Response = AutoRunBridgeResponses.Fail(job.Request.id, "execution_error", ex.Message);
        }
        finally
        {
            if (shouldComplete)
            {
                LogResponse(job.Response);
                job.WaitHandle.Set();
            }
        }
    }

    private AutoRunBridgeResponse Execute(AutoRunBridgeRequest request, AutoRunBridgeJob job, out bool shouldComplete)
    {
        shouldComplete = true;
        switch (request.command)
        {
            case "status":
                return ExecuteStatus(request.id);
            case "play":
                EditorApplication.isPlaying = true;
                return AutoRunBridgeResponses.Success(request.id, "Play mode requested.");
            case "stop":
                EditorApplication.isPlaying = false;
                return AutoRunBridgeResponses.Success(request.id, "Stop requested.");
            case "list_buttons":
                return ListButtons(request);
            case "list_open_views":
                return ListOpenViews(request);
            case "click_button":
                return ClickButton(request);
            case "run_sequence":
                shouldComplete = StartSequence(job);
                return job.Response;
            case "navigate_route":
                shouldComplete = StartNavigation(job);
                return job.Response;
            case "cancel_navigation":
                return CancelNavigation(request);
            default:
                return AutoRunBridgeResponses.Fail(request.id, "unknown_command", $"Unknown command: {request.command}");
        }
    }

    private static AutoRunBridgeResponse ListButtons(AutoRunBridgeRequest request)
    {
        AutoRunBridgePayload payload = request.payload ?? new AutoRunBridgePayload();
        string framework = string.IsNullOrEmpty(payload.framework) ? "all" : payload.framework;
        var buttons = AutoRunButtonService.ListButtons(framework);
        return AutoRunBridgeResponses.Success(request.id, $"Found {buttons.Count} buttons.", new AutoRunBridgeData
        {
            framework = framework,
            buttons = buttons,
        });
    }

    private static AutoRunBridgeResponse ClickButton(AutoRunBridgeRequest request)
    {
        AutoRunParam param = ToParam(request.payload);
        AutoRunButtonResult result = AutoRunButtonService.Click(param);
        return AutoRunBridgeResponses.FromButtonResult(request.id, result);
    }

    private static AutoRunBridgeResponse ListOpenViews(AutoRunBridgeRequest request)
    {
        List<string> openViews = AutoRunViewService.ListOpenViewNames();
        return AutoRunBridgeResponses.Success(request.id, $"Found {openViews.Count} active view candidates.", new AutoRunBridgeData
        {
            openViews = openViews,
        });
    }

    private static AutoRunParam ToParam(AutoRunBridgePayload payload)
    {
        return new AutoRunParam
        {
            buttonName = payload?.name ?? AutoRunParam.DEFAULT_NAME,
            buttonText = string.IsNullOrEmpty(payload?.text) ? AutoRunParam.DEFAULT_TEXT : payload.text,
            isFairyGUI = (payload?.framework ?? "ugui").ToLowerInvariant() == "fairygui",
        };
    }

    private static void LogResponse(AutoRunBridgeResponse response)
    {
        if (response == null)
        {
            return;
        }

        AutoRunWindow.AppendBridgeConsoleText($"MCP {response.code}: {response.message}",
            response.ok ? AutoRunLogLevel.Info : AutoRunLogLevel.Error);
        if (response.data?.messages == null)
        {
            return;
        }

        foreach (string message in response.data.messages)
        {
            AutoRunWindow.AppendBridgeConsoleText(message, AutoRunLogLevel.Debug);
        }
    }

    private sealed class AutoRunBridgeJob
    {
        public AutoRunBridgeJob(AutoRunBridgeRequest request)
        {
            Request = request;
        }

        public AutoRunBridgeRequest Request { get; }
        public AutoRunBridgeResponse Response { get; set; }
        public ManualResetEvent WaitHandle { get; } = new ManualResetEvent(false);
    }
}
