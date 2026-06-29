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
        _mcpInstallTargetPath = GUILayout.TextField(_mcpInstallTargetPath);

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
                AppendConsoleText(message);
                EditorUtility.DisplayDialog("MCP Install Failed", message, "OK");
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.Label("Select a .codex folder for Codex or a .claude folder for Claude.");
    }

    private void RenderMcpTools()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Open Terminal", GUILayout.Width(120)))
        {
            string message;
            if (McpTerminalService.OpenToolRootTerminal(out message))
            {
                AppendConsoleText(message);
            }
            else
            {
                AppendConsoleText(message);
                EditorUtility.DisplayDialog("Open Terminal Failed", message, "OK");
            }
        }

        GUILayout.Label("Open a terminal at the Unity AutoRun tool root.");
        GUILayout.EndHorizontal();
    }

    private void RenderMcpPanel()
    {
        BeginPanel("MCP");
        GUILayout.Label("Bridge");
        RenderBridgeControls();
        GUILayout.Space(4);
        GUILayout.Label("Install");
        RenderMcpInstallControls();
        GUILayout.Space(4);
        GUILayout.Label("Tools");
        RenderMcpTools();
        EndPanel();
    }
}
