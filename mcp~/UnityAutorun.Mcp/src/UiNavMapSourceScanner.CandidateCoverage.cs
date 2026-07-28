using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static partial class UiNavMapSourceScanner
    {
        public static JsonObject GetCandidateCoverage(JsonObject args)
        {
            CandidateSnapshot snapshot = BuildCandidateSnapshot(args, Text(args, "query", ""));
            JsonObject map = LoadMapOrEmpty(snapshot.MapPath);
            return BuildCandidateCoverageResponse(snapshot, map, args);
        }

        public static JsonObject FinalizeGeneration(JsonObject args)
        {
            string mapPath = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            if (!File.Exists(mapPath))
            {
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "nav_map_missing"),
                    ("path", mapPath),
                    ("message", "Cannot finalize candidate coverage because the navigation map does not exist.")
                );
            }

            CandidateSnapshot snapshot = BuildCandidateSnapshot(args, "");
            JsonObject map = UiNavMapPatchTools.LoadOrSkeleton(mapPath);
            JsonObject ledgerValidation = UiNavMapPatchTools.ValidateCandidateDecisionLedger(map);
            if (ledgerValidation["ok"]?.GetValue<bool>() != true)
            {
                return JsonUtil.Obj(
                    ("ok", false),
                    ("code", "candidate_decisions_invalid"),
                    ("path", mapPath),
                    ("completionGatePassed", false),
                    ("errors", ledgerValidation["errors"]?.DeepClone()),
                    ("warnings", ledgerValidation["warnings"]?.DeepClone()),
                    ("message", "Fix invalid candidateDecisions entries before finalizing generation.")
                );
            }

            JsonObject coverage = BuildCandidateCoverageResponse(snapshot, map, JsonUtil.Obj(
                ("offset", 0),
                ("limit", Math.Max(1, Math.Min(200, Int(args, "limit", 50))))
            ));
            if (coverage["isComplete"]?.GetValue<bool>() != true)
            {
                coverage["ok"] = false;
                coverage["code"] = "candidate_review_incomplete";
                coverage["completionGatePassed"] = false;
                coverage["message"] = "Review every returned candidate, merge one candidateDecisions item per candidate, then call finalize_ui_nav_map_generation again.";
                return coverage;
            }

            Dictionary<string, string> currentVersions = snapshot.AllCandidates.ToDictionary(
                item => item.Id,
                item => item.CandidateVersion,
                StringComparer.Ordinal);
            JsonArray retainedDecisions = new JsonArray();
            int staleDecisionCount = 0;
            foreach (JsonObject decision in CandidateDecisionObjects(map["candidateDecisions"] as JsonArray))
            {
                string id = Text(decision, "id");
                string candidateVersion = Text(decision, "candidateVersion");
                if (NotBlank(id)
                    && currentVersions.ContainsKey(id)
                    && string.Equals(currentVersions[id], candidateVersion, StringComparison.Ordinal))
                {
                    retainedDecisions.Add(decision.DeepClone());
                }
                else
                {
                    staleDecisionCount++;
                }
            }

            map["candidateDecisions"] = retainedDecisions;
            map["generation"] = JsonUtil.Obj(
                ("status", "complete"),
                ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                ("candidateSetVersion", snapshot.CandidateSetVersion),
                ("candidateCount", snapshot.AllCandidates.Count),
                ("reviewedCandidateCount", snapshot.AllCandidates.Count),
                ("completedAt", DateTimeOffset.UtcNow.ToString("o"))
            );
            UiNavMapMetadata.PrepareForWrite(map, false);
            Directory.CreateDirectory(Path.GetDirectoryName(mapPath));
            File.WriteAllText(
                mapPath,
                JsonUtil.Pretty(UiNavMapPatchTools.SortMap(map)) + Environment.NewLine,
                new UTF8Encoding(false));

            return JsonUtil.Obj(
                ("ok", true),
                ("path", mapPath),
                ("completionGatePassed", true),
                ("candidateSetVersion", snapshot.CandidateSetVersion),
                ("candidateCount", snapshot.AllCandidates.Count),
                ("reviewedCandidateCount", snapshot.AllCandidates.Count),
                ("staleDecisionsRemoved", staleDecisionCount),
                ("version", UiNavMapMetadata.Describe(map)),
                ("summary", UiNavMapPatchTools.GetSummary(JsonUtil.Obj(("mapPath", mapPath)))),
                ("message", "Navigation map candidate coverage is complete and the versioned map is ready for route resolution.")
            );
        }

        private static CandidateSnapshot BuildCandidateSnapshot(JsonObject args, string query)
        {
            string assetsRoot = ResolveAssetsRoot();
            string mapPath = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            List<SourceEvidence> evidence = BuildEvidence(assetsRoot);
            JsonObject map = LoadMapOrEmpty(mapPath);
            Dictionary<string, string> knownViews = BuildKnownViews(evidence, map, args);
            List<NavigationCallCandidate> allCandidates = BuildSourceCodeNavigationModel(assetsRoot, knownViews)
                .BuildNavigationCandidates()
                .OrderBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();
            List<NavigationCallCandidate> candidates = allCandidates
                .Where(item => item.Matches(query))
                .ToList();
            string candidateSetVersion = "candidate-set."
                + UiNavMapMetadata.CandidateProtocolVersion
                + "."
                + StableHash(string.Join(
                    "\n",
                    allCandidates.Select(item => item.Id + "@" + item.CandidateVersion)));
            return new CandidateSnapshot(
                assetsRoot,
                mapPath,
                knownViews.Count,
                allCandidates,
                candidates,
                candidateSetVersion,
                query);
        }

        private static JsonObject BuildCandidateCoverageResponse(
            CandidateSnapshot snapshot,
            JsonObject map,
            JsonObject args)
        {
            int offset = Math.Max(0, Int(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, Int(args, "limit", 50)));
            Dictionary<string, string> reviewedVersions = CandidateDecisionObjects(
                    map?["candidateDecisions"] as JsonArray)
                .Where(item => NotBlank(Text(item, "id")))
                .GroupBy(item => Text(item, "id"), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => Text(group.Last(), "candidateVersion"),
                    StringComparer.Ordinal);
            Dictionary<string, string> allCandidateVersions = snapshot.AllCandidates.ToDictionary(
                item => item.Id,
                item => item.CandidateVersion,
                StringComparer.Ordinal);
            List<NavigationCallCandidate> unreviewed = snapshot.Candidates
                .Where(item =>
                    !reviewedVersions.ContainsKey(item.Id)
                    || !string.Equals(
                        reviewedVersions[item.Id],
                        item.CandidateVersion,
                        StringComparison.Ordinal))
                .ToList();
            List<NavigationCallCandidate> pageItems = unreviewed
                .Skip(offset)
                .Take(limit)
                .ToList();
            var items = new JsonArray();
            foreach (NavigationCallCandidate candidate in pageItems)
            {
                JsonObject item = candidate.ToJson(snapshot.AssetsRoot);
                if (reviewedVersions.ContainsKey(candidate.Id))
                {
                    item["reviewStatus"] = "outdated-decision";
                    item["previousCandidateVersion"] = reviewedVersions[candidate.Id];
                }
                else
                {
                    item["reviewStatus"] = "unreviewed";
                }

                items.Add(item);
            }

            int reviewedInScope = snapshot.Candidates.Count - unreviewed.Count;
            int nextOffset = offset + pageItems.Count;
            int staleDecisionCount = reviewedVersions.Keys.Count(id => !allCandidateVersions.ContainsKey(id));
            int outdatedDecisionCount = reviewedVersions.Count(pair =>
                allCandidateVersions.ContainsKey(pair.Key)
                && !string.Equals(
                    allCandidateVersions[pair.Key],
                    pair.Value,
                    StringComparison.Ordinal));
            bool hasMore = nextOffset < unreviewed.Count;

            return JsonUtil.Obj(
                ("ok", true),
                ("path", snapshot.MapPath),
                ("query", snapshot.Query),
                ("scope", string.IsNullOrWhiteSpace(snapshot.Query) ? "all-candidates" : "query"),
                ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                ("candidateSetVersion", snapshot.CandidateSetVersion),
                ("knownViewCount", snapshot.KnownViewCount),
                ("total", snapshot.Candidates.Count),
                ("reviewed", reviewedInScope),
                ("remaining", unreviewed.Count),
                ("staleDecisionCount", staleDecisionCount),
                ("outdatedDecisionCount", outdatedDecisionCount),
                ("isComplete", unreviewed.Count == 0),
                ("page", JsonUtil.Obj(
                    ("offset", offset),
                    ("limit", limit),
                    ("returned", pageItems.Count),
                    ("hasMore", hasMore),
                    ("nextOffset", hasMore ? nextOffset : 0),
                    ("remainingAfterPage", Math.Max(0, unreviewed.Count - nextOffset))
                )),
                ("items", items),
                ("generation", map?["generation"]?.DeepClone()),
                ("decisionPolicy", StaticDecisionPolicy()),
                ("workflowHint", "Review every returned item, copy id and candidateVersion exactly, and merge one candidateDecisions entry with outcome=transition, unresolved, or ignored. For reviewStatus=outdated-decision, replace the existing decision with the new evidence version; this intentional candidate-ledger update may use allowConflicts=true after validation. After each merge request the next unreviewed page with offset=0. Global generation is complete only when query is empty, remaining=0, and finalize_ui_nav_map_generation succeeds.")
            );
        }

        private static IEnumerable<JsonObject> CandidateDecisionObjects(JsonArray array)
        {
            return array != null
                ? array.OfType<JsonObject>()
                : Enumerable.Empty<JsonObject>();
        }

        private sealed class CandidateSnapshot
        {
            public CandidateSnapshot(
                string assetsRoot,
                string mapPath,
                int knownViewCount,
                List<NavigationCallCandidate> allCandidates,
                List<NavigationCallCandidate> candidates,
                string candidateSetVersion,
                string query)
            {
                AssetsRoot = assetsRoot;
                MapPath = mapPath;
                KnownViewCount = knownViewCount;
                AllCandidates = allCandidates;
                Candidates = candidates;
                CandidateSetVersion = candidateSetVersion;
                Query = query;
            }

            public string AssetsRoot { get; }
            public string MapPath { get; }
            public int KnownViewCount { get; }
            public List<NavigationCallCandidate> AllCandidates { get; }
            public List<NavigationCallCandidate> Candidates { get; }
            public string CandidateSetVersion { get; }
            public string Query { get; }
        }
    }
}
