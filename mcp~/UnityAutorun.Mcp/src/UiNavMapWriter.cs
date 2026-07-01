using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapWriter
    {
        public static JsonObject Save(JsonObject args)
        {
            JsonObject map = ReadMap(args);
            ValidateMap(map);

            string path = UiNavMapPaths.ResolveDefaultMapPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtil.Pretty(map) + Environment.NewLine, new UTF8Encoding(false));

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("message", "UI navigation map saved.")
            );
        }

        private static JsonObject ReadMap(JsonObject args)
        {
            if (args?["map"] is JsonObject mapObject)
            {
                return mapObject.DeepClone().AsObject();
            }

            string mapJson = args?["mapJson"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(mapJson))
            {
                return JsonNode.Parse(mapJson)?.AsObject()
                    ?? throw new InvalidOperationException("mapJson must be a JSON object.");
            }

            throw new InvalidOperationException("save_ui_nav_map requires either a map object or mapJson string.");
        }

        private static void ValidateMap(JsonObject map)
        {
            RequireArray(map, "views");
            RequireArray(map, "controls");
            RequireArray(map, "transitions");
            RequireArray(map, "routes");
            RequireArray(map, "unresolved");
        }

        private static void RequireArray(JsonObject map, string key)
        {
            if (!(map?[key] is JsonArray))
            {
                throw new InvalidOperationException("Navigation map must contain array: " + key);
            }
        }
    }
}
