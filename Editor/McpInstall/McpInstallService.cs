using System;
using System.IO;

public static class McpInstallService
{
    private const string BeginMarker = "# BEGIN Unity AutoRun MCP";
    private const string EndMarker = "# END Unity AutoRun MCP";

    public static bool Install(string targetFolder, out string message)
    {
        try
        {
            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                message = "Select an existing .codex or .claude folder first.";
                return false;
            }

            string folderName = new DirectoryInfo(targetFolder).Name;
            McpInstallConfig config = McpInstallConfig.Create();
            if (!File.Exists(config.PublishedDllPath))
            {
                message = "Publish MCP first. Missing published server: " + config.PublishedDllPath;
                return false;
            }

            if (string.Equals(folderName, ".codex", StringComparison.OrdinalIgnoreCase))
            {
                string path = InstallCodex(targetFolder, config);
                message = "Installed Unity AutoRun MCP for Codex: " + path;
                return true;
            }

            if (string.Equals(folderName, ".claude", StringComparison.OrdinalIgnoreCase))
            {
                string path = InstallClaude(targetFolder, config);
                message = "Installed Unity AutoRun MCP for Claude: " + path;
                return true;
            }

            message = "Target folder must be named .codex or .claude.";
            return false;
        }
        catch (Exception ex)
        {
            message = "MCP install failed: " + ex.Message;
            return false;
        }
    }

    private static string InstallCodex(string codexFolder, McpInstallConfig config)
    {
        string path = Path.Combine(codexFolder, "config.toml");
        string existing = File.Exists(path) ? File.ReadAllText(path) : "";
        string block = BeginMarker + Environment.NewLine
            + config.ToCodexTomlBlock() + Environment.NewLine
            + EndMarker;
        string updated = ReplaceMarkedBlock(existing, block);
        File.WriteAllText(path, updated);
        return path;
    }

    private static string InstallClaude(string claudeFolder, McpInstallConfig config)
    {
        string path = Path.Combine(claudeFolder, ".mcp.json");
        string serverJson = "\"" + McpInstallConfig.ServerName + "\":" + config.ToClaudeServerJson();
        string updated;

        if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
        {
            updated = "{\n  \"mcpServers\": {\n    " + serverJson + "\n  }\n}\n";
        }
        else
        {
            string existing = File.ReadAllText(path);
            updated = JsonObjectUpdater.UpsertMcpServer(existing, McpInstallConfig.ServerName, serverJson);
        }

        File.WriteAllText(path, updated);
        return path;
    }

    private static string ReplaceMarkedBlock(string existing, string block)
    {
        int begin = existing.IndexOf(BeginMarker, StringComparison.Ordinal);
        int end = existing.IndexOf(EndMarker, StringComparison.Ordinal);
        if (begin >= 0 && end >= begin)
        {
            end += EndMarker.Length;
            return existing.Substring(0, begin).TrimEnd()
                + Environment.NewLine + Environment.NewLine
                + block
                + Environment.NewLine
                + existing.Substring(end).TrimStart();
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return block + Environment.NewLine;
        }

        return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + block + Environment.NewLine;
    }
}
