using System;
using UnityEditor;

public static class NavigationAutoRunSession
{
    public const string StatusRunning = "running";
    public const string StatusSucceeded = "succeeded";
    public const string StatusFailed = "failed";
    public const string StatusCanceled = "canceled";

    private const string PendingKey = "UnityAutorunTool.Navigation.Pending";
    private const string TargetViewIdKey = "UnityAutorunTool.Navigation.TargetViewId";
    private const string TargetNameKey = "UnityAutorunTool.Navigation.TargetName";
    private const string RunIdKey = "UnityAutorunTool.Navigation.RunId";
    private const string ActiveRequestKey = "UnityAutorunTool.Navigation.ActiveRequest";
    private const string ActiveTargetViewIdKey = "UnityAutorunTool.Navigation.ActiveTargetViewId";
    private const string ActiveTargetNameKey = "UnityAutorunTool.Navigation.ActiveTargetName";
    private const string TrackedKey = "UnityAutorunTool.Navigation.Tracked";
    private const string NavigationIdKey = "UnityAutorunTool.Navigation.NavigationId";
    private const string NavigationStatusKey = "UnityAutorunTool.Navigation.Status";
    private const string NavigationPhaseKey = "UnityAutorunTool.Navigation.Phase";
    private const string NavigationTargetViewIdKey = "UnityAutorunTool.Navigation.TrackedTargetViewId";
    private const string NavigationTargetNameKey = "UnityAutorunTool.Navigation.TrackedTargetName";
    private const string NavigationTargetRuntimeTokenKey = "UnityAutorunTool.Navigation.TrackedTargetRuntimeToken";
    private const string NavigationRouteIdKey = "UnityAutorunTool.Navigation.RouteId";
    private const string NavigationResultCodeKey = "UnityAutorunTool.Navigation.ResultCode";
    private const string NavigationResultMessageKey = "UnityAutorunTool.Navigation.ResultMessage";
    private const string NavigationStartedAtKey = "UnityAutorunTool.Navigation.StartedAt";
    private const string NavigationCompletedAtKey = "UnityAutorunTool.Navigation.CompletedAt";

    public static bool HasPending => GetBool(PendingKey);
    public static bool HasActiveRequest => GetBool(ActiveRequestKey);
    public static bool HasTrackedNavigation => GetBool(TrackedKey);
    public static string TargetViewId => GetString(TargetViewIdKey);
    public static string TargetName => GetString(TargetNameKey);
    public static string ActiveTargetViewId => GetString(ActiveTargetViewIdKey);
    public static string ActiveTargetName => GetString(ActiveTargetNameKey);
    public static int RunId => GetInt(RunIdKey);
    public static string NavigationId => GetString(NavigationIdKey);
    public static string NavigationStatus => GetString(NavigationStatusKey);
    public static string NavigationPhase => GetString(NavigationPhaseKey);
    public static string NavigationTargetViewId => GetString(NavigationTargetViewIdKey);
    public static string NavigationTargetName => GetString(NavigationTargetNameKey);
    public static string NavigationTargetRuntimeToken => GetString(NavigationTargetRuntimeTokenKey);
    public static string NavigationRouteId => GetString(NavigationRouteIdKey);
    public static string NavigationResultCode => GetString(NavigationResultCodeKey);
    public static string NavigationResultMessage => GetString(NavigationResultMessageKey);
    public static bool IsTrackedTerminal => IsTerminalStatus(NavigationStatus);
    public static long NavigationElapsedMilliseconds
    {
        get
        {
            long startedAt = GetLong(NavigationStartedAtKey);
            if (startedAt <= 0)
            {
                return 0;
            }

            long completedAt = GetLong(NavigationCompletedAtKey);
            long end = completedAt > 0 ? completedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Math.Max(0, end - startedAt);
        }
    }

    public static int SavePending(NavigationAutoRunOption target, int runId)
    {
        EditorPrefs.SetBool(PendingKey, true);
        EditorPrefs.SetString(TargetViewIdKey, target.ViewId);
        EditorPrefs.SetString(TargetNameKey, target.Name);
        EditorPrefs.SetInt(RunIdKey, runId);
        SessionState.SetBool(PendingKey, true);
        SessionState.SetString(TargetViewIdKey, target.ViewId);
        SessionState.SetString(TargetNameKey, target.Name);
        SessionState.SetInt(RunIdKey, runId);
        return runId;
    }

    public static void StartTracked(string navigationId, NavigationAutoRunOption target, int runId, string phase)
    {
        ClearTracked();
        SetBool(TrackedKey, true);
        SetString(NavigationIdKey, navigationId);
        SetString(NavigationStatusKey, StatusRunning);
        SetString(NavigationPhaseKey, phase);
        SetString(NavigationTargetViewIdKey, target?.ViewId);
        SetString(NavigationTargetNameKey, target?.Name);
        SetString(NavigationTargetRuntimeTokenKey, target?.RuntimeToken);
        SetString(NavigationStartedAtKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        SavePending(target, runId);
    }

    public static void MarkActiveRequest(NavigationAutoRunPlan plan)
    {
        EditorPrefs.SetBool(ActiveRequestKey, true);
        SessionState.SetBool(ActiveRequestKey, true);
        string targetViewId = plan?.ToViewId ?? "";
        string targetName = string.IsNullOrEmpty(TargetName) ? targetViewId : TargetName;
        EditorPrefs.SetString(ActiveTargetViewIdKey, targetViewId);
        EditorPrefs.SetString(ActiveTargetNameKey, targetName);
        SessionState.SetString(ActiveTargetViewIdKey, targetViewId);
        SessionState.SetString(ActiveTargetNameKey, targetName);
        SetTrackedProgress("navigating", plan?.RouteId);
    }

    public static void SetTrackedProgress(string phase, string routeId = null, string message = null)
    {
        if (!HasTrackedNavigation || IsTrackedTerminal)
        {
            return;
        }

        SetString(NavigationStatusKey, StatusRunning);
        SetString(NavigationPhaseKey, phase);
        if (!string.IsNullOrEmpty(routeId))
        {
            SetString(NavigationRouteIdKey, routeId);
        }

        if (!string.IsNullOrEmpty(message))
        {
            SetString(NavigationResultMessageKey, message);
        }
    }

    public static void CompleteTracked(AutoRunBridgeResponse response)
    {
        if (!HasTrackedNavigation || IsTrackedTerminal)
        {
            return;
        }

        string status = response != null && response.ok ? StatusSucceeded : StatusFailed;
        if (response != null && response.code == "navigation_canceled")
        {
            status = StatusCanceled;
        }

        SetString(NavigationStatusKey, status);
        SetString(NavigationPhaseKey, status);
        SetString(NavigationResultCodeKey, response?.code ?? "navigation_no_response");
        SetString(NavigationResultMessageKey, response?.message ?? "Navigation completed without a response.");
        SetString(NavigationCompletedAtKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    public static void FailTracked(string code, string message)
    {
        CompleteTracked(AutoRunBridgeResponses.Fail(NavigationId, code, message));
    }

    public static void CancelTracked(string message)
    {
        CompleteTracked(AutoRunBridgeResponses.Fail(
            NavigationId,
            "navigation_canceled",
            string.IsNullOrEmpty(message) ? "Navigation canceled." : message
        ));
    }

    public static bool MatchesNavigationId(string navigationId)
    {
        return string.IsNullOrEmpty(navigationId)
            || string.Equals(NavigationId, navigationId, StringComparison.Ordinal);
    }

    public static bool IsTerminalStatus(string status)
    {
        return status == StatusSucceeded || status == StatusFailed || status == StatusCanceled;
    }

    public static void ClearPending()
    {
        EditorPrefs.SetBool(PendingKey, false);
        EditorPrefs.SetString(TargetViewIdKey, "");
        EditorPrefs.SetString(TargetNameKey, "");
        EditorPrefs.SetInt(RunIdKey, 0);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetString(TargetViewIdKey, "");
        SessionState.SetString(TargetNameKey, "");
        SessionState.SetInt(RunIdKey, 0);
    }

    public static void ClearActiveRequest()
    {
        EditorPrefs.SetBool(ActiveRequestKey, false);
        EditorPrefs.SetString(ActiveTargetViewIdKey, "");
        EditorPrefs.SetString(ActiveTargetNameKey, "");
        SessionState.SetBool(ActiveRequestKey, false);
        SessionState.SetString(ActiveTargetViewIdKey, "");
        SessionState.SetString(ActiveTargetNameKey, "");
    }

    private static void ClearTracked()
    {
        SetBool(TrackedKey, false);
        SetString(NavigationIdKey, "");
        SetString(NavigationStatusKey, "");
        SetString(NavigationPhaseKey, "");
        SetString(NavigationTargetViewIdKey, "");
        SetString(NavigationTargetNameKey, "");
        SetString(NavigationTargetRuntimeTokenKey, "");
        SetString(NavigationRouteIdKey, "");
        SetString(NavigationResultCodeKey, "");
        SetString(NavigationResultMessageKey, "");
        SetString(NavigationStartedAtKey, "");
        SetString(NavigationCompletedAtKey, "");
    }

    private static bool GetBool(string key)
    {
        return EditorPrefs.GetBool(key, false) || SessionState.GetBool(key, false);
    }

    private static void SetBool(string key, bool value)
    {
        EditorPrefs.SetBool(key, value);
        SessionState.SetBool(key, value);
    }

    private static void SetString(string key, string value)
    {
        value = value ?? "";
        EditorPrefs.SetString(key, value);
        SessionState.SetString(key, value);
    }

    private static string GetString(string key)
    {
        string value = EditorPrefs.GetString(key, "");
        return string.IsNullOrEmpty(value) ? SessionState.GetString(key, "") : value;
    }

    private static int GetInt(string key)
    {
        int value = EditorPrefs.GetInt(key, 0);
        return value == 0 ? SessionState.GetInt(key, 0) : value;
    }

    private static long GetLong(string key)
    {
        string value = GetString(key);
        return long.TryParse(value, out long result) ? result : 0;
    }
}
