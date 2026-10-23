using System;
using UnityEngine;

[Serializable]
public class MeleeWeaponStats
{
    [Header("武器性能")]
    [Min(0.01f)]
    public float attackSpeed = 1f;

    [Header("资源")]
    [Min(0f)]
    public float staminaEfficiency = 1f;

    [Header("判定修正")]
    [Min(0f)]
    public float rangeMultiplier = 1f;

    [Min(0f)]
    public float hitboxScaleMultiplier = 1f;
}