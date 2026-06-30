using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityGameFramework.Runtime;

public static class AutoRunViewService
{
    public static List<string> ListOpenViewNames()
    {
        var names = new HashSet<string>();
        AddOpenUnityGameFrameworkForms(names);
        foreach (GameObject root in Object.FindObjectsOfType<GameObject>())
        {
            if (!root.activeInHierarchy)
            {
                continue;
            }

            AddViewCandidate(names, root.name);
        }

        return names.OrderBy(name => name).ToList();
    }

    public static bool HasView(string viewIdOrName)
    {
        if (string.IsNullOrEmpty(viewIdOrName))
        {
            return false;
        }

        string target = NormalizeViewName(viewIdOrName);
        return ListOpenViewNames().Any(name => NormalizeViewName(name) == target);
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

    private static void AddOpenUnityGameFrameworkForms(HashSet<string> names)
    {
        foreach (UIFormLogic logic in Object.FindObjectsOfType<UIFormLogic>())
        {
            if (logic == null || (!logic.Available && !logic.gameObject.activeInHierarchy))
            {
                continue;
            }

            AddViewCandidate(names, logic.GetType().Name);
            AddViewCandidate(names, logic.Name);
            UIForm uiForm = logic.UIForm;
            if (uiForm == null)
            {
                continue;
            }

            AddViewCandidate(names, uiForm.name);
            AddViewCandidate(names, Path.GetFileNameWithoutExtension(uiForm.UIFormAssetName));
        }
    }

    private static string NormalizeViewName(string viewIdOrName)
    {
        string value = viewIdOrName.ToLowerInvariant();
        if (value.StartsWith("view."))
        {
            value = value.Substring("view.".Length);
        }

        return value
            .Replace("ui.form.", "uiform")
            .Replace(".", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }
}
