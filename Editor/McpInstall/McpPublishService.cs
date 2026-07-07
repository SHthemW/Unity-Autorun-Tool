using System;
using System.Diagnostics;
using System.IO;
using System.Text;

public static class McpPublishService
{
    public static bool PublishCurrentVersion(out string message)
    {
        try
        {
            string projectPath = McpInstallConfig.GetMcpProjectPath();
            string projectDirectory = Path.GetDirectoryName(projectPath);
            string publishedDllPath = McpInstallConfig.GetPublishedDllPath();

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

                if (!process.WaitForExit(120000))
                {
                    process.Kill();
                    message = "MCP publish timed out after 120 seconds.";
                    return false;
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    message = "MCP publish failed: " + TrimOutput(error.ToString(), output.ToString());
                    return false;
                }
            }

            if (!File.Exists(publishedDllPath))
            {
                message = "MCP publish finished but output was not found: " + publishedDllPath;
                return false;
            }

            message = "Published MCP: " + publishedDllPath;
            return true;
        }
        catch (Exception ex)
        {
            message = "MCP publish failed: " + ex.Message;
            return false;
        }
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
