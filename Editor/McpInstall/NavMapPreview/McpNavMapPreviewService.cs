using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class McpNavMapPreviewService
{
    private const string NavMapPath = "Gen/ui-nav-map.json";
    private const string ExampleNavMapPath = "Gen/ui-nav-map.example.json";
    private const string PreviewPath = "Gen/ui-nav-map.preview.html";
    private const string PreviewStylesPath =
        "Editor/McpInstall/NavMapPreview/Web~/nav-map-preview.css";
    private const string VizScriptPath =
        "Editor/McpInstall/NavMapPreview/Web~/vendor/viz-js/viz-global.js";
    private const string PreviewScriptPath =
        "Editor/McpInstall/NavMapPreview/Web~/nav-map-preview.js";

    public static bool OpenPreview(out string message)
    {
        try
        {
            string htmlPath = GeneratePreviewFile();
            Application.OpenURL(new Uri(htmlPath).AbsoluteUri);
            message = "Opened nav map preview: " + htmlPath;
            return true;
        }
        catch (Exception ex)
        {
            message = "Nav map preview failed: " + ex.Message;
            return false;
        }
    }

    internal static string GeneratePreviewFile()
    {
        string root = McpInstallConfig.GetToolRootDirectory();
        string mapPath = ResolveMapPath(root);
        string previewStylesPath = ResolvePreviewAsset(root, PreviewStylesPath);
        string vizScriptPath = ResolvePreviewAsset(root, VizScriptPath);
        string previewScriptPath = ResolvePreviewAsset(root, PreviewScriptPath);
        NavMapGraph graph = NavMapParser.Parse(File.ReadAllText(mapPath));
        string htmlPath = Path.Combine(root, PreviewPath);
        string htmlDirectory = Path.GetDirectoryName(htmlPath);
        if (!string.IsNullOrEmpty(htmlDirectory))
        {
            Directory.CreateDirectory(htmlDirectory);
        }

        string html = NavMapHtmlRenderer.Render(
            graph,
            mapPath,
            MakeRelativeUrl(htmlPath, previewStylesPath),
            MakeRelativeUrl(htmlPath, vizScriptPath),
            MakeRelativeUrl(htmlPath, previewScriptPath));
        File.WriteAllText(htmlPath, html, Encoding.UTF8);
        return htmlPath;
    }

    private static string ResolveMapPath(string root)
    {
        string mapPath = Path.Combine(root, NavMapPath);
        if (File.Exists(mapPath))
        {
            return mapPath;
        }

        string examplePath = Path.Combine(root, ExampleNavMapPath);
        if (File.Exists(examplePath))
        {
            return examplePath;
        }

        throw new FileNotFoundException("Cannot find Gen/ui-nav-map.json or Gen/ui-nav-map.example.json.");
    }

    private static string ResolvePreviewAsset(string root, string relativePath)
    {
        string path = Path.Combine(root, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Cannot find nav map preview asset: " + path);
        }

        return path;
    }

    private static string MakeRelativeUrl(string htmlPath, string assetPath)
    {
        string htmlDirectory = Path.GetDirectoryName(htmlPath);
        if (string.IsNullOrEmpty(htmlDirectory))
        {
            throw new InvalidOperationException("Cannot resolve nav map preview directory.");
        }

        string basePath = htmlDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var baseUri = new Uri(basePath);
        var assetUri = new Uri(assetPath);
        return baseUri.MakeRelativeUri(assetUri).ToString();
    }
}
