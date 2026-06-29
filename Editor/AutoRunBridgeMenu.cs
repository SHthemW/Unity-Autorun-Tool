using UnityEditor;

[InitializeOnLoad]
public static class AutoRunBridgeMenu
{
    private const string BridgeEnabledKey = "UnityAutorunTool.BridgeEnabled";
    private static readonly AutoRunBridgeServer Server = new();

    static AutoRunBridgeMenu()
    {
        EditorApplication.delayCall += RestoreBridgeIfNeeded;
        AssemblyReloadEvents.beforeAssemblyReload += StopBeforeAssemblyReload;
    }

    [MenuItem("Window/Auto Run MCP Bridge/Start")]
    public static void Start()
    {
        SessionState.SetBool(BridgeEnabledKey, true);
        Server.Start();
    }

    [MenuItem("Window/Auto Run MCP Bridge/Stop")]
    public static void Stop()
    {
        SessionState.SetBool(BridgeEnabledKey, false);
        Server.Stop();
    }

    private static void RestoreBridgeIfNeeded()
    {
        if (SessionState.GetBool(BridgeEnabledKey, false))
        {
            Server.Start();
        }
    }

    private static void StopBeforeAssemblyReload()
    {
        Server.Stop();
    }
}
