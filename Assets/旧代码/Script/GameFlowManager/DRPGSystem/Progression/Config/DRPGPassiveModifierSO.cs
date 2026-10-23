using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGPassiveModifier",
    menuName = "ANKALUTE/DRPG/Progression/Passive Modifier")]
public sealed class DRPGPassiveModifierSO : ScriptableObject
{
    [SerializeField] private string modifierId = "passive";
    [SerializeField] private string displayName = "Passive";
    [TextArea]
    [SerializeField] private string description;

    [Header("Core multipliers")]
    [Min(0f)] [SerializeField] private float damageMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float maxHpMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float agilityMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float recoveryDelayMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float maxPostureMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float postureRecoveryMultiplier = 1f;

    [Header("Interactive combat")]
    [SerializeField] private float freeStrikeDurationBonus;
    [Min(0.01f)] [SerializeField] private float freeStrikeCadenceMultiplier = 1f;
    [Min(0f)] [SerializeField] private float freeStrikePostureCostMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float freeStrikeReachMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float guardWindowMultiplier = 1f;
    [SerializeField] private float escapeChanceBonus;

    [Header("Single-action contributions")]
    [Tooltip("Strongly typed changes applied only while preparing one action. These do not replace the long-lived Build multipliers above.")]
    [SerializeField] private List<DRPGActionSpecModifierDefinition> actionModifiers =
        new List<DRPGActionSpecModifierDefinition>();

    public string ModifierId => modifierId;
    public string DisplayName => displayName;
    public string Description => description;
    public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
    public float MaxHpMultiplier => Mathf.Max(0.01f, maxHpMultiplier);
    public float AgilityMultiplier => Mathf.Max(0.01f, agilityMultiplier);
    public float RecoveryDelayMultiplier =>
        Mathf.Max(0.01f, recoveryDelayMultiplier);
    public float MaxPostureMultiplier => Mathf.Max(0.01f, maxPostureMultiplier);
    public float PostureRecoveryMultiplier =>
        Mathf.Max(0.01f, postureRecoveryMultiplier);
    public float FreeStrikeDurationBonus => freeStrikeDurationBonus;
    public float FreeStrikeCadenceMultiplier =>
        Mathf.Max(0.01f, freeStrikeCadenceMultiplier);
    public float FreeStrikePostureCostMultiplier =>
        Mathf.Max(0f, freeStrikePostureCostMultiplier);
    public float FreeStrikeReachMultiplier =>
        Mathf.Max(0.01f, freeStrikeReachMultiplier);
    public float GuardWindowMultiplier => Mathf.Max(0.01f, guardWindowMultiplier);
    public float EscapeChanceBonus => escapeChanceBonus;
    public IReadOnlyList<DRPGActionSpecModifierDefinition> ActionModifiers =>
        actionModifiers ??
        (IReadOnlyList<DRPGActionSpecModifierDefinition>)
        Array.Empty<DRPGActionSpecModifierDefinition>();
}
