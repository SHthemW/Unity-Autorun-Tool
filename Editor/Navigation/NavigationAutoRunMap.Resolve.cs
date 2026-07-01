using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class NavigationAutoRunMap
{
    private NavigationMapRoute ResolveRoute(string fromViewId, string toViewId)
    {
        return ResolveBestRouteObject(fromViewId, toViewId);
    }

    private string ResolveViewId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = NormalizeViewToken(value);
        NavigationMapView view = Views().FirstOrDefault(item =>
            item.id == value
            || item.name == value
            || NormalizeViewToken(item.id) == normalized
            || NormalizeViewToken(item.name) == normalized);
        return view != null ? view.id : value;
    }

    private AutoRunNavStep ResolveNavigationStep(NavigationMapRouteStep step)
    {
        NavigationMapTransition transition = FindStepTransition(step);
        if (transition == null)
        {
            throw new InvalidOperationException("Transition not found: " + step.transitionId);
        }

        AutoRunParam autoRun = ResolveStepAutoRun(step, transition);
        NavigationMapAutomation automation = transition.automation;
        string mode = !string.IsNullOrEmpty(automation?.mode) ? automation.mode : (autoRun != null ? "click" : "wait");
        var navStep = new AutoRunNavStep
        {
            transitionId = step.transitionId,
            controlId = !string.IsNullOrEmpty(step.controlId) ? step.controlId : transition.controlId,
            fromViewId = transition.fromViewId,
            toViewId = transition.toViewId,
            kind = string.IsNullOrEmpty(transition.kind) ? "interaction" : transition.kind,
            mode = mode,
            isAutoRunnable = autoRun != null,
            action = autoRun,
            waitForViewId = !string.IsNullOrEmpty(automation?.waitForViewId) ? automation.waitForViewId : transition.toViewId,
        };
        if (automation != null && automation.timeout > 0f)
        {
            navStep.timeout = automation.timeout;
        }

        return navStep;
    }

    private AutoRunParam ResolveStepAutoRun(NavigationMapRouteStep step, NavigationMapTransition transition)
    {
        string controlId = !string.IsNullOrEmpty(step.controlId) ? step.controlId : transition.controlId;
        NavigationMapControl control = null;
        if (!string.IsNullOrEmpty(controlId))
        {
            _controls.TryGetValue(controlId, out control);
        }

        if (transition.automation != null && transition.automation.autoRun != null)
        {
            AutoRunParam autoRun = NormalizeAutoRun(transition.automation.autoRun, control);
            return IsDefaultAction(autoRun) ? null : autoRun;
        }

        if (control == null)
        {
            return null;
        }

        AutoRunParam controlAutoRun = NormalizeAutoRun(control.autoRun, control);
        return IsDefaultAction(controlAutoRun) ? null : controlAutoRun;
    }

    private static AutoRunParam NormalizeAutoRun(AutoRunParam autoRun, NavigationMapControl control)
    {
        if (autoRun == null)
        {
            return CreateAutoRunFromControl(control);
        }

        string objectPathButtonName = GetObjectPathLeaf(control?.objectPath);
        if (!IsDefaultAction(autoRun) && !IsFairyGUIControl(control) && !string.IsNullOrEmpty(objectPathButtonName))
        {
            return CopyAutoRunWithButtonName(autoRun, objectPathButtonName);
        }

        if (control == null || !IsDefaultAction(autoRun))
        {
            return autoRun;
        }

        AutoRunParam fallback = CreateAutoRunFromControl(control);
        if (fallback == null)
        {
            return autoRun;
        }

        fallback.delay = autoRun.delay;
        fallback.isTest = autoRun.isTest;
        return fallback;
    }

    private static AutoRunParam CopyAutoRunWithButtonName(AutoRunParam autoRun, string buttonName)
    {
        return new AutoRunParam
        {
            buttonName = buttonName,
            buttonText = autoRun.buttonText,
            isFairyGUI = autoRun.isFairyGUI,
            delay = autoRun.delay,
            isTest = autoRun.isTest,
        };
    }

    private static bool IsFairyGUIControl(NavigationMapControl control)
    {
        return string.Equals(control?.framework, "fairygui", StringComparison.OrdinalIgnoreCase);
    }

    private static AutoRunParam CreateAutoRunFromControl(NavigationMapControl control)
    {
        string buttonName = ResolveAutoRunButtonName(control);
        if (string.IsNullOrEmpty(buttonName))
        {
            return null;
        }

        return new AutoRunParam
        {
            buttonName = buttonName,
            buttonText = string.IsNullOrEmpty(control.text) ? AutoRunParam.DEFAULT_TEXT : control.text,
            isFairyGUI = !string.IsNullOrEmpty(control.framework) && control.framework.ToLowerInvariant() == "fairygui",
        };
    }

    private static string ResolveAutoRunButtonName(NavigationMapControl control)
    {
        if (control == null)
        {
            return null;
        }

        string pathLeaf = GetObjectPathLeaf(control.objectPath);
        return string.IsNullOrEmpty(pathLeaf) ? control.name : pathLeaf;
    }

    private static string GetObjectPathLeaf(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return null;
        }

        string normalized = objectPath.Replace('\\', '/').Trim('/');
        int slashIndex = normalized.LastIndexOf('/');
        return slashIndex >= 0 ? normalized.Substring(slashIndex + 1) : normalized;
    }

    private static bool IsDefaultAction(AutoRunParam autoRun)
    {
        return string.IsNullOrEmpty(autoRun.buttonName) || autoRun.buttonName == AutoRunParam.DEFAULT_NAME;
    }

    private NavigationMapTransition FindStepTransition(NavigationMapRouteStep step)
    {
        if (step == null || string.IsNullOrEmpty(step.transitionId))
        {
            return null;
        }

        return _transitions.TryGetValue(step.transitionId, out NavigationMapTransition transition) ? transition : null;
    }

    private NavigationMapRoute TryResolveRoute(string fromViewId, string toViewId)
    {
        try
        {
            return ResolveRoute(fromViewId, toViewId);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private IEnumerable<NavigationMapView> Views()
    {
        return _document.views ?? new NavigationMapView[0];
    }

    private IEnumerable<NavigationMapTransition> Transitions()
    {
        return _document.transitions ?? new NavigationMapTransition[0];
    }

    private IEnumerable<NavigationMapRoute> Routes()
    {
        return _document.routes ?? new NavigationMapRoute[0];
    }

    private static IReadOnlyList<NavigationMapRouteStep> Steps(NavigationMapRoute route)
    {
        return route.steps ?? new NavigationMapRouteStep[0];
    }

    private static string NormalizeViewToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string token = value.ToLowerInvariant();
        if (token.StartsWith("view."))
        {
            token = token.Substring("view.".Length);
        }

        return token
            .Replace("ui.form.", "uiform")
            .Replace(".", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }
}
