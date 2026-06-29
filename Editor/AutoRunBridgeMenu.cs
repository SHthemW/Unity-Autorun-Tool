using UnityEditor;

public static class AutoRunBridgeMenu
{
    private static readonly AutoRunBridgeServer Server = new();

    [MenuItem("Window/Auto Run MCP Bridge/Start")]
    public static void Start()
    {
        Server.Start();
    }

    [MenuItem("Window/Auto Run MCP Bridge/Stop")]
    public static void Stop()
    {
        Server.Stop();
    }
}
