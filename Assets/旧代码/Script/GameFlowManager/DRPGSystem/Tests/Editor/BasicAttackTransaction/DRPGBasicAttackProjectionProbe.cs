#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using DG.Tweening;
using UnityEngine;

public static class DRPGBasicAttackProjectionProbe
{
    public static void LeaseBlocksRealEventBridgeRebind()
    {
        foreach (bool lethal in new[] { false, true })
        using (var f = new Fixture(lethal))
        {
            f.AcquireLeaseIfAvailable();
            f.InjectImpact();
            Check(f.Bridge.PendingSignalCount > 0, "Session did not queue ParticipantChanged.");
            f.Bridge.FlushQueuedSignals();
            f.AssertPinned();
        }
    }

    public static IEnumerator ProductionLeaseBlocksRebind()
    {
        foreach (string boundary in new[] { "Stage5", "recoil-out", "recoil-return", "Stage8" })
        foreach (bool lethal in new[] { false, true })
        using (var f = new Fixture(lethal, true))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var clock = f.Root.AddComponent<UnityRenderFrameFence>();
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                f.Pool.TryGet(f.Player.RuntimeId, out var actor);
                var actorHome = actor.transform.position; var targetHome = f.Target.transform.position;
                var scale = f.Target.transform.localScale;
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action,
                    new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId),
                    new Vector3(-1, 0, 0), new Vector3(1, 0, 0), clock.Now));
                PresentationReceipt recoil = null, actorReturn = null, targetReturn = null;
                if (boundary != "Stage5")
                {
                    double hitAt = clock.Now;
                    recoil = motion.Recoil(new RecoilMotionCommand(new HitOutcomeBeatId(f.Owner.Action, 0),
                        new ParticipantId(f.Enemy.RuntimeId), 1, .45f, hitAt));
                    if (boundary == "recoil-return" || boundary == "Stage8")
                    {
                        while ((boundary == "Stage8" ? !recoil.Completion.IsCompleted : clock.Now - hitAt < .11d) &&
                            clock.Now - hitAt < 2d)
                        { yield return new WaitForEndOfFrame(); motion.Tick(); }
                        Check(boundary == "Stage8" || !recoil.Completion.IsCompleted,
                            "Production frame skipped the recoil-return injection window.");
                    }
                    if (boundary == "Stage8")
                    {
                        double returnAt = clock.Now;
                        actorReturn = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId), returnAt));
                        targetReturn = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Enemy.RuntimeId), returnAt));
                    }
                }
                Vector3 leasedPosition = f.Target.transform.position;
                f.InjectImpact();
                Check(f.Bridge.PendingSignalCount > 0, boundary + ": signal not queued.");
                yield return new WaitForEndOfFrame();
                Check(f.Bridge.PendingSignalCount == 0, boundary + ": LateUpdate did not consume signal.");
                f.AssertPinned();
                Check(lease.Count == 6 && f.Pool.ReleaseCount == 0 &&
                    f.Target.transform.position.Equals(leasedPosition) && f.Target.transform.localScale == scale,
                    boundary + ": canonical refresh changed the real leased motion or roster.");
                motion.Tick();
                if (recoil != null)
                {
                    double deadline = clock.Now + 2d;
                    while (!recoil.Completion.IsCompleted && clock.Now < deadline)
                    { yield return new WaitForEndOfFrame(); motion.Tick(); }
                    Check(recoil.Completion.IsCompleted && recoil.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                        boundary + ": the real recoil was interrupted.");
                }
                if (actorReturn == null)
                {
                    double returnAt = clock.Now;
                    actorReturn = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId), returnAt));
                    targetReturn = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Enemy.RuntimeId), returnAt));
                }
                double returnDeadline = clock.Now + 2d;
                while ((!actorReturn.Completion.IsCompleted || !targetReturn.Completion.IsCompleted) && clock.Now < returnDeadline)
                { yield return new WaitForEndOfFrame(); motion.Tick(); }
                Check(actorReturn.Completion.IsCompleted && targetReturn.Completion.IsCompleted &&
                    actor.transform.position.Equals(actorHome) && f.Target.transform.position.Equals(targetHome) &&
                    f.Pool.ReleaseCount == 0, boundary + ": return did not retain both identities to exact home.");
            }
        }
    }

    public static void LeasedViewIdentityIsStable()
    {
        using (var f = new Fixture(true))
        {
            var lease = f.Surface.AcquireProjectionLease(f.Owner);
            try
            {
                Check(lease.Count == 2, "Lease did not pin the complete battlefield.");
                var baseline = lease.LockBaseline;
                f.InjectImpact();
                f.Pool.Release(f.Enemy.RuntimeId);
                f.Pool.ReleaseAll();
                f.Roster.Clear();
                f.Roster.Bind(Array.Empty<DRPGBattleParticipantViewModel>(),
                    Array.Empty<DRPGBattleParticipantViewModel>());
                f.Target.ResetForPool();
                f.Target.Bind(999);
                f.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Session));
                f.AssertPinned();
                Check(f.Roster.ActiveViews.Count == 2 && f.Pool.ReleaseCount == 0,
                    "Rejected pool release still cleared the roster index.");
                Vector3 home = baseline.Actors[1].Home.Position;
                f.Target.transform.position += Vector3.right;
                var conflict = lease.Capture(DRPGBattleProjectionCheckpointKind.ConflictCheckpoint);
                Check(baseline.Actors[1].Motion.Position == home &&
                    conflict.Actors[1].Motion.Position != home &&
                    conflict.Actors[1].Home.Position == home,
                    "A live transform write mutated the immutable Home/checkpoint.");
            }
            finally { lease.Dispose(); }
        }
    }

    public static void CursorIsOwnedOrderedAndSemanticOnly()
    {
        using (var f = new Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var before = lease.GetPresented(f.Enemy.RuntimeId);
            var after = new BattleProjectedParticipant(before.RuntimeId, before.Hp - 1f, before.MaxHp);
            var wrong = BattleTransactionIdentity.ForRootAction(
                new ActionIdentity(f.Session.BattleGeneration + 1, 1, 71));
            Check(!lease.Apply(new BattleProjectionCursor(wrong, 0, before, after)).Accepted,
                "A foreign cursor was accepted.");
            Check(!lease.Apply(new BattleProjectionCursor(f.Owner, 1, before, after)).Accepted,
                "Future cursor skipped an item.");
            Vector3 position = f.Target.transform.position;
            Vector3 scale = f.Target.transform.localScale;
            Check(lease.Apply(new BattleProjectionCursor(f.Owner, 0, before, after)).Accepted,
                "Matching cursor was rejected.");
            Check(!lease.Apply(new BattleProjectionCursor(f.Owner, 0, before, after)).Accepted,
                "Duplicate cursor applied twice.");
            Check(f.Target.transform.position == position && f.Target.transform.localScale == scale &&
                f.Enemy.CurrentHp == before.Hp && f.Pool.ReleaseCount == 0,
                "A semantic cursor moved/rebound an actor or mutated rules.");
        }
    }

    public static void CanonicalFlushOccursOnce()
    {
        using (var f = new Fixture(true))
        {
            var arbiter = f.Surface.ProjectionArbiter;
            var lease = f.Surface.AcquireProjectionLease(f.Owner);
            bool reentryRejected = false;
            f.Surface.CanonicalProjectionClosing += () =>
                reentryRejected = !arbiter.TryAcquire(f.Owner).Accepted;
            f.InjectImpact();
            f.Bridge.FlushQueuedSignals();
            f.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Session));
            Check(!arbiter.TryAcquire(f.Owner).Accepted, "Overlapping lease was allowed.");
            lease.Dispose();
            lease.Dispose();
            Check(reentryRejected && arbiter.CanonicalFlushCount == 1 && !arbiter.IsLeased &&
                f.Bridge.PendingSignalCount == 0, "Close was duplicated/reentrant or replayed old events.");
            Check(f.Pool.ActiveCount == 1 && f.Pool.ReleaseCount == 1,
                "Canonical close did not release the defeated enemy exactly once.");
            var stages = new System.Collections.Generic.List<string>(arbiter.Trace);
            int combatants = stages.IndexOf("Canonical:Combatants");
            int timeline = stages.IndexOf("Canonical:FormationTimeline");
            int turn = stages.IndexOf("Canonical:TurnOutcome");
            Check(combatants >= 0 && timeline > combatants && turn > timeline,
                "Canonical reconciliation must order combatants, formation/timeline, turn/outcome.");
        }
    }

    public static void AuthoritativeChannelDoesNotAdvanceProjection()
    {
        using (var f = new Fixture(true))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            f.InjectImpact();
            f.Bridge.FlushQueuedSignals();
            f.AssertPinned();
            bool found = false;
            foreach (var item in f.Bridge.AuthoritativeParticipants)
                if (item.RuntimeId == f.Enemy.RuntimeId && item.Hp == 0f) found = true;
            Check(found, "Authoritative snapshots must retain rule-dead values without revealing them.");
            Check(lease.GetPresented(f.Enemy.RuntimeId).Hp > 0f,
                "Authoritative data advanced the presentation cursor.");
        }
    }

    public static void HpCursorDoesNotInvokeGameplayListeners()
    {
        using (var f = new Fixture(false))
        {
            var obj = new GameObject("PartyHp", typeof(RectTransform));
            obj.transform.SetParent(f.Root.transform, false);
            var slider = obj.AddComponent<UnityEngine.UI.Slider>();
            var view = obj.AddComponent<BattlePartyMemberView>();
            Set(view, "hpBar", slider);
            var hud = f.Root.AddComponent<BattlePartyHud>();
            Set(hud, "memberViews", new System.Collections.Generic.List<BattlePartyMemberView> { view });
            Set(f.Surface, "partyHud", hud);
            hud.Bind(f.Initial.PartyMembers);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var before = lease.GetPresented(f.Player.RuntimeId);
                var after = new BattleProjectedParticipant(before.RuntimeId, before.Hp / 2f, before.MaxHp);
                var cursor = new BattleProjectionCursor(f.Owner, 0, before, after);
                int calls = 0;
                slider.onValueChanged.AddListener(_ =>
                {
                    calls++;
                    if (calls == 1) lease.Apply(cursor);
                });
                Check(lease.Apply(cursor).Accepted, "Valid HUD cursor rejected.");
                Check(calls == 0 && lease.NextCursorOrdinal == 1,
                    "HP projection invoked gameplay callbacks/reentered the same cursor.");
            }
        }
    }

    public static void LeaseRejectsBindingReplacementAndIncompleteRoster()
    {
        using (var f = new Fixture(false))
        using (var other = new Fixture(false))
        {
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                f.Surface.Bind(other.Session);
                f.Surface.ConfigureWorldPresentation(null, other.Roster, null);
                Check(f.Surface.BattleGeneration == f.Session.BattleGeneration &&
                    ReferenceEquals(f.Surface.WorldRosterPresenter, f.Roster),
                    "Binding replacement escaped the active projection lease.");
            }
        }
        using (var f = new Fixture(false))
        {
            f.Roster.Clear();
            var result = f.Surface.ProjectionArbiter.TryAcquire(f.Owner);
            result.Lease?.Dispose();
            Check(!result.Accepted, "Empty/incomplete battlefield acquired a projection lease.");
        }
    }

    public static void ClosingObserverCannotSkipCanonicalReconciliation()
    {
        using (var f = new Fixture(true))
        {
            var lease = f.Surface.AcquireProjectionLease(f.Owner);
            f.Surface.CanonicalProjectionClosing += () => { throw new InvalidOperationException("observer test"); };
            f.InjectImpact();
            lease.Dispose();
            Check(f.Pool.ActiveCount == 1 && !f.Surface.ProjectionArbiter.IsLeased,
                "Throwing close observer stranded the canonical projection.");
        }
    }

    public static void CanonicalLeafReentryCannotReleaseActors()
    {
        using (var f = new Fixture(false))
        {
            var obj = new GameObject("CanonicalReentryHp", typeof(RectTransform));
            obj.transform.SetParent(f.Root.transform, false);
            var slider = obj.AddComponent<UnityEngine.UI.Slider>();
            var view = obj.AddComponent<BattlePartyMemberView>();
            Set(view, "hpBar", slider);
            var hud = f.Root.AddComponent<BattlePartyHud>();
            Set(hud, "memberViews", new System.Collections.Generic.List<BattlePartyMemberView> { view });
            Set(f.Surface, "partyHud", hud);
            hud.Bind(f.Initial.PartyMembers);
            var lease = f.Surface.AcquireProjectionLease(f.Owner);
            var before = lease.GetPresented(f.Player.RuntimeId);
            lease.Apply(new BattleProjectionCursor(f.Owner, 0, before,
                new BattleProjectedParticipant(before.RuntimeId, before.Hp / 2f, before.MaxHp)));
            int reentries = 0;
            slider.onValueChanged.AddListener(_ =>
            {
                reentries++;
                f.Pool.Release(f.Enemy.RuntimeId);
            });
            lease.Dispose();
            Check(reentries == 1, "Canonical listener was not exercised.");
            Check(f.Pool.ReleaseCount == 0 && f.Pool.TryGet(f.Enemy.RuntimeId, out var target) &&
                target.GetInstanceID() == f.TargetInstance,
                "Closing temporarily removed guards and admitted a reentrant release.");
        }
    }

    public static void RootOwnsProjectionBeforeAnnouncement()
    {
        using (var f = new Fixture(false))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            var labelObject = new GameObject("ScopeAnnouncement", typeof(RectTransform));
            labelObject.transform.SetParent(f.Root.transform, false);
            var label = labelObject.AddComponent<TMPro.TextMeshProUGUI>();
            var group = labelObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock,
                new BattleActionAnnouncementPresenter(label, group, clock),
                new BattleAnimationPlaybackRouter(clock, (_, __) => null),
                _ => null, projectionArbiter: f.Surface.ProjectionArbiter);
            var slot = new DRPGBattleTransactionPresentationPortSlot(f.Session.BattleGeneration);
            slot.TryBind(f.Session.BattleGeneration, adapter);
            Check(f.Session.TransactionArbiter.TryAcquire(f.Owner, out var ruleLease, out string error), error);
            var turn = f.Session.CurrentActorTurnToken;
            var frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                new ParticipantId(f.Enemy.RuntimeId), f.Player.Build.BasicAttack).WithIdentity(f.Owner.Action);
            var module = new DRPGStandardBasicAttackTransactionModule(slot);
            try
            {
                var start = module.TryStart(frozen, f.Owner.Action, ruleLease, "projection-locked");
                Check(f.Surface.ProjectionArbiter.IsLeased && group.alpha == 1f &&
                    !start.Receipt.Completion.IsCompleted,
                    "Root announcement began without its full-battlefield projection lease.");
                Check(f.Surface.ProjectionArbiter.Active.Owner.Equals(f.Owner),
                    "Root and projection scope owners differ.");
            }
            finally { module.Dispose(); ruleLease.Dispose(); }
            Check(!f.Surface.ProjectionArbiter.IsLeased && group.alpha == 0f,
                "Root disposal leaked its projection or announcement.");
        }
    }

    public static void FullBattlefieldRejectsUnindexedPooledActor()
    {
        using (var f = new Fixture(false, true))
        {
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
                Check(lease.Count == 6, "Lease did not pin the full three-versus-three field.");
            var extra = f.Pool.Acquire(7199);
            Check(!f.Surface.ProjectionArbiter.TryAcquire(f.Owner).Accepted,
                "An unindexed active pooled actor escaped the full-field lock check.");
            f.Surface.ProjectionArbiter.Abandon();
            f.Pool.Release(extra.RuntimeId);
        }
    }

    public static void RootFaultsWhenProjectionIsAbandoned()
    {
        foreach (bool destroyActor in new[] { false, true })
        using (var f = new Fixture(false))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            var labelObject = new GameObject("AbandonedScopeAnnouncement", typeof(RectTransform));
            labelObject.transform.SetParent(f.Root.transform, false);
            var label = labelObject.AddComponent<TMPro.TextMeshProUGUI>();
            var group = labelObject.AddComponent<CanvasGroup>();
            var adapter = new DRPGBattleTransactionPresentationAdapter(clock,
                new BattleActionAnnouncementPresenter(label, group, clock),
                new BattleAnimationPlaybackRouter(clock, (_, __) => null),
                _ => null, projectionArbiter: f.Surface.ProjectionArbiter);
            var slot = new DRPGBattleTransactionPresentationPortSlot(f.Session.BattleGeneration);
            slot.TryBind(f.Session.BattleGeneration, adapter);
            Check(f.Session.TransactionArbiter.TryAcquire(f.Owner, out var ruleLease, out string error), error);
            var turn = f.Session.CurrentActorTurnToken;
            var frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                new ParticipantId(f.Enemy.RuntimeId), f.Player.Build.BasicAttack).WithIdentity(f.Owner.Action);
            using (var module = new DRPGStandardBasicAttackTransactionModule(slot))
            {
                var start = module.TryStart(frozen, f.Owner.Action, ruleLease, "abandon-locked");
                if (destroyActor) UnityEngine.Object.DestroyImmediate(f.Target.gameObject);
                else f.Surface.Unbind();
                module.Tick();
                Check(start.Receipt.Completion.IsCompleted && !ruleLease.IsActive && group.alpha == 0f,
                    "Root continued after its surface abandoned the projection lease.");
            }
        }
    }

    public static void EnemyCursorUpdatesWorldStatusWithoutRebinding()
    {
        using (var f = new Fixture(false))
        {
            var obj = new GameObject("WorldEnemyUi", typeof(RectTransform));
            obj.transform.SetParent(f.Root.transform, false);
            var cameraObject = new GameObject("ProbeCamera", typeof(Camera));
            cameraObject.transform.SetParent(f.Root.transform, false);
            var targeting = obj.AddComponent<BattleWorldTargetingPresenter>();
            if (f.Target.GetComponentInChildren<Renderer>() == null)
                f.Target.gameObject.AddComponent<SpriteRenderer>();
            targeting.Configure(cameraObject.GetComponent<Camera>(), f.Pool, obj.transform as RectTransform);
            f.Surface.ConfigureWorldPresentation(null, f.Roster, targeting);
            targeting.Bind(f.Initial.Enemies);
            var status = obj.GetComponentInChildren<BattleWorldActorStatusView>();
            Check(status != null, "Fixture did not create a real world status plate.");
            var fill = status.transform.Find("HpTrack/Fill") as RectTransform;
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var before = lease.GetPresented(f.Enemy.RuntimeId);
                Check(lease.Apply(new BattleProjectionCursor(f.Owner, 0, before,
                    new BattleProjectedParticipant(before.RuntimeId, before.Hp / 2f, before.MaxHp))).Accepted,
                    "Enemy HP cursor was rejected.");
                Check(Mathf.Abs(fill.anchorMax.x - .5f) < .0001f,
                    "Enemy cursor did not update the real world HP fill.");
                status.Bind(null);
                targeting.Bind(Array.Empty<DRPGBattleParticipantViewModel>());
                targeting.Clear();
                Check(status != null && status.gameObject.activeInHierarchy && fill.anchorMax.x == .5f,
                    "A direct canonical leaf erased the leased enemy status plate.");
            }
        }
    }

    public static void CheckpointFreezesRectLayout()
    {
        using (var f = new Fixture(false))
        {
            var obj = new GameObject("CheckpointRect", typeof(RectTransform));
            obj.transform.SetParent(f.Root.transform, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(.1f, .2f); rect.anchorMax = new Vector2(.8f, .9f);
            rect.pivot = new Vector2(.3f, .7f); rect.sizeDelta = new Vector2(123f, 57f);
            rect.anchoredPosition3D = new Vector3(17f, -31f, 2f);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var saved = lease.LockBaseline;
                rect.anchorMin = Vector2.zero; rect.pivot = Vector2.one;
                rect.sizeDelta = Vector2.zero; rect.anchoredPosition3D = Vector3.zero;
                foreach (var pose in saved.UiPoses)
                    if (pose.InstanceId == rect.GetInstanceID())
                    {
                        Check(pose.IsRect && pose.AnchorMin == new Vector2(.1f, .2f) &&
                            pose.AnchorMax == new Vector2(.8f, .9f) && pose.Pivot == new Vector2(.3f, .7f) &&
                            pose.SizeDelta == new Vector2(123f, 57f) && pose.AnchoredPosition == new Vector3(17f, -31f, 2f),
                            "Checkpoint lost frozen RectTransform layout values.");
                        return;
                    }
                Check(false, "Checkpoint omitted the UI rect.");
            }
        }
    }

    public static void CheckpointFreezesCanvasVisibility()
    {
        using (var f = new Fixture(false))
        {
            var group = f.Root.AddComponent<CanvasGroup>();
            group.alpha = .37f; group.interactable = false;
            group.blocksRaycasts = false; group.ignoreParentGroups = true;
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var saved = lease.LockBaseline;
                group.alpha = 1f; group.interactable = true; group.ignoreParentGroups = false;
                Check(saved.CanvasGroups.Count == 1 && saved.CanvasGroups[0].Alpha == .37f &&
                    !saved.CanvasGroups[0].Interactable && !saved.CanvasGroups[0].BlocksRaycasts &&
                    saved.CanvasGroups[0].IgnoreParentGroups,
                    "Checkpoint omitted frozen CanvasGroup visibility and input values.");
            }
        }
    }

    public static void CheckpointIncludesDetachedWorldAndCamera()
    {
        using (var f = new Fixture(false))
        {
            var world = new GameObject("CheckpointDetachedWorld");
            try
            {
                var camera = world.AddComponent<Camera>();
                camera.fieldOfView = 43f; camera.orthographicSize = 8f;
                camera.transform.position = new Vector3(7f, 8f, -12f);
                var renderer = world.AddComponent<MeshRenderer>();
                renderer.sortingOrder = 37; renderer.enabled = false;
                var host = f.Root.AddComponent<BattleWorldRuntimeHost>();
                Set(host, "worldInstance", world);
                f.Surface.ConfigureWorldPresentation(host, f.Roster, null);
                using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
                {
                    var saved = lease.LockBaseline;
                    camera.fieldOfView = 80f; camera.transform.position = Vector3.zero;
                    renderer.sortingOrder = 0; renderer.enabled = true;
                    Check(saved.CameraInstanceId == camera.GetInstanceID() && saved.Camera.FieldOfView == 43f &&
                        saved.Camera.Position == new Vector3(7f, 8f, -12f) && saved.WorldRenderers.Count == 1 &&
                        saved.WorldRenderers[0].SortingOrder == 37 && !saved.WorldRenderers[0].Enabled,
                        "Checkpoint omitted detached-world mesh or camera restore values.");
                }
                Set(host, "worldInstance", null);
            }
            finally { UnityEngine.Object.DestroyImmediate(world); }
        }
    }

    public static void RejectedWritesCarryStructuredOwnerAndTarget()
    {
        using (var f = new Fixture(false))
        using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
        {
            var wrong = BattleTransactionIdentity.ForRootAction(new ActionIdentity(
                f.Owner.BattleGeneration + 1, 1, 71));
            f.Surface.ProjectionArbiter.TryAcquire(wrong);
            f.Pool.Release(f.Enemy.RuntimeId);
            var before = lease.GetPresented(f.Enemy.RuntimeId);
            lease.Apply(new BattleProjectionCursor(wrong, 0, before, before));
            var rows = f.Surface.ProjectionArbiter.Rejections;
            Check(rows.Count == 3 && rows[0].Kind == ProjectionRejectionKind.Acquire &&
                rows[1].Kind == ProjectionRejectionKind.Canonical && rows[2].Kind == ProjectionRejectionKind.Cursor &&
                rows[1].RuntimeId == f.Enemy.RuntimeId && rows[2].RuntimeId == f.Enemy.RuntimeId &&
                rows[1].Owner == f.Owner && rows[2].Owner == f.Owner && rows[2].RequestedOwner == wrong &&
                !string.IsNullOrEmpty(rows[1].Operation) && !string.IsNullOrEmpty(rows[2].Reason),
                "Rejected projection writes lack structured owner/target diagnostics.");
        }
    }

    public static void IdenticalCanonicalRosterDoesNotRestartFormationMotion()
    {
        using (var f = new Fixture(false))
        {
            Check(!DOTween.IsTweening(f.Target.transform),
                "The initial immediate roster bind left a formation tween running.");

            Vector3 position = f.Target.transform.position;
            Vector3 scale = f.Target.transform.localScale;
            f.Roster.Bind(f.Initial.PartyMembers, f.Initial.Enemies, false);

            Check(!DOTween.IsTweening(f.Target.transform) &&
                f.Target.transform.position == position &&
                f.Target.transform.localScale == scale,
                "An unchanged canonical roster restarted no-op position/scale tweens; this makes every turn look like a full UI/actor refresh.");
        }
    }

    public static void IdenticalActorVisualDoesNotRebindOrResetMotionRoot()
    {
        using (var f = new Fixture(false))
        {
            var motionRoot = new GameObject("ActorMotionRoot").transform;
            motionRoot.SetParent(f.Target.transform, false);
            Set(f.Target, "actorMotionRoot", motionRoot);
            var driver = f.Target.gameObject.AddComponent<ProjectionBindCountingDriver>();
            f.Target.ConfigureVisualDriver(driver);
            f.Target.Bind(f.Enemy.RuntimeId, f.Initial.Enemies[0]);
            int initialBinds = driver.BindCount;
            motionRoot.localPosition = new Vector3(.25f, 0f, 0f);

            DRPGBattlePresentationSnapshot repeated =
                DRPGBattlePresentationSnapshot.Capture(f.Session);
            f.Target.Bind(f.Enemy.RuntimeId, repeated.Enemies[0]);

            Check(driver.BindCount == initialBinds &&
                motionRoot.localPosition == new Vector3(.25f, 0f, 0f),
                "An identical runtime actor rebound its unchanged visual or reset ActorMotionRoot during a canonical refresh.");
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly BattleWorldActorPool Pool;
        internal readonly BattleWorldRosterPresenter Roster;
        internal readonly DRPGBattleUGUISurface Surface;
        internal readonly DRPGBattlePresentationEventBridge Bridge;
        internal readonly DRPGBattleSession Session;
        internal readonly BattleParticipantRuntime Player;
        internal readonly BattleParticipantRuntime Enemy;
        internal readonly BattleWorldActorView Target;
        internal readonly int TargetInstance;
        internal readonly DRPGBattlePresentationSnapshot Initial;
        private readonly DRPGCombatantProfileSO playerProfile;
        private readonly DRPGCombatantProfileSO enemyProfile;
        private readonly DRPGSkillSO attack;
        private IDisposable lease;
        internal BattleTransactionIdentity Owner => BattleTransactionIdentity.ForRootAction(
            new ActionIdentity(Session.BattleGeneration, Session.CurrentActorTurnToken.TurnSerial, 71));

        internal Fixture(bool lethal, bool fullBattlefield = false, bool enemyFirst = false,
            Action<DRPGBattleSession, BattleParticipantRuntime, BattleParticipantRuntime> beforeStart = null)
        {
            Root = new GameObject("Task7ProjectionProbe");
            Pool = Root.AddComponent<BattleWorldActorPool>();
            Roster = Root.AddComponent<BattleWorldRosterPresenter>();
            Roster.Configure(Pool, null, null);
            playerProfile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
            enemyProfile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
            attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
            Set(playerProfile, "agility", enemyFirst ? 20f : 30f);
            Set(enemyProfile, "agility", enemyFirst ? 30f : 20f);
            Set(attack, "skillId", "projection_basic_attack");
            Set(attack, "power", lethal ? 10000f : 1f);
            Set(playerProfile, "skills", new System.Collections.Generic.List<DRPGSkillSO> { attack });
            Player = new BattleParticipantRuntime(7101, new BattleParticipantSpec(
                playerProfile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            Enemy = new BattleParticipantRuntime(7102, new BattleParticipantSpec(
                enemyProfile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            var participants = new System.Collections.Generic.List<BattleParticipantRuntime> { Player, Enemy };
            if (fullBattlefield)
            {
                for (int index = 1; index <= 2; index++)
                {
                    participants.Add(new BattleParticipantRuntime(7110 + index, new BattleParticipantSpec(
                        playerProfile, DRPGBattleSide.Player, DRPGBattleRosterRole.PartyMember, index)));
                    participants.Add(new BattleParticipantRuntime(7120 + index, new BattleParticipantSpec(
                        enemyProfile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, index)));
                }
            }
            Session = new DRPGBattleSession(participants);
            Surface = Root.AddComponent<DRPGBattleUGUISurface>();
            Surface.ConfigureWorldPresentation(null, Roster, null);
            Surface.Bind(Session);
            beforeStart?.Invoke(Session, Player, Enemy);
            Session.Start();
            Check(ReferenceEquals(Session.CurrentActor, enemyFirst ? Enemy : Player), "Wrong fixture actor.");
            Initial = DRPGBattlePresentationSnapshot.Capture(Session);
            Surface.RenderInitial(Initial);
            Bridge = Root.AddComponent<DRPGBattlePresentationEventBridge>();
            Bridge.Configure(Root.AddComponent<DRPGBattlePresentationDirector>(), Surface,
                NullBattleAudioCueSink.Instance);
            Bridge.Bind(Session);
            Check(Pool.TryGet(Enemy.RuntimeId, out Target), "No target view.");
            TargetInstance = Target.GetInstanceID();
        }

        internal void AcquireLeaseIfAvailable()
        {
            // Before Task7 exists the same real chain runs unprotected, giving a
            // behavioral RED (view reset/release), never a missing-type failure.
            MethodInfo method = typeof(DRPGBattleUGUISurface).GetMethod(
                "AcquireProjectionLease", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                lease = (IDisposable)method.Invoke(Surface, new object[] {
                    BattleTransactionIdentity.ForRootAction(
                        new ActionIdentity(Session.BattleGeneration, Session.CurrentActorTurnToken.TurnSerial, 71)) });
        }

        internal void InjectImpact()
        {
            var command = DRPGBattleCommand.BasicAttack(Player.RuntimeId, Enemy.RuntimeId, attack);
            Check(Session.TryPrepareCommand(command, out var prepared, out string error), error);
            Check(Session.TryResolveImpact(prepared, out _, out error), error);
        }

        internal void AssertPinned()
        {
            Check(Pool.TryGet(Enemy.RuntimeId, out var view) && view != null &&
                view.GetInstanceID() == TargetInstance && view.gameObject.activeInHierarchy,
                "ParticipantChanged released/replaced the target before return.");
            Check(ReferenceEquals(Surface.LastSnapshot, Initial),
                "ParticipantChanged projected final canonical HP before its cursor.");
        }

        public void Dispose()
        {
            lease?.Dispose();
            Bridge.Unbind();
            Surface.Unbind();
            foreach (Transform item in Root.GetComponentsInChildren<Transform>(true))
                item.DOKill(false);
            UnityEngine.Object.DestroyImmediate(Root);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class ProjectionBindCountingDriver : MonoBehaviour,
    IBattleWorldActorVisualDriver
{
    internal int BindCount { get; private set; }
    public void Bind(DRPGBattleParticipantViewModel actor) => BindCount++;
    public IBattlePresentationHandle PlayIntent(EAnimationLogicState intent)
    {
        var handle = new BattlePresentationHandle();
        handle.TrySettle(BattlePresentationOutcome.Skipped);
        return handle;
    }
    public void RestoreVisual() { }
    public void ResetVisual() { }
}
#endif
