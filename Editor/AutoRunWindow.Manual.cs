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
            + "View full document on my Github:"
        );

        if (GUILayout.Button("more info"))
        {
            Application.OpenURL("https://github.com/SHthemW/Unity-Autorun-Tool");
        }
    }

    private void RenderManualConfigControls()
    {
        GUILayout.Label("Config");
        GUILayout.BeginHorizontal();
        RenderPresetSelector();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        RenderConfigFileActions();
        GUILayout.EndHorizontal();
    }

    private void RenderPresetSelector()
    {
        if (HasPreset)
        {
            _currentSelectingClassIndex = EditorGUILayout.Popup(_currentSelectingClassIndex, LoadedPresetNames);
            _currentSelectingClassName = LoadedPresetNames[_currentSelectingClassIndex];
        }

        if (IsConfigFileExists)
        {
            RenderAddPresetButton();
        }
        else
        {
            _currentLoadingConfig = new AutoRunParamConfig();
        }
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

        if (GUILayout.Button("Then, press me to create a new action preset"))
        {
            _currentLoadingConfig.AppendClass($"new preset {LoadedPresetNames.Length + 1} (you should save it before edit!)");
        }
    }

    private void RenderConfigFileActions()
    {
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
            return;
        }

        if (GUILayout.Button("First use? Press me to create an autorun action config :)", GUILayout.Height(30)))
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
        GUILayout.BeginHorizontal();
        GUILayout.Label(title);

        if (HasPreset)
        {
            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
            {
                _currentLoadingConfig.AppendAction(_currentSelectingClassName, new AutoRunParam(), status);
            }
        }

        GUILayout.EndHorizontal();

        for (int i = 0; i < actionParams.Count; i++)
        {
            int index = i;
            GUILayout.BeginHorizontal();
            RenderActionParam(actionParams[index], () => actionParams.RemoveAt(index));
            GUILayout.EndHorizontal();
        }
    }
}
