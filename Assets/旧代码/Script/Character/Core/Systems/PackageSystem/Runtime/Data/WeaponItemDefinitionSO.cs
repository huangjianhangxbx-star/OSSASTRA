using UnityEngine;

/// <summary>
/// Weapon item definition (Plan B: wraps existing WeaponConfigSO without modifying it).
/// </summary>
[CreateAssetMenu(fileName = "WeaponItemDef", menuName = "ScriptableObject/Package/WeaponItemDefinition")]
public class WeaponItemDefinitionSO : ItemDefinitionSO
{
    [SerializeField] private WeaponConfigSO weaponConfig;

    public WeaponConfigSO WeaponConfig => weaponConfig;
}
