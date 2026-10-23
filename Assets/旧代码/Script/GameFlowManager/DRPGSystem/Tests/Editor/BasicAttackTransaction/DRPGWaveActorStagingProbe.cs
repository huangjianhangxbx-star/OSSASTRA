#if UNITY_EDITOR
using System;
using UnityEngine;

internal static class DRPGWaveActorStagingProbe
{
    internal static void MissingOrUndeployedPeerCannotPartiallyExposeTheBatch()
    {
        using (var f = new Fixture())
        using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
        {
            var first = staging.Stage(f.Model);
            var second = staging.Stage(f.SecondModel);
            f.CommitDeployment();
            Check(!staging.TryAdoptDeployed() && f.Pool.ActiveCount == 2 && !f.Pool.TryGet(9901, out _),
                "One deployed member exposed a partially deployed batch.");
            f.CommitSecondDeployment();
            UnityEngine.Object.DestroyImmediate(second.gameObject);
            Check(!staging.TryAdoptDeployed() && f.Pool.ActiveCount == 2 && !f.Pool.TryGet(9901, out _),
                "Missing staged peer caused partial adoption of its survivor.");
            Check(first.gameObject.activeSelf, "Failed adoption prematurely removed the surviving staged view.");
        }
    }

    internal static void FormalSpritePrefabKeepsItsArtAndPoseThroughAdoption()
    {
        using (var f = new Fixture(true))
        using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
        {
            var view = staging.Stage(f.Model);
            var renderer = view.GetComponentInChildren<SpriteRenderer>(true);
            Check(renderer != null && renderer.enabled && renderer.sprite != null,
                "Formal Sprite prefab did not bind visible reserve enemy art.");
            var sprite = renderer.sprite; var scale = renderer.transform.localScale;
            var local = renderer.transform.localPosition; var color = renderer.color;
            f.CommitDeployment(); Check(staging.TryAdoptDeployed(), "Formal Sprite adoption failed.");
            Check(f.Pool.TryGet(9901, out var actual) && ReferenceEquals(actual, view) &&
                renderer.sprite == sprite && renderer.transform.localScale == scale &&
                renderer.transform.localPosition == local && renderer.color == color,
                "Formal Sprite adoption reset art, scale, feet, or color.");
        }
    }

    internal static void ForeignPoolAndDuplicateStagingOwnersAreRejected()
    {
        using (var f = new Fixture())
        using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
        {
            bool duplicate = false;
            try { new BattleWaveActorStaging(f.Pool, f.Projection); } catch (InvalidOperationException) { duplicate = true; }
            var other = new GameObject("ForeignWavePool");
            try
            {
                bool foreign = false;
                try { new BattleWaveActorStaging(other.AddComponent<BattleWorldActorPool>(), f.Projection); }
                catch (InvalidOperationException) { foreign = true; }
                Check(duplicate && foreign, "Projection capability leaked into another staging scope or pool.");
                Check(staging.Stage(f.Model) != null, "Rejected owner displaced the legitimate staging scope.");
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
        }
    }

    internal static void BindingCancellationCannotReactivateARecycledView()
    {
        using (var f = new Fixture())
        {
            var template = new GameObject("WaveBindCancellationTemplate");
            try
            {
                var driver = template.AddComponent<WaveStagingCancellationDriver>();
                var prefab = template.AddComponent<BattleWorldActorView>();
                prefab.ConfigureVisualDriver(driver); f.Pool.Configure(prefab, f.Pool.transform);
                var staging = new BattleWaveActorStaging(f.Pool, f.Projection);
                WaveStagingCancellationDriver.OnBind = staging.Dispose;
                bool rejected = false;
                try { staging.Stage(f.Model); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Binding cancellation was not reported.");
                foreach (var view in f.Pool.GetComponentsInChildren<BattleWorldActorView>(true))
                    Check(view.RuntimeId != 0 || !view.gameObject.activeSelf,
                        "Bind resumed after cancellation and reactivated the recycled view.");
            }
            finally { WaveStagingCancellationDriver.OnBind = null; UnityEngine.Object.DestroyImmediate(template); }
        }
    }

    internal static void StagingDisablesTargetCollidersUntilAdoption()
    {
        using (var f = new Fixture())
        {
            var template = new GameObject("WaveTargetTemplate");
            try
            {
                var prefab = template.AddComponent<BattleWorldActorView>();
                template.AddComponent<BoxCollider2D>();
                var inactiveCollider = template.AddComponent<CircleCollider2D>(); inactiveCollider.enabled = false;
                f.Pool.Configure(prefab, f.Pool.transform);
                using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
                {
                    var view = staging.Stage(f.Model);
                    Check(!view.GetComponent<BoxCollider2D>().enabled, "Staged enemy already had an enabled target collider.");
                    f.CommitDeployment(); Check(staging.TryAdoptDeployed(), "Collider fixture adoption failed.");
                    Check(view.GetComponent<BoxCollider2D>().enabled && !view.GetComponent<CircleCollider2D>().enabled,
                        "Adoption did not restore the exact collider enabled states.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
        }
    }

    internal static void StagedActorsStayOutsideTheActivePool()
    {
        using (var f = new Fixture())
        using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
        {
            var view = staging.Stage(f.Model);
            Check(view != null && view.gameObject.activeSelf, "Wave staging did not create a visible actor.");
            Check(f.Pool.ActiveCount == 2 && !f.Pool.TryGet(f.Reserve.RuntimeId, out _),
                "Staging exposed an enemy in the active pool before deployment.");
            Check(ReferenceEquals(view, staging.Stage(f.Model)), "Duplicate staging regenerated the actor.");
            Check(!staging.TryAdoptDeployed(), "Undeployed enemy was adopted.");
            Check(!f.Reserve.IsDeployed, "Presentation changed deployment rules.");
        }
    }

    internal static void CommittedDeploymentAdoptsTheSameActor()
    {
        using (var f = new Fixture())
        using (var staging = new BattleWaveActorStaging(f.Pool, f.Projection))
        {
            var view = staging.Stage(f.Model);
            Check(view != null, "No staged actor to adopt.");
            var position = new Vector3(3, 2, 0); view.transform.position = position;
            f.CommitDeployment();
            Check(staging.TryAdoptDeployed(), "Committed deployment was not adopted.");
            Check(f.Pool.ActiveCount == 3 && f.Pool.TryGet(f.Reserve.RuntimeId, out var actual) &&
                ReferenceEquals(view, actual) && actual.transform.position == position,
                "Adoption regenerated, relocated, or lost the staged actor.");
            staging.Dispose();
            Check(f.Pool.TryGet(f.Reserve.RuntimeId, out actual) && actual.gameObject.activeSelf,
                "Staging cleanup removed an adopted actor.");
        }
    }

    internal static void CancellationRetractsAndRejectsLateStaging()
    {
        using (var f = new Fixture())
        {
            var staging = new BattleWaveActorStaging(f.Pool, f.Projection);
            var view = staging.Stage(f.Model);
            Check(view != null, "No staged actor to cancel.");
            staging.Dispose(); staging.Dispose();
            Check(!view.gameObject.activeSelf && view.RuntimeId == 0 && f.Pool.ActiveCount == 2,
                "Cancellation left a live staged actor or changed the existing roster.");
            bool rejected = false;
            try { staging.Stage(f.Model); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !staging.TryAdoptDeployed(), "Late callback revived cancelled staging.");
        }
    }

    internal static void ClosingProjectionRetractsUnadoptedActors()
    {
        using (var f = new Fixture())
        {
            var staging = new BattleWaveActorStaging(f.Pool, f.Projection);
            var view = staging.Stage(f.Model);
            Check(view != null, "No staged actor to close.");
            f.Projection.Dispose();
            Check(!view.gameObject.activeSelf && view.RuntimeId == 0 && !staging.TryAdoptDeployed(),
                "Projection disposal leaked a staged actor or left its capability live.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DRPGBasicAttackProjectionProbe.Fixture basis = new DRPGBasicAttackProjectionProbe.Fixture(false);
        private readonly DRPGCombatantProfileSO profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        internal readonly BattleParticipantRuntime Reserve;
        internal readonly DRPGBattleParticipantViewModel Model;
        internal readonly BattleParticipantRuntime SecondReserve;
        internal readonly DRPGBattleParticipantViewModel SecondModel;
        internal readonly DRPGBattleActionProjectionLease Projection;
        private readonly DRPGBattleSessionState state;
        private readonly BattleRuleMutationLease ruleLease;
        internal BattleWorldActorPool Pool => basis.Pool;
        internal Fixture(bool realAssets = false)
        {
            if (realAssets)
            {
                var source = UnityEditor.AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>(
                    "Assets/GameResources/DRPG/Config/Test/DRPGCombatant_ankalute_enemy_sentinel.asset");
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleWorldActorView>(
                    "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleWorldActor.prefab");
                Check(source != null && prefab != null, "Formal enemy profile or world actor prefab missing.");
                UnityEditor.EditorUtility.CopySerialized(source, profile);
                Pool.Configure(prefab, Pool.transform);
            }
            Reserve = new BattleParticipantRuntime(9901, new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0, initiallyDeployed: false));
            Model = new DRPGBattleParticipantViewModel(Reserve, 0, false);
            SecondReserve = new BattleParticipantRuntime(9902, new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 1, initiallyDeployed: false));
            SecondModel = new DRPGBattleParticipantViewModel(SecondReserve, 0, false);
            var owner = BattleTransactionIdentity.ForWave(new WaveTransitionIdentity(basis.Session.BattleGeneration, 1, 1));
            Projection = basis.Surface.ProjectionArbiter.TryAcquire(owner).Lease;
            Check(Projection != null, "Wave projection fixture failed.");
            var arbiter = new DRPGBattleTransactionArbiter(basis.Session.BattleGeneration,
                new DRPGBattleActionTraceWriter(new DRPGBoundedActionTraceSink(64)));
            state = new DRPGBattleSessionState(basis.Session.BattleGeneration, new[] { Reserve, SecondReserve }, new DRPGTurnScheduler(), arbiter);
            Check(arbiter.TryAcquire(owner, out ruleLease, out _), "Wave rule fixture failed.");
        }
        internal void CommitDeployment()
        {
            var draft = state.CreateWorkspace(); draft.For(new ParticipantId(9901)).SetDeployed(true);
            Check(state.TryCommitJournal(ruleLease, draft).Succeeded, "Deployment fixture commit failed.");
        }
        internal void CommitSecondDeployment()
        {
            var draft = state.CreateWorkspace(); draft.For(new ParticipantId(9902)).SetDeployed(true);
            Check(state.TryCommitJournal(ruleLease, draft).Succeeded, "Second deployment fixture commit failed.");
        }
        public void Dispose()
        {
            Projection?.Dispose(); ruleLease?.Dispose(); basis.Dispose(); UnityEngine.Object.DestroyImmediate(profile);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

internal sealed class WaveStagingCancellationDriver : MonoBehaviour, IBattleWorldActorVisualDriver
{
    internal static Action OnBind;
    public void Bind(DRPGBattleParticipantViewModel actor) { if (actor != null) OnBind?.Invoke(); }
    public IBattlePresentationHandle PlayIntent(EAnimationLogicState intent) => null;
    public void RestoreVisual() { }
    public void ResetVisual() { }
}
#endif
