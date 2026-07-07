using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public partial class AutoRunWindow : EditorWindow
{
    private const int MaxConsoleChars = 20000;
    private const int MaxConsoleEntryChars = 1200;
    private const float WindowVerticalScrollbarWidth = 18f;
    private const float WindowContentPadding = 10f;
    private const float MinimumWindowContentWidth = 260f;

    private readonly List<AutoRunConsoleEntry> _consoleEntries = new List<AutoRunConsoleEntry>();
    private int _consoleCharCount;
    private bool _showDebugLogs = true;
    private bool _showInfoLogs = true;
    private bool _showWarningLogs = true;
    private bool _showErrorLogs = true;
    private const string WindowScrollXKey = "UnityAutorunTool.Window.ScrollX";
    private const string WindowScrollYKey = "UnityAutorunTool.Window.ScrollY";
    private static readonly Dictionary<AutoRunLogLevel, GUIStyle> ConsoleEntryStyles = new Dictionary<AutoRunLogLevel, GUIStyle>();
    private static readonly Dictionary<GUIStyle, GUIStyle> SqueezedStyles = new Dictionary<GUIStyle, GUIStyle>();
    private static Font _consoleFont;
    private Vector2 _windowScrollPosition;
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

    private void OnEnable()
    {
        _windowScrollPosition = new Vector2(
            0f,
            EditorPrefs.GetFloat(WindowScrollYKey, 0f)
        );
    }

    private void OnDisable()
    {
        SaveWindowScrollPosition();
    }

    private void OnFocus()
    {
        Repaint();

        // Do not load config when in play mode
        if (EditorApplication.isPlaying)
        {
            return;
        }

        LoadConfig(false);
    }

    private void OnGUI()
    {
        Vector2 nextScrollPosition = GUILayout.BeginScrollView(
            _windowScrollPosition,
            false,
            true,
            GUIStyle.none,
            GUI.skin.verticalScrollbar
        );
        nextScrollPosition.x = 0f;
        if (nextScrollPosition != _windowScrollPosition)
        {
            _windowScrollPosition = nextScrollPosition;
            SaveWindowScrollPosition();
        }

        GUILayout.BeginVertical(GUILayout.Width(GetWindowContentWidth()), GUILayout.ExpandWidth(false));
        GUILayout.Label("Auto Run Game Utility");

        RenderMcpPanel();
        RenderNavigationAutoRunPanel();
        RenderManualAutoRunPanel();
        RenderConsole();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }

    private void SaveWindowScrollPosition()
    {
        EditorPrefs.SetFloat(WindowScrollXKey, 0f);
        EditorPrefs.SetFloat(WindowScrollYKey, _windowScrollPosition.y);
    }

    private float GetWindowContentWidth()
    {
        return Mathf.Max(MinimumWindowContentWidth, position.width - WindowVerticalScrollbarWidth - WindowContentPadding);
    }

    private static GUIStyle GetSqueezedStyle(GUIStyle baseStyle)
    {
        if (SqueezedStyles.TryGetValue(baseStyle, out GUIStyle style))
        {
            return style;
        }

        style = new GUIStyle(baseStyle)
        {
            clipping = TextClipping.Clip,
            wordWrap = false,
        };
        SqueezedStyles[baseStyle] = style;
        return style;
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

    private void LoadConfig(bool logLoaded = true)
    {
        bool hasConfigFile = XmlHelper.TryLoadConfig<AutoRunParamConfig>(
            ConfigPath,
            out var config
        );

        if (hasConfigFile)
        {
            _currentLoadingConfig = config;
            if (logLoaded)
            {
                AppendConsoleText($"Config loaded. Details: " + config.Info());
            }
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
        _consoleEntries.Clear();
        _consoleCharCount = 0;
        _consoleScrollPosition = Vector2.zero;
    }

    private void AppendConsoleText(string text)
    {
        AppendConsoleText(text, AutoRunLogLevel.Info);
    }

    private void AppendConsoleText(string text, AutoRunLogLevel level)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (text.Length > MaxConsoleEntryChars)
        {
            text = text.Substring(0, MaxConsoleEntryChars) + "... [truncated]";
        }
        
        text = text.TrimEnd('\r', '\n');
        var entry = new AutoRunConsoleEntry(level, text);
        _consoleEntries.Add(entry);
        _consoleCharCount += entry.Text.Length;
        while (_consoleCharCount > MaxConsoleChars && _consoleEntries.Count > 0)
        {
            _consoleCharCount -= _consoleEntries[0].Text.Length;
            _consoleEntries.RemoveAt(0);
        }

        _consoleScrollPosition.y += 100; // Keep scroll at the bottom
    }

    public static void AppendBridgeConsoleText(string text)
    {
        AppendBridgeConsoleText(text, AutoRunLogLevel.Info);
    }

    public static void AppendBridgeConsoleText(string text, AutoRunLogLevel level)
    {
        foreach (AutoRunWindow window in Resources.FindObjectsOfTypeAll<AutoRunWindow>())
        {
            window.AppendConsoleText(text, level);
            window.Repaint();
        }
    }

    public static void RepaintAllNavigationWindows()
    {
        foreach (AutoRunWindow window in Resources.FindObjectsOfTypeAll<AutoRunWindow>())
        {
            window.Repaint();
        }
    }

    private void RenderConsole()
    {
        GUILayout.Label("Console");
        RenderConsoleLevelFilters();
        if (GUILayout.Button("Clear"))
        {
            ClearConsoleText();
        }

        GUILayout.BeginVertical(EditorStyles.helpBox);
        _consoleScrollPosition = GUILayout.BeginScrollView(_consoleScrollPosition, GUILayout.Height(220));
        if (_consoleEntries.Count == 0)
        {
            GUILayout.Label("No logs.", GetConsoleEntryStyle(AutoRunLogLevel.Debug));
        }
        else
        {
            bool hasVisibleEntry = false;
            foreach (AutoRunConsoleEntry entry in _consoleEntries)
            {
                if (!ShouldShowConsoleEntry(entry.Level))
                {
                    continue;
                }

                hasVisibleEntry = true;
                GUILayout.Label(FormatConsoleEntry(entry), GetConsoleEntryStyle(entry.Level));
            }

            if (!hasVisibleEntry)
            {
                GUILayout.Label("No logs match the selected levels.", GetConsoleEntryStyle(AutoRunLogLevel.Debug));
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void RenderConsoleLevelFilters()
    {
        GUILayout.BeginHorizontal();
        _showDebugLogs = GUILayout.Toggle(_showDebugLogs, "Debug", GUILayout.Width(70));
        _showInfoLogs = GUILayout.Toggle(_showInfoLogs, "Info", GUILayout.Width(60));
        _showWarningLogs = GUILayout.Toggle(_showWarningLogs, "Warning", GUILayout.Width(85));
        _showErrorLogs = GUILayout.Toggle(_showErrorLogs, "Error", GUILayout.Width(65));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private bool ShouldShowConsoleEntry(AutoRunLogLevel level)
    {
        switch (level)
        {
            case AutoRunLogLevel.Debug:
                return _showDebugLogs;
            case AutoRunLogLevel.Info:
                return _showInfoLogs;
            case AutoRunLogLevel.Warning:
                return _showWarningLogs;
            case AutoRunLogLevel.Error:
                return _showErrorLogs;
            default:
                return true;
        }
    }

    private static string FormatConsoleEntry(AutoRunConsoleEntry entry)
    {
        return "[" + GetConsoleLevelPrefix(entry.Level) + "] " + entry.Text;
    }

    private static string GetConsoleLevelPrefix(AutoRunLogLevel level)
    {
        switch (level)
        {
            case AutoRunLogLevel.Debug:
                return "D";
            case AutoRunLogLevel.Info:
                return "I";
            case AutoRunLogLevel.Warning:
                return "W";
            case AutoRunLogLevel.Error:
                return "E";
            default:
                return "?";
        }
    }

    private static GUIStyle GetConsoleEntryStyle(AutoRunLogLevel level)
    {
        if (ConsoleEntryStyles.TryGetValue(level, out GUIStyle style))
        {
            return style;
        }

        style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            richText = false,
            font = GetConsoleFont(),
            fontSize = 12,
            fixedHeight = 0f,
        };
        style.margin = new RectOffset(0, 0, 2, 6);
        style.padding = new RectOffset(0, 0, 3, 3);
        style.normal.textColor = GetConsoleTextColor(level);
        ConsoleEntryStyles[level] = style;
        return style;
    }

    private static Font GetConsoleFont()
    {
        if (_consoleFont != null)
        {
            return _consoleFont;
        }

        _consoleFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Consolas", "Courier New", "Menlo", "Monaco", "monospace" },
            12
        );
        return _consoleFont;
    }

    private static Color GetConsoleTextColor(AutoRunLogLevel level)
    {
        switch (level)
        {
            case AutoRunLogLevel.Debug:
                return new Color(0.55f, 0.55f, 0.55f);
            case AutoRunLogLevel.Warning:
                return new Color(1f, 0.72f, 0.16f);
            case AutoRunLogLevel.Error:
                return new Color(1f, 0.25f, 0.25f);
            default:
                return EditorStyles.label.normal.textColor;
        }
    }

    private sealed class AutoRunConsoleEntry
    {
        public AutoRunConsoleEntry(AutoRunLogLevel level, string text)
        {
            Level = level;
            Text = text;
        }

        public AutoRunLogLevel Level { get; }
        public string Text { get; }
    }
}

public enum AutoRunLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}
