using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class AutoRunParam
{
    public const string DEFAULT_NAME = "unnamed";
    public const string DEFAULT_TEXT = "untitled";

    [field: SerializeField]
    public string buttonName { get; set; } = DEFAULT_NAME;

    [field: SerializeField]
    public string buttonText { get; set; } = DEFAULT_TEXT;

    [field: SerializeField]
    public bool   isFairyGUI { get; set; } = false;

    [field: SerializeField]
    public float  delay { get; set; } = 0f;

    [field: SerializeField]
    public bool   isTest { get; set; } = false;
}

[Serializable]
public sealed class AutoRunParamConfig
{
    private readonly Dictionary<AutoRunActionClass, List<AutoRunParam>> _classSeqDict = new();

    public string Info()
    {
        return $"{_classSeqDict.Count} classes, total {_classSeqDict.Sum(x => x.Value.Count)} params";
    }

    public bool ParamsOf(AutoRunActionClass c, out List<AutoRunParam> result)
    {
        bool success = _classSeqDict.TryGetValue(c, out var r);
        result = r;
        return success;
    }

    public void Append(AutoRunActionClass c, AutoRunParam p)
    {
        if (!_classSeqDict.ContainsKey(c))
        {
            _classSeqDict.Add(c, new List<AutoRunParam>());
        }

        _classSeqDict[c].Add(p);
    }
}

public enum AutoRunActionClass
{
    NeverLand,
    AvatarStore,
}