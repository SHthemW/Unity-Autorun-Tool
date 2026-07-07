using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class McpNavMapPreviewService
{
    private const string NavMapPath = "mcp/ui-nav-map.json";
    private const string ExampleNavMapPath = "mcp/ui-nav-map.example.json";
    private const string PreviewPath = "mcp/ui-nav-map.preview.html";

    public static bool OpenPreview(out string message)
    {
        try
        {
            string root = McpInstallConfig.GetToolRootDirectory();
            string mapPath = ResolveMapPath(root);
            NavMapGraph graph = NavMapParser.Parse(File.ReadAllText(mapPath));
            string htmlPath = Path.Combine(root, PreviewPath);
            File.WriteAllText(htmlPath, NavMapHtmlRenderer.Render(graph, mapPath), Encoding.UTF8);
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

        throw new FileNotFoundException("Cannot find mcp/ui-nav-map.json or mcp/ui-nav-map.example.json.");
    }
}
