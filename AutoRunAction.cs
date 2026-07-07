[System.Serializable]
public sealed class AutoRunAction
{
    public AutoRunParam Param { get; set; }

    public string buttonName { get => Param.buttonName; set => Param.buttonName = value; }
    public string buttonText { get => Param.buttonText; set => Param.buttonText = value; }
    public float delay { get => Param.delay; set => Param.delay = value; }
    public bool isTest { get => Param.isTest; set => Param.isTest = value; }
    public bool isFairyGUI { get => Param.isFairyGUI; set => Param.isFairyGUI = value; }

    public AutoRunAction(AutoRunParam param)
    {
        Param = param;
    }

    public string Execute()
    {
        if (isTest)
        {
            return $"testing {buttonName}.";
        }

        return AutoRunButtonService.Click(Param).message;
    }
}
