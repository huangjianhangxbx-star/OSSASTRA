using UnityEngine;
using UnityEngine.UI;

public sealed class BattleTimelineEntryView : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image frame;
    [SerializeField] private GameObject currentActorMarker;
    [SerializeField] private Outline targetOutline;
    [SerializeField] private Image targetOverlay;
    [SerializeField] private Color playerColor = new Color32(234, 231, 238, 255);
    [SerializeField] private Color enemyColor = new Color32(174, 34, 54, 255);
    [SerializeField] private Color currentColor = new Color32(239, 35, 112, 255);

    public int BoundRuntimeId { get; private set; }
    public RectTransform RectTransform => transform as RectTransform;

    public void Bind(DRPGBattleParticipantViewModel viewModel)
    {
        gameObject.SetActive(viewModel != null);
        if (viewModel == null)
        {
            BoundRuntimeId = 0;
            SetTargetFeedback(DRPGBattleTargetFeedback.None);
            return;
        }

        BoundRuntimeId = viewModel.RuntimeId;

        DRPGPortraitImageBinder.Bind(portrait, viewModel, true);
        if (frame != null)
        {
            Color formationColor = DRPGFormationColorPalette.Resolve(
                viewModel.FormationRow);
            frame.color = viewModel.IsCurrentActor
                ? Color.Lerp(formationColor, Color.white, 0.22f)
                : formationColor;
        }

        if (currentActorMarker != null)
            currentActorMarker.SetActive(viewModel.IsCurrentActor);
    }

    public void SetTargetFeedback(DRPGBattleTargetFeedback feedback)
    {
        DRPGBattleTargetFeedbackVisual.Apply(
            targetOutline,
            targetOverlay,
            transform as RectTransform,
            feedback);
    }
}
