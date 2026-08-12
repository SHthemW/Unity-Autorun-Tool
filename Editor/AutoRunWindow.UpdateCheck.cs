using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void EnableUpdateCheck()
    {
        AutoRunUpdateCheckService.StatusChanged -= HandleUpdateStatusChanged;
        AutoRunUpdateCheckService.StatusChanged += HandleUpdateStatusChanged;
        AutoRunUpdateCheckService.CheckIfDue();
    }

    private void DisableUpdateCheck()
    {
        AutoRunUpdateCheckService.StatusChanged -= HandleUpdateStatusChanged;
    }

    private void HandleUpdateStatusChanged()
    {
        Repaint();
    }

    private void RenderVersionStatus()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        RenderVersionRow(
            "MCP",
            BuildMcpVersionText(),
            "Source version comes from UnityAutorun.Mcp.csproj. Published version "
                + "comes from the current Release DLL.");

        SkillInstallStatus skillStatus = GetSkillInstallStatus();
        RenderVersionRow(
            "Skill",
            BuildSkillVersionText(skillStatus),
            skillStatus.IsTargetValid
                ? skillStatus.InstallDirectory
                : skillStatus.Error);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Updates", EditorStyles.miniLabel, GUILayout.Width(52));
        GUILayout.Label(
            new GUIContent(BuildUpdateStatusText(), BuildUpdateStatusTooltip()),
            GetSqueezedStyle(EditorStyles.miniLabel),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true));
        using (new EditorGUI.DisabledScope(AutoRunUpdateCheckService.IsChecking))
        {
            if (GUILayout.Button("Check Now", GUILayout.Width(90)))
            {
                AutoRunUpdateCheckService.CheckNow();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private static void RenderVersionRow(
        string label,
        string value,
        string tooltip)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(52));
        GUILayout.Label(
            new GUIContent(value, tooltip),
            GetSqueezedStyle(EditorStyles.miniLabel),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
    }

    private static string BuildMcpVersionText()
    {
        string sourceVersion = McpInstallConfig.GetMcpSourceVersion();
        string publishedPath;
        try
        {
            publishedPath = McpInstallConfig.GetPublishedDllPath();
        }
        catch
        {
            publishedPath = "";
        }

        if (string.IsNullOrEmpty(publishedPath) || !File.Exists(publishedPath))
        {
            return "source v" + sourceVersion + " | not published";
        }

        string publishedVersion = McpProcessService.GetMcpVersion(publishedPath);
        string suffix = "";
        if (AutoRunVersionComparer.TryCompare(
                sourceVersion,
                publishedVersion,
                out int comparison))
        {
            if (comparison > 0)
            {
                suffix = " | publish required";
            }
            else if (comparison < 0)
            {
                suffix = " | published is newer";
            }
            else
            {
                suffix = " | current";
            }
        }

        return "source v"
            + sourceVersion
            + " | published v"
            + publishedVersion
            + suffix;
    }

    private static string BuildSkillVersionText(SkillInstallStatus status)
    {
        string prefix = "bundled v" + status.BundledVersion + " | ";
        if (!status.IsTargetValid)
        {
            return prefix + "select target";
        }

        if (!status.IsInstalled)
        {
            return prefix + "not installed";
        }

        string suffix = "";
        if (status.IsCurrent)
        {
            suffix = " | current";
        }
        else if (status.IsUpdateAvailable)
        {
            suffix = " | update available";
        }
        else if (status.IsInstalledNewer)
        {
            suffix = " | installed is newer";
        }

        return prefix + "installed v" + status.InstalledVersion + suffix;
    }

    private static string BuildUpdateStatusText()
    {
        if (AutoRunUpdateCheckService.IsChecking)
        {
            return "checking...";
        }

        string latestVersion = AutoRunUpdateCheckService.LatestVersion;
        string currentVersion = McpInstallConfig.GetToolVersion();
        if (!string.IsNullOrEmpty(AutoRunUpdateCheckService.LastError))
        {
            return string.IsNullOrEmpty(latestVersion)
                ? "check failed"
                : "cached v" + latestVersion + " | check failed";
        }

        if (string.IsNullOrEmpty(latestVersion))
        {
            return "not checked";
        }

        if (!AutoRunVersionComparer.TryCompare(
                latestVersion,
                currentVersion,
                out int comparison))
        {
            return "latest v" + latestVersion;
        }

        if (comparison > 0)
        {
            return "v" + latestVersion + " available";
        }

        if (comparison < 0)
        {
            return "local v" + currentVersion + " is newer";
        }

        return "up to date | v" + currentVersion;
    }

    private static string BuildUpdateStatusTooltip()
    {
        string tooltip = "Automatically checks once every 24 hours. Source: "
            + AutoRunUpdateCheckService.LatestPackageUrl
            + "\nRepository: "
            + AutoRunUpdateCheckService.RepositoryUrl;
        DateTime? checkedUtc = AutoRunUpdateCheckService.LastSuccessfulCheckUtc;
        if (checkedUtc.HasValue)
        {
            tooltip += "\nLast successful check: "
                + checkedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (!string.IsNullOrEmpty(AutoRunUpdateCheckService.LastError))
        {
            tooltip += "\nLast error: " + AutoRunUpdateCheckService.LastError;
        }

        if (AutoRunUpdateCheckService.IsUpdateAvailable)
        {
            tooltip += "\nUpdate the Unity package, then publish MCP and update the Skill.";
        }

        return tooltip;
    }
}
