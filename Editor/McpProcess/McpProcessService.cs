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
                + " -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*UnityAutorun.Mcp.csproj*' -and $_.CommandLine -like '*-- mcp*')};"
                + "$p | ForEach-Object {"
                + "$item=$_;$parent=$all | Where-Object {$_.ProcessId -eq $item.ParentProcessId} | Select-Object -First 1;"
                + "$ai=$parent;while($ai -and $ai.Name -notmatch 'codex|claude|cursor|code|windsurf'){"
                + "$ai=$all | Where-Object {$_.ProcessId -eq $ai.ParentProcessId} | Select-Object -First 1};"
                + "$aiPid=if($ai){$ai.ProcessId}else{0};$aiName=if($ai){$ai.Name}else{'unknown'};"
                + "$parentName=if($parent){$parent.Name}else{'unknown'};"
                + "Write-Output ($item.ProcessId.ToString()+'|'+$item.Name+'|'+$item.ParentProcessId.ToString()+'|'+$parentName+'|'+$aiPid.ToString()+'|'+$aiName+'|'+$item.CommandLine)}\"",
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
        string[] parts = line.Split(new[] { '|' }, 7);
        if (parts.Length < 7 || !int.TryParse(parts[0], out int processId))
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
            CommandLine = Shorten(CleanCommandLine(parts[6])),
        };
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
