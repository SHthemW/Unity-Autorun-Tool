
using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

public static class FairyGUIHelper
{
    public static List<GObject> AllComponentChildren(this GObject root)
    {
        List<GObject> components = new();
        GetComponentsRecursive(root, components);
        return components;
    }

    private static void GetComponentsRecursive(GObject obj, List<GObject> components)
    {
        var com = obj.asCom;
        if (com == null)
        {
            Debug.Log("Obj is not a component: " + obj.gameObjectName);
            return;
        }

        Debug.Log("Obj is a component: " + obj.gameObjectName);

        components.Add(com);
        for (int i = 0; i < com.numChildren; i++)
        {
            GObject child = com.GetChildAt(i);
            GetComponentsRecursive(child, components);
        }
    }
}