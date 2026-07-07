using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AutoRunViewService
{
    public static List<string> ListOpenViewNames()
    {
        var names = new HashSet<string>();
        AddActiveGameObjectIdentifiers(names);
        AddActiveComponentIdentifiers(names);

        return names.OrderBy(name => name).ToList();
    }

    public static bool HasView(string viewIdOrName)
    {
        if (string.IsNullOrEmpty(viewIdOrName))
        {
            return false;
        }

        string target = NormalizeViewName(viewIdOrName);
        return ListOpenViewNames().Any(name => IsViewNameMatch(NormalizeViewName(name), target));
    }

    private static void AddViewCandidate(HashSet<string> names, string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
        {
            return;
        }

        names.Add(rawName);
        string normalized = NormalizeViewName(rawName);
        if (!string.IsNullOrEmpty(normalized))
        {
            names.Add(normalized);
        }
    }

    private static void AddActiveGameObjectIdentifiers(HashSet<string> names)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsRuntimeActive(candidate))
            {
                continue;
            }

            AddViewCandidate(names, candidate.name);
            AddViewCandidate(names, BuildHierarchyPath(candidate.transform));
        }
    }

    private static void AddActiveComponentIdentifiers(HashSet<string> names)
    {
        foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (component == null || !IsRuntimeActive(component.gameObject))
            {
                continue;
            }

            Type type = component.GetType();
            AddViewCandidate(names, type.Name);
            AddViewCandidate(names, type.FullName);
        }
    }

    private static bool IsRuntimeActive(GameObject gameObject)
    {
        return gameObject != null
            && gameObject.scene.IsValid()
            && gameObject.activeInHierarchy;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return null;
        }

        var parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string NormalizeViewName(string viewIdOrName)
    {
        string value = viewIdOrName.ToLowerInvariant().Replace("(clone)", string.Empty);
        if (value.StartsWith("view."))
        {
            value = value.Substring("view.".Length);
        }

        if (value.EndsWith(".prefab"))
        {
            value = value.Substring(0, value.Length - ".prefab".Length);
        }

        return value
            .Replace(".", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("/", string.Empty)
            .Replace("\\", string.Empty)
            .Replace(" ", string.Empty);
    }

    private static bool IsViewNameMatch(string candidate, string target)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
        {
            return false;
        }

        return candidate == target
            || (target.Length >= 4 && candidate.EndsWith(target))
            || (candidate.Length >= 4 && target.EndsWith(candidate));
    }
}
