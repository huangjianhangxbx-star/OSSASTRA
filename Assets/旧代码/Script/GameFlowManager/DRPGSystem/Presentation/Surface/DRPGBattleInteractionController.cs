using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Converts UGUI intent into domain commands. It never applies battle rules or
/// completes action transactions; those remain in session/coordinator.
/// </summary>
public sealed class DRPGBattleInteractionController : MonoBehaviour
{
    [SerializeField] private DRPGBattlePresentationDirector directorComponent;
    [SerializeField] private DRPGBattleUGUISurface surface;
    [SerializeField] private BattleCommandMenu commandMenu;
    [SerializeField] private BattleSkillMenu skillMenu;
    [SerializeField] private BattleWeaponMenu weaponMenu;
    [SerializeField] private BattleResonanceMenu resonanceMenu;
    [SerializeField] private BattlePartyHud partyHud;
    [SerializeField] private BattlefieldEnemyRosterView enemyRoster;
    [SerializeField] private BattleWorldTargetingPresenter worldTargeting;
    [SerializeField] private BattleFixedSkillBar battleV2SkillBar;
    [SerializeField] private BattleCardHandView battleV2CardHand;
    [SerializeField] private BattleCardTargetArrow battleV2CardArrow;

    private readonly BattlePresentationRequestFactory requestFactory =
        new BattlePresentationRequestFactory();
    private IBattlePresentationDirector configuredDirector;
    private IBattlePresentationDirector director;
    private IBattleAudioCueSink audioCueSink = NullBattleAudioCueSink.Instance;
    private Func<long> turnSerialProvider;
    private DRPGBattleSession session;
    private DRPGSkillSO pendingSkill;
    private DRPGBattleItemDefinition pendingItem;
    private DRPGBattleCommandKind pendingKind = DRPGBattleCommandKind.Skill;
    private DRPGBattleCommand pendingCommand;
    private long battleGeneration;
    private long bindingVersion;
    private bool awaitingTargetConfirmation;
    private readonly InputAction backAction = new InputAction(
        "Battle Back",
        InputActionType.Button,
        "<Mouse>/rightButton");
    private readonly BattleSelectionMode battleV2Selection =
        new BattleSelectionMode();
    private readonly BattleCardHand battleV2PreviewHand = new BattleCardHand();
    private long battleV2InputSerial;
    private int pendingBattleV2Slot = -1;

    public bool AcceptsInput { get; private set; }
    public BattleSelectionKind BattleV2SelectionKind => battleV2Selection.Kind;
    public BattleCardHand BattleV2PreviewHand => battleV2PreviewHand;

    private void Awake()
    {
        backAction.performed += OnBackPerformed;
    }

    private void OnEnable()
    {
        backAction.Enable();
    }

    public void Configure(
        IBattlePresentationDirector presentationDirector,
        DRPGBattleUGUISurface presentationSurface,
        IBattleAudioCueSink cueSink,
        Func<long> currentTurnSerial)
    {
        if (session != null)
        {
            throw new InvalidOperationException(
                "Interaction dependencies cannot change while bound.");
        }
        configuredDirector = presentationDirector ??
            throw new ArgumentNullException(nameof(presentationDirector));
        surface = presentationSurface ??
            throw new ArgumentNullException(nameof(presentationSurface));
        audioCueSink = cueSink ?? NullBattleAudioCueSink.Instance;
        turnSerialProvider = currentTurnSerial ??
            throw new ArgumentNullException(nameof(currentTurnSerial));
    }

    public void Bind(DRPGBattleSession battleSession, long generation)
    {
        if (battleSession == null)
            throw new ArgumentNullException(nameof(battleSession));
        if (generation <= 0L)
            throw new ArgumentOutOfRangeException(nameof(generation));

        Unbind();
        director = configuredDirector ?? directorComponent;
        if (director == null)
            throw new InvalidOperationException("A presentation director is required.");
        if (surface == null)
            throw new InvalidOperationException("A UGUI surface is required.");

        commandMenu = commandMenu != null ? commandMenu : surface.CommandMenu;
        skillMenu = skillMenu != null ? skillMenu : surface.SkillMenu;
        weaponMenu = weaponMenu != null ? weaponMenu : surface.WeaponMenu;
        resonanceMenu = resonanceMenu != null
            ? resonanceMenu
            : surface.ResonanceMenu;
        partyHud = partyHud != null ? partyHud : surface.PartyHud;
        enemyRoster = enemyRoster != null ? enemyRoster : surface.EnemyRoster;
        worldTargeting = worldTargeting != null
            ? worldTargeting
            : surface.WorldTargetingPresenter;
        battleV2SkillBar = battleV2SkillBar != null
            ? battleV2SkillBar
            : surface.BattleV2SkillBar;
        battleV2CardHand = battleV2CardHand != null
            ? battleV2CardHand
            : surface.BattleV2CardHand;
        battleV2CardArrow = battleV2CardArrow != null
            ? battleV2CardArrow
            : surface.BattleV2CardArrow;
        if (turnSerialProvider == null)
            turnSerialProvider = () => 1L;

        session = battleSession;
        battleGeneration = generation;
        battleV2Selection.Reset(generation);
        battleV2InputSerial = 0L;
        bindingVersion++;
        if (skillMenu != null)
            skillMenu.SkillRequested += OnSkillRequested;
        if (battleV2SkillBar != null)
        {
            battleV2SkillBar.SkillSlotRequested += OnBattleV2SkillSlotRequested;
            battleV2SkillBar.PostureRecoveryRequested += OnDefendRequested;
        }
        if (battleV2CardHand != null)
            battleV2CardHand.CardRightClicked += OnBattleV2CardRightClicked;
        BindPreviewCards();
        if (weaponMenu != null)
            weaponMenu.WeaponRequested += OnWeaponRequested;
        if (resonanceMenu != null)
            resonanceMenu.SkillRequested += OnResonanceSkillRequested;
        if (commandMenu != null)
        {
            commandMenu.BasicAttackRequested += OnBasicAttackRequested;
            commandMenu.ItemRequested += OnItemRequested;
            commandMenu.DefendRequested += OnDefendRequested;
            commandMenu.EscapeRequested += OnEscapeRequested;
        }
        if (partyHud != null)
            partyHud.TargetClicked += OnPartyTargetClicked;
        if (enemyRoster != null)
            enemyRoster.TargetClicked += OnEnemyTargetClicked;
        if (worldTargeting != null)
            worldTargeting.TargetClicked += OnWorldEnemyTargetClicked;
        director.InputGate.Changed += OnInputGateChanged;
        RefreshInputState();
    }

    public void Unbind()
    {
        if (skillMenu != null)
            skillMenu.SkillRequested -= OnSkillRequested;
        if (battleV2SkillBar != null)
        {
            battleV2SkillBar.SkillSlotRequested -= OnBattleV2SkillSlotRequested;
            battleV2SkillBar.PostureRecoveryRequested -= OnDefendRequested;
        }
        if (battleV2CardHand != null)
            battleV2CardHand.CardRightClicked -= OnBattleV2CardRightClicked;
        if (weaponMenu != null)
            weaponMenu.WeaponRequested -= OnWeaponRequested;
        if (resonanceMenu != null)
            resonanceMenu.SkillRequested -= OnResonanceSkillRequested;
        if (commandMenu != null)
        {
            commandMenu.BasicAttackRequested -= OnBasicAttackRequested;
            commandMenu.ItemRequested -= OnItemRequested;
            commandMenu.DefendRequested -= OnDefendRequested;
            commandMenu.EscapeRequested -= OnEscapeRequested;
        }
        if (partyHud != null)
            partyHud.TargetClicked -= OnPartyTargetClicked;
        if (enemyRoster != null)
            enemyRoster.TargetClicked -= OnEnemyTargetClicked;
        if (worldTargeting != null)
            worldTargeting.TargetClicked -= OnWorldEnemyTargetClicked;
        if (director != null)
            director.InputGate.Changed -= OnInputGateChanged;

        bindingVersion++;
        session = null;
        director = null;
        battleGeneration = 0L;
        ClearPendingSelection();
        AcceptsInput = false;
        surface?.SetInputEnabled(false);
        surface?.SetTargetingEnabled(false);
        surface?.SetPartyTargetingEnabled(false);
        battleV2CardArrow?.Hide();
    }

    public void OnTurnActivated()
    {
        ClearPendingSelection();
        if (battleGeneration > 0L)
            battleV2Selection.Reset(battleGeneration);
        surface?.ClearTargetFeedback();
        surface?.ShowCommandRoot();
        RefreshInputState();
    }

    public void Suspend()
    {
        ClearPendingSelection();
        AcceptsInput = false;
        surface?.SetInputEnabled(false);
        surface?.SetTargetingEnabled(false);
        surface?.SetPartyTargetingEnabled(false);
    }

    private void OnBasicAttackRequested()
    {
        if (!CanChooseCommand() || session.CurrentActor.Build.BasicAttack == null)
            return;
        BeginAttackTargetSelection(
            session.CurrentActor.Build.BasicAttack,
            DRPGBattleCommandKind.BasicAttack);
    }

    private void OnSkillRequested(DRPGSkillSO skill)
    {
        if (!CanChooseCommand() || skill == null)
            return;
        if (!DRPGBattleSkillModePolicy.IsAllowed(skill))
        {
            skillMenu?.SetFeedback("该手动操作技能已退出当前战斗模式");
            return;
        }
        BeginAttackTargetSelection(skill, DRPGBattleCommandKind.Skill);
    }

    private void OnBattleV2SkillSlotRequested(int slot)
    {
        if (!CanChooseCommand() || session.CurrentActor.Build == null)
            return;
        var configured = session.CurrentActor.Build.ConfiguredSkills;
        if (slot < 0 || slot >= configured.Count || configured[slot] == null)
            return;
        if (!DRPGBattleSkillModePolicy.IsAllowed(configured[slot]))
        {
            skillMenu?.SetFeedback("该技能已退出当前标准战斗模式");
            return;
        }

        long serial = NextBattleV2InputSerial();
        if (battleV2Selection.Kind != BattleSelectionKind.None)
        {
            battleV2Selection.Cancel(serial);
            return;
        }
        if (!battleV2Selection.TryBegin(BattleSelectionKind.Skill, slot, serial))
            return;
        pendingBattleV2Slot = slot;
        BeginConfiguredSkillTargetSelection(configured[slot]);
    }

    private void BeginConfiguredSkillTargetSelection(DRPGSkillSO skill)
    {
        // The standard action router owns target confirmation. Opening the
        // legacy session selection here would hold its gate and reject the
        // configured action as Busy when the player clicks a target.
        pendingSkill = skill;
        pendingItem = null;
        pendingKind = DRPGBattleCommandKind.Skill;
        pendingCommand = null;
        surface.ClearTargetFeedback();
        surface.SetTargetSelectionMode(true);
        bool ally = IsAllyTargetSkill(skill);
        surface.SetTargetPrompt(ally ? "选择己方目标" : "选择敌方目标");
        RefreshInputState();
    }

    private static bool IsAllyTargetSkill(DRPGSkillSO skill)
    {
        return skill != null &&
            (skill.TargetRule == DRPGSkillTargetRule.SingleAlly ||
             skill.TargetRule == DRPGSkillTargetRule.AllAllies ||
             skill.TargetRule == DRPGSkillTargetRule.Self);
    }

    public void ResetPreviewCards(BattleCardTargetRule?[] fourRules)
    {
        battleV2PreviewHand.Reset(fourRules);
        if (battleGeneration > 0L)
            battleV2Selection.Reset(battleGeneration);
    }

    public bool TryBeginPreviewCard(int slot, long inputSerial)
    {
        if (!CanChooseCommand() || !battleV2PreviewHand.HasCard(slot))
            return false;
        if (battleV2Selection.Kind != BattleSelectionKind.None)
            return battleV2Selection.Cancel(inputSerial) && false;
        if (!battleV2Selection.TryBegin(
                BattleSelectionKind.Card, slot, inputSerial))
            return false;
        RefreshPreviewCardTargeting();
        battleV2CardArrow?.Begin(battleV2CardHand?.GetSlotRect(slot));
        return true;
    }

    private void OnBattleV2CardRightClicked(int slot)
    {
        TryBeginPreviewCard(slot, NextBattleV2InputSerial());
    }

    private void BindPreviewCards()
    {
        var cards = surface != null ? surface.BattleV2PreviewCards : null;
        var rules = new BattleCardTargetRule?[4];
        for (int i = 0; i < rules.Length; i++)
            rules[i] = cards != null && i < cards.Count && cards[i] != null
                ? cards[i].TargetRule
                : (BattleCardTargetRule?)null;
        battleV2PreviewHand.Reset(rules);
        battleV2CardHand?.Bind(cards);
        battleV2CardArrow?.Hide();
    }

    private void OnResonanceSkillRequested(DRPGSkillSO skill)
    {
        if (!CanChooseCommand() || skill == null)
            return;
        if (skill.ExecutionMode == DRPGSkillExecutionMode.FreeStrike)
        {
            BeginUntargetedResonanceStrike(skill);
            return;
        }
        BeginAttackTargetSelection(
            skill,
            DRPGBattleCommandKind.ResonanceSkill);
    }

    private void BeginAttackTargetSelection(
        DRPGSkillSO skill,
        DRPGBattleCommandKind kind)
    {
        string error;
        bool began = kind == DRPGBattleCommandKind.ResonanceSkill
            ? session.TryBeginResonanceTargetSelection(skill, out error)
            : session.TryBeginTargetSelection(skill, out error);
        if (!began)
        {
            SetSkillFeedback(kind, error);
            RefreshInputState();
            return;
        }
        pendingSkill = skill;
        pendingItem = null;
        pendingKind = kind;
        pendingCommand = null;
        surface.ClearTargetFeedback();
        surface.SetTargetSelectionMode(true);
        surface.SetTargetPrompt("选择敌方目标");
        audioCueSink.Play(new BattleAudioCueRequest(
            BattleAudioCue.SkillSelected,
            session.CurrentActor?.RuntimeId ?? 0,
            0,
            skill.SkillId));
        RefreshInputState();
    }

    private void OnItemRequested(DRPGBattleItemDefinition item)
    {
        if (!CanChooseCommand() || item == null)
            return;
        if (!session.TryBeginItemTargetSelection(item, out string error))
        {
            skillMenu?.SetFeedback(error);
            RefreshInputState();
            return;
        }
        pendingSkill = null;
        pendingItem = item;
        pendingKind = DRPGBattleCommandKind.Item;
        pendingCommand = null;
        surface.ClearTargetFeedback();
        surface.SetTargetSelectionMode(true);
        surface.SetTargetPrompt("选择己方道具目标");
        RefreshInputState();
    }

    private void OnDefendRequested()
    {
        if (session?.CurrentActor == null)
            return;
        SubmitNoTarget(DRPGBattleCommand.Defend(session.CurrentActor.RuntimeId));
    }

    private void OnWeaponRequested(DRPGBattleWeaponCandidate candidate)
    {
        if (!CanChooseCommand() || candidate == null ||
            !candidate.IsEligible || session?.CurrentActor == null)
        {
            weaponMenu?.SetFeedback(
                candidate?.RejectionReason ?? "当前不可切换武器");
            return;
        }
        SubmitNoTarget(DRPGBattleCommand.SwitchWeapon(
            session.CurrentActor.RuntimeId,
            candidate.WeaponInstanceId));
    }

    private void OnEscapeRequested()
    {
        if (session?.CurrentActor == null)
            return;
        SubmitNoTarget(DRPGBattleCommand.Escape(session.CurrentActor.RuntimeId));
    }

    private void SubmitNoTarget(DRPGBattleCommand command)
    {
        if (!CanChooseCommand())
            return;
        if (!session.TryPrepareCommand(command, out _, out string error))
        {
            skillMenu?.SetFeedback(error);
            RefreshInputState();
            return;
        }
        ClearPendingSelection();
        AcceptsInput = false;
        surface.SetInputEnabled(false);
    }

    private void BeginUntargetedFreeStrike(DRPGSkillSO skill)
    {
        ClearPendingSelection();
        surface.ClearTargetFeedback();
        surface.SetTargetingEnabled(false);
        surface.SetPartyTargetingEnabled(false);
        surface.SetTargetPrompt("全屏自由攻击：移动光标并连续点击");
        if (!session.TryPrepareUntargetedPlayerCommand(
                skill,
                out _,
                out string error))
        {
            skillMenu?.SetFeedback(error);
            RefreshInputState();
            return;
        }
        audioCueSink.Play(new BattleAudioCueRequest(
            BattleAudioCue.SkillSelected,
            session.CurrentActor?.RuntimeId ?? 0,
            0,
            skill.SkillId));
        AcceptsInput = false;
        surface.SetInputEnabled(false);
    }

    private void BeginUntargetedResonanceStrike(DRPGSkillSO skill)
    {
        ClearPendingSelection();
        surface.ClearTargetFeedback();
        surface.SetTargetingEnabled(false);
        surface.SetPartyTargetingEnabled(false);
        surface.SetTargetPrompt("全屏同律攻击：移动光标并连续点击");
        if (!session.TryPrepareUntargetedResonanceCommand(
                skill,
                out _,
                out string error))
        {
            resonanceMenu?.SetFeedback(error);
            RefreshInputState();
            return;
        }
        audioCueSink.Play(new BattleAudioCueRequest(
            BattleAudioCue.SkillSelected,
            session.CurrentActor?.RuntimeId ?? 0,
            0,
            skill.SkillId));
        AcceptsInput = false;
        surface.SetInputEnabled(false);
    }

    private void OnEnemyTargetClicked(BattleParticipantRuntime target)
    {
        if (target == null)
            return;
        if (battleV2Selection.Kind == BattleSelectionKind.Card)
        {
            TryUsePreviewCardOn(target);
            return;
        }
        if (!CanChooseTarget() || pendingItem != null)
            return;
        ConfirmTarget(target);
    }

    private void OnWorldEnemyTargetClicked(int runtimeId)
    {
        BattleParticipantRuntime target = FindParticipant(runtimeId);
        if (target != null)
            OnEnemyTargetClicked(target);
    }

    private void OnPartyTargetClicked(int runtimeId)
    {
        BattleParticipantRuntime target = FindParticipant(runtimeId);
        if (target == null)
            return;
        if (battleV2Selection.Kind == BattleSelectionKind.Card)
        {
            TryUsePreviewCardOn(target);
            return;
        }
        if (CanChooseTarget() &&
            (pendingItem != null || IsAllyTargetSkill(pendingSkill)))
            ConfirmTarget(target);
    }

    private void ConfirmTarget(BattleParticipantRuntime target)
    {
        battleV2Selection.Lock();
        if (pendingKind == DRPGBattleCommandKind.Skill &&
            pendingBattleV2Slot >= 0 && session.StandardBasicAttackEnabled)
        {
            var selection = new DRPGSkillSelection(
                session.CurrentActorTurnToken,
                pendingBattleV2Slot,
                pendingSkill,
                target.RuntimeId);
            RootActionStartResult started =
                session.TryStartConfiguredSkill(selection);
            if (!started.IsAccepted)
            {
                SetSkillFeedback(pendingKind, started.Rejection.ToString());
                // Lock belongs only to an accepted root transaction. If the
                // authority rejects before allocating an ActionId, restore a
                // fresh selection state so the player can choose again.
                battleV2Selection.Reset(battleGeneration);
                ClearPendingSelection();
                surface.SetTargetingEnabled(false);
                surface.SetPartyTargetingEnabled(false);
                surface.SetTargetSelectionMode(false);
                surface.ShowCommandRoot();
                RefreshInputState();
                return;
            }
            ClearPendingSelection();
            surface.SetTargetingEnabled(false);
            surface.SetPartyTargetingEnabled(false);
            surface.SetInputEnabled(false);
            AcceptsInput = false;
            return;
        }
        if (pendingKind == DRPGBattleCommandKind.BasicAttack && session.StandardBasicAttackEnabled)
        {
            var started = session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId));
            if (!started.IsAccepted)
            {
                SetSkillFeedback(pendingKind, started.Rejection.ToString());
                battleV2Selection.Reset(battleGeneration);
                ClearPendingSelection();
                surface.SetTargetingEnabled(false);
                surface.SetPartyTargetingEnabled(false);
                surface.SetTargetSelectionMode(false);
                surface.ShowCommandRoot();
                RefreshInputState(); return;
            }
            ClearPendingSelection();
            surface.SetTargetingEnabled(false);
            surface.SetPartyTargetingEnabled(false);
            surface.SetInputEnabled(false);
            AcceptsInput = false;
            return;
        }
        DRPGBattleCommand command;
        if (pendingKind == DRPGBattleCommandKind.BasicAttack)
        {
            command = DRPGBattleCommand.BasicAttack(
                session.CurrentActor.RuntimeId,
                target.RuntimeId,
                pendingSkill);
        }
        else if (pendingKind == DRPGBattleCommandKind.Item)
        {
            command = DRPGBattleCommand.Item(
                session.CurrentActor.RuntimeId,
                target.RuntimeId,
                pendingItem);
        }
        else if (pendingKind == DRPGBattleCommandKind.ResonanceSkill)
        {
            command = DRPGBattleCommand.ResonanceSkill(
                session.CurrentActor.RuntimeId,
                target.RuntimeId,
                pendingSkill);
        }
        else
        {
            command = DRPGBattleCommand.UseSkill(
                session.CurrentActor.RuntimeId,
                target.RuntimeId,
                pendingSkill);
        }

        if (!session.TryPreviewCommand(command, out string error))
        {
            SetSkillFeedback(command.Kind, error);
            RefreshInputState();
            return;
        }
        pendingCommand = command;
        awaitingTargetConfirmation = true;
        long capturedBinding = bindingVersion;
        DRPGBattleSession capturedSession = session;
        RefreshInputState();

        BattlePresentationRequest request;
        try
        {
            request = requestFactory.CreateTargetingChanged(
                BattlePresentationRequestIds.Next(),
                battleGeneration,
                Math.Max(1L, turnSerialProvider()),
                command,
                capturedSession);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CompleteTargetConfirmation(
                capturedSession,
                capturedBinding,
                command,
                BattlePresentationOutcome.Faulted);
            return;
        }

        try
        {
            BattlePresentationRun run = director.Play(request);
            if (run == null)
            {
                CompleteTargetConfirmation(
                    capturedSession,
                    capturedBinding,
                    command,
                    BattlePresentationOutcome.Skipped);
                return;
            }
            run.WhenGateReleased(outcome => CompleteTargetConfirmation(
                capturedSession,
                capturedBinding,
                command,
                outcome));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CompleteTargetConfirmation(
                capturedSession,
                capturedBinding,
                command,
                BattlePresentationOutcome.Faulted);
        }
    }

    private void CompleteTargetConfirmation(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        DRPGBattleCommand command,
        BattlePresentationOutcome outcome)
    {
        if (!ReferenceEquals(session, capturedSession) ||
            bindingVersion != capturedBinding ||
            !ReferenceEquals(pendingCommand, command))
        {
            return;
        }

        ClearPendingSelection();
        surface.SetTargetingEnabled(false);
        surface.SetPartyTargetingEnabled(false);
        if (!capturedSession.TryPrepareCommand(command, out _, out string error))
        {
            SetSkillFeedback(command.Kind, error);
            RefreshInputState();
            return;
        }
        AcceptsInput = false;
        surface.SetInputEnabled(false);
    }

    private void OnInputGateChanged(bool blocked)
    {
        RefreshInputState();
    }

    public bool TryNavigateBack()
    {
        if (session == null || awaitingTargetConfirmation ||
            director != null && director.InputGate.IsBlocked)
        {
            return false;
        }

        long inputSerial = NextBattleV2InputSerial();
        if (battleV2Selection.Kind == BattleSelectionKind.Card)
        {
            if (!battleV2Selection.Cancel(inputSerial))
                return false;
            surface?.ClearTargetFeedback();
            surface?.SetTargetingEnabled(false);
            surface?.SetPartyTargetingEnabled(false);
            battleV2CardArrow?.Hide();
            RefreshInputState();
            return true;
        }

        if (session.Phase == DRPGBattlePhase.TargetSelection)
        {
            bool returnToResonance =
                pendingKind == DRPGBattleCommandKind.ResonanceSkill;
            if (!session.TryCancelTargetSelection(out _))
                return false;
            ClearPendingSelection();
            battleV2Selection.Cancel(inputSerial);
            surface?.ClearTargetFeedback();
            surface?.SetTargetingEnabled(false);
            surface?.SetPartyTargetingEnabled(false);
            surface?.SetTargetSelectionMode(false);
            if (returnToResonance)
                surface?.ShowResonanceSkillList();
            else
                surface?.ShowCommandRoot();
            RefreshInputState();
            return true;
        }

        return commandMenu != null && commandMenu.TryNavigateBack();
    }

    private void OnBackPerformed(InputAction.CallbackContext context)
    {
        GameUIModalCoordinator modal = GameUIRoot.Instance != null
            ? GameUIRoot.Instance.ModalCoordinator
            : null;
        if (modal != null && modal.ActiveOwner != null)
            return;
        TryNavigateBack();
    }

    private void RefreshInputState()
    {
        bool blocked = director != null && director.InputGate.IsBlocked;
        bool canChooseCommand = !blocked && !awaitingTargetConfirmation &&
            pendingBattleV2Slot < 0 &&
            CanChooseCommand();
        bool canChooseTarget = !blocked && !awaitingTargetConfirmation &&
            CanChooseTarget();
        AcceptsInput = canChooseCommand || canChooseTarget;
        surface?.SetInputEnabled(canChooseCommand);
        surface?.SetTargetingEnabled(
            canChooseTarget && pendingItem == null &&
                !IsAllyTargetSkill(pendingSkill),
            pendingSkill);
        surface?.SetPartyTargetingEnabled(canChooseTarget &&
            (pendingItem != null || IsAllyTargetSkill(pendingSkill)));
        if (battleV2Selection.Kind == BattleSelectionKind.Card)
            RefreshPreviewCardTargeting();
    }

    private bool CanChooseCommand()
    {
        return session != null && session.IsActive &&
            session.TransactionArbiter.IsCommandInputOpen &&
            session.CurrentActor != null &&
            session.CurrentActor.Side == DRPGBattleSide.Player &&
            session.Phase == DRPGBattlePhase.AwaitingCommand;
    }

    private bool CanChooseTarget()
    {
        return session != null && session.IsActive &&
            session.TransactionArbiter.IsCommandInputOpen &&
            (pendingSkill != null || pendingItem != null) &&
            session.CurrentActor != null &&
            session.CurrentActor.Side == DRPGBattleSide.Player &&
            (session.Phase == DRPGBattlePhase.TargetSelection ||
             pendingBattleV2Slot >= 0 &&
             session.Phase == DRPGBattlePhase.AwaitingCommand);
    }

    private BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        if (session == null)
            return null;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            if (session.Participants[i].RuntimeId == runtimeId)
                return session.Participants[i];
        }
        return null;
    }

    private void ClearPendingSelection()
    {
        pendingSkill = null;
        pendingItem = null;
        pendingKind = DRPGBattleCommandKind.Skill;
        pendingCommand = null;
        awaitingTargetConfirmation = false;
        pendingBattleV2Slot = -1;
    }

    private void TryUsePreviewCardOn(BattleParticipantRuntime target)
    {
        if (!CanChooseCommand() || target == null)
            return;
        int slot = battleV2Selection.Slot;
        if (!battleV2PreviewHand.TryUse(slot,
                target.Side == DRPGBattleSide.Enemy,
                target.IsAlive,
                target.IsDeployed))
            return;
        battleV2Selection.Cancel(NextBattleV2InputSerial());
        battleV2CardHand?.ClearSlot(slot);
        battleV2CardArrow?.Hide();
        surface?.ClearTargetFeedback();
        surface?.SetTargetingEnabled(false);
        surface?.SetPartyTargetingEnabled(false);
        skillMenu?.SetFeedback("卡牌演示：未接入实际效果");
        RefreshInputState();
    }

    private void RefreshPreviewCardTargeting()
    {
        BattleCardTargetRule? rule =
            battleV2PreviewHand.GetTargetRule(battleV2Selection.Slot);
        bool enemy = rule == BattleCardTargetRule.Enemy ||
            rule == BattleCardTargetRule.AnyLiving;
        bool ally = rule == BattleCardTargetRule.Ally ||
            rule == BattleCardTargetRule.AnyLiving;
        surface?.SetTargetingEnabled(enemy);
        surface?.SetPartyTargetingEnabled(ally);
        surface?.SetTargetPrompt("卡牌演示：选择目标（不产生实际效果）");
    }

    private long NextBattleV2InputSerial() => ++battleV2InputSerial;

    private void SetSkillFeedback(
        DRPGBattleCommandKind kind,
        string value)
    {
        if (kind == DRPGBattleCommandKind.ResonanceSkill)
            resonanceMenu?.SetFeedback(value);
        else
            skillMenu?.SetFeedback(value);
    }

    private void OnDisable()
    {
        backAction.Disable();
        Unbind();
    }

    private void OnDestroy()
    {
        backAction.performed -= OnBackPerformed;
        backAction.Dispose();
    }
}
