using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class AutoRunBridgeEndpointState
{
    private const string StateDirectoryName = "UnityAutorunTool";
    private const string StateFileName = "bridge-endpoint.json";

    public static string StateFilePath
    {
        get
        {
            return Path.Combine(GetProjectRootDirectory(), "Library", StateDirectoryName, StateFileName);
        }
    }

    public static void Publish(string host, int port)
    {
        string projectRoot = GetProjectRootDirectory();
        string path = StateFilePath;
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);

        var state = new EndpointState
        {
            host = host,
            port = port,
            url = $"http://{host}:{port}/",
            processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            projectRoot = projectRoot.Replace('\\', '/'),
            updatedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        string temporaryPath = path + "." + state.processId + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(state), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void Clear()
    {
        try
        {
            string path = StateFilePath;
            if (!File.Exists(path))
            {
                return;
            }

            EndpointState state = JsonUtility.FromJson<EndpointState>(File.ReadAllText(path));
            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            if (state != null && state.processId == processId)
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Runtime discovery cleanup must not prevent the bridge from stopping.
        }
    }

    private static string GetProjectRootDirectory()
    {
        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        if (projectRoot == null)
        {
            throw new InvalidOperationException("Cannot resolve the Unity project root directory.");
        }

        return projectRoot.FullName;
    }

    [Serializable]
    private sealed class EndpointState
    {
        public string host;
        public int port;
        public string url;
        public int processId;
        public string projectRoot;
        public string updatedAtUtc;
    }
}
