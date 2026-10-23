using System;

[Flags]
public enum CharacterControlChannel
{
    None = 0,
    Move = 1 << 0,
    Facing = 1 << 1,
    AimTracking = 1 << 2,
    PrimaryAttack = 1 << 3,
    SecondaryAim = 1 << 4,
    Reload = 1 << 5,
    WeaponSwitch = 1 << 6,
    Jump = 1 << 7,
    Interact = 1 << 8
}

public enum CharacterFacingMode
{
    Cursor = 0,
    Movement = 1,
    Hold = 2
}

public enum AimTrackingMode
{
    FollowCursor = 0,
    Hold = 1,
    AnimationDriven = 2
}

public readonly struct CharacterControlOverride
{
    public CharacterControlChannel BlockedChannels { get; }
    public int Priority { get; }
    public bool HasFacingMode { get; }
    public CharacterFacingMode FacingMode { get; }
    public bool HasAimTrackingMode { get; }
    public AimTrackingMode AimTrackingMode { get; }

    public CharacterControlOverride(
        CharacterControlChannel blockedChannels,
        int priority = 0)
        : this(
            blockedChannels,
            priority,
            false,
            CharacterFacingMode.Cursor,
            false,
            AimTrackingMode.FollowCursor)
    {
    }

    public CharacterControlOverride(
        CharacterControlChannel blockedChannels,
        int priority,
        CharacterFacingMode facingMode,
        AimTrackingMode aimTrackingMode)
        : this(
            blockedChannels,
            priority,
            true,
            facingMode,
            true,
            aimTrackingMode)
    {
    }

    private CharacterControlOverride(
        CharacterControlChannel blockedChannels,
        int priority,
        bool hasFacingMode,
        CharacterFacingMode facingMode,
        bool hasAimTrackingMode,
        AimTrackingMode aimTrackingMode)
    {
        BlockedChannels = blockedChannels;
        Priority = priority;
        HasFacingMode = hasFacingMode;
        FacingMode = facingMode;
        HasAimTrackingMode = hasAimTrackingMode;
        AimTrackingMode = aimTrackingMode;
    }

    public static CharacterControlOverride CommittedAttack(int priority = 100)
    {
        return new CharacterControlOverride(
            CharacterControlChannel.Move |
            CharacterControlChannel.Facing |
            CharacterControlChannel.AimTracking |
            CharacterControlChannel.WeaponSwitch |
            CharacterControlChannel.Jump |
            CharacterControlChannel.Interact,
            priority,
            CharacterFacingMode.Hold,
            AimTrackingMode.AnimationDriven);
    }
}
