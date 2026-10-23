using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattlePresentationDirector :
    MonoBehaviour,
    IBattlePresentationDirector
{
    [SerializeField] private MonoBehaviour[] cueExecutorBehaviours =
        Array.Empty<MonoBehaviour>();
    [Min(8)]
    [SerializeField] private int traceCapacity = 64;

    private readonly BattleInputGate inputGate = new BattleInputGate();
    private readonly List<IBattlePresentationCueExecutor> runtimeExecutors =
        new List<IBattlePresentationCueExecutor>();
    private BattlePresentationDirector core;
    private BattleSemanticRecipeCompiler semanticCompiler;
    private BattleConflictPresentationContext conflictContext;
    private readonly HashSet<ActionIdentity> standardScopes = new HashSet<ActionIdentity>();

    internal void RetainStandardScope(ActionIdentity identity)
    {
        if (!identity.IsValid) throw new ArgumentException("Complete action required.");
        standardScopes.Add(identity);
    }
    internal void ReleaseStandardScope(ActionIdentity identity) => standardScopes.Remove(identity);

    public BattleInputGate InputGate => inputGate;
    public bool HasDanglingRuns => core != null && core.HasDanglingRuns;

    public void ConfigureConflictContext(
        BattleConflictPresentationContext value)
    {
        conflictContext = value;
    }

    public void RegisterRuntimeExecutor(
        IBattlePresentationCueExecutor executor)
    {
        if (executor != null && !runtimeExecutors.Contains(executor))
            runtimeExecutors.Add(executor);
    }

    public void UnregisterRuntimeExecutor(
        IBattlePresentationCueExecutor executor)
    {
        if (executor != null)
            runtimeExecutors.Remove(executor);
    }

    public void Bind(BattlePresentationRecipeSetSO recipeSet)
    {
        Bind(
            recipeSet,
            recipeSet != null ? recipeSet.SemanticRecipeCatalog : null);
    }

    public void Bind(
        BattlePresentationRecipeSetSO recipeSet,
        BattlePresentationRecipeCatalogSO semanticCatalog)
    {
        if (recipeSet == null)
            throw new ArgumentNullException(nameof(recipeSet));

        Unbind(BattlePresentationCancelReason.ScopeEnded);
        var executors = new List<IBattlePresentationCueExecutor>();
        for (int i = 0; i < cueExecutorBehaviours.Length; i++)
        {
            if (cueExecutorBehaviours[i] == null)
                continue;
            if (!(cueExecutorBehaviours[i] is IBattlePresentationCueExecutor executor))
            {
                throw new InvalidOperationException(
                    cueExecutorBehaviours[i].name +
                    " does not implement IBattlePresentationCueExecutor.");
            }
            executors.Add(executor);
        }
        for (int i = 0; i < runtimeExecutors.Count; i++)
        {
            if (runtimeExecutors[i] != null &&
                !executors.Contains(runtimeExecutors[i]))
            {
                executors.Add(runtimeExecutors[i]);
            }
        }

        BattlePresentationRecipeResolver resolver;
        if (semanticCatalog != null)
        {
            semanticCompiler = new BattleSemanticRecipeCompiler();
            resolver = new BattlePresentationRecipeResolver(
                semanticCatalog.CreateResolver(),
                semanticCompiler);
        }
        else
        {
            resolver = new BattlePresentationRecipeResolver();
        }

        core = new BattlePresentationDirector(
            resolver,
            recipeSet,
            executors,
            inputGate,
            new BattlePresentationTraceBuffer(Math.Max(8, traceCapacity)));
    }

    public void Unbind(
        BattlePresentationCancelReason reason =
            BattlePresentationCancelReason.ScopeEnded)
    {
        core?.CancelAll(reason);
        core = null;
        semanticCompiler?.Dispose();
        semanticCompiler = null;
    }

    public BattlePresentationRun Play(BattlePresentationRequest request)
    {
        if (core == null)
            throw new InvalidOperationException("Presentation director is not bound.");
        if (request.IsStandardBasicAttackLeaf) RetainStandardScope(request.StandardAction);
        BattlePresentationRun run = core.Play(
            request,
            Time.realtimeSinceStartup);
        run.WhenFinished(outcome =>
        {
            if (!request.IsStandardBasicAttackLeaf && BattleConflictPresentationLifecycle.ShouldRestoreAfterRun(
                request.Moment,
                outcome,
                run.GateOutcome))
            {
                conflictContext?.Restore(request);
            }
        });
        return run;
    }

    public void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason)
    {
        core?.CancelScope(scope, reason);
    }

    private void Update()
    {
        core?.Tick(Time.realtimeSinceStartup);
    }

    private void OnDisable()
    {
        // The root presentation session owns its checkpoint even after this
        // component disappears. OnDisable must not rewind a standard action.
        if (standardScopes.Count == 0) conflictContext?.RestoreAll();
        Unbind(BattlePresentationCancelReason.BattleHidden);
    }
}
