using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UnityAutorun.Mcp
{
    public static partial class UiNavMapSourceScanner
    {
        private static readonly HashSet<string> NonTransitionEvidenceKinds =
            new HashSet<string>(
                new[]
                {
                    "source-contradiction",
                    "asset-contradiction",
                    "runtime-contradiction",
                    "human-confirmation"
                },
                StringComparer.Ordinal);

        private static JsonObject BuildCandidateDecisionHint(
            NavigationCallCandidate candidate,
            JsonObject map)
        {
            string referenceRole = InferReferenceRole(candidate.Reference.Reference.Invocation);
            string referenceUsage = InferReferenceUsage(
                candidate.Reference.Reference.View,
                candidate.Reference.Reference.Text);
            List<string> sourceViewIds = candidate.SourceViewCandidates.Count == 1
                ? MapViewIds(map, candidate.SourceViewCandidates[0])
                : new List<string>();
            List<string> targetViewIds = MapViewIds(
                map,
                candidate.Reference.Reference.View);
            bool strongTopology = HasStrongTopologyEvidence(
                candidate,
                sourceViewIds,
                targetViewIds,
                referenceRole,
                referenceUsage);

            string recommendation;
            if (referenceUsage == "qualifier" || referenceRole == "query-view")
            {
                recommendation = "ignored";
            }
            else if (strongTopology)
            {
                recommendation = "transition";
            }
            else
            {
                recommendation = "review";
            }

            var basis = new JsonArray();
            if (candidate.SourceViewCandidates.Count == 1)
            {
                basis.Add("single-source-view");
            }

            if (targetViewIds.Count > 0)
            {
                basis.Add("known-target-view");
            }

            if (referenceUsage == "value")
            {
                basis.Add("explicit-target-token");
            }

            if (referenceRole == "open-view")
            {
                basis.Add("navigation-like-invocation");
            }

            if (!candidate.AnalysisTruncated)
            {
                basis.Add("complete-call-graph-slice");
            }

            if (candidate.Reference.Depth <= 1)
            {
                basis.Add("direct-or-one-hop-call");
            }

            string controlResolution = candidate.Binding.SerializedControls.Any(
                item => item.Status == "resolved")
                ? "resolved"
                : candidate.Binding.SerializedControls.Any(
                    item => item.Status == "null-reference")
                    ? "null-reference"
                    : "not-resolved";
            bool nestedReusableControl =
                IsNestedReusableControl(candidate);

            return JsonUtil.Obj(
                ("referenceRoleHint", referenceRole),
                ("referenceUsage", referenceUsage),
                ("topologyStrength", strongTopology ? "strong" : "requires-review"),
                ("recommendedOutcome", recommendation),
                ("nonTransitionRequiresContradictoryEvidence", strongTopology),
                ("controlResolution", controlResolution),
                ("controlMultiplicityHint", nestedReusableControl
                    ? "potentially-repeated-nested-prefab"
                    : "view-owned-control"),
                ("recommendedMatchPolicy", nestedReusableControl
                    ? "first-interactable"
                    : "unique"),
                ("matchPolicyRule", nestedReusableControl
                    ? "When the serialized nested control shares this handler and reaches the same immediate target, use automation.mode=click with matchPolicy=first-interactable and cite that source/prefab evidence. AutoRun owns repeated-instance enumeration and recovery when later route eligibility varies. Use unique only with evidence for one complete-selector match, and use manual only for a concrete unsupported limitation of the immediate action."
                    : "Use unique unless runtime evidence proves multiple equivalent matches."),
                ("runtimeRepeatedControlPolicy", nestedReusableControl
                    ? "Downstream item eligibility is resolved dynamically by enumerating visible matches, scrolling, switching generic branches, dismissing an ineligible target, and retrying. Static generation does not need to identify the eligible item or a project-specific recovery path."
                    : "not-applicable"),
                ("mappedEndpoints", JsonUtil.Obj(
                    ("fromViewIds", StringJsonArray(sourceViewIds)),
                    ("toViewIds", StringJsonArray(targetViewIds))
                )),
                ("basis", basis),
                ("topologyRule", "Reachability and AutoRun executability are separate. Missing exact click metadata, async work, or branch preconditions do not by themselves invalidate a proven navigation edge.")
            );
        }

        private static CandidateDecisionReview ReviewCandidateDecision(
            NavigationCallCandidate candidate,
            JsonObject decision,
            JsonObject map)
        {
            if (decision == null)
            {
                return CandidateDecisionReview.Pending("unreviewed", null);
            }

            string recordedVersion = NodeText(decision, "candidateVersion");
            if (!string.Equals(
                candidate.CandidateVersion,
                recordedVersion,
                StringComparison.Ordinal))
            {
                return CandidateDecisionReview.Pending(
                    "outdated-decision",
                    JsonUtil.Obj(
                        ("code", "candidate-evidence-version-changed"),
                        ("previousCandidateVersion", recordedVersion),
                        ("candidateVersion", candidate.CandidateVersion),
                        ("requiredAction", "Review the current evidence and replace this candidate decision.")
                    ));
            }

            string role = InferReferenceRole(candidate.Reference.Reference.Invocation);
            string usage = InferReferenceUsage(
                candidate.Reference.Reference.View,
                candidate.Reference.Reference.Text);
            List<string> sourceViewIds = candidate.SourceViewCandidates.Count == 1
                ? MapViewIds(map, candidate.SourceViewCandidates[0])
                : new List<string>();
            List<string> targetViewIds = MapViewIds(
                map,
                candidate.Reference.Reference.View);
            bool strongTopology = HasStrongTopologyEvidence(
                candidate,
                sourceViewIds,
                targetViewIds,
                role,
                usage);
            string outcome = NodeText(decision, "outcome");

            if (outcome == "transition")
            {
                string targetId = NodeText(decision, "targetId");
                JsonObject transition = FindMapItem(map, "transitions", targetId);
                if (transition == null)
                {
                    return CandidateDecisionReview.Pending(
                        "semantic-review-required",
                        JsonUtil.Obj(
                            ("code", "candidate-transition-target-missing"),
                            ("targetId", targetId),
                            ("requiredAction", "Create the referenced transition or point the decision at an existing transition.")
                        ));
                }

                string fromViewId = NodeText(transition, "fromViewId");
                string toViewId = NodeText(transition, "toViewId");
                if (sourceViewIds.Count > 0
                    && targetViewIds.Count > 0
                    && (!sourceViewIds.Contains(fromViewId, StringComparer.Ordinal)
                        || !targetViewIds.Contains(toViewId, StringComparer.Ordinal)))
                {
                    return CandidateDecisionReview.Pending(
                        "semantic-review-required",
                        JsonUtil.Obj(
                            ("code", "candidate-transition-endpoint-mismatch"),
                            ("targetId", targetId),
                            ("actualFromViewId", fromViewId),
                            ("actualToViewId", toViewId),
                            ("expectedFromViewIds", StringJsonArray(sourceViewIds)),
                            ("expectedToViewIds", StringJsonArray(targetViewIds)),
                            ("requiredAction", "Reference a transition whose endpoints match the resolved candidate source and target views.")
                        ));
                }

                JsonObject controlIssue = ValidateTransitionControlEvidence(
                    candidate,
                    transition,
                    sourceViewIds,
                    map);
                if (controlIssue != null)
                {
                    return CandidateDecisionReview.Pending(
                        "semantic-review-required",
                        controlIssue);
                }

                return CandidateDecisionReview.Complete();
            }

            if (strongTopology && !HasValidNonTransitionEvidence(decision))
            {
                return CandidateDecisionReview.Pending(
                    "semantic-review-required",
                    JsonUtil.Obj(
                        ("code", "strong-topology-needs-transition-or-contradiction"),
                        ("currentOutcome", outcome),
                        ("expectedFromViewIds", StringJsonArray(sourceViewIds)),
                        ("expectedToViewIds", StringJsonArray(targetViewIds)),
                        ("referenceRoleHint", role),
                        ("requiredAction", "Create a matching transition, or attach nonTransitionEvidence with a concrete contradictory source, asset, runtime, or user-confirmed observation."),
                        ("notContradictory", new JsonArray
                        {
                            "exact AutoRun button metadata is not resolved",
                            "the handler performs async work",
                            "the edge has branch or data preconditions",
                            "runtime confirmation has not yet been collected"
                        }),
                        ("allowedEvidenceKinds", StringJsonArray(
                            NonTransitionEvidenceKinds.OrderBy(item => item)))
                    ));
            }

            return CandidateDecisionReview.Complete();
        }

        private static JsonObject ValidateTransitionControlEvidence(
            NavigationCallCandidate candidate,
            JsonObject transition,
            List<string> sourceViewIds,
            JsonObject map)
        {
            List<SerializedControlEvidence> resolvedControls = candidate.Binding
                .SerializedControls
                .Where(item =>
                    item.Status == "resolved"
                    && !string.IsNullOrWhiteSpace(item.ObjectName))
                .ToList();
            bool nestedReusableControl =
                IsNestedReusableControl(candidate);
            if (resolvedControls.Count == 0
                && !nestedReusableControl)
            {
                return null;
            }

            string transitionId = NodeText(transition, "id");
            string controlId = NodeText(transition, "controlId");
            if (string.IsNullOrWhiteSpace(controlId))
            {
                return JsonUtil.Obj(
                    ("code", nestedReusableControl
                        ? "candidate-nested-control-missing"
                        : "candidate-transition-control-missing"),
                    ("targetId", transitionId),
                    ("expectedObjectNames", StringJsonArray(
                        resolvedControls.Select(item => item.ObjectName).Distinct())),
                    ("requiredAction", nestedReusableControl
                        ? "Reference an explicit control from transition.controlId before assigning click automation to this potentially repeated nested-prefab handler."
                        : "Create a control from buttonBinding.serializedControls and reference it from transition.controlId.")
                );
            }

            JsonObject control = FindMapItem(map, "controls", controlId);
            if (control == null)
            {
                return JsonUtil.Obj(
                    ("code", "candidate-transition-control-target-missing"),
                    ("targetId", transitionId),
                    ("controlId", controlId),
                    ("requiredAction", "Create the referenced control from buttonBinding.serializedControls.")
                );
            }

            string controlViewId = NodeText(control, "viewId");
            if (sourceViewIds.Count > 0
                && !sourceViewIds.Contains(controlViewId, StringComparer.Ordinal))
            {
                return JsonUtil.Obj(
                    ("code", "candidate-transition-control-view-mismatch"),
                    ("targetId", transitionId),
                    ("controlId", controlId),
                    ("actualViewId", controlViewId),
                    ("expectedViewIds", StringJsonArray(sourceViewIds)),
                    ("requiredAction", "Attach the control to the resolved source view.")
                );
            }

            if (resolvedControls.Count > 0)
            {
                var authoredNames = new HashSet<string>(
                    StringComparer.Ordinal);
                AddValue(authoredNames, NodeText(control, "name"));
                AddValue(
                    authoredNames,
                    ObjectPathLeaf(NodeText(control, "objectPath")));
                JsonObject autoRun = control["autoRun"] as JsonObject;
                AddValue(authoredNames, NodeText(autoRun, "buttonName"));
                List<string> expectedNames = resolvedControls
                    .Select(item => item.ObjectName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList();
                if (!expectedNames.Any(authoredNames.Contains))
                {
                    return JsonUtil.Obj(
                        ("code", "candidate-transition-control-name-mismatch"),
                        ("targetId", transitionId),
                        ("controlId", controlId),
                        ("actualNames", StringJsonArray(authoredNames.OrderBy(item => item))),
                        ("expectedObjectNames", StringJsonArray(expectedNames)),
                        ("requiredAction", "Use the resolved serialized control objectName or objectPath leaf for control and AutoRun metadata.")
                    );
                }
            }

            JsonObject multiplicityIssue =
                ValidateNestedReusableControlAutomation(
                    candidate,
                    transition,
                    control);
            if (multiplicityIssue != null)
            {
                return multiplicityIssue;
            }

            return null;
        }

        private static JsonObject ValidateNestedReusableControlAutomation(
            NavigationCallCandidate candidate,
            JsonObject transition,
            JsonObject control)
        {
            if (!IsNestedReusableControl(candidate))
            {
                return null;
            }

            JsonObject automation = transition["automation"] as JsonObject;
            JsonObject transitionAutoRun =
                automation?["autoRun"] as JsonObject;
            JsonObject controlAutoRun = control["autoRun"] as JsonObject;
            // Match runtime resolution: a transition-level autoRun object
            // replaces the control-level object instead of merging with it.
            JsonObject effectiveAutoRun =
                transitionAutoRun ?? controlAutoRun;
            string mode = NodeText(automation, "mode");
            if (string.Equals(
                mode,
                "manual",
                StringComparison.OrdinalIgnoreCase))
            {
                string manualReason = NodeText(
                    automation,
                    "manualReason");
                if (!string.IsNullOrWhiteSpace(manualReason))
                {
                    return null;
                }

                return JsonUtil.Obj(
                    ("code", "candidate-nested-control-manual-reason-required"),
                    ("targetId", NodeText(transition, "id")),
                    ("controlId", NodeText(control, "id")),
                    ("requiredAction", "Use automation.mode=click with matchPolicy=first-interactable when the shared nested handler reaches the same immediate target. AutoRun handles later item eligibility dynamically. Keep mode=manual only when the immediate action itself cannot be automated, and record that concrete limitation in automation.manualReason.")
                );
            }

            bool clickAutomation = string.Equals(
                    mode,
                    "click",
                    StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrWhiteSpace(mode)
                    && effectiveAutoRun != null);
            if (!clickAutomation)
            {
                return null;
            }

            string matchPolicy = NodeText(
                effectiveAutoRun,
                "matchPolicy");

            string transitionId = NodeText(transition, "id");
            string controlId = NodeText(control, "id");
            if (string.IsNullOrWhiteSpace(matchPolicy))
            {
                return JsonUtil.Obj(
                    ("code", "candidate-nested-control-match-policy-required"),
                    ("targetId", transitionId),
                    ("controlId", controlId),
                    ("requiredAction", "Set matchPolicy explicitly for this potentially repeated nested-prefab control. Use first-interactable when source or prefab evidence shows the shared handler reaches the same immediate target; AutoRun handles later item eligibility dynamically. Use unique only with evidence for one complete-selector match.")
                );
            }

            if (!string.Equals(
                    matchPolicy,
                    "unique",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    matchPolicy,
                    "first-interactable",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JsonUtil.Obj(
                    ("code", "candidate-nested-control-match-policy-invalid"),
                    ("targetId", transitionId),
                    ("controlId", controlId),
                    ("actualMatchPolicy", matchPolicy),
                    ("requiredAction", "Use matchPolicy=unique or first-interactable exactly. Use automation.mode=manual only for a concrete unsupported immediate action and record automation.manualReason.")
                );
            }

            string evidence = NodeText(
                effectiveAutoRun,
                "matchPolicyEvidence");

            if (!string.IsNullOrWhiteSpace(evidence))
            {
                return null;
            }

            return JsonUtil.Obj(
                ("code", "candidate-repeated-match-policy-evidence-required"),
                ("targetId", transitionId),
                ("controlId", controlId),
                ("actualMatchPolicy", matchPolicy),
                ("requiredAction", string.Equals(
                        matchPolicy,
                        "first-interactable",
                        StringComparison.OrdinalIgnoreCase)
                    ? "Add non-empty matchPolicyEvidence citing source, prefab, or runtime evidence that matching instances share the handler and reach the same immediate target. Do not require proof of which item satisfies later route steps or a project-specific dismiss path; AutoRun owns that runtime recovery."
                    : "Add non-empty matchPolicyEvidence proving that the complete runtime selector has exactly one match, or use first-interactable with shared-handler immediate-edge evidence.")
            );
        }

        private static bool IsNestedReusableControl(
            NavigationCallCandidate candidate)
        {
            return candidate.SourceViewCandidates.Count == 1
                && !string.Equals(
                    candidate.Binding.OwnerType,
                    candidate.SourceViewCandidates[0],
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasStrongTopologyEvidence(
            NavigationCallCandidate candidate,
            List<string> sourceViewIds,
            List<string> targetViewIds,
            string referenceRole,
            string referenceUsage)
        {
            return candidate.SourceViewCandidates.Count == 1
                && sourceViewIds.Count > 0
                && targetViewIds.Count > 0
                && !string.Equals(
                    candidate.SourceViewCandidates[0],
                    candidate.Reference.Reference.View,
                    StringComparison.OrdinalIgnoreCase)
                && referenceRole == "open-view"
                && referenceUsage == "value"
                && candidate.Reference.Depth <= 1
                && !candidate.AnalysisTruncated
                && !candidate.Binding.SerializedControls.Any(
                    item => item.Status == "null-reference");
        }

        private static bool HasValidNonTransitionEvidence(JsonObject decision)
        {
            JsonObject evidence = decision?["nonTransitionEvidence"] as JsonObject;
            if (evidence == null)
            {
                return false;
            }

            string kind = NodeText(evidence, "kind");
            string summary = NodeText(evidence, "summary");
            return NonTransitionEvidenceKinds.Contains(kind)
                && !string.IsNullOrWhiteSpace(summary);
        }

        private static string InferReferenceRole(string invocation)
        {
            string method = string.IsNullOrWhiteSpace(invocation)
                ? ""
                : invocation.Split('.').Last();
            string normalized = method.ToLowerInvariant();

            if (normalized.Contains("open")
                || normalized.Contains("show")
                || normalized.Contains("present")
                || normalized.Contains("push")
                || normalized.Contains("navigate")
                || normalized.Contains("goto")
                || normalized.StartsWith("display", StringComparison.Ordinal))
            {
                return "open-view";
            }

            if (normalized.Contains("close")
                || normalized.Contains("hide")
                || normalized.Contains("dismiss")
                || normalized.StartsWith("pop", StringComparison.Ordinal))
            {
                return "close-view";
            }

            if (normalized.StartsWith("get", StringComparison.Ordinal)
                || normalized.StartsWith("find", StringComparison.Ordinal)
                || normalized.StartsWith("has", StringComparison.Ordinal)
                || normalized.StartsWith("is", StringComparison.Ordinal)
                || normalized.StartsWith("tryget", StringComparison.Ordinal)
                || normalized.StartsWith("contains", StringComparison.Ordinal))
            {
                return "query-view";
            }

            return "undetermined";
        }

        private static string InferReferenceUsage(string view, string text)
        {
            if (string.IsNullOrWhiteSpace(view) || string.IsNullOrWhiteSpace(text))
            {
                return "unknown";
            }

            return Regex.IsMatch(
                text,
                @"\b" + Regex.Escape(view) + @"\s*\.",
                RegexOptions.IgnoreCase)
                ? "qualifier"
                : "value";
        }

        private static List<string> MapViewIds(JsonObject map, string viewName)
        {
            if (map == null || string.IsNullOrWhiteSpace(viewName))
            {
                return new List<string>();
            }

            JsonArray views = map["views"] as JsonArray;
            if (views == null)
            {
                return new List<string>();
            }

            return views
                .OfType<JsonObject>()
                .Where(view =>
                    string.Equals(
                        NodeText(view, "name"),
                        viewName,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        LastPathSegment(NodeText(view, "rootObjectPath")),
                        viewName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(view => NodeText(view, "id"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static JsonObject FindMapItem(
            JsonObject map,
            string section,
            string id)
        {
            if (map == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            JsonArray items = map[section] as JsonArray;
            return items?
                .OfType<JsonObject>()
                .FirstOrDefault(item =>
                    string.Equals(
                        NodeText(item, "id"),
                        id,
                        StringComparison.Ordinal));
        }

        private static JsonArray StringJsonArray(IEnumerable<string> values)
        {
            var result = new JsonArray();
            if (values == null)
            {
                return result;
            }

            foreach (string value in values)
            {
                result.Add(value);
            }

            return result;
        }

        private static string ObjectPathLeaf(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return null;
            }

            string normalized = objectPath.Replace('\\', '/').Trim('/');
            int separator = normalized.LastIndexOf('/');
            return separator >= 0
                ? normalized.Substring(separator + 1)
                : normalized;
        }

        private static void AddValue(HashSet<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        private static string NodeText(JsonObject obj, string key)
        {
            JsonNode node = obj?[key];
            if (node == null)
            {
                return null;
            }

            try
            {
                return node.GetValue<string>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private sealed class CandidateDecisionReview
        {
            private CandidateDecisionReview(
                bool isComplete,
                string status,
                JsonObject issue)
            {
                IsComplete = isComplete;
                Status = status;
                Issue = issue;
            }

            public bool IsComplete { get; }
            public string Status { get; }
            public JsonObject Issue { get; }

            public static CandidateDecisionReview Complete()
            {
                return new CandidateDecisionReview(true, "reviewed", null);
            }

            public static CandidateDecisionReview Pending(
                string status,
                JsonObject issue)
            {
                return new CandidateDecisionReview(false, status, issue);
            }
        }
    }
}
