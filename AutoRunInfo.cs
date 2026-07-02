using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class AutoRunParam
{
    public const string DEFAULT_NAME = "unnamed";
    public const string DEFAULT_TEXT = "untitled";

    public string buttonName = DEFAULT_NAME;

    public string buttonText = DEFAULT_TEXT;

    public bool isFairyGUI = false;

    public float delay = 0f;

    public bool isTest = false;
}

public enum HandlerStatus
{
    None = 0,
    Go = 1,
    Stop = 2,
}

[Serializable]
public sealed class AutoRunParamClassPair
{
    public string Key;
    public List<AutoRunParam> GoActionParams;
    public List<AutoRunParam> StopActionParams;
}

[Serializable]
public sealed class AutoRunParamConfig
{
    public readonly List<AutoRunParamClassPair> _classSeqDict = new();

    public string Info()
    {
        return $"{_classSeqDict.Count} classes, total "
             + $"{_classSeqDict.Sum(x => x.GoActionParams.Count)} go action params, "
             + $"{_classSeqDict.Sum(x => x.StopActionParams.Count)} stop action params. ";
    }

    public string[] GetClassNames()
    {
        return _classSeqDict.Select(x => x.Key.ToString()).ToArray();
    }

    public bool GetActions(string className, out List<AutoRunParam> goActions, out List<AutoRunParam> stopActions)
    {   
        var matches = _classSeqDict.FindAll(x => x.Key == className);

        if (matches.Count > 1)
        {
            AutoRunWindow.AppendBridgeConsoleText($"class amount not 1: {className}, {matches.Count}", AutoRunLogLevel.Error);
            goActions = null;
            stopActions = null;
            return false;
        }

        if (matches.Count == 0)
        {
            goActions = null;
            stopActions = null;
            return false;
        }

        goActions = matches[0].GoActionParams;
        stopActions = matches[0].StopActionParams;
        return true;
    }

    public void AppendAction(string className, AutoRunParam p, HandlerStatus moment)
    {
        if (!GetActions(className, out var goActions, out var stopActions))
        {
            throw new Exception($"Error: Action in class {nameof(className)} not found. Try create class first.");
        }

        if (moment == HandlerStatus.Go)
        {
            goActions.Add(p);
        }
        else if (moment == HandlerStatus.Stop)
        {
            stopActions.Add(p);
        }
    }

    public void AppendClass(string className)
    {
        _classSeqDict.Add(new AutoRunParamClassPair() {
            Key = className,
            GoActionParams = new List<AutoRunParam>(),
            StopActionParams = new List<AutoRunParam>(),
        });
    }
}
