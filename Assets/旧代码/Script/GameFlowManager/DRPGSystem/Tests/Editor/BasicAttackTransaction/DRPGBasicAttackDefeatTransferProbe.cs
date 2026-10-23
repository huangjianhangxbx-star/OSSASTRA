#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBasicAttackDefeatTransferProbe
{
    public static void DefeatTransferWaitsForRenderedHomeAndPreservesRoot()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new Fixture(enemyFirst))
        {
            f.Hit();
            var tooEarly = f.Adapter.CompleteDefeats(f.Departures);
            Check(Faulted(tooEarly.Receipt) && f.Domain.Domain.Pool.ReleaseCount == 0 && f.Defeated.Count == 0,
                "Defeat transfer crossed the impact/return boundary early.");
            var returning = f.Adapter.Return(new ReturnPresentationCommand(f.Domain.Frozen.Identity));
            f.Advance(.4d);
            Check(!returning.Completion.IsCompleted && Faulted(f.Adapter.CompleteDefeats(f.Departures).Receipt) &&
                f.Domain.Domain.Pool.ReleaseCount == 0, "Home tween completion substituted for a rendered home frame.");
            f.Advance(.01d);
            Check(returning.Completion.IsCompleted && !Faulted(returning), "Fixture return did not render home.");
            long revision = f.Domain.Domain.Session.TransactionState.BattleRevision;
            var result = f.Adapter.CompleteDefeats(f.Departures);
            Check(!Faulted(result.Receipt) && f.Domain.Domain.Pool.ReleaseCount == 1 &&
                !f.Original.gameObject.activeSelf && f.Projection.IsActive && f.Domain.Lease.IsActive &&
                f.Domain.Domain.Session.TransactionState.BattleRevision == revision && f.Domain.Events == 1,
                "Post-home transfer did not release exactly its original actor while retaining Root/projection ownership: " + enemyFirst);
            Check(ReferenceEquals(result.Departures, f.Departures), "Typed transfer reconstructed the departure member set.");
            Check(!result.Receipt.Completion.IsCompleted, "Transfer completion skipped its visible new-state frame.");
            Check(!f.Domain.Domain.Roster.ActiveViews.ContainsReference(f.Original), "Departed view still occupies active formation.");
            if (enemyFirst)
            {
                Check(f.Defeated.TryGet(f.Domain.Frozen.Target, out var model) && model.activeInHierarchy &&
                    model.transform.parent == f.Anchor && model.GetComponent<SpriteRenderer>().sprite == f.IncapSprite &&
                    f.Domain.Domain.Player.IsDeployed && f.Visuals.ActiveDefeatMarkers().Length == 1,
                    "Ally was not transferred to the independent authored incap model/anchor with its X.");
            }
            else Check(f.Defeated.Count == 0 && f.Visuals.ActiveDefeatMarkers().Length == 0, "Enemy departure left a model or stale X.");
            f.Advance(.01d);
            Check(result.Receipt.Completion.IsCompleted && !Faulted(result.Receipt) &&
                ReferenceEquals(result, f.Adapter.CompleteDefeats(f.Departures)) && f.Domain.Domain.Pool.ReleaseCount == 1,
                "Transfer did not complete once after rendering or duplicate transfer released twice.");
            f.Projection.Dispose();
            if (enemyFirst) Check(f.Defeated.TryGet(f.Domain.Frozen.Target, out var retained) && retained.activeInHierarchy,
                "Closing the action scope destroyed the battle-lifetime incapacitated model.");
        }
    }

    public static void DepartureReceiptCarriesExactTransferredViewIdentity()
    {
        foreach (bool enemyFirst in new[] { false, true })
        using (var f = new Fixture(enemyFirst))
        {
            f.Hit(); f.RenderHome();
            int originalId = f.Original.GetInstanceID();
            var result = f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01d);
            Check(result.Views.Count == 1 && result.Views[0].Participant == f.Domain.Frozen.Target &&
                result.Views[0].OriginalViewInstanceId == originalId,
                "Typed departure receipt omitted the exact departed ViewInstanceId.");
            if (enemyFirst)
            {
                Check(f.Defeated.TryGet(f.Domain.Frozen.Target, out var model) &&
                    result.Views[0].IncapacitatedModelInstanceId == model.GetInstanceID(), "Typed ally transfer lost the new model identity.");
            }
            else Check(result.Views[0].IncapacitatedModelInstanceId == 0, "Enemy departure invented an incapacitated model.");
        }
    }

    public static void InvisibleIncapCannotCompleteDeparture()
    {
        using (var f = new Fixture(true))
        {
            f.Hit(); f.RenderHome();
            var result = f.Adapter.CompleteDefeats(f.Departures);
            Check(f.Defeated.TryGet(f.Domain.Frozen.Target, out var model), "Missing transferred fixture model.");
            model.SetActive(false); f.Advance(.01d);
            Check(Faulted(result.Receipt) && f.Domain.Lease.IsActive,
                "An invisible incapacitated model was accepted as rendered departure.");
        }
    }

    public static void TransferRejectsWrongFactsAndMissingBindingWithoutWrites()
    {
        using (var f = new Fixture(true))
        {
            f.Hit(); f.RenderHome();
            var owner = f.Departures.Owner; var action = owner.Action;
            var foreign = BattleTransactionIdentity.ForRootAction(new ActionIdentity(action.BattleGeneration, action.TurnSerial, action.ActionId + 1));
            foreach (var facts in new[] {
                new DRPGBattleDepartureJournal(foreign, f.Departures.SourceFingerprint, f.Departures.Members),
                new DRPGBattleDepartureJournal(owner, f.Departures.SourceFingerprint + 1, f.Departures.Members),
                new DRPGBattleDepartureJournal(owner, f.Departures.SourceFingerprint, Array.Empty<DRPGBattleDepartureMember>()),
                new DRPGBattleDepartureJournal(owner, f.Departures.SourceFingerprint,
                    new[] { new DRPGBattleDepartureMember(f.Domain.Frozen.Target, DRPGBattleSide.Enemy) }) })
                Check(Faulted(f.Adapter.CompleteDefeats(facts).Receipt) && f.Domain.Domain.Pool.ReleaseCount == 0 && f.Defeated.Count == 0,
                    "Mismatched departure provenance or member set caused partial writes.");
        }
        using (var f = new Fixture(true, false))
        {
            f.Hit(); f.RenderHome();
            Check(Faulted(f.Adapter.CompleteDefeats(f.Departures).Receipt) && f.Domain.Domain.Pool.ReleaseCount == 0 &&
                f.Original.gameObject.activeInHierarchy && f.Visuals.ActiveDefeatMarkers().Length == 1,
                "Missing authored incap binding removed or replaced the original actor.");
        }
    }

    public static void IncapAnchorCannotBeAnActiveActorChild()
    {
        using (var f = new Fixture(true))
        {
            f.Anchor.SetParent(f.Original.VisualRoot, false);
            f.Hit(); f.RenderHome();
            Check(Faulted(f.Adapter.CompleteDefeats(f.Departures).Receipt) && f.Domain.Domain.Pool.ReleaseCount == 0 &&
                f.Defeated.Count == 0 && f.Original.gameObject.activeInHierarchy,
                "An active-actor child anchor accepted incap ownership and disappeared with the released actor.");
        }
    }

    public static void InvisibleAuthoredIncapCannotRemoveOriginal()
    {
        using (var f = new Fixture(true))
        {
            f.Template.GetComponent<SpriteRenderer>().enabled = false;
            f.Hit(); f.RenderHome();
            Check(Faulted(f.Adapter.CompleteDefeats(f.Departures).Receipt) && f.Domain.Domain.Pool.ReleaseCount == 0 &&
                f.Original.gameObject.activeInHierarchy && f.Defeated.Count == 0,
                "Invisible authored incap art removed the original actor before preflight.");
        }
    }

    public static void EquivalentDepartureReplayReturnsOriginalReceipt()
    {
        using (var f = new Fixture(false))
        {
            f.Hit(); f.RenderHome();
            var result = f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01d);
            var sameFacts = new DRPGBattleDepartureJournal(f.Departures.Owner, f.Departures.SourceFingerprint, f.Departures.Members);
            Check(ReferenceEquals(result, f.Adapter.CompleteDefeats(sameFacts)) && f.Domain.Domain.Pool.ReleaseCount == 1,
                "An equivalent owner/fingerprint/member replay did not return the original typed receipt.");
        }
    }

    public static void HiddenParentCanvasCannotCompleteIncapDeparture()
    {
        using (var f = new Fixture(true))
        {
            f.Hit(); f.RenderHome();
            var result = f.Adapter.CompleteDefeats(f.Departures);
            f.Visuals.ActiveDefeatMarkers()[0].transform.parent.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            f.Advance(.01d);
            Check(Faulted(result.Receipt), "Invisible inherited marker alpha completed an incapacitation transfer.");
        }
    }

    public static void IncapActivationReentryCannotLeakModelOrThrow()
    {
        using (var f = new Fixture(true))
        {
            f.Template.AddComponent<IncapEnableHook>();
            f.Hit(); f.RenderHome();
            int activations = 0;
            IncapEnableHook.Callback = () => { activations++; f.Projection.Dispose(); };
            try
            {
                DRPGBattleDeparturePresentation result = null; Exception thrown = null;
                try { result = f.Adapter.CompleteDefeats(f.Departures); } catch (Exception error) { thrown = error; }
                Check(activations == 1, "Fixture must observe the real model OnEnable callback.");
                Check(thrown == null && result != null && Faulted(result.Receipt) && f.Defeated.Count == 0,
                    "Incap activation lost ownership but threw or retained an orphan model: " + thrown?.Message);
            }
            finally { IncapEnableHook.Callback = null; }
        }
    }

    [ExecuteAlways]
    public sealed class IncapEnableHook : MonoBehaviour
    {
        internal static Action Callback;
        private void OnEnable() { Callback?.Invoke(); }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackImpactSequenceProbe.Fixture Domain;
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        internal readonly BattleDefeatedActorRoster Defeated;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly DRPGBattleActionProjectionLease Projection;
        internal readonly BattleWorldActorView Original;
        internal readonly Transform Anchor;
        internal readonly Sprite IncapSprite;
        internal readonly GameObject Template;
        private readonly Sprite originalSprite;
        internal DRPGBattleDepartureJournal Departures;
        internal Fixture(bool enemyFirst, bool bindIncap = true, bool fullBattlefield = false, bool lethal = true,
            Action<DRPGBasicAttackProjectionProbe.Fixture> configureScene = null)
        {
            Domain = new DRPGBasicAttackImpactSequenceProbe.Fixture(false, enemyFirst, false, lethal: lethal,
                fullBattlefield: fullBattlefield, configureRules: (domain, lease) =>
                {
                    if (fullBattlefield) ConfigureWorldRows(domain, lease);
                    else ConfigureWorldOrigins(domain, lease);
                    configureScene?.Invoke(domain);
                });
            Domain.Domain.Pool.TryGet(Domain.Frozen.Target.Value, out Original);
            originalSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f));
            Original.VisualRoot.gameObject.AddComponent<SpriteRenderer>().sprite = originalSprite;
            IncapSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f));
            var template = new GameObject("AuthoredIncapModelProbe", typeof(SpriteRenderer));
            Template = template;
            template.transform.SetParent(Domain.Domain.Root.transform, false); template.GetComponent<SpriteRenderer>().sprite = IncapSprite;
            template.SetActive(false);
            var anchor = new GameObject("DefeatedAnchorProbe"); anchor.transform.SetParent(Domain.Domain.Root.transform, false);
            Anchor = anchor.transform; Anchor.position = new Vector3(0, -3, 0);
            Defeated = Domain.Domain.Root.AddComponent<BattleDefeatedActorRoster>();
            Defeated.Configure(bindIncap ? new[] { new BattleIncapacitationBinding(Domain.Frozen.Target, template, Anchor) } : Array.Empty<BattleIncapacitationBinding>());
            Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Domain.Domain.Root, Domain.Clock);
            Adapter = Visuals.CreateAdapter(Domain, Defeated);
            Projection = (DRPGBattleActionProjectionLease)Adapter.OpenProjection(Domain.Frozen);
        }
        internal void Hit()
        {
            Adapter.EnterConflict(new ConflictCompositionCommand(Domain.Frozen, Domain.Clock.Now));
            Advance(.13d); Domain.Commit();
            Check(Domain.Prepared.TryDescribeDepartures(out Departures, out var error), error);
            Adapter.PresentImpact(new ImpactPresentationCommand(Domain.Ledger, Domain.Clock.Now));
            Advance(.46d);
        }
        internal void Advance(double seconds) { Domain.Clock.Now += seconds; Domain.Clock.RenderedFrame++; Adapter.Tick(); }
        internal void RenderHome()
        {
            var returning = Adapter.Return(new ReturnPresentationCommand(Domain.Frozen.Identity));
            Advance(.4d); Advance(.01d);
            Check(returning.Completion.IsCompleted && !Faulted(returning), "Fixture did not render return home.");
        }
        internal static void ConfigureWorldRows(DRPGBasicAttackProjectionProbe.Fixture domain, BattleRuleMutationLease lease)
        {
            DRPGBasicAttackDepartureProbe.SetThreeRows(domain, lease);
            ConfigureWorldOrigins(domain, lease);
        }
        internal static void ConfigureWorldOrigins(DRPGBasicAttackProjectionProbe.Fixture domain, BattleRuleMutationLease lease)
        {
            var player = new GameObject("FormationPlayerOriginProbe").transform;
            var enemy = new GameObject("FormationEnemyOriginProbe").transform;
            player.SetParent(domain.Root.transform, false); enemy.SetParent(domain.Root.transform, false);
            player.position = new Vector3(-4, 0, 0); enemy.position = new Vector3(4, 0, 0);
            domain.Roster.Configure(domain.Pool, player, enemy);
            var snapshot = DRPGBattlePresentationSnapshot.Capture(domain.Session);
            domain.Surface.RenderInitial(snapshot);
            // Geometry-only fixture configuration need not dirty semantic DTOs;
            // establish the real initial layout before acquiring projection.
            domain.Roster.Bind(snapshot.PartyMembers, snapshot.Enemies, true);
        }
        public void Dispose()
        {
            Projection?.Dispose(); Domain.Dispose();
            UnityEngine.Object.DestroyImmediate(originalSprite); UnityEngine.Object.DestroyImmediate(IncapSprite);
        }
    }
    private static bool ContainsReference(this System.Collections.Generic.IReadOnlyList<BattleWorldActorView> views, BattleWorldActorView target)
    { foreach (var view in views) if (ReferenceEquals(view, target)) return true; return false; }
    private static bool Faulted(PresentationReceipt receipt) => receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted;
    private static void Check(bool value, string message) => DRPGBasicAttackProjectionProbe.Check(value, message);
}
#endif
