using UnityEngine;

public sealed class BattleActorScreenProjector : MonoBehaviour
{
    [SerializeField] private Camera battleCamera;
    [SerializeField] private Renderer worldRenderer;
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private bool projectWhileVisible = true;
    private CanvasGroup projectionVisibility;

    public void Configure(
        Camera camera,
        Renderer renderer,
        RectTransform rect)
    {
        battleCamera = camera;
        worldRenderer = renderer;
        targetRect = rect;
        if (targetRect != null)
        {
            projectionVisibility = targetRect.GetComponent<CanvasGroup>();
            if (projectionVisibility == null)
                projectionVisibility = targetRect.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void LateUpdate()
    {
        if (!projectWhileVisible || battleCamera == null ||
            worldRenderer == null || targetRect == null)
        {
            return;
        }

        bool visible = TryProjectToViewport(
            battleCamera,
            worldRenderer.bounds,
            out Rect viewportRect,
            out float projectedDepth);
        // This updater lives on the target itself. Disabling that object when
        // offscreen prevents LateUpdate from ever restoring it (and its HP UI).
        if (projectionVisibility != null)
        {
            bool show = visible && !BattleCloseupFloatingUiGate.IsSuppressed(projectionVisibility);
            projectionVisibility.alpha = show ? 1f : 0f;
            projectionVisibility.blocksRaycasts = show;
            projectionVisibility.interactable = show;
        }
        if (!visible)
            return;

        targetRect.anchorMin = viewportRect.min;
        targetRect.anchorMax = viewportRect.max;
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;
        BattleActorScreenHitProxy proxy =
            targetRect.GetComponent<BattleActorScreenHitProxy>();
        int sortingOrder = worldRenderer is SpriteRenderer spriteRenderer
            ? spriteRenderer.sortingOrder
            : 0;
        proxy?.UpdateProjection(sortingOrder, projectedDepth);
    }

    public static bool TryProjectToViewport(
        Camera camera,
        Bounds bounds,
        out Rect viewportRect,
        out float projectedDepth)
    {
        viewportRect = default;
        projectedDepth = 0f;
        if (camera == null)
            return false;

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        float nearestDepth = float.PositiveInfinity;
        bool hasFrontPoint = false;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z));
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    if (viewport.z <= 0f)
                        continue;
                    hasFrontPoint = true;
                    minX = Mathf.Min(minX, viewport.x);
                    minY = Mathf.Min(minY, viewport.y);
                    maxX = Mathf.Max(maxX, viewport.x);
                    maxY = Mathf.Max(maxY, viewport.y);
                    nearestDepth = Mathf.Min(nearestDepth, viewport.z);
                }
            }
        }

        if (!hasFrontPoint || maxX < 0f || minX > 1f ||
            maxY < 0f || minY > 1f)
        {
            return false;
        }

        minX = Mathf.Clamp01(minX);
        minY = Mathf.Clamp01(minY);
        maxX = Mathf.Clamp01(maxX);
        maxY = Mathf.Clamp01(maxY);
        viewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        projectedDepth = nearestDepth;
        return viewportRect.width > 0f && viewportRect.height > 0f;
    }
}
