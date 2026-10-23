using UnityEngine;

[System.Serializable]
public class WeaponCommonCoreStats
{
    [Header("»ù´¡ÉËº¦")]
    [Min(0f)]
    public float damage = 10f;

    [Header("±©»÷»ù´¡Öµ")]
    [Range(0f, 1f)]
    public float critChance = 0f;

    [Min(1f)]
    public float critDamage = 1.5f;

    [Header("Ï÷ÈÍ / »÷ÍË")]
    [Min(0f)]
    public float poiseDamage = 0f;

    [Min(0f)]
    public float knockbackForce = 0f;
}