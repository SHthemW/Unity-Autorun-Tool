using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class NavigationMapDocument
{
    public string schemaVersion;
    public string generatorVersion;
    public int mapVersion;
    public NavigationMapGeneration generation;
    public NavigationMapView[] views;
    public NavigationMapControl[] controls;
    public NavigationMapTransition[] transitions;
    public NavigationMapRoute[] routes;
}

[Serializable]
public sealed class NavigationMapGeneration
{
    public string status;
    public string candidateProtocolVersion;
    public string candidateSetVersion;
    public int candidateCount;
    public int reviewedCandidateCount;
    public string completedAt;
}

[Serializable]
public sealed class NavigationPackageDocument
{
    public string version;
}

[Serializable]
public sealed class NavigationMapView
{
    public string id;
    public string name;
    public string rootObjectPath;
    public string prefabPath;
    public string framework;
}

[Serializable]
public sealed class NavigationMapControl
{
    public string id;
    public string viewId;
    public string name;
    public string text;
    public string objectPath;
    public string framework;
    public NavigationMapAutoRun autoRun;
    public NavigationMapSource source;
}

[Serializable]
public sealed class NavigationMapAutoRun
{
    public string buttonName;
    public string buttonText;
    public bool isFairyGUI;
    public float delay;
    public bool isTest;
    public string objectPath;
    public string scopeRootName;
    public string matchPolicy;
    public string matchPolicyEvidence;
}

[Serializable]
public sealed class NavigationMapSource
{
    public string type;
}

[Serializable]
public sealed class NavigationMapTransition
{
    public string id;
    public string fromViewId;
    public string controlId;
    public string toViewId;
    public string kind;
    public NavigationMapAutomation automation;
}

[Serializable]
public sealed class NavigationMapAutomation
{
    public string mode;
    public string waitForViewId;
    public float timeout;
    public NavigationMapAutoRun autoRun;
}

[Serializable]
public sealed class NavigationMapRoute
{
    public string id;
    public string fromViewId;
    public string toViewId;
    public NavigationMapRouteStep[] steps;
}

[Serializable]
public sealed class NavigationMapRouteStep
{
    public string transitionId;
    public string controlId;
}

public sealed class NavigationAutoRunOption
{
    public string ViewId;
    public string Name;
    public string DisplayName;
    public string RuntimeToken;
}

public sealed class NavigationAutoRunPlan
{
    public string RouteId;
    public string FromViewId;
    public string ToViewId;
    public List<AutoRunNavStep> Steps = new List<AutoRunNavStep>();
}

public static class NavigationAutoRunLog
{
    private const int MaxOpenViewSamples = 20;
    private const int MaxOpenViewNameLength = 80;
    private const int MaxStepSamples = 8;

    public static string FormatOpenViews(IReadOnlyList<string> openViews)
    {
        if (openViews == null || openViews.Count == 0)
        {
            return "count=0";
        }

        int sampleCount = openViews.Count < MaxOpenViewSamples ? openViews.Count : MaxOpenViewSamples;
        var builder = new StringBuilder();
        builder.Append("count=").Append(openViews.Count).Append(", sample=[");
        for (int i = 0; i < sampleCount; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(TrimName(openViews[i]));
        }

        if (openViews.Count > sampleCount)
        {
            builder.Append(", ... +").Append(openViews.Count - sampleCount);
        }

        builder.Append("]");
        return builder.ToString();
    }

    public static string FormatSteps(IReadOnlyList<AutoRunNavStep> steps)
    {
        if (steps == null || steps.Count == 0)
        {
            return "count=0";
        }

        int sampleCount = steps.Count < MaxStepSamples ? steps.Count : MaxStepSamples;
        var builder = new StringBuilder();
        builder.Append("count=").Append(steps.Count).Append(", sample=[");
        for (int i = 0; i < sampleCount; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            AutoRunNavStep step = steps[i];
            builder.Append(step.transitionId)
                .Append(" mode=").Append(step.mode)
                .Append(" control=").Append(Safe(step.controlId))
                .Append(" action=").Append(step.action == null ? "null" : Safe(step.action.buttonName));
            if (step.action != null)
            {
                builder.Append(" policy=").Append(Safe(step.action.matchPolicy))
                    .Append(" scope=").Append(Safe(step.action.scopeRootName))
                    .Append(" path=").Append(Safe(step.action.objectPath));
            }
        }

        if (steps.Count > sampleCount)
        {
            builder.Append("; ... +").Append(steps.Count - sampleCount);
        }

        builder.Append("]");
        return builder.ToString();
    }

    private static string TrimName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxOpenViewNameLength)
        {
            return value;
        }

        return value.Substring(0, MaxOpenViewNameLength - 3) + "...";
    }

    private static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "none" : value;
    }
}
