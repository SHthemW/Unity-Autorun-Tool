using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using FairyGUI;
using System.Collections.Generic;

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
        if (buttonText != AutoRunParam.DEFAULT_TEXT)
        {
            return $"err: button text not supported on FGUI. use button name instead!";
        }

        GComponent view = GRoot.inst.asCom;

        var allObjects = view.AllComponentChildren();

        var allButtons = allObjects.Where(c => c.asButton != null).Cast<GButton>();

        var nameMachedComponents = allButtons.Where(c => c.name == buttonName);
        if (nameMachedComponents.Count() == 0)
        {
            return $"err: button '{buttonName}' not found. view: {view.displayObject.name}, childlen: {allObjects.Count}";
        }
        if (nameMachedComponents.Count() > 1)
        {
            return $"err: button '{buttonName}' not unique!";
        }

        var selectedComponent = nameMachedComponents.First();

        var button = selectedComponent.asButton;
        if (button == null)
        {
            return $"err: button '{buttonName}' is not a button!";
        }
        
        button.onClick.Call();
        
        return $"btn {buttonName} is clicked.";
    }
}