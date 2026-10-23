using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

internal readonly struct HitOutcomeBeatId : IEquatable<HitOutcomeBeatId>
{
    internal HitOutcomeBeatId(ActionIdentity action, int impactOrdinal)
    { Action = action; ImpactOrdinal = impactOrdinal; }
    internal ActionIdentity Action { get; }
    internal int ImpactOrdinal { get; }
    public bool Equals(HitOutcomeBeatId other) => Action == other.Action && ImpactOrdinal == other.ImpactOrdinal;
    public override bool Equals(object obj) => obj is HitOutcomeBeatId other && Equals(other);
    public override int GetHashCode() => unchecked(Action.GetHashCode() * 397 ^ ImpactOrdinal);
}

internal readonly struct ConflictMotionCommand
{
    internal ConflictMotionCommand(ActionIdentity action, ParticipantId actor, ParticipantId target,
        Vector3 actorPosition, Vector3 targetPosition, double issuedAt)
    { Action = action; Actor = actor; Target = target; ActorPosition = actorPosition; TargetPosition = targetPosition; IssuedAt = issuedAt; }
    internal ActionIdentity Action { get; }
    internal ParticipantId Actor { get; }
    internal ParticipantId Target { get; }
    internal Vector3 ActorPosition { get; }
    internal Vector3 TargetPosition { get; }
    internal double IssuedAt { get; }
}

internal readonly struct RecoilMotionCommand
{
    internal RecoilMotionCommand(HitOutcomeBeatId beat, ParticipantId target, int direction,
        float distance, double issuedAt, float outSeconds = .09f, float returnSeconds = .16f)
    { Beat = beat; Target = target; Direction = direction; Distance = distance; IssuedAt = issuedAt; OutSeconds = outSeconds; ReturnSeconds = returnSeconds; }
    internal HitOutcomeBeatId Beat { get; }
    internal ParticipantId Target { get; }
    internal int Direction { get; }
    internal float Distance { get; }
    internal double IssuedAt { get; }
    internal float OutSeconds { get; }
    internal float ReturnSeconds { get; }
}

internal readonly struct ReturnMotionCommand
{
    internal ReturnMotionCommand(ActionIdentity action, ParticipantId participant, double issuedAt, float seconds = .38f)
    { Action = action; Participant = participant; IssuedAt = issuedAt; Seconds = seconds; }
    internal ActionIdentity Action { get; }
    internal ParticipantId Participant { get; }
    internal double IssuedAt { get; }
    internal float Seconds { get; }
}

internal readonly struct BattleActorMotionWrite
{
    internal BattleActorMotionWrite(BattleTransactionIdentity owner, ParticipantId participant, DRPGBattleActionStage stage,
        double timestamp, long frame, long microstep, Vector3 before, Vector3 after)
    { Transaction = owner; Participant = participant; Stage = stage; Timestamp = timestamp; Frame = frame; Microstep = microstep; Before = before; After = after; }
    internal ActionIdentity Owner => Transaction.Action;
    internal BattleTransactionIdentity Transaction { get; }
    internal ParticipantId Participant { get; }
    internal DRPGBattleActionStage Stage { get; }
    internal double Timestamp { get; }
    internal long Frame { get; }
    internal long Microstep { get; }
    internal Vector3 Before { get; }
    internal Vector3 After { get; }
}

internal interface IBattleActorMotionController
{
    PresentationReceipt SnapConflict(in ConflictMotionCommand command);
    PresentationReceipt Recoil(in RecoilMotionCommand command);
    PresentationReceipt ReturnHome(in ReturnMotionCommand command);
}

internal sealed class BattleActorMotionController : IBattleActorMotionController, IDisposable
{
    private sealed class Run
    {
        internal int RuntimeId;
        internal Sequence Tween;
        internal PresentationReceipt Receipt;
        internal double StartedAt;
        internal long StartedFrame;
        internal float OutSeconds, Duration;
        internal bool VisitedPeak;
        internal bool Dodge;
        internal BattleActorVisualRestore.Transition VisualReturn;
    }
    private readonly DRPGBattleActionProjectionLease lease;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly List<BattleActorMotionWrite> journal = new List<BattleActorMotionWrite>();
    private readonly Dictionary<int, Vector3> conflicts = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
    private readonly HashSet<HitOutcomeBeatId> beats = new HashSet<HitOutcomeBeatId>();
    private readonly HashSet<int> displaced = new HashSet<int>();
    private readonly Dictionary<int, bool> enemySides = new Dictionary<int, bool>();
    private readonly Dictionary<int, float> widths = new Dictionary<int, float>();
    private readonly HashSet<DRPGReactionLedgerEntry> joinedReactions = new HashSet<DRPGReactionLedgerEntry>();
    private long microstep;
    private bool disposed, composed, returning;

    internal BattleActorMotionController(DRPGBattleActionProjectionLease lease, IDRPGBattlePresentationClock clock)
    {
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (!lease.TryClaimMotion(this))
            throw new InvalidOperationException("The projection already has a tactical motion owner or is closed.");
        foreach (var pin in lease.LockBaseline.Actors)
        {
            lease.TryGetView(pin.RuntimeId, out var view);
            enemySides.Add(pin.RuntimeId, view.BoundActor.IsEnemy);
            widths.Add(pin.RuntimeId, Mathf.Max(.1f, view.BoundActor.FormationVisualWidth * Mathf.Abs(view.transform.lossyScale.x)));
        }
    }
    internal IReadOnlyList<BattleActorMotionWrite> Journal => journal.AsReadOnly();
    internal IReadOnlyList<ParticipantId> DisplacedParticipants
    {
        get
        {
            var ids = new List<int>(displaced); ids.Sort();
            var participants = new List<ParticipantId>();
            foreach (int id in ids) participants.Add(new ParticipantId(id));
            return participants.AsReadOnly();
        }
    }

    public PresentationReceipt SnapConflict(in ConflictMotionCommand command)
    {
        var receipt = new PresentationReceipt(command.Action, DRPGBattleActionStage.ConflictComposition);
        if (!Owns(command.Action) || composed || command.Actor == command.Target ||
            !Finite(command.ActorPosition) || !Finite(command.TargetPosition) ||
            !lease.TryGetView(command.Actor.Value, out _) || !lease.TryGetView(command.Target.Value, out _))
            return Fault(receipt, "Conflict requires two pinned actors and unused motion ownership.");
        composed = true;
        conflicts.Add(command.Actor.Value, command.ActorPosition);
        conflicts.Add(command.Target.Value, command.TargetPosition);
        Write(command.Actor.Value, command.ActorPosition, DRPGBattleActionStage.ConflictComposition);
        Write(command.Target.Value, command.TargetPosition, DRPGBattleActionStage.ConflictComposition);
        receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Conflict batch snapped."));
        return receipt;
    }

    public PresentationReceipt Recoil(in RecoilMotionCommand command)
        => PlayHorizontal(command, false);

    internal PresentationReceipt Dodge(HitOutcomeBeatId beat, ParticipantId target, int direction, double issuedAt)
        => PlayHorizontal(new RecoilMotionCommand(beat, target, direction, .65f, issuedAt, .12f, .18f), true);

    private PresentationReceipt PlayHorizontal(in RecoilMotionCommand command, bool dodge)
    {
        var receipt = new PresentationReceipt(command.Beat.Action, DRPGBattleActionStage.ImpactAndReactions,
            command.Beat.ImpactOrdinal);
        if (!Owns(command.Beat.Action) || runs.ContainsKey(command.Target.Value) ||
            !conflicts.TryGetValue(command.Target.Value, out var origin) || beats.Contains(command.Beat) ||
            Math.Abs(command.Direction) != 1 || !Finite(command.Distance) || command.Distance <= 0f ||
            !Finite(command.OutSeconds) || !Finite(command.ReturnSeconds) ||
            command.OutSeconds < .09f || command.ReturnSeconds < .16f)
            return Fault(receipt, "Recoil requires an unused beat and a pinned conflict position.");
        beats.Add(command.Beat);
        int runtimeId = command.Target.Value;
        Vector3 peak = origin + Vector3.right * (command.Direction * command.Distance);
        var run = new Run { RuntimeId = runtimeId, Receipt = receipt, StartedAt = command.IssuedAt,
            StartedFrame = clock.RenderedFrame, OutSeconds = command.OutSeconds,
            Duration = command.OutSeconds + command.ReturnSeconds, Dodge = dodge };
        // DOTween owns interpolation. A private paused sequence is advanced by
        // this action's clock only; no global ManualUpdate or transform DOKill.
        run.Tween = DOTween.Sequence().SetAutoKill(false).Pause()
            .Append(DOTween.To(() => 0f, progress => Write(runtimeId,
                Vector3.LerpUnclamped(origin, peak, progress), DRPGBattleActionStage.ImpactAndReactions),
                1f, command.OutSeconds).SetEase(Ease.InOutSine))
            .Append(DOTween.To(() => 0f, progress => Write(runtimeId,
                Vector3.LerpUnclamped(peak, origin, progress), DRPGBattleActionStage.ImpactAndReactions),
                1f, command.ReturnSeconds).SetEase(Ease.InOutSine));
        runs.Add(runtimeId, run);
        return receipt;
    }

    // Only the ledger-bound outcome presenter calls this after validating its
    // current cursor. Existing conflict participants never jump to new seats.
    internal bool StageReaction(DRPGReactionLedgerEntry reaction, double issuedAt)
    {
        if (reaction == null || !Owns(reaction.Identity) || !composed || returning ||
            !reaction.RequiresPresentation || reaction.Batch.Hits.Count != 1 ||
            double.IsNaN(issuedAt) || double.IsInfinity(issuedAt) || issuedAt != clock.Now)
            return false;
        if (joinedReactions.Contains(reaction)) return true;
        var ids = new List<int>();
        if (reaction.Category == DRPGImmediateReactionCategory.Counterattack || reaction.Category == DRPGImmediateReactionCategory.Pursuit)
            ids.Add(reaction.SourceParticipant.Value);
        int target = reaction.TargetParticipant.Value;
        if (!ids.Contains(target)) ids.Add(target);
        var planned = new Dictionary<int, Vector3>();
        foreach (int id in ids)
        {
            if (!lease.TryGetView(id, out _) || runs.ContainsKey(id)) return false;
            if (conflicts.ContainsKey(id)) continue;
            if (!TryPlanReactionSeat(id, planned, out var position)) return false;
            planned.Add(id, position);
        }
        // Preflight the entire pair before the first write; no partial grant.
        foreach (var pair in planned) conflicts.Add(pair.Key, pair.Value);
        foreach (var pair in planned) Write(pair.Key, pair.Value, DRPGBattleActionStage.ImpactAndReactions);
        joinedReactions.Add(reaction);
        return true;
    }

    private bool TryPlanReactionSeat(int id, Dictionary<int, Vector3> planned, out Vector3 position)
    {
        position = default;
        bool found = false;
        // The side axis comes from the actual composed pair, not player/enemy
        // branch timing. Further participants occupy distinct outward seats.
        Vector3 sameSide = default, opposite = default;
        bool oppositeFound = false;
        foreach (var pair in conflicts)
            if (enemySides[pair.Key] == enemySides[id]) { sameSide = pair.Value; found = true; }
            else { opposite = pair.Value; oppositeFound = true; }
        if (!found || !oppositeFound || Mathf.Abs(sameSide.x - opposite.x) < .001f) return false;
        int direction = sameSide.x > opposite.x ? 1 : -1;
        position = sameSide;
        var occupied = new Dictionary<int, Vector3>();
        foreach (var pin in lease.LockBaseline.Actors)
        {
            if (pin.RuntimeId == id) continue;
            if (!lease.TryGetView(pin.RuntimeId, out var view)) return false;
            occupied.Add(pin.RuntimeId, planned.TryGetValue(pin.RuntimeId, out var pending) ? pending : view.MotionRoot.position);
        }
        // Interval packing is bounded by the frozen field. The gap also leaves
        // room for hit recoil and the wider evade without touching its neighbor.
        for (int attempt = 0; attempt <= occupied.Count; attempt++)
        {
            bool clear = true;
            foreach (var pair in occupied)
            {
                float separation = (widths[id] + widths[pair.Key]) * .5f + .8f;
                if (Mathf.Abs(position.y - pair.Value.y) > .01f || Mathf.Abs(position.z - pair.Value.z) > .01f ||
                    Mathf.Abs(position.x - pair.Value.x) >= separation) continue;
                // Keep a small positive clearance so float subtraction cannot
                // classify the exact packed boundary as a collision forever.
                position.x = pair.Value.x + direction * (separation + .001f);
                clear = false;
            }
            if (clear) return Finite(position);
        }
        return false;
    }

    public PresentationReceipt ReturnHome(in ReturnMotionCommand command)
    {
        var receipt = new PresentationReceipt(command.Action, DRPGBattleActionStage.Return);
        if (!Owns(command.Action) || runs.ContainsKey(command.Participant.Value) ||
            !lease.TryGetView(command.Participant.Value, out var view) ||
            !Finite(command.Seconds) || command.Seconds <= 0f)
            return Fault(receipt, "Return requires an idle pinned motion root and its approved minimum.");
        BattleLeasedActorCheckpoint home = null;
        foreach (var actor in lease.LockBaseline.Actors)
            if (actor.RuntimeId == command.Participant.Value) { home = actor; break; }
        if (home == null) return Fault(receipt, "Frozen home is missing.");
        returning = true;
        int runtimeId = command.Participant.Value;
        Vector3 origin = view.MotionRoot.position, destination = home.Home.Position;
        Vector3 rootScale = view.MotionRoot.localScale;
        Quaternion rootRotation = view.MotionRoot.rotation;
        var visualReturn = lease.BeginVisualReturn(this, runtimeId);
        if (visualReturn == null || !visualReturn.IsValid) return Fault(receipt, "Frozen visual bindings are unavailable.");
        var run = new Run { RuntimeId = runtimeId, Receipt = receipt, StartedAt = command.IssuedAt,
            StartedFrame = clock.RenderedFrame, Duration = command.Seconds, VisitedPeak = true, VisualReturn = visualReturn };
        run.Tween = DOTween.Sequence().SetAutoKill(false).Pause()
            .Append(DOTween.To(() => 0f, progress =>
            {
                if (!Owns(lease.Owner.Action) || view == null || !visualReturn.IsValid) return;
                // Select endpoints explicitly; no second completion restore.
                Write(runtimeId, progress >= 1f ? destination : Vector3.LerpUnclamped(origin, destination, progress), DRPGBattleActionStage.Return);
                view.MotionRoot.rotation = progress >= 1f ? home.Home.Rotation : Quaternion.SlerpUnclamped(rootRotation, home.Home.Rotation, progress);
                view.MotionRoot.localScale = progress >= 1f ? home.Home.LocalScale : Vector3.LerpUnclamped(rootScale, home.Home.LocalScale, progress);
                visualReturn.Apply(progress);
            }, 1f, command.Seconds).SetEase(Ease.InOutSine));
        runs.Add(runtimeId, run);
        return receipt;
    }
    internal PresentationReceipt MoveFormation(ActionIdentity action, DRPGWorldFormationPlacement target, double issuedAt,
        DRPGBattleActionStage stage = DRPGBattleActionStage.Return, int stepOrdinal = 2)
        => MoveFormation(BattleTransactionIdentity.ForRootAction(action), target, issuedAt, stage, stepOrdinal);

    internal PresentationReceipt MoveFormation(BattleTransactionIdentity owner, DRPGWorldFormationPlacement target, double issuedAt,
        DRPGBattleActionStage stage, int stepOrdinal = 2)
    {
        var receipt = new PresentationReceipt(owner, stage, stepOrdinal);
        bool validStage = owner.Kind == BattleTransactionKind.TurnEntry ? stage == DRPGBattleActionStage.TurnBegin :
            owner.Kind == BattleTransactionKind.RootAction && (stage == DRPGBattleActionStage.Return || stage == DRPGBattleActionStage.Settlement);
        if (!Owns(owner) || runs.ContainsKey(target.RuntimeId) || !lease.TryGetView(target.RuntimeId, out var view) ||
            !Finite(target.WorldPosition) || !Finite(target.UniformScale) || target.UniformScale <= 0f ||
            !validStage ||
            double.IsNaN(issuedAt) || double.IsInfinity(issuedAt) || issuedAt != clock.Now)
            return Fault(receipt, "Formation requires its idle pinned motion owner and current batch timestamp.");
        Vector3 origin = view.transform.position, scale = view.transform.localScale;
        var run = new Run { RuntimeId = target.RuntimeId, Receipt = receipt, StartedAt = issuedAt,
            StartedFrame = clock.RenderedFrame, Duration = .30f, VisitedPeak = true };
        run.Tween = DOTween.Sequence().SetAutoKill(false).Pause()
            .Append(DOTween.To(() => 0f, progress =>
            {
                if (!Owns(owner) || view == null) return;
                Write(target.RuntimeId, progress >= 1f ? target.WorldPosition : Vector3.LerpUnclamped(origin, target.WorldPosition, progress), stage, formation: true);
                view.transform.localScale = progress >= 1f ? Vector3.one * target.UniformScale : Vector3.LerpUnclamped(scale, Vector3.one * target.UniformScale, progress);
                if (progress >= 1f) ApplyFormationSorting(view, target.SortingOrder);
            }, 1f, .30f).SetEase(Ease.InOutSine));
        runs.Add(target.RuntimeId, run); return receipt;
    }

    internal void SealFormationHomes(IReadOnlyList<DRPGWorldFormationPlacement> targets)
    {
        if (!Owns(lease.Owner)) throw new InvalidOperationException("Formation motion owner lost.");
        foreach (var target in targets)
            if (!lease.TryGetView(target.RuntimeId, out var view) || runs.ContainsKey(target.RuntimeId) ||
                view.transform.position != target.WorldPosition || view.transform.localScale != Vector3.one * target.UniformScale)
                throw new InvalidOperationException("Formation endpoints are not complete.");
        foreach (var target in targets)
        {
            lease.TryGetView(target.RuntimeId, out var view);
            ApplyFormationSorting(view, target.SortingOrder);
            lease.UpdateFormationHome(this, target.RuntimeId);
        }
    }
    private static void ApplyFormationSorting(BattleWorldActorView view, int order)
    {
        var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = order + i;
    }
    internal void CancelOwnedReceipt(PresentationReceipt receipt)
    {
        foreach (var run in new List<Run>(runs.Values))
            if (ReferenceEquals(run.Receipt, receipt))
            { run.Tween.Kill(false); runs.Remove(run.RuntimeId); Fault(receipt, "Owned formation motion cancelled."); return; }
    }

    internal void Tick()
    {
        if (disposed) return;
        if (!lease.IsActive) { Dispose(); return; }
        foreach (var run in new List<Run>(runs.Values))
        {
            if (run.VisualReturn != null && !run.VisualReturn.IsValid)
            { CancelOwnedReceipt(run.Receipt); continue; }
            float elapsed = (float)Math.Max(0d, clock.Now - run.StartedAt);
            if (!run.VisitedPeak && elapsed >= run.OutSeconds)
            {
                // Retain the exact reversal microstep even on a coarse frame.
                run.Tween.Goto(run.OutSeconds, false);
                run.VisitedPeak = true;
            }
            run.Tween.Goto(Math.Min(elapsed, run.Duration), false);
            if (disposed || !lease.IsActive) return;
            if (elapsed >= run.Duration && clock.RenderedFrame > run.StartedFrame)
            {
                run.Tween.Kill(false);
                runs.Remove(run.RuntimeId);
                run.Receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed,
                    run.Receipt.Stage == DRPGBattleActionStage.Return ?
                        "Motion returned to frozen home." : run.Dodge ? "Horizontal evade returned to conflict." : "Horizontal recoil returned to conflict."));
            }
        }
    }

    private bool Owns(ActionIdentity action) => action.IsValid && lease.Owner.Kind == BattleTransactionKind.RootAction &&
        Owns(BattleTransactionIdentity.ForRootAction(action));
    private bool Owns(BattleTransactionIdentity owner) => owner.IsValid && !disposed && lease.OwnsMotion(this) && lease.Owner == owner;
    private void Write(int runtimeId, Vector3 position, DRPGBattleActionStage stage, bool formation = false)
    {
        if (!Owns(lease.Owner) || !lease.TryGetView(runtimeId, out var view)) return;
        var root = formation ? view.transform : view.MotionRoot;
        Vector3 before = root.position;
        if (before.Equals(position)) return;
        root.position = position;
        if (stage != DRPGBattleActionStage.Return && stage != DRPGBattleActionStage.Settlement && stage != DRPGBattleActionStage.TurnBegin) displaced.Add(runtimeId);
        journal.Add(new BattleActorMotionWrite(lease.Owner, new ParticipantId(runtimeId), stage,
            clock.Now, clock.RenderedFrame, ++microstep, before, position));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var run in runs.Values)
        {
            run.Tween.Kill(false);
            Fault(run.Receipt, "Owned motion was cancelled.");
        }
        runs.Clear();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static PresentationReceipt Fault(PresentationReceipt receipt, string reason)
    {
        receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, reason));
        return receipt;
    }
}
