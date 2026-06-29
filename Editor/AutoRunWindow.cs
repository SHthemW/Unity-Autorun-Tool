using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Xml;

public partial class AutoRunWindow : EditorWindow
{
    private string _logText = "";
    private Vector2 _actionScrollPosition;
    private Vector2 _consoleScrollPosition;
    private const string HANDLER_OBJECT_NAME = "AutoRunHandler";

    [MenuItem("Window/Auto Run Window")]
    public static void ShowWindow()
    {
        GetWindow<AutoRunWindow>("Auto Run");
    }

    private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutorunToolData", "config.xml");
    private AutoRunParamConfig _currentLoadingConfig = new();
    private string _currentSelectingClassName;
    private int _currentSelectingClassIndex = 0;

    private string[] LoadedPresetNames => _currentLoadingConfig.GetClassNames();
    private bool HasPreset => LoadedPresetNames.Length > 0;
    private bool IsConfigFileExists => File.Exists(ConfigPath);

    private void OnFocus()
    {
        // Do not load config when in play mode
        if (EditorApplication.isPlaying)
        {
            return;
        }

        LoadConfig();
    }

    private void OnGUI()
    {   
        // main
        GUILayout.Label("Auto Run Game Utility");

        if (IsConfigFileExists)
        {
            if (GUILayout.Button("Go!", GUILayout.Height(40)))
            {
                ClearConsoleText();

                EditorApplication.isPlaying = true;

                LoadConfig();

                var handler = GetHandler();
                handler.SetStatus(HandlerStatus.Go);
            }

            if (GUILayout.Button("Stop", GUILayout.Height(40)))
            {
                var handler = GetHandler();
                handler.SetStatus(HandlerStatus.Stop);
            }
        }
        else
        {
            GUILayout.Label(
                  "\n"
                + "Follow the instructions on bottons to use this tool.\n"
                + "\n"
                + "View full document on my Github:"
            );

            if (GUILayout.Button("more info"))
            {
                Application.OpenURL("https://github.com/SHthemW/Unity-Autorun-Tool");
            }
        }

        // actions

        GUILayout.Label("Config");

        GUILayout.BeginHorizontal();

        if (HasPreset)
        {
            _currentSelectingClassIndex = EditorGUILayout.Popup(_currentSelectingClassIndex, LoadedPresetNames);
            _currentSelectingClassName = LoadedPresetNames[_currentSelectingClassIndex];
        }

        if (IsConfigFileExists)
        {
            if (HasPreset)
            {
                if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
                {
                    _currentLoadingConfig.AppendClass($"new preset {LoadedPresetNames.Length + 1} (change name in config file)");
                }
            }
            else
            {
                // maybe first use, show a tutorial-style description.

                if (GUILayout.Button("Then, press me to create a new action preset"))
                {
                    _currentLoadingConfig.AppendClass($"new preset {LoadedPresetNames.Length + 1} (you should save it before edit!)");
                }
            }
        }
        else
        {
            _currentLoadingConfig = new();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (IsConfigFileExists)
        {
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
                if (!Directory.Exists(Path.GetDirectoryName(ConfigPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                }

                if (!XmlHelper.SaveConfig(_currentLoadingConfig, ConfigPath))
                {
                    Debug.LogError("Failed to create config file.");
                }

                AppendConsoleText("Config is created on: " + ConfigPath);
            }
        }

        GUILayout.EndHorizontal();

        _actionScrollPosition = GUILayout.BeginScrollView(_actionScrollPosition, GUILayout.Height(120));

        if (_currentLoadingConfig.GetActions(_currentSelectingClassName, out var goParams, out var stopParams))
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Action - Go");

            if (HasPreset)
            {
                if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
                {
                    _currentLoadingConfig.AppendAction(_currentSelectingClassName, new AutoRunParam(), HandlerStatus.Go);
                }
            }

            GUILayout.EndHorizontal();

            for (int i = 0; i < goParams.Count; i++)
            {
                GUILayout.BeginHorizontal();

                RenderActionParam(
                    goParams[i],
                    () => goParams.RemoveAt(i)
                );

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label("Action - Stop");

            if (HasPreset)
            {
                if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
                {
                    _currentLoadingConfig.AppendAction(_currentSelectingClassName, new AutoRunParam(), HandlerStatus.Stop);
                }
            }

            GUILayout.EndHorizontal();

            for (int i = 0; i < stopParams.Count; i++)
            {
                GUILayout.BeginHorizontal();

                RenderActionParam(
                    stopParams[i],
                    () => stopParams.RemoveAt(i)
                );

                GUILayout.EndHorizontal();
            }
        }
        
        GUILayout.EndScrollView();

        // console
        GUILayout.Label("Console");
        if (GUILayout.Button("Clear"))
        {
            ClearConsoleText();
        }

        _consoleScrollPosition = GUILayout.BeginScrollView(_consoleScrollPosition, GUILayout.Height(100));
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
            AppendConsoleText($"Config loaded. Details: " + config.Info());
        }
    }

    private AutoRunHandler GetHandler()
    {
        if (!_currentLoadingConfig.GetActions(_currentSelectingClassName, out var goActionParams, out var stopActionParams))
        {
            AppendConsoleText("Config not found. Press 'Add' to create one.");
            return null;
        }

        var handler = FindObjectOfType<AutoRunHandler>();

        if (handler == null)
        {
            handler = new GameObject(HANDLER_OBJECT_NAME).AddComponent<AutoRunHandler>();
        }

        handler.Init(
            goActionParams: goActionParams,
            stopActionParams: stopActionParams,
            stopActionCallback: () => 
            {
                handler.SetStatus(HandlerStatus.None);
                EditorApplication.isPlaying = false;
                AppendConsoleText($"Progress done.");
            },
            msgHandler: AppendConsoleText
        );

        return handler;
    }

    private void ClearConsoleText()
    {
        _logText = string.Empty;
        _consoleScrollPosition = Vector2.zero;
    }

    private void AppendConsoleText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        
        if (!text.StartsWith("\n"))
            text += "\n";

        _logText += text;
        _consoleScrollPosition.y += 100; // Keep scroll at the bottom
    }
}
