using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

public static class McpPublishService
{
    private const int PublishTimeoutMilliseconds = 120000;
    private const int ProcessExitTimeoutMilliseconds = 5000;
    private const int FileUnlockTimeoutMilliseconds = 5000;
    private const int ReconnectTimeoutMilliseconds = 5000;
    private const int PollIntervalMilliseconds = 250;

    public static bool PublishCurrentVersion(out string message)
    {
        return PublishCurrentVersion(out message, out _);
    }

    public static bool PublishCurrentVersion(out string message, out bool reconnectPending)
    {
        reconnectPending = false;

        try
        {
            string projectPath = McpInstallConfig.GetMcpProjectPath();
            string projectDirectory = Path.GetDirectoryName(projectPath);
            string publishedDllPath = McpInstallConfig.GetPublishedDllPath();

            if (!McpProcessService.TryTerminateUnityAutorunProcesses(
                    publishedDllPath,
                    ProcessExitTimeoutMilliseconds,
                    out List<McpProcessInfo> terminatedProcesses,
                    out string terminationError))
            {
                message = "MCP publish canceled because running MCP processes could not be stopped: "
                    + terminationError;
                return false;
            }

            if (!WaitForFileUnlock(publishedDllPath, FileUnlockTimeoutMilliseconds))
            {
                message = "MCP publish canceled because the output DLL is still locked after stopping "
                    + "the matching MCP processes: " + publishedDllPath;
                return false;
            }

            if (!RunDotnetPublish(projectPath, projectDirectory, out string publishError))
            {
                message = publishError;
                return false;
            }

            if (!File.Exists(publishedDllPath))
            {
                message = "MCP publish finished but output was not found: " + publishedDllPath;
                return false;
            }

            List<McpProcessInfo> reconnectedProcesses = WaitForReconnect(
                publishedDllPath,
                terminatedProcesses.Count,
                ReconnectTimeoutMilliseconds);
            reconnectPending = terminatedProcesses.Count > reconnectedProcesses.Count;
            message = BuildSuccessMessage(
                publishedDllPath,
                terminatedProcesses,
                reconnectedProcesses,
                reconnectPending);
            return true;
        }
        catch (Exception ex)
        {
            message = "MCP publish failed: " + ex.Message;
            return false;
        }
    }

    private static bool RunDotnetPublish(
        string projectPath,
        string projectDirectory,
        out string errorMessage)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish \"" + projectPath + "\" -c Release --nologo",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        using (Process process = Process.Start(startInfo))
        {
            process.OutputDataReceived += (sender, args) => AppendLine(output, args.Data);
            process.ErrorDataReceived += (sender, args) => AppendLine(error, args.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(PublishTimeoutMilliseconds))
            {
                process.Kill();
                errorMessage = "MCP publish timed out after "
                    + PublishTimeoutMilliseconds / 1000 + " seconds.";
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                errorMessage = "MCP publish failed: " + TrimOutput(error.ToString(), output.ToString());
                return false;
            }
        }

        errorMessage = "";
        return true;
    }

    private static bool WaitForFileUnlock(string filePath, int timeoutMilliseconds)
    {
        if (!File.Exists(filePath))
        {
            return true;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        do
        {
            try
            {
                using (File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds);

        return false;
    }

    private static List<McpProcessInfo> WaitForReconnect(
        string publishedDllPath,
        int expectedProcessCount,
        int timeoutMilliseconds)
    {
        if (expectedProcessCount == 0)
        {
            return new List<McpProcessInfo>();
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<McpProcessInfo> processes;
        do
        {
            processes = McpProcessService.ListUnityAutorunProcesses(publishedDllPath);
            if (processes.Count >= expectedProcessCount)
            {
                return processes;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds);

        return McpProcessService.ListUnityAutorunProcesses(publishedDllPath);
    }

    private static string BuildSuccessMessage(
        string publishedDllPath,
        List<McpProcessInfo> terminatedProcesses,
        List<McpProcessInfo> reconnectedProcesses,
        bool reconnectPending)
    {
        var builder = new StringBuilder();
        builder.Append("Published MCP: ").Append(publishedDllPath);
        if (terminatedProcesses.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        builder.Append("Stopped ")
            .Append(terminatedProcesses.Count)
            .Append(" running MCP process(es): ")
            .Append(FormatProcessIds(terminatedProcesses))
            .Append('.');
        builder.AppendLine();

        if (reconnectPending)
        {
            builder.Append("Detected ")
                .Append(reconnectedProcesses.Count)
                .Append(" of ")
                .Append(terminatedProcesses.Count)
                .Append(" expected MCP reconnect(s). Restart or reconnect the affected AI client ")
                .Append("session(s) before using MCP.");
        }
        else
        {
            builder.Append("Detected ")
                .Append(reconnectedProcesses.Count)
                .Append(" running MCP process(es) after publish: ")
                .Append(FormatProcessIds(reconnectedProcesses))
                .Append('.');
        }

        return builder.ToString();
    }

    private static string FormatProcessIds(List<McpProcessInfo> processes)
    {
        var values = new List<string>();
        foreach (McpProcessInfo process in processes)
        {
            values.Add(process.ProcessId.ToString());
        }

        return string.Join(", ", values.ToArray());
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (line != null)
        {
            builder.AppendLine(line);
        }
    }

    private static string TrimOutput(string primary, string fallback)
    {
        string value = string.IsNullOrWhiteSpace(primary) ? fallback : primary;
        value = string.IsNullOrWhiteSpace(value) ? "unknown error" : value.Trim();
        return value.Length <= 500 ? value : value.Substring(value.Length - 500);
    }
}
