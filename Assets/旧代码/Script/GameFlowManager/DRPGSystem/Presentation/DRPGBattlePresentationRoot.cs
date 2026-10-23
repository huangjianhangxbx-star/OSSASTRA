using System;
using UnityEngine;

public sealed class DRPGBattlePresentationRoot : MonoBehaviour,
    IDRPGBattlePresentation
{
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private DRPGBattleUGUISurface surface;
    [SerializeField] private DRPGBattlePresentationDirector director;
    [SerializeField] private DRPGBattleActionTransactionCoordinator coordinator;
    [SerializeField] private DRPGBattleInteractionController interaction;
    [SerializeField] private DRPGBattlePresentationEventBridge eventBridge;
    [SerializeField] private BattlePresentationRecipeSetSO recipeSetOverride;
    [SerializeField] private BattleWorldRuntimeHost battleWorldHost;
    [SerializeField] private BattleHudBinding battleV2HudBinding;

    private DRPGBattleSession session;
    private IBattleAudioCueSink audioCueSink = NullBattleAudioCueSink.Instance;
    private BattlePresentationRecipeSetSO transientFallbackRecipeSet;
    private DRPGEquipmentRoot equipmentRoot;
    private BattleSpineMarkerCueExecutor spineMarkerExecutor;
    private BattleSpineEventBridge spineEventGateway;
    private BattleConflictPresentationContext conflictContext;
    private BattleHudPolicyCueExecutor hudPolicyExecutor;
    private BattleStageCueExecutor stageCueExecutor;
    private BattleCameraCueExecutor cameraCueExecutor;
    private BattleActorStagingCueExecutor actorStagingExecutor;
    private BattleBackgroundCueExecutor backgroundCueExecutor;
    private BattleConflictRestoreCueExecutor conflictRestoreExecutor;
    private BattleSemanticModuleCueExecutor semanticModuleExecutor;
    private BattlePresentationResourceCatalog presentationResources;
    private BattleEntryPresentationCoordinator entryCoordinator;
    private IDisposable entryInputLease;
    private IBattlePresentationHandle entryHandle;
    private BattleEntryPresentationRecipeSO currentEntryRecipe;
    private DRPGBattleTransactionPresentationAdapter standardAdapter;
    private DRPGNodeBattleContext activeNodeContext;
    internal UnityRenderFrameFence PreparedClock { get; private set; }

    public void ConfigureEquipment(DRPGEquipmentRoot root)
    {
        equipmentRoot = root;
        if (session != null)
            surface?.ConfigureEquipment(equipmentRoot);
    }

    public void Show(DRPGBattleSession battleSession, DRPGBattleStartRequest request)
        => ShowCore(battleSession, request, false);

    internal void ShowPrepared(DRPGBattleSession battleSession, DRPGBattleStartRequest request)
        => ShowCore(battleSession, request, true);

    private void ShowCore(DRPGBattleSession battleSession, DRPGBattleStartRequest request, bool preparedStartup)
    {
        if (battleSession == null)
            throw new ArgumentNullException(nameof(battleSession));

        UnbindSession(false);
        activeNodeContext = request != null ? request.NodeContext : null;
        ResolveComposition();
        session = battleSession;
        long domainBattleGeneration = battleSession.BattleGeneration;

        DRPGBattlePresentationSO presentation =
            request != null && request.Encounter != null
                ? request.Encounter.Presentation
                : null;
        currentEntryRecipe = presentation != null
            ? presentation.FixedEntryRecipe
            : null;
        if (activeNodeContext != null)
        {
            battleWorldHost.BeginInPlaceBattle(activeNodeContext);
            ConfigureWorldPresentation();
        }
        SetPerspectiveWorldActive(true);
        if (activeNodeContext == null)
            ActivateDefaultStage(presentation);

        surface.Bind(session);
        battleV2HudBinding = battleV2HudBinding != null
            ? battleV2HudBinding
            : GetComponent<BattleHudBinding>();
        battleV2HudBinding?.Bind(session);
        surface.ConfigureEquipment(equipmentRoot);
        surface.SetBackground(null);
        audioCueSink = surface.AudioCueSink;
        audioCueSink.Begin(presentation != null
            ? presentation.AudioPalette
            : null);
        audioCueSink.Play(new BattleAudioCueRequest(BattleAudioCue.BattleBgm));

        BattlePresentationRecipeSetSO recipeSet = ResolveRecipeSet(presentation);
        BattlePresentationRecipeCatalogSO semanticCatalog =
            presentation != null && presentation.SemanticRecipeCatalog != null
                ? presentation.SemanticRecipeCatalog
                : recipeSet.SemanticRecipeCatalog;
        BindSpineMarkerGateway();
        director.Bind(recipeSet, semanticCatalog);
        BeginEntryInputGate();
        eventBridge.Configure(
            director,
            surface,
            audioCueSink,
            interaction);
        interaction.Configure(
            director,
            surface,
            audioCueSink,
            () => eventBridge.CurrentTurnSerial);
        coordinator.ConfigureDirector(director);
        coordinator.ConfigureInteractiveView(
            surface.InteractiveCombatView,
            surface,
            audioCueSink);

        eventBridge.Bind(session);
        coordinator.Bind(session);
        interaction.Bind(session, domainBattleGeneration);

        if (visibleRoot != null)
            visibleRoot.SetActive(true);
        else
            gameObject.SetActive(true);
        surface.RenderInitial(
            DRPGBattlePresentationSnapshot.Capture(session));
        if (preparedStartup)
        {
            var defeated = GetComponent<BattleDefeatedActorRoster>();
            if (defeated == null) defeated = gameObject.AddComponent<BattleDefeatedActorRoster>();
            defeated.ConfigureSpriteParty(surface.WorldRosterPresenter.ActiveViews);
            var binding = GetComponent<BattleStandardPresentationBinding>();
            PreparedClock = GetComponent<UnityRenderFrameFence>();
            if (binding == null || PreparedClock == null)
                throw new InvalidOperationException("Prepared battle requires authored transaction binding and render clock.");
            standardAdapter = binding.CreateAdapter(surface, PreparedClock,
                command => PlayBattleEntry(currentEntryRecipe), GetComponent<BattleDefeatedActorRoster>());
            if (session.TransactionPresentationPortSlot.TryBind(session.BattleGeneration, standardAdapter) != PresentationPortBindResult.Bound ||
                !session.TryBindTurnEntryPresentationFactory(new BattleTurnEntryPresentationFactory(surface.ProjectionArbiter,
                    surface.ImpactFeedbackPresenter, surface.ProjectionCamera, GetComponent<BattleDefeatedActorRoster>(), PreparedClock)))
                throw new InvalidOperationException("Prepared battle presentation could not bind to its Session.");
        }
        else PlayBattleEntry(currentEntryRecipe);
    }

    public void Hide()
    {
        UnbindSession(true);
        if (visibleRoot != null)
            visibleRoot.SetActive(false);
        else
            gameObject.SetActive(false);
        battleWorldHost?.EndInPlaceBattle();
        SetPerspectiveWorldActive(false);
        activeNodeContext = null;
    }

    private void ResolveComposition()
    {
        surface = surface != null
            ? surface
            : GetComponent<DRPGBattleUGUISurface>();
        director = director != null
            ? director
            : GetComponent<DRPGBattlePresentationDirector>();
        coordinator = coordinator != null
            ? coordinator
            : GetComponent<DRPGBattleActionTransactionCoordinator>();
        interaction = interaction != null
            ? interaction
            : GetComponent<DRPGBattleInteractionController>();
        eventBridge = eventBridge != null
            ? eventBridge
            : GetComponent<DRPGBattlePresentationEventBridge>();
        battleWorldHost = battleWorldHost != null
            ? battleWorldHost
            : GetComponent<BattleWorldRuntimeHost>();
        battleWorldHost?.EnsureWorld();
        ConfigureWorldPresentation();
        spineMarkerExecutor = spineMarkerExecutor != null
            ? spineMarkerExecutor
            : GetComponentInChildren<BattleSpineMarkerCueExecutor>(true);
        if (spineMarkerExecutor == null && director != null)
        {
            spineMarkerExecutor =
                director.gameObject.AddComponent<BattleSpineMarkerCueExecutor>();
        }

        ResolveConflictPresentationComposition();

        if (surface == null || director == null || coordinator == null ||
            interaction == null || eventBridge == null)
        {
            throw new InvalidOperationException(
                "The DRPG presentation prefab is missing its orchestration composition.");
        }

        ResolveEntryPresentationComposition();
    }

    private BattlePresentationRecipeSetSO ResolveRecipeSet(
        DRPGBattlePresentationSO presentation)
    {
        if (recipeSetOverride != null)
            return recipeSetOverride;
        if (presentation != null && presentation.RecipeSet != null)
            return presentation.RecipeSet;

        transientFallbackRecipeSet =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        transientFallbackRecipeSet.name =
            "DRPG Empty Fail-Open Presentation Recipes";
        transientFallbackRecipeSet.hideFlags = HideFlags.HideAndDontSave;
        return transientFallbackRecipeSet;
    }

    private void UnbindSession(bool abortIncompleteAction)
    {
        if (session != null && standardAdapter != null)
            session.TransactionPresentationPortSlot.TryUnbind(session.BattleGeneration, standardAdapter);
        standardAdapter = null;
        PreparedClock = null;
        entryHandle?.Cancel(BattlePresentationCancelReason.BattleHidden);
        entryHandle = null;
        entryInputLease?.Dispose();
        entryInputLease = null;
        UnregisterConflictPresentationExecutors();
        conflictContext?.RestoreAll();
        if (director != null && spineMarkerExecutor != null)
            director.UnregisterRuntimeExecutor(spineMarkerExecutor);
        spineMarkerExecutor?.Unbind();
        spineEventGateway?.Dispose();
        spineEventGateway = null;
        interaction?.Unbind();
        battleV2HudBinding?.Unbind();
        coordinator?.Unbind(abortIncompleteAction);
        eventBridge?.Unbind();
        director?.Unbind(BattlePresentationCancelReason.BattleHidden);
        surface?.CancelPresentation(BattlePresentationCancelReason.BattleHidden);
        surface?.Unbind();
        GetComponent<BattleDefeatedActorRoster>()?.ClearBattle();
        audioCueSink.End();
        audioCueSink = NullBattleAudioCueSink.Instance;
        session = null;
        currentEntryRecipe = null;
        DestroyTransientRecipeSet();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetBattleV2PreviewEnabledForDevelopment(bool enabled)
    {
        battleV2HudBinding = battleV2HudBinding != null
            ? battleV2HudBinding
            : GetComponent<BattleHudBinding>();
        battleV2HudBinding?.SetPreviewEnabled(enabled);
    }

    public bool ReplayEntryForDevelopment()
    {
        if (session == null || !session.IsActive || entryCoordinator == null || standardAdapter != null)
            return false;
        BeginEntryInputGate();
        PlayBattleEntry(currentEntryRecipe);
        return true;
    }
#endif

    private void ResolveEntryPresentationComposition()
    {
        if (surface == null || director == null)
            return;
        entryCoordinator = entryCoordinator != null
            ? entryCoordinator
            : GetComponentInChildren<BattleEntryPresentationCoordinator>(true);
        if (entryCoordinator == null)
        {
            entryCoordinator =
                director.gameObject.AddComponent<BattleEntryPresentationCoordinator>();
        }

        CanvasGroup hudRoot = surface.GetComponent<CanvasGroup>();
        if (hudRoot == null)
            hudRoot = surface.gameObject.AddComponent<CanvasGroup>();
        BattleCameraPresenter battleCamera =
            battleWorldHost != null
                ? battleWorldHost.CameraPresenter
                : GetComponentInChildren<BattleCameraPresenter>(true);
        entryCoordinator.Configure(
            hudRoot,
            surface.WorldRosterPresenter,
            battleCamera);
        entryCoordinator.SetInPlaceMode(activeNodeContext != null);
    }

    private void BeginEntryInputGate()
    {
        entryInputLease?.Dispose();
        entryInputLease = director.InputGate.Acquire("BattleEntry");
        entryCoordinator?.PrepareHidden();
    }

    private IBattlePresentationHandle PlayBattleEntry(BattleEntryPresentationRecipeSO recipe)
    {
        entryHandle = entryCoordinator != null
            ? entryCoordinator.Play(recipe)
            : null;
        if (entryHandle == null)
        {
            CompleteBattleEntry();
            return null;
        }
        var issued = entryHandle;
        issued.WhenSettled(_ => CompleteBattleEntry());
        return issued;
    }

    private void CompleteBattleEntry()
    {
        entryInputLease?.Dispose();
        entryInputLease = null;
        entryHandle = null;
    }

    private void ActivateDefaultStage(DRPGBattlePresentationSO presentation)
    {
        BattleStageProfileSO profile = presentation != null
            ? presentation.DefaultStageProfile
            : null;
        if (profile == null && presentationResources != null)
        {
            presentationResources.TryGetStage(
                "default-perspective",
                out profile);
        }
        BattleStagePresenter stage = battleWorldHost != null
            ? battleWorldHost.StagePresenter
            : GetComponentInChildren<BattleStagePresenter>(true);
        stage?.Activate(profile);
    }

    private void SetPerspectiveWorldActive(bool active)
    {
        if (battleWorldHost != null)
        {
            battleWorldHost.SetWorldActive(active);
            return;
        }
        BattleCameraPresenter fallback =
            GetComponentInChildren<BattleCameraPresenter>(true);
        if (fallback != null && fallback.BattleCamera != null)
            fallback.BattleCamera.enabled = active;
    }

    private void ConfigureWorldPresentation()
    {
        if (surface == null || battleWorldHost == null)
            return;
        BattleWorldTargetingPresenter targeting =
            GetComponent<BattleWorldTargetingPresenter>();
        targeting?.ConfigureWorld(
            battleWorldHost.BattleCamera,
            battleWorldHost.ActorPool);
        surface.ConfigureWorldPresentation(
            battleWorldHost,
            battleWorldHost.RosterPresenter,
            targeting);
    }

    private void ResolveConflictPresentationComposition()
    {
        if (director == null)
            return;

        conflictContext = conflictContext != null
            ? conflictContext
            : GetComponentInChildren<BattleConflictPresentationContext>(true);
        if (conflictContext == null)
            conflictContext = director.gameObject.AddComponent<BattleConflictPresentationContext>();

        BattleHudPresenter hud = GetComponentInChildren<BattleHudPresenter>(true);
        BattleCameraPresenter battleCamera =
            battleWorldHost != null
                ? battleWorldHost.CameraPresenter
                : GetComponentInChildren<BattleCameraPresenter>(true);
        BattleStagePresenter battleStage =
            battleWorldHost != null
                ? battleWorldHost.StagePresenter
                : GetComponentInChildren<BattleStagePresenter>(true);
        BattleWorldActorPool worldActors =
            battleWorldHost != null
                ? battleWorldHost.ActorPool
                : GetComponentInChildren<BattleWorldActorPool>(true);
        BattleBackgroundEffectPresenter background =
            GetComponentInChildren<BattleBackgroundEffectPresenter>(true);
        presentationResources = presentationResources != null
            ? presentationResources
            : battleWorldHost != null
                ? battleWorldHost.ResourceCatalog
                : GetComponentInChildren<BattlePresentationResourceCatalog>(true);
        conflictContext.Configure(
            hud,
            battleCamera,
            battleStage,
            worldActors,
            background);
        director.ConfigureConflictContext(conflictContext);

        hudPolicyExecutor = ResolveExecutor(hudPolicyExecutor);
        stageCueExecutor = ResolveExecutor(stageCueExecutor);
        cameraCueExecutor = ResolveExecutor(cameraCueExecutor);
        actorStagingExecutor = ResolveExecutor(actorStagingExecutor);
        backgroundCueExecutor = ResolveExecutor(backgroundCueExecutor);
        conflictRestoreExecutor = ResolveExecutor(conflictRestoreExecutor);
        semanticModuleExecutor = ResolveExecutor(semanticModuleExecutor);

        hudPolicyExecutor.Configure(conflictContext);
        stageCueExecutor.Configure(conflictContext);
        cameraCueExecutor.Configure(conflictContext);
        actorStagingExecutor.Configure(conflictContext);
        backgroundCueExecutor.Configure(conflictContext);
        conflictRestoreExecutor.Configure(conflictContext);
        semanticModuleExecutor.Configure(conflictContext, presentationResources);

        RegisterConflictPresentationExecutors();
    }

    private T ResolveExecutor<T>(T executor)
        where T : MonoBehaviour, IBattlePresentationCueExecutor
    {
        if (executor != null)
            return executor;
        T existing = director.GetComponent<T>();
        return existing != null ? existing : director.gameObject.AddComponent<T>();
    }

    private void RegisterConflictPresentationExecutors()
    {
        RegisterConflictExecutor(hudPolicyExecutor);
        if (activeNodeContext == null)
            RegisterConflictExecutor(stageCueExecutor);
        RegisterConflictExecutor(cameraCueExecutor);
        RegisterConflictExecutor(actorStagingExecutor);
        RegisterConflictExecutor(backgroundCueExecutor);
        RegisterConflictExecutor(conflictRestoreExecutor);
        RegisterConflictExecutor(semanticModuleExecutor);
    }

    private void RegisterConflictExecutor(IBattlePresentationCueExecutor executor)
    {
        if (director != null && executor != null)
            director.RegisterRuntimeExecutor(executor);
    }

    private void UnregisterConflictPresentationExecutors()
    {
        if (director == null)
            return;
        director.UnregisterRuntimeExecutor(hudPolicyExecutor);
        director.UnregisterRuntimeExecutor(stageCueExecutor);
        director.UnregisterRuntimeExecutor(cameraCueExecutor);
        director.UnregisterRuntimeExecutor(actorStagingExecutor);
        director.UnregisterRuntimeExecutor(backgroundCueExecutor);
        director.UnregisterRuntimeExecutor(conflictRestoreExecutor);
        director.UnregisterRuntimeExecutor(semanticModuleExecutor);
    }

    private void BindSpineMarkerGateway()
    {
        spineEventGateway?.Dispose();
        spineEventGateway = new BattleSpineEventBridge(
            ResolveSpineSource,
            new BattlePresentationTraceBuffer(64),
            () => Time.realtimeSinceStartup);
        spineMarkerExecutor.Bind(spineEventGateway, session);
        director.RegisterRuntimeExecutor(spineMarkerExecutor);
    }

    private IBattleSpineEventSource ResolveSpineSource(int runtimeId)
    {
        BattleSpineEventSourceAdapter[] sources =
            GetComponentsInChildren<BattleSpineEventSourceAdapter>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].RuntimeId == runtimeId)
                return sources[i];
        }
        if (battleWorldHost != null)
        {
            sources = battleWorldHost.ResolveAll<BattleSpineEventSourceAdapter>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i].RuntimeId == runtimeId)
                    return sources[i];
            }
        }
        return null;
    }

    private void DestroyTransientRecipeSet()
    {
        if (transientFallbackRecipeSet == null)
            return;

        if (Application.isPlaying)
            Destroy(transientFallbackRecipeSet);
        else
            DestroyImmediate(transientFallbackRecipeSet);
        transientFallbackRecipeSet = null;
    }

    private void OnDisable()
    {
        UnbindSession(true);
        if (battleWorldHost != null && battleWorldHost.HasWorld)
            battleWorldHost.SetWorldActive(false);
    }

    private void Update()
    {
        spineEventGateway?.Tick();
    }
}
