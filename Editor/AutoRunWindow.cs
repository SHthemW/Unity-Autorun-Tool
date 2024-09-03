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

    private GameObject _handlerObject;
    private AutoRunHandler _handler;


    private void OnGUI()
    {   
        try
        {
            // main
            GUILayout.Label("Auto Run Game Utility");
            if (GUILayout.Button("Go!"))
            {
                ClearConsoleText();
                CleanHandlerObjects();

                EditorApplication.isPlaying = true;
                
                _handlerObject = new GameObject(HANDLER_OBJECT_NAME);
                _handler = _handlerObject.AddComponent<AutoRunHandler>();

                _handler.Init(buttonActions, AppendConsoleText);

                AppendConsoleText("Game started...\n");
            }

            if (GUILayout.Button("Stop"))
            {
                int cleanCount = CleanHandlerObjects();

                EditorApplication.isPlaying = false;

                AppendConsoleText($"Game stopped. {cleanCount} handler objects cleaned.");
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
                CleanHandlerObjects();
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

    private int CleanHandlerObjects()
    {   
        int count = 0;

        if (_handlerObject != null)
        {
            DestroyImmediate(_handlerObject);
            count += 1;
        }

        var handlerObjects = FindObjectsOfType<AutoRunHandler>(true);

        for (int i = 0; i < handlerObjects.Length; i++)
        {
            DestroyImmediate(handlerObjects[i].gameObject);
            count += 1;
        }
        return count;
    }

    private void ClearConsoleText()
    {
        logText = string.Empty;
    }

    private void AppendConsoleText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        
        if (!text.StartsWith("\n"))
            text = "\n" + text;

        logText += text;
    }
}
