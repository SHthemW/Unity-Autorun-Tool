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
        RenderNavigationTargetStatus();
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
                    SelectNavigationTarget(nextIndex);
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
            SyncNavigationTargetIndexFromSelectedTarget();
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

        SelectNavigationTarget(_navigationTargetIndex);
        NavigationAutoRunOption target = _navigationFilteredTargets[_navigationTargetIndex];
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
        if (SyncNavigationTargetIndexFromSelectedTarget())
        {
            return;
        }

        if (_navigationTargetNames.Length == 0)
        {
            _navigationTargetIndex = 0;
            return;
        }

        if (_navigationTargetIndex >= _navigationTargetNames.Length)
        {
            _navigationTargetIndex = _navigationTargetNames.Length - 1;
        }

        if (string.IsNullOrEmpty(_navigationSelectedTargetViewId))
        {
            SyncSelectedNavigationTarget();
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

    private void RenderNavigationTargetStatus()
    {
        NavigationAutoRunOption currentTarget = GetCurrentNavigationTarget();
        string currentText = currentTarget != null ? currentTarget.DisplayName : "None";
        string savedText = string.IsNullOrEmpty(_navigationSelectedTargetViewId) ? "None" : _navigationSelectedTargetViewId;
        string pendingText = GetNavigationSessionTargetText(currentTarget);

        GUILayout.Label(
            "Current target: " + currentText
            + " | Saved: " + savedText
            + " | Pending: " + pendingText,
            EditorStyles.helpBox);
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

    private void SelectNavigationTarget(int targetIndex)
    {
        if (_navigationFilteredTargets.Count == 0)
        {
            _navigationTargetIndex = 0;
            return;
        }

        _navigationTargetIndex = Mathf.Clamp(targetIndex, 0, _navigationFilteredTargets.Count - 1);
        SyncSelectedNavigationTarget();
    }

    private bool SyncNavigationTargetIndexFromSelectedTarget()
    {
        if (string.IsNullOrEmpty(_navigationSelectedTargetViewId) || _navigationFilteredTargets.Count == 0)
        {
            return false;
        }

        int selectedIndex = _navigationFilteredTargets.FindIndex(target => target.ViewId == _navigationSelectedTargetViewId);
        if (selectedIndex < 0)
        {
            return false;
        }

        _navigationTargetIndex = selectedIndex;
        return true;
    }

    private void SyncSelectedNavigationTarget()
    {
        if (_navigationTargetIndex < 0 || _navigationTargetIndex >= _navigationFilteredTargets.Count)
        {
            return;
        }

        string selectedTargetViewId = _navigationFilteredTargets[_navigationTargetIndex].ViewId;
        if (_navigationSelectedTargetViewId == selectedTargetViewId)
        {
            return;
        }

        _navigationSelectedTargetViewId = selectedTargetViewId;
        EditorPrefs.SetString(NavigationSelectedTargetKey, _navigationSelectedTargetViewId);
        LogNavigation("Saved selected target: " + _navigationSelectedTargetViewId);
    }

    private NavigationAutoRunOption GetCurrentNavigationTarget()
    {
        if (_pendingNavigationTarget != null)
        {
            return _pendingNavigationTarget;
        }

        if (NavigationAutoRunSession.HasActiveRequest && !string.IsNullOrEmpty(NavigationAutoRunSession.ActiveTargetViewId))
        {
            string activeName = string.IsNullOrEmpty(NavigationAutoRunSession.ActiveTargetName)
                ? NavigationAutoRunSession.ActiveTargetViewId
                : NavigationAutoRunSession.ActiveTargetName;
            return new NavigationAutoRunOption
            {
                ViewId = NavigationAutoRunSession.ActiveTargetViewId,
                Name = activeName,
                DisplayName = activeName + " (" + NavigationAutoRunSession.ActiveTargetViewId + ")",
            };
        }

        if (_navigationTargetIndex >= 0 && _navigationTargetIndex < _navigationFilteredTargets.Count)
        {
            return _navigationFilteredTargets[_navigationTargetIndex];
        }

        if (string.IsNullOrEmpty(_navigationSelectedTargetViewId))
        {
            return null;
        }

        NavigationAutoRunOption savedTarget = _navigationTargets.Find(target => target.ViewId == _navigationSelectedTargetViewId);
        if (savedTarget != null)
        {
            return savedTarget;
        }

        return new NavigationAutoRunOption
        {
            ViewId = _navigationSelectedTargetViewId,
            Name = _navigationSelectedTargetViewId,
            DisplayName = _navigationSelectedTargetViewId,
        };
    }

    private string GetNavigationSessionTargetText(NavigationAutoRunOption currentTarget)
    {
        if (NavigationAutoRunSession.HasPending)
        {
            return NavigationAutoRunSession.TargetName + " (" + NavigationAutoRunSession.TargetViewId + ")";
        }

        if (NavigationAutoRunSession.HasActiveRequest && !string.IsNullOrEmpty(NavigationAutoRunSession.ActiveTargetViewId))
        {
            string activeName = string.IsNullOrEmpty(NavigationAutoRunSession.ActiveTargetName)
                ? NavigationAutoRunSession.ActiveTargetViewId
                : NavigationAutoRunSession.ActiveTargetName;
            return activeName + " (" + NavigationAutoRunSession.ActiveTargetViewId + ")";
        }

        if (_navigationRunning && currentTarget != null)
        {
            return currentTarget.DisplayName;
        }

        return "None";
    }

    private NavigationAutoRunOption RestorePendingNavigationTarget()
    {
        string targetViewId = NavigationAutoRunSession.TargetViewId;
        if (string.IsNullOrEmpty(targetViewId))
        {
            return null;
        }

        string targetName = NavigationAutoRunSession.TargetName;
        if (string.IsNullOrEmpty(targetName))
        {
            targetName = targetViewId;
        }

        var target = new NavigationAutoRunOption
        {
            ViewId = targetViewId,
            Name = targetName,
            DisplayName = targetName + " (" + targetViewId + ")",
        };
        _pendingNavigationTarget = target;
        _navigationSelectedTargetViewId = target.ViewId;
        EditorPrefs.SetString(NavigationSelectedTargetKey, _navigationSelectedTargetViewId);

        int selectedIndex = _navigationFilteredTargets.FindIndex(item => item.ViewId == target.ViewId);
        if (selectedIndex >= 0)
        {
            _navigationTargetIndex = selectedIndex;
        }

        return target;
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
