using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class AutoRunUGuiStateAdapter
{
    private const string RedactedValue = "[REDACTED]";

    public static List<AutoRunUiElementState> Read(
        bool includeInactive,
        bool includeSensitive)
    {
        var elements = new List<AutoRunUiElementState>();
        AddComponents<Text>(elements, includeInactive, BuildText);
        AddComponents<InputField>(elements, includeInactive, input =>
            BuildInputField(input, includeSensitive));
        AddComponents<Toggle>(elements, includeInactive, BuildToggle);
        AddComponents<Slider>(elements, includeInactive, BuildSlider);
        AddComponents<Dropdown>(elements, includeInactive, BuildDropdown);
        AddComponents<Scrollbar>(elements, includeInactive, BuildScrollbar);
        AddComponents<ScrollRect>(elements, includeInactive, BuildScrollRect);
        AddComponents<Button>(elements, includeInactive, BuildButton);
        AddComponents<Image>(elements, includeInactive, BuildImage);
        AddComponents<RawImage>(elements, includeInactive, BuildRawImage);
        return elements;
    }

    private static void AddComponents<T>(
        List<AutoRunUiElementState> elements,
        bool includeInactive,
        Func<T, AutoRunUiElementState> factory)
        where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (!AutoRunUiStateAdapterUtility.IsRuntimeComponent(
                    component,
                    includeInactive))
            {
                continue;
            }

            try
            {
                AutoRunUiElementState element = factory(component);
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            catch (Exception)
            {
                // A UI object may be destroyed while the runtime hierarchy is read.
            }
        }
    }

    private static AutoRunUiElementState BuildText(Text text)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            text,
            "Text",
            "text");
        element.properties.Add(AutoRunUiStateProperty.String("text", text.text));
        return element;
    }

    private static AutoRunUiElementState BuildInputField(
        InputField input,
        bool includeSensitive)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            input,
            "InputField",
            "text");
        bool sensitive = input.contentType == InputField.ContentType.Password
            || input.contentType == InputField.ContentType.Pin
            || input.inputType == InputField.InputType.Password;
        bool masked = sensitive && !includeSensitive;
        string value = masked ? RedactedValue : input.text;
        element.properties.Add(AutoRunUiStateProperty.String(
            "text",
            value,
            sensitive,
            masked));
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "hasValue",
            !string.IsNullOrEmpty(input.text)));
        if (!masked)
        {
            element.properties.Add(AutoRunUiStateProperty.Integer(
                "textLength",
                input.text?.Length ?? 0));
        }

        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "isFocused",
            input.isFocused));
        element.properties.Add(AutoRunUiStateProperty.String(
            "contentType",
            input.contentType.ToString()));
        if (input.placeholder != null)
        {
            element.properties.Add(AutoRunUiStateProperty.String(
                "placeholder",
                AutoRunUiStateAdapterUtility.ResolveTextObject(input.placeholder)));
        }

        return element;
    }

    private static AutoRunUiElementState BuildToggle(Toggle toggle)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            toggle,
            "Toggle",
            "isOn");
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "isOn",
            toggle.isOn));
        element.properties.Add(AutoRunUiStateProperty.String(
            "text",
            AutoRunUiStateAdapterUtility.ResolveChildText(toggle)));
        return element;
    }

    private static AutoRunUiElementState BuildSlider(Slider slider)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            slider,
            "Slider",
            "value");
        element.properties.Add(AutoRunUiStateProperty.Number("value", slider.value));
        element.properties.Add(AutoRunUiStateProperty.Number(
            "normalizedValue",
            slider.normalizedValue));
        element.properties.Add(AutoRunUiStateProperty.Number("minValue", slider.minValue));
        element.properties.Add(AutoRunUiStateProperty.Number("maxValue", slider.maxValue));
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "wholeNumbers",
            slider.wholeNumbers));
        return element;
    }

    private static AutoRunUiElementState BuildDropdown(Dropdown dropdown)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            dropdown,
            "Dropdown",
            "selectedText");
        int selectedIndex = dropdown.value;
        string optionText = selectedIndex >= 0
            && selectedIndex < dropdown.options.Count
                ? dropdown.options[selectedIndex].text
                : string.Empty;
        string selectedText = dropdown.captionText != null
            ? dropdown.captionText.text
            : optionText;
        element.properties.Add(AutoRunUiStateProperty.Integer(
            "selectedIndex",
            selectedIndex));
        element.properties.Add(AutoRunUiStateProperty.String(
            "selectedText",
            selectedText));
        element.properties.Add(AutoRunUiStateProperty.String(
            "optionText",
            optionText));
        element.properties.Add(AutoRunUiStateProperty.Integer(
            "optionCount",
            dropdown.options.Count));
        return element;
    }

    private static AutoRunUiElementState BuildScrollbar(Scrollbar scrollbar)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            scrollbar,
            "Scrollbar",
            "value");
        element.properties.Add(AutoRunUiStateProperty.Number("value", scrollbar.value));
        element.properties.Add(AutoRunUiStateProperty.Number("size", scrollbar.size));
        element.properties.Add(AutoRunUiStateProperty.Integer(
            "numberOfSteps",
            scrollbar.numberOfSteps));
        return element;
    }

    private static AutoRunUiElementState BuildScrollRect(ScrollRect scrollRect)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            scrollRect,
            "ScrollRect",
            "verticalNormalizedPosition");
        element.properties.Add(AutoRunUiStateProperty.Number(
            "horizontalNormalizedPosition",
            scrollRect.horizontalNormalizedPosition));
        element.properties.Add(AutoRunUiStateProperty.Number(
            "verticalNormalizedPosition",
            scrollRect.verticalNormalizedPosition));
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "horizontal",
            scrollRect.horizontal));
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "vertical",
            scrollRect.vertical));
        return element;
    }

    private static AutoRunUiElementState BuildButton(Button button)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            button,
            "Button",
            "text");
        element.properties.Add(AutoRunUiStateProperty.String(
            "text",
            AutoRunUiStateAdapterUtility.ResolveChildText(button)));
        return element;
    }

    private static AutoRunUiElementState BuildImage(Image image)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            image,
            "Image",
            "sprite");
        element.properties.Add(AutoRunUiStateProperty.String(
            "sprite",
            ResolveDisplayedSpriteName(image)));
        element.properties.Add(AutoRunUiStateProperty.String(
            "imageType",
            image.type.ToString()));
        if (image.type == Image.Type.Filled)
        {
            element.properties.Add(AutoRunUiStateProperty.Number(
                "fillAmount",
                image.fillAmount));
        }

        return element;
    }

    private static string ResolveDisplayedSpriteName(Image image)
    {
        Sprite sprite = image.overrideSprite != null
            ? image.overrideSprite
            : image.sprite;
        return sprite != null ? sprite.name : string.Empty;
    }

    private static AutoRunUiElementState BuildRawImage(RawImage image)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            image,
            "RawImage",
            "texture");
        element.properties.Add(AutoRunUiStateProperty.String(
            "texture",
            image.texture != null ? image.texture.name : string.Empty));
        return element;
    }
}
