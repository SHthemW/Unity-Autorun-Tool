public static class AutoRunBridgeResponses
{
    public static AutoRunBridgeResponse Success(
        string id,
        string message,
        AutoRunBridgeData data = null
    )
    {
        return new AutoRunBridgeResponse
        {
            id = id,
            ok = true,
            code = "ok",
            message = message,
            data = data ?? new AutoRunBridgeData(),
        };
    }

    public static AutoRunBridgeResponse Fail(string id, string code, string message)
    {
        return new AutoRunBridgeResponse
        {
            id = id,
            ok = false,
            code = code,
            message = message,
            data = new AutoRunBridgeData(),
        };
    }

    public static AutoRunBridgeResponse FromButtonResult(string id, AutoRunButtonResult result)
    {
        return new AutoRunBridgeResponse
        {
            id = id,
            ok = result.ok,
            code = result.code,
            message = result.message,
            data = new AutoRunBridgeData
            {
                buttons = result.buttons,
            },
        };
    }
}
