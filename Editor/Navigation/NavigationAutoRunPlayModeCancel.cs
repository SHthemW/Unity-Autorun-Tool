using UnityEditor;

[InitializeOnLoad]
public static class NavigationAutoRunPlayModeCancel
{
    private static bool _canceling;

    static NavigationAutoRunPlayModeCancel()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode
            && state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        if (_canceling && state == PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        if (!NavigationAutoRunSession.HasPending && !NavigationAutoRunSession.HasActiveRequest)
        {
            return;
        }

        _canceling = true;
        Log("Play Mode stopped. Canceling navigation task. state=" + state, AutoRunLogLevel.Warning);
        NavigationAutoRunSession.ClearPending();
        NavigationAutoRunSession.ClearActiveRequest();
        NavigationAutoRunCancelRequest.Start(OnCancelCompleted);
        AutoRunWindow.RepaintAllNavigationWindows();
    }

    private static void OnCancelCompleted(AutoRunBridgeResponse response)
    {
        _canceling = false;
        if (response == null)
        {
            Log("Cancel completed with no response.", AutoRunLogLevel.Error);
            return;
        }

        Log("Cancel response ok=" + response.ok + ", code=" + response.code + ", message=" + response.message,
            response.ok ? AutoRunLogLevel.Info : AutoRunLogLevel.Error);
    }

    private static void Log(string message, AutoRunLogLevel level = AutoRunLogLevel.Debug)
    {
        AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message, level);
    }
}
