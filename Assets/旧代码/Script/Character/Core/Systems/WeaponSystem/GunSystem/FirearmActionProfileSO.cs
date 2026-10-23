using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FirearmActionProfile", menuName = "ScriptableObject/武器/枪械行为配置")]
public class FirearmActionProfileSO : WeaponActionProfileSO
{
    public override WeaponType WeaponType => WeaponType.Firearm;

    [Header("枪械分类")]
    public FirearmCategory firearmCategory = FirearmCategory.Rifle;

    [Header("默认射击模式")]
    public GunFireModeType defaultFireMode = GunFireModeType.SemiAuto;

    [Header("可用射击模式")]
    public List<GunFireModeConfig> fireModes = new List<GunFireModeConfig>();

    [HideInInspector]
    [System.Obsolete("Firearm melee input was retired. Field retained for serialized compatibility.")]
    public MeleeComboActionSO auxiliaryMeleeAction;



    [Header("命中方式")]
    public GunHitType hitType = GunHitType.Hitscan;

    [Header("动画")]
    [Min(0.01f)]
    public float fireAnimationDuration = 0.12f;

    [Min(0.01f)]
    public float reloadAnimationFallbackDuration = 0.6f;

    public GunFireModeConfig GetDefaultFireModeConfig()
    {
        return GetFireModeConfig(defaultFireMode);
    }

    public GunFireModeConfig GetFireModeConfig(GunFireModeType modeType)
    {
        if (fireModes == null)
            return null;

        for (int i = 0; i < fireModes.Count; i++)
        {
            GunFireModeConfig config = fireModes[i];

            if (config == null)
                continue;

            if (config.fireModeType == modeType)
                return config;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fireModes == null)
            fireModes = new List<GunFireModeConfig>();

        for (int i = fireModes.Count - 1; i >= 0; i--)
        {
            if (fireModes[i] == null)
                fireModes.RemoveAt(i);
        }

        if (fireModes.Count == 0)
        {
            fireModes.Add(new GunFireModeConfig
            {
                fireModeType = defaultFireMode
            });
        }

        if (GetFireModeConfig(defaultFireMode) == null && fireModes.Count > 0)
            defaultFireMode = fireModes[0].fireModeType;

        //UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
