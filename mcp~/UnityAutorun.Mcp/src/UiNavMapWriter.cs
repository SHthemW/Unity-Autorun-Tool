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
            UiNavMapMetadata.PrepareForWrite(map, true);
            ValidateMap(map);

            string path = UiNavMapPaths.ResolveDefaultMapPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(
                path,
                JsonUtil.Pretty(UiNavMapPatchTools.SortMap(map)) + Environment.NewLine,
                new UTF8Encoding(false));

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("version", UiNavMapMetadata.Describe(map)),
                ("message", "UI navigation map saved. Candidate coverage must be finalized before route execution.")
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
            RequireArray(map, "candidateDecisions");
            JsonObject ledgerValidation = UiNavMapPatchTools.ValidateCandidateDecisionLedger(map);
            if (ledgerValidation["ok"]?.GetValue<bool>() != true)
            {
                throw new InvalidOperationException(
                    "Invalid candidateDecisions: "
                    + JsonUtil.Pretty(ledgerValidation["errors"]));
            }
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
