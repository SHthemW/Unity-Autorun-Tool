using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AutoRunTmpUiStateAdapter
{
    private const string RedactedValue = "[REDACTED]";

    public static List<AutoRunUiElementState> Read(
        bool includeInactive,
        bool includeSensitive)
    {
        var elements = new List<AutoRunUiElementState>();
        AddComponents(
            elements,
            "TMPro.TMP_Text",
            includeInactive,
            BuildText);
        AddComponents(
            elements,
            "TMPro.TMP_InputField",
            includeInactive,
            component => BuildInputField(component, includeSensitive));
        AddComponents(
            elements,
            "TMPro.TMP_Dropdown",
            includeInactive,
            BuildDropdown);
        return elements;
    }

    private static void AddComponents(
        List<AutoRunUiElementState> elements,
        string typeName,
        bool includeInactive,
        Func<Component, AutoRunUiElementState> factory)
    {
        Type type = AutoRunUiStateAdapterUtility.FindType(typeName);
        if (type == null)
        {
            return;
        }

        foreach (UnityEngine.Object candidate in Resources.FindObjectsOfTypeAll(type))
        {
            Component component = candidate as Component;
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
                // Optional TMP components can disappear while the hierarchy is read.
            }
        }
    }

    private static AutoRunUiElementState BuildText(Component text)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            text,
            "TMP_Text",
            "text");
        element.properties.Add(AutoRunUiStateProperty.String(
            "text",
            AutoRunUiStateAdapterUtility.ReadMember(text, "text") as string));
        return element;
    }

    private static AutoRunUiElementState BuildInputField(
        Component input,
        bool includeSensitive)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            input,
            "TMP_InputField",
            "text");
        string text = AutoRunUiStateAdapterUtility.ReadMember(input, "text") as string
            ?? string.Empty;
        string contentType = AutoRunUiStateAdapterUtility.ReadMember(
                input,
                "contentType")
                ?.ToString()
            ?? string.Empty;
        string inputType = AutoRunUiStateAdapterUtility.ReadMember(
                input,
                "inputType")
                ?.ToString()
            ?? string.Empty;
        bool sensitive = contentType.IndexOf(
                "Password",
                StringComparison.OrdinalIgnoreCase) >= 0
            || contentType.IndexOf(
                "Pin",
                StringComparison.OrdinalIgnoreCase) >= 0
            || inputType.IndexOf(
                "Password",
                StringComparison.OrdinalIgnoreCase) >= 0;
        bool masked = sensitive && !includeSensitive;
        element.properties.Add(AutoRunUiStateProperty.String(
            "text",
            masked ? RedactedValue : text,
            sensitive,
            masked));
        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "hasValue",
            !string.IsNullOrEmpty(text)));
        if (!masked)
        {
            element.properties.Add(AutoRunUiStateProperty.Integer(
                "textLength",
                text.Length));
        }

        element.properties.Add(AutoRunUiStateProperty.Boolean(
            "isFocused",
            AutoRunUiStateAdapterUtility.ReadBool(input, "isFocused")));
        element.properties.Add(AutoRunUiStateProperty.String(
            "contentType",
            contentType));
        object placeholder = AutoRunUiStateAdapterUtility.ReadMember(
            input,
            "placeholder");
        if (placeholder != null)
        {
            element.properties.Add(AutoRunUiStateProperty.String(
                "placeholder",
                AutoRunUiStateAdapterUtility.ResolveTextObject(placeholder)));
        }

        return element;
    }

    private static AutoRunUiElementState BuildDropdown(Component dropdown)
    {
        AutoRunUiElementState element = AutoRunUiStateAdapterUtility.CreateElement(
            dropdown,
            "TMP_Dropdown",
            "selectedText");
        int selectedIndex = AutoRunUiStateAdapterUtility.ReadInt(
            dropdown,
            "value");
        IList options = AutoRunUiStateAdapterUtility.ReadMember(
            dropdown,
            "options") as IList;
        string optionText = string.Empty;
        if (options != null
            && selectedIndex >= 0
            && selectedIndex < options.Count)
        {
            optionText = AutoRunUiStateAdapterUtility.ReadMember(
                    options[selectedIndex],
                    "text") as string
                ?? string.Empty;
        }

        object captionText = AutoRunUiStateAdapterUtility.ReadMember(
            dropdown,
            "captionText");
        string selectedText = captionText != null
            ? AutoRunUiStateAdapterUtility.ResolveTextObject(captionText)
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
            options?.Count ?? 0));
        return element;
    }
}
