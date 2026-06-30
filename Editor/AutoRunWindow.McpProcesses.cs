using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private const float McpProcessColumnGap = 12f;
    private const float McpProcessColumnPadding = 4f;

    private List<McpProcessInfo> _mcpProcesses = new List<McpProcessInfo>();
    private double _mcpProcessLastRefreshAt;

    private void RenderMcpProcesses()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Processes", GUILayout.Width(140)))
        {
            RefreshMcpProcesses();
        }

        GUILayout.Label("MCP stdio processes started by AI clients.");
        GUILayout.EndHorizontal();

        float[] widths = CalculateMcpProcessColumnWidths();
        RenderMcpProcessRow("MCP PID", "MCP Process", "AI PID", "AI Process", "Parent", EditorStyles.boldLabel, widths);
        if (_mcpProcesses.Count == 0)
        {
            RenderMcpProcessRow("-", "No UnityAutorun.Mcp process found.", "-", "-", "-", EditorStyles.miniLabel, widths);
            return;
        }

        foreach (McpProcessInfo process in _mcpProcesses)
        {
            RenderMcpProcessRow(
                process.ProcessId.ToString(),
                process.ProcessName,
                process.AiProcessId.ToString(),
                process.AiProcessName,
                process.ParentProcessName + " (" + process.ParentProcessId + ")",
                EditorStyles.miniLabel,
                widths);
        }
    }

    private void RenderMcpProcessRow(
        string mcpPid,
        string mcpProcess,
        string aiPid,
        string aiProcess,
        string parent,
        GUIStyle style,
        float[] widths)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(mcpPid, style, GUILayout.Width(widths[0]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(mcpProcess, style, GUILayout.Width(widths[1]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(aiPid, style, GUILayout.Width(widths[2]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(aiProcess, style, GUILayout.Width(widths[3]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(parent, style, GUILayout.Width(widths[4]));
        GUILayout.EndHorizontal();
    }

    private float[] CalculateMcpProcessColumnWidths()
    {
        float[] widths =
        {
            MeasureMcpProcessCell("MCP PID", EditorStyles.boldLabel),
            MeasureMcpProcessCell("MCP Process", EditorStyles.boldLabel),
            MeasureMcpProcessCell("AI PID", EditorStyles.boldLabel),
            MeasureMcpProcessCell("AI Process", EditorStyles.boldLabel),
            MeasureMcpProcessCell("Parent", EditorStyles.boldLabel),
        };

        if (_mcpProcesses.Count == 0)
        {
            widths[1] = Mathf.Max(widths[1], MeasureMcpProcessCell("No UnityAutorun.Mcp process found.", EditorStyles.miniLabel));
            return widths;
        }

        foreach (McpProcessInfo process in _mcpProcesses)
        {
            widths[0] = MaxCell(widths[0], process.ProcessId.ToString());
            widths[1] = MaxCell(widths[1], process.ProcessName);
            widths[2] = MaxCell(widths[2], process.AiProcessId.ToString());
            widths[3] = MaxCell(widths[3], process.AiProcessName);
            widths[4] = MaxCell(widths[4], process.ParentProcessName + " (" + process.ParentProcessId + ")");
        }

        return widths;
    }

    private static float MaxCell(float current, string text)
    {
        return Mathf.Max(current, MeasureMcpProcessCell(text, EditorStyles.miniLabel));
    }

    private static float MeasureMcpProcessCell(string text, GUIStyle style)
    {
        return style.CalcSize(new GUIContent(text ?? "")).x + McpProcessColumnPadding;
    }

    private void RefreshMcpProcesses()
    {
        _mcpProcesses = McpProcessService.ListUnityAutorunProcesses();
        _mcpProcessLastRefreshAt = EditorApplication.timeSinceStartup;
    }
}
