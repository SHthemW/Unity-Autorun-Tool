using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private const float McpProcessColumnGap = 12f;
    private const float McpProcessColumnPadding = 4f;
    private const float McpProcessTablePadding = 28f;

    private List<McpProcessInfo> _mcpProcesses = new List<McpProcessInfo>();
    private double _mcpProcessLastRefreshAt;

    private void RenderMcpProcesses()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Processes", GUILayout.Width(140)))
        {
            RefreshMcpProcesses();
        }

        GUILayout.Label(
            new GUIContent(
                "MCP stdio processes and their server versions.",
                "MCP Version is read from the UnityAutorun.Mcp executable or DLL "
                + "loaded by each process."),
            GetSqueezedStyle(EditorStyles.label),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true)
        );
        GUILayout.EndHorizontal();

        float[] widths = CalculateMcpProcessColumnWidths(GetWindowContentWidth() - McpProcessTablePadding);
        RenderMcpProcessRow(
            "MCP PID",
            "MCP Process",
            "AI PID",
            "AI Process",
            "MCP Version",
            "Published",
            EditorStyles.boldLabel,
            widths);
        if (_mcpProcesses.Count == 0)
        {
            RenderMcpProcessRow(
                "-",
                "No UnityAutorun.Mcp process found.",
                "-",
                "-",
                "-",
                "-",
                EditorStyles.miniLabel,
                widths);
            return;
        }

        foreach (McpProcessInfo process in _mcpProcesses)
        {
            RenderMcpProcessRow(
                process.ProcessId.ToString(),
                process.ProcessName,
                process.AiProcessId.ToString(),
                process.AiProcessName,
                process.McpVersion,
                process.PublishedAt,
                EditorStyles.miniLabel,
                widths);
        }
    }

    private void RenderMcpProcessRow(
        string mcpPid,
        string mcpProcess,
        string aiPid,
        string aiProcess,
        string mcpVersion,
        string published,
        GUIStyle style,
        float[] widths)
    {
        GUILayout.BeginHorizontal();
        GUIStyle cellStyle = GetSqueezedStyle(style);
        GUILayout.Label(mcpPid, cellStyle, GUILayout.Width(widths[0]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(mcpProcess, cellStyle, GUILayout.Width(widths[1]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(aiPid, cellStyle, GUILayout.Width(widths[2]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(aiProcess, cellStyle, GUILayout.Width(widths[3]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(mcpVersion, cellStyle, GUILayout.Width(widths[4]));
        GUILayout.Space(McpProcessColumnGap);
        GUILayout.Label(published, cellStyle, GUILayout.Width(widths[5]));
        GUILayout.EndHorizontal();
    }

    private float[] CalculateMcpProcessColumnWidths(float availableWidth)
    {
        float[] widths =
        {
            MeasureMcpProcessCell("MCP PID", EditorStyles.boldLabel),
            MeasureMcpProcessCell("MCP Process", EditorStyles.boldLabel),
            MeasureMcpProcessCell("AI PID", EditorStyles.boldLabel),
            MeasureMcpProcessCell("AI Process", EditorStyles.boldLabel),
            MeasureMcpProcessCell("MCP Version", EditorStyles.boldLabel),
            MeasureMcpProcessCell("Published", EditorStyles.boldLabel),
        };

        if (_mcpProcesses.Count == 0)
        {
            widths[1] = Mathf.Max(widths[1], MeasureMcpProcessCell("No UnityAutorun.Mcp process found.", EditorStyles.miniLabel));
            return FitMcpProcessColumnWidths(widths, availableWidth);
        }

        foreach (McpProcessInfo process in _mcpProcesses)
        {
            widths[0] = MaxCell(widths[0], process.ProcessId.ToString());
            widths[1] = MaxCell(widths[1], process.ProcessName);
            widths[2] = MaxCell(widths[2], process.AiProcessId.ToString());
            widths[3] = MaxCell(widths[3], process.AiProcessName);
            widths[4] = MaxCell(widths[4], process.McpVersion);
            widths[5] = MaxCell(widths[5], process.PublishedAt);
        }

        return FitMcpProcessColumnWidths(widths, availableWidth);
    }

    private static float[] FitMcpProcessColumnWidths(float[] widths, float availableWidth)
    {
        float availableForColumns = Mathf.Max(120f, availableWidth - McpProcessColumnGap * (widths.Length - 1));
        float total = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            total += widths[i];
        }

        if (total <= availableForColumns)
        {
            return widths;
        }

        float[] minimums =
        {
            50f,
            70f,
            42f,
            70f,
            70f,
            70f,
        };
        float minimumTotal = 0f;
        float flexibleTotal = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            minimumTotal += minimums[i];
            flexibleTotal += Mathf.Max(0f, widths[i] - minimums[i]);
        }

        float remaining = availableForColumns - minimumTotal;
        if (remaining <= 0f || flexibleTotal <= 0f)
        {
            float scale = availableForColumns / minimumTotal;
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = Mathf.Max(28f, minimums[i] * scale);
            }

            return widths;
        }

        for (int i = 0; i < widths.Length; i++)
        {
            float flexible = Mathf.Max(0f, widths[i] - minimums[i]);
            widths[i] = minimums[i] + remaining * (flexible / flexibleTotal);
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
