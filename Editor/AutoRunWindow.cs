using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class AutoRunWindow : EditorWindow
{
    private string logText = "";
    private Vector2 scrollPosition;
    private const string HANDLER_OBJECT_NAME = "AutoRunHandler";

    [MenuItem("Window/Auto Run Window")]
    public static void ShowWindow()
    {
        GetWindow<AutoRunWindow>("Auto Run");
    }

    private GameObject _handlerObject;
    private AutoRunHandler _handler;

    private const string ConfigPath = @"/Users/shenhanwen/Documents/Projects/Unity/_lib/AutoRunConfig.xml";
    private AutoRunParamConfig currentLoadingConfig { get; set; } = new();
    private AutoRunActionClass currentSelectingClass { get; set; }

    private void OnGUI()
    {   
        try
        {
            // main
            GUILayout.Label("Auto Run Game Utility");

            if (GUILayout.Button("Go!", GUILayout.Height(40)))
            {
                ClearConsoleText();
                CleanHandlerObjects();

                EditorApplication.isPlaying = true;

                LoadConfig();

                _handlerObject = new GameObject(HANDLER_OBJECT_NAME);
                _handler = _handlerObject.AddComponent<AutoRunHandler>();

                if (!currentLoadingConfig.ParamsOf(currentSelectingClass, out var param))
                {
                    AppendConsoleText("Config not found. Press 'Add' to create one.");
                }
                else
                {
                    _handler.Init(param, AppendConsoleText);
                    AppendConsoleText("Game started...\n");
                }
            }

            if (GUILayout.Button("Stop", GUILayout.Height(40)))
            {
                int cleanCount = CleanHandlerObjects();

                EditorApplication.isPlaying = false;

                AppendConsoleText($"Game stopped. {cleanCount} handler objects cleaned.");
            }

            // actions
            GUILayout.Label("Actions");

            currentSelectingClass = (AutoRunActionClass)EditorGUILayout.EnumPopup(currentSelectingClass);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Add"))
            {
                currentLoadingConfig.Append(currentSelectingClass, new());
            }

            if (GUILayout.Button("Load"))
            {
                LoadConfig();
            }

            if (GUILayout.Button("Save"))
            {
                XmlHelper.SaveConfig<AutoRunParamConfig>(currentLoadingConfig, ConfigPath);
                AppendConsoleText($"Config saved. Details: {currentLoadingConfig.Info()}");
            }

            GUILayout.EndHorizontal();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(80));

            if (currentLoadingConfig.ParamsOf(currentSelectingClass, out var selectingParams))
            {
                for (int i = 0; i < selectingParams.Count; i++)
                {
                    var param = selectingParams[i];

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Name:");
                    param.buttonName = GUILayout.TextField(param.buttonName, GUILayout.Width(50));

                    GUILayout.Label("Text:");
                    param.buttonText = GUILayout.TextField(param.buttonText, GUILayout.Width(50));

                    GUILayout.Label("Delay:");
                    param.delay = float.Parse(GUILayout.TextField(param.delay.ToString(), GUILayout.Width(20)));

                    param.isFairyGUI = GUILayout.Toggle(param.isFairyGUI, "FGUI");

                    if (GUILayout.Button("-"))
                    {
                        selectingParams.RemoveAt(i);
                    }

                    GUILayout.EndHorizontal();
                }
            }
            
            GUILayout.EndScrollView();

            // console
            GUILayout.Label("Console");
            if (GUILayout.Button("Clear"))
            {
                ClearConsoleText();
                CleanHandlerObjects();
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            GUILayout.TextArea(logText);
            GUILayout.EndScrollView();
        }
        catch (System.Exception e)
        {
            // AppendConsoleText("err: " + e.ToString() + "\n");
            throw;
        }
    }

    private void LoadConfig()
    {
        bool hasConfigFile = XmlHelper.TryLoadConfig<AutoRunParamConfig>(
            ConfigPath,
            out var config
        );

        if (hasConfigFile)
        {
            currentLoadingConfig = config;
            AppendConsoleText($"Config loaded. Details: {config.Info()}");
        }
        else
        {
            AppendConsoleText("Config not found. Press 'Add' to create one.");
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
            text += "\n";

        logText += text;
    }
}
