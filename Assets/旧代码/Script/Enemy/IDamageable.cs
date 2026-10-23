
using UnityEngine;

public struct DamageInfo
{
    public GameObject attacker;
    public float damage;
    public Vector2 hitPoint;
    public Vector2 hitNormal;
    public Vector2 shotDirection;
    public CombatImpactType impactType;
}


public interface IDamageable
{
    bool CanTakeDamage { get; }

    void TakeDamage(DamageInfo damageInfo);
}