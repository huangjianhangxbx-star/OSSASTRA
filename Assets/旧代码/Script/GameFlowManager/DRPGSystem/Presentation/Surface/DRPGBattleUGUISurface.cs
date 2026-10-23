using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DRPGBattleUGUISurface :
    MonoBehaviour,
    IDRPGBattleTargetFeedbackSurface
{
    [SerializeField] private BattleBackgroundPresenter backgroundPresenter;
    [SerializeField] private BattleTimelineView timelineView;
    [SerializeField] private BattlePartyHud partyHud;
    [SerializeField] private BattleActorPresenter actorPresenter;
    [SerializeField] private BattleSkillMenu skillMenu;
    [SerializeField] private BattleCommandMenu commandMenu;
    [SerializeField] private BattleWeaponMenu weaponMenu;
    [SerializeField] private BattleResonanceMenu resonanceMenu;
    [SerializeField] private BattleRecoveryBagView recoveryBagView;
    [SerializeField] private BattlefieldEnemyRosterView enemyRoster;
    [SerializeField] private BattleImpactFeedbackPresenter impactFeedbackPresenter;
    [SerializeField] private BattleAttackCutInPresenter attackCutInPresenter;
    [SerializeField] private BattleDamageBrokenCutInPresenter damageBrokenCutInPresenter;
    [SerializeField] private BattleTurnQuotePresenter turnQuotePresenter;
    [SerializeField] private BattleInteractiveCombatView interactiveCombatView;
    [SerializeField] private MonoBehaviour audioCueSinkComponent;
    [SerializeField] private TMP_Text targetPrompt;
    [SerializeField] private BattleFormationOverlayController formationOverlayController;
    [SerializeField] private BattleWorldRosterPresenter worldRosterPresenter;
    [SerializeField] private BattleWorldTargetingPresenter worldTargetingPresenter;
    [SerializeField] private BattleFixedSkillBar battleV2SkillBar;
    [SerializeField] private BattleCardHandView battleV2CardHand;
    [SerializeField] private BattleCardTargetArrow battleV2CardArrow;
    [SerializeField] private BattlePipRow battleV2PipRow;
    [SerializeField] private DRPGBattleCardPreviewSO[] battleV2PreviewCards =
        new DRPGBattleCardPreviewSO[4];
    [SerializeField] private TMP_Text battleV2ActorName;
    [SerializeField] private TMP_Text battleV2ActorStatus;

    private BattleWorldRuntimeHost worldHost;

    private DRPGBattleSession session;
    private DRPGBattleProjectionLeaseArbiter projectionArbiter;
    internal DRPGBattleProjectionLeaseArbiter ProjectionArbiter =>
        projectionArbiter ?? (projectionArbiter = new DRPGBattleProjectionLeaseArbiter(this));
    internal long BattleGeneration => session != null ? session.BattleGeneration : 0L;
    internal Transform ProjectionWorldRoot => worldHost != null && worldHost.WorldInstance != null
        ? worldHost.WorldInstance.transform : worldRosterPresenter != null ? worldRosterPresenter.transform : null;
    internal Camera ProjectionCamera => worldHost != null ? worldHost.BattleCamera : null;
    internal long ProjectionEpoch { get; private set; }
    internal event Action CanonicalProjectionClosing;

    internal IReadOnlyList<CanvasGroup> CaptureCloseupFloatingGroups()
    {
        var result = new List<CanvasGroup>();
        if (worldTargetingPresenter != null)
            result.AddRange(worldTargetingPresenter.CaptureFloatingGroups());
        if (targetPrompt != null)
        {
            CanvasGroup prompt = targetPrompt.GetComponent<CanvasGroup>();
            if (prompt == null) prompt = targetPrompt.gameObject.AddComponent<CanvasGroup>();
            if (!result.Contains(prompt)) result.Add(prompt);
        }
        return result;
    }

    internal DRPGBattleActionProjectionLease AcquireProjectionLease(
        BattleTransactionIdentity owner)
    {
        ProjectionLeaseAcquireResult result = ProjectionArbiter.TryAcquire(owner);
        if (!result.Accepted) throw new InvalidOperationException(result.Reason);
        return result.Lease;
    }

    internal bool DeferCanonicalProjection(string operation, int runtimeId = 0) =>
        projectionArbiter != null && projectionArbiter.DeferCanonical(operation, runtimeId);

    internal void InstallProjectionGuard(DRPGBattleProjectionLeaseArbiter guard)
    {
        worldRosterPresenter?.SetProjectionGuard(guard);
        worldTargetingPresenter?.SetProjectionGuard(guard);
        partyHud?.SetProjectionGuard(guard);
        enemyRoster?.SetProjectionGuard(guard);
    }

    internal void BeginCanonicalProjectionClose()
    {
        ProjectionEpoch++;
        var listeners = CanonicalProjectionClosing;
        if (listeners == null) return;
        foreach (Action listener in listeners.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception)
            { projectionArbiter.Record("CanonicalObserverFault:" + exception.GetType().Name); }
        }
    }

    internal void ReconcileCanonicalProjection(BattleCanonicalProjectionPermit permit)
    {
        if (session == null) return;
        var snapshot = DRPGBattlePresentationSnapshot.Capture(session);
        bool firstBind = LastSnapshot == null;
        LastSnapshot = snapshot;
        projectionArbiter.Record("Canonical:Combatants");
        RenderParticipantBindings(snapshot, permit);
        projectionArbiter.Record("Canonical:FormationTimeline");
        RenderFormationBindings(snapshot, true, permit);
        timelineView?.BindImmediate(snapshot.Timeline);
        projectionArbiter.Record("Canonical:TurnOutcome");
        RenderTurnBindings(snapshot);
    }

    internal void ApplyLeasedParticipant(BattleProjectedParticipant participant)
    {
        partyHud?.ApplyLeasedParticipant(participant);
        enemyRoster?.ApplyLeasedParticipant(participant);
        worldTargetingPresenter?.ApplyLeasedParticipant(participant);
    }

    internal bool HasCompleteBattlefieldProjection()
    {
        if (session == null || worldRosterPresenter == null ||
            !worldRosterPresenter.HasConsistentPool) return false;
        var expected = new HashSet<int>();
        foreach (var participant in session.Participants)
            if (participant.IsAlive && participant.IsDeployed) expected.Add(participant.RuntimeId);
        if (expected.Count == 0 || expected.Count != worldRosterPresenter.ActiveViews.Count) return false;
        foreach (var view in worldRosterPresenter.ActiveViews)
            if (view == null || !view.gameObject.activeInHierarchy ||
                view.BoundActor == null || view.BoundActor.RuntimeId != view.RuntimeId ||
                !expected.Remove(view.RuntimeId)) return false;
        return expected.Count == 0;
    }

    public DRPGBattlePresentationSnapshot LastSnapshot { get; private set; }
    public IBattleAudioCueSink AudioCueSink =>
        audioCueSinkComponent as IBattleAudioCueSink ??
        NullBattleAudioCueSink.Instance;
    public BattleSkillMenu SkillMenu => skillMenu;
    public BattleCommandMenu CommandMenu => commandMenu;
    public BattleWeaponMenu WeaponMenu => weaponMenu;
    public BattleResonanceMenu ResonanceMenu => resonanceMenu;
    public BattleRecoveryBagView RecoveryBagView => recoveryBagView;
    public BattlePartyHud PartyHud => partyHud;
    public BattlefieldEnemyRosterView EnemyRoster => enemyRoster;
    public BattleTimelineView TimelineView => timelineView;
    public BattleActorPresenter ActorPresenter => actorPresenter;
    public BattleImpactFeedbackPresenter ImpactFeedbackPresenter =>
        impactFeedbackPresenter;
    public BattleDamageBrokenCutInPresenter DamageBrokenCutInPresenter =>
        damageBrokenCutInPresenter;
    public BattleTurnQuotePresenter TurnQuotePresenter => turnQuotePresenter;
    public BattleInteractiveCombatView InteractiveCombatView =>
        interactiveCombatView;
    public BattleWorldRosterPresenter WorldRosterPresenter =>
        worldRosterPresenter;
    public BattleWorldTargetingPresenter WorldTargetingPresenter =>
        worldTargetingPresenter;
    public BattleFixedSkillBar BattleV2SkillBar => battleV2SkillBar;
    public BattleCardHandView BattleV2CardHand => battleV2CardHand;
    public BattleCardTargetArrow BattleV2CardArrow => battleV2CardArrow;
    public BattlePipRow BattleV2PipRow => battleV2PipRow;
    public IReadOnlyList<DRPGBattleCardPreviewSO> BattleV2PreviewCards =>
        battleV2PreviewCards;

    public void ConfigureWorldPresentation(
        BattleWorldRuntimeHost host,
        BattleWorldRosterPresenter roster,
        BattleWorldTargetingPresenter targeting)
    {
        if (DeferCanonicalProjection("Surface.ReplaceWorldBinding")) return;
        worldHost = host;
        worldRosterPresenter = roster;
        worldTargetingPresenter = targeting;
    }

    public bool TryResolveParticipantAnchor(
        int runtimeId,
        out BattleImpactAnchor anchor)
    {
        if (partyHud != null &&
            partyHud.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        if (worldTargetingPresenter != null &&
            worldTargetingPresenter.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        if (enemyRoster != null &&
            enemyRoster.TryResolveImpactAnchor(runtimeId, out anchor))
        {
            return true;
        }
        anchor = null;
        return false;
    }

    private void OnEnable()
    {
        if (attackCutInPresenter != null)
        {
            attackCutInPresenter.VisibilityChanged +=
                HandleAttackCutInVisibilityChanged;
            HandleAttackCutInVisibilityChanged(attackCutInPresenter.IsVisible);
        }
    }

    private void OnDisable()
    {
        projectionArbiter?.Abandon();
        if (attackCutInPresenter != null)
        {
            attackCutInPresenter.VisibilityChanged -=
                HandleAttackCutInVisibilityChanged;
        }
        actorPresenter?.SetPresentationSuppressed(false);
    }

    public void Bind(DRPGBattleSession battleSession)
    {
        if (DeferCanonicalProjection("Surface.ReplaceSession")) return;
        session = battleSession ??
            throw new ArgumentNullException(nameof(battleSession));
        formationOverlayController?.Configure(enemyRoster, partyHud);
        impactFeedbackPresenter?.PrepareForBattle();
    }

    public void ConfigureEquipment(DRPGEquipmentRoot equipmentRoot)
    {
        recoveryBagView?.Bind(equipmentRoot);
    }

    public void Unbind()
    {
        projectionArbiter?.Abandon();
        CancelPresentation(BattlePresentationCancelReason.ScopeEnded);
        ClearTargetFeedback();
        SetTargetingEnabled(false);
        skillMenu?.SetInputEnabled(false);
        skillMenu?.Unbind();
        commandMenu?.Unbind();
        resonanceMenu?.Unbind();
        recoveryBagView?.Unbind();
        partyHud?.SetTargetingEnabled(false);
        formationOverlayController?.ResetState();
        worldRosterPresenter?.Clear();
        worldTargetingPresenter?.Clear();
        session = null;
        LastSnapshot = null;
    }

    public void SetBackground(Sprite background)
    {
        if (worldHost != null)
        {
            backgroundPresenter?.Bind(null);
            worldHost.SetBackground(background);
            return;
        }
        backgroundPresenter?.Bind(background);
    }

    public void RenderInitial(DRPGBattlePresentationSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        RenderCombatants(snapshot);
        RenderTimelineImmediate(snapshot.Timeline);
        ClearTargetFeedback();
        SetTargetingEnabled(false);
    }

    public void RenderCombatants(DRPGBattlePresentationSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        if (DeferCanonicalProjection("Surface.Combatants")) return;
        RenderCombatantsCore(snapshot);
    }

    private void RenderCombatantsCore(DRPGBattlePresentationSnapshot snapshot)
    {
        bool firstBind = LastSnapshot == null;
        LastSnapshot = snapshot;
        RenderParticipantBindings(snapshot);
        RenderFormationBindings(snapshot, firstBind);
        RenderTurnBindings(snapshot);
    }

    private void RenderParticipantBindings(DRPGBattlePresentationSnapshot snapshot,
        BattleCanonicalProjectionPermit permit = null)
    {
        partyHud?.BindCanonical(snapshot.PartyMembers, permit);
        enemyRoster?.BindCanonical(snapshot.Enemies, permit);
    }

    private void RenderFormationBindings(DRPGBattlePresentationSnapshot snapshot, bool firstBind,
        BattleCanonicalProjectionPermit permit = null)
    {
        worldRosterPresenter?.BindCanonical(
            snapshot.PartyMembers,
            snapshot.Enemies,
            firstBind, permit);
        worldTargetingPresenter?.BindCanonical(snapshot.Enemies, permit);
        enemyRoster?.SetWorldPresentationMode(worldRosterPresenter != null);
    }

    private void RenderTurnBindings(DRPGBattlePresentationSnapshot snapshot)
    {
        actorPresenter?.Bind(snapshot.CurrentActor);
        skillMenu?.BindAvailability(session, snapshot.CurrentSkillAvailability);
        if (battleV2SkillBar != null && snapshot.CurrentActor != null)
        {
            if (battleV2ActorName != null)
                battleV2ActorName.text = snapshot.CurrentActor.DisplayName;
            if (battleV2ActorStatus != null)
            {
                battleV2ActorStatus.text =
                    "HP  " + Mathf.CeilToInt(snapshot.CurrentActor.CurrentHp) +
                    " / " + Mathf.CeilToInt(snapshot.CurrentActor.MaxHp) +
                    "\n架势  " + Mathf.CeilToInt(
                        snapshot.CurrentActor.ProjectionBaseline.Posture);
            }
            battleV2SkillBar.Bind(BattleHudSnapshotBuilder.Build(
                BattleGeneration,
                snapshot.CurrentActor.Participant,
                ProjectionEpoch,
                false));
            GetComponent<BattleHudBinding>()?.RefreshCurrent();
        }
        commandMenu?.Bind(session);
        enemyRoster?.SetReinforcementStatus(
            snapshot.HasIncomingEnemyWarning,
            snapshot.ReserveEnemyCount);
    }

    public void RenderTimelineImmediate(
        IReadOnlyList<DRPGBattleParticipantViewModel> timeline)
    {
        if (DeferCanonicalProjection("Surface.Timeline")) return;
        timelineView?.BindImmediate(timeline ??
            Array.Empty<DRPGBattleParticipantViewModel>());
    }

    public void SetInputEnabled(bool enabled)
    {
        skillMenu?.SetInputEnabled(enabled);
        battleV2SkillBar?.SetInputEnabled(enabled);
        commandMenu?.SetInputEnabled(enabled);
        weaponMenu?.SetInputEnabled(enabled);
        resonanceMenu?.SetInputEnabled(enabled);
    }

    public void SetTargetingEnabled(
        bool enabled,
        DRPGSkillSO skill = null)
    {
        enemyRoster?.SetTargetingEnabled(
            enabled,
            target => skill == null ||
                session != null && session.CanCurrentActorTarget(target, skill));
        worldTargetingPresenter?.SetTargetingEnabled(
            enabled,
            runtimeId => skill == null ||
                session != null &&
                FindParticipant(runtimeId) is BattleParticipantRuntime target &&
                session.CanCurrentActorTarget(target, skill));
    }

    public void SetPartyTargetingEnabled(bool enabled)
    {
        partyHud?.SetTargetingEnabled(enabled);
    }

    public void ShowCommandRoot()
    {
        commandMenu?.ShowCommandRoot();
    }

    public void ShowResonanceSkillList()
    {
        commandMenu?.ShowResonanceSkillList();
    }

    public void SetTargetSelectionMode(bool enabled)
    {
        commandMenu?.SetTargetSelectionMode(enabled);
    }

    public void ApplyTargetFeedback(
        int runtimeId,
        DRPGBattleTargetFeedback feedback)
    {
        timelineView?.SetTargetFeedback(runtimeId, feedback);
        partyHud?.SetTargetFeedback(runtimeId, feedback);
        enemyRoster?.SetTargetFeedback(runtimeId, feedback);
        worldTargetingPresenter?.SetTargetFeedback(runtimeId, feedback);
        if (actorPresenter != null && actorPresenter.BoundRuntimeId == runtimeId)
            actorPresenter.SetTargetFeedback(feedback);
    }

    public void ClearTargetFeedback()
    {
        timelineView?.ClearTargetFeedback();
        partyHud?.ClearTargetFeedback();
        enemyRoster?.ClearTargetFeedback();
        worldTargetingPresenter?.ClearTargetFeedback();
        actorPresenter?.SetTargetFeedback(DRPGBattleTargetFeedback.None);
        SetTargetPrompt(string.Empty);
    }

    public void SetTargetPrompt(string value)
    {
        if (targetPrompt == null)
            return;

        bool hasValue = !string.IsNullOrEmpty(value);
        targetPrompt.gameObject.SetActive(hasValue);
        targetPrompt.text = hasValue ? value : string.Empty;
    }

    public void CancelPresentation(BattlePresentationCancelReason reason)
    {
        timelineView?.KillActiveTransition(reason);
        impactFeedbackPresenter?.CancelAll(reason);
        attackCutInPresenter?.Cancel(reason);
        damageBrokenCutInPresenter?.Cancel(reason);
        turnQuotePresenter?.Hide(reason);
        interactiveCombatView?.HideImmediate();
        interactiveCombatView?.ClearTelegraph();
    }

    private void HandleAttackCutInVisibilityChanged(bool visible)
    {
        actorPresenter?.SetPresentationSuppressed(visible);
    }

    private BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        if (session == null)
            return null;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            if (session.Participants[i].RuntimeId == runtimeId)
                return session.Participants[i];
        }
        return null;
    }
}
