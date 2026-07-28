using System.Text;
using UnityEngine;

public static class NavMapHtmlRenderer
{
    public static string Render(
        NavMapGraph graph,
        string sourcePath,
        string stylesheetUrl,
        string vizScriptUrl,
        string previewScriptUrl)
    {
        NavMapPreviewModel model = NavMapPreviewModel.Create(graph, sourcePath);
        string modelJson = EscapeJsonForHtmlScript(JsonUtility.ToJson(model));
        var builder = new StringBuilder(modelJson.Length + 5000);

        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        builder.AppendLine("  <title>Unity AutoRun Nav Map</title>");
        builder.Append("  <link rel=\"stylesheet\" href=\"")
            .Append(Html(stylesheetUrl))
            .AppendLine("\">");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        AppendHeader(builder);
        AppendToolbar(builder);
        AppendWorkspace(builder);
        builder.AppendLine("  <div id=\"nav-tooltip\" class=\"nav-tooltip\" role=\"tooltip\" hidden></div>");
        builder.AppendLine("  <script id=\"nav-map-data\" type=\"application/json\">");
        builder.AppendLine(modelJson);
        builder.AppendLine("  </script>");
        builder.Append("  <script src=\"")
            .Append(Html(vizScriptUrl))
            .AppendLine("\"></script>");
        builder.Append("  <script src=\"")
            .Append(Html(previewScriptUrl))
            .AppendLine("\"></script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("  <header class=\"app-header\">");
        builder.AppendLine("    <div class=\"title-block\">");
        builder.AppendLine("      <div class=\"eyebrow\">UNITY AUTORUN</div>");
        builder.AppendLine("      <h1>Navigation Map</h1>");
        builder.AppendLine("      <div id=\"source-path\" class=\"source-path\"></div>");
        builder.AppendLine("    </div>");
        builder.AppendLine("    <div class=\"stats\" aria-label=\"Map statistics\">");
        builder.AppendLine("      <div class=\"stat\"><strong id=\"stat-views\">0</strong><span>Views</span></div>");
        builder.AppendLine("      <div class=\"stat\"><strong id=\"stat-transitions\">0</strong><span>Transitions</span></div>");
        builder.AppendLine("      <div class=\"stat\"><strong id=\"stat-connected\">0</strong><span>Connected</span></div>");
        builder.AppendLine("      <div class=\"stat\"><strong id=\"stat-unconnected\">0</strong><span>Unconnected</span></div>");
        builder.AppendLine("    </div>");
        builder.AppendLine("  </header>");
    }

    private static void AppendToolbar(StringBuilder builder)
    {
        builder.AppendLine("  <div class=\"toolbar\">");
        builder.AppendLine("    <label class=\"search-box\" for=\"view-search\">");
        builder.AppendLine("      <span class=\"search-icon\" aria-hidden=\"true\"></span>");
        builder.AppendLine("      <input id=\"view-search\" type=\"search\" autocomplete=\"off\" spellcheck=\"false\" placeholder=\"Search view name, id, or prefab path...\">");
        builder.AppendLine("      <kbd>/</kbd>");
        builder.AppendLine("    </label>");
        builder.AppendLine("    <div class=\"toolbar-actions\">");
        builder.AppendLine("      <button id=\"zoom-out\" class=\"icon-button\" type=\"button\" title=\"Zoom out\" aria-label=\"Zoom out\">&minus;</button>");
        builder.AppendLine("      <button id=\"zoom-in\" class=\"icon-button\" type=\"button\" title=\"Zoom in\" aria-label=\"Zoom in\">+</button>");
        builder.AppendLine("      <button id=\"fit-graph\" type=\"button\">Fit</button>");
        builder.AppendLine("      <button id=\"clear-selection\" type=\"button\">Clear focus</button>");
        builder.AppendLine("      <label class=\"toggle\"><input id=\"edge-label-toggle\" type=\"checkbox\"><span>All edge labels</span></label>");
        builder.AppendLine("      <span id=\"zoom-level\" class=\"zoom-level\">100%</span>");
        builder.AppendLine("    </div>");
        builder.AppendLine("  </div>");
    }

    private static void AppendWorkspace(StringBuilder builder)
    {
        builder.AppendLine("  <main class=\"workspace\">");
        builder.AppendLine("    <section class=\"graph-pane\" aria-label=\"Navigation graph\">");
        builder.AppendLine("      <div id=\"graph-host\" class=\"graph-host\" tabindex=\"0\">");
        builder.AppendLine("        <div id=\"graph-status\" class=\"graph-status is-loading\">");
        builder.AppendLine("          <span class=\"spinner\" aria-hidden=\"true\"></span>");
        builder.AppendLine("          <span id=\"graph-status-text\">Laying out navigation graph...</span>");
        builder.AppendLine("        </div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"graph-hint\">Drag the background to pan &middot; Scroll to zoom &middot; Select a node to isolate its routes</div>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <aside class=\"inspector\" aria-label=\"Navigation details\">");
        builder.AppendLine("      <section id=\"search-section\" class=\"panel-section\" hidden>");
        builder.AppendLine("        <div class=\"section-heading\"><h2>Search results</h2><span id=\"search-count\" class=\"count-badge\">0</span></div>");
        builder.AppendLine("        <div id=\"search-results\" class=\"item-list search-results\"></div>");
        builder.AppendLine("      </section>");
        builder.AppendLine("      <section class=\"panel-section details-section\">");
        builder.AppendLine("        <div class=\"section-heading\"><h2>Details</h2></div>");
        builder.AppendLine("        <div id=\"selection-details\" class=\"selection-details\"></div>");
        builder.AppendLine("      </section>");
        builder.AppendLine("      <details class=\"panel-section unconnected-section\" open>");
        builder.AppendLine("        <summary><span>Unconnected views</span><span id=\"unconnected-count\" class=\"count-badge\">0</span></summary>");
        builder.AppendLine("        <p class=\"section-copy\">Views without a valid transition stay outside the main graph to keep connected routes readable.</p>");
        builder.AppendLine("        <div id=\"unconnected-views\" class=\"item-list unconnected-list\"></div>");
        builder.AppendLine("      </details>");
        builder.AppendLine("    </aside>");
        builder.AppendLine("  </main>");
        builder.AppendLine("  <noscript>This preview requires JavaScript.</noscript>");
    }

    private static string EscapeJsonForHtmlScript(string json)
    {
        return json
            .Replace("&", "\\u0026")
            .Replace("<", "\\u003c")
            .Replace(">", "\\u003e")
            .Replace("\u2028", "\\u2028")
            .Replace("\u2029", "\\u2029");
    }

    private static string Html(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
