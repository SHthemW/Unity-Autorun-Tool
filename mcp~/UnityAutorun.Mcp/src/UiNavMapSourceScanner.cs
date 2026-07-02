using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static class UiNavMapSourceScanner
    {
        private static readonly Regex UiFormIdRegex = new Regex(@"public\s+const\s+int\s+(UIForm[A-Za-z0-9_]+)\s*=\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex UiFormTokenRegex = new Regex(@"UIFormId\.(UIForm[A-Za-z0-9_]+)|\b(UIForm[A-Za-z0-9_]+)\b", RegexOptions.Compiled);
        private static readonly Regex ClassRegex = new Regex(@"\bclass\s+(UIForm[A-Za-z0-9_]+)\b", RegexOptions.Compiled);

        public static JsonObject Scan(JsonObject args)
        {
            string assetsRoot = ResolveAssetsRoot();
            string query = Text(args, "query", "");
            string kind = Text(args, "kind", "all");
            int offset = Math.Max(0, Int(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, Int(args, "limit", 50)));
            bool includeText = Bool(args, "includeText", true);

            List<SourceEvidence> evidence = BuildEvidence(assetsRoot);
            List<SourceEvidence> filtered = evidence
                .Where(item => KindMatches(item, kind))
                .Where(item => QueryMatches(item, query))
                .OrderBy(item => item.Path)
                .ThenBy(item => item.Line)
                .ThenBy(item => item.Kind)
                .ToList();

            JsonArray items = new JsonArray();
            foreach (SourceEvidence item in filtered.Skip(offset).Take(limit))
            {
                items.Add(item.ToJson(includeText));
            }

            JsonObject coverage = BuildCoverage(evidence, Text(args, "mapPath"));
            return JsonUtil.Obj(
                ("ok", true),
                ("assetsRoot", assetsRoot),
                ("query", query),
                ("kind", kind),
                ("offset", offset),
                ("limit", limit),
                ("total", filtered.Count),
                ("counts", CountByKind(evidence)),
                ("coverage", coverage),
                ("items", items),
                ("workflowHint", "Use this source index before generating nav map patches. Include procedure, scene, event, and lifecycle evidence as flow transitions; do not stop at direct button transitions.")
            );
        }

        public static JsonObject Backfill(JsonObject args)
        {
            string assetsRoot = ResolveAssetsRoot();
            string mapPath = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            bool previewOnly = Bool(args, "previewOnly", false);
            bool includeEvidenceBacklog = Bool(args, "includeEvidenceBacklog", true);
            List<SourceEvidence> evidence = BuildEvidence(assetsRoot);
            JsonObject existingMap = LoadMapOrEmpty(mapPath);
            JsonObject patch = BuildSourcePatch(assetsRoot, existingMap, evidence, includeEvidenceBacklog);

            if (previewOnly)
            {
                return JsonUtil.Obj(
                    ("ok", true),
                    ("path", mapPath),
                    ("previewOnly", true),
                    ("patchCounts", PatchCounts(patch))
                );
            }

            JsonObject result = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                ("mapPath", mapPath),
                ("patch", patch)
            ));
            result["sourceBackfill"] = JsonUtil.Obj(
                ("patchCounts", PatchCounts(patch)),
                ("includeEvidenceBacklog", includeEvidenceBacklog),
                ("message", "Source-derived nav map baseline merged. Run get_nav_map_summary and resolve important routes next.")
            );
            return result;
        }

        private static List<SourceEvidence> BuildEvidence(string assetsRoot)
        {
            var evidence = new List<SourceEvidence>();
            AddUiFormIds(evidence, assetsRoot);
            AddPrefabs(evidence, assetsRoot);
            AddCodeEvidence(evidence, assetsRoot);
            return evidence;
        }

        private static void AddUiFormIds(List<SourceEvidence> evidence, string assetsRoot)
        {
            foreach (string path in EnumerateFiles(assetsRoot, "UIFormId.cs"))
            {
                int lineNo = 0;
                foreach (string line in File.ReadLines(path))
                {
                    lineNo++;
                    Match match = UiFormIdRegex.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    evidence.Add(new SourceEvidence("ui-form-id", match.Groups[1].Value, path, lineNo, line.Trim(), "UIFormId." + match.Groups[1].Value));
                }
            }
        }

        private static void AddPrefabs(List<SourceEvidence> evidence, string assetsRoot)
        {
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "UIForm*.prefab", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                evidence.Add(new SourceEvidence("prefab", name, path, 0, "", name));
            }
        }

        private static void AddCodeEvidence(List<SourceEvidence> evidence, string assetsRoot)
        {
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (IsIgnoredCodePath(path))
                {
                    continue;
                }

                int lineNo = 0;
                foreach (string line in File.ReadLines(path))
                {
                    lineNo++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    Match classMatch = ClassRegex.Match(trimmed);
                    if (classMatch.Success)
                    {
                        string view = classMatch.Groups[1].Value;
                        evidence.Add(new SourceEvidence("ui-form-class", view, path, lineNo, trimmed, view));
                    }

                    if (!LooksLikeNavigationLine(trimmed))
                    {
                        continue;
                    }

                    List<string> views = ExtractUiForms(trimmed);
                    string kind = ClassifyNavigationLine(trimmed);
                    if (views.Count == 0)
                    {
                        evidence.Add(new SourceEvidence(kind, "", path, lineNo, trimmed, ""));
                        continue;
                    }

                    foreach (string view in views)
                    {
                        evidence.Add(new SourceEvidence(kind, view, path, lineNo, trimmed, view));
                    }
                }
            }
        }

        private static bool LooksLikeNavigationLine(string line)
        {
            return line.IndexOf("OpenUIForm", StringComparison.Ordinal) >= 0
                || line.IndexOf("CloseUIForm", StringComparison.Ordinal) >= 0
                || line.IndexOf("TryCloseUIForm", StringComparison.Ordinal) >= 0
                || line.IndexOf("GetUIFormByUIFormId", StringComparison.Ordinal) >= 0
                || line.IndexOf("LoadScene", StringComparison.Ordinal) >= 0
                || line.IndexOf("ChangeState", StringComparison.Ordinal) >= 0
                || line.IndexOf("Procedure", StringComparison.Ordinal) >= 0
                || line.IndexOf("EventArgs", StringComparison.Ordinal) >= 0
                || line.IndexOf("DataNodeDefinition.UIForm", StringComparison.Ordinal) >= 0
                || line.IndexOf("UIFormId.", StringComparison.Ordinal) >= 0;
        }

        private static string ClassifyNavigationLine(string line)
        {
            if (line.IndexOf("OpenUIForm", StringComparison.Ordinal) >= 0)
            {
                return "open-ui-form";
            }

            if (line.IndexOf("CloseUIForm", StringComparison.Ordinal) >= 0 || line.IndexOf("TryCloseUIForm", StringComparison.Ordinal) >= 0)
            {
                return "close-ui-form";
            }

            if (line.IndexOf("LoadScene", StringComparison.Ordinal) >= 0)
            {
                return "load-scene";
            }

            if (line.IndexOf("ChangeState", StringComparison.Ordinal) >= 0 || line.IndexOf("Procedure", StringComparison.Ordinal) >= 0)
            {
                return "procedure-flow";
            }

            if (line.IndexOf("EventArgs", StringComparison.Ordinal) >= 0)
            {
                return "event-flow";
            }

            if (line.IndexOf("DataNodeDefinition.UIForm", StringComparison.Ordinal) >= 0)
            {
                return "data-node-flow";
            }

            return "ui-form-reference";
        }

        private static List<string> ExtractUiForms(string line)
        {
            var result = new HashSet<string>();
            foreach (Match match in UiFormTokenRegex.Matches(line))
            {
                string value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }

            return result.OrderBy(item => item).ToList();
        }

        private static JsonObject BuildCoverage(List<SourceEvidence> evidence, string mapPath)
        {
            var uiFormIds = new HashSet<string>(evidence.Where(item => item.Kind == "ui-form-id").Select(item => item.View).Where(NotBlank));
            var prefabs = new HashSet<string>(evidence.Where(item => item.Kind == "prefab").Select(item => item.View).Where(NotBlank));
            var classes = new HashSet<string>(evidence.Where(item => item.Kind == "ui-form-class").Select(item => item.View).Where(NotBlank));
            var openTargets = new HashSet<string>(evidence.Where(item => item.Kind == "open-ui-form").Select(item => item.View).Where(NotBlank));
            var mappedViews = LoadMappedViews(mapPath);

            return JsonUtil.Obj(
                ("uiFormIdCount", uiFormIds.Count),
                ("prefabCount", prefabs.Count),
                ("uiFormClassCount", classes.Count),
                ("openTargetCount", openTargets.Count),
                ("mappedViewCount", mappedViews.Count),
                ("uiFormIdsMissingInMap", StringArray(uiFormIds.Where(item => !mappedViews.Contains(NormalizeViewId(item))).OrderBy(item => item).Take(100))),
                ("prefabsMissingInMap", StringArray(prefabs.Where(item => !mappedViews.Contains(NormalizeViewId(item))).OrderBy(item => item).Take(100))),
                ("openTargetsMissingInMap", StringArray(openTargets.Where(item => !mappedViews.Contains(NormalizeViewId(item))).OrderBy(item => item).Take(100)))
            );
        }

        private static JsonObject BuildSourcePatch(string assetsRoot, JsonObject existingMap, List<SourceEvidence> evidence, bool includeEvidenceBacklog)
        {
            Dictionary<string, int> uiFormIds = BuildUiFormIds(evidence);
            Dictionary<string, string> prefabs = BuildFirstPathByView(evidence, "prefab");
            Dictionary<string, string> classes = BuildFirstPathByView(evidence, "ui-form-class");
            HashSet<string> existingViews = ExistingViewTokens(existingMap);
            HashSet<string> existingTransitions = ExistingIds(existingMap, "transitions");
            HashSet<string> existingRoutes = ExistingIds(existingMap, "routes");
            HashSet<string> existingUnresolved = ExistingIds(existingMap, "unresolved");

            var allViews = new SortedSet<string>(uiFormIds.Keys);
            foreach (string item in prefabs.Keys) allViews.Add(item);
            foreach (string item in classes.Keys) allViews.Add(item);

            var views = new JsonArray();
            foreach (string view in allViews)
            {
                if (existingViews.Contains(NormalizeViewId(view)))
                {
                    continue;
                }

                views.Add(BuildView(view, uiFormIds, prefabs, classes, assetsRoot));
            }

            var transitions = new JsonArray();
            var generatedTransitionIds = new HashSet<string>();
            foreach (SourceEvidence item in evidence.Where(item => item.Kind == "open-ui-form" && NotBlank(item.View)))
            {
                string sourceView = InferSourceViewFromPath(item.Path);
                if (!NotBlank(sourceView) || sourceView == item.View)
                {
                    continue;
                }

                string id = TransitionId(sourceView, item.View);
                if (existingTransitions.Contains(id) || generatedTransitionIds.Contains(id))
                {
                    continue;
                }

                transitions.Add(BuildFlowTransition(id, sourceView, item.View, "flow", "wait", item, assetsRoot));
                generatedTransitionIds.Add(id);
            }

            AddStartupTransition(transitions, generatedTransitionIds, existingTransitions, "UIFormLogin", "UIFormLoadScene", "ProcedureLogin/ProcedurePreload/ProcedureLoadGame startup flow", assetsRoot);
            AddStartupTransition(transitions, generatedTransitionIds, existingTransitions, "UIFormLoadScene", "UIFormOperation", "ProcedureLoadGame opens UIFormOperation after scene loading", assetsRoot);

            var routes = new JsonArray();
            string startupRouteId = "route.login.to.operation";
            if (!existingRoutes.Contains(startupRouteId))
            {
                routes.Add(JsonUtil.Obj(
                    ("id", startupRouteId),
                    ("fromViewId", ViewIdForName("UIFormLogin")),
                    ("toViewId", ViewIdForName("UIFormOperation")),
                    ("steps", new JsonArray
                    {
                        JsonUtil.Obj(("transitionId", TransitionId("UIFormLogin", "UIFormLoadScene"))),
                        JsonUtil.Obj(("transitionId", TransitionId("UIFormLoadScene", "UIFormOperation")))
                    }),
                    ("source", JsonUtil.Obj(("type", "source-backfill"), ("symbol", "startup-flow")))
                ));
            }

            var unresolved = new JsonArray();
            if (includeEvidenceBacklog)
            {
                foreach (SourceEvidence item in evidence.Where(IsBacklogEvidence))
                {
                    string id = UnresolvedId(item, assetsRoot);
                    if (existingUnresolved.Contains(id))
                    {
                        continue;
                    }

                    unresolved.Add(JsonUtil.Obj(
                        ("id", id),
                        ("kind", item.Kind),
                        ("view", item.View),
                        ("reason", "Source navigation evidence requires route/context refinement."),
                        ("source", SourceObject(item, assetsRoot))
                    ));
                }
            }

            return JsonUtil.Obj(
                ("views", views),
                ("controls", new JsonArray()),
                ("transitions", transitions),
                ("routes", routes),
                ("unresolved", unresolved)
            );
        }

        private static JsonObject BuildView(string view, Dictionary<string, int> uiFormIds, Dictionary<string, string> prefabs, Dictionary<string, string> classes, string assetsRoot)
        {
            var obj = JsonUtil.Obj(
                ("id", ViewIdForName(view)),
                ("name", view),
                ("rootObjectPath", view),
                ("framework", "ugui"),
                ("source", JsonUtil.Obj(("type", "source-backfill")))
            );

            if (prefabs.TryGetValue(view, out string prefabPath))
            {
                obj["prefabPath"] = RelativeToAssets(assetsRoot, prefabPath);
            }

            if (classes.TryGetValue(view, out string classPath))
            {
                obj["scriptPath"] = RelativeToAssets(assetsRoot, classPath);
            }

            if (uiFormIds.TryGetValue(view, out int uiFormId))
            {
                obj["uiFormId"] = uiFormId;
            }

            return obj;
        }

        private static JsonObject BuildFlowTransition(string id, string fromView, string toView, string kind, string mode, SourceEvidence item, string assetsRoot)
        {
            return JsonUtil.Obj(
                ("id", id),
                ("fromViewId", ViewIdForName(fromView)),
                ("toViewId", ViewIdForName(toView)),
                ("kind", kind),
                ("trigger", JsonUtil.Obj(("type", item.Kind), ("symbol", item.Symbol))),
                ("effect", JsonUtil.Obj(("type", "open-view"), ("targetViewId", ViewIdForName(toView)))),
                ("automation", JsonUtil.Obj(("mode", mode), ("waitForViewId", ViewIdForName(toView)), ("timeout", 15))),
                ("confidence", 0.7),
                ("source", SourceObject(item, assetsRoot))
            );
        }

        private static void AddStartupTransition(JsonArray transitions, HashSet<string> generated, HashSet<string> existing, string from, string to, string symbol, string assetsRoot)
        {
            string id = TransitionId(from, to);
            if (generated.Contains(id) || existing.Contains(id))
            {
                return;
            }

            var item = new SourceEvidence("procedure-flow", to, Path.Combine(assetsRoot, "Scripts/Game/Hot/Code/Runtime/Procedure"), 0, symbol, symbol);
            transitions.Add(BuildFlowTransition(id, from, to, "flow", "wait", item, assetsRoot));
            generated.Add(id);
        }

        private static bool IsBacklogEvidence(SourceEvidence item)
        {
            return item.Kind == "open-ui-form"
                || item.Kind == "close-ui-form"
                || item.Kind == "load-scene"
                || item.Kind == "procedure-flow"
                || item.Kind == "event-flow"
                || item.Kind == "data-node-flow"
                || item.Kind == "ui-form-reference";
        }

        private static JsonObject SourceObject(SourceEvidence item, string assetsRoot)
        {
            var source = JsonUtil.Obj(
                ("type", item.Kind),
                ("path", RelativeToAssets(assetsRoot, item.Path)),
                ("line", item.Line),
                ("symbol", item.Symbol)
            );
            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                source["text"] = item.Text;
            }

            return source;
        }

        private static JsonObject LoadMapOrEmpty(string mapPath)
        {
            if (!File.Exists(mapPath))
            {
                return JsonUtil.Obj(("views", new JsonArray()), ("transitions", new JsonArray()), ("routes", new JsonArray()), ("unresolved", new JsonArray()));
            }

            return JsonNode.Parse(File.ReadAllText(mapPath))?.AsObject()
                ?? JsonUtil.Obj(("views", new JsonArray()), ("transitions", new JsonArray()), ("routes", new JsonArray()), ("unresolved", new JsonArray()));
        }

        private static JsonObject PatchCounts(JsonObject patch)
        {
            return JsonUtil.Obj(
                ("views", (patch["views"] as JsonArray)?.Count ?? 0),
                ("controls", (patch["controls"] as JsonArray)?.Count ?? 0),
                ("transitions", (patch["transitions"] as JsonArray)?.Count ?? 0),
                ("routes", (patch["routes"] as JsonArray)?.Count ?? 0),
                ("unresolved", (patch["unresolved"] as JsonArray)?.Count ?? 0)
            );
        }

        private static Dictionary<string, int> BuildUiFormIds(List<SourceEvidence> evidence)
        {
            var result = new Dictionary<string, int>();
            foreach (SourceEvidence item in evidence.Where(item => item.Kind == "ui-form-id"))
            {
                Match match = UiFormIdRegex.Match(item.Text);
                if (match.Success && int.TryParse(match.Groups[2].Value, out int id) && !result.ContainsKey(item.View))
                {
                    result[item.View] = id;
                }
            }

            return result;
        }

        private static Dictionary<string, string> BuildFirstPathByView(List<SourceEvidence> evidence, string kind)
        {
            var result = new Dictionary<string, string>();
            foreach (SourceEvidence item in evidence.Where(item => item.Kind == kind && NotBlank(item.View)).OrderBy(item => item.Path))
            {
                if (!result.ContainsKey(item.View))
                {
                    result[item.View] = item.Path;
                }
            }

            return result;
        }

        private static HashSet<string> ExistingViewTokens(JsonObject map)
        {
            var result = new HashSet<string>();
            foreach (JsonObject view in map?["views"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                Add(result, NormalizeViewId(Text(view, "id")));
                Add(result, NormalizeViewId(Text(view, "name")));
            }

            return result;
        }

        private static HashSet<string> ExistingIds(JsonObject map, string key)
        {
            var result = new HashSet<string>();
            foreach (JsonObject item in map?[key]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                Add(result, Text(item, "id"));
            }

            return result;
        }

        private static string InferSourceViewFromPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Match match = Regex.Match(name, @"^(UIForm[A-Za-z0-9_]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ViewIdForName(string view)
        {
            string token = view;
            if (token.StartsWith("UIForm", StringComparison.Ordinal))
            {
                token = token.Substring("UIForm".Length);
            }

            return "view." + PascalToDotted(token);
        }

        private static string TransitionId(string from, string to)
        {
            return "transition." + ViewIdForName(from).Substring("view.".Length) + ".flow.to." + ViewIdForName(to).Substring("view.".Length);
        }

        private static string PascalToDotted(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            string dotted = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1.$2");
            dotted = Regex.Replace(dotted, "([A-Z]+)([A-Z][a-z])", "$1.$2");
            return dotted.Replace("_", ".").Replace("-", ".").ToLowerInvariant();
        }

        private static string UnresolvedId(SourceEvidence item, string assetsRoot)
        {
            string key = RelativeToAssets(assetsRoot, item.Path) + ":" + item.Line + ":" + item.Kind + ":" + item.View;
            return "unresolved.source." + StableHash(key);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private static HashSet<string> LoadMappedViews(string mapPath)
        {
            var result = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                mapPath = UiNavMapPaths.ResolveDefaultMapPath();
            }
            else
            {
                mapPath = UiNavMapPaths.ResolveMapPath(mapPath);
            }

            if (!File.Exists(mapPath))
            {
                return result;
            }

            JsonObject map = JsonNode.Parse(File.ReadAllText(mapPath)) as JsonObject;
            foreach (JsonObject view in map?["views"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                Add(result, NormalizeViewId(Text(view, "id")));
                Add(result, NormalizeViewId(Text(view, "name")));
            }

            return result;
        }

        private static JsonObject CountByKind(List<SourceEvidence> evidence)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (SourceEvidence item in evidence)
            {
                counts[item.Kind] = counts.ContainsKey(item.Kind) ? counts[item.Kind] + 1 : 1;
            }

            var result = new JsonObject();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static IEnumerable<string> EnumerateFiles(string root, string pattern)
        {
            return Directory.Exists(root)
                ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                : Enumerable.Empty<string>();
        }

        private static bool IsIgnoredCodePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Library/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/HybridCLR/Generate/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool KindMatches(SourceEvidence item, string kind)
        {
            return string.IsNullOrWhiteSpace(kind) || kind == "all" || string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool QueryMatches(SourceEvidence item, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return item.View.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Kind.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveAssetsRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(UiNavMapPaths.ResolveToolRootDirectory());
            while (directory != null)
            {
                if (string.Equals(directory.Name, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Cannot resolve Unity Assets root from tool root.");
        }

        private static string RelativeToAssets(string assetsRoot, string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? "Assets/" + full.Substring(root.Length).Replace('\\', '/')
                : full.Replace('\\', '/');
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

        private static string NormalizeViewId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string token = value.ToLowerInvariant();
            if (token.StartsWith("view."))
            {
                token = token.Substring("view.".Length);
            }

            return token.Replace(".", "").Replace("_", "").Replace("-", "").Replace(" ", "");
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

        private sealed class SourceEvidence
        {
            public SourceEvidence(string kind, string view, string path, int line, string text, string symbol)
            {
                Kind = kind;
                View = view ?? "";
                Path = path;
                Line = line;
                Text = text ?? "";
                Symbol = symbol ?? "";
            }

            public string Kind { get; }
            public string View { get; }
            public string Path { get; }
            public int Line { get; }
            public string Text { get; }
            public string Symbol { get; }

            public JsonObject ToJson(bool includeText)
            {
                string assetsRoot = ResolveAssetsRoot();
                JsonObject obj = JsonUtil.Obj(
                    ("kind", Kind),
                    ("view", View),
                    ("path", RelativeToAssets(assetsRoot, Path)),
                    ("line", Line),
                    ("symbol", Symbol)
                );
                if (includeText && !string.IsNullOrWhiteSpace(Text))
                {
                    obj["text"] = Text;
                }

                return obj;
            }
        }
    }
}
