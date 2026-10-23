using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattleActorScreenHitProxy : Graphic,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    public event Action<int, bool> HoverChanged;
    public event Action<int> Clicked;

    public int RuntimeId { get; private set; }
    public int WorldSortingOrder { get; private set; }
    public float ProjectedDepth { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        color = Color.clear;
        raycastTarget = true;
    }

    public void Bind(
        int runtimeId,
        int sortingOrder,
        float projectedDepth)
    {
        RuntimeId = runtimeId;
        WorldSortingOrder = sortingOrder;
        ProjectedDepth = projectedDepth;
        raycastTarget = runtimeId > 0;
    }

    public void ResetBinding()
    {
        if (RuntimeId > 0)
            HoverChanged?.Invoke(RuntimeId, false);
        RuntimeId = 0;
        WorldSortingOrder = 0;
        ProjectedDepth = 0f;
        raycastTarget = false;
    }

    public void SetInteractable(bool value)
    {
        raycastTarget = value && RuntimeId > 0;
    }

    public void UpdateProjection(int sortingOrder, float projectedDepth)
    {
        WorldSortingOrder = sortingOrder;
        ProjectedDepth = projectedDepth;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (RuntimeId > 0)
            HoverChanged?.Invoke(RuntimeId, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (RuntimeId > 0)
            HoverChanged?.Invoke(RuntimeId, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (RuntimeId > 0 &&
            (eventData == null || eventData.button == PointerEventData.InputButton.Left))
        {
            Clicked?.Invoke(RuntimeId);
        }
    }

    public static int CompareFrontMost(
        BattleActorScreenHitProxy left,
        BattleActorScreenHitProxy right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        int sorting = right.WorldSortingOrder.CompareTo(left.WorldSortingOrder);
        return sorting != 0
            ? sorting
            : left.ProjectedDepth.CompareTo(right.ProjectedDepth);
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
    }
}
