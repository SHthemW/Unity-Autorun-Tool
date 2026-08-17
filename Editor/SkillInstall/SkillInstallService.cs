using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class SkillInstallStatus
{
    public bool IsTargetValid;
    public bool IsInstalled;
    public string ClientName = "";
    public string InstallDirectory = "";
    public string BundledVersion = "unknown";
    public string InstalledVersion = "";
    public string Error = "";

    public bool IsCurrent
    {
        get
        {
            return IsInstalled
                && AutoRunVersionComparer.TryCompare(
                    InstalledVersion,
                    BundledVersion,
                    out int comparison)
                && comparison == 0;
        }
    }

    public bool IsInstalledNewer
    {
        get
        {
            return IsInstalled
                && AutoRunVersionComparer.TryCompare(
                    InstalledVersion,
                    BundledVersion,
                    out int comparison)
                && comparison > 0;
        }
    }

    public bool IsUpdateAvailable
    {
        get
        {
            return IsInstalled
                && AutoRunVersionComparer.TryCompare(
                    InstalledVersion,
                    BundledVersion,
                    out int comparison)
                && comparison < 0;
        }
    }
}

public static class SkillInstallService
{
    public const string SkillName = "unity-autorun";
    public const string VersionFileName = "skill-version.json";

    private const string SkillFileName = "SKILL.md";
    private const string InstallerWorkDirectoryName = ".unity-autorun-installer";

    public static SkillInstallStatus GetStatus(string targetFolder)
    {
        string bundledVersion = GetBundledVersion();
        if (!TryResolveInstallDirectory(
                targetFolder,
                out string clientName,
                out string installDirectory,
                out string error))
        {
            return new SkillInstallStatus
            {
                BundledVersion = bundledVersion,
                Error = error,
            };
        }

        bool isInstalled = File.Exists(Path.Combine(installDirectory, SkillFileName));
        string installedVersion = isInstalled
            ? ReadVersion(installDirectory)
            : "";

        return new SkillInstallStatus
        {
            IsTargetValid = true,
            IsInstalled = isInstalled,
            ClientName = clientName,
            InstallDirectory = installDirectory,
            BundledVersion = bundledVersion,
            InstalledVersion = string.IsNullOrEmpty(installedVersion)
                ? "unknown"
                : installedVersion,
        };
    }

    public static bool Install(string targetFolder, out string message)
    {
        SkillInstallStatus status = GetStatus(targetFolder);
        if (!status.IsTargetValid)
        {
            message = status.Error;
            return false;
        }

        if (status.IsInstalledNewer)
        {
            message = "Installed Unity Autorun Skill v"
                + status.InstalledVersion
                + " is newer than bundled v"
                + status.BundledVersion
                + ". Update the Unity package before reinstalling.";
            return false;
        }

        string sourceDirectory;
        try
        {
            sourceDirectory = GetBundledSkillDirectory();
        }
        catch (Exception ex)
        {
            message = "Cannot resolve bundled Unity Autorun Skill: " + ex.Message;
            return false;
        }

        if (!File.Exists(Path.Combine(sourceDirectory, SkillFileName)))
        {
            message = "Bundled Unity Autorun Skill is incomplete: " + sourceDirectory;
            return false;
        }

        string sourceVersion = ReadVersion(sourceDirectory);
        if (string.IsNullOrEmpty(sourceVersion))
        {
            message = "Bundled Unity Autorun Skill has no valid "
                + VersionFileName
                + ": "
                + sourceDirectory;
            return false;
        }

        string destinationDirectory = status.InstallDirectory;
        if (File.Exists(destinationDirectory))
        {
            message = "Skill install destination is a file: " + destinationDirectory;
            return false;
        }

        string skillsDirectory = Path.GetDirectoryName(destinationDirectory);
        DirectoryInfo clientDirectory = Directory.GetParent(skillsDirectory);
        if (clientDirectory == null)
        {
            message = "Cannot resolve Skill installer work directory for: "
                + destinationDirectory;
            return false;
        }

        string workDirectory = Path.Combine(
            clientDirectory.FullName,
            InstallerWorkDirectoryName);
        string operationId = Guid.NewGuid().ToString("N");
        string stagingDirectory = Path.Combine(
            workDirectory,
            SkillName + ".installing-" + operationId);
        string backupDirectory = Path.Combine(
            workDirectory,
            SkillName + ".backup-" + operationId);
        bool previousInstallMoved = false;
        bool newInstallMoved = false;

        try
        {
            Directory.CreateDirectory(skillsDirectory);
            Directory.CreateDirectory(workDirectory);
            CopyDirectory(sourceDirectory, stagingDirectory);

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Move(destinationDirectory, backupDirectory);
                previousInstallMoved = true;
            }

            Directory.Move(stagingDirectory, destinationDirectory);
            newInstallMoved = true;
        }
        catch (Exception ex)
        {
            string rollbackError = RollbackInstall(
                destinationDirectory,
                stagingDirectory,
                backupDirectory,
                previousInstallMoved,
                newInstallMoved);
            message = "Skill install failed: " + ex.Message + rollbackError;
            return false;
        }

        string cleanupWarning = CleanupBackup(backupDirectory, workDirectory);
        message = (status.IsInstalled ? "Updated" : "Installed")
            + " Unity Autorun Skill v"
            + sourceVersion
            + " for "
            + status.ClientName
            + ": "
            + destinationDirectory
            + cleanupWarning;
        return true;
    }

    public static bool TryResolveInstallDirectory(
        string targetFolder,
        out string clientName,
        out string installDirectory,
        out string error)
    {
        clientName = "";
        installDirectory = "";
        error = "";

        if (string.IsNullOrWhiteSpace(targetFolder)
            || !Directory.Exists(targetFolder))
        {
            error = "Select an existing .codex folder or Claude Code project .claude folder first.";
            return false;
        }

        var target = new DirectoryInfo(Path.GetFullPath(targetFolder));
        if (string.Equals(target.Name, ".codex", StringComparison.OrdinalIgnoreCase))
        {
            if (target.Parent == null)
            {
                error = "Cannot resolve the owner directory of: " + target.FullName;
                return false;
            }

            clientName = "Codex";
            installDirectory = Path.Combine(
                target.Parent.FullName,
                ".agents",
                "skills",
                SkillName);
            return true;
        }

        if (string.Equals(target.Name, ".claude", StringComparison.OrdinalIgnoreCase))
        {
            clientName = "Claude Code";
            installDirectory = Path.Combine(
                target.FullName,
                "skills",
                SkillName);
            return true;
        }

        if (File.Exists(Path.Combine(target.FullName, "claude_desktop_config.json")))
        {
            error = "Claude Desktop supports MCP installation here, but it does not load Claude Code filesystem Skills. Select a Claude Code project .claude folder to install the Skill.";
            return false;
        }

        error = "Target folder must be named .codex or be a Claude Code project .claude folder.";
        return false;
    }

    public static string GetBundledVersion()
    {
        try
        {
            string version = ReadVersion(GetBundledSkillDirectory());
            return string.IsNullOrEmpty(version)
                ? McpInstallConfig.GetToolVersion()
                : version;
        }
        catch
        {
            return McpInstallConfig.GetToolVersion();
        }
    }

    private static string GetBundledSkillDirectory()
    {
        return Path.Combine(
            McpInstallConfig.GetToolRootDirectory(),
            "skill~",
            SkillName);
    }

    private static string ReadVersion(string skillDirectory)
    {
        string path = Path.Combine(skillDirectory, VersionFileName);
        if (!File.Exists(path))
        {
            return "";
        }

        try
        {
            SkillVersionDocument document = JsonUtility.FromJson<SkillVersionDocument>(
                File.ReadAllText(path));
            if (document == null
                || !string.Equals(
                    document.name,
                    SkillName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return (document.version ?? "").Trim();
        }
        catch
        {
            return "";
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
        {
            string fileName = Path.GetFileName(sourceFile);
            File.Copy(
                sourceFile,
                Path.Combine(destinationDirectory, fileName),
                true);
        }

        foreach (string sourceChildDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string directoryName = Path.GetFileName(sourceChildDirectory);
            CopyDirectory(
                sourceChildDirectory,
                Path.Combine(destinationDirectory, directoryName));
        }
    }

    private static string RollbackInstall(
        string destinationDirectory,
        string stagingDirectory,
        string backupDirectory,
        bool previousInstallMoved,
        bool newInstallMoved)
    {
        var errors = new List<string>();
        try
        {
            if (newInstallMoved && Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, true);
            }
        }
        catch (Exception ex)
        {
            errors.Add("remove incomplete install: " + ex.Message);
        }

        try
        {
            if (previousInstallMoved
                && Directory.Exists(backupDirectory)
                && !Directory.Exists(destinationDirectory))
            {
                Directory.Move(backupDirectory, destinationDirectory);
            }
        }
        catch (Exception ex)
        {
            errors.Add("restore previous install: " + ex.Message);
        }

        try
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }
        }
        catch (Exception ex)
        {
            errors.Add("remove staging directory: " + ex.Message);
        }

        return errors.Count == 0
            ? ""
            : " Rollback issues: " + string.Join("; ", errors.ToArray());
    }

    private static string CleanupBackup(
        string backupDirectory,
        string workDirectory)
    {
        try
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            if (Directory.Exists(workDirectory)
                && Directory.GetFileSystemEntries(workDirectory).Length == 0)
            {
                Directory.Delete(workDirectory);
            }

            return "";
        }
        catch (Exception ex)
        {
            return " Previous-version cleanup was skipped: " + ex.Message;
        }
    }

    [Serializable]
    private sealed class SkillVersionDocument
    {
        public string name = "";
        public string version = "";
    }
}
