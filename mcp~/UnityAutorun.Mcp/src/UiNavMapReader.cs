using System;
using System.IO;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapReader
    {
        public static JsonObject GetCurrent(JsonObject args)
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

            bool full = args?["full"]?.GetValue<bool>() == true;
            if (!full)
            {
                JsonObject summary = UiNavMapPatchTools.GetSummary(JsonUtil.Obj(("mapPath", path)));
                summary["fullMapIncluded"] = false;
                summary["message"] = "Full map omitted. Use query_nav_map_items or get_ui_nav_subgraph for targeted reads, or set full=true only when necessary.";
                return summary;
            }

            JsonNode map = JsonNode.Parse(File.ReadAllText(path));
            if (map == null)
            {
                throw new InvalidOperationException("Invalid UI navigation map: " + path);
            }

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("fullMapIncluded", true),
                ("map", map)
            );
        }
    }
}
