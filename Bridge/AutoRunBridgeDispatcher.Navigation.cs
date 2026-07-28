using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class AutoRunBridgeDispatcher
{
    private const float NavigationDefaultStepTimeoutSeconds = 15f;

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

    private static AutoRunBridgeResponse CreateTrackedNavigationStatusResponse(string requestId, string message)
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

        if (!AutoRunButtonService.HasButton(step.action))
        {
            CompleteNavigationTimeout(step, "button '" + step.action.buttonName
                + "' (controlId=" + step.controlId + ")");
            return;
        }

        AutoRunButtonResult result = AutoRunButtonService.Click(step.action);
        _navigationJob.Messages.Add(FormatStepMessage(step, result.message));
        if (!result.ok)
        {
            CompleteNavigationFail(result.code, result.message);
            return;
        }

        _navigationJob.ClickedSteps.Add(_navigationJob.CurrentIndex);
        _navigationJob.StepStartedAt = EditorApplication.timeSinceStartup;
        _navigationJob.StepElapsed = 0f;
        WaitForNavigationClickTarget(step);
    }

    private void RunNavigationWaitStep(AutoRunNavStep step)
    {
        string waitForViewId = string.IsNullOrEmpty(step.waitForViewId) ? step.toViewId : step.waitForViewId;
        if (AutoRunViewService.HasView(waitForViewId))
        {
            _navigationJob.Messages.Add(FormatStepMessage(step, $"view '{waitForViewId}' appeared."));
            AdvanceNavigationStep();
            return;
        }

        CompleteNavigationTimeout(step, $"view '{waitForViewId}'");
    }

    private void WaitForNavigationClickTarget(AutoRunNavStep step)
    {
        string waitForViewId = string.IsNullOrEmpty(step.waitForViewId) ? step.toViewId : step.waitForViewId;
        if (string.IsNullOrEmpty(waitForViewId))
        {
            AdvanceNavigationStep();
            return;
        }

        if (AutoRunViewService.HasView(waitForViewId))
        {
            _navigationJob.Messages.Add(FormatStepMessage(step, $"view '{waitForViewId}' appeared."));
            AdvanceNavigationStep();
            return;
        }

        CompleteNavigationTimeout(step, $"view '{waitForViewId}'");
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

    private void AdvanceNavigationStep()
    {
        _navigationJob.CurrentIndex++;
        _navigationJob.StepStartedAt = EditorApplication.timeSinceStartup;
        _navigationJob.StepElapsed = 0f;
    }

    private string FormatStepMessage(AutoRunNavStep step, string message)
    {
        return $"nav {_navigationJob.CurrentIndex + 1}/{_navigationJob.Steps.Count} [{step.mode}] {step.fromViewId} -> {step.toViewId}: {message}";
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
        public int CurrentIndex { get; set; }
        public float StepElapsed { get; set; }
        public double StepStartedAt { get; set; }
    }
}
