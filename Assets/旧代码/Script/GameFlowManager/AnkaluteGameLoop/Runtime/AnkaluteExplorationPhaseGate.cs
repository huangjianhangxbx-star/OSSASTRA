using System;

/// <summary>
/// Applies the top-level application's exploration permission to the current
/// Hunt data and player. Unlike a snapshot lease, this is the steady-state
/// owner: every Apply call re-resolves the current player and writes the phase
/// truth directly.
/// </summary>
public sealed class AnkaluteExplorationPhaseGate
{
    private const int PhaseControlPriority = 9500;

    private readonly Func<HuntFlowDataBus> resolveHuntData;
    private readonly Func<CharacterControlAuthority> resolveAuthority;
    private readonly Action<bool> setGameplayInputEnabled;
    private readonly Action<bool> setPickupBlocked;
    private readonly object controlOwner = new object();
    private CharacterControlAuthority controlledAuthority;

    public bool IsExplorationEnabled { get; private set; } = true;

    public AnkaluteExplorationPhaseGate(GameFlowRoot root)
        : this(
            () => ResolveHuntData(root),
            () => ResolvePlayer(root)?.controlAuthority,
            enabled =>
            {
                CharacterRoot player = ResolvePlayer(root);
                player?.inputHandler?.SetGameplayInputEnabled(enabled);
            },
            blocked =>
            {
                CharacterRoot player = ResolvePlayer(root);
                player?.characterPickupController?.SetInputBlocked(blocked);
            })
    {
    }

    public AnkaluteExplorationPhaseGate(
        Func<HuntFlowDataBus> resolveHuntData,
        Func<CharacterControlAuthority> resolveAuthority,
        Action<bool> setGameplayInputEnabled,
        Action<bool> setPickupBlocked)
    {
        this.resolveHuntData = resolveHuntData;
        this.resolveAuthority = resolveAuthority;
        this.setGameplayInputEnabled = setGameplayInputEnabled;
        this.setPickupBlocked = setPickupBlocked;
    }

    public void Apply(bool explorationEnabled)
    {
        HuntFlowDataBus huntData = resolveHuntData?.Invoke();
        if (huntData != null)
            huntData.isPaused = !explorationEnabled;

        CharacterControlAuthority currentAuthority =
            resolveAuthority?.Invoke();
        if (explorationEnabled)
        {
            ReleaseControlledAuthority();
        }
        else
        {
            if (!ReferenceEquals(controlledAuthority, currentAuthority))
            {
                ReleaseControlledAuthority();
                controlledAuthority = currentAuthority;
            }

            controlledAuthority?.SetOverride(
                controlOwner,
                new CharacterControlOverride(
                    AllControlChannels,
                    PhaseControlPriority,
                    CharacterFacingMode.Hold,
                    AimTrackingMode.Hold));
        }

        setGameplayInputEnabled?.Invoke(explorationEnabled);
        setPickupBlocked?.Invoke(!explorationEnabled);
        IsExplorationEnabled = explorationEnabled;
    }

    public void Release()
    {
        ReleaseControlledAuthority();
        HuntFlowDataBus huntData = resolveHuntData?.Invoke();
        if (huntData != null)
            huntData.isPaused = false;
        setGameplayInputEnabled?.Invoke(true);
        setPickupBlocked?.Invoke(false);
        IsExplorationEnabled = true;
    }

    private void ReleaseControlledAuthority()
    {
        controlledAuthority?.ReleaseOverride(controlOwner);
        controlledAuthority = null;
    }

    private static CharacterControlChannel AllControlChannels =>
        CharacterControlChannel.Move |
        CharacterControlChannel.Facing |
        CharacterControlChannel.AimTracking |
        CharacterControlChannel.PrimaryAttack |
        CharacterControlChannel.SecondaryAim |
        CharacterControlChannel.Reload |
        CharacterControlChannel.WeaponSwitch |
        CharacterControlChannel.Jump |
        CharacterControlChannel.Interact;

    private static HuntFlowDataBus ResolveHuntData(GameFlowRoot root)
    {
        return root != null && root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
    }

    private static CharacterRoot ResolvePlayer(GameFlowRoot root)
    {
        return ResolveHuntData(root)?.player;
    }
}
