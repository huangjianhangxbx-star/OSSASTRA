using UnityEngine;

public sealed class BattleHudGroupView : MonoBehaviour
{
    [SerializeField] private BattleHudGroupKind kind;
    [SerializeField] private CanvasGroup canvasGroup;

    public BattleHudGroupKind Kind => kind;
    public CanvasGroup CanvasGroup
    {
        get
        {
            EnsureCanvasGroup();
            return canvasGroup;
        }
    }
    public bool IsVisible => gameObject.activeSelf && CanvasGroup.alpha > 0.001f;

    public void Configure(BattleHudGroupKind groupKind, CanvasGroup group)
    {
        kind = groupKind;
        canvasGroup = group != null ? group : GetComponent<CanvasGroup>();
        EnsureCanvasGroup();
    }

    public BattleHudGroupState Capture() => new BattleHudGroupState(
        kind,
        gameObject.activeSelf,
        CanvasGroup.alpha,
        CanvasGroup.interactable,
        CanvasGroup.blocksRaycasts);

    public void ApplyVisible(bool visible)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        CanvasGroup.alpha = visible ? 1f : 0f;
        CanvasGroup.interactable = visible;
        CanvasGroup.blocksRaycasts = visible;
    }

    public void Restore(BattleHudGroupState state)
    {
        CanvasGroup.alpha = state.Alpha;
        CanvasGroup.interactable = state.Interactable;
        CanvasGroup.blocksRaycasts = state.BlocksRaycasts;
        gameObject.SetActive(state.ActiveSelf);
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
