using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;



/// <summary>
/// 是资产中的角色定义
/// </summary>
[CreateAssetMenu(
    fileName = "DRPGCombatantProfile",
    menuName = "ANKALUTE/DRPG/Combatant Profile")]
public sealed class DRPGCombatantProfileSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string combatantId = "combatant";
    [SerializeField] private string displayName = "Combatant";
    [SerializeField] private CharacterSO explorationCharacter;
    [SerializeField] private GalActorSO galActor;
    [SerializeField] private DRPGBattleQuoteSetSO battleQuotes;

    [Header("Shared portrait source")]
    [Tooltip("The same full-body portrait asset can be referenced by both Gal and battle UI.")]
    [SerializeField] private Sprite fullPortrait;
    [Tooltip("Optional hit variant. Falls back to Full Portrait when omitted.")]
    [SerializeField] private Sprite hitPortrait;
    [FormerlySerializedAs("brokenPortrait")]
    [Tooltip("Optional low-HP DamageBroken variant. Falls back to Full Portrait when omitted.")]
    [SerializeField] private Sprite damageBrokenPortrait;
    [Tooltip("Optional skill cut-in pose. Falls back to Full Portrait when omitted.")]
    [SerializeField] private Sprite attackCutInPortrait;
    [SerializeField] private Vector2 portraitFocus = new Vector2(0.5f, 0.72f);
    [Min(0.1f)]
    [SerializeField] private float portraitScale = 1f;
    [Min(0.1f)]
    [Tooltip("Additional zoom used only by masked timeline and party avatar views.")]
    [SerializeField] private float avatarZoom = 2.25f;

    [Header("Formation")]
    [SerializeField] private DRPGFormationRow preferredFormationRow =
        DRPGFormationRow.Front;
    [Min(0.25f)]
    [Tooltip("Relative horizontal footprint used by the dynamic battlefield layout.")]
    [SerializeField] private float formationVisualWidth = 1f;

    [Header("Base battle stats")]
    [Min(1f)]
    [SerializeField] private float maxHp = 100f;
    [Range(0f, 1f)]
    [FormerlySerializedAs("brokenHpRatio")]
    [SerializeField] private float damageBrokenHpRatio = 0.35f;
    [Min(0.01f)]
    [SerializeField] private float agility = 10f;
    [Min(0f)]
    [SerializeField] private float baseTimelineDelay = 1000f;
    [Min(0f)]
    [SerializeField] private float baseRecoveryDelay = 100f;
    [Min(0f)]
    [SerializeField] private float maxMp = 30f;
    [Min(1)]
    [SerializeField] private int staminaCapacity = 6;
    [Min(0f)]
    [SerializeField] private float staminaRecoveryPerCompletedAction = 0.5f;
    [Min(1f)]
    [SerializeField] private float expeditionEnergy = 100f;

    [Header("Posture")]
    [Min(1f)]
    [SerializeField] private float maxPosture = 100f;
    [Min(0f)]
    [SerializeField] private float postureRecoveryPerTurn = 15f;
    [Range(0.01f, 1f)]
    [SerializeField] private float postureBreakRecoveryRatio = 0.5f;
    [Min(0)]
    [SerializeField] private int postureBreakStabilityOwnActions = 3;
    [Min(0f)]
    [SerializeField] private float postureBreakStabilityDamageMultiplier = 0.5f;
    [Min(0f)]
    [SerializeField] private float postureBreakExposedDamageMultiplier = 1.25f;
    [Min(0f)]
    [SerializeField] private float postureBreakAilmentBuildUpMultiplier = 1.25f;

    [Header("Damage responses")]
    [Tooltip("Every matching entry multiplies incoming tagged damage. Empty is neutral.")]
    [SerializeField] private List<DRPGDamageResponseDefinition> damageResponses =
        new List<DRPGDamageResponseDefinition>();
    [Tooltip("Battle V2 integer resistance. Only an exact matching attribute is applied.")]
    [SerializeField] private List<BattleAttributeResistance> battleResistances =
        new List<BattleAttributeResistance>();

    [Header("Available skills")]
    [SerializeField] private List<DRPGSkillSO> skills = new List<DRPGSkillSO>();

    [Header("Progression")]
    [SerializeField] private DRPGCharacterProgressionDefinitionSO progressionDefinition;

    [Header("Free-strike target geometry")]
    [SerializeField] private Rect freeStrikeBodyBounds =
        new Rect(0f, 0f, 1f, 1f);

    public string CombatantId => combatantId;
    public string DisplayName => displayName;
    public CharacterSO ExplorationCharacter => explorationCharacter;
    public GalActorSO GalActor => galActor;
    public DRPGBattleQuoteSetSO BattleQuotes => battleQuotes;
    public Sprite FullPortrait => fullPortrait;
    public Sprite HitPortrait => hitPortrait;
    public Sprite DamageBrokenPortrait => damageBrokenPortrait;
    public Sprite AttackCutInPortrait => attackCutInPortrait != null
        ? attackCutInPortrait
        : fullPortrait;
    public Vector2 PortraitFocus => portraitFocus;
    public float PortraitScale => portraitScale;
    public float AvatarZoom => avatarZoom;
    public DRPGFormationRow PreferredFormationRow => preferredFormationRow;
    public float FormationVisualWidth => Mathf.Max(0.25f, formationVisualWidth);
    public float MaxHp => Mathf.Max(1f, maxHp);
    public float DamageBrokenHpRatio => Mathf.Clamp01(damageBrokenHpRatio);
    public float Agility => Mathf.Max(0.01f, agility);
    public float BaseTimelineDelay => Mathf.Max(0f, baseTimelineDelay);
    public float BaseRecoveryDelay => Mathf.Max(0f, baseRecoveryDelay);
    public float MaxMp => Mathf.Max(0f, maxMp);
    public int StaminaCapacity => Mathf.Max(1, staminaCapacity);
    public double StaminaRecoveryPerCompletedAction =>
        Mathf.Max(0f, staminaRecoveryPerCompletedAction);
    public float ExpeditionEnergy => Mathf.Max(1f, expeditionEnergy);
    public float MaxPosture => Mathf.Max(1f, maxPosture);
    public float PostureRecoveryPerTurn =>
        Mathf.Max(0f, postureRecoveryPerTurn);
    public float PostureBreakRecoveryRatio =>
        Mathf.Clamp(postureBreakRecoveryRatio, 0.01f, 1f);
    public int PostureBreakStabilityOwnActions =>
        Mathf.Max(0, postureBreakStabilityOwnActions);
    public float PostureBreakStabilityDamageMultiplier =>
        Mathf.Max(0f, postureBreakStabilityDamageMultiplier);
    public float PostureBreakExposedDamageMultiplier =>
        Mathf.Max(0f, postureBreakExposedDamageMultiplier);
    public float PostureBreakAilmentBuildUpMultiplier =>
        Mathf.Max(0f, postureBreakAilmentBuildUpMultiplier);
    public IReadOnlyList<DRPGDamageResponseDefinition> DamageResponses =>
        damageResponses ??
        (IReadOnlyList<DRPGDamageResponseDefinition>)
        Array.Empty<DRPGDamageResponseDefinition>();
    public IReadOnlyList<BattleAttributeResistance> BattleResistances =>
        battleResistances ??
        (IReadOnlyList<BattleAttributeResistance>)
        Array.Empty<BattleAttributeResistance>();
    public IReadOnlyList<DRPGSkillSO> Skills => skills;
    public DRPGCharacterProgressionDefinitionSO ProgressionDefinition =>
        progressionDefinition;
    public Rect FreeStrikeBodyBounds => freeStrikeBodyBounds;

    public DRPGPostureBreakRules CreatePostureBreakRules()
    {
        return new DRPGPostureBreakRules(
            PostureBreakRecoveryRatio,
            PostureBreakStabilityOwnActions,
            PostureBreakStabilityDamageMultiplier,
            PostureBreakExposedDamageMultiplier,
            PostureBreakAilmentBuildUpMultiplier);
    }

    public Sprite ResolvePortrait(DRPGBattlePortraitState state)
    {
        Sprite candidate = state == DRPGBattlePortraitState.Broken
            ? damageBrokenPortrait
            : state == DRPGBattlePortraitState.Hit
                ? hitPortrait
                : fullPortrait;
        return candidate != null ? candidate : fullPortrait;
    }
}
