using UnityEditor;

public static class NavigationAutoRunSession
{
    private const string PendingKey = "UnityAutorunTool.Navigation.Pending";
    private const string TargetViewIdKey = "UnityAutorunTool.Navigation.TargetViewId";
    private const string TargetNameKey = "UnityAutorunTool.Navigation.TargetName";
    private const string RunIdKey = "UnityAutorunTool.Navigation.RunId";
    private const string ActiveRequestKey = "UnityAutorunTool.Navigation.ActiveRequest";
    private const string ActiveTargetViewIdKey = "UnityAutorunTool.Navigation.ActiveTargetViewId";
    private const string ActiveTargetNameKey = "UnityAutorunTool.Navigation.ActiveTargetName";

    public static bool HasPending => EditorPrefs.GetBool(PendingKey, false) || SessionState.GetBool(PendingKey, false);
    public static bool HasActiveRequest => EditorPrefs.GetBool(ActiveRequestKey, false) || SessionState.GetBool(ActiveRequestKey, false);
    public static string TargetViewId => GetString(TargetViewIdKey);
    public static string TargetName => GetString(TargetNameKey);
    public static string ActiveTargetViewId => GetString(ActiveTargetViewIdKey);
    public static string ActiveTargetName => GetString(ActiveTargetNameKey);
    public static int RunId => GetInt(RunIdKey);

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
}
