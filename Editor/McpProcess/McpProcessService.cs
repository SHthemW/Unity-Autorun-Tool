using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class McpProcessService
{
    private const int MaxMcpVersionLength = 36;
    private static readonly Dictionary<string, string> McpVersionCache =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

    public static List<McpProcessInfo> ListUnityAutorunProcesses(string binaryPath)
    {
        return ListUnityAutorunProcesses()
            .Where(info => IsSameBinaryPath(info.BinaryPath, binaryPath))
            .ToList();
    }

    public static bool TryTerminateUnityAutorunProcesses(
        string binaryPath,
        int waitForExitMilliseconds,
        out List<McpProcessInfo> terminatedProcesses,
        out string error)
    {
        List<McpProcessInfo> matchingProcesses = ListUnityAutorunProcesses(binaryPath);
        terminatedProcesses = new List<McpProcessInfo>();
        var failures = new List<string>();

        foreach (McpProcessInfo processInfo in matchingProcesses)
        {
            if (TryTerminateProcess(processInfo, waitForExitMilliseconds, out string processError))
            {
                terminatedProcesses.Add(processInfo);
            }
            else
            {
                failures.Add(processError);
            }
        }

        error = string.Join("; ", failures.ToArray());
        return failures.Count == 0;
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
                + "$ai=$parent;while($ai -and $ai.Name -notmatch '^(codex(?:-cli)?|claude(?:-code)?|cursor|code(?: - insiders)?|windsurf)(\\.exe)?$'){"
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
        string binaryPath = NormalizePath(ResolveMainProgramPath(parts[1], parts[6], parts[7]));
        return new McpProcessInfo
        {
            ProcessId = processId,
            ProcessName = Safe(parts[1]),
            ParentProcessId = parentId,
            ParentProcessName = Safe(parts[3]),
            AiProcessId = aiId,
            AiProcessName = Safe(parts[5]),
            McpVersion = GetMcpVersion(binaryPath),
            PublishedAt = GetPublishedAt(binaryPath),
            BinaryPath = binaryPath,
            CommandLine = Shorten(CleanCommandLine(parts[7])),
        };
    }

    private static bool TryTerminateProcess(
        McpProcessInfo processInfo,
        int waitForExitMilliseconds,
        out string error)
    {
        try
        {
            using Process process = Process.GetProcessById(processInfo.ProcessId);
            if (!process.HasExited)
            {
                process.Kill();
            }

            if (!process.WaitForExit(waitForExitMilliseconds))
            {
                error = "PID " + processInfo.ProcessId + " did not exit within "
                    + waitForExitMilliseconds + " ms";
                return false;
            }

            error = "";
            return true;
        }
        catch (ArgumentException)
        {
            // 进程可能在枚举结束后自行退出，此时目标文件同样已经释放。
            error = "";
            return true;
        }
        catch (InvalidOperationException)
        {
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = "PID " + processInfo.ProcessId + " could not be terminated: " + ex.Message;
            return false;
        }
    }

    private static string GetPublishedAt(string binaryPath)
    {
        return string.IsNullOrEmpty(binaryPath) || !File.Exists(binaryPath)
            ? "unknown"
            : File.GetLastWriteTime(binaryPath).ToString("MMdd HHmm");
    }

    private static string GetMcpVersion(string binaryPath)
    {
        if (string.IsNullOrEmpty(binaryPath) || !File.Exists(binaryPath))
        {
            return "unknown";
        }

        try
        {
            var file = new FileInfo(binaryPath);
            string cacheKey = binaryPath
                + "|"
                + file.Length
                + "|"
                + file.LastWriteTimeUtc.Ticks;
            if (McpVersionCache.TryGetValue(cacheKey, out string cachedVersion))
            {
                return cachedVersion;
            }

            string version = NormalizeMcpVersion(ReadFileVersion(binaryPath));
            if (string.IsNullOrEmpty(version))
            {
                version = "unknown";
            }

            McpVersionCache[cacheKey] = version;
            return version;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string ReadFileVersion(string executablePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
            return !string.IsNullOrWhiteSpace(info.ProductVersion)
                ? info.ProductVersion
                : info.FileVersion;
        }
        catch
        {
            return "";
        }
    }

    private static string NormalizeMcpVersion(string value)
    {
        string compact = Regex.Replace(value ?? "", "\\s+", " ").Trim();
        Match match = Regex.Match(
            compact,
            "(?<!\\d)(\\d+(?:\\.\\d+)+(?:-[0-9A-Za-z.-]+)?)",
            RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return compact.Length <= MaxMcpVersionLength
            ? compact
            : compact.Substring(0, MaxMcpVersionLength - 3) + "...";
    }

    private static string ResolveMainProgramPath(string processName, string executablePath, string commandLine)
    {
        if (string.Equals(processName, "UnityAutorun.Mcp.exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(executablePath))
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

    private static bool IsSameBinaryPath(string left, string right)
    {
        string normalizedLeft = NormalizePath(left);
        string normalizedRight = NormalizePath(right);
        if (string.IsNullOrEmpty(normalizedLeft) || string.IsNullOrEmpty(normalizedRight))
        {
            return false;
        }

        StringComparison comparison = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(normalizedLeft, normalizedRight, comparison);
    }

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        value = value.Trim().Trim('"');
        try
        {
            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
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
