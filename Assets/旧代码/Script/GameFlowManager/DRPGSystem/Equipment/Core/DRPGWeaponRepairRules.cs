public static class DRPGWeaponRepairRules
{
    public static bool ApplyStandardFullRepair(DRPGWeaponInstance instance)
    {
        if (instance == null ||
            instance.CurrentDurability >= instance.CurrentMaxDurability)
        {
            return false;
        }

        DRPGWeaponDurabilityProfile profile =
            DRPGWeaponRarityRules.GetDurabilityProfile(
                instance.Definition.Rarity);
        int loss = UnityEngine.Mathf.CeilToInt(
            instance.RolledMaxDurability * profile.RepairMaximumLossRatio);
        int floor = UnityEngine.Mathf.CeilToInt(
            instance.RolledMaxDurability * profile.RepairMinimumRetainedRatio);
        int nextMaximum = UnityEngine.Mathf.Max(
            floor,
            instance.CurrentMaxDurability - loss);
        instance.RestoreDurability(nextMaximum, nextMaximum);
        return true;
    }
}
