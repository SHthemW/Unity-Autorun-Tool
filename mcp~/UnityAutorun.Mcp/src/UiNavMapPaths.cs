using System;
using System.IO;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapPaths
    {
        public const string DefaultRelativePath =
            "ProjectSettings/Packages/com.shthemw.unity-autorun-tool/ui-nav-map.json";
        public const string ExampleRelativePath = "Example/ui-nav-map.example.json";

        private const string NavMapEnvironmentVariable = "UNITY_AUTORUN_NAV_MAP";
        private const string ProjectRootEnvironmentVariable = "UNITY_AUTORUN_PROJECT_ROOT";
        private const string ToolRootEnvironmentVariable = "UNITY_AUTORUN_TOOL_ROOT";

        public static string ResolveDefaultMapPath()
        {
            string configured = Environment.GetEnvironmentVariable(NavMapEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(ResolveProjectRootDirectory(), DefaultRelativePath))
                : ResolveMapPath(configured);
        }

        public static string ResolveMapPath(string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                return ResolveDefaultMapPath();
            }

            if (Path.IsPathRooted(mapPath))
            {
                return Path.GetFullPath(mapPath);
            }

            return Path.GetFullPath(Path.Combine(ResolveProjectRootDirectory(), mapPath));
        }

        public static string ResolveExampleMapPath()
        {
            return Path.GetFullPath(Path.Combine(ResolveToolRootDirectory(), ExampleRelativePath));
        }

        public static string ResolveProjectRootDirectory()
        {
            string configured = Environment.GetEnvironmentVariable(ProjectRootEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string configuredPath = Path.GetFullPath(configured);
                if (IsProjectRoot(configuredPath))
                {
                    return configuredPath;
                }

                throw new InvalidOperationException(
                    $"{ProjectRootEnvironmentVariable} does not point to a Unity project: {configuredPath}");
            }

            string fromToolRoot = FindProjectRoot(
                Environment.GetEnvironmentVariable(ToolRootEnvironmentVariable));
            if (!string.IsNullOrEmpty(fromToolRoot))
            {
                return fromToolRoot;
            }

            string fromCurrentDirectory = FindProjectRoot(Environment.CurrentDirectory);
            if (!string.IsNullOrEmpty(fromCurrentDirectory))
            {
                return fromCurrentDirectory;
            }

            string fromApplication = FindProjectRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(fromApplication))
            {
                return fromApplication;
            }

            throw new InvalidOperationException(
                $"Cannot resolve the Unity project root. Set {ProjectRootEnvironmentVariable} or run from inside a Unity project.");
        }

        public static string ResolveToolRootDirectory()
        {
            string configured = Environment.GetEnvironmentVariable(ToolRootEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            {
                return Path.GetFullPath(configured);
            }

            string current = Directory.GetCurrentDirectory();
            if (IsToolRoot(current))
            {
                return Path.GetFullPath(current);
            }

            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (IsToolRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return Path.GetFullPath(current);
        }

        private static string FindProjectRoot(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return null;
            }

            DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(startPath));
            while (directory != null)
            {
                if (IsProjectRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static bool IsProjectRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "Assets"))
                && Directory.Exists(Path.Combine(path, "ProjectSettings"));
        }

        private static bool IsToolRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "Example"))
                && Directory.Exists(Path.Combine(path, "mcp~"));
        }
    }
}
