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
    public string query;
    public string navigationId;
    public string targetViewId;
    public string routeId;
    public bool exact;
    public bool namesOnly;
    public bool ensurePlayMode = true;
    public int limit = 100;
    public int waitMilliseconds;
    public int pollMilliseconds;
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
    public List<string> buttonNames = new();
    public List<string> openViews = new();
    public List<string> messages = new();
    public int buttonCount;
    public int openViewCount;
    public bool truncated;
    public bool viewOpen;
    public string navigationId;
    public string navigationStatus;
    public string navigationPhase;
    public string targetViewId;
    public string routeId;
    public string resultCode;
    public string resultMessage;
    public bool terminal;
    public long elapsedMilliseconds;
}

[Serializable]
public sealed class AutoRunNavStep
{
    public string transitionId;
    public string controlId;
    public string fromViewId;
    public string toViewId;
    public string kind;
    public string mode;
    public bool isAutoRunnable;
    public AutoRunParam action;
    public string waitForViewId;
    public float timeout = 15f;
}
