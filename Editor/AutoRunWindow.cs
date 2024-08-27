using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class AutoRunWindow : EditorWindow
{
    private string logText = "";
    private Vector2 scrollPosition;
    private readonly List<AutoRunAction> buttonActions = new() {
        new() { buttonName = "a1", delay = 3f },
        new() { buttonName = "a2", delay = 3f },
        new() { buttonName = "a3", delay = 3f },
    };
    private const string HANDLER_OBJECT_NAME = "AutoRunHandler";

    [MenuItem("Window/Auto Run Window")]
    public static void ShowWindow()
    {
        GetWindow<AutoRunWindow>("Auto Run");
    }

    private void OnGUI()
    {   
        try
        {
            // main
            GUILayout.Label("Auto Run Game Utility");
            if (GUILayout.Button("Go!"))
            {
                ClearConsoleText();

                EditorApplication.isPlaying = true;

                var handlerObject = new GameObject(HANDLER_OBJECT_NAME);
                var handler = handlerObject.AddComponent<AutoRunHandler>();

                handler.Init(buttonActions, AppendConsoleText);

                AppendConsoleText("Game started...\n");
            }

            if (GUILayout.Button("Stop"))
            {
                var handlerObject = GameObject.Find(HANDLER_OBJECT_NAME);
                DestroyImmediate(handlerObject);

                EditorApplication.isPlaying = false;

                AppendConsoleText("Game stopped.\n");
            }

            // actions
            GUILayout.Label("Actions");
            if (GUILayout.Button("Add Action"))
            {
                buttonActions.Add(new AutoRunAction());
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
            foreach (var action in buttonActions)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Button Name:");
                action.buttonName = GUILayout.TextField(action.buttonName, GUILayout.Width(80));
                GUILayout.Label("Delay Second:");
                action.delay = float.Parse(GUILayout.TextField(action.delay.ToString(), GUILayout.Width(30)));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            // console
            GUILayout.Label("Console");
            if (GUILayout.Button("Clear"))
            {
                ClearConsoleText();
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
            GUILayout.TextArea(logText);
            GUILayout.EndScrollView();
        }
        catch (System.Exception e)
        {
            AppendConsoleText("err: " + e.ToString() + "\n");
        }
    }

    private void ClearConsoleText()
    {
        logText = string.Empty;
    }

    private void AppendConsoleText(string text)
    {
        logText += text;
    }
}
