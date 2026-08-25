using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void RenderBridgeControls()
    {
        ResponsiveRow row = BeginResponsiveRow();

        string status = AutoRunBridgeController.IsRunning ? "Running" : "Stopped";
        string desired = AutoRunBridgeController.IsEnabled ? "Auto restore on" : "Auto restore off";
        row.Add(240f);
        GUILayout.Label(
            $"{status} | {desired} | {AutoRunBridgeController.Url}",
            GetWrappedStyle(EditorStyles.label),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true)
        );

        row.Add(144f);
        using (new EditorGUI.DisabledScope(AutoRunBridgeController.IsRunning))
        {
            if (GUILayout.Button("Start", GUILayout.Width(70)))
            {
                AutoRunBridgeController.Start();
            }
        }

        using (new EditorGUI.DisabledScope(!AutoRunBridgeController.IsRunning && !AutoRunBridgeController.IsEnabled))
        {
            if (GUILayout.Button("Stop", GUILayout.Width(70)))
            {
                AutoRunBridgeController.Stop();
            }
        }

        row.End();
    }
}
