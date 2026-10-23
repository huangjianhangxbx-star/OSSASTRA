using System;
using System.Collections.Generic;
using System.Threading;

internal readonly struct DRPGBattleWeaponResourceBinding
{
    internal DRPGBattleWeaponResourceBinding(ParticipantId participant, DRPGWeaponInstance weapon)
    {
        Participant = participant;
        Weapon = weapon;
    }
    internal ParticipantId Participant { get; }
    internal DRPGWeaponInstance Weapon { get; }
}

internal sealed class DRPGBattleActionResourceBindings
{
    private static long nextBindingId;
    private readonly long bindingId = Interlocked.Increment(ref nextBindingId);
    private readonly DRPGBattleProgressLedger progress;
    private readonly DRPGWeaponUseLedger weaponUses;
    private readonly DRPGBattleWeaponDurabilityService weaponService;
    private readonly DRPGResonanceExpeditionState resonance;
    private readonly DRPGBattleResonanceAccess resonanceAccess;
    private IReadOnlyList<BattleParticipantRuntime> participants;
    private readonly Dictionary<string, DRPGBattleWeaponResourceBinding> weapons =
        new Dictionary<string, DRPGBattleWeaponResourceBinding>(StringComparer.Ordinal);
    private readonly HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
    private long battleGeneration;
    private DRPGBattleStateMutationCapability mutationCapability;

    internal DRPGBattleActionResourceBindings(DRPGBattleProgressLedger progress,
        DRPGWeaponUseLedger weaponUses, IEnumerable<DRPGBattleWeaponResourceBinding> weapons,
        DRPGBattleWeaponDurabilityService weaponService = null, DRPGResonanceExpeditionState resonance = null,
        DRPGBattleResonanceAccess resonanceAccess = null)
    {
        this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        this.weaponUses = weaponUses ?? throw new ArgumentNullException(nameof(weaponUses));
        this.weaponService = weaponService;
        this.resonance = resonance ?? resonanceAccess?.Expedition;
        this.resonanceAccess = resonanceAccess;
        if (resonanceAccess != null && !ReferenceEquals(this.resonance, resonanceAccess.Expedition))
            throw new ArgumentException("Resonance access and expedition binding must agree.");
        if (weapons == null) return;
        foreach (var binding in weapons)
        {
            if (binding.Participant.Value <= 0 || binding.Weapon == null)
                throw new ArgumentException("A bound weapon requires a participant and real instance.");
            this.weapons.Add(binding.Weapon.InstanceId, binding);
        }
    }

    internal void BindParticipants(long generation, IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleStateMutationCapability capability)
    {
        if (generation <= 0 || battleGeneration != 0 || capability == null || capability.BattleGeneration != generation)
            throw new InvalidOperationException("Action resource bindings belong to exactly one battle State.");
        var ids = new HashSet<ParticipantId>();
        foreach (var participant in participants)
        {
            ids.Add(new ParticipantId(participant.RuntimeId));
            if (participant.Profile != null && !string.IsNullOrWhiteSpace(participant.Profile.CombatantId))
                owners.Add(participant.Profile.CombatantId.Trim());
        }
        foreach (var binding in weapons.Values)
            if (!ids.Contains(binding.Participant))
                throw new InvalidOperationException("A bound weapon owner is outside this battle.");
        battleGeneration = generation;
        mutationCapability = capability;
        this.participants = Array.AsReadOnly(new List<BattleParticipantRuntime>(participants).ToArray());
    }

    internal DRPGBattleActionResourceSnapshot Capture()
    {
        RefreshWeaponBindings();
        var capturedWeapons = new List<DRPGBattleWeaponResourceSnapshot>();
        foreach (var binding in weapons.Values)
            capturedWeapons.Add(new DRPGBattleWeaponResourceSnapshot(binding));
        capturedWeapons.Sort((left, right) => StringComparer.Ordinal.Compare(left.WeaponId, right.WeaponId));
        return new DRPGBattleActionResourceSnapshot(bindingId, battleGeneration,
            progress.Revision, weaponUses.Revision, progress.CreateSnapshot(), capturedWeapons, owners,
            weaponService?.CaptureEquipmentResources(participants, weapons.Values), resonance?.CaptureActionResources(),
            resonanceAccess?.CaptureActionRules(participants));
    }

    internal DRPGBattlePreparedResourceCommit Prepare(DRPGBattleActionResourceJournal journal,
        BattleTransactionIdentity owner, DRPGBattleMutationJournal battleJournal = null, BattleRuleSnapshot battleSnapshot = null)
    {
        if (journal == null || journal.Snapshot == null)
            throw new InvalidOperationException("The State's action resource snapshot is missing.");
        RefreshWeaponBindings();
        var snapshot = journal.Snapshot;
        if (snapshot.BindingId != bindingId || snapshot.BattleGeneration != battleGeneration ||
            snapshot.ProgressRevision != progress.Revision || snapshot.WeaponUseRevision != weaponUses.Revision ||
            !ProgressMatches(snapshot.Progress, progress.CreateSnapshot()) || snapshot.Weapons.Count != weapons.Count)
            throw new InvalidOperationException("The action resource snapshot is foreign or stale.");
        foreach (var captured in snapshot.Weapons)
        {
            if (!weapons.TryGetValue(captured.WeaponId, out var binding) ||
                captured.Participant != binding.Participant ||
                captured.CurrentDurability != binding.Weapon.CurrentDurability ||
                captured.MaximumDurability != binding.Weapon.CurrentMaxDurability ||
                captured.Revision != binding.Weapon.DurabilityRevision ||
                captured.BreakDisposition != binding.Weapon.Definition.BreakDisposition)
                throw new InvalidOperationException("A captured weapon changed before journal commit.");
        }
        if (journal.HasChanges && (!journal.Identity.IsValid ||
            journal.Identity.BattleGeneration != battleGeneration ||
            owner != BattleTransactionIdentity.ForRootAction(journal.Identity)))
            throw new InvalidOperationException("Action resource mutations require their exact Root Action lease.");

        if ((snapshot.Resonance != null) != (resonance != null) || (resonance == null && journal.ResonanceMutations.Count != 0))
            throw new InvalidOperationException("Resonance journal differs from its expedition binding.");
        foreach (var mutation in journal.ResonanceMutations)
            if (mutation == null || !owners.Contains(mutation.Partner))
                throw new InvalidOperationException("Resonance journal contains a foreign partner.");
        var preparedResonance = resonance?.PrepareActionResources(snapshot.Resonance, journal.ResonanceMutations, mutationCapability);
        if ((snapshot.ResonanceRules != null) != (resonanceAccess != null) ||
            (snapshot.ResonanceRules != null && !snapshot.ResonanceRules.Matches(resonanceAccess)))
            throw new InvalidOperationException("The selected resonance partner or equipped skill changed after action lock.");

        DRPGBattleProgressLedger preparedProgress = null;
        if (journal.ProgressMutations.Count > 0)
        {
            preparedProgress = progress.Fork();
            foreach (var planned in journal.ProgressMutations)
            {
                if (planned == null || !owners.Contains(planned.Request.OwnerStableId))
                    throw new InvalidOperationException("A progress mutation has a foreign owner.");
                var actual = preparedProgress.Apply(planned.Request);
                if (!actual.WasApplied || actual.ValueBefore != planned.ValueBefore ||
                    actual.ValueAfter != planned.ValueAfter || actual.Capacity != planned.Capacity ||
                    actual.AppliedAmount != planned.AppliedAmount || actual.OverflowAmount != planned.OverflowAmount)
                    throw new InvalidOperationException("Progress journal values do not match its rule requests.");
            }
        }

        var keys = new List<string>();
        var losses = new List<DRPGBattlePreparedWeaponLoss>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var eventTotals = new Dictionary<string, long>(StringComparer.Ordinal);
        var eventKeys = new HashSet<(string, string)>();
        foreach (var use in journal.WeaponEvents)
        {
            if (string.IsNullOrWhiteSpace(use.WeaponId) || string.IsNullOrWhiteSpace(use.EventId) || use.Units < 0 ||
                !eventKeys.Add((use.WeaponId, use.EventId)) ||
                (use.UseKind.HasValue && !Enum.IsDefined(typeof(DRPGWeaponUseKind), use.UseKind.Value)))
                throw new InvalidOperationException("Weapon use event identity, units or kind is invalid.");
            eventTotals.TryGetValue(use.WeaponId, out long previous);
            eventTotals[use.WeaponId] = checked(previous + use.Units);
        }
        if (eventTotals.Count != journal.WeaponUses.Count ||
            journal.WeaponCompletions.Count != (snapshot.Equipment != null ? journal.WeaponUses.Count : 0))
            throw new InvalidOperationException("Weapon uses, events and equipment completions must have exact coverage.");
        foreach (var mutation in journal.WeaponUses)
        {
            if (!seen.Add(mutation.WeaponId) || !weapons.TryGetValue(mutation.WeaponId, out var binding) ||
                !eventTotals.TryGetValue(mutation.WeaponId, out long total) || total != mutation.Units ||
                mutation.Units < 0 || mutation.Units > binding.Weapon.CurrentDurability ||
                binding.Weapon.IsBroken || mutation.DurabilityAfter != binding.Weapon.CurrentDurability - mutation.Units)
                throw new InvalidOperationException("Weapon use journal is invalid or exceeds captured durability.");
            if (mutation.DurabilityAfter == 0 && binding.Weapon.Definition.BreakDisposition == DRPGWeaponBreakDisposition.DestroyIntoFragments && snapshot.Equipment == null)
                throw new InvalidOperationException("Atomic ordinary weapon break requires the equipment/fragment reconciliation binding.");
            keys.Add(DRPGBattleActionResourceWorkspace.WeaponActionKey(journal.Identity, mutation.WeaponId));
            losses.Add(new DRPGBattlePreparedWeaponLoss(binding.Weapon, mutation.Units));
        }
        if (journal.WeaponCompletions.Count > 0)
        {
            if (battleJournal == null || battleSnapshot == null || !snapshot.Equipment.IsOwnedBy(weaponService))
                throw new InvalidOperationException("Equipment completion requires the full captured State journal.");
            var completed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var completion in journal.WeaponCompletions)
            {
                if (completion == null || completion.Plan == null || !completed.Add(completion.Plan.WeaponId) ||
                    !eventTotals.TryGetValue(completion.Plan.WeaponId, out long total) || total != completion.Plan.Units ||
                    !battleSnapshot.TryGetParticipant(new ParticipantId(completion.Plan.Actor.RuntimeId), out var actor))
                    throw new InvalidOperationException("Equipment completion identity or use count differs from its journal.");
                float hp = actor.CurrentHp, mana = actor.CurrentMp, posture = actor.CurrentPosture;
                foreach (var changed in battleJournal.ParticipantMutations)
                {
                    if (changed.Participant != actor.Participant) continue;
                    if ((changed.DirtySet & DRPGBattleMutationDirtySet.HitPoints) != 0) hp = changed.HitPoints;
                    if ((changed.DirtySet & DRPGBattleMutationDirtySet.Mana) != 0) mana = changed.Mana;
                    if ((changed.DirtySet & DRPGBattleMutationDirtySet.Posture) != 0) posture = changed.Posture;
                }
                if (completion.HpBefore != hp || completion.ManaBefore != mana || completion.PostureBefore != posture)
                    throw new InvalidOperationException("Weapon reconciliation resources differ from the full action journal.");
            }
        }
        var preparedUses = keys.Count > 0 ? weaponUses.PrepareRootCompletions(keys) : null;
        var preparedEquipment = snapshot.Equipment?.Prepare(journal.WeaponCompletions, mutationCapability);
        var expectedCapacity = snapshot.Equipment != null
            ? snapshot.Equipment.BuildProgressReconciliations(journal.Identity, journal.WeaponCompletions, preparedProgress ?? progress, snapshot.Progress)
            : Array.Empty<DRPGWeaponProgressReconciliation>();
        if (expectedCapacity.Count != journal.WeaponProgressReconciliations.Count)
            throw new InvalidOperationException("Weapon capacity reconciliation is missing or has extra facts.");
        for (int i = 0; i < expectedCapacity.Count; i++)
        {
            if (!expectedCapacity[i].Matches(journal.WeaponProgressReconciliations[i]))
                throw new InvalidOperationException("Weapon capacity facts differ from the captured equipment and full progress journal.");
            if (preparedProgress == null) preparedProgress = progress.Fork();
            var applied = preparedProgress.Apply(expectedCapacity[i].Mutation.Request);
            if (!applied.WasApplied) throw new InvalidOperationException("Validated weapon capacity request could not be prepared.");
        }
        return new DRPGBattlePreparedResourceCommit(progress, preparedProgress, weaponUses, preparedUses, losses, mutationCapability, preparedEquipment, preparedResonance);
    }

    private static bool ProgressMatches(IReadOnlyList<DRPGProgressSnapshot> left, IReadOnlyList<DRPGProgressSnapshot> right)
    {
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
        {
            var a = left[i]; var b = right[i];
            if (a.OwnerStableId != b.OwnerStableId || a.ResourceId != b.ResourceId || a.Scope != b.Scope ||
                !a.Definition.HasSameValues(b.Definition) || a.CurrentValue != b.CurrentValue)
                return false;
        }
        return true;
    }

    private void RefreshWeaponBindings()
    {
        if (weaponService == null) return;
        var current = weaponService.CaptureWeaponBindings(participants);
        weapons.Clear();
        foreach (var binding in current) weapons.Add(binding.Weapon.InstanceId, binding);
    }
}

internal readonly struct DRPGBattleWeaponResourceSnapshot
{
    internal DRPGBattleWeaponResourceSnapshot(DRPGBattleWeaponResourceBinding binding)
    {
        Participant = binding.Participant;
        WeaponId = binding.Weapon.InstanceId;
        CurrentDurability = binding.Weapon.CurrentDurability;
        MaximumDurability = binding.Weapon.CurrentMaxDurability;
        Revision = binding.Weapon.DurabilityRevision;
        BreakDisposition = binding.Weapon.Definition.BreakDisposition;
    }
    internal ParticipantId Participant { get; }
    internal string WeaponId { get; }
    internal int CurrentDurability { get; }
    internal int MaximumDurability { get; }
    internal long Revision { get; }
    internal DRPGWeaponBreakDisposition BreakDisposition { get; }
}

internal sealed class DRPGBattleActionResourceSnapshot
{
    private readonly HashSet<string> owners;
    internal DRPGBattleActionResourceSnapshot(long bindingId, long generation, long progressRevision, long weaponUseRevision,
        IReadOnlyList<DRPGProgressSnapshot> progress, IReadOnlyList<DRPGBattleWeaponResourceSnapshot> weapons, IEnumerable<string> owners,
        DRPGBattleEquipmentSnapshot equipment = null, DRPGResonanceResourceSnapshot resonance = null,
        DRPGActionResonanceRules resonanceRules = null)
    {
        BindingId = bindingId;
        BattleGeneration = generation;
        ProgressRevision = progressRevision;
        WeaponUseRevision = weaponUseRevision;
        Progress = Array.AsReadOnly(new List<DRPGProgressSnapshot>(progress).ToArray());
        Weapons = Array.AsReadOnly(new List<DRPGBattleWeaponResourceSnapshot>(weapons).ToArray());
        this.owners = new HashSet<string>(owners, StringComparer.Ordinal);
        Equipment = equipment;
        Resonance = resonance;
        ResonanceRules = resonanceRules;
    }
    internal long BindingId { get; }
    internal long BattleGeneration { get; }
    internal long ProgressRevision { get; }
    internal long WeaponUseRevision { get; }
    internal IReadOnlyList<DRPGProgressSnapshot> Progress { get; }
    internal IReadOnlyList<DRPGBattleWeaponResourceSnapshot> Weapons { get; }
    internal DRPGBattleEquipmentSnapshot Equipment { get; }
    internal DRPGResonanceResourceSnapshot Resonance { get; }
    internal DRPGActionResonanceRules ResonanceRules { get; }
    internal bool IsKnownOwner(string owner) => owners.Contains(owner);
    internal bool TryGetWeapon(string id, out DRPGBattleWeaponResourceSnapshot snapshot)
    {
        foreach (var weapon in Weapons)
            if (StringComparer.Ordinal.Equals(weapon.WeaponId, id)) { snapshot = weapon; return true; }
        snapshot = default;
        return false;
    }
}

internal readonly struct DRPGBattleWeaponUseMutation
{
    internal DRPGBattleWeaponUseMutation(string weaponId, int units, int durabilityAfter)
    { WeaponId = weaponId; Units = units; DurabilityAfter = durabilityAfter; }
    internal string WeaponId { get; }
    internal int Units { get; }
    internal int DurabilityAfter { get; }
}

internal readonly struct DRPGBattleWeaponUseEvent
{
    internal DRPGBattleWeaponUseEvent(string weaponId, string eventId, int units, DRPGWeaponUseKind? useKind = null)
    { WeaponId = weaponId; EventId = eventId; Units = units; UseKind = useKind; }
    internal string WeaponId { get; }
    internal string EventId { get; }
    internal int Units { get; }
    internal DRPGWeaponUseKind? UseKind { get; }
}

internal sealed class DRPGBattleActionResourceJournal
{
    internal DRPGBattleActionResourceJournal(DRPGBattleActionResourceSnapshot snapshot, ActionIdentity identity,
        IReadOnlyList<DRPGProgressMutationResult> progress, IReadOnlyList<DRPGBattleWeaponUseMutation> uses,
        IReadOnlyList<DRPGBattleWeaponUseEvent> events, IReadOnlyList<DRPGWeaponCompletionMutation> completions = null,
        IReadOnlyList<DRPGWeaponProgressReconciliation> progressReconciliations = null,
        IReadOnlyList<DRPGActionResonanceMutation> resonanceMutations = null)
    {
        Snapshot = snapshot;
        Identity = identity;
        ProgressMutations = Array.AsReadOnly(new List<DRPGProgressMutationResult>(progress).ToArray());
        WeaponUses = Array.AsReadOnly(new List<DRPGBattleWeaponUseMutation>(uses).ToArray());
        WeaponEvents = Array.AsReadOnly(new List<DRPGBattleWeaponUseEvent>(events).ToArray());
        WeaponCompletions = Array.AsReadOnly(completions != null ? new List<DRPGWeaponCompletionMutation>(completions).ToArray() : Array.Empty<DRPGWeaponCompletionMutation>());
        WeaponProgressReconciliations = Array.AsReadOnly(progressReconciliations != null ? new List<DRPGWeaponProgressReconciliation>(progressReconciliations).ToArray() : Array.Empty<DRPGWeaponProgressReconciliation>());
        ResonanceMutations = Array.AsReadOnly(resonanceMutations != null ? new List<DRPGActionResonanceMutation>(resonanceMutations).ToArray() : Array.Empty<DRPGActionResonanceMutation>());
    }
    internal DRPGBattleActionResourceSnapshot Snapshot { get; }
    internal ActionIdentity Identity { get; }
    internal IReadOnlyList<DRPGProgressMutationResult> ProgressMutations { get; }
    internal IReadOnlyList<DRPGBattleWeaponUseMutation> WeaponUses { get; }
    internal IReadOnlyList<DRPGBattleWeaponUseEvent> WeaponEvents { get; }
    internal IReadOnlyList<DRPGWeaponCompletionMutation> WeaponCompletions { get; }
    internal IReadOnlyList<DRPGWeaponProgressReconciliation> WeaponProgressReconciliations { get; }
    internal IReadOnlyList<DRPGActionResonanceMutation> ResonanceMutations { get; }
    internal bool HasChanges => ProgressMutations.Count > 0 || WeaponUses.Count > 0 || WeaponCompletions.Count > 0 || WeaponEvents.Count > 0 || WeaponProgressReconciliations.Count > 0 || ResonanceMutations.Count > 0;
}

internal sealed partial class DRPGBattleActionResourceWorkspace
{
    private readonly DRPGBattleActionResourceSnapshot snapshot;
    private readonly DRPGBattleMutationWorkspace owner;
    private DRPGBattleProgressLedger progress;
    private ActionIdentity identity;
    private readonly List<DRPGProgressMutationResult> progressMutations = new List<DRPGProgressMutationResult>();
    private readonly Dictionary<(string, string, string, string), DRPGProgressMutationResult> progressKeys =
        new Dictionary<(string, string, string, string), DRPGProgressMutationResult>();
    private readonly Dictionary<(string, string), int> weaponEvents = new Dictionary<(string, string), int>();
    private readonly Dictionary<(string, string), DRPGWeaponUseKind?> weaponKinds = new Dictionary<(string, string), DRPGWeaponUseKind?>();
    private readonly Dictionary<string, DRPGWeaponCompletionPlan> weaponCompletions = new Dictionary<string, DRPGWeaponCompletionPlan>(StringComparer.Ordinal);
    private readonly Dictionary<string, DRPGBattleWeaponUseMutation> weaponUses =
        new Dictionary<string, DRPGBattleWeaponUseMutation>(StringComparer.Ordinal);

    internal DRPGBattleActionResourceWorkspace(DRPGBattleActionResourceSnapshot snapshot, DRPGBattleMutationWorkspace owner = null)
    {
        this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        this.owner = owner;
        progress = DRPGBattleProgressLedger.FromSnapshots(snapshot.Progress);
    }

    internal static string WeaponActionKey(ActionIdentity identity, string weaponId) =>
        "root:" + identity + ":weapon:" + weaponId;

    internal DRPGBattleEquipmentSnapshot EquipmentSnapshot => snapshot.Equipment;
    internal DRPGActionResonanceRules ResonanceRules => snapshot.ResonanceRules;

    internal bool TryStageProgress(ActionIdentity identity, DRPGProgressMutationRequest request,
        out DRPGProgressMutationResult result, out string error)
    {
        result = null;
        if (!ValidateIdentity(identity, out error)) return false;
        if (request == null || request.Scope != DRPGProgressScope.Battle || !snapshot.IsKnownOwner(request.OwnerStableId))
        { error = "Progress requires a captured owner and Battle scope."; return false; }
        var key = (request.SourceId, request.OwnerStableId, request.ResourceId, request.DeduplicationKey);
        if (progressKeys.TryGetValue(key, out result))
        {
            if (!SameRequest(result.Request, request))
            { result = null; error = "Progress deduplication key has a conflicting request."; return false; }
            return true;
        }
        var candidate = progress.Fork();
        result = candidate.Apply(request);
        if (!result.WasApplied) { error = result.Message; return false; }
        progress = candidate;
        this.identity = identity;
        progressKeys.Add(key, result);
        progressMutations.Add(result);
        return true;
    }

    internal bool TryStageWeaponUse(ActionIdentity identity, string eventId, string weaponId,
        int units, out string error, DRPGWeaponUseKind? useKind = null)
    {
        if (!ValidateIdentity(identity, out error)) return false;
        if (string.IsNullOrWhiteSpace(eventId) || units < 0 || !snapshot.TryGetWeapon(weaponId, out var weapon) ||
            (useKind.HasValue && !Enum.IsDefined(typeof(DRPGWeaponUseKind), useKind.Value)))
        { error = "Weapon use requires a captured weapon, event identity and non-negative units."; return false; }
        var key = (weaponId, eventId);
        if (weaponEvents.TryGetValue(key, out int previous))
        {
            if (previous != units || weaponKinds[key] != useKind) { error = "Weapon event has conflicting use units or kind."; return false; }
            return true;
        }
        weaponUses.TryGetValue(weaponId, out var existing);
        long total = (long)existing.Units + units;
        if (weapon.CurrentDurability <= 0 || total > weapon.CurrentDurability)
        { error = "The captured weapon has insufficient durability."; return false; }
        int after = weapon.CurrentDurability - (int)total;
        if (after == 0 && weapon.BreakDisposition == DRPGWeaponBreakDisposition.DestroyIntoFragments && snapshot.Equipment == null)
        { error = "Atomic ordinary weapon break requires the equipment/fragment reconciliation binding."; return false; }
        DRPGWeaponCompletionPlan completion = null;
        if (snapshot.Equipment != null)
        {
            try { completion = snapshot.Equipment.Plan(weaponId, (int)total); }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        this.identity = identity;
        weaponEvents.Add(key, units);
        weaponKinds.Add(key, useKind);
        if (completion != null) weaponCompletions[weaponId] = completion;
        weaponUses[weaponId] = new DRPGBattleWeaponUseMutation(weaponId, (int)total, after);
        return true;
    }

    internal bool TryGetProgress(string owner, string resource, out DRPGProgressSnapshot snapshot)
    {
        return progress.TryGetSnapshot(owner, resource, out snapshot);
    }

    internal DRPGBattleActionResourceWorkspace Fork(DRPGBattleMutationWorkspace nextOwner = null)
    {
        var fork = new DRPGBattleActionResourceWorkspace(snapshot, nextOwner) { identity = identity, progress = progress.Fork() };
        fork.progressMutations.AddRange(progressMutations);
        foreach (var pair in progressKeys) fork.progressKeys.Add(pair.Key, pair.Value);
        foreach (var pair in weaponEvents) fork.weaponEvents.Add(pair.Key, pair.Value);
        foreach (var pair in weaponKinds) fork.weaponKinds.Add(pair.Key, pair.Value);
        foreach (var pair in weaponCompletions) fork.weaponCompletions.Add(pair.Key, pair.Value);
        foreach (var pair in weaponUses) fork.weaponUses.Add(pair.Key, pair.Value);
        fork.resonance = resonance != null ? new Dictionary<string, int>(resonance, StringComparer.Ordinal) : null;
        fork.resonanceMutations.AddRange(resonanceMutations);
        foreach (var pair in resonanceKeys) fork.resonanceKeys.Add(pair.Key, pair.Value);
        return fork;
    }

    internal DRPGBattleActionResourceJournal BuildJournal()
    {
        var uses = new List<DRPGBattleWeaponUseMutation>(weaponUses.Values);
        uses.Sort((left, right) => StringComparer.Ordinal.Compare(left.WeaponId, right.WeaponId));
        var events = new List<DRPGBattleWeaponUseEvent>(weaponEvents.Count);
        foreach (var pair in weaponEvents)
            events.Add(new DRPGBattleWeaponUseEvent(pair.Key.Item1, pair.Key.Item2, pair.Value, weaponKinds[pair.Key]));
        events.Sort((left, right) =>
        {
            int comparison = StringComparer.Ordinal.Compare(left.WeaponId, right.WeaponId);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.EventId, right.EventId);
        });
        var completions = new List<DRPGWeaponCompletionMutation>();
        foreach (var use in uses)
        {
            if (!weaponCompletions.TryGetValue(use.WeaponId, out var plan)) continue;
            if (owner == null) throw new InvalidOperationException("Equipment completion needs its owning mutation workspace.");
            var values = owner.For(new ParticipantId(plan.Actor.RuntimeId));
            completions.Add(new DRPGWeaponCompletionMutation(plan, values.HitPoints, values.Mana, values.Posture));
        }
        var capacity = snapshot.Equipment?.BuildProgressReconciliations(identity, completions, progress, snapshot.Progress);
        return new DRPGBattleActionResourceJournal(snapshot, identity, progressMutations, uses, events, completions, capacity, resonanceMutations);
    }

    private bool ValidateIdentity(ActionIdentity requested, out string error)
    {
        error = string.Empty;
        if (!requested.IsValid || requested.BattleGeneration != snapshot.BattleGeneration ||
            (identity.IsValid && identity != requested))
        { error = "Action resource workspace belongs to another Root Action."; return false; }
        return true;
    }

    private static bool SameRequest(DRPGProgressMutationRequest left, DRPGProgressMutationRequest right)
    {
        return left.Kind == right.Kind && left.Scope == right.Scope && left.Amount == right.Amount &&
            left.Priority == right.Priority && left.IncludeReactionDepth == right.IncludeReactionDepth &&
            ((left.Definition == null && right.Definition == null) ||
             (left.Definition != null && left.Definition.HasSameValues(right.Definition)));
    }
}

internal readonly struct DRPGBattlePreparedWeaponLoss
{
    internal DRPGBattlePreparedWeaponLoss(DRPGWeaponInstance weapon, int units) { Weapon = weapon; Units = units; }
    internal DRPGWeaponInstance Weapon { get; }
    internal int Units { get; }
}

internal sealed class DRPGBattlePreparedResourceCommit
{
    private readonly DRPGBattleProgressLedger progress;
    private readonly DRPGBattleProgressLedger preparedProgress;
    private readonly DRPGWeaponUseLedger uses;
    private readonly DRPGWeaponUseLedger preparedUses;
    private readonly IReadOnlyList<DRPGBattlePreparedWeaponLoss> losses;
    private readonly DRPGBattleStateMutationCapability mutationCapability;
    private readonly DRPGBattlePreparedEquipmentCommit equipment;
    private readonly DRPGResonanceExpeditionState.PreparedActionResources resonance;
    internal DRPGBattlePreparedResourceCommit(DRPGBattleProgressLedger progress, DRPGBattleProgressLedger preparedProgress,
        DRPGWeaponUseLedger uses, DRPGWeaponUseLedger preparedUses, IReadOnlyList<DRPGBattlePreparedWeaponLoss> losses,
        DRPGBattleStateMutationCapability mutationCapability, DRPGBattlePreparedEquipmentCommit equipment = null,
        DRPGResonanceExpeditionState.PreparedActionResources resonance = null)
    {
        this.progress = progress; this.preparedProgress = preparedProgress;
        this.uses = uses; this.preparedUses = preparedUses; this.losses = losses;
        this.mutationCapability = mutationCapability;
        this.equipment = equipment;
        this.resonance = resonance;
    }
    internal void Apply(DRPGBattleStateMutationCapability capability = null)
    {
        if (mutationCapability == null || !ReferenceEquals(mutationCapability, capability))
            throw new InvalidOperationException("Prepared action resources require their State's private mutation capability.");
        if (preparedProgress != null) progress.ApplyPreparedValues(preparedProgress);
        foreach (var loss in losses) loss.Weapon.ApplyDurabilityLoss(loss.Units);
        equipment?.Apply(capability);
        if (preparedUses != null) uses.ApplyPreparedRootCompletions(preparedUses);
        resonance?.Apply(capability);
    }
}
