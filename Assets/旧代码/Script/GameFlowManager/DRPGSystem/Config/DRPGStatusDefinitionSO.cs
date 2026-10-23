using UnityEngine;

public enum DRPGStatusTriggerTiming
{
    TurnStart = 0,
    AfterAction = 1,
    ActorTurnEnd = 2,
    RoundEnd = 3
}

public enum DRPGStatusRefreshPolicy
{
    RefreshDuration = 0,
    AddDuration = 1,
    KeepLongerDuration = 2
}

[CreateAssetMenu(
    fileName = "DRPGStatus",
    menuName = "ANKALUTE/DRPG/Status")]
public sealed class DRPGStatusDefinitionSO : ScriptableObject
{
    [SerializeField] private DRPGStatusTriggerTiming triggerTiming = DRPGStatusTriggerTiming.TurnStart;
    [SerializeField] private int priority;
    [Tooltip("Ordinary ticks remain eligible after their source is defeated. Does not revive a defeated owner.")]
    [SerializeField] private bool persistsAfterSourceDefeat;
    public DRPGStatusTriggerTiming TriggerTiming => triggerTiming;
    public int Priority => priority;
    public bool PersistsAfterSourceDefeat => persistsAfterSourceDefeat;
    [SerializeField] private string statusId = "status";
    [SerializeField] private string displayName = "Status";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Min(1)]
    [SerializeField] private int durationTurns = 1;
    [Min(1)]
    [SerializeField] private int maxStacks = 1;
    [SerializeField] private DRPGStatusRefreshPolicy refreshPolicy =
        DRPGStatusRefreshPolicy.RefreshDuration;

    [Header("Action modifiers")]
    [Min(0f)]
    [SerializeField] private float outgoingDamageMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float incomingDamageMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float outgoingHealingMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float manaCostMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float postureCostMultiplier = 1f;

    [Header("Turn start ticks")]
    [Tooltip("Positive heals; negative damages.")]
    [SerializeField] private float turnStartHpDelta;
    [Tooltip("Positive restores posture; negative damages posture.")]
    [SerializeField] private float turnStartPostureDelta;

    public string StatusId => statusId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int DurationTurns => Mathf.Max(1, durationTurns);
    public int MaxStacks => Mathf.Max(1, maxStacks);
    public DRPGStatusRefreshPolicy RefreshPolicy => refreshPolicy;
    public float OutgoingDamageMultiplier =>
        Mathf.Max(0f, outgoingDamageMultiplier);
    public float IncomingDamageMultiplier =>
        Mathf.Max(0f, incomingDamageMultiplier);
    public float OutgoingHealingMultiplier =>
        Mathf.Max(0f, outgoingHealingMultiplier);
    public float ManaCostMultiplier => Mathf.Max(0f, manaCostMultiplier);
    public float PostureCostMultiplier => Mathf.Max(0f, postureCostMultiplier);
    public float TurnStartHpDelta => turnStartHpDelta;
    public float TurnStartPostureDelta => turnStartPostureDelta;
}
