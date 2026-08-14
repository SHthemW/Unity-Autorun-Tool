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
                int semanticReviewCount = coverage["semanticReviewCount"]?.GetValue<int>() ?? 0;
                coverage["ok"] = false;
                coverage["code"] = semanticReviewCount > 0
                    ? "candidate_semantic_review_incomplete"
                    : "candidate_review_incomplete";
                coverage["completionGatePassed"] = false;
                coverage["message"] = semanticReviewCount > 0
                    ? "Some candidate decisions conflict with strong topology evidence or reference mismatched transitions. Replace each returned decision with a matching transition, or provide concrete nonTransitionEvidence, then finalize again."
                    : "Review every returned candidate, merge one candidateDecisions item per candidate, then call finalize_ui_nav_map_generation again.";
                return coverage;
            }

            CandidateDecisionResolution decisionResolution =
                ResolveCandidateDecisions(snapshot.AllCandidates, map);
            JsonArray retainedDecisions = new JsonArray();
            foreach (NavigationCallCandidate candidate in snapshot.AllCandidates)
            {
                JsonObject decision;
                if (decisionResolution.DecisionsByCurrentId.TryGetValue(
                    candidate.Id,
                    out decision))
                {
                    retainedDecisions.Add(decision.DeepClone());
                }
            }

            int staleDecisionCount = decisionResolution.StaleDecisionCount;
            if (IsAlreadyFinalized(
                map,
                snapshot,
                retainedDecisions,
                decisionResolution))
            {
                return JsonUtil.Obj(
                    ("ok", true),
                    ("path", mapPath),
                    ("completionGatePassed", true),
                    ("mapChanged", false),
                    ("writePerformed", false),
                    ("candidateSetVersion", snapshot.CandidateSetVersion),
                    ("candidateCount", snapshot.AllCandidates.Count),
                    ("reviewedCandidateCount", snapshot.AllCandidates.Count),
                    ("carriedForwardDecisionCount", 0),
                    ("staleDecisionsRemoved", 0),
                    ("semanticHash", UiNavMapPatchTools.SemanticHash(map)),
                    ("version", UiNavMapMetadata.Describe(map)),
                    ("summary", UiNavMapPatchTools.GetSummary(JsonUtil.Obj(("mapPath", mapPath)))),
                    ("message", "Candidate coverage is already current; no file was written."));
            }

            map["candidateDecisions"] = retainedDecisions;
            map["generation"] = JsonUtil.Obj(
                ("status", "complete"),
                ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion),
                ("candidateSetVersion", snapshot.CandidateSetVersion),
                ("candidateCount", snapshot.AllCandidates.Count),
                ("reviewedCandidateCount", snapshot.AllCandidates.Count),
                ("semanticReviewCount", 0),
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
                ("mapChanged", true),
                ("writePerformed", true),
                ("candidateSetVersion", snapshot.CandidateSetVersion),
                ("candidateCount", snapshot.AllCandidates.Count),
                ("reviewedCandidateCount", snapshot.AllCandidates.Count),
                ("carriedForwardDecisionCount", decisionResolution.CarriedForwardCount),
                ("staleDecisionsRemoved", staleDecisionCount),
                ("semanticHash", UiNavMapPatchTools.SemanticHash(map)),
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
                map,
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
            CandidateDecisionResolution decisionResolution =
                ResolveCandidateDecisions(snapshot.AllCandidates, map);
            Dictionary<string, JsonObject> decisionsById =
                decisionResolution.DecisionsByCurrentId;
            Dictionary<string, string> reviewedVersions = decisionsById
                .ToDictionary(
                    pair => pair.Key,
                    pair => Text(pair.Value, "candidateVersion"),
                    StringComparer.Ordinal);
            Dictionary<string, string> allCandidateVersions = snapshot.AllCandidates.ToDictionary(
                item => item.Id,
                item => item.CandidateVersion,
                StringComparer.Ordinal);
            Dictionary<string, CandidateDecisionReview> reviews = snapshot.Candidates
                .ToDictionary(
                    item => item.Id,
                    item =>
                    {
                        JsonObject decision;
                        decisionsById.TryGetValue(item.Id, out decision);
                        return ReviewCandidateDecision(item, decision, map);
                    },
                    StringComparer.Ordinal);
            List<NavigationCallCandidate> unreviewed = snapshot.Candidates
                .Where(item => !reviews[item.Id].IsComplete)
                .ToList();
            List<NavigationCallCandidate> pageItems = unreviewed
                .Skip(offset)
                .Take(limit)
                .ToList();
            var items = new JsonArray();
            foreach (NavigationCallCandidate candidate in pageItems)
            {
                JsonObject item = candidate.ToJson(snapshot.AssetsRoot, map);
                CandidateDecisionReview review = reviews[candidate.Id];
                item["reviewStatus"] = review.Status;
                if (review.Issue != null)
                {
                    item["semanticReview"] = review.Issue.DeepClone();
                }

                JsonObject currentDecision;
                if (decisionsById.TryGetValue(candidate.Id, out currentDecision))
                {
                    item["currentDecision"] = currentDecision.DeepClone();
                }

                if (decisionResolution.CarriedForwardCandidateIds.Contains(
                    candidate.Id))
                {
                    item["decisionCarriedForward"] = true;
                }

                items.Add(item);
            }

            int reviewedInScope = snapshot.Candidates.Count - unreviewed.Count;
            int nextOffset = offset + pageItems.Count;
            int staleDecisionCount = decisionResolution.StaleDecisionCount;
            int outdatedDecisionCount = reviewedVersions.Count(pair =>
                allCandidateVersions.ContainsKey(pair.Key)
                && !string.Equals(
                    allCandidateVersions[pair.Key],
                    pair.Value,
                    StringComparison.Ordinal));
            int semanticReviewCount = reviews.Values.Count(item =>
                item.Status == "semantic-review-required");
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
                ("carriedForwardDecisionCount", decisionResolution.CarriedForwardCount),
                ("outdatedDecisionCount", outdatedDecisionCount),
                ("semanticReviewCount", semanticReviewCount),
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
                ("workflowHint", "Review every returned item, copy id and candidateVersion exactly, and merge one candidateDecisions entry with outcome=transition, unresolved, or ignored. For reviewStatus=outdated-decision or semantic-review-required, replace the existing decision after validating the current evidence; this intentional candidate-ledger update may use allowConflicts=true. Strong topology evidence should become a matching transition. Missing exact AutoRun metadata, async work, branch preconditions, or absent runtime confirmation are not contradictory topology evidence. After each merge request the next unreviewed page with offset=0. Global generation is complete only when query is empty, remaining=0, and finalize_ui_nav_map_generation succeeds.")
            );
        }

        private static IEnumerable<JsonObject> CandidateDecisionObjects(JsonArray array)
        {
            return array != null
                ? array.OfType<JsonObject>()
                : Enumerable.Empty<JsonObject>();
        }

        private static CandidateDecisionResolution ResolveCandidateDecisions(
            List<NavigationCallCandidate> candidates,
            JsonObject map)
        {
            List<JsonObject> recorded = CandidateDecisionObjects(
                    map?["candidateDecisions"] as JsonArray)
                .ToList();
            Dictionary<string, JsonObject> recordedById = recorded
                .Where(item => NotBlank(Text(item, "id")))
                .GroupBy(item => Text(item, "id"), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.Ordinal);
            var decisionsByCurrentId = new Dictionary<string, JsonObject>(
                StringComparer.Ordinal);
            var consumedRecordedIds = new HashSet<string>(StringComparer.Ordinal);
            var carriedForwardCandidateIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (NavigationCallCandidate candidate in candidates)
            {
                JsonObject current;
                if (recordedById.TryGetValue(candidate.Id, out current))
                {
                    decisionsByCurrentId[candidate.Id] = current;
                    consumedRecordedIds.Add(candidate.Id);
                    continue;
                }

                JsonObject legacy;
                if (!recordedById.TryGetValue(candidate.LegacyId, out legacy))
                {
                    continue;
                }

                consumedRecordedIds.Add(candidate.LegacyId);
                JsonObject carried = legacy.DeepClone().AsObject();
                carried["id"] = candidate.Id;
                carried["candidateVersion"] = candidate.CandidateVersion;
                CandidateDecisionReview review = ReviewCandidateDecision(
                    candidate,
                    carried,
                    map);
                if (!review.IsComplete)
                {
                    decisionsByCurrentId[candidate.Id] = legacy;
                    continue;
                }

                decisionsByCurrentId[candidate.Id] = carried;
                carriedForwardCandidateIds.Add(candidate.Id);
            }

            return new CandidateDecisionResolution(
                decisionsByCurrentId,
                carriedForwardCandidateIds,
                Math.Max(0, recorded.Count - consumedRecordedIds.Count));
        }

        private static bool IsAlreadyFinalized(
            JsonObject map,
            CandidateSnapshot snapshot,
            JsonArray retainedDecisions,
            CandidateDecisionResolution decisionResolution)
        {
            if (decisionResolution.CarriedForwardCount > 0
                || decisionResolution.StaleDecisionCount > 0)
            {
                return false;
            }

            JsonObject version = UiNavMapMetadata.Describe(map);
            if (version["schemaMatches"]?.GetValue<bool>() != true
                || version["generatorMatches"]?.GetValue<bool>() != true
                || version["candidateProtocolMatches"]?.GetValue<bool>() != true)
            {
                return false;
            }

            JsonObject generation = map?["generation"] as JsonObject;
            if (!string.Equals(
                    Text(generation, "status"),
                    "complete",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Text(generation, "candidateProtocolVersion"),
                    UiNavMapMetadata.CandidateProtocolVersion,
                    StringComparison.Ordinal)
                || !string.Equals(
                    Text(generation, "candidateSetVersion"),
                    snapshot.CandidateSetVersion,
                    StringComparison.Ordinal)
                || Int(generation, "candidateCount", -1) != snapshot.AllCandidates.Count
                || Int(generation, "reviewedCandidateCount", -1) != snapshot.AllCandidates.Count
                || Int(generation, "semanticReviewCount", -1) != 0
                || !NotBlank(Text(generation, "completedAt")))
            {
                return false;
            }

            return JsonUtil.SemanticallyEquals(
                SortCandidateDecisions(map?["candidateDecisions"] as JsonArray),
                SortCandidateDecisions(retainedDecisions));
        }

        private static JsonArray SortCandidateDecisions(JsonArray decisions)
        {
            var result = new JsonArray();
            foreach (JsonObject decision in CandidateDecisionObjects(decisions)
                .OrderBy(item => Text(item, "id"), StringComparer.Ordinal))
            {
                result.Add(decision.DeepClone());
            }

            return result;
        }

        private sealed class CandidateDecisionResolution
        {
            public CandidateDecisionResolution(
                Dictionary<string, JsonObject> decisionsByCurrentId,
                HashSet<string> carriedForwardCandidateIds,
                int staleDecisionCount)
            {
                DecisionsByCurrentId = decisionsByCurrentId;
                CarriedForwardCandidateIds = carriedForwardCandidateIds;
                StaleDecisionCount = staleDecisionCount;
            }

            public Dictionary<string, JsonObject> DecisionsByCurrentId { get; }
            public HashSet<string> CarriedForwardCandidateIds { get; }
            public int CarriedForwardCount
            {
                get { return CarriedForwardCandidateIds.Count; }
            }
            public int StaleDecisionCount { get; }
        }

        private sealed class CandidateSnapshot
        {
            public CandidateSnapshot(
                string assetsRoot,
                string mapPath,
                JsonObject map,
                int knownViewCount,
                List<NavigationCallCandidate> allCandidates,
                List<NavigationCallCandidate> candidates,
                string candidateSetVersion,
                string query)
            {
                AssetsRoot = assetsRoot;
                MapPath = mapPath;
                Map = map;
                KnownViewCount = knownViewCount;
                AllCandidates = allCandidates;
                Candidates = candidates;
                CandidateSetVersion = candidateSetVersion;
                Query = query;
            }

            public string AssetsRoot { get; }
            public string MapPath { get; }
            public JsonObject Map { get; }
            public int KnownViewCount { get; }
            public List<NavigationCallCandidate> AllCandidates { get; }
            public List<NavigationCallCandidate> Candidates { get; }
            public string CandidateSetVersion { get; }
            public string Query { get; }
        }
    }
}
