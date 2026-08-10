using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AutoRunViewService
{
    private const double CacheDurationSeconds = 0.5;
    private static List<string> _cachedNames;
    private static Dictionary<string, List<GameObject>> _cachedRoots =
        new Dictionary<string, List<GameObject>>();
    private static double _cachedAt = -1;

    public static List<string> ListOpenViewNames()
    {
        return new List<string>(GetOpenViewNames());
    }

    public static List<string> ListOpenViewNames(string query, bool exact, int limit, out int total)
    {
        IEnumerable<string> names = GetOpenViewNames();
        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalizedQuery = NormalizeViewName(query);
            names = exact
                ? names.Where(name => IsViewNameMatch(NormalizeViewName(name), normalizedQuery))
                : names.Where(name => name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || NormalizeViewName(name).Contains(normalizedQuery));
        }

        List<string> matches = names.ToList();
        total = matches.Count;
        int safeLimit = Mathf.Clamp(limit <= 0 ? 100 : limit, 1, 10000);
        return matches.Take(safeLimit).ToList();
    }

    public static List<string> ListOpenViewMatches(string viewIdOrName, int limit, out int total)
    {
        return ListOpenViewNames(viewIdOrName, true, limit, out total);
    }

    private static IReadOnlyList<string> GetOpenViewNames()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cachedNames != null && now - _cachedAt < CacheDurationSeconds)
        {
            return _cachedNames;
        }

        var names = new HashSet<string>();
        var roots = new Dictionary<string, List<GameObject>>();
        AddActiveGameObjectIdentifiers(names, roots);
        AddActiveComponentIdentifiers(names, roots);

        _cachedNames = names.OrderBy(name => name).ToList();
        _cachedRoots = roots;
        _cachedAt = EditorApplication.timeSinceStartup;
        return _cachedNames;
    }

    public static bool HasView(string viewIdOrName)
    {
        if (string.IsNullOrEmpty(viewIdOrName))
        {
            return false;
        }

        string target = NormalizeViewName(viewIdOrName);
        return GetOpenViewNames().Any(name => IsViewNameMatch(NormalizeViewName(name), target));
    }

    public static bool IsViewForeground(
        string viewIdOrName,
        out string detail)
    {
        List<GameObject> roots = GetActiveViewRoots(viewIdOrName);
        if (roots.Count == 0)
        {
            detail = "view root is not active.";
            return false;
        }

        bool assessed = false;
        string blockedDetail = null;
        foreach (GameObject root in roots)
        {
            bool rootAssessed;
            string rootDetail;
            if (AutoRunPointerService.IsViewForeground(
                    root,
                    out rootAssessed,
                    out rootDetail))
            {
                detail = rootDetail;
                return true;
            }

            assessed |= rootAssessed;
            if (rootAssessed && blockedDetail == null)
            {
                blockedDetail = rootDetail;
            }
        }

        if (!assessed)
        {
            detail = "view is active; foreground could not be verified because it has no raycastable uGUI surface.";
            return true;
        }

        detail = blockedDetail
            ?? "view has no foreground pointer target.";
        return false;
    }

    private static List<GameObject> GetActiveViewRoots(
        string viewIdOrName)
    {
        if (string.IsNullOrWhiteSpace(viewIdOrName))
        {
            return new List<GameObject>();
        }

        GetOpenViewNames();
        string target = NormalizeViewName(viewIdOrName);
        return _cachedRoots
            .Where(pair => IsViewNameMatch(pair.Key, target))
            .SelectMany(pair => pair.Value)
            .Where(IsRuntimeActive)
            .Distinct()
            .ToList();
    }

    private static void AddViewCandidate(
        HashSet<string> names,
        Dictionary<string, List<GameObject>> roots,
        string rawName,
        GameObject root)
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

        if (root == null || string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (!roots.TryGetValue(
                normalized,
                out List<GameObject> matches))
        {
            matches = new List<GameObject>();
            roots[normalized] = matches;
        }

        if (!matches.Contains(root))
        {
            matches.Add(root);
        }
    }

    private static void AddActiveGameObjectIdentifiers(
        HashSet<string> names,
        Dictionary<string, List<GameObject>> roots)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsRuntimeActive(candidate))
            {
                continue;
            }

            AddViewCandidate(
                names,
                roots,
                candidate.name,
                candidate);
            AddViewCandidate(
                names,
                roots,
                BuildHierarchyPath(candidate.transform),
                candidate);
        }
    }

    private static void AddActiveComponentIdentifiers(
        HashSet<string> names,
        Dictionary<string, List<GameObject>> roots)
    {
        foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (component == null || !IsRuntimeActive(component.gameObject))
            {
                continue;
            }

            Type type = component.GetType();
            AddViewCandidate(
                names,
                roots,
                type.Name,
                component.gameObject);
            AddViewCandidate(
                names,
                roots,
                type.FullName,
                component.gameObject);
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
