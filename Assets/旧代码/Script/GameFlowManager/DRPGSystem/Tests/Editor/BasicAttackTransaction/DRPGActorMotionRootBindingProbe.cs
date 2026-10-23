#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGActorMotionRootBindingProbe
{
    internal static void WiringPreservesSixAnchorIdentitiesAndOffsets()
    {
        var root = new GameObject("ActorBindingProbe");
        try
        {
            var view = root.AddComponent<BattleWorldActorView>(); view.EnsureAnchors();
            var anchors = Anchors(view);
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i].localPosition = new Vector3(i, i * .2f, -.1f);
                anchors[i].localRotation = Quaternion.Euler(0, 0, i * 3);
                anchors[i].localScale = Vector3.one * (1 + i * .1f);
            }
            DRPGBasicAttackActorWiring.Wire(view);
            var motion = root.transform.Find("ActorMotionRoot");
            Require(motion != null, "Wiring did not create the independent ActorMotionRoot.");
            Require(motion.localPosition == Vector3.zero && motion.localScale == Vector3.one && motion.localRotation == Quaternion.identity,
                "Motion root must not alter authored pose.");
            for (int i = 0; i < anchors.Length; i++)
                Require(Anchors(view)[i] == anchors[i] && anchors[i].parent == motion && anchors[i].GetSiblingIndex() == i &&
                    anchors[i].localPosition == new Vector3(i, i * .2f, -.1f) && anchors[i].localScale == Vector3.one * (1 + i * .1f) &&
                    Quaternion.Angle(anchors[i].localRotation, Quaternion.Euler(0, 0, i * 3)) < .001f,
                    "Wiring replaced/reordered an anchor or changed its local pose: " + anchors[i].name);
            DRPGBasicAttackActorWiring.Wire(view);
            Require(root.transform.Find("ActorMotionRoot") == motion && motion.childCount == 6,
                "Repeated wiring duplicated the hierarchy.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    internal static void BoundMotionMovesAnchorsNotCanonicalSlotAndReturnsSmoothly()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            f.Pool.TryGet(f.Player.RuntimeId, out var actor);
            DRPGBasicAttackActorWiring.Wire(actor); DRPGBasicAttackActorWiring.Wire(f.Target);
            var actorHome = actor.transform.position; var targetHome = f.Target.transform.position;
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
                using (var motion = new BattleActorMotionController(lease, clock))
                {
                    motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId),
                        new ParticipantId(f.Enemy.RuntimeId), new Vector3(-1, 2, 3), new Vector3(1, 2, 3), 0));
                    Require(actor.transform.position == actorHome && f.Target.transform.position == targetHome,
                        "Tactical motion moved the canonical slot instead of its bound motion root.");
                    var targetRoot = f.Target.transform.Find("ActorMotionRoot");
                    Require(targetRoot.position == new Vector3(1, 2, 3), "Target did not enter conflict.");
                    var a = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId), 0));
                    var b = motion.ReturnHome(new ReturnMotionCommand(f.Owner.Action, new ParticipantId(f.Enemy.RuntimeId), 0));
                    clock.Now = .19; clock.RenderedFrame = 1; motion.Tick();
                    Require(targetRoot.position != targetHome && targetRoot.position != new Vector3(1, 2, 3),
                        "Return snapped instead of rendering an intermediate pose.");
                    foreach (var anchor in Anchors(f.Target))
                        Require(anchor.parent == targetRoot && Vector3.Distance(anchor.position, targetRoot.TransformPoint(anchor.localPosition)) < .0001f,
                            "An anchor detached during return.");
                    clock.Now = .38; clock.RenderedFrame = 2; motion.Tick();
                    Require(a.Completion.IsCompleted && b.Completion.IsCompleted && targetRoot.position == targetHome &&
                        actor.transform.Find("ActorMotionRoot").position == actorHome,
                        "Both motion roots must finish at the frozen homes.");
                }
            }
        }
    }
    private static Transform[] Anchors(BattleWorldActorView v) => new[] {
        v.VisualRoot, v.ShadowAnchor, v.VfxAnchor, v.TargetAnchor, v.StatusAnchor, v.PresentationAnchor };
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void ReparentedMotionRootCannotKeepWritingUnderOldLease()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            DRPGBasicAttackActorWiring.Wire(f.Target);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var root = f.Target.transform.Find("ActorMotionRoot");
                root.SetParent(f.Root.transform, true);
                Require(!lease.IsActive, "A reparented motion binding retained the old projection capability.");
            }
        }
    }

    internal static void FormationRebasesCanonicalHomeWithoutDoublingMotionScale()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            DRPGBasicAttackActorWiring.Wire(f.Target);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
                using (var motion = new BattleActorMotionController(lease, clock))
                {
                    var target = new DRPGWorldFormationPlacement(f.Enemy.RuntimeId, DRPGFormationRow.Front,
                        new Vector3(4, 1, 2), .8f, 1, 10);
                    var receipt = motion.MoveFormation(f.Owner.Action, target, clock.Now);
                    clock.Now = .30; clock.RenderedFrame++; motion.Tick();
                    motion.SealFormationHomes(new[] { target });
                    Require(receipt.Completion.IsCompleted && f.Target.transform.position == target.WorldPosition &&
                        f.Target.MotionRoot.position == target.WorldPosition && f.Target.MotionRoot.localScale == Vector3.one &&
                        f.Target.transform.localScale == Vector3.one * .8f && lease.IsAtCurrentHome(f.Enemy.RuntimeId),
                        "Formation must rebase the canonical slot without doubling scale or retaining tactical offset.");
                }
            }
        }
    }

    internal static void CancelAndRebindClearMotionOffsetWithoutDetachingAnchors()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            DRPGBasicAttackActorWiring.Wire(f.Target);
            var root = f.Target.transform.Find("ActorMotionRoot");
            var lease = f.Surface.AcquireProjectionLease(f.Owner);
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            using (var motion = new BattleActorMotionController(lease, clock))
            {
                motion.SnapConflict(new ConflictMotionCommand(f.Owner.Action, new ParticipantId(f.Player.RuntimeId),
                    new ParticipantId(f.Enemy.RuntimeId), new Vector3(-1, 2, 3), new Vector3(1, 2, 3), 0));
                lease.Dispose();
                Require(root.localPosition == Vector3.zero, "Cancellation left the previous conflict offset on canonical refresh.");
                clock.Now = 10; clock.RenderedFrame++; motion.Tick();
                Require(root.localPosition == Vector3.zero, "Late motion wrote after cancellation.");
            }
            f.Target.ResetForPool(); f.Target.Bind(f.Enemy.RuntimeId, f.Initial.Enemies[0]);
            Require(root.localPosition == Vector3.zero && root.localScale == Vector3.one && root.localRotation == Quaternion.identity,
                "Pool reuse inherited motion state.");
            foreach (var anchor in Anchors(f.Target)) Require(anchor.parent == root, "Rebind detached an authored anchor.");
        }
    }
}
#endif
