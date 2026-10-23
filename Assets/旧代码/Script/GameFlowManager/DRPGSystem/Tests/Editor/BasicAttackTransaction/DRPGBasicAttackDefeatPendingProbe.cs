#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBasicAttackDefeatPendingProbe
{
    public static void LethalHitRetainsXDimAndPoseThroughRenderedHome()
        => RunLethalReturn(false);

    public static void HiddenDefeatMarkerCannotCrossHomeRenderedBarrier()
        => RunLethalReturn(true);

    private static void RunLethalReturn(bool hideMarker)
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false, lethal: true))
        {
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Domain.Root, f.Clock); var adapter = visuals.CreateAdapter(f);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f));
            try
            {
                f.Domain.Pool.TryGet(f.Frozen.Target.Value, out var initialTarget);
                var renderer = initialTarget.VisualRoot.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.color = new Color(.8f, .7f, .6f, 1f);
                using (var scope = adapter.OpenProjection(f.Frozen))
                {
                    var projection = (DRPGBattleActionProjectionLease)scope;
                    projection.TryGetView(f.Frozen.Target.Value, out var target);
                    int viewId = target.GetInstanceID(); var home = target.transform.position;
                    adapter.EnterConflict(new ConflictCompositionCommand(f.Frozen, 0d));
                    target.VisualRoot.localPosition = new Vector3(.2f, .1f, 0f);
                    var defeatPose = target.VisualRoot.localPosition;
                    f.Clock.Now = .13d; f.Clock.RenderedFrame++; adapter.Tick(); f.Commit();
                    Check(f.Ledger.PrimaryImpact.Hit.WasDefeated && f.Ledger.PrimaryImpact.Hit.HpAfter == 0f,
                        "Lethal fixture did not commit a rule defeat.");
                    adapter.PresentImpact(new ImpactPresentationCommand(f.Ledger, f.Clock.Now));
                    Check(visuals.ActiveDefeatMarkers().Length == 1 && visuals.ActiveDefeatMarkers()[0].color.a >= .95f &&
                        renderer.color.r <= .4f && renderer.enabled && renderer.sprite == sprite && target.gameObject.activeInHierarchy &&
                        target.GetInstanceID() == viewId && f.Domain.Pool.ReleaseCount == 0,
                        "HP0 did not retain a visible X/dim/same rendered actor at primary impact: " + enemyFirst);
                    var dimColor = renderer.color;
                    f.Clock.Now = .59d; f.Clock.RenderedFrame++; adapter.Tick();
                    var returning = adapter.Return(new ReturnPresentationCommand(f.Frozen.Identity));
                    f.Clock.Now += .2d; f.Clock.RenderedFrame++; adapter.Tick();
                    Check(renderer.color == dimColor && target.VisualRoot.localPosition == defeatPose && visuals.ActiveDefeatMarkers().Length == 1,
                        "Dead actor flashed normal color or idle pose during return.");
                    if (hideMarker) visuals.ActiveDefeatMarkers()[0].gameObject.SetActive(false);
                    f.Clock.Now += .2d; f.Clock.RenderedFrame++; adapter.Tick();
                    if (hideMarker)
                    {
                        f.Clock.RenderedFrame++; adapter.Tick();
                        Check(returning.Completion.IsCompleted && returning.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted &&
                            f.Domain.Pool.ReleaseCount == 0 && f.Lease.IsActive,
                            "An invisible defeat marker was accepted as a rendered home result.");
                        continue;
                    }
                    Check(target.transform.position == home && !returning.Completion.IsCompleted && target.GetInstanceID() == viewId &&
                        target.gameObject.activeInHierarchy && f.Domain.Pool.ReleaseCount == 0,
                        "Dead actor was released/swapped before a rendered home frame.");
                    f.Clock.RenderedFrame++; adapter.Tick();
                    Check(returning.Completion.IsCompleted && renderer.color == dimColor && renderer.sprite == sprite &&
                        target.VisualRoot.localPosition == defeatPose && visuals.ActiveDefeatMarkers().Length == 1 &&
                        f.Domain.Pool.ReleaseCount == 0 && f.Lease.IsActive && f.Events == 1,
                        "Return itself released defeat ownership or lost X/dim/pose before explicit transfer.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); }
        }
    }
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
