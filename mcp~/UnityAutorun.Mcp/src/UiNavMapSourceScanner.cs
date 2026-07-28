using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace UnityAutorun.Mcp
{
    public static partial class UiNavMapSourceScanner
    {
        private static readonly Regex UiFormIdRegex = new Regex(@"public\s+const\s+int\s+(UIForm[A-Za-z0-9_]+)\s*=", RegexOptions.Compiled);
        private static readonly Regex UiFormTokenRegex = new Regex(@"UIFormId\.(UIForm[A-Za-z0-9_]+)|\b(UIForm[A-Za-z0-9_]+)\b", RegexOptions.Compiled);
        private static readonly Regex ViewClassRegex = new Regex(@"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
        private static readonly Regex ExButtonFieldRegex = new Regex(@"\b(?:private|public|protected|internal)?\s*(?:(?:Game\.)?ExButton|(?:UnityEngine\.UI\.)?Button)\s+(?:m_)?([A-Za-z0-9_]+(?:ExButton|Button))\b", RegexOptions.Compiled);
        private static readonly Regex ButtonHandlerRegex = new Regex(@"\b(?<control>[A-Za-z0-9_]+(?:ExButton|Button))\s*(?:\.onClick)?\s*\.\s*(?:Set|SetAsync|AddListener)\s*\(\s*(?<handler>[A-Za-z0-9_]+)\s*\)", RegexOptions.Compiled);
        private static readonly Regex MethodDeclarationRegex = new Regex(
            @"^\s*(?:(?:private|public|protected|internal|static|virtual|override|abstract|sealed|new|async|extern|unsafe|partial)\s+)*(?:void|bool|byte|sbyte|short|ushort|int|uint|long|ulong|float|double|decimal|char|string|[A-Z][A-Za-z0-9_.]*(?:<[^>{};]+>)?(?:\[\])?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>{};]+>)?\s*\(",
            RegexOptions.Compiled);

        public static JsonObject Scan(JsonObject args)
        {
            string assetsRoot = ResolveAssetsRoot();
            string query = Text(args, "query", "");
            string kind = Text(args, "kind", "all");
            int offset = Math.Max(0, Int(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, Int(args, "limit", 50)));
            bool includeText = Bool(args, "includeText", true);

            List<SourceEvidence> evidence = BuildEvidence(assetsRoot);
            JsonObject existingMap = LoadMapOrEmpty(UiNavMapPaths.ResolveMapPath(Text(args, "mapPath")));
            Dictionary<string, string> knownViews = BuildKnownViews(evidence, existingMap, args);
            int navigationCandidateCount = BuildSourceCodeNavigationModel(assetsRoot, knownViews)
                .BuildNavigationCandidates()
                .Count;
            List<SourceEvidence> filtered = evidence
                .Where(item => KindMatches(item, kind))
                .Where(item => QueryMatches(item, query))
                .OrderBy(item => item.Path)
                .ThenBy(item => item.Line)
                .ThenBy(item => item.Kind)
                .ToList();

            var items = new JsonArray();
            foreach (SourceEvidence item in filtered.Skip(offset).Take(limit))
            {
                items.Add(item.ToJson(assetsRoot, includeText));
            }

            return JsonUtil.Obj(
                ("ok", true),
                ("assetsRoot", assetsRoot),
                ("query", query),
                ("kind", kind),
                ("offset", offset),
                ("limit", limit),
                ("total", filtered.Count),
                ("counts", CountByKind(evidence)),
                ("coverage", BuildCoverage(evidence, Text(args, "mapPath"))),
                ("knownViewCount", knownViews.Count),
                ("navigationCandidateCount", navigationCandidateCount),
                ("items", items),
                ("workflowHint", "Use coverage gaps as the source backlog. Call trace_ui_navigation_calls for bounded button call-chain evidence. External AI must decide graph edges and submit them through validate_ui_nav_map_patch and merge_ui_nav_map_patch.")
            );
        }

        public static JsonObject TraceNavigationCalls(JsonObject args)
        {
            string assetsRoot = ResolveAssetsRoot();
            string mapPath = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            string query = Text(args, "query", "");
            int offset = Math.Max(0, Int(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, Int(args, "limit", 50)));
            List<SourceEvidence> evidence = BuildEvidence(assetsRoot);
            Dictionary<string, string> knownViews = BuildKnownViews(evidence, LoadMapOrEmpty(mapPath), args);
            List<NavigationCallCandidate> candidates = BuildSourceCodeNavigationModel(assetsRoot, knownViews)
                .BuildNavigationCandidates()
                .Where(item => item.Matches(query))
                .OrderBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = new JsonArray();
            foreach (NavigationCallCandidate candidate in candidates.Skip(offset).Take(limit))
            {
                items.Add(candidate.ToJson(assetsRoot));
            }

            return JsonUtil.Obj(
                ("ok", true),
                ("path", mapPath),
                ("query", query),
                ("offset", offset),
                ("limit", limit),
                ("total", candidates.Count),
                ("knownViewCount", knownViews.Count),
                ("items", items),
                ("decisionPolicy", StaticDecisionPolicy()),
                ("workflowHint", "Treat every item as call-graph evidence, not as a confirmed transition. External AI must determine source view, target role, transition kind, automation, and confidence before validating and merging a patch.")
            );
        }

        public static JsonObject Backfill(JsonObject args)
        {
            string assetsRoot = ResolveAssetsRoot();
            string mapPath = UiNavMapPaths.ResolveMapPath(Text(args, "mapPath"));
            bool previewOnly = Bool(args, "previewOnly", true);
            bool includeEvidenceBacklog = Bool(args, "includeEvidenceBacklog", true);
            JsonObject existingMap = LoadMapOrEmpty(mapPath);
            JsonObject patch = BuildSourcePatch(assetsRoot, existingMap, BuildEvidence(assetsRoot), includeEvidenceBacklog);

            if (previewOnly)
            {
                return JsonUtil.Obj(
                    ("ok", true),
                    ("path", mapPath),
                    ("previewOnly", true),
                    ("patchCounts", PatchCounts(patch)),
                    ("patch", patch),
                    ("decisionPolicy", StaticDecisionPolicy())
                );
            }

            JsonObject result = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                ("mapPath", mapPath),
                ("patch", patch),
                ("allowConflicts", true)
            ));
            result["sourceBackfill"] = JsonUtil.Obj(
                ("patchCounts", PatchCounts(patch)),
                ("message", "Deterministic source views and unresolved evidence were merged. No controls, transitions, or routes were inferred. Use trace_ui_navigation_calls, then validate and merge an AI-authored navigation patch.")
            );
            return result;
        }

        private static List<SourceEvidence> BuildEvidence(string assetsRoot)
        {
            var evidence = new List<SourceEvidence>();
            AddUiFormIds(evidence, assetsRoot);
            AddCodeEvidence(evidence, assetsRoot);
            AddPrefabs(evidence, assetsRoot);
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
                    if (match.Success)
                    {
                        evidence.Add(new SourceEvidence("ui-form-id", match.Groups[1].Value, path, lineNo, line.Trim(), "UIFormId." + match.Groups[1].Value));
                    }
                }
            }
        }

        private static void AddPrefabs(List<SourceEvidence> evidence, string assetsRoot)
        {
            HashSet<string> sourceViewNames = EvidenceViews(evidence, "ui-form-id", "ui-form-class", "view-class");
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories))
            {
                string view = Path.GetFileNameWithoutExtension(path);
                if (LooksLikeViewName(view) || sourceViewNames.Contains(view))
                {
                    evidence.Add(new SourceEvidence("prefab", view, path, 0, "", view));
                }
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

                string sourceView = InferSourceViewFromPath(path);
                int lineNo = 0;
                foreach (string line in File.ReadLines(path))
                {
                    lineNo++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    Match classMatch = ViewClassRegex.Match(trimmed);
                    if (classMatch.Success)
                    {
                        string className = classMatch.Groups["name"].Value;
                        if (LooksLikeViewName(className))
                        {
                            string classKind = className.StartsWith("UIForm", StringComparison.Ordinal)
                                ? "ui-form-class"
                                : "view-class";
                            evidence.Add(new SourceEvidence(classKind, className, path, lineNo, trimmed, className));
                        }
                    }

                    Match buttonMatch = ExButtonFieldRegex.Match(trimmed);
                    if (buttonMatch.Success && NotBlank(sourceView))
                    {
                        evidence.Add(new SourceEvidence("button-binding", sourceView, path, lineNo, trimmed, buttonMatch.Groups[1].Value));
                    }

                    if (!LooksLikeNavigationLine(trimmed))
                    {
                        continue;
                    }

                    List<string> views = ExtractUiForms(trimmed);
                    string kind = ClassifyNavigationLine(trimmed);
                    if (views.Count == 0)
                    {
                        evidence.Add(new SourceEvidence(kind, sourceView ?? "", path, lineNo, trimmed, ""));
                        continue;
                    }

                    foreach (string view in views)
                    {
                        evidence.Add(new SourceEvidence(kind, view, path, lineNo, trimmed, view));
                    }
                }
            }
        }

        private static JsonObject BuildCoverage(List<SourceEvidence> evidence, string mapPath)
        {
            var uiFormIds = EvidenceViews(evidence, "ui-form-id");
            var prefabs = EvidenceViews(evidence, "prefab");
            var classes = EvidenceViews(evidence, "ui-form-class");
            var genericClasses = EvidenceViews(evidence, "view-class");
            var openTargets = EvidenceViews(evidence, "open-ui-form");
            var buttonViews = EvidenceViews(evidence, "button-binding");
            var mappedViews = LoadMappedViews(mapPath);

            return JsonUtil.Obj(
                ("uiFormIdCount", uiFormIds.Count),
                ("prefabCount", prefabs.Count),
                ("uiFormClassCount", classes.Count),
                ("viewClassCount", genericClasses.Count),
                ("openTargetCount", openTargets.Count),
                ("buttonBindingViewCount", buttonViews.Count),
                ("mappedViewCount", mappedViews.Count),
                ("uiFormIdsMissingInMap", Missing(uiFormIds, mappedViews)),
                ("prefabsMissingInMap", Missing(prefabs, mappedViews)),
                ("classesMissingInMap", Missing(classes, mappedViews)),
                ("viewClassesMissingInMap", Missing(genericClasses, mappedViews)),
                ("openTargetsMissingInMap", Missing(openTargets, mappedViews)),
                ("buttonBindingViewsMissingInMap", Missing(buttonViews, mappedViews))
            );
        }

        private static JsonObject BuildSourcePatch(string assetsRoot, JsonObject existingMap, List<SourceEvidence> evidence, bool includeEvidenceBacklog)
        {
            var existingViews = ExistingViewTokens(existingMap);
            var existingUnresolved = ExistingIds(existingMap, "unresolved");
            var views = new JsonArray();
            foreach (string view in EvidenceViews(evidence, "ui-form-id", "prefab", "ui-form-class", "view-class").OrderBy(item => item))
            {
                if (!existingViews.Contains(NormalizeViewId(view)))
                {
                    views.Add(BuildView(view, assetsRoot, evidence));
                }
            }

            var unresolved = new JsonArray();
            if (includeEvidenceBacklog)
            {
                foreach (SourceEvidence item in evidence.Where(IsBacklogEvidence))
                {
                    string id = UnresolvedId(item, assetsRoot);
                    if (!existingUnresolved.Contains(id))
                    {
                        unresolved.Add(JsonUtil.Obj(
                            ("id", id),
                            ("kind", item.Kind),
                            ("view", item.View),
                            ("reason", "Source evidence needs concrete route/control modeling before reliable AutoRun navigation."),
                            ("source", item.SourceObject(assetsRoot))
                        ));
                    }
                }
            }

            return JsonUtil.Obj(
                ("views", views),
                ("controls", new JsonArray()),
                ("transitions", new JsonArray()),
                ("routes", new JsonArray()),
                ("unresolved", unresolved)
            );
        }

        private static JsonObject BuildView(string view, string assetsRoot, List<SourceEvidence> evidence)
        {
            var obj = JsonUtil.Obj(
                ("id", ViewIdForName(view)),
                ("name", view),
                ("rootObjectPath", view),
                ("framework", "ugui"),
                ("source", JsonUtil.Obj(("type", "source-scan")))
            );

            SourceEvidence prefab = evidence.FirstOrDefault(item => item.Kind == "prefab" && item.View == view);
            SourceEvidence script = evidence.FirstOrDefault(item =>
                (item.Kind == "ui-form-class" || item.Kind == "view-class")
                && item.View == view);
            if (prefab != null)
            {
                obj["prefabPath"] = RelativeToAssets(assetsRoot, prefab.Path);
            }

            if (script != null)
            {
                obj["scriptPath"] = RelativeToAssets(assetsRoot, script.Path);
            }

            return obj;
        }

        private static SourceCodeNavigationModel BuildSourceCodeNavigationModel(
            string assetsRoot,
            Dictionary<string, string> knownViews)
        {
            return BuildCallGraphNavigationModel(assetsRoot, knownViews);
        }

        private static Dictionary<string, string> BuildKnownViews(
            List<SourceEvidence> evidence,
            JsonObject existingMap,
            JsonObject args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string view in EvidenceViews(evidence, "ui-form-id", "prefab", "ui-form-class", "view-class"))
            {
                AddKnownView(result, view);
            }

            foreach (JsonObject view in existingMap?["views"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                AddKnownView(result, Text(view, "name"));
                AddKnownView(result, LastPathSegment(Text(view, "rootObjectPath")));
            }

            foreach (string view in StringArray(args, "knownViewNames"))
            {
                AddKnownView(result, view);
            }

            return result;
        }

        private static void AddKnownView(Dictionary<string, string> knownViews, string view)
        {
            if (!NotBlank(view) || !Regex.IsMatch(view, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                return;
            }

            knownViews[view] = view;
        }

        private static string LastPathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = value.Replace('\\', '/').TrimEnd('/');
            int separator = normalized.LastIndexOf('/');
            return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
        }

        private static JsonObject StaticDecisionPolicy()
        {
            return JsonUtil.Obj(
                ("stage", "static-analysis"),
                ("role", "evidence-only"),
                ("createsExecutableEdges", false),
                ("requiresExternalAiDecision", true),
                ("decisionFields", new JsonArray
                {
                    "fromViewId",
                    "toViewId",
                    "transitionKind",
                    "control",
                    "automation",
                    "confidence"
                })
            );
        }

        private static List<string> ExtractMethodBody(string[] lines, int startIndex, out int endIndex)
        {
            var body = new List<string>();
            bool opened = false;
            int depth = 0;
            endIndex = startIndex;
            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i];
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        opened = true;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                    }
                }

                if (opened)
                {
                    if (body.Count == 0 && i > startIndex)
                    {
                        for (int skipped = startIndex; skipped < i; skipped++)
                        {
                            body.Add(string.Empty);
                        }
                    }

                    body.Add(line);
                }

                if (opened && depth <= 0)
                {
                    endIndex = i;
                    break;
                }
            }

            return opened ? body : new List<string>();
        }

        private static bool LooksLikeNavigationLine(string line)
        {
            return line.Contains("OpenUIForm")
                || line.Contains("CloseUIForm")
                || line.Contains("TryCloseUIForm")
                || line.Contains("LoadScene")
                || line.Contains("ChangeState")
                || line.Contains("Procedure")
                || line.Contains("EventArgs")
                || line.Contains("UIFormId.");
        }

        private static string ClassifyNavigationLine(string line)
        {
            if (line.Contains("OpenUIForm")) return "open-ui-form";
            if (line.Contains("CloseUIForm") || line.Contains("TryCloseUIForm")) return "close-ui-form";
            if (line.Contains("LoadScene")) return "load-scene";
            if (line.Contains("ChangeState") || line.Contains("Procedure")) return "procedure-flow";
            if (line.Contains("EventArgs")) return "event-flow";
            return "ui-form-reference";
        }

        private static List<string> ExtractUiForms(string line)
        {
            var result = new SortedSet<string>();
            foreach (Match match in UiFormTokenRegex.Matches(line))
            {
                string value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (NotBlank(value))
                {
                    result.Add(value);
                }
            }

            return result.ToList();
        }

        private static bool IsBacklogEvidence(SourceEvidence item)
        {
            return item.Kind == "button-binding"
                || item.Kind == "open-ui-form"
                || item.Kind == "close-ui-form"
                || item.Kind == "load-scene"
                || item.Kind == "procedure-flow"
                || item.Kind == "event-flow"
                || item.Kind == "ui-form-reference";
        }

        private static HashSet<string> EvidenceViews(List<SourceEvidence> evidence, params string[] kinds)
        {
            var kindSet = new HashSet<string>(kinds);
            return new HashSet<string>(evidence.Where(item => kindSet.Contains(item.Kind)).Select(item => item.View).Where(NotBlank));
        }

        private static JsonArray Missing(HashSet<string> sourceViews, HashSet<string> mappedViews)
        {
            var array = new JsonArray();
            foreach (string view in sourceViews.Where(item => !mappedViews.Contains(NormalizeViewId(item))).OrderBy(item => item).Take(100))
            {
                array.Add(view);
            }

            return array;
        }

        private static JsonObject LoadMapOrEmpty(string mapPath)
        {
            if (!File.Exists(mapPath))
            {
                return JsonUtil.Obj(("views", new JsonArray()), ("unresolved", new JsonArray()));
            }

            return JsonNode.Parse(File.ReadAllText(mapPath)) as JsonObject
                ?? JsonUtil.Obj(("views", new JsonArray()), ("unresolved", new JsonArray()));
        }

        private static HashSet<string> LoadMappedViews(string mapPath)
        {
            var result = new HashSet<string>();
            string path = string.IsNullOrWhiteSpace(mapPath) ? UiNavMapPaths.ResolveDefaultMapPath() : UiNavMapPaths.ResolveMapPath(mapPath);
            if (!File.Exists(path))
            {
                return result;
            }

            JsonObject map = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            foreach (JsonObject view in map?["views"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                Add(result, NormalizeViewId(Text(view, "id")));
                Add(result, NormalizeViewId(Text(view, "name")));
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
                || item.Kind.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Symbol.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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
            return normalized.Contains("/Library/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/HybridCLR/Generate/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/Editor/", StringComparison.OrdinalIgnoreCase);
        }

        private static string InferSourceViewFromPath(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            int partialSeparator = fileName.IndexOf('.');
            string typeName = partialSeparator >= 0 ? fileName.Substring(0, partialSeparator) : fileName;
            return LooksLikeViewName(typeName) ? typeName : null;
        }

        private static bool LooksLikeViewName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.StartsWith("UIForm", StringComparison.Ordinal)
                || value.EndsWith("View", StringComparison.Ordinal)
                || value.EndsWith("Window", StringComparison.Ordinal)
                || value.EndsWith("Panel", StringComparison.Ordinal)
                || value.EndsWith("Screen", StringComparison.Ordinal)
                || value.EndsWith("Dialog", StringComparison.Ordinal)
                || value.EndsWith("Popup", StringComparison.Ordinal)
                || value.EndsWith("Page", StringComparison.Ordinal);
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

        private static string ViewIdForName(string view)
        {
            string token = view;
            if (token.StartsWith("UIForm", StringComparison.Ordinal))
            {
                token = token.Substring("UIForm".Length);
            }

            return "view." + PascalToDotted(token);
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

        private static string RelativeToAssets(string assetsRoot, string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? "Assets/" + full.Substring(root.Length).Replace('\\', '/')
                : full.Replace('\\', '/');
        }

        private static string UnresolvedId(SourceEvidence item, string assetsRoot)
        {
            string key = RelativeToAssets(assetsRoot, item.Path) + ":" + item.Line + ":" + item.Kind + ":" + item.View + ":" + item.Symbol;
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

        private static IEnumerable<string> StringArray(JsonObject obj, string key)
        {
            JsonArray values = obj?[key] as JsonArray;
            return values == null
                ? Enumerable.Empty<string>()
                : values
                    .Select(item => item == null ? null : item.GetValue<string>())
                    .Where(NotBlank);
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

            public JsonObject ToJson(string assetsRoot, bool includeText)
            {
                JsonObject obj = SourceObject(assetsRoot);
                if (!includeText)
                {
                    obj.Remove("text");
                }

                return obj;
            }

            public JsonObject SourceObject(string assetsRoot)
            {
                var obj = JsonUtil.Obj(
                    ("kind", Kind),
                    ("view", View),
                    ("path", RelativeToAssets(assetsRoot, Path)),
                    ("line", Line),
                    ("symbol", Symbol)
                );
                if (!string.IsNullOrWhiteSpace(Text))
                {
                    obj["text"] = Text;
                }

                return obj;
            }
        }
    }
}
