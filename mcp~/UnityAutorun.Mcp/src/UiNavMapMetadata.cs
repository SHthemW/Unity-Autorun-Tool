using System;
using System.Reflection;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapMetadata
    {
        public const string SchemaVersion = "2.0";
        public const string CandidateProtocolVersion = "1.0";

        public static readonly string GeneratorVersion = ResolveGeneratorVersion();

        public static void PrepareForWrite(JsonObject map, bool invalidateCandidateCoverage)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            map["schemaVersion"] = SchemaVersion;
            map["generatorVersion"] = GeneratorVersion;
            map["mapVersion"] = ReadMapVersion(map) + 1;
            map["generatedAt"] = DateTimeOffset.UtcNow.ToString("o");
            if (!(map["candidateDecisions"] is JsonArray))
            {
                map["candidateDecisions"] = new JsonArray();
            }

            if (invalidateCandidateCoverage)
            {
                JsonObject generation = map["generation"] as JsonObject ?? new JsonObject();
                generation["status"] = "review-required";
                generation["candidateProtocolVersion"] = CandidateProtocolVersion;
                generation["message"] = "Candidate coverage must be completed with get_ui_nav_candidate_coverage and finalize_ui_nav_map_generation.";
                generation.Remove("completedAt");
                map["generation"] = generation;
            }
        }

        public static JsonObject Describe(JsonObject map)
        {
            string schemaVersion = ReadText(map, "schemaVersion");
            string generatorVersion = ReadText(map, "generatorVersion");
            int mapVersion = ReadMapVersion(map);
            string generationStatus = ReadText(map?["generation"] as JsonObject, "status");
            string candidateProtocolVersion = ReadText(
                map?["generation"] as JsonObject,
                "candidateProtocolVersion");
            bool schemaMatches = string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal);
            bool generatorMatches = string.Equals(generatorVersion, GeneratorVersion, StringComparison.Ordinal);
            bool generationComplete = string.Equals(generationStatus, "complete", StringComparison.Ordinal);
            bool candidateProtocolMatches = string.Equals(
                candidateProtocolVersion,
                CandidateProtocolVersion,
                StringComparison.Ordinal);

            return JsonUtil.Obj(
                ("schemaVersion", schemaVersion),
                ("expectedSchemaVersion", SchemaVersion),
                ("generatorVersion", generatorVersion),
                ("expectedGeneratorVersion", GeneratorVersion),
                ("mapVersion", mapVersion),
                ("candidateProtocolVersion", candidateProtocolVersion),
                ("expectedCandidateProtocolVersion", CandidateProtocolVersion),
                ("generationStatus", generationStatus),
                ("schemaMatches", schemaMatches),
                ("generatorMatches", generatorMatches),
                ("candidateProtocolMatches", candidateProtocolMatches),
                ("generationComplete", generationComplete),
                ("requiresRegeneration", !schemaMatches
                    || !generatorMatches
                    || !candidateProtocolMatches
                    || mapVersion <= 0
                    || !generationComplete)
            );
        }

        public static void ValidateForExecution(JsonObject map, string path)
        {
            JsonObject version = Describe(map);
            if (version["requiresRegeneration"]?.GetValue<bool>() != true)
            {
                return;
            }

            throw new InvalidOperationException(
                "UI navigation map is stale or incomplete: "
                + path
                + ". Expected schemaVersion="
                + SchemaVersion
                + ", generatorVersion="
                + GeneratorVersion
                + ", candidateProtocolVersion="
                + CandidateProtocolVersion
                + ", mapVersion>0, and generation.status=complete. Regenerate and finalize the map with the current MCP server.");
        }

        private static int ReadMapVersion(JsonObject map)
        {
            JsonNode value = map?["mapVersion"];
            if (value == null)
            {
                return 0;
            }

            try
            {
                return value.GetValue<int>();
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        private static string ResolveGeneratorVersion()
        {
            Assembly assembly = typeof(UiNavMapMetadata).Assembly;
            AssemblyInformationalVersionAttribute attribute =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string version = attribute != null ? attribute.InformationalVersion : null;
            if (string.IsNullOrWhiteSpace(version))
            {
                Version assemblyVersion = assembly.GetName().Version;
                return assemblyVersion != null ? assemblyVersion.ToString() : "unknown";
            }

            int metadataSeparator = version.IndexOf('+');
            return metadataSeparator >= 0 ? version.Substring(0, metadataSeparator) : version;
        }

        private static string ReadText(JsonObject obj, string key)
        {
            return obj?[key]?.GetValue<string>();
        }
    }
}
