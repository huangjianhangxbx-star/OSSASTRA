using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact world-projected status plate used by perspective battle actors.
/// It deliberately owns no battle rules; the presenter feeds it immutable
/// participant view-model data whenever the surface refreshes.
/// </summary>
public sealed class BattleWorldActorStatusView : MonoBehaviour
{
    private static readonly Color NormalBackground =
        new Color32(7, 14, 20, 168);
    private static readonly Color HoverBackground =
        new Color32(18, 48, 57, 235);
    private static readonly Color SelectedBackground =
        new Color32(91, 8, 48, 242);

    [SerializeField] private Image background;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFill;
    [SerializeField] private BattleParticipantStatusWidgets statusWidgets;
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private Image warningBackground;
    [SerializeField] private TMP_Text warningLabel;
    [SerializeField] private TMP_Text postureText;
    [SerializeField] private BattlePipRow battleV2Pips;
    [SerializeField] private BattleIntentView battleV2Intent;

    public string CurrentPostureLabel => postureText != null ? postureText.text : "";

    public Graphic ImpactGraphic => background;
    private int boundRuntimeId;
    private DRPGBattleProjectionLeaseArbiter projectionGuard;
    internal void SetProjectionGuard(DRPGBattleProjectionLeaseArbiter guard) => projectionGuard = guard;

    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        if (hpText != null)
            hpText.text = $"HP {Mathf.CeilToInt(participant.Hp)} / {Mathf.CeilToInt(participant.MaxHp)}";
        if (hpFill != null)
            hpFill.rectTransform.anchorMax = new Vector2(participant.Hp / participant.MaxHp, 1f);
        statusWidgets?.Apply(participant);
    }

    public static BattleWorldActorStatusView Create(RectTransform owner)
    {
        if (owner == null)
            return null;

        RectTransform plate = CreateRect(
            "WorldStatusPlate",
            owner,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(164f, 34f),
            new Vector2(0f, -4f));
        Image plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.color = NormalBackground;
        plateImage.raycastTarget = false;

        RectTransform nameRect = CreateRect(
            "Name",
            plate,
            new Vector2(0f, 0.42f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        nameRect.offsetMin = new Vector2(7f, 0f);
        nameRect.offsetMax = new Vector2(-7f, 0f);
        TMP_Text name = CreateLabel(nameRect, 13f, TextAlignmentOptions.Center);

        RectTransform hpRect = CreateRect(
            "HpText",
            plate,
            new Vector2(0f, 0.12f),
            new Vector2(1f, 0.43f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        hpRect.offsetMin = new Vector2(7f, 0f);
        hpRect.offsetMax = new Vector2(-7f, 0f);
        TMP_Text hp = CreateLabel(hpRect, 9f, TextAlignmentOptions.Center);
        hp.color = new Color32(210, 221, 226, 255);

        RectTransform track = CreateRect(
            "HpTrack",
            plate,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(0f, 4f));
        track.offsetMin = new Vector2(6f, 4f);
        track.offsetMax = new Vector2(-6f, 8f);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color32(20, 27, 34, 255);
        trackImage.raycastTarget = false;

        RectTransform fillRect = CreateRect(
            "Fill",
            track,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = new Color32(255, 28, 119, 255);
        fill.raycastTarget = false;

        RectTransform warning = CreateRect(
            "Warning",
            owner,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(42f, 34f),
            new Vector2(0f, 10f));
        Image warningImage = warning.gameObject.AddComponent<Image>();
        warningImage.raycastTarget = false;
        RectTransform warningLabelRect = CreateRect(
            "Label",
            warning,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        TMP_Text warningText = CreateLabel(
            warningLabelRect,
            20f,
            TextAlignmentOptions.Center);
        warningText.fontStyle = FontStyles.Bold;

        var result = plate.gameObject.AddComponent<BattleWorldActorStatusView>();
        result.background = plateImage;
        result.displayName = name;
        result.hpText = hp;
        result.hpFill = fill;
        result.warningRoot = warning.gameObject;
        result.warningBackground = warningImage;
        result.warningLabel = warningText;
        RectTransform postureRect = CreateRect(
            "Posture", plate, new Vector2(0f, 0f), new Vector2(.24f, .43f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        postureRect.offsetMin = new Vector2(7f, 0f);
        TMP_Text posture = CreateLabel(postureRect, 11f, TextAlignmentOptions.Center);
        posture.color = new Color32(156, 163, 166, 255);
        RectTransform pipsRect = CreateRect(
            "StaminaPips", plate, new Vector2(.25f, 0f), new Vector2(.98f, .20f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        BattlePipRow pips = pipsRect.gameObject.AddComponent<BattlePipRow>();
        BattleIntentView intent = BattleIntentView.Create(owner);
        intent.Clear();
        result.postureText = posture;
        result.battleV2Pips = pips;
        result.battleV2Intent = intent;
        warning.gameObject.SetActive(false);
        return result;
    }

    public void Bind(DRPGBattleParticipantViewModel model)
        => BindCanonical(model, null);

    internal void BindCanonical(DRPGBattleParticipantViewModel model,
        BattleCanonicalProjectionPermit permit)
    {
        if (projectionGuard != null &&
            projectionGuard.DeferCanonical("WorldStatus.Bind", boundRuntimeId, permit)) return;
        boundRuntimeId = model != null ? model.RuntimeId : 0;
        gameObject.SetActive(model != null);
        if (model == null)
        {
            statusWidgets?.Clear();
            return;
        }
        statusWidgets?.Apply(model.ProjectionBaseline);
        BindBattleV2(
            Mathf.CeilToInt(model.CurrentHp),
            Mathf.CeilToInt(model.MaxHp),
            (long)Mathf.Ceil(model.ProjectionBaseline.Posture),
            null,
            null,
            model.Intent);

        if (displayName != null)
            displayName.text = model.DisplayName;
        if (hpText != null)
        {
            hpText.text = $"HP {Mathf.CeilToInt(model.CurrentHp)} / " +
                Mathf.CeilToInt(model.MaxHp);
        }
        if (hpFill != null)
        {
            RectTransform fill = hpFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(model.HpRatio), 1f);
        }

        bool hasWarning = model.HasEnemyWarning;
        if (warningRoot != null)
            warningRoot.SetActive(hasWarning);
        if (hasWarning && warningLabel != null)
            warningLabel.text = Mathf.Max(0, model.EnemyWarningValue).ToString();
        if (hasWarning && warningBackground != null)
        {
            warningBackground.color = model.EnemyWarningValue <= 0
                ? new Color32(255, 45, 94, 255)
                : model.EnemyWarningValue == 1
                    ? new Color32(237, 173, 57, 250)
                    : new Color32(70, 167, 204, 245);
        }
    }

    public void BindBattleV2(
        int hp,
        int maxHp,
        long posture,
        int? availablePips,
        int? capacity,
        BattleIntentSnapshot? intent)
    {
        if (hpText != null)
            hpText.text = "HP " + hp + " / " + Mathf.Max(1, maxHp);
        if (hpFill != null)
            hpFill.rectTransform.anchorMax = new Vector2(
                Mathf.Clamp01((float)hp / Mathf.Max(1, maxHp)), 1f);
        if (postureText != null)
            postureText.text = posture.ToString();
        battleV2Pips?.Bind(
            availablePips.GetValueOrDefault(),
            capacity.GetValueOrDefault(),
            0);
        if (battleV2Intent != null)
        {
            if (intent.HasValue)
                battleV2Intent.Bind(intent.Value);
            else
                battleV2Intent.Clear();
        }
    }

    public void SetFeedback(DRPGBattleTargetFeedback feedback)
    {
        if (background == null)
            return;
        background.color = feedback == DRPGBattleTargetFeedback.Selected
            ? SelectedBackground
            : feedback == DRPGBattleTargetFeedback.Hovered
                ? HoverBackground
                : NormalBackground;
        transform.localScale = feedback == DRPGBattleTargetFeedback.None
            ? Vector3.one
            : Vector3.one * (feedback == DRPGBattleTargetFeedback.Hovered
                ? 1.035f
                : 1.065f);
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        var child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private static TMP_Text CreateLabel(
        RectTransform owner,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        var label = owner.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ResolveCjkFont(label.font);
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_FontAsset ResolveCjkFont(TMP_FontAsset fallback)
    {
        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            TMP_FontAsset candidate = loaded[i];
            if (candidate != null && candidate.name.IndexOf(
                    "NotoSansSC", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }
        return fallback;
    }
}
