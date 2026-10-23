using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGSkill",
    menuName = "ANKALUTE/DRPG/Skill")]
public sealed class DRPGSkillSO : ScriptableObject
{
    [SerializeField] private string skillId = "basic_attack";
    [SerializeField] private string displayName = "Attack";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Min(0f)]
    [SerializeField] private float power = 20f;
    [Min(0f)]
    [SerializeField] private float prepareDelay;
    [Min(0f)]
    [SerializeField] private float recoveryDelay = 100f;
    [SerializeField] private EAnimationLogicState animationIntent = EAnimationLogicState.Skill_1;
    [SerializeField] private string presentationKey = "basic_attack";

    [Header("Formation range")]
    [Tooltip("Rows from which the actor may execute this skill.")]
    [SerializeField] private DRPGFormationRowMask sourceFormationRows =
        DRPGFormationRowMask.All;
    [Tooltip("Enemy rows this skill may target. The UI renders these as red/yellow/blue chips.")]
    [SerializeField] private DRPGFormationRowMask targetFormationRows =
        DRPGFormationRowMask.All;

    [Header("Execution")]
    [SerializeField] private DRPGSkillPurpose purpose = DRPGSkillPurpose.Damage;
    [SerializeField] private DRPGSkillTargetRule targetRule =
        DRPGSkillTargetRule.SingleEnemy;
    [SerializeField] private DRPGSkillExecutionMode executionMode =
        DRPGSkillExecutionMode.Standard;
    [Tooltip("Rule-owned damage shares for indexed Spine Impact markers. Empty preserves one impact at 100% damage.")]
    [SerializeField] private List<float> impactDamageMultipliers =
        new List<float>();

    [Header("Enemy intent")]
    [Tooltip("Logical warning stages shown before an ordinary enemy attack becomes ready. Zero still grants the mandatory warning-0 response turn.")]
    [Min(0)]
    [SerializeField] private int enemyWarningSteps;
    [Tooltip("Hidden by default. RevealAtCommit publishes only the target chosen at warning zero.")]
    [SerializeField] private DRPGEnemyTargetDisclosureMode
        enemyTargetDisclosureMode = DRPGEnemyTargetDisclosureMode.Hidden;
    [Tooltip("Controls whether PostureBreak may cancel this skill before its execution phase begins.")]
    [SerializeField] private DRPGPostureBreakInterruptionPolicy
        postureBreakInterruptionPolicy =
            DRPGPostureBreakInterruptionPolicy.InterruptBeforeExecution;
    [Tooltip("Allows this hostile single-target skill to select an exposed target even when a stable legal alternative exists.")]
    [SerializeField] private bool ignorePostureBreakProtection;
    [Tooltip("When enabled, the card remains configured but is unavailable while no functional weapon is equipped.")]
    [SerializeField] private bool requiresFunctionalWeapon;
    [Min(0f)]
    [SerializeField] private float manaCost;
    [Min(0f)]
    [SerializeField] private float postureCost;
    [Min(0)]
    [SerializeField] private int staminaCost;

    [Header("Progress resources")]
    [Tooltip("Consumed once when this action is committed. Empty keeps legacy behavior.")]
    [SerializeField] private DRPGSkillProgressCostDefinition actionProgressCost;
    [Tooltip("Consumed for every accepted FreeStrike input, including a miss.")]
    [SerializeField] private DRPGSkillProgressCostDefinition inputProgressCost;
    [SerializeField] private List<DRPGProgressGainRuleDefinition>
        progressGainRules = new List<DRPGProgressGainRuleDefinition>();
    [SerializeField] private DRPGSelfPostureDepletionPolicy
        selfPostureDepletionPolicy =
            DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak;

    [Header("Resolved effects")]
    [Tooltip("When empty, legacy Damage/Healing/Defense purpose fields are converted into one compatible effect at prepare time.")]
    [SerializeField] private List<DRPGActionEffectDefinition> effects =
        new List<DRPGActionEffectDefinition>();

    [Header("Command expansion")]
    [SerializeField] private DRPGSkillCommandExpansionMode commandExpansionMode;
    [SerializeField] private List<DRPGSkillSO> childSkills =
        new List<DRPGSkillSO>();

    [Header("Telegraphed special")]
    [SerializeField] private List<float> specialImpactTimes =
        new List<float> { 0.65f, 1.10f, 1.55f };
    [Min(0.01f)]
    [SerializeField] private float guardLeadSeconds = 0.42f;
    [Min(0.01f)]
    [SerializeField] private float perfectLeadSeconds = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float guardedDamageMultiplier = 0.45f;
    [Min(0f)]
    [SerializeField] private float missedGuardPostureDamage = 34f;
    [Min(0f)]
    [SerializeField] private float guardedPostureDamage = 22f;

    [Header("Free strike")]
    [Min(0.1f)]
    [SerializeField] private float freeStrikeDuration = 3f;
    [Min(0.01f)]
    [SerializeField] private float freeStrikeCadence = 0.18f;
    [Min(0f)]
    [SerializeField] private float freeStrikePostureCost = 12f;
    [SerializeField] private DRPGFreeStrikeShape freeStrikeShape =
        DRPGFreeStrikeShape.Point;
    [Min(0f)]
    [SerializeField] private float freeStrikeLineLength = 0.6f;
    [Min(0f)]
    [SerializeField] private float freeStrikeLineWidth = 0.08f;
    [SerializeField] private DRPGFreeStrikeContactPolicy freeStrikeContactPolicy =
        DRPGFreeStrikeContactPolicy.AllContacts;
    [Min(1)]
    [SerializeField] private int freeStrikeMaxPrimaryContacts = 1;
    [SerializeField] private DRPGSecondaryImpactKind freeStrikeSecondaryImpactKind =
        DRPGSecondaryImpactKind.None;
    [Min(0f)]
    [SerializeField] private float freeStrikeSecondaryRadius;
    [Min(0f)]
    [SerializeField] private float freeStrikeSecondaryDamageMultiplier = 0.35f;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public float Power => Mathf.Max(0f, power);
    public float PrepareDelay => Mathf.Max(0f, prepareDelay);
    public float RecoveryDelay => Mathf.Max(0f, recoveryDelay);
    public EAnimationLogicState AnimationIntent => animationIntent;
    public DRPGFormationRowMask SourceFormationRows => sourceFormationRows;
    public DRPGFormationRowMask TargetFormationRows => targetFormationRows;
    public bool CanUseFrom(DRPGFormationRow row) =>
        sourceFormationRows.Contains(row);
    public bool CanTarget(DRPGFormationRow row) =>
        targetFormationRows.Contains(row);
    public DRPGSkillPurpose Purpose => purpose;
    public DRPGSkillTargetRule TargetRule => targetRule;
    public DRPGSkillExecutionMode ExecutionMode => executionMode;
    public IReadOnlyList<float> ImpactDamageMultipliers =>
        impactDamageMultipliers ??
        (IReadOnlyList<float>)Array.Empty<float>();
    public int EnemyWarningSteps => Mathf.Max(0, enemyWarningSteps);
    public DRPGEnemyTargetDisclosureMode EnemyTargetDisclosureMode =>
        enemyTargetDisclosureMode;
    public DRPGPostureBreakInterruptionPolicy
        PostureBreakInterruptionPolicy => postureBreakInterruptionPolicy;
    public bool IgnorePostureBreakProtection =>
        ignorePostureBreakProtection;
    public bool RequiresFunctionalWeapon => requiresFunctionalWeapon;
    public float ManaCost => Mathf.Max(0f, manaCost);
    public float PostureCost => Mathf.Max(0f, postureCost);
    public int StaminaCost => Mathf.Max(0, staminaCost);
    public DRPGSkillProgressCostDefinition ActionProgressCost =>
        actionProgressCost;
    public DRPGSkillProgressCostDefinition InputProgressCost =>
        inputProgressCost;
    public IReadOnlyList<DRPGProgressGainRuleDefinition> ProgressGainRules =>
        progressGainRules ??
        (IReadOnlyList<DRPGProgressGainRuleDefinition>)
        Array.Empty<DRPGProgressGainRuleDefinition>();
    public DRPGSelfPostureDepletionPolicy SelfPostureDepletionPolicy =>
        selfPostureDepletionPolicy;
    public IReadOnlyList<DRPGActionEffectDefinition> Effects =>
        effects ??
        (IReadOnlyList<DRPGActionEffectDefinition>)
        Array.Empty<DRPGActionEffectDefinition>();
    public DRPGSkillCommandExpansionMode CommandExpansionMode =>
        commandExpansionMode;
    public IReadOnlyList<DRPGSkillSO> ChildSkills =>
        childSkills ?? (IReadOnlyList<DRPGSkillSO>)Array.Empty<DRPGSkillSO>();
    public IReadOnlyList<float> SpecialImpactTimes =>
        specialImpactTimes ?? (IReadOnlyList<float>)Array.Empty<float>();
    public float GuardLeadSeconds => Mathf.Max(0.01f, guardLeadSeconds);
    public float PerfectLeadSeconds => Mathf.Clamp(
        perfectLeadSeconds,
        0.01f,
        GuardLeadSeconds);
    public float GuardedDamageMultiplier =>
        Mathf.Clamp01(guardedDamageMultiplier);
    public float MissedGuardPostureDamage =>
        Mathf.Max(0f, missedGuardPostureDamage);
    public float GuardedPostureDamage =>
        Mathf.Max(0f, guardedPostureDamage);
    public float FreeStrikeDuration => Mathf.Max(0.1f, freeStrikeDuration);
    public float FreeStrikeCadence => Mathf.Max(0.01f, freeStrikeCadence);
    public float FreeStrikePostureCost =>
        Mathf.Max(0f, freeStrikePostureCost);
    public DRPGFreeStrikeShape FreeStrikeShape => freeStrikeShape;
    public float FreeStrikeLineLength =>
        Mathf.Max(0f, freeStrikeLineLength);
    public float FreeStrikeLineWidth =>
        Mathf.Max(0f, freeStrikeLineWidth);
    public DRPGFreeStrikeContactPolicy FreeStrikeContactPolicy =>
        freeStrikeContactPolicy;
    public int FreeStrikeMaxPrimaryContacts =>
        Mathf.Max(1, freeStrikeMaxPrimaryContacts);
    public DRPGSecondaryImpactKind FreeStrikeSecondaryImpactKind =>
        freeStrikeSecondaryImpactKind;
    public float FreeStrikeSecondaryRadius =>
        Mathf.Max(0f, freeStrikeSecondaryRadius);
    public float FreeStrikeSecondaryDamageMultiplier =>
        Mathf.Max(0f, freeStrikeSecondaryDamageMultiplier);
    public string PresentationKey => string.IsNullOrWhiteSpace(presentationKey)
        ? skillId
        : presentationKey;
}
