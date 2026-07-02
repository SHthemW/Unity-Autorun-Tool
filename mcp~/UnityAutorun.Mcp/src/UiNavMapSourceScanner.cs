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
        private static readonly Regex UiFormIdRegex = new Regex(@"public\s+const\s+int\s+(UIForm[A-Za-z0-9_]+)\s*=", RegexOptions.Compiled);
        private static readonly Regex UiFormTokenRegex = new Regex(@"UIFormId\.(UIForm[A-Za-z0-9_]+)|\b(UIForm[A-Za-z0-9_]+)\b", RegexOptions.Compiled);
        private static readonly Regex UiFormClassRegex = new Regex(@"\bclass\s+(UIForm[A-Za-z0-9_]+)\b", RegexOptions.Compiled);
        private static readonly Regex ExButtonFieldRegex = new Regex(@"\b(?:private|public|protected)?\s*(?:Game\.)?ExButton\s+(?:m_)?([A-Za-z0-9_]+ExButton)\b", RegexOptions.Compiled);
        private static readonly Regex ButtonHandlerRegex = new Regex(@"\b(?<control>[A-Za-z0-9_]+ExButton)\s*(?:\.onClick)?\s*\.\s*(?:Set|SetAsync|AddListener)\s*\(\s*(?<handler>[A-Za-z0-9_]+)\s*\)", RegexOptions.Compiled);
        private static readonly Regex MethodDeclarationRegex = new Regex(@"\b(?:private|public|protected|internal)?\s*(?:async\s+)?(?:void|UniTask(?:Void)?|Task|IEnumerator|bool|int)\s+(?<name>[A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);

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
                ("items", items),
                ("workflowHint", "Use coverage gaps as the source backlog. Model startup gates such as login/auth/update/loading before claiming app-start routes reach the main UI.")
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
                    ("patch", patch)
                );
            }

            JsonObject result = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                ("mapPath", mapPath),
                ("patch", patch),
                ("allowConflicts", true)
            ));
            result["sourceBackfill"] = JsonUtil.Obj(
                ("patchCounts", PatchCounts(patch)),
                ("message", "Source-derived baseline merged. Inspect unresolved evidence and add concrete controls/transitions with merge_ui_nav_map_patch.")
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
                    if (match.Success)
                    {
                        evidence.Add(new SourceEvidence("ui-form-id", match.Groups[1].Value, path, lineNo, line.Trim(), "UIFormId." + match.Groups[1].Value));
                    }
                }
            }
        }

        private static void AddPrefabs(List<SourceEvidence> evidence, string assetsRoot)
        {
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "UIForm*.prefab", SearchOption.AllDirectories))
            {
                string view = Path.GetFileNameWithoutExtension(path);
                evidence.Add(new SourceEvidence("prefab", view, path, 0, "", view));
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

                    Match classMatch = UiFormClassRegex.Match(trimmed);
                    if (classMatch.Success)
                    {
                        evidence.Add(new SourceEvidence("ui-form-class", classMatch.Groups[1].Value, path, lineNo, trimmed, classMatch.Groups[1].Value));
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
            var openTargets = EvidenceViews(evidence, "open-ui-form");
            var buttonViews = EvidenceViews(evidence, "button-binding");
            var mappedViews = LoadMappedViews(mapPath);

            return JsonUtil.Obj(
                ("uiFormIdCount", uiFormIds.Count),
                ("prefabCount", prefabs.Count),
                ("uiFormClassCount", classes.Count),
                ("openTargetCount", openTargets.Count),
                ("buttonBindingViewCount", buttonViews.Count),
                ("mappedViewCount", mappedViews.Count),
                ("uiFormIdsMissingInMap", Missing(uiFormIds, mappedViews)),
                ("prefabsMissingInMap", Missing(prefabs, mappedViews)),
                ("classesMissingInMap", Missing(classes, mappedViews)),
                ("openTargetsMissingInMap", Missing(openTargets, mappedViews)),
                ("buttonBindingViewsMissingInMap", Missing(buttonViews, mappedViews))
            );
        }

        private static JsonObject BuildSourcePatch(string assetsRoot, JsonObject existingMap, List<SourceEvidence> evidence, bool includeEvidenceBacklog)
        {
            var existingViews = ExistingViewTokens(existingMap);
            var existingControls = ExistingIds(existingMap, "controls");
            var existingTransitions = ExistingIds(existingMap, "transitions");
            var existingRoutes = ExistingIds(existingMap, "routes");
            var existingUnresolved = ExistingIds(existingMap, "unresolved");
            var views = new JsonArray();
            foreach (string view in EvidenceViews(evidence, "ui-form-id", "prefab", "ui-form-class").OrderBy(item => item))
            {
                if (!existingViews.Contains(NormalizeViewId(view)))
                {
                    views.Add(BuildView(view, assetsRoot, evidence));
                }
            }

            SourceNavigationPatch sourceNavigation = BuildSourceNavigationPatch(assetsRoot, existingMap, evidence, existingControls, existingTransitions, existingRoutes);

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
                ("controls", sourceNavigation.Controls),
                ("transitions", sourceNavigation.Transitions),
                ("routes", sourceNavigation.Routes),
                ("unresolved", unresolved)
            );
        }

        private static SourceNavigationPatch BuildSourceNavigationPatch(
            string assetsRoot,
            JsonObject existingMap,
            List<SourceEvidence> evidence,
            HashSet<string> existingControls,
            HashSet<string> existingTransitions,
            HashSet<string> existingRoutes)
        {
            var result = new SourceNavigationPatch();
            SourceCodeNavigationModel model = BuildSourceCodeNavigationModel(assetsRoot);
            foreach (ButtonBinding binding in model.ButtonBindings.OrderBy(item => item.SourceView).ThenBy(item => item.ControlProperty).ThenBy(item => item.Handler))
            {
                HandlerFlow handler;
                if (!model.TryGetHandler(binding.SourceView, binding.Handler, out handler))
                {
                    continue;
                }

                foreach (string targetView in handler.OpenTargets.OrderBy(item => item))
                {
                    AddClickOpenTransition(result, assetsRoot, existingMap, existingControls, existingTransitions, existingRoutes, binding, targetView, handler, "open-ui-form", 0.9f);
                }

                if (handler.FiresLoginRequest && CanInferLoginToOperationFlow(assetsRoot))
                {
                    AddClickOpenTransition(result, assetsRoot, existingMap, existingControls, existingTransitions, existingRoutes, binding, "UIFormOperation", handler, "login-flow", 0.8f, 120);
                }
            }

            AddStartupLoginGate(result, assetsRoot, existingMap, evidence, existingTransitions, existingRoutes);
            return result;
        }

        private static void AddClickOpenTransition(
            SourceNavigationPatch patch,
            string assetsRoot,
            JsonObject existingMap,
            HashSet<string> existingControls,
            HashSet<string> existingTransitions,
            HashSet<string> existingRoutes,
            ButtonBinding binding,
            string targetView,
            HandlerFlow handler,
            string sourceType,
            float confidence,
            int waitTimeout = 0)
        {
            string fromViewId = ViewIdForName(existingMap, binding.SourceView);
            string toViewId = ViewIdForName(existingMap, targetView);
            if (string.IsNullOrEmpty(fromViewId) || string.IsNullOrEmpty(toViewId) || fromViewId == toViewId)
            {
                return;
            }

            PrefabButtonInfo button = ResolvePrefabButtonInfo(assetsRoot, binding.SourceView, binding.ControlProperty);
            string controlToken = TokenForControl(binding.ControlProperty, toViewId);
            string fromToken = TokenForViewId(fromViewId);
            string toToken = TokenForViewId(toViewId);
            string controlId = "control." + fromToken + "." + controlToken;
            string transitionId = "transition." + fromToken + "." + controlToken + ".to." + toToken;
            string routeId = "route." + fromToken + ".to." + toToken;

            if (!patch.ControlIds.Contains(controlId))
            {
                patch.ControlIds.Add(controlId);
                patch.Controls.Add(JsonUtil.Obj(
                    ("id", controlId),
                    ("viewId", fromViewId),
                    ("type", "button"),
                    ("name", button.Name),
                    ("text", "untitled"),
                    ("objectPath", button.ObjectPath),
                    ("framework", "ugui"),
                    ("autoRun", JsonUtil.Obj(
                        ("buttonName", button.Name),
                        ("buttonText", "untitled"),
                        ("isFairyGUI", false),
                        ("delay", 0.5)
                    )),
                    ("source", JsonUtil.Obj(
                        ("type", "code-bind"),
                        ("summary", binding.ControlProperty + " is bound to " + binding.Handler + ".")
                    ))
                ));
            }

            if (!patch.TransitionIds.Contains(transitionId))
            {
                patch.TransitionIds.Add(transitionId);
                JsonObject automation = JsonUtil.Obj(("mode", "click"));
                if (waitTimeout > 0)
                {
                    automation["waitForViewId"] = toViewId;
                    automation["timeout"] = waitTimeout;
                }

                string summary = sourceType == "login-flow"
                    ? binding.SourceView + "." + handler.Name + " fires EventArgsSendLoginReq; ProcedureLogin changes into preload/load-game flow; ProcedureLoadGame opens " + targetView + "."
                    : binding.SourceView + "." + handler.Name + " opens " + targetView + ".";

                patch.Transitions.Add(JsonUtil.Obj(
                    ("id", transitionId),
                    ("fromViewId", fromViewId),
                    ("toViewId", toViewId),
                    ("kind", "interaction"),
                    ("controlId", controlId),
                    ("trigger", JsonUtil.Obj(("type", "user-action"), ("action", "click"), ("controlId", controlId))),
                    ("effect", JsonUtil.Obj(("type", "open-view"), ("targetViewId", toViewId))),
                    ("automation", automation),
                    ("confidence", confidence),
                    ("source", JsonUtil.Obj(
                        ("type", sourceType),
                        ("summary", summary),
                        ("path", RelativeToAssets(assetsRoot, handler.Path)),
                        ("line", handler.Line)
                    ))
                ));
            }

            if (!patch.RouteIds.Contains(routeId) && !existingRoutes.Contains(routeId))
            {
                patch.RouteIds.Add(routeId);
                patch.Routes.Add(JsonUtil.Obj(
                    ("id", routeId),
                    ("fromViewId", fromViewId),
                    ("toViewId", toViewId),
                    ("steps", new JsonArray
                    {
                        JsonUtil.Obj(("transitionId", transitionId), ("controlId", controlId))
                    })
                ));
            }
        }

        private static void AddStartupLoginGate(
            SourceNavigationPatch patch,
            string assetsRoot,
            JsonObject existingMap,
            List<SourceEvidence> evidence,
            HashSet<string> existingTransitions,
            HashSet<string> existingRoutes)
        {
            if (!EvidenceViews(evidence, "open-ui-form").Contains("UIFormLogin"))
            {
                return;
            }

            string appStartViewId = ViewIdForName(existingMap, "Application Start");
            if (string.IsNullOrEmpty(appStartViewId))
            {
                appStartViewId = "view.app.start";
            }

            string loginViewId = ViewIdForName(existingMap, "UIFormLogin");
            if (string.IsNullOrEmpty(loginViewId))
            {
                return;
            }

            string transitionId = "transition.app.start.to.login";
            if (!existingTransitions.Contains(transitionId) && !patch.TransitionIds.Contains(transitionId))
            {
                patch.TransitionIds.Add(transitionId);
                patch.Transitions.Add(JsonUtil.Obj(
                    ("id", transitionId),
                    ("fromViewId", appStartViewId),
                    ("toViewId", loginViewId),
                    ("kind", "lifecycle"),
                    ("automation", JsonUtil.Obj(("mode", "wait"), ("waitForViewId", loginViewId), ("timeout", 30))),
                    ("confidence", 0.8),
                    ("source", JsonUtil.Obj(("type", "procedure-flow"), ("summary", "ProcedureLogin opens UIFormLogin during startup.")))
                ));
            }

            string operationViewId = ViewIdForName(existingMap, "UIFormOperation");
            string loginToOperationTransitionId = "transition.login.login.to." + TokenForViewId(operationViewId);
            string unsafeDirectTransitionId = "transition.app.start.to.operation";
            if (!string.IsNullOrEmpty(operationViewId)
                && existingTransitions.Contains(unsafeDirectTransitionId)
                && !patch.TransitionIds.Contains(unsafeDirectTransitionId))
            {
                patch.TransitionIds.Add(unsafeDirectTransitionId);
                patch.Transitions.Add(JsonUtil.Obj(
                    ("id", unsafeDirectTransitionId),
                    ("fromViewId", appStartViewId),
                    ("toViewId", operationViewId),
                    ("kind", "inferred"),
                    ("automation", JsonUtil.Obj(("mode", "manual"))),
                    ("confidence", 0.2),
                    ("source", JsonUtil.Obj(
                        ("type", "startup-gate-correction"),
                        ("summary", "Direct startup-to-operation waiting is unsafe because ProcedureLoadGame opens UIFormOperation only after login/preload/load-game gates. Use route.app.start.to.operation instead.")
                    ))
                ));
            }

            string routeId = "route.app.start.to.operation";
            if (!string.IsNullOrEmpty(operationViewId) && !patch.RouteIds.Contains(routeId))
            {
                patch.RouteIds.Add(routeId);
                patch.Routes.Add(JsonUtil.Obj(
                    ("id", routeId),
                    ("fromViewId", appStartViewId),
                    ("toViewId", operationViewId),
                    ("steps", new JsonArray
                    {
                        JsonUtil.Obj(("transitionId", transitionId)),
                        JsonUtil.Obj(("transitionId", loginToOperationTransitionId), ("controlId", "control.login.login"))
                    })
                ));
            }
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
            SourceEvidence script = evidence.FirstOrDefault(item => item.Kind == "ui-form-class" && item.View == view);
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

        private static SourceCodeNavigationModel BuildSourceCodeNavigationModel(string assetsRoot)
        {
            var model = new SourceCodeNavigationModel();
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (IsIgnoredCodePath(path))
                {
                    continue;
                }

                string sourceView = InferSourceViewFromPath(path);
                if (!NotBlank(sourceView))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    foreach (Match match in ButtonHandlerRegex.Matches(line))
                    {
                        model.ButtonBindings.Add(new ButtonBinding(sourceView, match.Groups["control"].Value, match.Groups["handler"].Value, path, i + 1));
                    }

                    Match methodMatch = MethodDeclarationRegex.Match(line);
                    if (!methodMatch.Success)
                    {
                        continue;
                    }

                    int endIndex;
                    List<string> body = ExtractMethodBody(lines, i, out endIndex);
                    if (body.Count == 0)
                    {
                        continue;
                    }

                    HandlerFlow flow = BuildHandlerFlow(methodMatch.Groups["name"].Value, sourceView, path, i + 1, body);
                    model.AddHandler(flow);
                }
            }

            return model;
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

        private static HandlerFlow BuildHandlerFlow(string name, string sourceView, string path, int line, List<string> body)
        {
            var flow = new HandlerFlow(name, sourceView, path, line);
            foreach (string rawLine in body)
            {
                string trimmed = rawLine.Trim();
                if (trimmed.Contains("EventArgsSendLoginReq"))
                {
                    flow.FiresLoginRequest = true;
                }

                if (!trimmed.Contains("OpenUIForm"))
                {
                    continue;
                }

                foreach (string view in ExtractUiForms(trimmed))
                {
                    if (IsConcreteUiFormTarget(view))
                    {
                        flow.OpenTargets.Add(view);
                    }
                }
            }

            return flow;
        }

        private static bool CanInferLoginToOperationFlow(string assetsRoot)
        {
            return FileContains(assetsRoot, "ProcedureLogin.cs", "ChangeState<ProcedurePreload>")
                && FileContains(assetsRoot, "ProcedureLoadGame.cs", "OpenUIFormAsync(UIFormId.UIFormOperation");
        }

        private static bool FileContains(string root, string fileName, string text)
        {
            foreach (string path in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
            {
                if (IsIgnoredCodePath(path))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static PrefabButtonInfo ResolvePrefabButtonInfo(string assetsRoot, string sourceView, string controlProperty)
        {
            string fallbackName = controlProperty;
            string prefabPath = Directory.EnumerateFiles(assetsRoot, sourceView + ".prefab", SearchOption.AllDirectories).FirstOrDefault();
            if (prefabPath == null)
            {
                return new PrefabButtonInfo(fallbackName, sourceView + "/" + fallbackName);
            }

            string text = File.ReadAllText(prefabPath);
            string fieldName = "m_" + controlProperty;
            string componentId = MatchValue(text, @"\b" + Regex.Escape(fieldName) + @":\s*\{fileID:\s*(-?\d+)\}");
            string gameObjectId = FindReferencedGameObjectId(text, componentId);
            string objectName = FindGameObjectName(text, gameObjectId) ?? fallbackName;
            return new PrefabButtonInfo(objectName, sourceView + "/" + objectName);
        }

        private static string FindReferencedGameObjectId(string prefabText, string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
            {
                return null;
            }

            Match block = Regex.Match(prefabText, @"--- !u!\d+ &" + Regex.Escape(componentId) + @"\s*(?<body>.*?)(?=\r?\n--- !u!|\z)", RegexOptions.Singleline);
            return block.Success ? MatchValue(block.Groups["body"].Value, @"m_GameObject:\s*\{fileID:\s*(-?\d+)\}") : null;
        }

        private static string FindGameObjectName(string prefabText, string gameObjectId)
        {
            if (string.IsNullOrEmpty(gameObjectId))
            {
                return null;
            }

            Match block = Regex.Match(prefabText, @"--- !u!1 &" + Regex.Escape(gameObjectId) + @"\s*(?<body>.*?)(?=\r?\n--- !u!|\z)", RegexOptions.Singleline);
            return block.Success ? MatchValue(block.Groups["body"].Value, @"m_Name:\s*(.+)")?.Trim() : null;
        }

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(text ?? "", pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ViewIdForName(JsonObject existingMap, string view)
        {
            if (string.IsNullOrWhiteSpace(view))
            {
                return null;
            }

            string normalized = NormalizeViewId(view);
            foreach (JsonObject item in existingMap?["views"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                string id = Text(item, "id");
                if (NormalizeViewId(id) == normalized
                    || NormalizeViewId(Text(item, "name")) == normalized
                    || NormalizeViewId(Text(item, "rootObjectPath")) == normalized)
                {
                    return id;
                }
            }

            if (view == "Application Start")
            {
                return "view.app.start";
            }

            return view.StartsWith("view.", StringComparison.Ordinal) ? view : ViewIdForName(view);
        }

        private static string TokenForControl(string controlProperty, string toViewId)
        {
            string token = controlProperty;
            if (token.EndsWith("ExButton", StringComparison.Ordinal))
            {
                token = token.Substring(0, token.Length - "ExButton".Length);
            }

            return NormalizeViewId(token);
        }

        private static string TokenForViewId(string viewId)
        {
            return NormalizeViewId(viewId);
        }

        private static bool IsConcreteUiFormTarget(string view)
        {
            return NotBlank(view)
                && view.StartsWith("UIForm", StringComparison.Ordinal)
                && view != "UIFormAsset";
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
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"^(UIForm[A-Za-z0-9_]+)");
            return match.Success ? match.Groups[1].Value : null;
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

        private sealed class SourceNavigationPatch
        {
            public JsonArray Controls { get; } = new JsonArray();
            public JsonArray Transitions { get; } = new JsonArray();
            public JsonArray Routes { get; } = new JsonArray();
            public HashSet<string> ControlIds { get; } = new HashSet<string>();
            public HashSet<string> TransitionIds { get; } = new HashSet<string>();
            public HashSet<string> RouteIds { get; } = new HashSet<string>();
        }

        private sealed class SourceCodeNavigationModel
        {
            private readonly Dictionary<string, Dictionary<string, HandlerFlow>> _handlers = new Dictionary<string, Dictionary<string, HandlerFlow>>();

            public List<ButtonBinding> ButtonBindings { get; } = new List<ButtonBinding>();

            public void AddHandler(HandlerFlow flow)
            {
                Dictionary<string, HandlerFlow> byName;
                if (!_handlers.TryGetValue(flow.SourceView, out byName))
                {
                    byName = new Dictionary<string, HandlerFlow>();
                    _handlers[flow.SourceView] = byName;
                }

                HandlerFlow existing;
                if (byName.TryGetValue(flow.Name, out existing))
                {
                    foreach (string target in flow.OpenTargets)
                    {
                        existing.OpenTargets.Add(target);
                    }

                    existing.FiresLoginRequest = existing.FiresLoginRequest || flow.FiresLoginRequest;
                    return;
                }

                byName[flow.Name] = flow;
            }

            public bool TryGetHandler(string sourceView, string handlerName, out HandlerFlow flow)
            {
                flow = null;
                Dictionary<string, HandlerFlow> byName;
                return _handlers.TryGetValue(sourceView, out byName) && byName.TryGetValue(handlerName, out flow);
            }
        }

        private sealed class ButtonBinding
        {
            public ButtonBinding(string sourceView, string controlProperty, string handler, string path, int line)
            {
                SourceView = sourceView;
                ControlProperty = controlProperty;
                Handler = handler;
                Path = path;
                Line = line;
            }

            public string SourceView { get; }
            public string ControlProperty { get; }
            public string Handler { get; }
            public string Path { get; }
            public int Line { get; }
        }

        private sealed class HandlerFlow
        {
            public HandlerFlow(string name, string sourceView, string path, int line)
            {
                Name = name;
                SourceView = sourceView;
                Path = path;
                Line = line;
            }

            public string Name { get; }
            public string SourceView { get; }
            public string Path { get; }
            public int Line { get; }
            public HashSet<string> OpenTargets { get; } = new HashSet<string>();
            public bool FiresLoginRequest { get; set; }
        }

        private sealed class PrefabButtonInfo
        {
            public PrefabButtonInfo(string name, string objectPath)
            {
                Name = name;
                ObjectPath = objectPath;
            }

            public string Name { get; }
            public string ObjectPath { get; }
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
