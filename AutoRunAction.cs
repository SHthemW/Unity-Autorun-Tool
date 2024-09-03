using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using FairyGUI;

[System.Serializable]
public sealed class AutoRunAction
{
    const string DEFAULT_NAME = "unnamed";
    const string DEFAULT_TEXT = "untitled";

    public string buttonName = DEFAULT_NAME;
    public string buttonText = DEFAULT_TEXT;
    /// <summary>
    /// if true, it's a FGUI button
    /// </summary>
    public bool   isFairyGUI = false;
    public float  delay = 0f;
    public bool   isTest = false;

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
        if (buttonText != DEFAULT_TEXT)
        {
            return $"err: button text not supported on FGUI. use button name instead!";
        }

        GComponent view = GRoot.inst.asCom;

        var nameMachedComponents = AllChildren(view).Where(c => c.displayObject.name == buttonName);
        if (nameMachedComponents.Count() == 0)
        {
            return $"err: button '{buttonName}' not found. view: {view.displayObject.name}, childlen: {view.GetChildren().Length}";
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

    private static GObject[] AllChildren(GObject parent)
    {
        if (parent == null)
        {
            return null;
        }

        Debug.Log("err: msg: parent: " + parent.displayObject.name);

        if (parent is not GComponent && parent.asCom == null) 
        {
            return null;
        }

        var children = parent.asCom.GetChildren();

        foreach (var child in children)
        {
            var newChildren = AllChildren(child);
            if (newChildren == null)
            {
                continue;
            }
            children = children.Concat(newChildren).ToArray();
        }

        return children;
    }
}