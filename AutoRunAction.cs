using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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

        return isFairyGUI ? ExecuteWithFGUI() : ExecuteWithUGUI();
    }

    private string ExecuteWithUGUI()
    {
        var allButtons = Object.FindObjectsOfType<Button>();

        var nameMachedBtns = allButtons.Where(b => b.name == buttonName);
        var textMachedBtns = allButtons.Where(b => b.GetComponentInChildren<Text>().text == buttonText);

        var btnObject = (nameMachedBtns.Count(), textMachedBtns.Count()) switch
        {
            (1, _) => nameMachedBtns.First(),
            (_, 1) => textMachedBtns.First(),
            _ => null
        };

        if (!btnObject)
        {
            return $"err: button '{buttonName}' not found!";
        }

        var btnComponent = btnObject.GetComponent<Button>();
        if (!btnComponent)
        {
            return $"err: button '{buttonName}' has no button component!";
        }

        var btnClickAction = btnComponent.onClick;
        if (btnClickAction == null || btnClickAction.GetPersistentEventCount() == 0)
        {
            return $"err: button '{buttonName}' has no button click event!";
        }

        btnClickAction.Invoke();

        return $"btn {buttonName} is clicked. Text: {btnObject.GetComponentInChildren<Text>().text}";
    }

    private string ExecuteWithFGUI()
    {
        return FairyGUIHelper.ClickButton(buttonName, buttonText);
    }
}
