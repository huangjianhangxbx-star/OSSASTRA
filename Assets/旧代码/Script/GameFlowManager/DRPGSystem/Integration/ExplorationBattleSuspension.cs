using System;
using System.Collections.Generic;

/// <summary>
/// - 暂停 Hunt 探索流程。
//-通过 `CharacterControlAuthority` 高优先级覆盖，阻止移动、朝向、瞄准和动作输入。
//- 保持探索角色与场景对象仍然存在。
//- 不依赖 `Time.timeScale = 0`。
/// </summary>

public sealed class ExplorationBattleSuspension
{
    private const int BattleControlPriority = 9000;

    private readonly Func<CharacterControlAuthority> resolveAuthority;
    private readonly Func<bool> readPaused;
    private readonly Action<bool> writePaused;
    private readonly IReadOnlyList<IDRPGExplorationPauseTarget> pauseTargets;
    private readonly object characterControlOwner = new object();
    private SuspensionLease activeLease;

    public ExplorationBattleSuspension(GameFlowRoot root)
        : this(
            () => ResolveAuthority(root),
            () => ReadHuntPause(root),
            value => WriteHuntPause(root, value),
            ResolvePauseTargets(root))
    {
    }

    public ExplorationBattleSuspension(
        CharacterControlAuthority authority,
        Func<bool> readPaused,
        Action<bool> writePaused,
        IReadOnlyList<IDRPGExplorationPauseTarget> pauseTargets)
        : this(
            () => authority,
            readPaused,
            writePaused,
            pauseTargets)
    {
    }

    private ExplorationBattleSuspension(
        Func<CharacterControlAuthority> resolveAuthority,
        Func<bool> readPaused,
        Action<bool> writePaused,
        IReadOnlyList<IDRPGExplorationPauseTarget> pauseTargets)
    {
        this.resolveAuthority = resolveAuthority;
        this.readPaused = readPaused;
        this.writePaused = writePaused;
        this.pauseTargets = pauseTargets ?? Array.Empty<IDRPGExplorationPauseTarget>();
    }

    public bool IsSuspended => activeLease != null;

    public bool TryAcquire(out IDisposable lease)
    {
        lease = null;
        if (activeLease != null)
            return false;

        bool previousPaused = readPaused != null && readPaused();
        writePaused?.Invoke(true);

        CharacterControlAuthority acquiredAuthority = resolveAuthority?.Invoke();
        acquiredAuthority?.SetOverride(
            characterControlOwner,
            new CharacterControlOverride(
                AllControlChannels,
                BattleControlPriority,
                CharacterFacingMode.Hold,
                AimTrackingMode.Hold));

        for (int i = 0; i < pauseTargets.Count; i++)
            pauseTargets[i]?.SetBattlePaused(true);

        activeLease = new SuspensionLease(() =>
        {
            acquiredAuthority?.ReleaseOverride(characterControlOwner);

            for (int i = 0; i < pauseTargets.Count; i++)
                pauseTargets[i]?.SetBattlePaused(false);

            writePaused?.Invoke(previousPaused);
            activeLease = null;
        });
        lease = activeLease;
        return true;
    }

    public static CharacterControlChannel AllControlChannels =>
        CharacterControlChannel.Move |
        CharacterControlChannel.Facing |
        CharacterControlChannel.AimTracking |
        CharacterControlChannel.PrimaryAttack |
        CharacterControlChannel.SecondaryAim |
        CharacterControlChannel.Reload |
        CharacterControlChannel.WeaponSwitch |
        CharacterControlChannel.Jump |
        CharacterControlChannel.Interact;

    private static CharacterControlAuthority ResolveAuthority(GameFlowRoot root)
    {
        HuntFlowDataBus huntData = ResolveHuntData(root);
        return huntData != null && huntData.player != null
            ? huntData.player.controlAuthority
            : null;
    }

    private static bool ReadHuntPause(GameFlowRoot root)
    {
        HuntFlowDataBus huntData = ResolveHuntData(root);
        return huntData != null && huntData.isPaused;
    }

    private static void WriteHuntPause(GameFlowRoot root, bool value)
    {
        HuntFlowDataBus huntData = ResolveHuntData(root);
        if (huntData != null)
            huntData.isPaused = value;
    }

    private static HuntFlowDataBus ResolveHuntData(GameFlowRoot root)
    {
        return root != null && root.huntFlow != null
            ? root.huntFlow.huntFlowDataBus
            : null;
    }

    private static IReadOnlyList<IDRPGExplorationPauseTarget> ResolvePauseTargets(
        GameFlowRoot root)
    {
        if (root == null)
            return Array.Empty<IDRPGExplorationPauseTarget>();

        UnityEngine.MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true);
        var targets = new List<IDRPGExplorationPauseTarget>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDRPGExplorationPauseTarget target)
                targets.Add(target);
        }

        return targets;
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
