using System;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void RenderActionParam(AutoRunParam param, Action onRemove)
    {
        GUILayout.Label("- name");
        param.buttonName = GUILayout.TextField(param.buttonName, GUILayout.Width(50));

        GUILayout.Label("text");
        param.buttonText = GUILayout.TextField(param.buttonText, GUILayout.Width(50));

        GUILayout.Label("delay");
        param.delay = float.Parse(GUILayout.TextField(param.delay.ToString(), GUILayout.Width(20)));

        GUILayout.Space(10);

        if (FairyGUIHelper.IsInstalled)
        {
            param.isFairyGUI = GUILayout.Toggle(param.isFairyGUI, "FGUI");
        }
        else
        {
            param.isFairyGUI = false;
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Toggle(false, "FGUI");
            }
        }

        if (GUILayout.Button("-", GUILayout.MaxWidth(20)))
        {
            onRemove();
        }
    }
}
