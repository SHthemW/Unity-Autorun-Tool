using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Xml;

public class AutoRunWindow : EditorWindow
{
    private string _logText = "";
    private Vector2 _scrollPosition;
    private const string HANDLER_OBJECT_NAME = "AutoRunHandler";

    [MenuItem("Window/Auto Run Window")]
    public static void ShowWindow()
    {
        GetWindow<AutoRunWindow>("Auto Run");
    }

    private GameObject _handlerObject;
    private AutoRunHandler _handler;

    private string ConfigPath => AppDomain.CurrentDomain.BaseDirectory + @"\AutorunToolData\config.xml";
    private AutoRunParamConfig _currentLoadingConfig = new();
    private string _currentSelectingClassName;
    private int _currentSelectingClassIndex = 0;

    private void OnFocus()
    {
        LoadConfig();
    }

    private void OnLostFocus()
    {
        LoadConfig();
    }

    private void OnGUI()
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

            if (!_currentLoadingConfig.ParamsOf(_currentSelectingClassName, out var param))
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

        GUILayout.BeginHorizontal();

        var classNames = _currentLoadingConfig.GetClassNames();

        var hasPreset = classNames.Length > 0;

        var hasFile = File.Exists(ConfigPath);

        if (hasPreset)
        {
            _currentSelectingClassIndex = EditorGUILayout.Popup(_currentSelectingClassIndex, classNames);
            _currentSelectingClassName = classNames[_currentSelectingClassIndex];
        }

        if (hasFile)
        {
            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
            {
                _currentLoadingConfig.AppendClass($"new preset {classNames.Length + 1} (change name in config file)");
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (hasFile)
        {
            if (GUILayout.Button("Add action"))
            {
                _currentLoadingConfig.Append(_currentSelectingClassName, new AutoRunParam());
            }

            if (GUILayout.Button("Open config"))
            {
                XmlHelper.OpenWithDefaultEditor(ConfigPath);
            }

            if (GUILayout.Button("Save config"))
            {
                XmlHelper.SaveConfig(_currentLoadingConfig, ConfigPath);
                AppendConsoleText($"Config saved. Details: {_currentLoadingConfig.Info()}");
            }
        }
        else
        {
            if (GUILayout.Button("First use? Press me to create an autorun action config :)", GUILayout.Height(30)))
            {
                XmlHelper.SaveConfig(_currentLoadingConfig, ConfigPath);
                XmlHelper.OpenWithDefaultEditor(ConfigPath);

                AppendConsoleText("Config is created on: " + ConfigPath);
            }
        }

        GUILayout.EndHorizontal();

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(80));

        if (_currentLoadingConfig.ParamsOf(_currentSelectingClassName, out var selectingParams))
        {
            for (int i = 0; i < selectingParams.Count; i++)
            {
                var param = selectingParams[i];

                GUILayout.BeginHorizontal();

                GUILayout.Label("name");
                param.buttonName = GUILayout.TextField(param.buttonName, GUILayout.Width(50));

                GUILayout.Label("text");
                param.buttonText = GUILayout.TextField(param.buttonText, GUILayout.Width(50));

                GUILayout.Label("delay");
                param.delay = float.Parse(GUILayout.TextField(param.delay.ToString(), GUILayout.Width(20)));

                GUILayout.Space(10);

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

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
        GUILayout.TextArea(_logText);
        GUILayout.EndScrollView();
    }

    private void LoadConfig()
    {
        bool hasConfigFile = XmlHelper.TryLoadConfig<AutoRunParamConfig>(
            ConfigPath,
            out var config
        );

        if (hasConfigFile)
        {
            _currentLoadingConfig = config;
            AppendConsoleText($"Config loaded. Details: {config.Info()}");
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
        _logText = string.Empty;
    }

    private void AppendConsoleText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        
        if (!text.StartsWith("\n"))
            text += "\n";

        _logText += text;
    }
}
