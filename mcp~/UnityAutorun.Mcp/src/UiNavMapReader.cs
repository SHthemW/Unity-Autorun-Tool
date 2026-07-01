using System;
using System.IO;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapReader
    {
        public static JsonObject GetCurrent()
        {
            string path = UiNavMapPaths.ResolveDefaultMapPath();
            if (!File.Exists(path))
            {
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "nav_map_missing"),
                    ("path", path),
                    ("message", "Current UI navigation map does not exist.")
                );
            }

            JsonNode map = JsonNode.Parse(File.ReadAllText(path));
            if (map == null)
            {
                throw new InvalidOperationException("Invalid UI navigation map: " + path);
            }

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("map", map)
            );
        }
    }
}
