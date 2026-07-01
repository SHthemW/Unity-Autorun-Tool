using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class McpInstallConfig
{
    public const string ServerName = "unity-autorun";

    public string Command;
    public List<string> Args;
    public string Cwd;
    public string Host;
    public string Port;

    public static McpInstallConfig Create()
    {
        string projectPath = GetMcpProjectPath();
        string projectDirectory = Path.GetDirectoryName(projectPath);

        return new McpInstallConfig
        {
            Command = "dotnet",
            Args = new List<string> { "run", "--no-build", "--project", projectPath, "--", "mcp" },
            Cwd = projectDirectory,
            Host = "127.0.0.1",
            Port = AutoRunBridgeServer.DefaultPort.ToString()
        };
    }

    public static string GetMcpProjectPath()
    {
        return FindMcpProjectPath();
    }

    public static string GetToolRootDirectory()
    {
        string projectPath = GetMcpProjectPath();
        string projectDirectory = Path.GetDirectoryName(projectPath);
        DirectoryInfo mcpFolder = Directory.GetParent(projectDirectory);
        if (mcpFolder == null || mcpFolder.Parent == null)
        {
            throw new InvalidOperationException("Cannot resolve Unity AutoRun tool root directory.");
        }

        return mcpFolder.Parent.FullName;
    }

    public string ToCodexTomlBlock()
    {
        var builder = new StringBuilder();
        builder.AppendLine("[mcp_servers.unity_autorun]");
        builder.AppendLine("command = " + TomlString(Command));
        builder.AppendLine("args = [" + TomlStringList(Args) + "]");
        builder.AppendLine("cwd = " + TomlString(Cwd));
        builder.AppendLine("startup_timeout_sec = 20");
        builder.AppendLine("tool_timeout_sec = 60");
        builder.AppendLine("enabled = true");
        builder.AppendLine();
        builder.AppendLine("[mcp_servers.unity_autorun.env]");
        builder.AppendLine("UNITY_AUTORUN_HOST = " + TomlString(Host));
        builder.AppendLine("UNITY_AUTORUN_PORT = " + TomlString(Port));
        return builder.ToString().TrimEnd();
    }

    public string ToClaudeServerJson()
    {
        var builder = new StringBuilder();
        builder.Append("{");
        builder.Append("\"command\":\"").Append(JsonEscape(Command)).Append("\",");
        builder.Append("\"args\":").Append(JsonStringArray(Args)).Append(",");
        builder.Append("\"env\":{");
        builder.Append("\"UNITY_AUTORUN_HOST\":\"").Append(JsonEscape(Host)).Append("\",");
        builder.Append("\"UNITY_AUTORUN_PORT\":\"").Append(JsonEscape(Port)).Append("\"");
        builder.Append("}");
        builder.Append("}");
        return builder.ToString();
    }

    private static string FindMcpProjectPath()
    {
        string[] matches = Directory.GetFiles(UnityEngine.Application.dataPath, "UnityAutorun.Mcp.csproj", SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException("Cannot find UnityAutorun.Mcp.csproj under Assets.");
        }

        return Path.GetFullPath(matches[0]).Replace('\\', '/');
    }

    private static string TomlStringList(List<string> values)
    {
        var parts = new List<string>();
        foreach (string value in values)
        {
            parts.Add(TomlString(value));
        }

        return string.Join(", ", parts.ToArray());
    }

    private static string TomlString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string JsonStringArray(List<string> values)
    {
        var parts = new List<string>();
        foreach (string value in values)
        {
            parts.Add("\"" + JsonEscape(value) + "\"");
        }

        return "[" + string.Join(",", parts.ToArray()) + "]";
    }

    private static string JsonEscape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
