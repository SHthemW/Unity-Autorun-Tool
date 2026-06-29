using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class FairyGUIHelper
{
    public static bool IsInstalled => FindType("FairyGUI.GRoot") != null;

    public static List<AutoRunButtonInfo> ListButtons()
    {
        if (!TryGetRootView(out var view, out _))
        {
            return new List<AutoRunButtonInfo>();
        }

        return AllComponentChildren(view)
            .Where(obj => GetMemberValue(obj, "asButton") != null)
            .Select(obj => new AutoRunButtonInfo
            {
                name = GetMemberValue(obj, "name") as string,
                text = GetMemberValue(GetMemberValue(obj, "asButton"), "text") as string,
                framework = "fairygui",
                interactable = true,
            })
            .ToList();
    }

    public static AutoRunButtonResult ClickButton(string buttonName, string buttonText)
    {
        if (buttonText != AutoRunParam.DEFAULT_TEXT)
        {
            return AutoRunButtonResult.Fail(
                "unsupported_text",
                "err: button text not supported on FGUI. use button name instead!"
            );
        }

        if (!TryGetRootView(out var view, out var error))
        {
            return error;
        }

        var allObjects = AllComponentChildren(view);
        var allButtons = allObjects.Where(obj => GetMemberValue(obj, "asButton") != null).ToList();
        var nameMatchedComponents = allButtons.Where(obj => (string)GetMemberValue(obj, "name") == buttonName).ToList();

        if (nameMatchedComponents.Count == 0)
        {
            return AutoRunButtonResult.Fail(
                "button_not_found",
                $"err: button '{buttonName}' not found. view: {GetDisplayName(view)}, childlen: {allObjects.Count}"
            );
        }

        if (nameMatchedComponents.Count > 1)
        {
            return AutoRunButtonResult.Fail("button_not_unique", $"err: button '{buttonName}' not unique!");
        }

        var button = GetMemberValue(nameMatchedComponents[0], "asButton");
        if (button == null)
        {
            return AutoRunButtonResult.Fail("not_button", $"err: button '{buttonName}' is not a button!");
        }

        var onClick = GetMemberValue(button, "onClick");
        if (onClick == null)
        {
            return AutoRunButtonResult.Fail("no_click_event", $"err: button '{buttonName}' has no button click event!");
        }

        InvokeMember(onClick, "Call");
        return AutoRunButtonResult.Success($"btn {buttonName} is clicked.");
    }

    private static bool TryGetRootView(out object view, out AutoRunButtonResult error)
    {
        Type gRootType = FindType("FairyGUI.GRoot");
        if (gRootType == null)
        {
            view = null;
            error = AutoRunButtonResult.Fail(
                "framework_missing",
                "err: FairyGUI is not installed. Disable FGUI or install FairyGUI."
            );
            return false;
        }

        var root = GetStaticMemberValue(gRootType, "inst");
        view = GetMemberValue(root, "asCom");
        if (view == null)
        {
            error = AutoRunButtonResult.Fail("root_not_found", "err: FairyGUI root view not found.");
            return false;
        }

        error = null;
        return true;
    }

    private static List<object> AllComponentChildren(object root)
    {
        List<object> components = new();
        GetComponentsRecursive(root, components);
        return components;
    }

    private static void GetComponentsRecursive(object obj, List<object> components)
    {
        var com = GetMemberValue(obj, "asCom");
        if (com == null)
        {
            Debug.Log("Obj is not a component: " + GetMemberValue(obj, "gameObjectName"));
            return;
        }

        Debug.Log("Obj is a component: " + GetMemberValue(obj, "gameObjectName"));

        components.Add(com);
        int numChildren = (int)GetMemberValue(com, "numChildren");
        for (int i = 0; i < numChildren; i++)
        {
            object child = InvokeMember(com, "GetChildAt", i);
            GetComponentsRecursive(child, components);
        }
    }

    private static Type FindType(string typeName)
    {
        return Type.GetType(typeName)
            ?? Type.GetType($"{typeName}, FairyGUI")
            ?? System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(type => type != null);
    }

    private static object GetStaticMemberValue(Type type, string memberName)
    {
        return type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? type.GetField(memberName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null)
        {
            return null;
        }

        var type = target.GetType();
        return type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
            ?? type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
    }

    private static object InvokeMember(object target, string methodName, params object[] args)
    {
        if (target == null)
        {
            return null;
        }

        Type[] argTypes = args.Select(arg => arg?.GetType() ?? typeof(object)).ToArray();
        MethodInfo method = target.GetType()
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, argTypes, null)
            ?? target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(methodInfo => methodInfo.Name == methodName && methodInfo.GetParameters().Length == args.Length);

        return method?.Invoke(target, args);
    }

    private static string GetDisplayName(object view)
    {
        var displayObject = GetMemberValue(view, "displayObject");
        return GetMemberValue(displayObject, "name") as string ?? "unknown";
    }
}
