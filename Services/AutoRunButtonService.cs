using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class AutoRunButtonService
{
    public static List<AutoRunButtonInfo> ListButtons(string framework)
    {
        var buttons = new List<AutoRunButtonInfo>();
        if (ShouldIncludeFramework(framework, "ugui"))
        {
            buttons.AddRange(ListUGUIButtons());
        }

        if (ShouldIncludeFramework(framework, "fairygui"))
        {
            buttons.AddRange(FairyGUIHelper.ListButtons());
        }

        return buttons;
    }

    public static AutoRunButtonResult Click(AutoRunParam param)
    {
        return param.isFairyGUI ? ClickFairyGUI(param) : ClickUGUI(param);
    }

    private static AutoRunButtonResult ClickUGUI(AutoRunParam param)
    {
        Button[] allButtons = Object.FindObjectsOfType<Button>();
        var nameMatchedBtns = allButtons.Where(b => b.name == param.buttonName).ToList();
        var textMatchedBtns = allButtons
            .Where(b => GetButtonText(b) == param.buttonText)
            .ToList();

        Button btnObject = (nameMatchedBtns.Count, textMatchedBtns.Count) switch
        {
            (1, _) => nameMatchedBtns.First(),
            (_, 1) => textMatchedBtns.First(),
            _ => null
        };

        if (!btnObject)
        {
            return AutoRunButtonResult.Fail("button_not_found", $"err: button '{param.buttonName}' not found!");
        }

        if (btnObject.onClick == null || btnObject.onClick.GetPersistentEventCount() == 0)
        {
            return AutoRunButtonResult.Fail("no_click_event", $"err: button '{param.buttonName}' has no button click event!");
        }

        btnObject.onClick.Invoke();
        return AutoRunButtonResult.Success($"btn {param.buttonName} is clicked. Text: {GetButtonText(btnObject)}");
    }

    private static AutoRunButtonResult ClickFairyGUI(AutoRunParam param)
    {
        return FairyGUIHelper.ClickButton(param.buttonName, param.buttonText);
    }

    private static List<AutoRunButtonInfo> ListUGUIButtons()
    {
        return Object.FindObjectsOfType<Button>()
            .Select(button => new AutoRunButtonInfo
            {
                name = button.name,
                text = GetButtonText(button),
                framework = "ugui",
                interactable = button.interactable,
            })
            .ToList();
    }

    private static string GetButtonText(Button button)
    {
        return button.GetComponentInChildren<Text>()?.text;
    }

    private static bool ShouldIncludeFramework(string requested, string framework)
    {
        return string.IsNullOrEmpty(requested)
            || requested == "all"
            || requested.ToLowerInvariant() == framework;
    }
}
