using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.PackageManager;

public sealed class McpInstallConfig
{
    public const string PackageName = "com.shthemw.unity-autorun-tool";
    public const string ServerName = "unity-autorun";

    public string Command;
    public List<string> Args;
    public string Cwd;
    public string ProjectRoot;
    public string ToolRoot;
    public string PublishedDllPath;

    public static McpInstallConfig Create()
    {
        string toolRoot = GetToolRootDirectory();
        string publishedDllPath = GetPublishedDllPath();

        return new McpInstallConfig
        {
            Command = "dotnet",
            Args = new List<string> { publishedDllPath, "mcp" },
            Cwd = toolRoot,
            ProjectRoot = GetProjectRootDirectory(),
            ToolRoot = toolRoot,
            PublishedDllPath = publishedDllPath
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

    public static string GetProjectRootDirectory()
    {
        DirectoryInfo projectRoot = Directory.GetParent(UnityEngine.Application.dataPath);
        if (projectRoot == null)
        {
            throw new InvalidOperationException("Cannot resolve the Unity project root directory.");
        }

        return projectRoot.FullName;
    }

    public static string GetPublishedDllPath()
    {
        string projectPath = GetMcpProjectPath();
        string projectDirectory = Path.GetDirectoryName(projectPath);
        return Path.Combine(projectDirectory, "bin", "Release", "net8.0", "publish", "UnityAutorun.Mcp.dll");
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
        builder.AppendLine("UNITY_AUTORUN_PROJECT_ROOT = " + TomlString(ProjectRoot));
        builder.AppendLine("UNITY_AUTORUN_TOOL_ROOT = " + TomlString(ToolRoot));
        return builder.ToString().TrimEnd();
    }

    public string ToClaudeServerJson()
    {
        var builder = new StringBuilder();
        builder.Append("{");
        builder.Append("\"command\":\"").Append(JsonEscape(Command)).Append("\",");
        builder.Append("\"args\":").Append(JsonStringArray(Args)).Append(",");
        builder.Append("\"env\":{");
        builder.Append("\"UNITY_AUTORUN_PROJECT_ROOT\":\"").Append(JsonEscape(ProjectRoot)).Append("\",");
        builder.Append("\"UNITY_AUTORUN_TOOL_ROOT\":\"").Append(JsonEscape(ToolRoot)).Append("\"");
        builder.Append("}");
        builder.Append("}");
        return builder.ToString();
    }

    private static string FindMcpProjectPath()
    {
        string packageRoot = FindPackageRootDirectory();
        if (!string.IsNullOrEmpty(packageRoot))
        {
            string packageProjectPath = Path.Combine(packageRoot, "mcp~", "UnityAutorun.Mcp", "UnityAutorun.Mcp.csproj");
            if (File.Exists(packageProjectPath))
            {
                return Path.GetFullPath(packageProjectPath).Replace('\\', '/');
            }
        }

        string[] matches = Directory.GetFiles(UnityEngine.Application.dataPath, "UnityAutorun.Mcp.csproj", SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException("Cannot find UnityAutorun.Mcp.csproj under the Unity AutoRun package or Assets.");
        }

        return Path.GetFullPath(matches[0]).Replace('\\', '/');
    }

    private static string FindPackageRootDirectory()
    {
        PackageInfo packageInfo = PackageInfo.FindForAssetPath("Packages/" + PackageName + "/package.json");
        if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return null;
        }

        return Path.GetFullPath(packageInfo.resolvedPath);
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
