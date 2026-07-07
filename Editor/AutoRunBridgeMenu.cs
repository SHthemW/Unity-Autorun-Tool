using UnityEditor;

public static class AutoRunBridgeMenu
{
    [MenuItem("Window/Auto Run MCP Bridge/Start")]
    public static void Start()
    {
        AutoRunBridgeController.Start();
    }

    [MenuItem("Window/Auto Run MCP Bridge/Stop")]
    public static void Stop()
    {
        AutoRunBridgeController.Stop();
    }
}
