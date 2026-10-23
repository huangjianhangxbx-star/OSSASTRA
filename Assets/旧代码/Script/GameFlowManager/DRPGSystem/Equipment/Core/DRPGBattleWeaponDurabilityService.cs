using System;

public sealed partial class DRPGBattleWeaponDurabilityService :
    IDRPGBattleWeaponDurabilityService
{
    private readonly DRPGEquipmentRoot root;
    private readonly DRPGProgressionService progression;
    private readonly DRPGWeaponUseLedger ledger;
    private long captureSerial;

    internal DRPGBattleActionResourceBindings CreateActionResourceBindings(DRPGBattleProgressLedger progress,
        System.Collections.Generic.IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleResonanceAccess resonance = null) =>
        new DRPGBattleActionResourceBindings(progress, ledger, CaptureWeaponBindings(participants), this, resonanceAccess: resonance);

    internal System.Collections.Generic.IReadOnlyList<DRPGBattleWeaponResourceBinding> CaptureWeaponBindings(
        System.Collections.Generic.IReadOnlyList<BattleParticipantRuntime> participants)
    {
        var bindings = new System.Collections.Generic.List<DRPGBattleWeaponResourceBinding>();
        foreach (var actor in participants)
        {
            if (actor == null || actor.Profile == null) throw new ArgumentException("Weapon resource actor is missing.");
            if (root.TryGetCharacter(actor.Profile.CombatantId, out _, out var equipment) && equipment.EquippedWeapon != null)
                bindings.Add(new DRPGBattleWeaponResourceBinding(new ParticipantId(actor.RuntimeId), equipment.EquippedWeapon));
        }
        return bindings.AsReadOnly();
    }

    internal DRPGBattleEquipmentSnapshot CaptureEquipmentResources(System.Collections.Generic.IReadOnlyList<BattleParticipantRuntime> participants,
        System.Collections.Generic.IEnumerable<DRPGBattleWeaponResourceBinding> bindings) => new DRPGBattleEquipmentSnapshot(this, root, participants, bindings);

    internal bool TryStageRootUse(BattleParticipantRuntime actor, ActionIdentity identity, string eventId,
        DRPGWeaponUseKind kind, DRPGBattleMutationWorkspace workspace, out string error)
    {
        error = string.Empty;
        var equipment = workspace?.ActionResources?.EquipmentSnapshot;
        if (actor == null || !identity.IsValid || workspace == null || workspace.Snapshot.BattleGeneration != identity.BattleGeneration ||
            !workspace.Snapshot.TryGetParticipant(new ParticipantId(actor.RuntimeId), out var captured) ||
            actor.Profile == null || captured.OwnerStableId != actor.Profile.CombatantId ||
            equipment == null || !equipment.IsOwnedBy(this) || !Enum.IsDefined(typeof(DRPGWeaponUseKind), kind))
        { error = "Weapon use requires the captured service, participant and complete Root Action identity."; return false; }
        string weaponId = actor.Build.Equipment.WeaponInstanceId;
        if (string.IsNullOrEmpty(weaponId)) return true;
        if (!equipment.MatchesActor(actor, weaponId))
        { error = "Weapon use actor/build does not match its captured equipment."; return false; }
        // A preserved signature weapon at zero remains owned, but the resolved
        // action is the profession fallback and does not consume weapon durability.
        if (actor.Build.Equipment.IsBroken) return true;
        return workspace.ActionResources.TryStageWeaponUse(identity, eventId, weaponId, 1, out error, kind);
    }

    public DRPGBattleWeaponDurabilityService(
        DRPGEquipmentRoot root,
        DRPGProgressionService progression,
        DRPGWeaponUseLedger ledger = null)
    {
        this.root = root ?? throw new ArgumentNullException(nameof(root));
        this.progression = progression ??
            throw new ArgumentNullException(nameof(progression));
        this.ledger = ledger ?? new DRPGWeaponUseLedger();
    }

    public DRPGBattleWeaponActionContext CaptureAction(
        BattleParticipantRuntime actor,
        long actionId)
    {
        if (actor == null || actionId <= 0 || actor.Profile == null ||
            !root.TryGetCharacter(
                actor.Profile.CombatantId,
                out _,
                out DRPGCharacterEquipmentState equipment))
        {
            return null;
        }

        DRPGWeaponInstance weapon = equipment.EquippedWeapon;
        if (weapon == null || weapon.IsBroken ||
            !string.Equals(
                actor.Build.Equipment.WeaponInstanceId,
                weapon.InstanceId,
                StringComparison.Ordinal))
        {
            return null;
        }

        long serial = ++captureSerial;
        string ledgerActionId = actor.RuntimeId + ":" + actionId + ":" + serial;
        return new DRPGBattleWeaponActionContext(
            actionId,
            ledgerActionId,
            actor.Profile.CombatantId,
            weapon);
    }

    public DRPGEquipmentOperationResult TryRecordCommittedUse(
        DRPGBattleWeaponActionContext context,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1)
    {
        DRPGEquipmentOperationResult preflight = CanRecordCommittedUse(
            context,
            useKind,
            useUnitsOverride);
        if (!preflight.Success)
            return preflight;

        DRPGEquipmentOperationResult result = ledger.TryRecordCommittedUse(
            context.LedgerActionId,
            useKind,
            useUnitsOverride);
        if (result.Success)
        {
            context.CommittedUseUnits = ledger.GetPendingUseUnits(
                context.LedgerActionId);
        }
        return result;
    }

    public DRPGEquipmentOperationResult CanRecordCommittedUse(
        DRPGBattleWeaponActionContext context,
        DRPGWeaponUseKind useKind,
        int useUnitsOverride = -1)
    {
        if (context == null || context.IsCancelled || context.IsCompleted)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "Weapon action context is missing or closed.");
        }

        DRPGEquipmentOperationResult result = ledger.CanRecordCommittedUse(
            context.LedgerActionId,
            useKind,
            useUnitsOverride);
        if (!result.Success)
            return result;

        int units = useUnitsOverride >= 0 ? useUnitsOverride : 1;
        int projected = checked(context.CommittedUseUnits + units);
        if (context.Weapon.IsBroken ||
            projected > context.Weapon.CurrentDurability)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidInstanceState,
                "The captured weapon has insufficient durability for this input.");
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGBattleWeaponActionCompletion CompleteAction(
        BattleParticipantRuntime actor,
        DRPGBattleWeaponActionContext context)
    {
        if (context == null)
            return DRPGBattleWeaponActionCompletion.NoUse();
        if (actor == null || actor.Profile == null ||
            !string.Equals(
                actor.Profile.CombatantId,
                context.CharacterId,
                StringComparison.Ordinal))
        {
            return DRPGBattleWeaponActionCompletion.Fail(
                "Weapon action actor no longer matches its captured owner.");
        }
        if (context.IsCancelled)
        {
            return DRPGBattleWeaponActionCompletion.Fail(
                "Cancelled weapon action cannot be completed.");
        }
        if (context.IsCompleted)
        {
            return DRPGBattleWeaponActionCompletion.NoUse();
        }
        if (context.CommittedUseUnits <= 0)
        {
            CancelAction(context);
            return DRPGBattleWeaponActionCompletion.NoUse();
        }
        if (!root.TryGetCharacter(
                context.CharacterId,
                out DRPGCombatantProfileSO profile,
                out DRPGCharacterEquipmentState equipment) ||
            !ReferenceEquals(equipment.EquippedWeapon, context.Weapon))
        {
            return DRPGBattleWeaponActionCompletion.Fail(
                "Captured weapon changed before durability completion.");
        }

        DRPGWeaponUseCompletionResult use = ledger.CompleteAction(
            context.LedgerActionId,
            context.Weapon);
        if (!use.Success)
            return DRPGBattleWeaponActionCompletion.Fail(use.Message);

        if (use.BrokeThisCompletion && use.ProducesWeaponFragments)
        {
            DRPGEquipmentOperationResult destroyed =
                root.TryConvertBrokenEquippedWeaponToFragments(
                    context.CharacterId,
                    context.Weapon,
                    1);
            if (!destroyed.Success)
            {
                return DRPGBattleWeaponActionCompletion.Fail(
                    destroyed.Message);
            }
        }

        string professionId = actor.Build.Profession != null
            ? actor.Build.Profession.ProfessionId
            : actor.Build.Equipment.ActiveProfessionId;
        DRPGResolvedEquipmentSnapshot snapshot =
            DRPGResolvedEquipmentSnapshot.FromCharacterEquipment(
                equipment,
                professionId,
                root.DefinitionRegistry);
        DRPGResolvedCombatantBuild replacementBuild =
            progression.ResolveBuild(profile, snapshot);
        context.IsCompleted = true;
        return DRPGBattleWeaponActionCompletion.Applied(
            use,
            replacementBuild);
    }

    public void CancelAction(DRPGBattleWeaponActionContext context)
    {
        if (context == null || context.IsCancelled || context.IsCompleted)
            return;
        ledger.CancelAction(context.LedgerActionId);
        context.IsCancelled = true;
    }
}
