using System;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private const double SkillStatusRefreshIntervalSeconds = 2d;

    private SkillInstallStatus _skillInstallStatus;
    private string _skillInstallStatusTarget = "";
    private double _skillInstallStatusRefreshedAt = -1d;

    private void RenderSkillInstallButton()
    {
        SkillInstallStatus status = GetSkillInstallStatus();
        string label;
        if (!status.IsInstalled)
        {
            label = "Install Skill";
        }
        else if (status.IsInstalledNewer)
        {
            label = "Skill Newer";
        }
        else if (status.IsCurrent)
        {
            label = "Reinstall Skill";
        }
        else if (status.IsUpdateAvailable)
        {
            label = "Update Skill";
        }
        else
        {
            label = "Repair Skill";
        }

        string tooltip;
        if (status.IsInstalledNewer)
        {
            tooltip = "Installed v"
                + status.InstalledVersion
                + " is newer than bundled v"
                + status.BundledVersion
                + ". Update the Unity package before reinstalling.";
        }
        else
        {
            tooltip = status.IsTargetValid
                ? "Copy the bundled Unity Autorun Skill to " + status.InstallDirectory
                : status.Error;
        }
        using (new EditorGUI.DisabledScope(
            !status.IsTargetValid || status.IsInstalledNewer))
        {
            if (!GUILayout.Button(
                    new GUIContent(label, tooltip),
                    GUILayout.Width(105)))
            {
                return;
            }
        }

        string message;
        if (SkillInstallService.Install(_mcpInstallTargetPath, out message))
        {
            InvalidateSkillInstallStatus();
            AppendConsoleText(message);
            EditorUtility.DisplayDialog("Skill Install", message, "OK");
        }
        else
        {
            AppendConsoleText(message, AutoRunLogLevel.Error);
            EditorUtility.DisplayDialog("Skill Install Failed", message, "OK");
        }
    }

    private SkillInstallStatus GetSkillInstallStatus()
    {
        string target = _mcpInstallTargetPath ?? "";
        if (_skillInstallStatus == null
            || !string.Equals(
                target,
                _skillInstallStatusTarget,
                StringComparison.OrdinalIgnoreCase)
            || EditorApplication.timeSinceStartup - _skillInstallStatusRefreshedAt
                >= SkillStatusRefreshIntervalSeconds)
        {
            _skillInstallStatus = SkillInstallService.GetStatus(target);
            _skillInstallStatusTarget = target;
            _skillInstallStatusRefreshedAt = EditorApplication.timeSinceStartup;
        }

        return _skillInstallStatus;
    }

    private void InvalidateSkillInstallStatus()
    {
        _skillInstallStatus = null;
        _skillInstallStatusTarget = "";
        _skillInstallStatusRefreshedAt = -1d;
    }
}
