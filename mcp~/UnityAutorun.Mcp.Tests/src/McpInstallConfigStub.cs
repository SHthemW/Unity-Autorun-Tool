using System;

public sealed class McpInstallConfig
{
    public const string ServerName = "unity-autorun";

    public static McpInstallConfig TestInstance;

    public string PublishedDllPath = "";
    public string ClaudeServerJson = "{}";

    public static McpInstallConfig Create()
    {
        if (TestInstance == null)
        {
            throw new InvalidOperationException(
                "McpInstallConfig.TestInstance must be set by the test fixture.");
        }

        return TestInstance;
    }

    public string ToCodexTomlBlock()
    {
        return "[mcp_servers.unity_autorun]";
    }

    public string ToClaudeServerJson()
    {
        return ClaudeServerJson;
    }
}
