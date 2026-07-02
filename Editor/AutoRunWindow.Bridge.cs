using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void RenderBridgeControls()
    {
        GUILayout.BeginHorizontal();

        string status = AutoRunBridgeController.IsRunning ? "Running" : "Stopped";
        string desired = AutoRunBridgeController.IsEnabled ? "Auto restore on" : "Auto restore off";
        GUILayout.Label($"{status} | {desired} | {AutoRunBridgeController.Url}");

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

        GUILayout.EndHorizontal();
    }
}
