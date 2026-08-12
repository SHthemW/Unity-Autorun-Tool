using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[InitializeOnLoad]
public static class AutoRunUpdateCheckService
{
    public const string RepositoryUrl =
        "https://github.com/SHthemW/Unity-Autorun-Tool";
    public const string LatestPackageUrl =
        "https://raw.githubusercontent.com/SHthemW/Unity-Autorun-Tool/master/package.json";

    private const double SuccessIntervalHours = 24d;
    private const double FailureRetryIntervalHours = 1d;
    private const int RequestTimeoutSeconds = 15;
    private const string LatestVersionKey =
        "UnityAutorunTool.Update.LatestVersion";
    private const string LastAttemptUtcKey =
        "UnityAutorunTool.Update.LastAttemptUtc";
    private const string LastSuccessUtcKey =
        "UnityAutorunTool.Update.LastSuccessUtc";
    private const string LastErrorKey =
        "UnityAutorunTool.Update.LastError";

    private static UnityWebRequest _request;

    static AutoRunUpdateCheckService()
    {
        EditorApplication.delayCall += CheckIfDue;
        AssemblyReloadEvents.beforeAssemblyReload += DisposeActiveRequest;
        EditorApplication.quitting += DisposeActiveRequest;
    }

    public static event Action StatusChanged;

    public static bool IsChecking { get; private set; }

    public static string LatestVersion
    {
        get { return EditorPrefs.GetString(LatestVersionKey, ""); }
    }

    public static string LastError
    {
        get { return EditorPrefs.GetString(LastErrorKey, ""); }
    }

    public static DateTime? LastSuccessfulCheckUtc
    {
        get { return ReadUtc(LastSuccessUtcKey); }
    }

    public static bool IsUpdateAvailable
    {
        get
        {
            return AutoRunVersionComparer.TryCompare(
                    LatestVersion,
                    McpInstallConfig.GetToolVersion(),
                    out int comparison)
                && comparison > 0;
        }
    }

    public static void CheckIfDue()
    {
        if (Application.isBatchMode || IsChecking)
        {
            return;
        }

        DateTime? lastAttemptUtc = ReadUtc(LastAttemptUtcKey);
        double intervalHours = string.IsNullOrEmpty(LastError)
            ? SuccessIntervalHours
            : FailureRetryIntervalHours;
        if (lastAttemptUtc.HasValue
            && DateTime.UtcNow - lastAttemptUtc.Value
                < TimeSpan.FromHours(intervalHours))
        {
            return;
        }

        CheckNow();
    }

    public static void CheckNow()
    {
        if (Application.isBatchMode || IsChecking)
        {
            return;
        }

        try
        {
            IsChecking = true;
            _request = UnityWebRequest.Get(LatestPackageUrl);
            _request.timeout = RequestTimeoutSeconds;
            UnityWebRequestAsyncOperation operation = _request.SendWebRequest();
            operation.completed += OnRequestCompleted;
            NotifyChanged();
        }
        catch (Exception ex)
        {
            FinishWithError(ex.Message);
        }
    }

    private static void OnRequestCompleted(AsyncOperation operation)
    {
        UnityWebRequest request = _request;
        _request = null;
        string error = "";
        string latestVersion = "";

        try
        {
            if (request == null)
            {
                error = "Update request was lost during an editor reload.";
            }
            else if (request.result != UnityWebRequest.Result.Success)
            {
                error = request.error;
            }
            else
            {
                RemotePackageDocument document =
                    JsonUtility.FromJson<RemotePackageDocument>(
                        request.downloadHandler.text);
                latestVersion = document == null
                    ? ""
                    : (document.version ?? "").Trim();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    error = "Remote package.json does not contain a version.";
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            request?.Dispose();
        }

        DateTime nowUtc = DateTime.UtcNow;
        EditorPrefs.SetString(LastAttemptUtcKey, nowUtc.ToString("o"));
        if (string.IsNullOrEmpty(error))
        {
            EditorPrefs.SetString(LatestVersionKey, latestVersion);
            EditorPrefs.SetString(LastSuccessUtcKey, nowUtc.ToString("o"));
            EditorPrefs.DeleteKey(LastErrorKey);
        }
        else
        {
            EditorPrefs.SetString(LastErrorKey, error);
        }

        IsChecking = false;
        NotifyChanged();
    }

    private static void FinishWithError(string error)
    {
        _request?.Dispose();
        _request = null;
        EditorPrefs.SetString(LastAttemptUtcKey, DateTime.UtcNow.ToString("o"));
        EditorPrefs.SetString(
            LastErrorKey,
            string.IsNullOrEmpty(error) ? "Unknown update-check error." : error);
        IsChecking = false;
        NotifyChanged();
    }

    private static void DisposeActiveRequest()
    {
        UnityWebRequest request = _request;
        _request = null;
        if (request != null)
        {
            try
            {
                request.Abort();
            }
            catch
            {
                // Assembly reload and editor shutdown should not be blocked by request cleanup.
            }
            finally
            {
                request.Dispose();
            }
        }

        IsChecking = false;
    }

    private static DateTime? ReadUtc(string key)
    {
        string value = EditorPrefs.GetString(key, "");
        if (DateTime.TryParse(
                value,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime result))
        {
            return result.ToUniversalTime();
        }

        return null;
    }

    private static void NotifyChanged()
    {
        StatusChanged?.Invoke();
    }

    [Serializable]
    private sealed class RemotePackageDocument
    {
        public string version = "";
    }
}
