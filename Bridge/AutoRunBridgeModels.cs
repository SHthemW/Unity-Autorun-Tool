using System;
using System.Collections.Generic;

[Serializable]
public sealed class AutoRunBridgeRequest
{
    public string id;
    public string command;
    public AutoRunBridgePayload payload = new();
}

[Serializable]
public sealed class AutoRunBridgePayload
{
    public string name;
    public string text;
    public string framework;
    public string targetViewId;
    public string routeId;
    public List<AutoRunParam> actions = new();
    public List<AutoRunNavStep> navigationSteps = new();
}

[Serializable]
public sealed class AutoRunBridgeResponse
{
    public string id;
    public bool ok;
    public string code;
    public string message;
    public AutoRunBridgeData data = new();
}

[Serializable]
public sealed class AutoRunBridgeData
{
    public bool isPlaying;
    public string unityVersion;
    public string bridgeVersion;
    public string framework;
    public List<AutoRunButtonInfo> buttons = new();
    public List<string> openViews = new();
    public List<string> messages = new();
}

[Serializable]
public sealed class AutoRunNavStep
{
    public string transitionId;
    public string fromViewId;
    public string toViewId;
    public string kind;
    public string mode;
    public bool isAutoRunnable;
    public AutoRunParam action;
    public string waitForViewId;
    public float timeout = 10f;
}
