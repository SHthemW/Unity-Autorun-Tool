using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private void RenderManualAutoRunPanel()
    {
        BeginPanel("Manual AutoRun");
        RenderManualRunControls();
        RenderManualConfigControls();
        RenderManualActions();
        EndPanel();
    }

    private void RenderManualRunControls()
    {
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
            return;
        }

        GUILayout.Label(
              "\n"
            + "Follow the instructions on bottons to use this tool.\n"
            + "\n"
            + "View full document on my Github:",
            GetWrappedStyle(EditorStyles.label),
            GUILayout.MinWidth(0),
            GUILayout.ExpandWidth(true)
        );

        if (GUILayout.Button("more info"))
        {
            Application.OpenURL("https://github.com/SHW2002/Unity-Autorun-MCP");
        }
    }

    private void RenderManualConfigControls()
    {
        GUILayout.Label("Config");
        RenderPresetSelector();
        RenderConfigFileActions();
    }

    private void RenderPresetSelector()
    {
        ResponsiveRow row = BeginResponsiveRow();
        if (HasPreset)
        {
            row.Add(160f);
            _currentSelectingClassIndex = EditorGUILayout.Popup(
                _currentSelectingClassIndex,
                LoadedPresetNames,
                GUILayout.MinWidth(0),
                GUILayout.ExpandWidth(true));
            _currentSelectingClassName = LoadedPresetNames[_currentSelectingClassIndex];
        }

        if (IsConfigFileExists)
        {
            row.Add(HasPreset ? 20f : 220f);
            RenderAddPresetButton();
        }
        else
        {
            _currentLoadingConfig = new AutoRunParamConfig();
        }

        row.End();
    }

    private void RenderAddPresetButton()
    {
        if (HasPreset)
        {
            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
            {
                _currentLoadingConfig.AppendClass($"new preset {LoadedPresetNames.Length + 1} (change name in config file)");
            }
            return;
        }

        if (GUILayout.Button(
                "Then, press me to create a new action preset",
                GetWrappedStyle(GUI.skin.button),
                GUILayout.MinWidth(0),
                GUILayout.ExpandWidth(true)))
        {
            _currentLoadingConfig.AppendClass($"new preset {LoadedPresetNames.Length + 1} (you should save it before edit!)");
        }
    }

    private void RenderConfigFileActions()
    {
        ResponsiveRow row = BeginResponsiveRow();
        if (IsConfigFileExists)
        {
            row.Add(100f);
            if (GUILayout.Button("Open config"))
            {
                XmlHelper.OpenWithDefaultEditor(ConfigPath);
            }

            row.Add(100f);
            if (GUILayout.Button("Save config"))
            {
                XmlHelper.SaveConfig(_currentLoadingConfig, ConfigPath);
                AppendConsoleText($"Config saved. Details: {_currentLoadingConfig.Info()}");
            }
            row.End();
            return;
        }

        row.Add(220f);
        if (GUILayout.Button(
                "First use? Press me to create an autorun action config :)",
                GetWrappedStyle(GUI.skin.button),
                GUILayout.MinHeight(30),
                GUILayout.MinWidth(0),
                GUILayout.ExpandWidth(true)))
        {
            if (!Directory.Exists(Path.GetDirectoryName(ConfigPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            }

            if (!XmlHelper.SaveConfig(_currentLoadingConfig, ConfigPath))
            {
                AppendConsoleText("Failed to create config file.", AutoRunLogLevel.Error);
            }

            AppendConsoleText("Config is created on: " + ConfigPath);
        }
        row.End();
    }

    private void RenderManualActions()
    {
        _actionScrollPosition = GUILayout.BeginScrollView(_actionScrollPosition, GUILayout.Height(120));

        if (_currentLoadingConfig.GetActions(_currentSelectingClassName, out var goParams, out var stopParams))
        {
            RenderActionList("Action - Go", goParams, HandlerStatus.Go);
            RenderActionList("Action - Stop", stopParams, HandlerStatus.Stop);
        }

        GUILayout.EndScrollView();
    }

    private void RenderActionList(string title, System.Collections.Generic.List<AutoRunParam> actionParams, HandlerStatus status)
    {
        ResponsiveRow header = BeginResponsiveRow(WindowVerticalScrollbarWidth);
        header.Add(100f);
        GUILayout.Label(title);

        if (HasPreset)
        {
            header.Add(20f);
            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
            {
                _currentLoadingConfig.AppendAction(_currentSelectingClassName, new AutoRunParam(), status);
            }
        }

        header.End();

        for (int i = 0; i < actionParams.Count; i++)
        {
            int index = i;
            RenderActionParam(actionParams[index], () => actionParams.RemoveAt(index));
        }
    }
}
