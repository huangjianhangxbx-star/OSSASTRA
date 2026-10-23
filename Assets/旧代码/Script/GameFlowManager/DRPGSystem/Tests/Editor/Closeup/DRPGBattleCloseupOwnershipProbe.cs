#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

internal static class DRPGBattleCloseupOwnershipProbe
{
    internal static void Run()
    {
        Type participantsType = Find("BattleCloseupParticipants");
        Check(participantsType != null, "BattleCloseupParticipants production type is missing.");
        object participants = Activator.CreateInstance(participantsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { new ActionIdentity(1, 2, 3), new ParticipantId(11), new ParticipantId(22) }, null);
        MethodInfo contains = Require(participantsType, "Contains");
        MethodInfo includeReaction = Require(participantsType, "IncludeReaction");
        PropertyInfo count = participantsType.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(count != null && (int)count.GetValue(participants) == 2, "Actor and primary target must seed the set exactly once.");
        Check((bool)contains.Invoke(participants, new object[] { new ParticipantId(11) }), "Actor must be a participant.");
        Check(!(bool)contains.Invoke(participants, new object[] { new ParticipantId(33) }), "Reaction actor joined before its beat.");
        Check((bool)includeReaction.Invoke(participants, new object[] { new ActionIdentity(1, 2, 3), new ParticipantId(33) }),
            "Owned reaction participant was not accepted.");
        Check((int)count.GetValue(participants) == 3, "Reaction participant must join once.");
        Check(!(bool)includeReaction.Invoke(participants, new object[] { new ActionIdentity(1, 2, 3), new ParticipantId(33) }),
            "Duplicate reaction participant must be idempotent.");
        Check(!(bool)includeReaction.Invoke(participants, new object[] { new ActionIdentity(1, 2, 4), new ParticipantId(44) }),
            "Foreign ActionId must not mutate the set.");

        Type profileType = Find("BattleCloseupProfile");
        Check(profileType != null, "BattleCloseupProfile production type is missing.");
        object profile = profileType.GetProperty("ApprovedDefaults", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        Check(profile != null, "Approved close-up defaults are missing.");
        CheckNear(ReadFloat(profile, "CameraMagnification"), 1.15f, "camera magnification");
        CheckNear(ReadFloat(profile, "ParticipantScale"), 1.08f, "participant scale");
        CheckNear(ReadFloat(profile, "DownwardBattleFraction"), .03f, "downward fraction");

        Type gateType = Find("BattleCloseupFloatingUiGate");
        Check(gateType != null, "BattleCloseupFloatingUiGate production type is missing.");
        var firstObject = new GameObject("StatusA");
        var secondObject = new GameObject("StatusB");
        try
        {
            var first = firstObject.AddComponent<CanvasGroup>();
            var second = secondObject.AddComponent<CanvasGroup>();
            first.alpha = .65f; first.interactable = true; first.blocksRaycasts = false;
            second.alpha = .35f; second.interactable = false; second.blocksRaycasts = true;
            IList groups = Array.CreateInstance(typeof(CanvasGroup), 2);
            groups[0] = first; groups[1] = second;
            object gate = Activator.CreateInstance(gateType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new object[] { new ActionIdentity(1, 2, 3), groups }, null);
            Require(gateType, "Hide").Invoke(gate, new object[] { new ActionIdentity(1, 2, 3) });
            Check(first.alpha == 0f && second.alpha == 0f && !first.interactable && !second.blocksRaycasts,
                "Close-up floating UI was not fully gated.");
            Require(gateType, "RestoreAtReturnStart").Invoke(gate, new object[] { new ActionIdentity(1, 2, 4) });
            Check(first.alpha == 0f, "Foreign ActionId restored floating UI.");
            Require(gateType, "RestoreAtReturnStart").Invoke(gate, new object[] { new ActionIdentity(1, 2, 3) });
            CheckNear(first.alpha, .65f, "first restored alpha");
            CheckNear(second.alpha, .35f, "second restored alpha");
            Check(first.interactable && !first.blocksRaycasts && !second.interactable && second.blocksRaycasts,
                "Floating UI exact baseline was not restored.");
            Require(gateType, "Dispose").Invoke(gate, Array.Empty<object>());
            CheckNear(first.alpha, .65f, "idempotent dispose alpha");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }

        RunProductionLifecycle();
    }

    private static void RunProductionLifecycle()
    {
        using (var fixture = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, false, false,
            fullBattlefield: true, configureRules: DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins))
        using (var art = new DRPGBasicAttackVisualRestoreProbe.ActorArt(fixture))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(fixture.Domain.Root, fixture.Clock);
            var floating = new GameObject("CloseupFloating", typeof(CanvasGroup));
            floating.transform.SetParent(fixture.Domain.Root.transform, false);
            CanvasGroup group = floating.GetComponent<CanvasGroup>();
            group.alpha = .73f; group.interactable = true; group.blocksRaycasts = true;
            var adapter = new DRPGBattleTransactionPresentationAdapter(fixture.Clock, visuals.Announcement,
                new BattleAnimationPlaybackRouter(fixture.Clock, (_, __) => null),
                _ => { var handle = new BattlePresentationHandle(); handle.TrySettle(BattlePresentationOutcome.Completed); return handle; },
                projectionArbiter: fixture.Domain.Surface.ProjectionArbiter, impactFeedback: visuals.Feedback,
                battleCamera: visuals.Camera, closeupFactory: (snapshot, lease) => new BattleCloseupScope(snapshot,
                    lease, visuals.Camera, new[] { group }, BattleCloseupProfile.ApprovedDefaults));
            using (var lifetime = adapter.OpenProjection(fixture.Frozen))
            {
                var lease = (DRPGBattleActionProjectionLease)lifetime;
                lease.TryGetView(fixture.Frozen.Turn.Actor.Value, out BattleWorldActorView actor);
                Vector3 homeScale = actor.VisualRoot.localScale;
                Vector3 homePosition = actor.VisualRoot.localPosition;
                float homeSize = visuals.Camera.orthographicSize;
                DRPGBasicAttackVisualRestoreProbe.StartImpact(fixture, adapter, art);
                CheckNear(visuals.Camera.orthographicSize, homeSize / 1.15f, "live close-up camera size");
                CheckNear(actor.VisualRoot.localScale.x, homeScale.x * 1.08f, "live participant scale");
                Check(actor.VisualRoot.localPosition != homePosition, "Live participant did not step down from its frozen baseline.");
                Check(group.alpha == 0f && GameObject.Find("BattleCloseupComposite(Runtime)") != null,
                    "Live close-up did not hide floating UI or create the composite layer.");
                PresentationReceipt returning = adapter.Return(new ReturnPresentationCommand(fixture.Frozen.Identity));
                CheckNear(group.alpha, .73f, "return-start floating UI alpha");
                DRPGBasicAttackVisualRestoreProbe.Advance(fixture, adapter, .20d);
                Check(!returning.Completion.IsCompleted, "Return completed before its readable transition.");
                DRPGBasicAttackVisualRestoreProbe.Advance(fixture, adapter, .25d);
                if (!returning.Completion.IsCompleted)
                    DRPGBasicAttackVisualRestoreProbe.Advance(fixture, adapter, .10d);
                Check(returning.Completion.IsCompleted &&
                    returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                    "Live close-up return did not complete through the existing barrier.");
                CheckNear(visuals.Camera.orthographicSize, homeSize, "returned camera size");
                Check(actor.VisualRoot.localScale == homeScale && actor.VisualRoot.localPosition == homePosition,
                    "Live close-up geometry did not restore its exact frozen baseline.");
            }
        }
    }

    private static Type Find(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(name, false);
            if (found != null) return found;
        }
        return null;
    }
    private static MethodInfo Require(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(method != null, "Missing method: " + type.Name + "." + name + ".");
        return method;
    }
    private static float ReadFloat(object value, string property)
    {
        PropertyInfo item = value.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(item != null, "Missing profile property: " + property + ".");
        return (float)item.GetValue(value);
    }
    private static void CheckNear(float actual, float expected, string label) =>
        Check(Mathf.Abs(actual - expected) < .0001f, label + " mismatch: actual=" + actual + ", expected=" + expected + ".");
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
