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

    public bool   isFairyGUI = false;

    public float  delay = 0f;

    public bool   isTest = false;
}

[Serializable]
public sealed class AutoRunParamClassPair
{
    public AutoRunActionClass Key;
    public List<AutoRunParam> Value;
}

[Serializable]
public sealed class AutoRunParamConfig
{
    public readonly List<AutoRunParamClassPair> _classSeqDict = new();

    public string Info()
    {
        return $"{_classSeqDict.Count} classes, total {_classSeqDict.Sum(x => x.Value.Count)} params";
    }

    public bool ParamsOf(AutoRunActionClass c, out List<AutoRunParam> result)
    {   
        var matches = _classSeqDict.FindAll(x => x.Key == c);

        if (matches.Count > 1)
        {
            Debug.LogError($"class amount not 1: {c}, {matches.Count}");
            result = null;
            return false;
        }

        if (matches.Count == 0)
        {
            result = null;
            return false;
        }

        result = matches[0].Value;
        return true;
    }

    public void Append(AutoRunActionClass actionClass, AutoRunParam p)
    {
        if (!ParamsOf(actionClass, out _))
        {
            _classSeqDict.Add(new AutoRunParamClassPair() {
                Key = actionClass,
                Value = new List<AutoRunParam>()
            });
        }

        if (!ParamsOf(actionClass, out var appendTarget))
        {
            throw new Exception($"{nameof(appendTarget)} still not found, althouth we tried to append new.");
        }

        appendTarget.Add(p);
    }
}

[Serializable]
public enum AutoRunActionClass
{
    NeverLand,
    AvatarStore,
}