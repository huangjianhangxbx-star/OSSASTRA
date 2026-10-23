using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BattleCardTargetArrow : MonoBehaviour
{
    [SerializeField] private RectTransform segmentRoot;
    [SerializeField] private RectTransform coordinateRoot;
    [SerializeField] private Canvas canvas;
    [SerializeField] private UnityEngine.UI.Image segmentTemplate;
    [SerializeField] private UnityEngine.UI.Image tip;
    [SerializeField] private UnityEngine.UI.Image allegianceCore;
    [SerializeField] private float segmentSpacing = 34f;
    [SerializeField] private Color enemyCore = new Color(.48f, .14f, .15f, 1f);
    [SerializeField] private Color allyCore = new Color(.28f, .44f, .35f, 1f);
    [SerializeField] private Color invalidCore = new Color(.38f, .4f, .41f, 1f);
    private readonly List<UnityEngine.UI.Image> segments = new List<UnityEngine.UI.Image>();
    private RectTransform source;

    public static Vector2 Sample(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    public void Hide()
    {
        source = null;
        gameObject.SetActive(false);
    }

    public void Begin(RectTransform sourceRect)
    {
        source = sourceRect;
        gameObject.SetActive(source != null);
    }

    private void LateUpdate()
    {
        if (source == null || coordinateRoot == null || Mouse.current == null)
            return;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(
            eventCamera, source.TransformPoint(source.rect.center));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                coordinateRoot, startScreen, eventCamera, out Vector2 start) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                coordinateRoot, Mouse.current.position.ReadValue(), eventCamera,
                out Vector2 end))
            return;
        Rect bounds = coordinateRoot.rect;
        start.x = Mathf.Clamp(start.x, bounds.xMin + 24f, bounds.xMax - 24f);
        start.y = Mathf.Clamp(start.y, bounds.yMin + 24f, bounds.yMax - 24f);
        end.x = Mathf.Clamp(end.x, bounds.xMin + 42f, bounds.xMax - 42f);
        end.y = Mathf.Clamp(end.y, bounds.yMin + 42f, bounds.yMax - 42f);
        Bind(start, end, true, true);
    }

    public void Bind(Vector2 start, Vector2 end, bool legal, bool targetIsEnemy)
    {
        gameObject.SetActive(true);
        Vector2 delta = end - start;
        Vector2 control = (start + end) * .5f + new Vector2(
            Mathf.Clamp(-delta.y * .16f, -90f, 90f),
            Mathf.Max(56f, Mathf.Abs(delta.x) * .10f));
        float approximateLength = Vector2.Distance(start, control) +
            Vector2.Distance(control, end);
        int count = Mathf.Clamp(Mathf.CeilToInt(approximateLength /
            Mathf.Max(12f, segmentSpacing)), 2, 24);
        EnsureSegmentCount(count);

        for (int i = 0; i < segments.Count; i++)
        {
            bool visible = i < count;
            segments[i].gameObject.SetActive(visible);
            if (!visible)
                continue;
            float t = (i + .35f) / count;
            Vector2 position = Sample(start, control, end, t);
            Vector2 next = Sample(start, control, end, Mathf.Min(1f, t + .02f));
            Place(segments[i].rectTransform, position, next - position);
        }

        if (tip != null)
        {
            Vector2 previous = Sample(start, control, end, .98f);
            Place(tip.rectTransform, end, end - previous);
        }
        if (allegianceCore != null)
            allegianceCore.color = !legal ? invalidCore :
                targetIsEnemy ? enemyCore : allyCore;
    }

    private void EnsureSegmentCount(int count)
    {
        if (segmentTemplate == null || segmentRoot == null)
            return;
        if (segments.Count == 0)
        {
            segments.Add(segmentTemplate);
            segmentTemplate.gameObject.SetActive(true);
        }
        while (segments.Count < count)
        {
            UnityEngine.UI.Image clone = Instantiate(segmentTemplate, segmentRoot);
            clone.name = "Segment_" + segments.Count;
            clone.raycastTarget = false;
            segments.Add(clone);
        }
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 tangent)
    {
        rect.anchoredPosition = position;
        if (tangent.sqrMagnitude > .001f)
            rect.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f);
    }
}
