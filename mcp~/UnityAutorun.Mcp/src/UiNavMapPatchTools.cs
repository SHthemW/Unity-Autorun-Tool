using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapPatchTools
    {
        private static readonly string[] ArrayKeys =
        {
            "views",
            "controls",
            "transitions",
            "routes",
            "unresolved",
            "candidateDecisions"
        };

        private static readonly string[] IdRequiredKeys =
        {
            "views",
            "controls",
            "transitions",
            "routes",
            "candidateDecisions"
        };

        public static JsonObject GetSummary(JsonObject args)
        {
            string path = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            JsonObject map = LoadOrSkeleton(path);
            JsonArray views = Array(map, "views");
            JsonArray controls = Array(map, "controls");
            JsonArray transitions = Array(map, "transitions");
            JsonArray routes = Array(map, "routes");
            JsonArray unresolved = Array(map, "unresolved");
            JsonArray candidateDecisions = Array(map, "candidateDecisions");

            var viewIds = new HashSet<string>(Objects(views).Select(item => Text(item, "id")).Where(NotBlank));
            var referencedViews = new HashSet<string>();
            var incoming = new Dictionary<string, int>();
            var outgoing = new Dictionary<string, int>();
            foreach (JsonObject transition in Objects(transitions))
            {
                string from = Text(transition, "fromViewId");
                string to = Text(transition, "toViewId");
                AddCount(outgoing, from);
                AddCount(incoming, to);
                Add(referencedViews, from);
                Add(referencedViews, to);
            }

            foreach (JsonObject route in Objects(routes))
            {
                Add(referencedViews, Text(route, "fromViewId"));
                Add(referencedViews, Text(route, "toViewId"));
            }

            var missingViewRefs = referencedViews.Where(item => !viewIds.Contains(item)).OrderBy(item => item).ToList();
            var isolatedViews = viewIds
                .Where(id => !incoming.ContainsKey(id) && !outgoing.ContainsKey(id))
                .OrderBy(id => id)
                .ToList();

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("exists", File.Exists(path)),
                ("counts", JsonUtil.Obj(
                    ("views", views.Count),
                    ("controls", controls.Count),
                    ("transitions", transitions.Count),
                    ("routes", routes.Count),
                    ("unresolved", unresolved.Count),
                    ("candidateDecisions", candidateDecisions.Count)
                )),
                ("version", UiNavMapMetadata.Describe(map)),
                ("generation", map["generation"]?.DeepClone()),
                ("frameworks", CountBy(Objects(views), "framework")),
                ("transitionKinds", CountBy(Objects(transitions), "kind")),
                ("missingViewRefs", StringArray(missingViewRefs.Take(50))),
                ("missingViewRefCount", missingViewRefs.Count),
                ("isolatedViews", StringArray(isolatedViews.Take(50))),
                ("isolatedViewCount", isolatedViews.Count),
                ("highDegreeViews", HighDegreeViews(viewIds, incoming, outgoing))
            );
        }

        public static JsonObject QueryItems(JsonObject args)
        {
            string path = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            JsonObject map = LoadOrSkeleton(path);
            string section = NormalizeSection(Text(args, "section", "views"));
            string query = Text(args, "query", "");
            int offset = Math.Max(0, Int(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(100, Int(args, "limit", 25)));

            List<JsonObject> matches = Objects(Array(map, section))
                .Where(item => Matches(item, query))
                .OrderBy(item => Text(item, "id") ?? Text(item, "name") ?? "")
                .ToList();

            var items = new JsonArray();
            foreach (JsonObject item in matches.Skip(offset).Take(limit))
            {
                items.Add(item.DeepClone());
            }

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("section", section),
                ("query", query),
                ("offset", offset),
                ("limit", limit),
                ("total", matches.Count),
                ("items", items)
            );
        }

        public static JsonObject GetSubgraph(JsonObject args)
        {
            string path = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            JsonObject map = LoadOrSkeleton(path);
            int depth = Math.Max(0, Math.Min(6, Int(args, "depth", 1)));
            HashSet<string> selectedViews = ResolveRootViews(map, args);
            if (selectedViews.Count == 0)
            {
                throw new InvalidOperationException("get_ui_nav_subgraph requires viewId, from, to, or query.");
            }

            ExpandViews(map, selectedViews, depth);

            var transitions = Objects(Array(map, "transitions"))
                .Where(item => selectedViews.Contains(Text(item, "fromViewId")) || selectedViews.Contains(Text(item, "toViewId")))
                .ToList();
            var transitionIds = new HashSet<string>(transitions.Select(item => Text(item, "id")).Where(NotBlank));
            var controlIds = new HashSet<string>(transitions.Select(item => Text(item, "controlId")).Where(NotBlank));

            var controls = Objects(Array(map, "controls"))
                .Where(item => selectedViews.Contains(Text(item, "viewId")) || controlIds.Contains(Text(item, "id")))
                .ToList();
            foreach (JsonObject control in controls)
            {
                Add(controlIds, Text(control, "id"));
            }

            var routes = Objects(Array(map, "routes"))
                .Where(route => RouteTouches(route, selectedViews, transitionIds))
                .ToList();

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("depth", depth),
                ("viewIds", StringArray(selectedViews.OrderBy(item => item))),
                ("map", JsonUtil.Obj(
                    ("schemaVersion", Text(map, "schemaVersion") ?? UiNavMapMetadata.SchemaVersion),
                    ("generatorVersion", Text(map, "generatorVersion")),
                    ("mapVersion", map["mapVersion"]?.DeepClone()),
                    ("generation", map["generation"]?.DeepClone()),
                    ("views", CloneWhere(Array(map, "views"), item => selectedViews.Contains(Text(item, "id")))),
                    ("controls", CloneList(controls)),
                    ("transitions", CloneList(transitions)),
                    ("routes", CloneList(routes)),
                    ("unresolved", CloneWhere(Array(map, "unresolved"), item => MatchesAny(item, selectedViews))),
                    ("candidateDecisions", new JsonArray())
                ))
            );
        }

        public static JsonObject ValidatePatch(JsonObject args)
        {
            string path = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            JsonObject map = LoadOrSkeleton(path);
            JsonObject patch = ReadPatch(args);
            return ValidatePatchAgainstMap(map, patch, path);
        }

        public static JsonObject MergePatch(JsonObject args)
        {
            string path = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            JsonObject map = LoadOrSkeleton(path);
            JsonObject patch = ReadPatch(args);
            JsonObject validation = ValidatePatchAgainstMap(map, patch, path);
            if (validation["ok"]?.GetValue<bool>() != true)
            {
                return validation;
            }

            if (Array(validation, "conflicts").Count > 0 && !Bool(args, "allowConflicts", false))
            {
                validation["ok"] = false;
                validation["code"] = "patch_conflicts";
                validation["message"] = "Patch has conflicts. Review validate_ui_nav_map_patch output, then call merge_ui_nav_map_patch with allowConflicts=true only when overwriting is intentional.";
                return validation;
            }

            bool created = !File.Exists(path);
            var changed = new JsonArray();
            foreach (string key in ArrayKeys)
            {
                JsonObject result = MergeArray(map, patch, key);
                if (result["added"]?.GetValue<int>() > 0 || result["updated"]?.GetValue<int>() > 0)
                {
                    changed.Add(result);
                }
            }

            if (!(map["project"] is JsonObject))
            {
                map["project"] = JsonUtil.Obj(("source", "incremental-mcp-patches"));
            }

            UiNavMapMetadata.PrepareForWrite(map, true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtil.Pretty(SortMap(map)) + Environment.NewLine, new UTF8Encoding(false));

            return JsonUtil.Obj(
                ("ok", true),
                ("path", path),
                ("created", created),
                ("changed", changed),
                ("version", UiNavMapMetadata.Describe(map)),
                ("summary", GetSummary(JsonUtil.Obj(("mapPath", path))))
            );
        }

        private static JsonObject ValidatePatchAgainstMap(JsonObject map, JsonObject patch, string path)
        {
            var errors = new JsonArray();
            var warnings = new JsonArray();
            var conflicts = new JsonArray();

            foreach (string key in patch.Select(item => item.Key).ToList())
            {
                if (!ArrayKeys.Contains(key)
                    && key != "schemaVersion"
                    && key != "generatorVersion"
                    && key != "mapVersion"
                    && key != "generation"
                    && key != "project"
                    && key != "generatedAt")
                {
                    warnings.Add("Unknown patch key ignored by merge: " + key);
                }
            }

            foreach (string key in ArrayKeys)
            {
                if (patch[key] != null && !(patch[key] is JsonArray))
                {
                    errors.Add("Patch key must be an array: " + key);
                }
            }

            foreach (string key in IdRequiredKeys)
            {
                ValidateIds(patch, key, errors, conflicts);
                FindConflicts(map, patch, key, conflicts);
            }

            ValidateReferences(map, patch, errors, warnings);
            ValidateCandidateDecisions(map, patch, errors, warnings);

            return JsonUtil.Obj(
                ("ok", errors.Count == 0),
                ("path", path),
                ("errors", errors),
                ("warnings", warnings),
                ("conflicts", conflicts),
                ("patchCounts", JsonUtil.Obj(
                    ("views", Array(patch, "views").Count),
                    ("controls", Array(patch, "controls").Count),
                    ("transitions", Array(patch, "transitions").Count),
                    ("routes", Array(patch, "routes").Count),
                    ("unresolved", Array(patch, "unresolved").Count),
                    ("candidateDecisions", Array(patch, "candidateDecisions").Count)
                ))
            );
        }

        private static void ValidateCandidateDecisions(
            JsonObject map,
            JsonObject patch,
            JsonArray errors,
            JsonArray warnings)
        {
            HashSet<string> transitionIds = ExistingAndPatchIds(map, patch, "transitions");
            HashSet<string> unresolvedIds = ExistingAndPatchIds(map, patch, "unresolved");

            foreach (JsonObject decision in Objects(Array(patch, "candidateDecisions")))
            {
                string id = Text(decision, "id");
                string candidateVersion = Text(decision, "candidateVersion");
                string outcome = Text(decision, "outcome");
                string targetId = Text(decision, "targetId");
                string reason = Text(decision, "reason");
                JsonObject nonTransitionEvidence =
                    decision["nonTransitionEvidence"] as JsonObject;

                if (NotBlank(id) && !id.StartsWith("candidate.navigation.", StringComparison.Ordinal))
                {
                    warnings.Add("Candidate decision id does not use candidate.navigation.*: " + id);
                }

                if (!NotBlank(candidateVersion)
                    || !candidateVersion.StartsWith("candidate-evidence.", StringComparison.Ordinal))
                {
                    errors.Add("Candidate decision must copy candidateVersion from coverage output: " + id);
                }

                if (outcome != "transition" && outcome != "unresolved" && outcome != "ignored")
                {
                    errors.Add("Candidate decision outcome must be transition, unresolved, or ignored: " + id);
                    continue;
                }

                if (outcome == "transition" && (!NotBlank(targetId) || !transitionIds.Contains(targetId)))
                {
                    errors.Add("Transition candidate decision must reference a known transition targetId: " + id);
                }
                else if (outcome == "unresolved" && (!NotBlank(targetId) || !unresolvedIds.Contains(targetId)))
                {
                    errors.Add("Unresolved candidate decision must reference a known unresolved targetId: " + id);
                }
                else if (outcome == "ignored" && !NotBlank(reason))
                {
                    errors.Add("Ignored candidate decision must include a concise reason: " + id);
                }

                if (nonTransitionEvidence != null)
                {
                    string evidenceKind = Text(nonTransitionEvidence, "kind");
                    string evidenceSummary = Text(nonTransitionEvidence, "summary");
                    if (outcome == "transition")
                    {
                        errors.Add("Transition candidate decision must not include nonTransitionEvidence: " + id);
                    }

                    if (!NotBlank(evidenceKind) || !NotBlank(evidenceSummary))
                    {
                        errors.Add("nonTransitionEvidence requires kind and summary: " + id);
                    }
                }
            }
        }

        internal static JsonObject ValidateCandidateDecisionLedger(JsonObject map)
        {
            EnsureArrays(map);
            var errors = new JsonArray();
            var warnings = new JsonArray();
            ValidateCandidateDecisions(Skeleton(), map, errors, warnings);
            return JsonUtil.Obj(
                ("ok", errors.Count == 0),
                ("errors", errors),
                ("warnings", warnings)
            );
        }

        private static void ValidateReferences(JsonObject map, JsonObject patch, JsonArray errors, JsonArray warnings)
        {
            var viewIds = ExistingAndPatchIds(map, patch, "views");
            var controlIds = ExistingAndPatchIds(map, patch, "controls");
            var transitionIds = ExistingAndPatchIds(map, patch, "transitions");

            foreach (JsonObject control in Objects(Array(patch, "controls")))
            {
                string viewId = Text(control, "viewId");
                if (NotBlank(viewId) && !viewIds.Contains(viewId))
                {
                    warnings.Add("Control references unknown viewId: " + Text(control, "id") + " -> " + viewId);
                }
            }

            foreach (JsonObject transition in Objects(Array(patch, "transitions")))
            {
                RequireKnownRef(transition, "fromViewId", viewIds, warnings);
                RequireKnownRef(transition, "toViewId", viewIds, warnings);
                string controlId = Text(transition, "controlId");
                if (NotBlank(controlId) && !controlIds.Contains(controlId))
                {
                    warnings.Add("Transition references unknown controlId: " + Text(transition, "id") + " -> " + controlId);
                }
            }

            foreach (JsonObject route in Objects(Array(patch, "routes")))
            {
                RequireKnownRef(route, "fromViewId", viewIds, warnings);
                RequireKnownRef(route, "toViewId", viewIds, warnings);
                foreach (JsonObject step in Objects(route["steps"] as JsonArray ?? new JsonArray()))
                {
                    string transitionId = Text(step, "transitionId");
                    if (NotBlank(transitionId) && !transitionIds.Contains(transitionId))
                    {
                        warnings.Add("Route step references unknown transitionId: " + Text(route, "id") + " -> " + transitionId);
                    }
                }
            }
        }

        private static void RequireKnownRef(JsonObject item, string field, HashSet<string> ids, JsonArray warnings)
        {
            string value = Text(item, field);
            if (NotBlank(value) && !ids.Contains(value))
            {
                warnings.Add((Text(item, "id") ?? "item") + " references unknown " + field + ": " + value);
            }
        }

        private static void ValidateIds(JsonObject patch, string key, JsonArray errors, JsonArray conflicts)
        {
            var seen = new HashSet<string>();
            foreach (JsonObject item in Objects(Array(patch, key)))
            {
                string id = Text(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add("Patch " + key + " item is missing id.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors.Add("Patch " + key + " contains duplicate id: " + id);
                }
            }
        }

        private static void FindConflicts(JsonObject map, JsonObject patch, string key, JsonArray conflicts)
        {
            Dictionary<string, JsonObject> existing = ById(Array(map, key));
            foreach (JsonObject item in Objects(Array(patch, key)))
            {
                string id = Text(item, "id");
                if (id == null || !existing.ContainsKey(id))
                {
                    continue;
                }

                string oldText = JsonUtil.Pretty(existing[id]);
                string newText = JsonUtil.Pretty(item);
                if (oldText != newText)
                {
                    conflicts.Add(JsonUtil.Obj(("section", key), ("id", id), ("action", "update")));
                }
            }
        }

        private static JsonObject MergeArray(JsonObject map, JsonObject patch, string key)
        {
            JsonArray target = Array(map, key);
            Dictionary<string, int> indexById = new Dictionary<string, int>();
            for (int i = 0; i < target.Count; i++)
            {
                string id = Text(target[i] as JsonObject, "id");
                if (id != null && !indexById.ContainsKey(id))
                {
                    indexById[id] = i;
                }
            }

            int added = 0;
            int updated = 0;
            foreach (JsonObject item in Objects(Array(patch, key)))
            {
                string id = Text(item, "id");
                if (id != null && indexById.ContainsKey(id))
                {
                    target[indexById[id]] = item.DeepClone();
                    updated++;
                    continue;
                }

                target.Add(item.DeepClone());
                added++;
            }

            map[key] = target;
            return JsonUtil.Obj(("section", key), ("added", added), ("updated", updated));
        }

        internal static JsonObject SortMap(JsonObject map)
        {
            foreach (string key in ArrayKeys)
            {
                JsonArray sorted = new JsonArray();
                foreach (JsonObject item in Objects(Array(map, key)).OrderBy(item => Text(item, "id") ?? Text(item, "name") ?? JsonUtil.Pretty(item)))
                {
                    sorted.Add(item.DeepClone());
                }

                map[key] = sorted;
            }

            return map;
        }

        private static JsonObject ReadPatch(JsonObject args)
        {
            if (args?["patch"] is JsonObject patchObject)
            {
                return patchObject.DeepClone().AsObject();
            }

            string patchJson = Text(args, "patchJson");
            if (!string.IsNullOrWhiteSpace(patchJson))
            {
                return JsonNode.Parse(patchJson)?.AsObject()
                    ?? throw new InvalidOperationException("patchJson must be a JSON object.");
            }

            throw new InvalidOperationException("Patch tool requires either patch object or patchJson string.");
        }

        internal static JsonObject LoadOrSkeleton(string path)
        {
            if (!File.Exists(path))
            {
                return Skeleton();
            }

            JsonObject map = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidOperationException("Invalid UI navigation map: " + path);
            EnsureArrays(map);
            return map;
        }

        private static JsonObject Skeleton()
        {
            return JsonUtil.Obj(
                ("schemaVersion", UiNavMapMetadata.SchemaVersion),
                ("generatorVersion", UiNavMapMetadata.GeneratorVersion),
                ("mapVersion", 0),
                ("generatedAt", DateTimeOffset.UtcNow.ToString("o")),
                ("project", JsonUtil.Obj(("source", "incremental-mcp-patches"))),
                ("generation", JsonUtil.Obj(
                    ("status", "review-required"),
                    ("candidateProtocolVersion", UiNavMapMetadata.CandidateProtocolVersion)
                )),
                ("views", new JsonArray()),
                ("controls", new JsonArray()),
                ("transitions", new JsonArray()),
                ("routes", new JsonArray()),
                ("unresolved", new JsonArray()),
                ("candidateDecisions", new JsonArray())
            );
        }

        internal static void EnsureArrays(JsonObject map)
        {
            foreach (string key in ArrayKeys)
            {
                if (!(map[key] is JsonArray))
                {
                    map[key] = new JsonArray();
                }
            }
        }

        private static JsonArray Array(JsonObject map, string key)
        {
            return map?[key] as JsonArray ?? new JsonArray();
        }

        private static IEnumerable<JsonObject> Objects(JsonArray array)
        {
            return array?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();
        }

        private static string Text(JsonObject obj, string key, string fallback = null)
        {
            return obj?[key]?.GetValue<string>() ?? fallback;
        }

        private static int Int(JsonObject obj, string key, int fallback)
        {
            return obj?[key]?.GetValue<int>() ?? fallback;
        }

        private static bool Bool(JsonObject obj, string key, bool fallback)
        {
            return obj?[key]?.GetValue<bool>() ?? fallback;
        }

        private static bool NotBlank(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static void Add(HashSet<string> values, string value)
        {
            if (NotBlank(value))
            {
                values.Add(value);
            }
        }

        private static void AddCount(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
        }

        private static JsonObject CountBy(IEnumerable<JsonObject> items, string key)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (JsonObject item in items)
            {
                string value = Text(item, key) ?? "unknown";
                counts[value] = counts.ContainsKey(value) ? counts[value] + 1 : 1;
            }

            var result = new JsonObject();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static JsonArray HighDegreeViews(HashSet<string> viewIds, Dictionary<string, int> incoming, Dictionary<string, int> outgoing)
        {
            var result = new JsonArray();
            foreach (string id in viewIds
                .OrderByDescending(id => Count(incoming, id) + Count(outgoing, id))
                .ThenBy(id => id)
                .Take(20))
            {
                int degree = Count(incoming, id) + Count(outgoing, id);
                if (degree > 0)
                {
                    result.Add(JsonUtil.Obj(("viewId", id), ("incoming", Count(incoming, id)), ("outgoing", Count(outgoing, id)), ("degree", degree)));
                }
            }

            return result;
        }

        private static int Count(Dictionary<string, int> counts, string key)
        {
            return counts.ContainsKey(key) ? counts[key] : 0;
        }

        private static string NormalizeSection(string section)
        {
            if (ArrayKeys.Contains(section))
            {
                return section;
            }

            throw new InvalidOperationException("Unknown nav map section: " + section);
        }

        private static bool Matches(JsonObject item, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return JsonUtil.Pretty(item).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesAny(JsonObject item, HashSet<string> values)
        {
            string text = JsonUtil.Pretty(item);
            return values.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static HashSet<string> ResolveRootViews(JsonObject map, JsonObject args)
        {
            var roots = new HashSet<string>();
            AddResolvedView(map, roots, Text(args, "viewId"));
            AddResolvedView(map, roots, Text(args, "from"));
            AddResolvedView(map, roots, Text(args, "to"));

            string query = Text(args, "query");
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (JsonObject view in Objects(Array(map, "views")).Where(item => Matches(item, query)))
                {
                    Add(roots, Text(view, "id"));
                }
            }

            return roots;
        }

        private static void AddResolvedView(JsonObject map, HashSet<string> roots, string token)
        {
            string id = ResolveViewId(map, token);
            Add(roots, id);
        }

        private static string ResolveViewId(JsonObject map, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string normalized = NormalizeToken(token);
            JsonObject view = Objects(Array(map, "views")).FirstOrDefault(item =>
                Text(item, "id") == token
                || Text(item, "name") == token
                || NormalizeToken(Text(item, "id")) == normalized
                || NormalizeToken(Text(item, "name")) == normalized);
            return Text(view, "id") ?? token;
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : value.ToLowerInvariant().Replace("view.", "").Replace(".", "").Replace("_", "").Replace("-", "").Replace(" ", "");
        }

        private static void ExpandViews(JsonObject map, HashSet<string> selectedViews, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                var next = new HashSet<string>(selectedViews);
                foreach (JsonObject transition in Objects(Array(map, "transitions")))
                {
                    string from = Text(transition, "fromViewId");
                    string to = Text(transition, "toViewId");
                    if (selectedViews.Contains(from))
                    {
                        Add(next, to);
                    }

                    if (selectedViews.Contains(to))
                    {
                        Add(next, from);
                    }
                }

                if (next.Count == selectedViews.Count)
                {
                    return;
                }

                selectedViews.Clear();
                foreach (string item in next)
                {
                    selectedViews.Add(item);
                }
            }
        }

        private static bool RouteTouches(JsonObject route, HashSet<string> viewIds, HashSet<string> transitionIds)
        {
            if (viewIds.Contains(Text(route, "fromViewId")) || viewIds.Contains(Text(route, "toViewId")))
            {
                return true;
            }

            foreach (JsonObject step in Objects(route["steps"] as JsonArray ?? new JsonArray()))
            {
                if (transitionIds.Contains(Text(step, "transitionId")))
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonArray CloneWhere(JsonArray source, Func<JsonObject, bool> predicate)
        {
            return CloneList(Objects(source).Where(predicate));
        }

        private static JsonArray CloneList(IEnumerable<JsonObject> items)
        {
            var result = new JsonArray();
            foreach (JsonObject item in items)
            {
                result.Add(item.DeepClone());
            }

            return result;
        }

        private static JsonArray StringArray(IEnumerable<string> values)
        {
            var array = new JsonArray();
            foreach (string value in values)
            {
                array.Add(value);
            }

            return array;
        }

        private static Dictionary<string, JsonObject> ById(JsonArray array)
        {
            var result = new Dictionary<string, JsonObject>();
            foreach (JsonObject item in Objects(array))
            {
                string id = Text(item, "id");
                if (id != null && !result.ContainsKey(id))
                {
                    result[id] = item;
                }
            }

            return result;
        }

        private static HashSet<string> ExistingAndPatchIds(JsonObject map, JsonObject patch, string key)
        {
            var ids = new HashSet<string>();
            foreach (JsonObject item in Objects(Array(map, key)).Concat(Objects(Array(patch, key))))
            {
                Add(ids, Text(item, "id"));
            }

            return ids;
        }
    }
}
