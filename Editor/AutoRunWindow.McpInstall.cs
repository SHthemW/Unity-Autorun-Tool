using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private const string McpInstallTargetKey = "UnityAutorunTool.McpInstallTarget";
    private string _mcpInstallTargetPath;

    private void RenderMcpInstallControls()
    {
        if (string.IsNullOrEmpty(_mcpInstallTargetPath))
        {
            _mcpInstallTargetPath = EditorPrefs.GetString(McpInstallTargetKey, "");
        }

        GUILayout.BeginHorizontal();
        _mcpInstallTargetPath = GUILayout.TextField(_mcpInstallTargetPath, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Select", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select .codex or .claude folder", _mcpInstallTargetPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                _mcpInstallTargetPath = selected;
                EditorPrefs.SetString(McpInstallTargetKey, selected);
            }
        }

        if (GUILayout.Button("Install MCP", GUILayout.Width(100)))
        {
            string message;
            if (McpInstallService.Install(_mcpInstallTargetPath, out message))
            {
                AppendConsoleText(message);
                EditorUtility.DisplayDialog("MCP Install", message, "OK");
            }
            else
            {
                AppendConsoleText(message, AutoRunLogLevel.Error);
                EditorUtility.DisplayDialog("MCP Install Failed", message, "OK");
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.Label(
            "Select a .codex folder for Codex or a .claude folder for Claude.",
            GetSqueezedStyle(EditorStyles.label),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true)
        );
    }

    private void RenderMcpTools()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
                new GUIContent(
                    "Publish MCP",
                    "Stops matching MCP processes before publishing and waits for clients to reconnect."),
                GUILayout.Width(110)))
        {
            string message;
            bool published = McpPublishService.PublishCurrentVersion(out message, out bool reconnectPending);
            RefreshMcpProcesses();

            if (published)
            {
                AppendConsoleText(
                    message,
                    reconnectPending ? AutoRunLogLevel.Warning : AutoRunLogLevel.Info);
                if (reconnectPending)
                {
                    EditorUtility.DisplayDialog(
                        "MCP Published - Reconnect Required",
                        message,
                        "OK");
                }
            }
            else
            {
                AppendConsoleText(message, AutoRunLogLevel.Error);
                EditorUtility.DisplayDialog("Publish MCP Failed", message, "OK");
            }
        }

        if (GUILayout.Button("Open Root", GUILayout.Width(95)))
        {
            string message;
            if (OpenToolRootDirectory(out message))
            {
                AppendConsoleText(message);
            }
            else
            {
                AppendConsoleText(message, AutoRunLogLevel.Error);
                EditorUtility.DisplayDialog("Open Root Failed", message, "OK");
            }
        }

        if (GUILayout.Button("Preview Nav Map", GUILayout.Width(130)))
        {
            string message;
            if (McpNavMapPreviewService.OpenPreview(out message))
            {
                AppendConsoleText(message);
            }
            else
            {
                AppendConsoleText(message, AutoRunLogLevel.Error);
                EditorUtility.DisplayDialog("Preview Nav Map Failed", message, "OK");
            }
        }

        GUILayout.Label(
            "Publish the MCP server, open its root folder, or render Gen/ui-nav-map.json as an HTML graph.",
            GetSqueezedStyle(EditorStyles.label),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true)
        );
        GUILayout.EndHorizontal();
    }

    private static bool OpenToolRootDirectory(out string message)
    {
        try
        {
            string root = McpInstallConfig.GetToolRootDirectory();
            if (!Directory.Exists(root))
            {
                message = "Tool root directory does not exist: " + root;
                return false;
            }

            EditorUtility.RevealInFinder(root);
            message = "Opened tool root directory: " + root;
            return true;
        }
        catch (Exception ex)
        {
            message = "Open tool root directory failed: " + ex.Message;
            return false;
        }
    }

    private void RenderMcpPanel()
    {
        BeginPanel("MCP");
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            new GUIContent(
                "Unity AutoRun MCP",
                "The version is read from the tool package.json file."),
            EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            "v" + McpInstallConfig.GetToolVersion(),
            EditorStyles.miniBoldLabel);
        GUILayout.EndHorizontal();
        GUILayout.Space(2);

        if (EditorApplication.timeSinceStartup - _mcpProcessLastRefreshAt > 2)
        {
            RefreshMcpProcesses();
        }

        GUILayout.Label("Bridge");
        RenderBridgeControls();
        GUILayout.Space(4);
        GUILayout.Label("Processes");
        RenderMcpProcesses();
        GUILayout.Space(4);
        GUILayout.Label("Install");
        RenderMcpInstallControls();
        GUILayout.Space(4);
        GUILayout.Label("Tools");
        RenderMcpTools();
        EndPanel();
    }
}
