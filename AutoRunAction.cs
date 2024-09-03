using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public sealed class AutoRunAction
{
    public string buttonName = "button name";
    public float delay = 0f;
    public bool isTest = true;

    public string Execute()
    {
        if (isTest)
        {
            return $"testing {buttonName}.";
        }

        var btnObject = GameObject.Find(buttonName);
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

        return $"btn {buttonName} is clicked.";
    }
}