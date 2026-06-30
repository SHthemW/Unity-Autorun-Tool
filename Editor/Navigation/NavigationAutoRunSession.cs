using UnityEditor;

public static class NavigationAutoRunSession
{
    private const string PendingKey = "UnityAutorunTool.Navigation.Pending";
    private const string TargetViewIdKey = "UnityAutorunTool.Navigation.TargetViewId";
    private const string TargetNameKey = "UnityAutorunTool.Navigation.TargetName";
    private const string RunIdKey = "UnityAutorunTool.Navigation.RunId";

    public static bool HasPending => EditorPrefs.GetBool(PendingKey, false) || SessionState.GetBool(PendingKey, false);
    public static string TargetViewId => GetString(TargetViewIdKey);
    public static string TargetName => GetString(TargetNameKey);
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
