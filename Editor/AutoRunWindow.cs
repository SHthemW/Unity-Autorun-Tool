using UnityEditor;
using UnityEngine;
using System.IO;
using System;

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
    private AutoRunParamConfig _currentLoadingConfig = new AutoRunParamConfig();
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
        GUILayout.Label("Auto Run Game Utility");

        RenderMcpPanel();
        RenderManualAutoRunPanel();

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

    private void BeginPanel(string title)
    {
        GUILayout.Space(8);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(title, EditorStyles.boldLabel);
    }

    private void EndPanel()
    {
        GUILayout.EndVertical();
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

    public static void AppendBridgeConsoleText(string text)
    {
        foreach (AutoRunWindow window in Resources.FindObjectsOfTypeAll<AutoRunWindow>())
        {
            window.AppendConsoleText(text);
            window.Repaint();
        }
    }
}
