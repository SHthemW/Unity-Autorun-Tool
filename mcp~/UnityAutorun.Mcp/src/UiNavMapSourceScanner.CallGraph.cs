using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UnityAutorun.Mcp
{
    public static partial class UiNavMapSourceScanner
    {
        private const int MaxNavigationCallDepth = 12;
        private const int MaxVisitedMethodsPerBinding = 512;

        private static readonly Regex SourceTypeRegex = new Regex(
            @"^(?<type>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        private static readonly Regex MemberDeclarationRegex = new Regex(
            @"^\s*(?:public|private|protected|internal)\s+(?:(?:static|readonly|volatile|const|new)\s+)*(?<type>[A-Za-z_][A-Za-z0-9_.]*(?:<[^>]+>)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\{|;|=|=>)",
            RegexOptions.Compiled);

        private static readonly Regex TypedVariableRegex = new Regex(
            @"\b(?<type>[A-Z][A-Za-z0-9_.]*(?:<[^>{};()]+>)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled);

        private static readonly Regex AsVariableRegex = new Regex(
            @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;]*?\bas\s+(?<type>[A-Z][A-Za-z0-9_.]*)",
            RegexOptions.Compiled);

        private static readonly Regex GenericVariableRegex = new Regex(
            @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;]*?(?:GetComponent(?:InParent|InChildren)?|FindObjectOfType|FindFirstObjectByType)\s*<\s*(?<type>[A-Z][A-Za-z0-9_.]*)\s*>",
            RegexOptions.Compiled);

        private static readonly Regex MemberInvocationRegex = new Regex(
            @"(?<receiver>\b[A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*)\s*(?:\?|\!)?\.\s*(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^;\r\n()]+>)?\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex LocalInvocationRegex = new Regex(
            @"(?<![A-Za-z0-9_.])(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^;\r\n()]+>)?\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex IdentifierRegex = new Regex(
            @"\b[A-Za-z_][A-Za-z0-9_]*\b",
            RegexOptions.Compiled);

        private static readonly HashSet<string> NonInvocationKeywords = new HashSet<string>(
            new[]
            {
                "if",
                "for",
                "foreach",
                "while",
                "switch",
                "catch",
                "using",
                "lock",
                "nameof",
                "typeof",
                "sizeof",
                "default",
                "return"
            },
            StringComparer.Ordinal);

        private static SourceCodeNavigationModel BuildCallGraphNavigationModel(
            string assetsRoot,
            Dictionary<string, string> knownViews)
        {
            var model = new SourceCodeNavigationModel(knownViews);
            List<string> paths = Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsIgnoredCodePath(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sourceTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string sourceType = InferSourceTypeFromPath(path);
                if (!NotBlank(sourceType))
                {
                    continue;
                }

                sourceTypes[path] = sourceType;
                model.AddKnownType(sourceType);
                foreach (string line in File.ReadLines(path))
                {
                    Match memberMatch = MemberDeclarationRegex.Match(line);
                    if (!memberMatch.Success)
                    {
                        continue;
                    }

                    model.AddMemberType(
                        sourceType,
                        memberMatch.Groups["name"].Value,
                        NormalizeTypeName(memberMatch.Groups["type"].Value));
                }
            }

            foreach (KeyValuePair<string, string> sourceFile in sourceTypes)
            {
                string path = sourceFile.Key;
                string sourceType = sourceFile.Value;
                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in ButtonHandlerRegex.Matches(lines[i]))
                    {
                        model.AddButtonBinding(new ButtonBinding(
                            sourceType,
                            match.Groups["control"].Value,
                            match.Groups["handler"].Value,
                            path,
                            i + 1));
                    }
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    Match methodMatch = MethodDeclarationRegex.Match(lines[i]);
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

                    HandlerFlow flow = ParseHandlerFlow(
                        methodMatch.Groups["name"].Value,
                        sourceType,
                        path,
                        i + 1,
                        body,
                        knownViews);
                    model.AddHandler(flow);
                    i = Math.Max(i, endIndex);
                }
            }

            return model;
        }

        private static HandlerFlow ParseHandlerFlow(
            string name,
            string declaringType,
            string path,
            int line,
            List<string> body,
            Dictionary<string, string> knownViews)
        {
            var flow = new HandlerFlow(name, declaringType, path, line);

            for (int i = 0; i < body.Count; i++)
            {
                string trimmed = body[i].Trim();
                AddVariableTypes(flow, trimmed);

                foreach (string view in ExtractKnownViews(trimmed, knownViews))
                {
                    string invocation = FindInvocationForViewReference(trimmed, view, name);
                    if (NotBlank(invocation))
                    {
                        flow.AddViewReference(new ViewReference(
                            view,
                            invocation,
                            path,
                            line + i,
                            trimmed));
                    }
                }

                foreach (Match match in MemberInvocationRegex.Matches(trimmed))
                {
                    flow.AddInvocation(new MethodInvocation(
                        NormalizeReceiver(match.Groups["receiver"].Value),
                        match.Groups["method"].Value));
                }

                foreach (Match match in LocalInvocationRegex.Matches(trimmed))
                {
                    string methodName = match.Groups["method"].Value;
                    if (!NonInvocationKeywords.Contains(methodName))
                    {
                        flow.AddInvocation(new MethodInvocation(null, methodName));
                    }
                }
            }

            return flow;
        }

        private static IEnumerable<string> ExtractKnownViews(
            string line,
            Dictionary<string, string> knownViews)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in IdentifierRegex.Matches(line))
            {
                string canonical;
                if (knownViews.TryGetValue(match.Value, out canonical))
                {
                    result.Add(canonical);
                }
            }

            return result;
        }

        private static string FindInvocationForViewReference(
            string line,
            string view,
            string declaringMethod)
        {
            int viewIndex = line.IndexOf(view, StringComparison.OrdinalIgnoreCase);
            if (viewIndex < 0)
            {
                return null;
            }

            Match selectedMember = null;
            foreach (Match match in MemberInvocationRegex.Matches(line))
            {
                if (match.Index < viewIndex)
                {
                    selectedMember = match;
                }
            }

            if (selectedMember != null)
            {
                return NormalizeReceiver(selectedMember.Groups["receiver"].Value)
                    + "."
                    + selectedMember.Groups["method"].Value;
            }

            Match selectedLocal = null;
            foreach (Match match in LocalInvocationRegex.Matches(line))
            {
                string methodName = match.Groups["method"].Value;
                if (match.Index < viewIndex
                    && methodName != declaringMethod
                    && !NonInvocationKeywords.Contains(methodName))
                {
                    selectedLocal = match;
                }
            }

            return selectedLocal == null
                ? null
                : selectedLocal.Groups["method"].Value;
        }

        private static void AddVariableTypes(HandlerFlow flow, string line)
        {
            foreach (Match match in TypedVariableRegex.Matches(line))
            {
                flow.AddVariableType(
                    match.Groups["name"].Value,
                    NormalizeTypeName(match.Groups["type"].Value));
            }

            foreach (Match match in AsVariableRegex.Matches(line))
            {
                flow.AddVariableType(
                    match.Groups["name"].Value,
                    NormalizeTypeName(match.Groups["type"].Value));
            }

            foreach (Match match in GenericVariableRegex.Matches(line))
            {
                flow.AddVariableType(
                    match.Groups["name"].Value,
                    NormalizeTypeName(match.Groups["type"].Value));
            }
        }

        private static string InferSourceTypeFromPath(string path)
        {
            Match match = SourceTypeRegex.Match(Path.GetFileNameWithoutExtension(path));
            return match.Success ? match.Groups["type"].Value : null;
        }

        private static string NormalizeTypeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string type = value.Trim()
                .Replace("global::", "")
                .Replace("?", "")
                .Replace("[]", "");
            int genericIndex = type.IndexOf('<');
            if (genericIndex >= 0)
            {
                type = type.Substring(0, genericIndex);
            }

            int namespaceIndex = type.LastIndexOf('.');
            return namespaceIndex >= 0 ? type.Substring(namespaceIndex + 1) : type;
        }

        private static string NormalizeReceiver(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : Regex.Replace(value, @"\s+", "");
        }

        private sealed class SourceCodeNavigationModel
        {
            private readonly Dictionary<string, Dictionary<string, HandlerFlow>> _handlers =
                new Dictionary<string, Dictionary<string, HandlerFlow>>(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<string, Dictionary<string, string>> _memberTypes =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            private readonly HashSet<string> _knownTypes =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<string, string> _knownViews;
            private readonly HashSet<string> _buttonBindingKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public SourceCodeNavigationModel(Dictionary<string, string> knownViews)
            {
                _knownViews = knownViews
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public List<ButtonBinding> ButtonBindings { get; } = new List<ButtonBinding>();

            public void AddKnownType(string type)
            {
                if (NotBlank(type))
                {
                    _knownTypes.Add(type);
                }
            }

            public void AddMemberType(string declaringType, string memberName, string memberType)
            {
                if (!NotBlank(declaringType) || !NotBlank(memberName) || !NotBlank(memberType))
                {
                    return;
                }

                Dictionary<string, string> byName;
                if (!_memberTypes.TryGetValue(declaringType, out byName))
                {
                    byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _memberTypes[declaringType] = byName;
                }

                byName[memberName] = memberType;
            }

            public void AddButtonBinding(ButtonBinding binding)
            {
                string key = binding.OwnerType
                    + "|"
                    + binding.ControlProperty
                    + "|"
                    + binding.Handler
                    + "|"
                    + binding.Path
                    + "|"
                    + binding.Line;
                if (_buttonBindingKeys.Add(key))
                {
                    ButtonBindings.Add(binding);
                }
            }

            public void AddHandler(HandlerFlow flow)
            {
                Dictionary<string, HandlerFlow> byName;
                if (!_handlers.TryGetValue(flow.DeclaringType, out byName))
                {
                    byName = new Dictionary<string, HandlerFlow>(StringComparer.OrdinalIgnoreCase);
                    _handlers[flow.DeclaringType] = byName;
                }

                HandlerFlow existing;
                if (byName.TryGetValue(flow.Name, out existing))
                {
                    existing.Merge(flow);
                    return;
                }

                byName[flow.Name] = flow;
            }

            public List<NavigationCallCandidate> BuildNavigationCandidates()
            {
                var candidates = new Dictionary<string, NavigationCallCandidate>(StringComparer.Ordinal);
                foreach (ButtonBinding binding in ButtonBindings
                    .OrderBy(item => item.OwnerType)
                    .ThenBy(item => item.ControlProperty)
                    .ThenBy(item => item.Handler))
                {
                    ResolvedButtonFlow flow;
                    if (!TryResolveButtonFlow(binding, out flow))
                    {
                        continue;
                    }

                    foreach (ResolvedViewReference reference in flow.ViewReferences)
                    {
                        List<string> sourceViews = reference.MethodChain
                            .Select(item => CanonicalView(item.DeclaringType))
                            .Where(NotBlank)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var candidate = new NavigationCallCandidate(
                            binding,
                            reference,
                            sourceViews,
                            flow.AnalysisTruncated);
                        candidates[candidate.Id] = candidate;
                    }
                }

                return candidates.Values.ToList();
            }

            private bool TryResolveButtonFlow(ButtonBinding binding, out ResolvedButtonFlow resolved)
            {
                resolved = null;
                HandlerFlow root;
                if (!TryGetHandler(binding.OwnerType, binding.Handler, out root))
                {
                    return false;
                }

                var accumulator = new FlowAccumulator();
                Visit(
                    root,
                    0,
                    new List<HandlerFlow>(),
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    accumulator);

                if (accumulator.ViewReferences.Count == 0)
                {
                    return false;
                }

                resolved = new ResolvedButtonFlow(
                    root,
                    accumulator.ViewReferences.Values
                        .OrderBy(item => item.Reference.View)
                        .ThenBy(item => item.Reference.Path)
                        .ThenBy(item => item.Reference.Line)
                        .ToList(),
                    accumulator.WasTruncated);
                return true;
            }

            private void Visit(
                HandlerFlow flow,
                int depth,
                List<HandlerFlow> chain,
                Dictionary<string, int> bestDepths,
                FlowAccumulator accumulator)
            {
                if (depth > MaxNavigationCallDepth)
                {
                    accumulator.MarkTruncated();
                    return;
                }

                if (!accumulator.TryVisitMethod())
                {
                    return;
                }

                string key = flow.DeclaringType + "." + flow.Name;
                int bestDepth;
                if (bestDepths.TryGetValue(key, out bestDepth) && bestDepth <= depth)
                {
                    return;
                }

                bestDepths[key] = depth;
                chain.Add(flow);

                foreach (ViewReference reference in flow.ViewReferences)
                {
                    accumulator.AddViewReference(new ResolvedViewReference(
                        reference,
                        new List<HandlerFlow>(chain),
                        depth));
                }

                foreach (MethodInvocation invocation in flow.Invocations)
                {
                    HandlerFlow called;
                    if (TryResolveInvocation(flow, invocation, out called))
                    {
                        Visit(called, depth + 1, chain, bestDepths, accumulator);
                    }
                }

                chain.RemoveAt(chain.Count - 1);
            }

            private bool TryResolveInvocation(
                HandlerFlow caller,
                MethodInvocation invocation,
                out HandlerFlow called)
            {
                called = null;
                if (!NotBlank(invocation.Receiver))
                {
                    return TryGetHandler(caller.DeclaringType, invocation.MethodName, out called);
                }

                string receiverType = ResolveReceiverType(caller, invocation.Receiver);
                return NotBlank(receiverType)
                    && TryGetHandler(receiverType, invocation.MethodName, out called);
            }

            private string ResolveReceiverType(HandlerFlow caller, string receiver)
            {
                string[] segments = receiver.Split('.');
                if (segments.Length == 0)
                {
                    return null;
                }

                string currentType;
                string first = segments[0];
                if (first == "this" || first == "base")
                {
                    currentType = caller.DeclaringType;
                }
                else if (caller.VariableTypes.TryGetValue(first, out currentType))
                {
                }
                else if (_knownTypes.Contains(first))
                {
                    currentType = first;
                }
                else if (!TryGetMemberType(caller.DeclaringType, first, out currentType))
                {
                    return null;
                }

                for (int i = 1; i < segments.Length; i++)
                {
                    string nextType;
                    if (!TryGetMemberType(currentType, segments[i], out nextType))
                    {
                        return null;
                    }

                    currentType = nextType;
                }

                return currentType;
            }

            private string CanonicalView(string type)
            {
                string result;
                return NotBlank(type) && _knownViews.TryGetValue(type, out result)
                    ? result
                    : null;
            }

            private bool TryGetMemberType(string declaringType, string memberName, out string memberType)
            {
                memberType = null;
                Dictionary<string, string> byName;
                return _memberTypes.TryGetValue(declaringType, out byName)
                    && byName.TryGetValue(memberName, out memberType);
            }

            private bool TryGetHandler(string declaringType, string handlerName, out HandlerFlow flow)
            {
                flow = null;
                Dictionary<string, HandlerFlow> byName;
                return _handlers.TryGetValue(declaringType, out byName)
                    && byName.TryGetValue(handlerName, out flow);
            }
        }

        private sealed class FlowAccumulator
        {
            private int _visitedMethods;

            public Dictionary<string, ResolvedViewReference> ViewReferences { get; } =
                new Dictionary<string, ResolvedViewReference>(StringComparer.OrdinalIgnoreCase);

            public bool WasTruncated { get; private set; }

            public bool TryVisitMethod()
            {
                if (_visitedMethods >= MaxVisitedMethodsPerBinding)
                {
                    WasTruncated = true;
                    return false;
                }

                _visitedMethods++;
                return true;
            }

            public void MarkTruncated()
            {
                WasTruncated = true;
            }

            public void AddViewReference(ResolvedViewReference reference)
            {
                string key = reference.Reference.View
                    + "|"
                    + reference.Reference.Path
                    + "|"
                    + reference.Reference.Line
                    + "|"
                    + reference.Reference.Invocation;
                ResolvedViewReference existing;
                if (!ViewReferences.TryGetValue(key, out existing)
                    || reference.Depth < existing.Depth)
                {
                    ViewReferences[key] = reference;
                }
            }
        }

        private sealed class ButtonBinding
        {
            public ButtonBinding(string ownerType, string controlProperty, string handler, string path, int line)
            {
                OwnerType = ownerType;
                ControlProperty = controlProperty;
                Handler = handler;
                Path = path;
                Line = line;
            }

            public string OwnerType { get; }
            public string ControlProperty { get; }
            public string Handler { get; }
            public string Path { get; }
            public int Line { get; }
        }

        private sealed class HandlerFlow
        {
            private readonly HashSet<string> _invocationKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private readonly HashSet<string> _viewReferenceKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public HandlerFlow(string name, string declaringType, string path, int line)
            {
                Name = name;
                DeclaringType = declaringType;
                Path = path;
                Line = line;
            }

            public string Name { get; }
            public string DeclaringType { get; }
            public string Path { get; }
            public int Line { get; }
            public Dictionary<string, string> VariableTypes { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public List<MethodInvocation> Invocations { get; } =
                new List<MethodInvocation>();
            public List<ViewReference> ViewReferences { get; } =
                new List<ViewReference>();

            public void AddVariableType(string variableName, string variableType)
            {
                if (NotBlank(variableName) && NotBlank(variableType))
                {
                    VariableTypes[variableName] = variableType;
                }
            }

            public void AddInvocation(MethodInvocation invocation)
            {
                string key = (invocation.Receiver ?? "") + "." + invocation.MethodName;
                if (_invocationKeys.Add(key))
                {
                    Invocations.Add(invocation);
                }
            }

            public void AddViewReference(ViewReference reference)
            {
                string key = reference.View
                    + "|"
                    + reference.Path
                    + "|"
                    + reference.Line
                    + "|"
                    + reference.Invocation;
                if (_viewReferenceKeys.Add(key))
                {
                    ViewReferences.Add(reference);
                }
            }

            public void Merge(HandlerFlow other)
            {
                foreach (KeyValuePair<string, string> variable in other.VariableTypes)
                {
                    VariableTypes[variable.Key] = variable.Value;
                }

                foreach (MethodInvocation invocation in other.Invocations)
                {
                    AddInvocation(invocation);
                }

                foreach (ViewReference reference in other.ViewReferences)
                {
                    AddViewReference(reference);
                }
            }
        }

        private sealed class MethodInvocation
        {
            public MethodInvocation(string receiver, string methodName)
            {
                Receiver = receiver;
                MethodName = methodName;
            }

            public string Receiver { get; }
            public string MethodName { get; }
        }

        private sealed class ViewReference
        {
            public ViewReference(string view, string invocation, string path, int line, string text)
            {
                View = view;
                Invocation = invocation;
                Path = path;
                Line = line;
                Text = text;
            }

            public string View { get; }
            public string Invocation { get; }
            public string Path { get; }
            public int Line { get; }
            public string Text { get; }
        }

        private sealed class ResolvedButtonFlow
        {
            public ResolvedButtonFlow(
                HandlerFlow rootHandler,
                List<ResolvedViewReference> viewReferences,
                bool analysisTruncated)
            {
                RootHandler = rootHandler;
                ViewReferences = viewReferences;
                AnalysisTruncated = analysisTruncated;
            }

            public HandlerFlow RootHandler { get; }
            public List<ResolvedViewReference> ViewReferences { get; }
            public bool AnalysisTruncated { get; }
        }

        private sealed class ResolvedViewReference
        {
            public ResolvedViewReference(
                ViewReference reference,
                List<HandlerFlow> methodChain,
                int depth)
            {
                Reference = reference;
                MethodChain = methodChain;
                Depth = depth;
                CrossesTypeBoundary = methodChain
                    .Select(item => item.DeclaringType)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Skip(1)
                    .Any();
            }

            public ViewReference Reference { get; }
            public List<HandlerFlow> MethodChain { get; }
            public int Depth { get; }
            public bool CrossesTypeBoundary { get; }
        }

        private sealed class NavigationCallCandidate
        {
            public NavigationCallCandidate(
                ButtonBinding binding,
                ResolvedViewReference reference,
                List<string> sourceViewCandidates,
                bool analysisTruncated)
            {
                Binding = binding;
                Reference = reference;
                SourceViewCandidates = sourceViewCandidates;
                AnalysisTruncated = analysisTruncated;
                string key = binding.Path
                    + "|"
                    + binding.Line
                    + "|"
                    + binding.ControlProperty
                    + "|"
                    + reference.Reference.Path
                    + "|"
                    + reference.Reference.Line
                    + "|"
                    + reference.Reference.View;
                Id = "candidate.navigation." + StableHash(key);
                string evidenceKey = key
                    + "|"
                    + binding.OwnerType
                    + "|"
                    + binding.Handler
                    + "|"
                    + reference.Reference.Invocation
                    + "|"
                    + reference.Reference.Text
                    + "|"
                    + string.Join(
                        ">",
                        reference.MethodChain.Select(item =>
                            item.DeclaringType
                            + "."
                            + item.Name
                            + "@"
                            + item.Path
                            + ":"
                            + item.Line));
                CandidateVersion = "candidate-evidence."
                    + UiNavMapMetadata.CandidateProtocolVersion
                    + "."
                    + StableHash(evidenceKey);
            }

            public string Id { get; }
            public string CandidateVersion { get; }
            public ButtonBinding Binding { get; }
            public ResolvedViewReference Reference { get; }
            public List<string> SourceViewCandidates { get; }
            public bool AnalysisTruncated { get; }

            public string SortKey
            {
                get
                {
                    return string.Join(",", SourceViewCandidates)
                        + "|"
                        + Reference.Reference.View
                        + "|"
                        + Binding.OwnerType
                        + "|"
                        + Binding.ControlProperty;
                }
            }

            public bool Matches(string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return true;
                }

                return Contains(Binding.OwnerType, query)
                    || Contains(Binding.ControlProperty, query)
                    || Contains(Binding.Handler, query)
                    || Contains(Binding.Path, query)
                    || Contains(Reference.Reference.View, query)
                    || Contains(Reference.Reference.Invocation, query)
                    || Contains(Reference.Reference.Path, query)
                    || SourceViewCandidates.Any(item => Contains(item, query))
                    || Reference.MethodChain.Any(item =>
                        Contains(item.DeclaringType, query)
                        || Contains(item.Name, query)
                        || Contains(item.Path, query));
            }

            public JsonObject ToJson(string assetsRoot)
            {
                var sourceViews = new JsonArray();
                foreach (string view in SourceViewCandidates)
                {
                    sourceViews.Add(JsonUtil.Obj(
                        ("view", view),
                        ("basis", view == Binding.OwnerType
                            ? "button-binding-owner"
                            : "declaring-type-in-call-chain")
                    ));
                }

                var methodChain = new JsonArray();
                foreach (HandlerFlow method in Reference.MethodChain)
                {
                    methodChain.Add(JsonUtil.Obj(
                        ("declaringType", method.DeclaringType),
                        ("method", method.Name),
                        ("path", RelativeToAssets(assetsRoot, method.Path)),
                        ("line", method.Line)
                    ));
                }

                string chainSummary = string.Join(
                    " -> ",
                    Reference.MethodChain.Select(item => item.DeclaringType + "." + item.Name));

                return JsonUtil.Obj(
                    ("id", Id),
                    ("candidateVersion", CandidateVersion),
                    ("kind", "button-call-chain"),
                    ("decisionStatus", "requires-ai-review"),
                    ("buttonBinding", JsonUtil.Obj(
                        ("ownerType", Binding.OwnerType),
                        ("controlProperty", Binding.ControlProperty),
                        ("handler", Binding.Handler),
                        ("source", JsonUtil.Obj(
                            ("path", RelativeToAssets(assetsRoot, Binding.Path)),
                            ("line", Binding.Line)
                        ))
                    )),
                    ("sourceViewCandidates", sourceViews),
                    ("sourceViewResolution", SourceViewCandidates.Count == 1
                        ? "single-candidate"
                        : SourceViewCandidates.Count == 0
                            ? "not-found"
                            : "ambiguous"),
                    ("referencedView", Reference.Reference.View),
                    ("referenceRole", "undetermined"),
                    ("reference", JsonUtil.Obj(
                        ("invocation", Reference.Reference.Invocation),
                        ("path", RelativeToAssets(assetsRoot, Reference.Reference.Path)),
                        ("line", Reference.Reference.Line),
                        ("text", Reference.Reference.Text)
                    )),
                    ("callDepth", Reference.Depth),
                    ("crossesTypeBoundary", Reference.CrossesTypeBoundary),
                    ("analysisTruncated", AnalysisTruncated),
                    ("methodChain", methodChain),
                    ("summary", Binding.OwnerType
                        + "."
                        + Binding.Handler
                        + " reaches a reference to "
                        + Reference.Reference.View
                        + " through "
                        + chainSummary
                        + "."),
                    ("requiredDecision", JsonUtil.Obj(
                        ("externalAi", true),
                        ("fields", new JsonArray
                        {
                            "fromViewId",
                            "referencedViewRole",
                            "toViewId",
                            "transitionKind",
                            "control",
                            "automation",
                            "confidence",
                            "candidateDecision.candidateVersion"
                        })
                    ))
                );
            }

            private static bool Contains(string value, string query)
            {
                return !string.IsNullOrEmpty(value)
                    && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
