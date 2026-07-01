using System;
using System.IO;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapPaths
    {
        public const string DefaultRelativePath = "mcp/ui-nav-map.json";

        public static string ResolveDefaultMapPath()
        {
            return ResolveMapPath(null);
        }

        public static string ResolveMapPath(string mapPath)
        {
            string input = string.IsNullOrWhiteSpace(mapPath)
                ? Environment.GetEnvironmentVariable("UNITY_AUTORUN_NAV_MAP") ?? DefaultRelativePath
                : mapPath;

            if (Path.IsPathRooted(input))
            {
                return Path.GetFullPath(input);
            }

            return Path.GetFullPath(Path.Combine(ResolveToolRootDirectory(), input));
        }

        public static string ResolveToolRootDirectory()
        {
            string configured = Environment.GetEnvironmentVariable("UNITY_AUTORUN_TOOL_ROOT");
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

        private static bool IsToolRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "mcp"))
                && Directory.Exists(Path.Combine(path, "mcp~"));
        }
    }
}
