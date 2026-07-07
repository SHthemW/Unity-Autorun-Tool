using System;
using System.Threading;
using UnityEngine;

public static class NavigationAutoRunRequest
{
    public static void Start(NavigationAutoRunPlan plan, Action<AutoRunBridgeResponse> onCompleted)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        SynchronizationContext context = SynchronizationContext.Current;
        if (plan.Steps == null || plan.Steps.Count == 0)
        {
            bool alreadyAtTarget = !string.IsNullOrEmpty(plan.RouteId)
                && plan.RouteId.StartsWith("already.", StringComparison.Ordinal);
            string responseId = "window-nav-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AutoRunBridgeResponse response = alreadyAtTarget
                ? AutoRunBridgeResponses.Success(responseId, "Already at navigation target: " + plan.ToViewId)
                : AutoRunBridgeResponses.Fail(responseId, "navigation_empty_route", "Navigation route has no steps: " + plan.RouteId);
            response.data.openViews = AutoRunViewService.ListOpenViewNames();

            PostLog(context, "Navigation AutoRun completed without bridge request: route="
                + plan.RouteId + ", target=" + plan.ToViewId);
            if (context != null)
            {
                context.Post(_ => onCompleted?.Invoke(response), null);
                return;
            }

            UnityEditor.EditorApplication.delayCall += () => onCompleted?.Invoke(response);
            return;
        }

        AutoRunBridgeController.Start();
        var request = new AutoRunBridgeRequest
        {
            id = "window-nav-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            command = "navigate_route",
            payload = new AutoRunBridgePayload
            {
                routeId = plan.RouteId,
                targetViewId = plan.ToViewId,
                navigationSteps = plan.Steps,
            },
        };
        string json = JsonUtility.ToJson(request);
        NavigationAutoRunSession.MarkActiveRequest(plan);
        PostLog(context, "Navigation AutoRun request queued: " + request.id
            + ", route=" + plan.RouteId
            + ", steps=" + NavigationAutoRunLog.FormatSteps(plan.Steps));

        ThreadPool.QueueUserWorkItem(_ =>
        {
            AutoRunBridgeResponse response;
            try
            {
                PostLog(context, "Navigation AutoRun request sending: " + request.id);
                response = AutoRunBridgeController.Enqueue(json);
                PostLog(context, "Navigation AutoRun request response: " + response.code + ", " + response.message,
                    response.ok ? AutoRunLogLevel.Info : AutoRunLogLevel.Error);
            }
            catch (Exception ex)
            {
                response = AutoRunBridgeResponses.Fail(request.id, "navigation_request_error", ex.Message);
                PostLog(context, "Navigation AutoRun request exception: " + ex.Message, AutoRunLogLevel.Error);
            }

            if (context != null)
            {
                context.Post(__ => Complete(response, onCompleted), null);
                return;
            }

            UnityEditor.EditorApplication.delayCall += () => Complete(response, onCompleted);
        });
    }

    private static void Complete(AutoRunBridgeResponse response, Action<AutoRunBridgeResponse> onCompleted)
    {
        NavigationAutoRunSession.ClearActiveRequest();
        onCompleted?.Invoke(response);
    }

    private static void PostLog(SynchronizationContext context, string message, AutoRunLogLevel level = AutoRunLogLevel.Debug)
    {
        if (context != null)
        {
            context.Post(_ => AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message, level), null);
            return;
        }

        UnityEditor.EditorApplication.delayCall += () => AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message, level);
    }
}
