using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public static class McpTerminalService
{
    public static bool OpenToolRootTerminal(out string message)
    {
        try
        {
            string root = McpInstallConfig.GetToolRootDirectory();
            if (!Directory.Exists(root))
            {
                message = "Tool root directory does not exist: " + root;
                return false;
            }

            OpenTerminal(root);
            message = "Opened terminal at: " + root;
            return true;
        }
        catch (Exception ex)
        {
            message = "Open terminal failed: " + ex.Message;
            return false;
        }
    }

    private static void OpenTerminal(string workingDirectory)
    {
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            StartProcess("cmd.exe", "/k cd /d \"" + workingDirectory + "\"", workingDirectory);
            return;
        }

        if (Application.platform == RuntimePlatform.OSXEditor)
        {
            StartProcess("open", "-a Terminal \"" + workingDirectory + "\"", workingDirectory);
            return;
        }

        StartProcess("x-terminal-emulator", "--working-directory=\"" + workingDirectory + "\"", workingDirectory);
    }

    private static void StartProcess(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        Process.Start(startInfo);
    }
}
