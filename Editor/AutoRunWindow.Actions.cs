using System;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void RenderActionParam(AutoRunParam param, Action onRemove)
    {
        ResponsiveRow row = BeginResponsiveRow(WindowVerticalScrollbarWidth);
        row.Add(105f);
        GUILayout.Label("- name");
        param.buttonName = GUILayout.TextField(param.buttonName, GUILayout.Width(50));

        row.Add(85f);
        GUILayout.Label("text");
        param.buttonText = GUILayout.TextField(param.buttonText, GUILayout.Width(50));

        row.Add(70f);
        GUILayout.Label("delay");
        param.delay = float.Parse(GUILayout.TextField(param.delay.ToString(), GUILayout.Width(20)));

        row.Add(55f);
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

        row.Add(20f);
        if (GUILayout.Button("-", GUILayout.MaxWidth(20)))
        {
            onRemove();
        }
        row.End();
    }
}
