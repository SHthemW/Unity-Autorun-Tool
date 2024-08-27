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

        var btn = GameObject.Find(buttonName);
        if (!btn)
            return $"err: button '{buttonName}' not found!";

        btn.GetComponent<Button>().onClick?.Invoke();

        return $"btn {buttonName} is clicked.";
    }
}