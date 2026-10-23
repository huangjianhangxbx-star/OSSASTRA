#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// Non-temporal domain fixtures only. Never bind this to a real presentation,
// a minimum-dwell/watchdog test, or a production composition.
internal static class DRPGBattleHeadlessSessionFactory
{
    // Explicitly unbound variant for tests whose subject is the refusal to
    // start before a presentation composition is supplied.
    internal static DRPGBattleSession CreateUnbound(
        IEnumerable<BattleParticipantRuntime> participants,
        DRPGTurnScheduler scheduler = null)
    {
        return new DRPGBattleSession(participants, scheduler, null, null,
            null, null, null, null, null, null, null, null, null, null,
            DRPGProcessBattleGenerationSource.Instance);
    }

    internal static DRPGBattleSession Create(
        IEnumerable<BattleParticipantRuntime> participants,
        DRPGTurnScheduler scheduler = null,
        DRPGActionResolver resolver = null,
        IDRPGBattleRandom randomSource = null,
        IDRPGBattleItemSource itemSource = null,
        DRPGBattleEscapeRules? escapeRules = null,
        IEnumerable<BattleParticipantRuntime> reserveEnemies = null,
        DRPGFormationRules formationRules = null,
        IDRPGBattleEquipmentService equipmentService = null,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService = null,
        DRPGBattleReactionPipeline reactionPipeline = null,
        DRPGBattleEventFactory eventFactory = null,
        DRPGBattleProgressPipeline progressPipeline = null,
        IDRPGBattleResonanceAccess resonanceAccess = null,
        IDRPGBattleGenerationSource generationSource = null)
    {
        var session = new DRPGBattleSession(participants, scheduler, resolver, randomSource,
            itemSource, escapeRules, reserveEnemies, formationRules, equipmentService,
            weaponDurabilityService, reactionPipeline, eventFactory, progressPipeline,
            resonanceAccess, generationSource ?? DRPGProcessBattleGenerationSource.Instance);
        var port = new ImmediateExplicitTestPresentationPort(session);
        if (session.TransactionPresentationPortSlot.TryBind(session.BattleGeneration, port) != PresentationPortBindResult.Bound)
            throw new InvalidOperationException("Headless test composition could not bind its explicit port.");
        return session;
    }
}

// Compiled dependency audit, not a source-text check. A headless fixture that
// regresses to direct construction must fail even if its old rule assertions pass.
internal static class HeadlessFixtureConstructorAudit
{
    internal static void RequireExplicitComposition()
    {
        var opcodes = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.FieldType == typeof(OpCode))
            {
                var op = (OpCode)field.GetValue(null);
                opcodes.Add(op.Value, op);
            }
        var violations = new List<string>();
        foreach (var type in typeof(DRPGBattleHeadlessSessionFactory).Assembly.GetTypes())
        {
            if (!type.FullName.StartsWith("DRPG", StringComparison.Ordinal) ||
                !type.FullName.Contains("Probe")) continue;
            var methods = new List<MethodBase>(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            methods.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance));
            foreach (var method in methods)
            {
                var body = method.GetMethodBody();
                if (body == null) continue;
                var il = body.GetILAsByteArray();
                for (int offset = 0; offset < il.Length;)
                {
                    int instruction = offset;
                    short value = il[offset++];
                    if (value == 0xfe) value = unchecked((short)(0xfe00 | il[offset++]));
                    var op = opcodes[value];
                    if (op == OpCodes.Newobj &&
                        method.Module.ResolveMethod(BitConverter.ToInt32(il, offset)).DeclaringType == typeof(DRPGBattleSession) &&
                        !IsDeliberateProductionConstruction(type, method))
                        violations.Add(type.FullName + "." + method.Name + " IL_" + instruction.ToString("x4"));
                    switch (op.OperandType)
                    {
                        case OperandType.InlineNone: break;
                        case OperandType.ShortInlineBrTarget:
                        case OperandType.ShortInlineI:
                        case OperandType.ShortInlineVar: offset += 1; break;
                        case OperandType.InlineVar: offset += 2; break;
                        case OperandType.InlineI8:
                        case OperandType.InlineR: offset += 8; break;
                        case OperandType.InlineSwitch: offset += 4 + 4 * BitConverter.ToInt32(il, offset); break;
                        default: offset += 4; break;
                    }
                }
            }
        }
        violations.Sort(StringComparer.Ordinal);
        if (violations.Count != 0)
            throw new InvalidOperationException("Headless fixtures bypass explicit composition: " + string.Join(" | ", violations));
    }

    private static bool IsDeliberateProductionConstruction(Type type, MethodBase method)
    {
        string name = type.FullName;
        // These test actual Unity adapters, not immediate headless behavior.
        if (name == "DRPGBasicAttackProjectionProbe+Fixture") return true;
        if (name == "DRPGBattleBasicAttackProductionPresentationProbe" ||
            name.StartsWith("DRPGBattleBasicAttackProductionPresentationProbe+<", StringComparison.Ordinal)) return true;
        // Kernel/workspace fixtures do not start or advance a Session turn.
        if (name == "DRPGActionResourceJournalProbe+ProductionRulePlan" ||
            name == "DRPGWeaponCompletionJournalProbe+Fixture") return true;
        // Keep the constructor-under-test real, including its rejecting lambda.
        return name.StartsWith("DRPGBattleBasicAttackNineStageProbe", StringComparison.Ordinal) &&
            (method.Name == "CompositionIsCompleteBeforeEscape" ||
             method.Name == "DirectSessionConstructionStillUsesSingleArbiter" ||
             method.Name.StartsWith("<CompositionIsCompleteBeforeEscape>", StringComparison.Ordinal));
    }
}

// No Unity view, clock, projection scope or motion pump: this cannot satisfy
// the new Root's render barriers and cannot masquerade as real visual QA.
internal sealed class ImmediateExplicitTestPresentationPort : IStandardBasicAttackPresentationPort
{
    private readonly long generation;
    private readonly DRPGBattleTransactionPresentationPortSlot slot;
    private readonly DRPGBattleActionTraceWriter trace;

    internal ImmediateExplicitTestPresentationPort(DRPGBattleSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        generation = session.BattleGeneration;
        slot = session.TransactionPresentationPortSlot;
        trace = session.TransactionArbiter.Trace;
    }

    public bool TryCaptureFrozenSnapshot(in DRPGBasicAttackPresentationFreezeRequest request,
        out DRPGBasicAttackPresentationFreezeInput snapshot)
    {
        // There are no real view/slot/art bindings in a headless fixture.
        snapshot = default;
        return false;
    }

    public PresentationReceipt Announce(in AnnouncementCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.Announcement);
    public PresentationReceipt EnterConflict(in ConflictCompositionCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.ConflictComposition);
    public PresentationReceipt PlayWindup(in BasicAttackWindupCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.Windup);
    public PresentationReceipt PresentImpact(in ImpactPresentationCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.ImpactAndReactions);
    public PresentationReceipt PresentReaction(in ReactionPresentationCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.ImpactAndReactions, command.StepOrdinal);
    public PresentationReceipt Return(in ReturnPresentationCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.Return);
    public PresentationReceipt PresentSettlement(in SettlementPresentationCommand command) =>
        Complete(command.Identity, DRPGBattleActionStage.Settlement, command.StepOrdinal);

    public PresentationCleanupReceipt EmergencyCleanup(in BattlePresentationCleanupCommand command)
    {
        RequireBound(command.Owner);
        var receipt = new PresentationCleanupReceipt();
        receipt.TryComplete(true);
        return receipt;
    }

    private PresentationReceipt Complete(ActionIdentity identity, DRPGBattleActionStage stage, int ordinal = 0)
    {
        var owner = BattleTransactionIdentity.ForRootAction(identity);
        RequireBound(owner);
        var receipt = new PresentationReceipt(owner, stage, ordinal);
        string eventId = "headless-" + receipt.ReceiptId.Value;
        trace.Record(0d, 0d, owner, ActionTraceEventKind.ReceiptIssued, stage, ordinal,
            receipt.ReceiptId, eventId + "-issued", detail: "Explicit non-temporal Editor test port");
        receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Explicit headless completion"));
        trace.Record(0d, 0d, owner, ActionTraceEventKind.ReceiptCompleted, stage, ordinal,
            receipt.ReceiptId, eventId + "-completed", eventId + "-issued");
        return receipt;
    }

    private void RequireBound(BattleTransactionIdentity owner)
    {
        if (!owner.IsValid || owner.BattleGeneration != generation ||
            !slot.TryGet(generation, out var current) || !ReferenceEquals(current, this))
            throw new InvalidOperationException("Foreign generation or detached headless presentation port.");
    }
}
#endif
