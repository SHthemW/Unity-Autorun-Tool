using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class AutoRunUiStateAdapterUtility
{
    public static bool IsRuntimeComponent(
        Component component,
        bool includeInactive)
    {
        return component != null
            && component.gameObject != null
            && component.gameObject.scene.IsValid()
            && (includeInactive || component.gameObject.activeInHierarchy);
    }

    public static AutoRunUiElementState CreateElement(
        Component component,
        string type,
        string primaryProperty)
    {
        Behaviour behaviour = component as Behaviour;
        Selectable selectable = component as Selectable;
        bool active = component.gameObject.activeInHierarchy;
        bool enabled = behaviour == null || behaviour.enabled;
        return new AutoRunUiElementState
        {
            type = type,
            name = component.gameObject.name,
            path = GetHierarchyPath(component.transform),
            primaryProperty = primaryProperty,
            active = active,
            enabled = enabled,
            visible = IsVisible(component, active, enabled),
            selectable = selectable != null,
            interactable = selectable != null
                && active
                && selectable.enabled
                && selectable.IsInteractable(),
        };
    }

    public static string GetHierarchyPath(Transform transform)
    {
        var segments = new List<string>();
        for (Transform current = transform;
            current != null;
            current = current.parent)
        {
            segments.Add(current.name);
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    public static string ResolveChildText(Component owner)
    {
        if (owner == null)
        {
            return string.Empty;
        }

        Text text = owner.GetComponentsInChildren<Text>(true)
            .FirstOrDefault(candidate => candidate != null);
        if (text != null)
        {
            return text.text ?? string.Empty;
        }

        Component richText = owner.GetComponentsInChildren<Component>(true)
            .FirstOrDefault(candidate =>
                candidate != null
                && IsTypeOrBase(candidate.GetType(), "TMPro.TMP_Text"));
        return ReadMember(richText, "text") as string ?? string.Empty;
    }

    public static string ResolveTextObject(object target)
    {
        if (target is Text text)
        {
            return text.text ?? string.Empty;
        }

        return ReadMember(target, "text") as string ?? string.Empty;
    }

    public static Type FindType(string fullName)
    {
        return Type.GetType(fullName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
    }

    public static object ReadMember(object target, string memberName)
    {
        if (target == null)
        {
            return null;
        }

        try
        {
            Type type = target.GetType();
            return type.GetProperty(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(target, null)
                ?? type.GetField(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(target);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int ReadInt(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        try
        {
            return value == null ? 0 : Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static float ReadFloat(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        try
        {
            return value == null ? 0f : Convert.ToSingle(value);
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    public static bool ReadBool(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        try
        {
            return value != null && Convert.ToBoolean(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsTypeOrBase(Type type, string fullName)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            if (string.Equals(
                    current.FullName,
                    fullName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisible(
        Component component,
        bool active,
        bool enabled)
    {
        if (!active || !AreCanvasGroupsVisible(component.transform))
        {
            return false;
        }

        Canvas canvas = component.GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.enabled)
        {
            return false;
        }

        Graphic graphic = component as Graphic;
        if (graphic == null && component is Selectable selectable)
        {
            graphic = selectable.targetGraphic;
        }

        if (graphic == null)
        {
            graphic = component.GetComponent<Graphic>();
        }

        if (graphic == null)
        {
            return enabled;
        }

        return graphic.enabled
            && graphic.color.a > 0.001f
            && (graphic.canvasRenderer == null || !graphic.canvasRenderer.cull);
    }

    private static bool AreCanvasGroupsVisible(Transform transform)
    {
        for (Transform current = transform;
            current != null;
            current = current.parent)
        {
            CanvasGroup[] groups = current.GetComponents<CanvasGroup>();
            bool ignoreParents = false;
            foreach (CanvasGroup group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                if (group.alpha <= 0.001f)
                {
                    return false;
                }

                ignoreParents |= group.ignoreParentGroups;
            }

            if (ignoreParents)
            {
                break;
            }
        }

        return true;
    }
}
