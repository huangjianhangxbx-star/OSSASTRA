using System;
using UnityEngine;

public sealed class GameplaySuspensionCoordinator : IGameplaySuspensionService
{
    private const int GalControlPriority = 10000;

    private readonly GameFlowRoot gameFlowRoot;
    private readonly object characterControlOwner = new();
    private SuspensionLease activeLease;

    public GameplaySuspensionCoordinator(GameFlowRoot gameFlowRoot)
    {
        this.gameFlowRoot = gameFlowRoot;
    }

    public bool TryAcquire(
        GalPresentationMode mode,
        out IDisposable suspensionLease)
    {
        suspensionLease = null;

        if (activeLease != null)
            return false;

        if (mode == GalPresentationMode.ConcurrentOverlay)
        {
            activeLease = new SuspensionLease(ReleaseActiveLease);
            suspensionLease = activeLease;
            return true;
        }

        HuntFlowDataBus huntData = gameFlowRoot != null && gameFlowRoot.huntFlow != null
            ? gameFlowRoot.huntFlow.huntFlowDataBus
            : null;
        if (gameFlowRoot == null || huntData == null)
            return false;

        float previousTimeScale = Time.timeScale;
        bool previousHuntPaused = huntData.isPaused;
        CharacterRoot player = huntData.player;

        huntData.isPaused = true;
        Time.timeScale = 0f;

        if (player != null)
        {
            player.controlAuthority.SetOverride(
                characterControlOwner,
                new CharacterControlOverride(
                    AllControlChannels,
                    GalControlPriority,
                    CharacterFacingMode.Hold,
                    AimTrackingMode.Hold));
        }

        activeLease = new SuspensionLease(() =>
        {
            if (player != null)
                player.controlAuthority.ReleaseOverride(characterControlOwner);

            huntData.isPaused = previousHuntPaused;
            Time.timeScale = previousTimeScale;
            ReleaseActiveLease();
        });
        suspensionLease = activeLease;
        return true;
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

    private void ReleaseActiveLease()
    {
        activeLease = null;
    }

    private sealed class SuspensionLease : IDisposable
    {
        private Action release;

        public SuspensionLease(Action release)
        {
            this.release = release;
        }

        public void Dispose()
        {
            Action action = release;
            release = null;
            action?.Invoke();
        }
    }
}
