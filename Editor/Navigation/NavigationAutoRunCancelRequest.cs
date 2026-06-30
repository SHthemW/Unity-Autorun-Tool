using System;
using System.Threading;
using UnityEngine;

public static class NavigationAutoRunCancelRequest
{
    public static void Start(Action<AutoRunBridgeResponse> onCompleted)
    {
        SynchronizationContext context = SynchronizationContext.Current;
        var request = new AutoRunBridgeRequest
        {
            id = "window-nav-cancel-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            command = "cancel_navigation",
            payload = new AutoRunBridgePayload(),
        };
        string json = JsonUtility.ToJson(request);
        PostLog(context, "Navigation AutoRun cancel queued: " + request.id);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            AutoRunBridgeResponse response;
            try
            {
                PostLog(context, "Navigation AutoRun cancel sending: " + request.id);
                response = AutoRunBridgeController.Enqueue(json);
                PostLog(context, "Navigation AutoRun cancel response: " + response.code + ", " + response.message);
            }
            catch (Exception ex)
            {
                response = AutoRunBridgeResponses.Fail(request.id, "navigation_cancel_error", ex.Message);
                PostLog(context, "Navigation AutoRun cancel exception: " + ex.Message);
            }

            if (context != null)
            {
                context.Post(__ => onCompleted?.Invoke(response), null);
                return;
            }

            UnityEditor.EditorApplication.delayCall += () => onCompleted?.Invoke(response);
        });
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
