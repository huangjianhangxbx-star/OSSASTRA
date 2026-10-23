using System;

public sealed class DRPGInteractiveInputResourceSnapshot
{
    public DRPGInteractiveInputResourceSnapshot(
        DRPGProgressSnapshot progress,
        float currentPosture,
        float maximumPosture,
        bool hasWeaponDurability,
        int currentWeaponDurability,
        int projectedWeaponDurability,
        int maximumWeaponDurability)
    {
        if (currentPosture < 0f || maximumPosture < 0f ||
            currentPosture > maximumPosture + 0.0001f)
        {
            throw new ArgumentOutOfRangeException(nameof(currentPosture));
        }
        Progress = progress;
        CurrentPosture = currentPosture;
        MaximumPosture = maximumPosture;
        HasWeaponDurability = hasWeaponDurability;
        CurrentWeaponDurability = Math.Max(0, currentWeaponDurability);
        ProjectedWeaponDurability = Math.Max(0, projectedWeaponDurability);
        MaximumWeaponDurability = Math.Max(0, maximumWeaponDurability);
    }

    public DRPGProgressSnapshot Progress { get; }
    public bool HasProgress => Progress != null;
    public float CurrentPosture { get; }
    public float MaximumPosture { get; }
    public bool HasWeaponDurability { get; }
    public int CurrentWeaponDurability { get; }
    public int ProjectedWeaponDurability { get; }
    public int MaximumWeaponDurability { get; }
}
