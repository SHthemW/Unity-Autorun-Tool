using System;
using System.IO;

public static class McpInstallService
{
    private const string BeginMarker = "# BEGIN Unity AutoRun MCP";
    private const string EndMarker = "# END Unity AutoRun MCP";
    private const string ClaudeDesktopConfigFileName = "claude_desktop_config.json";

    public static bool Install(string targetFolder, out string message)
    {
        try
        {
            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                message = "Select an existing .codex folder, Claude Code project .claude folder, or Claude Desktop config folder first.";
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
                string path = InstallClaudeCode(targetFolder, config);
                string legacyPath = Path.Combine(targetFolder, ".mcp.json");
                string legacyMessage = File.Exists(legacyPath)
                    ? " A legacy config remains unchanged at "
                        + legacyPath
                        + "; Claude Code does not load project MCP servers from that location."
                    : "";
                message = "Installed Unity AutoRun MCP for Claude Code: "
                    + path
                    + "."
                    + legacyMessage
                    + " Restart Claude Code and approve the project-scoped server when prompted.";
                return true;
            }

            if (File.Exists(Path.Combine(targetFolder, ClaudeDesktopConfigFileName)))
            {
                string path = InstallClaudeDesktop(targetFolder, config);
                message = "Installed Unity AutoRun MCP for Claude Desktop: "
                    + path
                    + ". Restart Claude Desktop to reconnect the server.";
                return true;
            }

            message = "Target folder must be named .codex, be a Claude Code project .claude folder, or contain "
                + ClaudeDesktopConfigFileName
                + ".";
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

    private static string InstallClaudeCode(string claudeFolder, McpInstallConfig config)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(claudeFolder));
        if (directory.Parent == null)
        {
            throw new InvalidOperationException(
                "Cannot resolve the Claude Code project root for: " + claudeFolder);
        }

        string userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        string userClaudeFolder = string.IsNullOrEmpty(userProfile)
            ? ""
            : Path.Combine(userProfile, ".claude");
        if (!string.IsNullOrEmpty(userClaudeFolder)
            && string.Equals(
                directory.FullName.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Path.GetFullPath(userClaudeFolder).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The user-level .claude folder cannot hold a project-scoped .mcp.json. Select the current Claude Code project's .claude folder instead.");
        }

        string path = Path.Combine(directory.Parent.FullName, ".mcp.json");
        return InstallClaudeJson(path, config);
    }

    private static string InstallClaudeDesktop(
        string configFolder,
        McpInstallConfig config)
    {
        string path = Path.Combine(configFolder, ClaudeDesktopConfigFileName);
        return InstallClaudeJson(path, config);
    }

    private static string InstallClaudeJson(string path, McpInstallConfig config)
    {
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
