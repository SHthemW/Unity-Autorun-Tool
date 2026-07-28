using System;
using UnityEditor;

[InitializeOnLoad]
public static class NavigationAutoRunPendingRunner
{
    private const double StartupTimeoutSeconds = 30;
    private static bool _running;
    private static bool _requestSent;
    private static double _startedAt;
    private static double _lastWaitingForPlayLogAt;
    private static double _lastResolveWaitLogAt;
    private static double _nextResolveAttemptAt;
    private static string _lastError;

    static NavigationAutoRunPendingRunner()
    {
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        Log("Initialized. hasPending=" + NavigationAutoRunSession.HasPending
            + ", isPlaying=" + EditorApplication.isPlaying);
    }

    public static void EnsureListening()
    {
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        Log("EnsureListening. hasPending=" + NavigationAutoRunSession.HasPending
            + ", isPlaying=" + EditorApplication.isPlaying);
    }

    private static void Pump()
    {
        if (!NavigationAutoRunSession.HasPending)
        {
            if (_running)
            {
                Log("Stopped because pending state was cleared.");
            }

            Reset(false);
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            NavigationAutoRunSession.SetTrackedProgress("waiting_for_play_mode");
            LogWaitingForPlayMode();
            return;
        }

        if (!_running)
        {
            _running = true;
            _requestSent = false;
            _startedAt = EditorApplication.timeSinceStartup;
            _lastResolveWaitLogAt = -1;
            _lastError = null;
            NavigationAutoRunSession.SetTrackedProgress("resolving_route");
            Log("Pending detected in Play Mode. target=" + NavigationAutoRunSession.TargetViewId
                + ", name=" + NavigationAutoRunSession.TargetName
                + ", runId=" + NavigationAutoRunSession.RunId);
        }

        if (_requestSent)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < _nextResolveAttemptAt)
        {
            return;
        }

        _nextResolveAttemptAt = now + 0.5;
        if (TrySendRequest())
        {
            _requestSent = true;
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - _startedAt;
        LogResolveWait(elapsed);
        if (elapsed < StartupTimeoutSeconds)
        {
            return;
        }

        Log("Timed out after " + elapsed.ToString("0.0") + "s. Last error: " + Safe(_lastError), AutoRunLogLevel.Error);
        NavigationAutoRunSession.FailTracked(
            "navigation_startup_timeout",
            "Navigation route could not be resolved within " + StartupTimeoutSeconds.ToString("0") + "s. Last error: " + Safe(_lastError)
        );
        NavigationAutoRunSession.ClearPending();
        Reset(false);
    }

    private static bool TrySendRequest()
    {
        try
        {
            Log("Loading navigation map from: " + NavigationAutoRunMap.GetDefaultMapPath());
            NavigationAutoRunMap map = NavigationAutoRunMap.LoadDefault();
            var openViews = AutoRunViewService.ListOpenViewNames();
            Log("Resolving route. target=" + NavigationAutoRunSession.TargetViewId
                + ", openViews=" + NavigationAutoRunLog.FormatOpenViews(openViews));
            NavigationAutoRunPlan plan = map.ResolveToTarget(NavigationAutoRunSession.TargetViewId, openViews);
            Log("Resolved route " + plan.RouteId + ", steps=" + plan.Steps.Count);
            NavigationAutoRunSession.SetTrackedProgress("starting_route", plan.RouteId);
            NavigationAutoRunRequest.Start(plan, OnCompleted);
            Log("Request started for pending target " + NavigationAutoRunSession.TargetViewId);
            return true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            NavigationAutoRunSession.SetTrackedProgress("waiting_for_route", null, _lastError);
            return false;
        }
    }

    private static void OnCompleted(AutoRunBridgeResponse response)
    {
        if (response == null)
        {
            Log("Completed with no response.", AutoRunLogLevel.Error);
            NavigationAutoRunSession.FailTracked("navigation_no_response", "Navigation completed without a response.");
            NavigationAutoRunSession.ClearPending();
            Reset(true);
            return;
        }

        Log("Completed response ok=" + response.ok + ", code=" + response.code + ", message=" + response.message,
            response.ok ? AutoRunLogLevel.Info : AutoRunLogLevel.Error);
        if (IsRetryablePreflightResponse(response))
        {
            _lastError = response.message;
            _requestSent = false;
            NavigationAutoRunSession.SetTrackedProgress("waiting_for_route", null, _lastError);
            Log("Keeping pending target and retrying route resolution. lastError=" + Safe(_lastError));
            return;
        }

        if (!response.ok)
        {
            Log("Failed response " + response.code + ": " + response.message, AutoRunLogLevel.Error);
        }

        NavigationAutoRunSession.CompleteTracked(response);
        NavigationAutoRunSession.ClearPending();
        Reset(true);
    }

    private static bool IsRetryablePreflightResponse(AutoRunBridgeResponse response)
    {
        return response != null && response.code == "navigation_empty_route";
    }

    private static void LogWaitingForPlayMode()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastWaitingForPlayLogAt < 1)
        {
            return;
        }

        _lastWaitingForPlayLogAt = now;
        Log("Pending target exists, waiting for Play Mode. target=" + NavigationAutoRunSession.TargetViewId);
    }

    private static void LogResolveWait(double elapsed)
    {
        if (elapsed - _lastResolveWaitLogAt < 1)
        {
            return;
        }

        _lastResolveWaitLogAt = elapsed;
        Log("Waiting for route. elapsed=" + elapsed.ToString("0.0")
            + "s, target=" + NavigationAutoRunSession.TargetViewId
            + ", lastError=" + Safe(_lastError));
    }

    private static void Reset(bool keepListening)
    {
        _running = false;
        _requestSent = false;
        _startedAt = 0;
        _lastWaitingForPlayLogAt = 0;
        _lastResolveWaitLogAt = 0;
        _nextResolveAttemptAt = 0;
        _lastError = null;
        if (!keepListening)
        {
            return;
        }

        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }

    private static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "none" : value;
    }

    private static void Log(string message, AutoRunLogLevel level = AutoRunLogLevel.Debug)
    {
        AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message, level);
    }
}
