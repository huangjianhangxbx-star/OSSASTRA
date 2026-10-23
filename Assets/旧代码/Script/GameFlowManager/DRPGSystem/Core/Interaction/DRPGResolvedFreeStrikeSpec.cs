using System;

public enum DRPGFreeStrikeContactPolicy
{
    AllContacts = 0,
    FirstContact = 1,
    LimitedContacts = 2
}

public enum DRPGSecondaryImpactKind
{
    None = 0,
    RadialSplash = 1
}

public readonly struct DRPGResolvedFreeStrikeSpec
{
    public DRPGResolvedFreeStrikeSpec(
        DRPGFreeStrikeShape shape,
        float basePower,
        float duration,
        float cadence,
        float postureCost,
        float lineLength,
        float lineWidth,
        DRPGFreeStrikeContactPolicy contactPolicy,
        int maxPrimaryContacts,
        DRPGSecondaryImpactKind secondaryImpactKind,
        float secondaryRadius,
        float secondaryDamageMultiplier,
        string presentationKey)
    {
        Shape = shape;
        BasePower = Math.Max(0f, basePower);
        Duration = Math.Max(0.1f, duration);
        Cadence = Math.Max(0.01f, cadence);
        PostureCost = Math.Max(0f, postureCost);
        LineLength = Math.Max(0f, lineLength);
        LineWidth = Math.Max(0f, lineWidth);
        ContactPolicy = contactPolicy;
        MaxPrimaryContacts = contactPolicy == DRPGFreeStrikeContactPolicy.FirstContact
            ? 1
            : contactPolicy == DRPGFreeStrikeContactPolicy.AllContacts
                ? int.MaxValue
                : Math.Max(1, maxPrimaryContacts);
        SecondaryImpactKind = secondaryImpactKind;
        SecondaryRadius = Math.Max(0f, secondaryRadius);
        SecondaryDamageMultiplier = Math.Max(0f, secondaryDamageMultiplier);
        PresentationKey = presentationKey ?? string.Empty;
    }

    public DRPGFreeStrikeShape Shape { get; }
    public float BasePower { get; }
    public float Duration { get; }
    public float Cadence { get; }
    public float PostureCost { get; }
    public float LineLength { get; }
    public float LineWidth { get; }
    public DRPGFreeStrikeContactPolicy ContactPolicy { get; }
    public int MaxPrimaryContacts { get; }
    public DRPGSecondaryImpactKind SecondaryImpactKind { get; }
    public float SecondaryRadius { get; }
    public float SecondaryDamageMultiplier { get; }
    public string PresentationKey { get; }
}

public static class DRPGFreeStrikeSpecResolver
{
    public static DRPGResolvedFreeStrikeSpec Resolve(DRPGSkillSO skill)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));

        return new DRPGResolvedFreeStrikeSpec(
            skill.FreeStrikeShape,
            skill.Power,
            skill.FreeStrikeDuration,
            skill.FreeStrikeCadence,
            skill.FreeStrikePostureCost,
            skill.FreeStrikeLineLength,
            skill.FreeStrikeLineWidth,
            skill.FreeStrikeContactPolicy,
            skill.FreeStrikeMaxPrimaryContacts,
            skill.FreeStrikeSecondaryImpactKind,
            skill.FreeStrikeSecondaryRadius,
            skill.FreeStrikeSecondaryDamageMultiplier,
            skill.PresentationKey);
    }
}
