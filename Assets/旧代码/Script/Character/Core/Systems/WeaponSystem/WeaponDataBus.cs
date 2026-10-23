public class WeaponDataBus
{
    public WeaponRoot weaponRoot;
    public CharacterRoot owner;

    public WeaponConfigSO config;
    public WeaponStatsSO weaponStats;
    //public WeaponCombatProfileSO weaponCombatProfile;
    public WeaponActionProfileSO weaponActionProfile;
    public FirearmActionProfileSO firearmActionProfile;


    public AnimationPlanOverrideRuleSetSO weaponAnimationOverride;

    public bool isEquipped;
    public bool isAttacking;

    public void LoadFromSO(WeaponConfigSO config)
    {
        this.config = config;

        if (config == null)
            return;

        weaponStats = config.stats;
        //weaponCombatProfile = config.weaponProfile;

        weaponActionProfile = config.weaponActionProfile;
        firearmActionProfile = weaponActionProfile as FirearmActionProfileSO;

        weaponAnimationOverride = config.animationOverride;
    }
}