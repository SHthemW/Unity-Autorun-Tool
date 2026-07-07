using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

public static class McpProcessService
{
    public static List<McpProcessInfo> ListUnityAutorunProcesses()
    {
        try
        {
            return ListFromPowerShell();
        }
        catch
        {
            return new List<McpProcessInfo>();
        }
    }

    private static List<McpProcessInfo> ListFromPowerShell()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            return new List<McpProcessInfo>();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \""
                + "$all=Get-CimInstance Win32_Process;"
                + "$p=$all | Where-Object {($_.Name -eq 'UnityAutorun.Mcp.exe' -and $_.CommandLine -like '* mcp*')"
                + " -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*UnityAutorun.Mcp.dll*' -and $_.CommandLine -like '* mcp*')};"
                + "$p | ForEach-Object {"
                + "$item=$_;$parent=$all | Where-Object {$_.ProcessId -eq $item.ParentProcessId} | Select-Object -First 1;"
                + "$ai=$parent;while($ai -and $ai.Name -notmatch 'codex|claude|cursor|code|windsurf'){"
                + "$ai=$all | Where-Object {$_.ProcessId -eq $ai.ParentProcessId} | Select-Object -First 1};"
                + "$aiPid=if($ai){$ai.ProcessId}else{0};$aiName=if($ai){$ai.Name}else{'unknown'};"
                + "$parentName=if($parent){$parent.Name}else{'unknown'};"
                + "$exe=if($item.ExecutablePath){$item.ExecutablePath}else{''};"
                + "Write-Output ($item.ProcessId.ToString()+'|'+$item.Name+'|'+$item.ParentProcessId.ToString()+'|'+$parentName+'|'+$aiPid.ToString()+'|'+$aiName+'|'+$exe+'|'+$item.CommandLine)}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
        return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(info => info != null)
            .OrderBy(info => info.ParentProcessName)
            .ThenBy(info => info.ParentProcessId)
            .ThenBy(info => info.ProcessId)
            .ToList();
    }

    private static McpProcessInfo ParseLine(string line)
    {
        string[] parts = line.Split(new[] { '|' }, 8);
        if (parts.Length < 8 || !int.TryParse(parts[0], out int processId))
        {
            return null;
        }

        int.TryParse(parts[2], out int parentId);
        int.TryParse(parts[4], out int aiId);
        return new McpProcessInfo
        {
            ProcessId = processId,
            ProcessName = Safe(parts[1]),
            ParentProcessId = parentId,
            ParentProcessName = Safe(parts[3]),
            AiProcessId = aiId,
            AiProcessName = Safe(parts[5]),
            PublishedAt = GetPublishedAt(parts[1], parts[6], parts[7]),
            CommandLine = Shorten(CleanCommandLine(parts[7])),
        };
    }

    private static string GetPublishedAt(string processName, string executablePath, string commandLine)
    {
        string path = ResolveMainProgramPath(processName, executablePath, commandLine);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return "unknown";
        }

        return System.IO.File.GetLastWriteTime(path).ToString("MMdd HHmm");
    }

    private static string ResolveMainProgramPath(string processName, string executablePath, string commandLine)
    {
        if (string.Equals(processName, "UnityAutorun.Mcp.exe", StringComparison.OrdinalIgnoreCase)
            && System.IO.File.Exists(executablePath))
        {
            return executablePath;
        }

        string fromCommandLine = MatchUnityAutorunBinary(commandLine);
        if (!string.IsNullOrEmpty(fromCommandLine))
        {
            return fromCommandLine;
        }

        return executablePath;
    }

    private static string MatchUnityAutorunBinary(string commandLine)
    {
        Match match = Regex.Match(
            Safe(commandLine),
            "((?:\"[^\"]*UnityAutorun\\.Mcp\\.(?:dll|exe)\")|(?:\\S*UnityAutorun\\.Mcp\\.(?:dll|exe)))",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Value.Trim('"') : "";
    }

    private static string Shorten(string value)
    {
        value = Safe(value);
        return value.Length <= 140 ? value : value.Substring(0, 137) + "...";
    }

    private static string CleanCommandLine(string value)
    {
        return Regex.Replace(Safe(value), "\\s+", " ");
    }

    private static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}
