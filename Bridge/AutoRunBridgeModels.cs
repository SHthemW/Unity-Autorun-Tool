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
    public List<AutoRunParam> actions = new();
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
    public List<string> messages = new();
}
