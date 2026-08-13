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
        List<NavigationMapView> views = Views().ToList();
        NavigationMapView view = views.FirstOrDefault(item =>
            item.id == value || item.name == value);
        if (view == null)
        {
            view = views.FirstOrDefault(item =>
                NormalizeViewToken(item.id) == normalized
                || NormalizeViewToken(item.name) == normalized);
        }

        if (view == null)
        {
            view = views
                .Where(item =>
                    IsViewTokenMatch(NormalizeViewToken(item.id), normalized)
                    || IsViewTokenMatch(NormalizeViewToken(item.name), normalized))
                .OrderBy(item => ViewTokenDistance(item, normalized))
                .ThenBy(item => item.id)
                .FirstOrDefault();
        }

        return view != null ? view.id : value;
    }

    private static int ViewTokenDistance(NavigationMapView view, string target)
    {
        string id = NormalizeViewToken(view?.id);
        string name = NormalizeViewToken(view?.name);
        int idDistance = string.IsNullOrEmpty(id) ? int.MaxValue : Math.Abs(id.Length - target.Length);
        int nameDistance = string.IsNullOrEmpty(name) ? int.MaxValue : Math.Abs(name.Length - target.Length);
        return Math.Min(idDistance, nameDistance);
    }

    private string ResolveOpenViewId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = NormalizeViewToken(value);
        NavigationMapView view = Views().FirstOrDefault(item => IsOpenViewMatch(item, normalized));
        return view?.id;
    }

    private static bool IsOpenViewMatch(NavigationMapView view, string normalizedRuntimeToken)
    {
        if (view == null || string.IsNullOrEmpty(normalizedRuntimeToken))
        {
            return false;
        }

        return IsRuntimeTokenMatch(normalizedRuntimeToken, NormalizeViewToken(view.name))
            || IsRuntimeTokenMatch(normalizedRuntimeToken, NormalizeViewToken(view.rootObjectPath))
            || IsRuntimeTokenMatch(normalizedRuntimeToken, NormalizeViewToken(GetObjectPathLeaf(view.rootObjectPath)))
            || IsRuntimeTokenMatch(normalizedRuntimeToken, NormalizeViewToken(GetObjectPathLeaf(view.prefabPath)));
    }

    private static bool IsRuntimeTokenMatch(string runtimeToken, string mapToken)
    {
        if (string.IsNullOrEmpty(runtimeToken) || string.IsNullOrEmpty(mapToken))
        {
            return false;
        }

        return runtimeToken == mapToken
            || (runtimeToken.Length > mapToken.Length && runtimeToken.EndsWith(mapToken));
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
        string waitForViewId = !string.IsNullOrEmpty(automation?.waitForViewId) ? automation.waitForViewId : transition.toViewId;
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
            waitForViewId = ResolveViewRuntimeToken(waitForViewId),
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

    public string ResolveViewRuntimeToken(string viewIdOrName)
    {
        string viewId = ResolveViewId(viewIdOrName);
        if (!_views.TryGetValue(viewId, out NavigationMapView view))
        {
            return viewIdOrName;
        }

        if (!string.IsNullOrEmpty(view.rootObjectPath))
        {
            return view.rootObjectPath;
        }

        return string.IsNullOrEmpty(view.name) ? view.id : view.name;
    }

    private AutoRunParam NormalizeAutoRun(
        NavigationMapAutoRun mapAutoRun,
        NavigationMapControl control)
    {
        AutoRunParam autoRun = ToRuntimeAutoRun(mapAutoRun);
        AutoRunParam normalized;
        if (autoRun == null)
        {
            normalized = CreateAutoRunFromControl(control);
        }
        else
        {
            string objectPathButtonName = GetObjectPathLeaf(control?.objectPath);
            if (!IsDefaultAction(autoRun)
                && !IsFairyGUIControl(control)
                && !string.IsNullOrEmpty(objectPathButtonName))
            {
                normalized = CopyAutoRunWithButtonName(autoRun, objectPathButtonName);
            }
            else if (control == null || !IsDefaultAction(autoRun))
            {
                normalized = CopyAutoRun(autoRun);
            }
            else
            {
                AutoRunParam fallback = CreateAutoRunFromControl(control);
                if (fallback == null)
                {
                    normalized = CopyAutoRun(autoRun);
                }
                else
                {
                    fallback.delay = autoRun.delay;
                    fallback.isTest = autoRun.isTest;
                    normalized = fallback;
                }
            }
        }

        return AddControlSelectorContext(normalized, control);
    }

    private static AutoRunParam ToRuntimeAutoRun(
        NavigationMapAutoRun autoRun)
    {
        if (autoRun == null)
        {
            return null;
        }

        return new AutoRunParam
        {
            buttonName = autoRun.buttonName,
            buttonText = autoRun.buttonText,
            isFairyGUI = autoRun.isFairyGUI,
            delay = autoRun.delay,
            isTest = autoRun.isTest,
            objectPath = autoRun.objectPath,
            scopeRootName = autoRun.scopeRootName,
            matchPolicy = autoRun.matchPolicy,
        };
    }

    private AutoRunParam AddControlSelectorContext(
        AutoRunParam autoRun,
        NavigationMapControl control)
    {
        if (autoRun == null || control == null || autoRun.isFairyGUI)
        {
            return autoRun;
        }

        autoRun.objectPath = string.IsNullOrWhiteSpace(autoRun.objectPath)
            ? control.objectPath
            : autoRun.objectPath;
        string scopeRootName = ResolveControlScopeRootName(control);
        if (string.IsNullOrWhiteSpace(autoRun.scopeRootName))
        {
            autoRun.scopeRootName = scopeRootName;
        }

        if (string.IsNullOrWhiteSpace(autoRun.matchPolicy))
        {
            autoRun.matchPolicy = AutoRunParam.MATCH_UNIQUE;
        }

        return autoRun;
    }

    private static AutoRunParam CopyAutoRunWithButtonName(AutoRunParam autoRun, string buttonName)
    {
        AutoRunParam copy = CopyAutoRun(autoRun);
        copy.buttonName = buttonName;
        return copy;
    }

    private static AutoRunParam CopyAutoRun(AutoRunParam autoRun)
    {
        if (autoRun == null)
        {
            return null;
        }

        return new AutoRunParam
        {
            buttonName = autoRun.buttonName,
            buttonText = autoRun.buttonText,
            isFairyGUI = autoRun.isFairyGUI,
            delay = autoRun.delay,
            isTest = autoRun.isTest,
            objectPath = autoRun.objectPath,
            scopeRootName = autoRun.scopeRootName,
            matchPolicy = autoRun.matchPolicy,
        };
    }

    private string ResolveControlScopeRootName(NavigationMapControl control)
    {
        if (control == null
            || string.IsNullOrWhiteSpace(control.viewId)
            || !_views.TryGetValue(control.viewId, out NavigationMapView view))
        {
            return null;
        }

        string rootName = GetObjectPathLeaf(view.rootObjectPath);
        return string.IsNullOrWhiteSpace(rootName) ? view.name : rootName;
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
        return autoRun == null
            || string.IsNullOrEmpty(autoRun.buttonName)
            || autoRun.buttonName == AutoRunParam.DEFAULT_NAME;
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

        if (token.EndsWith(".prefab"))
        {
            token = token.Substring(0, token.Length - ".prefab".Length);
        }

        return token
            .Replace(".", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("/", string.Empty)
            .Replace("\\", string.Empty)
            .Replace(" ", string.Empty);
    }

    private static bool IsViewTokenMatch(string candidate, string target)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
        {
            return false;
        }

        return candidate == target
            || (target.Length >= 4 && candidate.EndsWith(target))
            || (candidate.Length >= 4 && target.EndsWith(candidate));
    }
}
