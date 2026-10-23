using UnityEngine;

public abstract class WeaponActionProfileSO : ScriptableObject
{
    public abstract WeaponType WeaponType { get; }
}