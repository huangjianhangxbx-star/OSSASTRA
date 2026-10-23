using System;
using TMPro;
using UnityEngine;

// Authored view references only. Session/Flow own routing and ticking; this
// component neither creates domain modules nor builds replacement UI at runtime.
public sealed class BattleStandardPresentationBinding : MonoBehaviour
{
    [SerializeField] private DRPGBasicAttackTimingProfile timingProfile;
    [SerializeField] private TMP_Text actionName;
    [SerializeField] private CanvasGroup actionNameGroup;
    [SerializeField] private CanvasGroup[] conflictHiddenGroups = Array.Empty<CanvasGroup>();
    [SerializeField, Range(.05f, .4f)] private float conflictHalfWidth = .18f;
    [SerializeField, Range(.1f, .5f)] private float conflictFootHeight = .25f;
    [SerializeField, Range(.5f, 1f)] private float conflictFovRatio = .9f;

    internal DRPGBattleTransactionPresentationAdapter CreateAdapter(DRPGBattleUGUISurface surface,
        IDRPGBattlePresentationClock clock, Func<BattleStartEntryCommand, IBattlePresentationHandle> entry,
        BattleDefeatedActorRoster defeated = null)
    {
        if (surface == null || surface.WorldRosterPresenter == null || surface.ProjectionCamera == null ||
            surface.ImpactFeedbackPresenter == null || actionName == null || actionName.font == null || actionNameGroup == null)
            throw new InvalidOperationException("Standard transaction requires the authored announcement, world roster, camera and impact feedback bindings.");
        var roster = surface.WorldRosterPresenter;
        var camera = surface.ProjectionCamera;
        BattleWorldActorView Find(ParticipantId id)
        {
            foreach (var view in roster.ActiveViews)
                if (view != null && view.RuntimeId == id.Value && view.gameObject.activeInHierarchy) return view;
            return null;
        }
        var playback = new BattleAnimationPlaybackRouter(clock, (identity, command) =>
        {
            var view = Find(command.Snapshot.Turn.Actor);
            var sprite = view != null ? view.GetComponentInChildren<BattleWorldActorSpriteDriver>(true) : null;
            if (sprite == null) throw new InvalidOperationException("Active Sprite actor has no bound playback driver.");
            return sprite.PlayBoundIntent(identity, command.Snapshot.AnimationIntent);
        });
        // Authored prefab text is only an editor preview. A live battle must not
        // expose that stale label while no action owns the announcement window.
        actionName.text = string.Empty;
        actionNameGroup.alpha = 0f;
        actionNameGroup.interactable = false;
        actionNameGroup.blocksRaycasts = false;
        var announcement = new BattleActionAnnouncementPresenter(actionName, actionNameGroup, clock, true);
        return new DRPGBattleTransactionPresentationAdapter(clock,
            announcement, playback,
            command =>
            {
                foreach (var group in conflictHiddenGroups)
                    if (group != null) { group.alpha = 0; group.interactable = false; group.blocksRaycasts = false; }
                var handle = new BattlePresentationHandle(); handle.TrySettle(BattlePresentationOutcome.Completed); return handle;
            },
            request =>
            {
                if (request.BattleGeneration != surface.BattleGeneration) return default;
                var actor = Find(request.Actor); var target = Find(request.Target);
                if (actor == null || target == null || actor == target) return default;
                var middle = (actor.transform.position + target.transform.position) * .5f;
                float depth = Vector3.Dot(middle - camera.transform.position, camera.transform.forward);
                if (depth <= camera.nearClipPlane) return default;
                float halfHeight = camera.orthographic ? camera.orthographicSize : Mathf.Tan(camera.fieldOfView * conflictFovRatio * .5f * Mathf.Deg2Rad) * depth;
                var center = camera.transform.position + camera.transform.forward * depth + camera.transform.up * ((conflictFootHeight * 2 - 1) * halfHeight);
                var offset = camera.transform.right * (conflictHalfWidth * 2 * halfHeight * camera.aspect);
                int sign = actor.BoundActor.IsEnemy ? -1 : 1;
                var actorConflict = center - offset * sign;
                var targetConflict = center + offset * sign;
                if (roster.PreservesNodeHomes)
                {
                    actorConflict.y = actor.transform.position.y;
                    actorConflict.z = actor.transform.position.z;
                    targetConflict.y = target.transform.position.y;
                    targetConflict.z = target.transform.position.z;
                }
                return new DRPGBasicAttackPresentationFreezeInput(Pos(actor.transform.position), Pos(actorConflict),
                    Pos(target.transform.position), Pos(targetConflict), sign, -sign, Baseline(actor), Baseline(target),
                    timingProfile != null ? timingProfile.Freeze() : DRPGBasicAttackTimingSnapshot.CompactDefaults,
                    new DRPGBasicAttackSemanticSnapshot(
                        string.IsNullOrWhiteSpace(request.ActionName) ? "技能" : request.ActionName,
                        EAnimationLogicState.Skill_1,
                        request.PresentationKey,
                        "Impact",
                        "Impact"));
            }, surface.ProjectionArbiter, surface.ImpactFeedbackPresenter, camera, defeated, entry,
            new BattleWavePresentationFactory(surface, announcement, clock), surface.AudioCueSink,
            (snapshot, lease) => new BattleCloseupScope(snapshot, lease, camera,
                surface.CaptureCloseupFloatingGroups(), BattleCloseupProfile.ApprovedDefaults));
    }

    internal static void DimSpectators(Transform worldRoot, int attackerId, int targetId)
    {
        if (worldRoot == null) return;
        foreach (var renderer in worldRoot.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!renderer.gameObject.activeInHierarchy) continue;
            var actor = renderer.GetComponentInParent<BattleWorldActorView>();
            if (actor != null && (actor.RuntimeId == attackerId || actor.RuntimeId == targetId)) continue;
            var color = renderer.color;
            renderer.color = new Color(color.r * .3f, color.g * .3f, color.b * .3f, color.a);
        }
    }

    private static ActionTracePosition Pos(Vector3 p) => new ActionTracePosition(p.x, p.y, p.z);
    private static DRPGBasicAttackVisualBaseline Baseline(BattleWorldActorView view)
    {
        var renderer = view.VisualRoot.GetComponentInChildren<SpriteRenderer>(true);
        var color = renderer != null ? renderer.color : Color.white;
        return new DRPGBasicAttackVisualBaseline(Pos(view.VisualRoot.localScale), color.r, color.g, color.b, color.a, "Idle");
    }
}
