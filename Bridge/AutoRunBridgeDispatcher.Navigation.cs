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
        AutoRunBridgeData data = new AutoRunBridgeData
        {
            messages = _navigationJob.Messages,
            openViews = AutoRunViewService.ListOpenViewNames(),
        };
        CompleteNavigation(AutoRunBridgeResponses.Success(_navigationJob.Id, $"Navigation completed: {_navigationJob.RouteId}", data));
    }

    private void CompleteNavigationFail(string code, string message)
    {
        AutoRunBridgeResponse response = AutoRunBridgeResponses.Fail(_navigationJob.Id, code, message);
        response.data.messages = _navigationJob.Messages;
        response.data.openViews = AutoRunViewService.ListOpenViewNames();
        CompleteNavigation(response);
    }

    private void CompleteNavigation(AutoRunBridgeResponse response)
    {
        if (_navigationJob == null)
        {
            return;
        }

        _navigationJob.Job.Response = response;
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
