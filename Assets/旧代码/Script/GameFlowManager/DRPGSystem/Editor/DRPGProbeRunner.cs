#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DRPGProbeRunner
{
    [MenuItem("Tools/ANKALUTE/DRPG/Run Foundation Probes")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    public static string RunAll()
    {
        var results = new List<string>();

        Run("domain", DRPGBattleDomainProbe.Run, results);
        Run("formation", DRPGFormationSystemProbe.Run, results);
        Run("progression", DRPGProgressionProbe.Run, results);
        Run("growth", DRPGGrowthSystemProbe.Run, results);
        Run("weapon definition", DRPGWeaponDefinitionProbe.Run, results);
        Run("weapon instance", DRPGWeaponInstanceProbe.Run, results);
        Run("weapon use ledger", DRPGWeaponUseLedgerProbe.Run, results);
        Run("profession unlock", DRPGProfessionUnlockProbe.Run, results);
        Run("party inventory", DRPGPartyInventoryProbe.Run, results);
        Run("grid inventory", DRPGGridInventoryProbe.Run, results);
        Run("party backpack presentation",
            DRPGPartyBackpackPresentationProbe.Run,
            results);
        Run("weapon equip transaction",
            DRPGWeaponEquipTransactionProbe.Run,
            results);
        Run("resolved equipment build",
            DRPGResolvedBuildEquipmentProbe.Run,
            results);
        Run("battle weapon switch",
            DRPGBattleWeaponSwitchProbe.Run,
            results);
        Run("weapon durability",
            DRPGWeaponDurabilityProbe.Run,
            results);
        Run("trait pool", DRPGTraitPoolProbe.Run, results);
        Run("recovery bag", DRPGRecoveryBagProbe.Run, results);
        Run("equipment save", DRPGEquipmentSaveProbe.Run, results);
        Run("posture", DRPGBattlePostureProbe.Run, results);
        Run("posture-break lifecycle",
            DRPGPostureBreakLifecycleProbe.Run,
            results);
        Run("posture-break session",
            DRPGPostureBreakSessionProbe.Run,
            results);
        Run("posture-break targeting",
            DRPGPostureBreakTargetingProbe.Run,
            results);
        Run("DamageBroken terminology",
            DRPGDamageBrokenTerminologyProbe.Run,
            results);
        Run("guard timing", DRPGGuardTimingProbe.Run, results);
        Run("free-strike geometry", DRPGFreeStrikeGeometryProbe.Run, results);
        Run("free-strike specification", DRPGFreeStrikeSpecProbe.Run, results);
        Run("free-strike contacts", DRPGFreeStrikeContactProbe.Run, results);
        Run("free-strike body contacts",
            DRPGFreeStrikeBodyContactProbe.Run,
            results);
        Run("battlefield free strike",
            DRPGFreeStrikeBattlefieldProbe.Run,
            results);
        Run("telegraphed action", DRPGTelegraphedActionProbe.Run, results);
        Run("enemy intent plan", DRPGEnemyIntentPlanProbe.Run, results);
        Run("free-strike action", DRPGFreeStrikeActionProbe.Run, results);
        Run("interactive run", DRPGBattleInteractiveRunProbe.Run, results);
        Run("interactive presentation",
            DRPGBattleInteractivePresentationProbe.Run,
            results);
        Run("interactive transaction",
            DRPGBattleInteractiveCoordinatorProbe.Run,
            results);
        Run("interactive assets",
            DRPGBattleInteractiveAssetProbe.Run,
            results);
        Run("protagonist invariant", DRPGProtagonistInvariantProbe.Run, results);
        Run("protagonist deployment", DRPGProtagonistDeploymentProbe.Run, results);
        Run("timeline", DRPGTurnSchedulerProbe.Run, results);
        Run("enemy intent session", DRPGEnemyIntentSessionProbe.Run, results);
        Run("session", DRPGBattleSessionProbe.Run, results);
        Run("command kinds", DRPGBattleCommandKindsProbe.Run, results);
        Run("UI modal ownership", GameUIModalCoordinatorProbe.Run, results);
        Run("development debug registry", DevDebugRegistryProbe.Run, results);
        Run("development debug adapters",
            ANKALUTEDevDebugActionsProbe.Run,
            results);
        Run("development debug prefab", DevDebugHubPrefabProbe.Run, results);
        Run("development debug presentation",
            DevDebugHubPresentationProbe.Run,
            results);
        Run("development debug input", DevDebugHubInputProbe.Run, results);
        Run("progression assets", DRPGProgressionAssetProbe.Run, results);
        Run("game menu prefab", GameMenuPrefabProbe.Run, results);
        Run("loadout pool filtering",
            GameMenuLoadoutBehaviorProbe.RunPoolExcludesEquipped,
            results);
        Run("loadout drag cleanup",
            GameMenuLoadoutBehaviorProbe.RunDropCleansGhostImmediately,
            results);
        Run("battle command prefab", DRPGBattleCommandMenuProbe.Run, results);
        Run("battle command pre-battle visibility",
            DRPGBattleCommandMenuProbe.RunHiddenBeforeBattle,
            results);
        Run("battle command hover feedback",
            DRPGBattleCommandMenuProbe.RunHoverFeedback,
            results);
        Run("battle command interrupted transition",
            DRPGBattleCommandMenuProbe.RunInterruptedTransitionDoesNotDrift,
            results);
        Run("battle command shared basic attack rows",
            DRPGBattleCommandMenuProbe.RunSharedBasicAttackSupportsConfiguredPlayerRows,
            results);
        Run("hierarchical battle flow lifecycle",
            DRPGBattleFlowLifecycleProbe.Run,
            results);
        Run("action transactions", DRPGBattleActionTransactionProbe.Run, results);
        Run("unified action resolution",
            DRPGUnifiedActionResolutionProbe.Run,
            results);
        Run("unified action targeting",
            DRPGUnifiedActionResolutionProbe.RunTargeting,
            results);
        Run("unified battlefield effects",
            DRPGUnifiedActionResolutionProbe.RunBattlefieldEffects,
            results);
        Run("unified status lifecycle",
            DRPGUnifiedActionResolutionProbe.RunStatusLifecycle,
            results);
        Run("action-spec contributors",
            DRPGUnifiedActionResolutionProbe.RunActionSpecContributors,
            results);
        Run("equipment action-spec contributors",
            DRPGUnifiedActionResolutionProbe.RunEquipmentActionSpecContributors,
            results);
        Run("action-spec surface and release",
            DRPGUnifiedActionResolutionProbe.RunActionSpecSurfaceAndRelease,
            results);
        Run("battle event and passive reactions",
            DRPGBattleReactionPipelineProbe.Run,
            results);
        Run("immutable battle event batch",
            DRPGBattleEventBatchProbe.Run,
            results);
        Run("deterministic battle progress",
            DRPGBattleProgressPipelineProbe.Run,
            results);
        Run("charged special attack",
            DRPGChargedSpecialAttackProbe.Run,
            results);
        Run("resonance state", DRPGResonanceStateProbe.Run, results);
        Run("resonance battle", DRPGResonanceBattleProbe.Run, results);
        Run("resonance menu", DRPGResonanceMenuProbe.Run, results);
        Run("resonance menu navigation",
            DRPGResonanceMenuProbe.RunRightClickNavigation,
            results);
        Run("resonance menu behavior",
            DRPGResonanceMenuProbe.RunPageBehavior,
            results);
        Run("game loop phases", DRPGGameLoopProbe.Run, results);
        Run("settlement loot transfer", DRPGLootTransferProbe.Run, results);
        Run("victory reward resolution", DRPGVictoryRewardProbe.Run, results);
        Run("game loop UGUI prefab", DRPGGameLoopUIPrefabProbe.Run, results);
        Run("impact feedback", DRPGBattleImpactFeedbackProbe.Run, results);
        Run("timeline motion", DRPGBattleTimelineMotionProbe.Run, results);
        Run("break presentation", DRPGBattleDamageBrokenProbe.Run, results);
        Run("quote and audio", DRPGBattleQuoteAndAudioProbe.Run, results);
        Run("audio palette", DRPGBattleAudioPaletteProbe.Run, results);
        Run("audio channels", GameAudioRootChannelProbe.Run, results);
        Run("portrait assets", DRPGBattlePortraitAssetProbe.Run, results);
        Run("exploration suspension", ExplorationBattleSuspensionProbe.Run, results);
        Run("game flow", DRPGBattleFlowProbe.Run, results);
        Run("presentation model", DRPGBattlePresentationProbe.Run, results);
        Run("hybrid timeline", DRPGHybridTimelineProbe.Run, results);
        Run("presentation runtime", DRPGBattlePresentationRuntimeProbe.Run, results);
        Run("presentation recipes", DRPGBattlePresentationRecipeProbe.Run, results);
        Run("semantic presentation recipes",
            DRPGBattleSemanticRecipeProbe.Run,
            results);
        Run("semantic presentation compilation",
            DRPGBattleSemanticRecipeCompilationProbe.Run,
            results);
        Run("Spine marker gateway",
            DRPGBattleSpineEventBridgeProbe.Run,
            results);
        Run("impact partition sequence",
            DRPGBattleImpactSequenceProbe.Run,
            results);
        Run("Spine marker cue execution",
            DRPGBattleSpineMarkerCueProbe.Run,
            results);
        Run("perspective stage definitions",
            DRPGBattleStageDefinitionProbe.Run,
            results);
        Run("perspective stage runtime",
            DRPGBattleStageRuntimeProbe.Run,
            results);
        Run("battle camera framing",
            DRPGBattleCameraFramingProbe.Run,
            results);
        Run("world actor proxies",
            DRPGBattleWorldActorProbe.Run,
            results);
        Run("world formation layout",
            DRPGWorldFormationLayoutProbe.Run,
            results);
        Run("battle actor hit proxies",
            DRPGBattleActorHitProxyProbe.Run,
            results);
        Run("grouped HUD presenter",
            DRPGBattleHudPresenterProbe.Run,
            results);
        Run("conflict presentation scope",
            DRPGBattleConflictScopeProbe.Run,
            results);
        Run("conflict presentation cues",
            DRPGBattleConflictCueProbe.Run,
            results);
        Run("fixed battle entry presentation",
            DRPGBattleEntryPresentationProbe.Run,
            results);
        Run("perspective presentation assets",
            DRPGBattlePerspectivePresentationAssetProbe.Run,
            results);
        Run("battle world and UGUI separation",
            DRPGBattleWorldUiSeparationProbe.Run,
            results);
        Run("battle world runtime host uniqueness",
            DRPGBattleWorldRuntimeHostProbe.Run,
            results);
        Run("DD1 battle presentation",
            DRPGBattleDD1PresentationProbe.Run,
            results);
        Run("presentation director", DRPGBattlePresentationDirectorProbe.Run, results);
        Run("presentation transaction",
            DRPGBattlePresentationTransactionCoordinatorProbe.Run,
            results);
        Run("presentation cues", DRPGBattlePresentationCueProbe.Run, results);
        Run("presentation bridge", DRPGBattlePresentationBridgeProbe.Run, results);
        Run("presentation assets", DRPGBattlePresentationAssetProbe.Run, results);
        Run("UGUI binding", DRPGBattleUGUIProbe.Run, results);
        Run("target selection", DRPGBattleTargetSelectionProbe.Run, results);
        Run("target feedback", DRPGBattleTargetFeedbackProbe.Run, results);
        Run("targeting prefab", DRPGBattleTargetingPrefabProbe.Run, results);
        Run("break presentation scenario",
            DRPGDamageBrokenPresentationScenarioProbe.Run,
            results);

        return "[DRPGProbes] PASS (" + results.Count + "/" +
            results.Count + "): " +
            string.Join(" | ", results);
    }

    private static void Run(
        string name,
        Func<string> probe,
        ICollection<string> results)
    {
        try
        {
            string result = probe();
            if (string.IsNullOrWhiteSpace(result) ||
                !result.StartsWith("PASS", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Probe did not report PASS: " + (result ?? "<null>"));
            }

            results.Add(name);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "DRPG probe failed: " + name,
                exception);
        }
    }
}
#endif
