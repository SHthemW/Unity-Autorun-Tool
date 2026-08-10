using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class AutoRunBridgeDispatcher
{
    private const float NavigationDefaultStepTimeoutSeconds = 15f;
    private const float RepeatedControlProbeIntervalSeconds = 0.75f;
    private const float RepeatedControlBacktrackTriggerSeconds = 8f;
    private const float RepeatedControlBacktrackTimeoutSeconds = 5f;
    private const float BranchSelectorSettleTimeoutSeconds = 5f;
    private const float RepeatedControlDiscoveryDelaySeconds = 3f;
    private const float NavigationMinimumClickSettleSeconds = 0.1f;
    private const float NavigationTargetStableSeconds = 0.15f;
    private const int NavigationTargetStableFrames = 2;

    private AutoRunNavigationJob _navigationJob;

    private bool StartNavigation(AutoRunBridgeJob job)
    {
        AutoRunBridgeRequest request = job.Request;
        if (_navigationJob != null || _sequenceJob != null)
        {
            job.Response = AutoRunBridgeResponses.Fail(request.id, "navigation_busy", "Another navigation or sequence request is still running.");
            return true;
        }

        AutoRunBridgePayload payload = request.payload ?? new AutoRunBridgePayload();
        List<AutoRunNavStep> steps = payload.navigationSteps ?? new List<AutoRunNavStep>();
        if (steps.Count == 0)
        {
            job.Response = AutoRunBridgeResponses.Success(request.id, "Navigation has no steps.", new AutoRunBridgeData());
            return true;
        }

        _navigationJob = new AutoRunNavigationJob(job, payload.routeId, payload.targetViewId, steps);
        _navigationJob.Messages.Add("Navigation started: route=" + _navigationJob.RouteId
            + ", steps=" + NavigationAutoRunLog.FormatSteps(steps));
        return false;
    }

    private AutoRunBridgeResponse StartTrackedNavigation(AutoRunBridgeRequest request)
    {
        AutoRunBridgePayload payload = request.payload ?? new AutoRunBridgePayload();
        string targetToken = payload.targetViewId;
        if (string.IsNullOrWhiteSpace(targetToken))
        {
            return AutoRunBridgeResponses.Fail(request.id, "navigation_target_required", "targetViewId is required.");
        }

        if (NavigationAutoRunSession.HasTrackedNavigation && !NavigationAutoRunSession.IsTrackedTerminal)
        {
            if (string.Equals(NavigationAutoRunSession.NavigationTargetViewId, targetToken, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NavigationAutoRunSession.NavigationTargetName, targetToken, StringComparison.OrdinalIgnoreCase))
            {
                return CreateTrackedNavigationStatusResponse(request.id, "Navigation is already running.");
            }

            return AutoRunBridgeResponses.Fail(
                request.id,
                "navigation_busy",
                "Another tracked navigation is already running: " + NavigationAutoRunSession.NavigationId
            );
        }

        if (NavigationAutoRunSession.HasPending || NavigationAutoRunSession.HasActiveRequest || _navigationJob != null || _sequenceJob != null)
        {
            return AutoRunBridgeResponses.Fail(request.id, "navigation_busy", "Another sequence or navigation request is still running.");
        }

        NavigationAutoRunMap map = NavigationAutoRunMap.LoadDefault();
        NavigationAutoRunOption target = ResolveNavigationTarget(map, targetToken);
        if (target == null)
        {
            return AutoRunBridgeResponses.Fail(
                request.id,
                "navigation_target_not_found",
                "Navigation target was not found in the current map: " + targetToken
            );
        }

        string navigationId = string.IsNullOrEmpty(payload.navigationId)
            ? "nav-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : payload.navigationId;
        int runId = unchecked((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & int.MaxValue));
        string phase = EditorApplication.isPlaying ? "resolving_route" : "waiting_for_play_mode";
        NavigationAutoRunSession.StartTracked(navigationId, target, runId, phase);
        NavigationAutoRunPendingRunner.EnsureListening();
        if (payload.ensurePlayMode && !EditorApplication.isPlaying)
        {
            NavigationAutoRunSession.SetTrackedProgress("entering_play_mode");
            EditorApplication.isPlaying = true;
        }

        return CreateTrackedNavigationStatusResponse(request.id, "Tracked UI navigation started.");
    }

    private AutoRunBridgeResponse GetTrackedNavigationStatus(AutoRunBridgeRequest request)
    {
        if (!NavigationAutoRunSession.HasTrackedNavigation)
        {
            return AutoRunBridgeResponses.Fail(request.id, "navigation_not_found", "No tracked UI navigation exists.");
        }

        string navigationId = request.payload?.navigationId;
        if (!NavigationAutoRunSession.MatchesNavigationId(navigationId))
        {
            return AutoRunBridgeResponses.Fail(request.id, "navigation_not_found", "Tracked UI navigation was not found: " + navigationId);
        }

        return CreateTrackedNavigationStatusResponse(request.id, "Tracked UI navigation status.");
    }

    private AutoRunBridgeResponse CancelTrackedNavigation(AutoRunBridgeRequest request)
    {
        if (!NavigationAutoRunSession.HasTrackedNavigation)
        {
            return AutoRunBridgeResponses.Success(request.id, "No tracked UI navigation exists.");
        }

        string navigationId = request.payload?.navigationId;
        if (!NavigationAutoRunSession.MatchesNavigationId(navigationId))
        {
            return AutoRunBridgeResponses.Fail(request.id, "navigation_not_found", "Tracked UI navigation was not found: " + navigationId);
        }

        if (!NavigationAutoRunSession.IsTrackedTerminal)
        {
            NavigationAutoRunSession.CancelTracked("Tracked UI navigation was canceled.");
            NavigationAutoRunSession.ClearPending();
            NavigationAutoRunSession.ClearActiveRequest();
            if (_navigationJob != null)
            {
                CompleteNavigationFail("navigation_canceled", "Tracked UI navigation was canceled.");
            }
        }

        return CreateTrackedNavigationStatusResponse(request.id, "Tracked UI navigation canceled.");
    }

    private static NavigationAutoRunOption ResolveNavigationTarget(NavigationAutoRunMap map, string token)
    {
        return map.ListNavigableTargets().Find(target =>
            string.Equals(target.ViewId, token, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target.Name, token, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target.DisplayName, token, StringComparison.OrdinalIgnoreCase)
        );
    }

    private AutoRunBridgeResponse CreateTrackedNavigationStatusResponse(string requestId, string message)
    {
        string runtimeTarget = NavigationAutoRunSession.NavigationTargetRuntimeToken;
        if (string.IsNullOrEmpty(runtimeTarget))
        {
            runtimeTarget = string.IsNullOrEmpty(NavigationAutoRunSession.NavigationTargetName)
                ? NavigationAutoRunSession.NavigationTargetViewId
                : NavigationAutoRunSession.NavigationTargetName;
        }

        int totalMatches = 0;
        List<string> matches = NavigationAutoRunSession.IsTrackedTerminal
            ? AutoRunViewService.ListOpenViewMatches(
                runtimeTarget,
                10,
                out totalMatches
            )
            : new List<string>();
        var data = new AutoRunBridgeData
        {
            isPlaying = EditorApplication.isPlaying,
            unityVersion = Application.unityVersion,
            bridgeVersion = AutoRunBridgeServer.Version,
            navigationId = NavigationAutoRunSession.NavigationId,
            navigationStatus = NavigationAutoRunSession.NavigationStatus,
            navigationPhase = NavigationAutoRunSession.NavigationPhase,
            targetViewId = NavigationAutoRunSession.NavigationTargetViewId,
            routeId = NavigationAutoRunSession.NavigationRouteId,
            resultCode = NavigationAutoRunSession.NavigationResultCode,
            resultMessage = NavigationAutoRunSession.NavigationResultMessage,
            messages = _navigationJob == null
                ? NavigationAutoRunSession.NavigationMessages
                : new List<string>(_navigationJob.Messages),
            terminal = NavigationAutoRunSession.IsTrackedTerminal,
            elapsedMilliseconds = NavigationAutoRunSession.NavigationElapsedMilliseconds,
            viewOpen = totalMatches > 0,
            openViewCount = totalMatches,
            openViews = matches,
            truncated = totalMatches > matches.Count,
        };
        return AutoRunBridgeResponses.Success(requestId, message, data);
    }

    private void PumpNavigation()
    {
        if (_navigationJob == null)
        {
            return;
        }

        _navigationJob.StepElapsed = (float)(EditorApplication.timeSinceStartup - _navigationJob.StepStartedAt);
        if (_navigationJob.CurrentIndex >= _navigationJob.Steps.Count)
        {
            CompleteNavigationSuccess();
            return;
        }

        if (_navigationJob.IsBacktracking)
        {
            PumpRepeatedControlBacktrack();
            return;
        }

        AutoRunNavStep step = _navigationJob.Steps[_navigationJob.CurrentIndex];
        if (step.mode == "click")
        {
            RunNavigationClickStep(step);
            return;
        }

        if (step.mode == "wait")
        {
            RunNavigationWaitStep(step);
            return;
        }

        CompleteNavigationFail("navigation_manual_step", $"Navigation step '{step.transitionId}' requires unsupported mode '{step.mode}'.");
    }

    private AutoRunBridgeResponse CancelNavigation(AutoRunBridgeRequest request)
    {
        if (_navigationJob == null)
        {
            return AutoRunBridgeResponses.Success(request.id, "No navigation is running.");
        }

        CompleteNavigationFail("navigation_canceled", "Navigation canceled.");
        return AutoRunBridgeResponses.Success(request.id, "Navigation cancel requested.");
    }

    private void RunNavigationClickStep(AutoRunNavStep step)
    {
        if (_navigationJob.ClickedSteps.Contains(_navigationJob.CurrentIndex))
        {
            WaitForNavigationClickTarget(step);
            return;
        }

        if (step.action == null)
        {
            CompleteNavigationFail("navigation_missing_action", $"Navigation step '{step.transitionId}' has no click action.");
            return;
        }

        int matchIndex = _navigationJob.NextMatchIndexes.TryGetValue(
            _navigationJob.CurrentIndex,
            out int nextMatchIndex)
            ? nextMatchIndex
            : 0;
        if (!AutoRunButtonService.HasButton(step.action, matchIndex))
        {
            if (TryBeginRepeatedControlDiscovery(step))
            {
                return;
            }

            if (TryBeginRepeatedControlBacktrack(step))
            {
                return;
            }

            CompleteNavigationTimeout(step, "button '" + step.action.buttonName
                + "' (controlId=" + step.controlId + ")");
            return;
        }

        int matchCount = AutoRunButtonService.GetButtonMatchCount(step.action);
        AutoRunButtonResult result =
            AutoRunButtonService.ClickForNavigation(
            step.action,
            matchIndex);
        if (!result.ok)
        {
            LogNavigationDiagnosticOnce(
                step,
                "click-" + result.code + "-" + matchIndex,
                result.message);
            if (IsRetryableNavigationClick(result))
            {
                RememberUnreachableRepeatedControl(
                    step,
                    matchIndex,
                    matchCount);
                CompleteNavigationTimeout(
                    step,
                    "pointer-reachable button '"
                        + step.action.buttonName
                        + "' (controlId="
                        + step.controlId
                        + ")");
                return;
            }

            CompleteNavigationFail(
                result.code,
                result.message);
            return;
        }

        _navigationJob.Messages.Add(
            FormatStepMessage(step, result.message));
        _navigationJob.ClickedSteps.Add(_navigationJob.CurrentIndex);
        _navigationJob.CurrentClickMatchIndex = matchIndex;
        RememberRepeatedControlProgress(
            step,
            matchIndex,
            matchCount);
        _navigationJob.LastClickAt = EditorApplication.timeSinceStartup;
        _navigationJob.StepStartedAt = EditorApplication.timeSinceStartup;
        _navigationJob.StepElapsed = 0f;
        ResetTargetObservation();
        WaitForNavigationClickTarget(step);
    }

    private void RunNavigationWaitStep(AutoRunNavStep step)
    {
        string waitForViewId = string.IsNullOrEmpty(step.waitForViewId) ? step.toViewId : step.waitForViewId;
        if (IsNavigationTargetStable(
                waitForViewId,
                0f,
                out bool targetActive,
                out string detail))
        {
            _navigationJob.Messages.Add(
                FormatStepMessage(
                    step,
                    $"view '{waitForViewId}' is active, foreground, and stable."));
            AdvanceNavigationStep();
            return;
        }

        LogBlockedTargetOnce(step, waitForViewId, detail);
        CompleteNavigationTargetTimeout(
            step,
            waitForViewId,
            targetActive,
            detail);
    }

    private void WaitForNavigationClickTarget(AutoRunNavStep step)
    {
        string waitForViewId = string.IsNullOrEmpty(step.waitForViewId) ? step.toViewId : step.waitForViewId;
        float clickSettleSeconds = ResolveClickSettleSeconds(step);
        if (string.IsNullOrEmpty(waitForViewId))
        {
            if (_navigationJob.StepElapsed >= clickSettleSeconds)
            {
                AdvanceNavigationStep();
            }

            return;
        }

        if (IsNavigationTargetStable(
                waitForViewId,
                clickSettleSeconds,
                out bool targetActive,
                out string detail))
        {
            _navigationJob.Messages.Add(
                FormatStepMessage(
                    step,
                    $"view '{waitForViewId}' is active, foreground, and stable after {clickSettleSeconds:0.###}s settle time."));
            AdvanceNavigationStep();
            return;
        }

        if (!targetActive && TryClickNextRepeatedControl(step))
        {
            return;
        }

        LogBlockedTargetOnce(step, waitForViewId, detail);
        CompleteNavigationTargetTimeout(
            step,
            waitForViewId,
            targetActive,
            detail);
    }

    private bool TryClickNextRepeatedControl(AutoRunNavStep step)
    {
        if (step.action == null
            || !string.Equals(
                step.action.matchPolicy,
                AutoRunParam.MATCH_FIRST_INTERACTABLE,
                StringComparison.OrdinalIgnoreCase)
            || _navigationJob.LastClickAt <= 0d
            || EditorApplication.timeSinceStartup - _navigationJob.LastClickAt
                < RepeatedControlProbeIntervalSeconds)
        {
            return false;
        }

        int nextMatchIndex = _navigationJob.CurrentClickMatchIndex + 1;
        int matchCount = AutoRunButtonService.GetButtonMatchCount(step.action);
        if (nextMatchIndex >= matchCount)
        {
            return false;
        }

        AutoRunButtonResult result =
            AutoRunButtonService.ClickForNavigation(
            step.action,
            nextMatchIndex);
        _navigationJob.Messages.Add(
            FormatStepMessage(
                step,
                $"repeated-control attempt {nextMatchIndex + 1}/{matchCount}: {result.message}"));
        if (!result.ok
            && !IsRetryableNavigationClick(result))
        {
            CompleteNavigationFail(
                result.code,
                result.message);
            return true;
        }

        _navigationJob.CurrentClickMatchIndex = nextMatchIndex;
        RememberRepeatedControlProgress(
            step,
            nextMatchIndex,
            matchCount);
        _navigationJob.LastClickAt = EditorApplication.timeSinceStartup;
        if (result.ok)
        {
            ResetTargetObservation();
        }

        return true;
    }

    private void RememberUnreachableRepeatedControl(
        AutoRunNavStep step,
        int matchIndex,
        int matchCount)
    {
        if (step.action == null
            || !string.Equals(
                step.action.matchPolicy,
                AutoRunParam.MATCH_FIRST_INTERACTABLE,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _navigationJob.CurrentClickMatchIndex = matchIndex;
        RememberRepeatedControlProgress(
            step,
            matchIndex,
            matchCount);
        _navigationJob.LastClickAt =
            EditorApplication.timeSinceStartup;
    }

    private static bool IsRetryableNavigationClick(
        AutoRunButtonResult result)
    {
        if (result == null)
        {
            return false;
        }

        return result.code == "button_not_found"
            || result.code == "button_not_interactable"
            || result.code == "button_not_pointer_reachable"
            || result.code == "event_system_not_ready"
            || result.code == "pointer_click_not_handled";
    }

    private bool IsNavigationTargetStable(
        string viewId,
        float minimumStepElapsed,
        out bool targetActive,
        out string detail)
    {
        targetActive = AutoRunViewService.HasView(viewId);
        if (!targetActive)
        {
            ResetTargetObservation();
            detail = "view is not active.";
            return false;
        }

        if (!AutoRunViewService.IsViewForeground(
                viewId,
                out string foregroundDetail))
        {
            ResetTargetObservation();
            detail = foregroundDetail;
            return false;
        }

        double now = EditorApplication.timeSinceStartup;
        if (_navigationJob.ObservedTargetStepIndex
                != _navigationJob.CurrentIndex
            || !string.Equals(
                _navigationJob.ObservedTargetViewId,
                viewId,
                StringComparison.Ordinal))
        {
            _navigationJob.ObservedTargetStepIndex =
                _navigationJob.CurrentIndex;
            _navigationJob.ObservedTargetViewId = viewId;
            _navigationJob.TargetReadySince = now;
            _navigationJob.TargetReadyFrameCount = 0;
            _navigationJob.LastTargetReadyFrame = -1;
        }

        int frame = Time.frameCount;
        if (_navigationJob.LastTargetReadyFrame != frame)
        {
            _navigationJob.LastTargetReadyFrame = frame;
            _navigationJob.TargetReadyFrameCount++;
        }

        double stableSeconds = now
            - _navigationJob.TargetReadySince;
        bool delayElapsed = _navigationJob.StepElapsed
            >= minimumStepElapsed;
        bool stable = stableSeconds
                >= NavigationTargetStableSeconds
            && _navigationJob.TargetReadyFrameCount
                >= NavigationTargetStableFrames;
        detail = !delayElapsed
            ? $"waiting for {minimumStepElapsed:0.###}s post-click settle delay."
            : !stable
                ? "view is foreground but has not remained stable for enough rendered frames."
                : foregroundDetail;
        return delayElapsed && stable;
    }

    private static float ResolveClickSettleSeconds(
        AutoRunNavStep step)
    {
        float configuredDelay = step?.action == null
            ? 0f
            : Mathf.Max(0f, step.action.delay);
        return Mathf.Max(
            NavigationMinimumClickSettleSeconds,
            configuredDelay);
    }

    private void LogBlockedTargetOnce(
        AutoRunNavStep step,
        string viewId,
        string detail)
    {
        if (string.IsNullOrEmpty(detail)
            || detail == "view is not active."
            || detail.StartsWith(
                "waiting for ",
                StringComparison.Ordinal)
            || detail.StartsWith(
                "view is foreground",
                StringComparison.Ordinal))
        {
            return;
        }

        LogNavigationDiagnosticOnce(
            step,
            "target-blocked-" + viewId,
            "target view '"
                + viewId
                + "' is active but not foreground-ready: "
                + detail);
    }

    private void LogNavigationDiagnosticOnce(
        AutoRunNavStep step,
        string diagnostic,
        string message)
    {
        string key = _navigationJob.CurrentIndex
            + ":"
            + diagnostic;
        if (!_navigationJob.NavigationDiagnostics.Add(key))
        {
            return;
        }

        _navigationJob.Messages.Add(
            FormatStepMessage(step, message));
    }

    private void ResetTargetObservation()
    {
        _navigationJob.ObservedTargetStepIndex = -1;
        _navigationJob.ObservedTargetViewId = null;
        _navigationJob.TargetReadySince = 0d;
        _navigationJob.TargetReadyFrameCount = 0;
        _navigationJob.LastTargetReadyFrame = -1;
    }

    private void RememberRepeatedControlProgress(
        AutoRunNavStep step,
        int matchIndex,
        int matchCount)
    {
        if (step.action == null
            || !string.Equals(
                step.action.matchPolicy,
                AutoRunParam.MATCH_FIRST_INTERACTABLE,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int rememberedMatchCount =
            _navigationJob.RepeatedMatchCounts.TryGetValue(
                _navigationJob.CurrentIndex,
                out int previousMatchCount)
                ? Math.Max(previousMatchCount, matchCount)
                : matchCount;
        _navigationJob.RepeatedMatchCounts[_navigationJob.CurrentIndex] =
            rememberedMatchCount;
        _navigationJob.NextMatchIndexes[_navigationJob.CurrentIndex] =
            matchIndex + 1;
    }

    private bool TryBeginRepeatedControlDiscovery(
        AutoRunNavStep step)
    {
        if (step.action == null
            || !string.Equals(
                step.action.matchPolicy,
                AutoRunParam.MATCH_FIRST_INTERACTABLE,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int stepIndex = _navigationJob.CurrentIndex;
        bool knownCheckpoint =
            _navigationJob.RepeatedMatchCounts.ContainsKey(
                stepIndex);
        if (!knownCheckpoint
            && _navigationJob.StepElapsed
                < RepeatedControlDiscoveryDelaySeconds)
        {
            return false;
        }

        int matchCount =
            AutoRunButtonService.GetButtonMatchCount(step.action);
        int nextMatchIndex =
            _navigationJob.NextMatchIndexes.TryGetValue(
                stepIndex,
                out int rememberedNextMatchIndex)
                ? rememberedNextMatchIndex
                : 0;
        _navigationJob.RepeatedMatchCounts[stepIndex] =
            Math.Max(
                matchCount,
                _navigationJob.RepeatedMatchCounts.TryGetValue(
                    stepIndex,
                    out int rememberedMatchCount)
                    ? rememberedMatchCount
                    : 0);
        _navigationJob.NextMatchIndexes[stepIndex] =
            nextMatchIndex;
        _navigationJob.Messages.Add(
            FormatStepMessage(
                step,
                "repeated control is not currently visible; exploring its scroll pages and branch selectors."));
        BeginRepeatedControlRecovery(stepIndex);
        return true;
    }

    private bool TryBeginRepeatedControlBacktrack(AutoRunNavStep blockedStep)
    {
        if (_navigationJob.StepElapsed
                < RepeatedControlBacktrackTriggerSeconds
            || blockedStep.action == null
            || _navigationJob.RepeatedMatchCounts.Count == 0)
        {
            return false;
        }

        int? checkpointIndex = _navigationJob.NextMatchIndexes.Keys
            .Where(index => index < _navigationJob.CurrentIndex)
            .OrderByDescending(index => index)
            .Select(index => (int?)index)
            .FirstOrDefault();
        if (!checkpointIndex.HasValue)
        {
            LogBacktrackDiagnosticOnce(
                "no-checkpoint",
                blockedStep,
                "branch precondition unavailable, but no earlier repeated-control checkpoint has an untried match; "
                + FormatRepeatedControlState());
            return false;
        }

        AutoRunButtonResult dismissResult =
            AutoRunButtonService.ClickDismissButton(
                blockedStep.action.scopeRootName);
        if (!dismissResult.ok)
        {
            LogBacktrackDiagnosticOnce(
                "dismiss-failed",
                blockedStep,
                "branch precondition unavailable; backtrack could not dismiss the current view: "
                + dismissResult.message
                + "; "
                + FormatRepeatedControlState());
            return false;
        }

        int checkpoint = checkpointIndex.Value;
        _navigationJob.Messages.Add(
            FormatStepMessage(
                blockedStep,
                "branch precondition unavailable; "
                + dismissResult.message
                + ", retrying repeated control step "
                + _navigationJob.Steps[checkpoint].transitionId
                + "."));
        BeginRepeatedControlRecovery(checkpoint);
        return true;
    }

    private void BeginRepeatedControlRecovery(int checkpoint)
    {
        _navigationJob.IsBacktracking = true;
        _navigationJob.BacktrackStepIndex = checkpoint;
        _navigationJob.BacktrackStartedAt =
            EditorApplication.timeSinceStartup;
        _navigationJob.BacktrackScrollPending = false;
        _navigationJob.BacktrackCandidateSignature = null;
        _navigationJob.BacktrackScrollFailure = null;
        _navigationJob.BacktrackScrollAttempted = false;
        _navigationJob.BacktrackBranchSwitchPending = false;
    }

    private void LogBacktrackDiagnosticOnce(
        string diagnostic,
        AutoRunNavStep step,
        string message)
    {
        string key = _navigationJob.CurrentIndex + ":" + diagnostic;
        if (!_navigationJob.BacktrackDiagnostics.Add(key))
        {
            return;
        }

        _navigationJob.Messages.Add(FormatStepMessage(step, message));
    }

    private string FormatRepeatedControlState()
    {
        if (_navigationJob.RepeatedMatchCounts.Count == 0)
        {
            return "repeated-control checkpoints=none";
        }

        string checkpoints = string.Join(
            ", ",
            _navigationJob.RepeatedMatchCounts
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    int next = _navigationJob.NextMatchIndexes.TryGetValue(
                        pair.Key,
                        out int value)
                        ? value
                        : 0;
                    AutoRunNavStep step = pair.Key >= 0
                        && pair.Key < _navigationJob.Steps.Count
                        ? _navigationJob.Steps[pair.Key]
                        : null;
                    return (step?.transitionId ?? ("step-" + pair.Key))
                        + " next=" + next
                        + "/" + pair.Value;
                }));
        return "repeated-control checkpoints=[" + checkpoints + "]";
    }

    private void PumpRepeatedControlBacktrack()
    {
        int checkpointIndex = _navigationJob.BacktrackStepIndex;
        if (checkpointIndex < 0
            || checkpointIndex >= _navigationJob.Steps.Count)
        {
            CompleteNavigationFail(
                "navigation_backtrack_invalid",
                "Repeated-control navigation checkpoint is invalid.");
            return;
        }

        AutoRunNavStep checkpoint = _navigationJob.Steps[checkpointIndex];
        int nextMatchIndex = _navigationJob.NextMatchIndexes[
            checkpointIndex];
        if (checkpoint.action == null)
        {
            CompleteNavigationFail(
                "navigation_backtrack_invalid",
                "Repeated-control navigation checkpoint has no action.");
            return;
        }

        double backtrackElapsed = EditorApplication.timeSinceStartup
            - _navigationJob.BacktrackStartedAt;
        if (_navigationJob.BacktrackBranchSwitchPending)
        {
            string currentSignature =
                AutoRunButtonService.GetButtonMatchSignature(
                    checkpoint.action);
            bool contentChanged = !string.IsNullOrEmpty(
                    currentSignature)
                && !string.Equals(
                    currentSignature,
                    _navigationJob.BacktrackCandidateSignature,
                    StringComparison.Ordinal);
            if (!contentChanged
                && backtrackElapsed
                    < BranchSelectorSettleTimeoutSeconds)
            {
                return;
            }

            _navigationJob.Messages.Add(
                FormatStepMessage(
                    checkpoint,
                    contentChanged
                        ? "branch selector content change observed."
                        : "branch selector settle timeout elapsed; retrying visible controls."));
            AutoRunButtonResult resetResult =
                AutoRunButtonService.ResetScrollToStart(
                    checkpoint.action);
            if (resetResult.ok)
            {
                _navigationJob.Messages.Add(
                    FormatStepMessage(
                        checkpoint,
                        "branch exploration restarted from the first page; "
                        + resetResult.message));
            }

            ReturnToRepeatedControlStep(checkpointIndex);
            return;
        }

        if (_navigationJob.BacktrackScrollPending)
        {
            string currentSignature =
                AutoRunButtonService.GetButtonMatchSignature(
                    checkpoint.action);
            if (!string.IsNullOrEmpty(currentSignature)
                && !string.Equals(
                    currentSignature,
                    _navigationJob.BacktrackCandidateSignature,
                    StringComparison.Ordinal))
            {
                _navigationJob.NextMatchIndexes[checkpointIndex] = 0;
                ReturnToRepeatedControlStep(checkpointIndex);
                return;
            }

            if (backtrackElapsed
                < RepeatedControlBacktrackTimeoutSeconds)
            {
                return;
            }

            _navigationJob.Messages.Add(
                FormatStepMessage(
                    checkpoint,
                    "scroll completed, but no candidate-content change was observed; continuing with alternate branches."));
            _navigationJob.BacktrackScrollPending = false;
            _navigationJob.BacktrackScrollFailure =
                "The visible candidate content did not change after scrolling.";
            _navigationJob.BacktrackStartedAt =
                EditorApplication.timeSinceStartup - 0.5d;
            return;
        }

        if (AutoRunButtonService.HasButton(
            checkpoint.action,
            nextMatchIndex))
        {
            ReturnToRepeatedControlStep(checkpointIndex);
            return;
        }

        int currentMatchCount =
            AutoRunButtonService.GetButtonMatchCount(checkpoint.action);
        if (nextMatchIndex >= currentMatchCount
            && backtrackElapsed >= 0.5d)
        {
            if (!_navigationJob.BacktrackScrollAttempted)
            {
                _navigationJob.BacktrackCandidateSignature =
                    AutoRunButtonService.GetButtonMatchSignature(
                        checkpoint.action);
                AutoRunButtonResult scrollResult =
                    AutoRunButtonService.ScrollForMoreMatches(
                        checkpoint.action);
                _navigationJob.Messages.Add(
                    FormatStepMessage(
                        checkpoint,
                        "visible repeated controls exhausted; "
                        + scrollResult.message));
                _navigationJob.BacktrackScrollAttempted = true;
                if (scrollResult.ok)
                {
                    _navigationJob.BacktrackScrollPending = true;
                    _navigationJob.BacktrackStartedAt =
                        EditorApplication.timeSinceStartup;
                    return;
                }

                _navigationJob.BacktrackScrollFailure =
                    scrollResult.message;
            }

            int branchSelectorIndex =
                _navigationJob.NextBranchSelectorIndexes.TryGetValue(
                    checkpointIndex,
                    out int rememberedBranchSelectorIndex)
                    ? rememberedBranchSelectorIndex
                    : 0;
            int branchSelectorCount =
                AutoRunButtonService.GetBranchSelectorCount(
                    checkpoint.action.scopeRootName);
            if (branchSelectorIndex < branchSelectorCount)
            {
                _navigationJob.BacktrackCandidateSignature =
                    AutoRunButtonService.GetButtonMatchSignature(
                        checkpoint.action);
                AutoRunButtonResult branchResult =
                    AutoRunButtonService.ClickBranchSelector(
                        checkpoint.action.scopeRootName,
                        branchSelectorIndex);
                _navigationJob.Messages.Add(
                    FormatStepMessage(
                        checkpoint,
                        "current repeated-control branch exhausted; "
                        + branchResult.message));
                _navigationJob.NextBranchSelectorIndexes[
                    checkpointIndex] = branchSelectorIndex + 1;
                if (branchResult.ok)
                {
                    _navigationJob.NextMatchIndexes[
                        checkpointIndex] = 0;
                    _navigationJob.BacktrackBranchSwitchPending =
                        true;
                    _navigationJob.BacktrackStartedAt =
                        EditorApplication.timeSinceStartup;
                    return;
                }
            }
        }

        if (backtrackElapsed
            < RepeatedControlBacktrackTimeoutSeconds)
        {
            return;
        }

        string failureDetail = string.IsNullOrEmpty(
            _navigationJob.BacktrackScrollFailure)
                ? ""
                : " " + _navigationJob.BacktrackScrollFailure;
        CompleteNavigationFail(
            "navigation_backtrack_timeout",
            $"Timed out returning to repeated-control step '{checkpoint.transitionId}'.{failureDetail}");
    }

    private void ReturnToRepeatedControlStep(int checkpointIndex)
    {
        _navigationJob.ClickedSteps.RemoveWhere(
            index => index >= checkpointIndex);
        _navigationJob.CurrentIndex = checkpointIndex;
        _navigationJob.CurrentClickMatchIndex = -1;
        _navigationJob.LastClickAt = 0d;
        _navigationJob.IsBacktracking = false;
        _navigationJob.BacktrackStepIndex = -1;
        _navigationJob.BacktrackScrollPending = false;
        _navigationJob.BacktrackCandidateSignature = null;
        _navigationJob.BacktrackScrollFailure = null;
        _navigationJob.BacktrackScrollAttempted = false;
        _navigationJob.BacktrackBranchSwitchPending = false;
        _navigationJob.StepStartedAt =
            EditorApplication.timeSinceStartup;
        _navigationJob.StepElapsed = 0f;
        ResetTargetObservation();
    }

    private void CompleteNavigationTimeout(AutoRunNavStep step, string target)
    {
        float timeout = step.timeout > 0f ? step.timeout : NavigationDefaultStepTimeoutSeconds;
        if (_navigationJob.StepElapsed < timeout)
        {
            return;
        }

        CompleteNavigationFail("navigation_wait_timeout", $"Navigation step '{step.transitionId}' timed out waiting for {target} within {timeout:0.##}s.");
    }

    private void CompleteNavigationTargetTimeout(
        AutoRunNavStep step,
        string viewId,
        bool targetActive,
        string detail)
    {
        float timeout = step.timeout > 0f
            ? step.timeout
            : NavigationDefaultStepTimeoutSeconds;
        if (_navigationJob.StepElapsed < timeout)
        {
            return;
        }

        if (!targetActive)
        {
            CompleteNavigationTimeout(
                step,
                $"stable foreground view '{viewId}' ({detail})");
            return;
        }

        string code = detail != null
                && detail.StartsWith(
                    "view is covered",
                    StringComparison.Ordinal)
            ? "navigation_target_covered"
            : "navigation_target_not_ready";
        CompleteNavigationFail(
            code,
            $"Navigation step '{step.transitionId}' found target view '{viewId}', "
            + $"but it did not become foreground-ready within {timeout:0.##}s: {detail}");
    }

    private void AdvanceNavigationStep()
    {
        _navigationJob.CurrentIndex++;
        _navigationJob.CurrentClickMatchIndex = -1;
        _navigationJob.LastClickAt = 0d;
        _navigationJob.StepStartedAt = EditorApplication.timeSinceStartup;
        _navigationJob.StepElapsed = 0f;
        ResetTargetObservation();
    }

    private string FormatStepMessage(AutoRunNavStep step, string message)
    {
        int stepIndex = _navigationJob.Steps.IndexOf(step);
        if (stepIndex < 0)
        {
            stepIndex = _navigationJob.CurrentIndex;
        }

        return $"nav {stepIndex + 1}/{_navigationJob.Steps.Count} [{step.mode}] {step.fromViewId} -> {step.toViewId}: {message}";
    }

    private void CompleteNavigationSuccess()
    {
        string runtimeTarget = GetNavigationRuntimeTarget();
        int totalMatches;
        AutoRunBridgeData data = new AutoRunBridgeData
        {
            messages = _navigationJob.Messages,
            openViews = AutoRunViewService.ListOpenViewMatches(runtimeTarget, 20, out totalMatches),
            openViewCount = totalMatches,
        };
        data.viewOpen = data.openViewCount > 0;
        data.truncated = data.openViewCount > data.openViews.Count;
        CompleteNavigation(AutoRunBridgeResponses.Success(_navigationJob.Id, $"Navigation completed: {_navigationJob.RouteId}", data));
    }

    private void CompleteNavigationFail(string code, string message)
    {
        string runtimeTarget = GetNavigationRuntimeTarget();
        AutoRunBridgeResponse response = AutoRunBridgeResponses.Fail(_navigationJob.Id, code, message);
        response.data.messages = _navigationJob.Messages;
        int totalMatches;
        response.data.openViews = AutoRunViewService.ListOpenViewMatches(runtimeTarget, 20, out totalMatches);
        response.data.openViewCount = totalMatches;
        response.data.viewOpen = totalMatches > 0;
        response.data.truncated = totalMatches > response.data.openViews.Count;
        CompleteNavigation(response);
    }

    private string GetNavigationRuntimeTarget()
    {
        for (int index = _navigationJob.Steps.Count - 1; index >= 0; index--)
        {
            AutoRunNavStep step = _navigationJob.Steps[index];
            string target = string.IsNullOrEmpty(step.waitForViewId) ? step.toViewId : step.waitForViewId;
            if (!string.IsNullOrEmpty(target))
            {
                return target;
            }
        }

        return _navigationJob.TargetViewId;
    }

    private void CompleteNavigation(AutoRunBridgeResponse response)
    {
        if (_navigationJob == null)
        {
            return;
        }

        _navigationJob.Job.Response = response;
        PopulateRuntimeData(response);
        LogResponse(response);
        _navigationJob.Job.WaitHandle.Set();
        _navigationJob = null;
    }

    private sealed class AutoRunNavigationJob
    {
        public AutoRunNavigationJob(AutoRunBridgeJob job, string routeId, string targetViewId, List<AutoRunNavStep> steps)
        {
            Job = job;
            RouteId = string.IsNullOrEmpty(routeId) ? "computed" : routeId;
            TargetViewId = targetViewId;
            Steps = steps;
            StepStartedAt = EditorApplication.timeSinceStartup;
        }

        public AutoRunBridgeJob Job { get; }
        public string Id => Job.Request.id;
        public string RouteId { get; }
        public string TargetViewId { get; }
        public List<AutoRunNavStep> Steps { get; }
        public List<string> Messages { get; } = new List<string>();
        public HashSet<int> ClickedSteps { get; } = new HashSet<int>();
        public Dictionary<int, int> RepeatedMatchCounts { get; } =
            new Dictionary<int, int>();
        public Dictionary<int, int> NextMatchIndexes { get; } =
            new Dictionary<int, int>();
        public Dictionary<int, int> NextBranchSelectorIndexes { get; } =
            new Dictionary<int, int>();
        public HashSet<string> BacktrackDiagnostics { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> NavigationDiagnostics { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        public int CurrentIndex { get; set; }
        public int CurrentClickMatchIndex { get; set; } = -1;
        public double LastClickAt { get; set; }
        public bool IsBacktracking { get; set; }
        public int BacktrackStepIndex { get; set; } = -1;
        public double BacktrackStartedAt { get; set; }
        public bool BacktrackScrollPending { get; set; }
        public string BacktrackCandidateSignature { get; set; }
        public string BacktrackScrollFailure { get; set; }
        public bool BacktrackScrollAttempted { get; set; }
        public bool BacktrackBranchSwitchPending { get; set; }
        public int ObservedTargetStepIndex { get; set; } = -1;
        public string ObservedTargetViewId { get; set; }
        public double TargetReadySince { get; set; }
        public int TargetReadyFrameCount { get; set; }
        public int LastTargetReadyFrame { get; set; } = -1;
        public float StepElapsed { get; set; }
        public double StepStartedAt { get; set; }
    }
}
