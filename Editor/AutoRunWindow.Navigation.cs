using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AutoRunWindow
{
    private const string NavigationSelectedTargetKey = "UnityAutorunTool.Navigation.SelectedTargetViewId";

    private NavigationAutoRunMap _navigationMap;
    private List<NavigationAutoRunOption> _navigationTargets = new List<NavigationAutoRunOption>();
    private List<NavigationAutoRunOption> _navigationFilteredTargets = new List<NavigationAutoRunOption>();
    private string[] _navigationTargetNames = new string[0];
    private int _navigationTargetIndex;
    private bool _navigationRunning;
    private bool _navigationLoadAttempted;
    private string _navigationSearchText = "";
    private string _navigationSelectedTargetViewId;
    private NavigationAutoRunOption _pendingNavigationTarget;
    private string _navigationStatusText;
    private bool _navigationCanceled;
    private int _navigationRunId;

    private void RenderNavigationAutoRunPanel()
    {
        BeginPanel("Navigation AutoRun");
        RefreshPendingNavigationUiState();
        EnsureNavigationTargetsLoaded(false);

        RenderNavigationStatus();
        using (new EditorGUI.DisabledScope(_navigationRunning))
        {
            string nextSearchText = EditorGUILayout.TextField("Search", _navigationSearchText);
            if (nextSearchText != _navigationSearchText)
            {
                _navigationSearchText = nextSearchText;
                RefreshNavigationTargetFilter();
            }

            GUILayout.BeginHorizontal();
            if (_navigationTargetNames.Length > 0)
            {
                _navigationTargetIndex = Mathf.Clamp(_navigationTargetIndex, 0, _navigationTargetNames.Length - 1);
                int nextIndex = EditorGUILayout.Popup(_navigationTargetIndex, _navigationTargetNames);
                if (nextIndex != _navigationTargetIndex)
                {
                    _navigationTargetIndex = nextIndex;
                    SaveSelectedNavigationTarget();
                }
            }
            else
            {
                GUILayout.Label("No navigable UI found.");
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                EnsureNavigationTargetsLoaded(true);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Go!", GUILayout.Height(40)))
            {
                StartNavigationAutoRun();
            }
        }

        if (_navigationRunning && GUILayout.Button("Cancel", GUILayout.Height(28)))
        {
            CancelNavigationAutoRun();
        }

        EndPanel();
    }

    private void EnsureNavigationTargetsLoaded(bool force)
    {
        if (string.IsNullOrEmpty(_navigationSelectedTargetViewId))
        {
            _navigationSelectedTargetViewId = EditorPrefs.GetString(NavigationSelectedTargetKey, "");
            LogNavigation("Loaded selected target from EditorPrefs: " + _navigationSelectedTargetViewId);
        }

        if (force)
        {
            _navigationLoadAttempted = false;
        }

        if (!force && (_navigationMap != null || _navigationLoadAttempted))
        {
            return;
        }

        _navigationLoadAttempted = true;
        try
        {
            LogNavigation("Loading navigation map from: " + NavigationAutoRunMap.GetDefaultMapPath());
            _navigationMap = NavigationAutoRunMap.LoadDefault();
            _navigationTargets = _navigationMap.ListNavigableTargets();
            RefreshNavigationTargetFilter();
            LogNavigation("Loaded navigation map: " + _navigationMap.Path + ", targets=" + _navigationTargets.Count);
        }
        catch (Exception ex)
        {
            _navigationMap = null;
            _navigationTargets = new List<NavigationAutoRunOption>();
            _navigationFilteredTargets = new List<NavigationAutoRunOption>();
            _navigationTargetNames = new string[0];
            AppendConsoleText("Navigation AutoRun load failed: " + ex.Message, AutoRunLogLevel.Error);
        }
    }

    private void StartNavigationAutoRun()
    {
        if (_navigationMap == null || _navigationFilteredTargets.Count == 0)
        {
            LogNavigation("Go requested with empty map state. Forcing navigation map reload.");
            EnsureNavigationTargetsLoaded(true);
        }

        if (_navigationMap == null || _navigationFilteredTargets.Count == 0)
        {
            LogNavigation("Go ignored. mapLoaded=" + (_navigationMap != null)
                + ", filteredTargets=" + _navigationFilteredTargets.Count);
            return;
        }

        NavigationAutoRunOption target = _navigationFilteredTargets[_navigationTargetIndex];
        SaveSelectedNavigationTarget();
        _navigationRunId++;
        _navigationRunning = true;
        _navigationCanceled = false;
        _navigationStatusText = "Starting navigation to " + target.Name;
        ClearConsoleText();
        LogNavigation("Started: " + target.DisplayName
            + ", runId=" + _navigationRunId
            + ", isPlaying=" + EditorApplication.isPlaying, AutoRunLogLevel.Info);
        if (!EditorApplication.isPlaying)
        {
            LogNavigation("Saving pending target before entering Play Mode: " + target.ViewId);
            NavigationAutoRunSession.SavePending(target, _navigationRunId);
            StartPendingNavigation(target);
            return;
        }

        TryStartNavigationPlan(target, false);
    }

    private void RefreshNavigationTargetFilter()
    {
        string searchText = (_navigationSearchText ?? "").Trim().ToLowerInvariant();
        _navigationFilteredTargets = string.IsNullOrEmpty(searchText)
            ? new List<NavigationAutoRunOption>(_navigationTargets)
            : _navigationTargets.FindAll(target => target.DisplayName.ToLowerInvariant().Contains(searchText));
        _navigationTargetNames = _navigationFilteredTargets.ConvertAll(target => target.DisplayName).ToArray();
        LogNavigation("Filter refreshed. search='" + searchText + "', matches=" + _navigationFilteredTargets.Count);
        int selectedIndex = _navigationFilteredTargets.FindIndex(target => target.ViewId == _navigationSelectedTargetViewId);
        if (selectedIndex >= 0)
        {
            _navigationTargetIndex = selectedIndex;
            return;
        }

        if (_navigationTargetIndex >= _navigationTargetNames.Length)
        {
            _navigationTargetIndex = 0;
        }
    }

    private void RenderNavigationStatus()
    {
        if (!_navigationRunning)
        {
            return;
        }

        string status = string.IsNullOrEmpty(_navigationStatusText) ? "Navigation AutoRun is running." : _navigationStatusText;
        GUILayout.Label(status, EditorStyles.miniLabel);
    }

    private void CancelNavigationAutoRun()
    {
        _navigationRunning = false;
        _navigationCanceled = true;
        _navigationRunId++;
        _navigationStatusText = null;
        StopPendingNavigation();
        NavigationAutoRunSession.ClearPending();
        NavigationAutoRunSession.ClearActiveRequest();
        LogNavigation("Canceled by user.", AutoRunLogLevel.Warning);
        NavigationAutoRunCancelRequest.Start(OnNavigationAutoRunCancelCompleted);
        Repaint();
    }

    private void SaveSelectedNavigationTarget()
    {
        if (_navigationTargetIndex < 0 || _navigationTargetIndex >= _navigationFilteredTargets.Count)
        {
            return;
        }

        _navigationSelectedTargetViewId = _navigationFilteredTargets[_navigationTargetIndex].ViewId;
        EditorPrefs.SetString(NavigationSelectedTargetKey, _navigationSelectedTargetViewId);
        LogNavigation("Saved selected target: " + _navigationSelectedTargetViewId);
    }

    private void OnNavigationAutoRunCancelCompleted(AutoRunBridgeResponse response)
    {
        if (response == null || response.ok)
        {
            return;
        }

        LogNavigation("Cancel response " + response.code + ": " + response.message, AutoRunLogLevel.Error);
        Repaint();
    }

    private void LogNavigation(string message, AutoRunLogLevel level = AutoRunLogLevel.Debug)
    {
        AppendConsoleText("[Navigation AutoRun] " + message, level);
    }
}
