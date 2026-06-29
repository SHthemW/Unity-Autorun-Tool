using UnityEditor;

[InitializeOnLoad]
public static class AutoRunBridgeController
{
    private const string BridgeEnabledKey = "UnityAutorunTool.BridgeEnabled";
    private static readonly AutoRunBridgeServer Server = new();

    public static bool IsEnabled => SessionState.GetBool(BridgeEnabledKey, false);
    public static bool IsRunning => Server.IsRunning;
    public static string Url => $"http://127.0.0.1:{AutoRunBridgeServer.DefaultPort}/";

    static AutoRunBridgeController()
    {
        EditorApplication.delayCall += RestoreBridgeIfNeeded;
        AssemblyReloadEvents.beforeAssemblyReload += StopBeforeAssemblyReload;
    }

    public static void Start()
    {
        SessionState.SetBool(BridgeEnabledKey, true);
        Server.Start();
    }

    public static void Stop()
    {
        SessionState.SetBool(BridgeEnabledKey, false);
        Server.Stop();
    }

    private static void RestoreBridgeIfNeeded()
    {
        if (IsEnabled)
        {
            Server.Start();
        }
    }

    private static void StopBeforeAssemblyReload()
    {
        Server.Stop();
    }
}
