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

        AutoRunBridgeController.Start();
        SynchronizationContext context = SynchronizationContext.Current;
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
        NavigationAutoRunSession.MarkActiveRequest();
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
                PostLog(context, "Navigation AutoRun request response: " + response.code + ", " + response.message);
            }
            catch (Exception ex)
            {
                response = AutoRunBridgeResponses.Fail(request.id, "navigation_request_error", ex.Message);
                PostLog(context, "Navigation AutoRun request exception: " + ex.Message);
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

    private static void PostLog(SynchronizationContext context, string message)
    {
        if (context != null)
        {
            context.Post(_ => AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message), null);
            return;
        }

        UnityEditor.EditorApplication.delayCall += () => AutoRunWindow.AppendBridgeConsoleText("[Navigation AutoRun] " + message);
    }
}
