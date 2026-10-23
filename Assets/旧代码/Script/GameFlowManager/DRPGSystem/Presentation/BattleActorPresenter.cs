using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleActorPresenter : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text actorName;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image accent;
    [SerializeField] private Color playerAccent = new Color32(239, 35, 112, 255);
    [SerializeField] private Color enemyAccent = new Color32(196, 42, 55, 255);
    [SerializeField] private MonoBehaviour animationDriverComponent;
    [SerializeField] private Outline targetOutline;
    [SerializeField] private Image targetOverlay;
    [SerializeField] private bool suppressCurrentActorPanel;

    private IBattleActorAnimationDriver animationDriver;
    private DRPGBattleParticipantViewModel boundActor;
    private bool externallySuppressed;
    private bool enemySuppressed;

    public int BoundRuntimeId => boundActor != null ? boundActor.RuntimeId : 0;
    public bool IsPresentationSuppressed =>
        suppressCurrentActorPanel || externallySuppressed || enemySuppressed;

    public void Bind(DRPGBattleParticipantViewModel viewModel)
    {
        boundActor = viewModel;
        enemySuppressed = viewModel != null && viewModel.IsEnemy;
        RefreshVisibility();
        DRPGPortraitImageBinder.Bind(portrait, viewModel);

        if (actorName != null)
            actorName.text = viewModel != null ? viewModel.DisplayName : string.Empty;

        if (hpText != null)
        {
            hpText.text = viewModel != null
                ? $"HP  {Mathf.CeilToInt(viewModel.CurrentHp)} / " +
                  $"{Mathf.CeilToInt(viewModel.MaxHp)}"
                : string.Empty;
        }

        if (accent != null)
            accent.color = viewModel != null && viewModel.IsEnemy
                ? enemyAccent
                : playerAccent;

        animationDriver = animationDriverComponent as IBattleActorAnimationDriver;
        animationDriver?.BindActor(viewModel != null ? viewModel.Profile : null);
        animationDriver?.SetLowHpState(
            viewModel != null
                ? viewModel.LowHpState
                : DRPGLowHpPresentationState.Normal);
        SetTargetFeedback(DRPGBattleTargetFeedback.None);
    }

    public void PlaySkillIntent(DRPGSkillSO skill)
    {
        if (skill != null)
            PlayIntent(skill.AnimationIntent);
    }

    public IBattlePresentationHandle PlayIntent(
        EAnimationLogicState animationIntent)
    {
        if (boundActor == null || animationDriver == null)
            return Settled(BattlePresentationOutcome.Skipped);

        return animationDriver.PlayIntent(animationIntent) ??
            Settled(BattlePresentationOutcome.Skipped);
    }

    public void SetTargetFeedback(DRPGBattleTargetFeedback feedback)
    {
        DRPGBattleTargetFeedbackVisual.Apply(
            targetOutline,
            targetOverlay,
            transform as RectTransform,
            feedback);
    }

    public void SetPresentationSuppressed(bool suppressed)
    {
        externallySuppressed = suppressed;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool visible = !IsPresentationSuppressed;
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private static IBattlePresentationHandle Settled(
        BattlePresentationOutcome outcome)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(outcome);
        return handle;
    }
}
