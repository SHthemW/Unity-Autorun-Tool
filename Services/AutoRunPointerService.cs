using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AutoRunPointerService
{
    private const int MaxViewProbeGraphics = 16;

    private static readonly Vector2[] ButtonProbePoints =
    {
        new Vector2(0.5f, 0.5f),
        new Vector2(0.25f, 0.5f),
        new Vector2(0.75f, 0.5f),
        new Vector2(0.5f, 0.25f),
        new Vector2(0.5f, 0.75f),
    };

    private static readonly Vector2[] ViewProbePoints =
    {
        new Vector2(0.5f, 0.5f),
    };

    public static AutoRunButtonResult Click(
        Button button,
        string selectorDescription)
    {
        string selector = string.IsNullOrWhiteSpace(selectorDescription)
            ? button == null ? "unknown" : button.name
            : selectorDescription;
        if (button == null)
        {
            return AutoRunButtonResult.Fail(
                "button_not_found",
                $"err: button '{selector}' not found.");
        }

        if (!button.enabled
            || !button.gameObject.activeInHierarchy
            || !button.IsInteractable())
        {
            return AutoRunButtonResult.Fail(
                "button_not_interactable",
                $"err: button '{selector}' is not active and interactable.");
        }

        if (EventSystem.current == null)
        {
            return AutoRunButtonResult.Fail(
                "event_system_not_ready",
                $"err: button '{selector}' cannot receive a real click because no active EventSystem exists.");
        }

        if (!TryFindButtonHit(
                button,
                out Vector2 screenPosition,
                out RaycastResult hit,
                out string obstruction))
        {
            return AutoRunButtonResult.Fail(
                "button_not_pointer_reachable",
                $"err: button '{selector}' is not the top pointer target. {obstruction}");
        }

        PointerEventData pointer = CreatePointerEvent(
            screenPosition,
            hit);
        GameObject currentOver = hit.gameObject;
        GameObject clickHandler =
            ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                currentOver);
        if (clickHandler != button.gameObject)
        {
            return AutoRunButtonResult.Fail(
                "button_not_pointer_reachable",
                $"err: button '{selector}' is not the click handler at {FormatVector(screenPosition)}.");
        }

        GameObject pressed = ExecuteEvents.ExecuteHierarchy(
            currentOver,
            pointer,
            ExecuteEvents.pointerDownHandler);
        if (pressed == null)
        {
            pressed = clickHandler;
        }

        pointer.pointerPress = pressed;
        pointer.rawPointerPress = currentOver;
        pointer.eligibleForClick = true;
        pointer.clickTime = Time.unscaledTime;
        pointer.clickCount = 1;

        ExecuteEvents.Execute(
            pressed,
            pointer,
            ExecuteEvents.pointerUpHandler);
        bool handled = pressed == clickHandler
            && ExecuteEvents.Execute(
                clickHandler,
                pointer,
                ExecuteEvents.pointerClickHandler);

        pointer.eligibleForClick = false;
        pointer.pointerPress = null;
        pointer.rawPointerPress = null;
        if (!handled)
        {
            return AutoRunButtonResult.Fail(
                "pointer_click_not_handled",
                $"err: button '{selector}' did not handle the pointer click.");
        }

        return AutoRunButtonResult.Success(
            $"button '{selector}' received an EventSystem click at {FormatVector(screenPosition)}. "
            + "Hit: "
            + GetHierarchyPath(currentOver.transform));
    }

    public static bool IsViewForeground(
        GameObject root,
        out bool assessed,
        out string detail)
    {
        assessed = false;
        if (root == null || !root.activeInHierarchy)
        {
            detail = "view root is not active.";
            return false;
        }

        if (EventSystem.current == null)
        {
            detail = "no active EventSystem is available for foreground verification.";
            return false;
        }

        List<Graphic> graphics = root
            .GetComponentsInChildren<Graphic>(false)
            .Where(IsRaycastProbe)
            .OrderByDescending(graphic =>
                graphic.GetComponentInParent<Button>() != null)
            .ThenByDescending(GraphicArea)
            .Take(MaxViewProbeGraphics)
            .ToList();
        if (graphics.Count == 0)
        {
            detail = "view has no active raycastable uGUI graphics.";
            return false;
        }

        assessed = true;
        string blockingPath = null;
        foreach (Graphic graphic in graphics)
        {
            if (!TryFindTopHit(
                    graphic.rectTransform,
                    ViewProbePoints,
                    out _,
                    out RaycastResult hit))
            {
                continue;
            }

            Transform hitTransform = hit.gameObject.transform;
            if (hitTransform == root.transform
                || hitTransform.IsChildOf(root.transform))
            {
                detail = "view owns the top pointer target.";
                return true;
            }

            if (blockingPath == null)
            {
                blockingPath = GetHierarchyPath(hitTransform);
            }
        }

        detail = blockingPath == null
            ? "view has raycastable graphics, but none currently receive pointer hits."
            : "view is covered by pointer target '" + blockingPath + "'.";
        return false;
    }

    private static bool TryFindButtonHit(
        Button button,
        out Vector2 screenPosition,
        out RaycastResult hit,
        out string obstruction)
    {
        RectTransform rectTransform =
            button.transform as RectTransform;
        if (rectTransform == null)
        {
            screenPosition = default;
            hit = default;
            obstruction = "Button has no RectTransform.";
            return false;
        }

        string blockingPath = null;
        foreach (Vector2 normalizedPoint in ButtonProbePoints)
        {
            Vector2 point = ToScreenPoint(
                rectTransform,
                normalizedPoint);
            if (!TryRaycast(point, out RaycastResult topHit))
            {
                continue;
            }

            GameObject clickHandler =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    topHit.gameObject);
            if (clickHandler == button.gameObject)
            {
                screenPosition = point;
                hit = topHit;
                obstruction = null;
                return true;
            }

            if (blockingPath == null)
            {
                blockingPath = GetHierarchyPath(
                    topHit.gameObject.transform);
            }
        }

        screenPosition = default;
        hit = default;
        obstruction = blockingPath == null
            ? "No pointer raycast reached the button bounds."
            : "Top pointer target is '" + blockingPath + "'.";
        return false;
    }

    private static bool TryFindTopHit(
        RectTransform rectTransform,
        IEnumerable<Vector2> normalizedPoints,
        out Vector2 screenPosition,
        out RaycastResult hit)
    {
        foreach (Vector2 normalizedPoint in normalizedPoints)
        {
            Vector2 point = ToScreenPoint(
                rectTransform,
                normalizedPoint);
            if (TryRaycast(point, out hit))
            {
                screenPosition = point;
                return true;
            }
        }

        screenPosition = default;
        hit = default;
        return false;
    }

    private static bool TryRaycast(
        Vector2 screenPosition,
        out RaycastResult hit)
    {
        PointerEventData pointer = new PointerEventData(
            EventSystem.current)
        {
            position = screenPosition,
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            hit = result;
            return true;
        }

        hit = default;
        return false;
    }

    private static PointerEventData CreatePointerEvent(
        Vector2 screenPosition,
        RaycastResult hit)
    {
        return new PointerEventData(EventSystem.current)
        {
            position = screenPosition,
            pressPosition = screenPosition,
            button = PointerEventData.InputButton.Left,
            pointerCurrentRaycast = hit,
            pointerPressRaycast = hit,
            pointerEnter = hit.gameObject,
            useDragThreshold = true,
        };
    }

    private static Vector2 ToScreenPoint(
        RectTransform rectTransform,
        Vector2 normalizedPoint)
    {
        Rect rect = rectTransform.rect;
        Vector3 localPoint = new Vector3(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y),
            0f);
        Vector3 worldPoint = rectTransform.TransformPoint(localPoint);
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas == null
            || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            worldPoint);
    }

    private static bool IsRaycastProbe(Graphic graphic)
    {
        return graphic != null
            && graphic.enabled
            && graphic.gameObject.activeInHierarchy
            && graphic.raycastTarget
            && graphic.color.a > 0.001f
            && !graphic.canvasRenderer.cull
            && graphic.canvasRenderer.GetAlpha() > 0.001f;
    }

    private static float GraphicArea(Graphic graphic)
    {
        Rect rect = graphic.rectTransform.rect;
        return Mathf.Abs(rect.width * rect.height);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var segments = new List<string>();
        for (Transform current = transform;
            current != null;
            current = current.parent)
        {
            segments.Add(current.name);
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string FormatVector(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }
}
