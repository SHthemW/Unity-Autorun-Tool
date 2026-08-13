using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class NavigationAutoRunMap
{
    private static NavigationMapDocument ParseDocument(
        string json,
        string path)
    {
        try
        {
            Dictionary<string, object> root = RequireObject(
                AutoRunJsonParser.Parse(json),
                "navigation map root");
            return new NavigationMapDocument
            {
                schemaVersion = Text(root, "schemaVersion"),
                generatorVersion = Text(root, "generatorVersion"),
                mapVersion = Integer(root, "mapVersion"),
                generation = ParseGeneration(Object(root, "generation")),
                views = ParseArray(root, "views", ParseView).ToArray(),
                controls = ParseArray(root, "controls", ParseControl).ToArray(),
                transitions = ParseArray(root, "transitions", ParseTransition).ToArray(),
                routes = ParseArray(root, "routes", ParseRoute).ToArray(),
            };
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                "Invalid navigation map JSON: " + path + ". " + ex.Message,
                ex);
        }
    }

    private static NavigationMapGeneration ParseGeneration(
        Dictionary<string, object> value)
    {
        if (value == null)
        {
            return null;
        }

        return new NavigationMapGeneration
        {
            status = Text(value, "status"),
            candidateProtocolVersion = Text(value, "candidateProtocolVersion"),
            candidateSetVersion = Text(value, "candidateSetVersion"),
            candidateCount = Integer(value, "candidateCount"),
            reviewedCandidateCount = Integer(value, "reviewedCandidateCount"),
            completedAt = Text(value, "completedAt"),
        };
    }

    private static NavigationMapView ParseView(
        Dictionary<string, object> value)
    {
        return new NavigationMapView
        {
            id = Text(value, "id"),
            name = Text(value, "name"),
            rootObjectPath = Text(value, "rootObjectPath"),
            prefabPath = Text(value, "prefabPath"),
            framework = Text(value, "framework"),
        };
    }

    private static NavigationMapControl ParseControl(
        Dictionary<string, object> value)
    {
        return new NavigationMapControl
        {
            id = Text(value, "id"),
            viewId = Text(value, "viewId"),
            name = Text(value, "name"),
            text = Text(value, "text"),
            objectPath = Text(value, "objectPath"),
            framework = Text(value, "framework"),
            autoRun = ParseAutoRun(Object(value, "autoRun")),
            source = ParseSource(Object(value, "source")),
        };
    }

    private static NavigationMapAutoRun ParseAutoRun(
        Dictionary<string, object> value)
    {
        if (value == null)
        {
            return null;
        }

        return new NavigationMapAutoRun
        {
            buttonName = Text(value, "buttonName"),
            buttonText = Text(value, "buttonText"),
            isFairyGUI = Boolean(value, "isFairyGUI"),
            delay = Single(value, "delay"),
            isTest = Boolean(value, "isTest"),
            objectPath = Text(value, "objectPath"),
            scopeRootName = Text(value, "scopeRootName"),
            matchPolicy = Text(value, "matchPolicy"),
            matchPolicyEvidence = Text(value, "matchPolicyEvidence"),
        };
    }

    private static NavigationMapSource ParseSource(
        Dictionary<string, object> value)
    {
        return value == null
            ? null
            : new NavigationMapSource { type = Text(value, "type") };
    }

    private static NavigationMapTransition ParseTransition(
        Dictionary<string, object> value)
    {
        return new NavigationMapTransition
        {
            id = Text(value, "id"),
            fromViewId = Text(value, "fromViewId"),
            controlId = Text(value, "controlId"),
            toViewId = Text(value, "toViewId"),
            kind = Text(value, "kind"),
            automation = ParseAutomation(Object(value, "automation")),
        };
    }

    private static NavigationMapAutomation ParseAutomation(
        Dictionary<string, object> value)
    {
        if (value == null)
        {
            return null;
        }

        return new NavigationMapAutomation
        {
            mode = Text(value, "mode"),
            waitForViewId = Text(value, "waitForViewId"),
            timeout = Single(value, "timeout"),
            autoRun = ParseAutoRun(Object(value, "autoRun")),
        };
    }

    private static NavigationMapRoute ParseRoute(
        Dictionary<string, object> value)
    {
        return new NavigationMapRoute
        {
            id = Text(value, "id"),
            fromViewId = Text(value, "fromViewId"),
            toViewId = Text(value, "toViewId"),
            steps = ParseArray(value, "steps", ParseRouteStep).ToArray(),
        };
    }

    private static NavigationMapRouteStep ParseRouteStep(
        Dictionary<string, object> value)
    {
        return new NavigationMapRouteStep
        {
            transitionId = Text(value, "transitionId"),
            controlId = Text(value, "controlId"),
        };
    }

    private static List<T> ParseArray<T>(
        Dictionary<string, object> owner,
        string key,
        Func<Dictionary<string, object>, T> parse)
    {
        var result = new List<T>();
        if (owner == null
            || !owner.TryGetValue(key, out object raw)
            || raw == null)
        {
            return result;
        }

        if (!(raw is List<object> array))
        {
            throw new FormatException("Expected '" + key + "' to be a JSON array.");
        }

        for (int index = 0; index < array.Count; index++)
        {
            result.Add(parse(RequireObject(array[index], key + "[" + index + "]")));
        }

        return result;
    }

    private static Dictionary<string, object> Object(
        Dictionary<string, object> owner,
        string key)
    {
        if (owner == null
            || !owner.TryGetValue(key, out object value)
            || value == null)
        {
            return null;
        }

        return RequireObject(value, key);
    }

    private static Dictionary<string, object> RequireObject(
        object value,
        string context)
    {
        if (value is Dictionary<string, object> result)
        {
            return result;
        }

        throw new FormatException("Expected " + context + " to be a JSON object.");
    }

    private static string Text(
        Dictionary<string, object> owner,
        string key)
    {
        return owner != null
            && owner.TryGetValue(key, out object value)
            && value is string text
                ? text
                : null;
    }

    private static int Integer(
        Dictionary<string, object> owner,
        string key)
    {
        if (owner == null || !owner.TryGetValue(key, out object value) || value == null)
        {
            return 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static float Single(
        Dictionary<string, object> owner,
        string key)
    {
        if (owner == null || !owner.TryGetValue(key, out object value) || value == null)
        {
            return 0f;
        }

        return Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    private static bool Boolean(
        Dictionary<string, object> owner,
        string key)
    {
        return owner != null
            && owner.TryGetValue(key, out object value)
            && value is bool result
            && result;
    }
}
