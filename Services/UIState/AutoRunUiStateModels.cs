using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable]
public sealed class AutoRunUiStateProperty
{
    private const int MaximumReturnedStringLength = 2048;

    public string name;
    public string valueType;
    public string value;
    public bool sensitive;
    public bool masked;
    public bool truncated;
    public int originalLength;

    public string ComparableValue { get; private set; }

    public static AutoRunUiStateProperty String(
        string name,
        string value,
        bool sensitive = false,
        bool masked = false)
    {
        string rawValue = value ?? string.Empty;
        bool truncated = rawValue.Length > MaximumReturnedStringLength;
        return new AutoRunUiStateProperty
        {
            name = name,
            valueType = "string",
            value = truncated
                ? rawValue.Substring(0, MaximumReturnedStringLength)
                : rawValue,
            sensitive = sensitive,
            masked = masked,
            truncated = truncated,
            originalLength = rawValue.Length,
            ComparableValue = rawValue,
        };
    }

    public static AutoRunUiStateProperty Boolean(string name, bool value)
    {
        return new AutoRunUiStateProperty
        {
            name = name,
            valueType = "boolean",
            value = value ? "true" : "false",
            ComparableValue = value ? "true" : "false",
        };
    }

    public static AutoRunUiStateProperty Number(string name, float value)
    {
        return new AutoRunUiStateProperty
        {
            name = name,
            valueType = "number",
            value = value.ToString("R", CultureInfo.InvariantCulture),
            ComparableValue = value.ToString("R", CultureInfo.InvariantCulture),
        };
    }

    public static AutoRunUiStateProperty Integer(string name, int value)
    {
        return new AutoRunUiStateProperty
        {
            name = name,
            valueType = "integer",
            value = value.ToString(CultureInfo.InvariantCulture),
            ComparableValue = value.ToString(CultureInfo.InvariantCulture),
        };
    }
}

[Serializable]
public sealed class AutoRunUiElementState
{
    public string framework = "ugui";
    public string type;
    public string name;
    public string path;
    public string primaryProperty;
    public bool active;
    public bool enabled;
    public bool visible;
    public bool selectable;
    public bool interactable;
    public List<AutoRunUiStateProperty> properties = new();

    public AutoRunUiStateProperty FindProperty(string propertyName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(propertyName)
            || string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase)
                ? primaryProperty
                : propertyName;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return null;
        }

        switch (resolvedName.ToLowerInvariant())
        {
            case "active":
                return AutoRunUiStateProperty.Boolean("active", active);
            case "enabled":
                return AutoRunUiStateProperty.Boolean("enabled", enabled);
            case "visible":
                return AutoRunUiStateProperty.Boolean("visible", visible);
            case "selectable":
                return AutoRunUiStateProperty.Boolean("selectable", selectable);
            case "interactable":
                return AutoRunUiStateProperty.Boolean(
                    "interactable",
                    interactable);
        }

        return properties.Find(property =>
            property != null
            && string.Equals(
                property.name,
                resolvedName,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AutoRunUiStateQuery
{
    public string Query { get; set; }
    public string Scope { get; set; }
    public bool Exact { get; set; }
    public bool IncludeInactive { get; set; }
    public bool IncludeSensitive { get; set; }
    public int Limit { get; set; } = 100;
    public List<string> Types { get; set; } = new();
}

public sealed class AutoRunUiStateQueryResult
{
    public int Total { get; set; }
    public bool Truncated { get; set; }
    public List<AutoRunUiElementState> Elements { get; set; } = new();
    public List<AutoRunUiElementState> AllElements { get; set; } = new();
}

public sealed class AutoRunUiStateAssertionResult
{
    public bool Matched { get; set; }
    public string ActualValue { get; set; }
    public List<AutoRunUiElementState> MatchedElements { get; set; } = new();
}
