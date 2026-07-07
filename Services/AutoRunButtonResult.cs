using System;
using System.Collections.Generic;

[Serializable]
public sealed class AutoRunButtonResult
{
    public bool ok;
    public string code;
    public string message;
    public List<AutoRunButtonInfo> buttons = new();

    public static AutoRunButtonResult Success(string message)
    {
        return new AutoRunButtonResult
        {
            ok = true,
            code = "ok",
            message = message,
        };
    }

    public static AutoRunButtonResult Fail(string code, string message)
    {
        return new AutoRunButtonResult
        {
            ok = false,
            code = code,
            message = message,
        };
    }
}
