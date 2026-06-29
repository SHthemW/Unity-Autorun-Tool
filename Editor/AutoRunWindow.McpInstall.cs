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

        GUILayout.Label("MCP Install");
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
}
