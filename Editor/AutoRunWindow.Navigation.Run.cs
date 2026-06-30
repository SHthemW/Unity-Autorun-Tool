using System;
using UnityEditor;

public partial class AutoRunWindow
{
    private void OnNavigationAutoRunCompleted(int runId, AutoRunBridgeResponse response)
    {
        if (_navigationCanceled || runId != _navigationRunId)
        {
            return;
        }

        _navigationRunning = false;
        _navigationStatusText = null;
        StopPendingNavigation();
        NavigationAutoRunSession.ClearPending();
        if (response == null)
        {
            LogNavigation("Failed: no response.");
            Repaint();
            return;
        }

        LogNavigation("Completed response ok=" + response.ok + ", code=" + response.code + ", message=" + response.message);
        if (!response.ok)
        {
            LogNavigation("Failed response " + response.code + ": " + response.message);
        }

        Repaint();
    }

    private void StartPendingNavigation(NavigationAutoRunOption target)
    {
        _pendingNavigationTarget = target;
        _navigationStatusText = "Entering Play Mode and waiting for a route to " + target.Name;
        LogNavigation("Entering Play Mode for target " + target.ViewId + ", runId=" + _navigationRunId);
        NavigationAutoRunPendingRunner.EnsureListening();
        EditorApplication.isPlaying = true;
    }

    private bool TryStartNavigationPlan(NavigationAutoRunOption target, bool waiting)
    {
        try
        {
            _navigationStatusText = "Resolving route to " + target.Name;
            var openViews = AutoRunViewService.ListOpenViewNames();
            LogNavigation("Resolving route. target=" + target.ViewId
                + ", openViews=" + NavigationAutoRunLog.FormatOpenViews(openViews));
            NavigationAutoRunPlan plan = _navigationMap.ResolveToTarget(target.ViewId, openViews);
            _navigationStatusText = "Running route " + plan.RouteId;
            LogNavigation("Resolved route " + plan.RouteId + ", steps=" + plan.Steps.Count);
            int runId = _navigationRunId;
            LogNavigation("Sending navigate_route request. runId=" + runId);
            NavigationAutoRunRequest.Start(plan, response => OnNavigationAutoRunCompleted(runId, response));
            return true;
        }
        catch (Exception ex)
        {
            LogNavigation("Resolve attempt failed: " + ex.Message);
            if (!waiting)
            {
                LogNavigation("Failed: " + ex.Message);
                _navigationRunning = false;
                _navigationStatusText = null;
                NavigationAutoRunSession.ClearPending();
                Repaint();
            }

            return false;
        }
    }

    private void StopPendingNavigation()
    {
        _pendingNavigationTarget = null;
        LogNavigation("Stopped pending navigation update.");
    }

    private void RefreshPendingNavigationUiState()
    {
        if (!NavigationAutoRunSession.HasPending)
        {
            if (_navigationRunning && !NavigationAutoRunSession.HasActiveRequest)
            {
                _navigationRunning = false;
                _navigationStatusText = null;
                _pendingNavigationTarget = null;
                _navigationCanceled = true;
                Repaint();
            }

            return;
        }

        if (_navigationRunning)
        {
            return;
        }

        _navigationRunId = NavigationAutoRunSession.RunId;
        _navigationCanceled = false;
        _navigationRunning = true;
        _navigationStatusText = "Restoring navigation to " + NavigationAutoRunSession.TargetName;
        EnsureNavigationTargetsLoaded(false);
        var target = new NavigationAutoRunOption
        {
            ViewId = NavigationAutoRunSession.TargetViewId,
            Name = NavigationAutoRunSession.TargetName,
            DisplayName = NavigationAutoRunSession.TargetName + " (" + NavigationAutoRunSession.TargetViewId + ")",
        };
        _pendingNavigationTarget = target;
        NavigationAutoRunPendingRunner.EnsureListening();
        LogNavigation("Restored pending UI state. target=" + target.ViewId + ", runId=" + _navigationRunId);
    }
}
