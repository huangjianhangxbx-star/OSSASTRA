using DG.Tweening;
using UnityEngine;

public sealed class BattleActorSelectionPresenter : MonoBehaviour
{
    [SerializeField] private BattleWorldActorView actorView;
    [SerializeField] private GameObject groundMarker;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float transitionSeconds = 0.1f;

    private Tween scaleTween;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (actorView == null)
            actorView = GetComponentInParent<BattleWorldActorView>();
        if (actorView != null)
            baseScale = actorView.transform.localScale;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (groundMarker != null)
            groundMarker.SetActive(highlighted);
        if (actorView == null)
            return;

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill(false);
        Vector3 target = highlighted
            ? baseScale * Mathf.Max(1f, hoverScale)
            : baseScale;
        scaleTween = actorView.transform.DOScale(
                target,
                Mathf.Max(0f, transitionSeconds))
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(actorView.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void OnDisable()
    {
        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill(false);
        scaleTween = null;
        if (actorView != null)
            actorView.transform.localScale = baseScale;
        if (groundMarker != null)
            groundMarker.SetActive(false);
    }
}
