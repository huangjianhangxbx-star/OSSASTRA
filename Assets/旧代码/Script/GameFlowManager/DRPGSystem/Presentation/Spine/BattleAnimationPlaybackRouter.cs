using System;

public readonly struct AnimationPlaybackIdentity : IEquatable<AnimationPlaybackIdentity>
{
    public AnimationPlaybackIdentity(ActionIdentity action, DRPGBattleActionStage stage, int stepOrdinal,
        long playbackSerial, int expectedMarkerOrdinal)
    {
        if (!action.IsValid || stage == DRPGBattleActionStage.None || stepOrdinal < 0 || playbackSerial <= 0 || expectedMarkerOrdinal < 0)
            throw new ArgumentException("Complete playback identity required.");
        Action = action; Stage = stage; StepOrdinal = stepOrdinal; PlaybackSerial = playbackSerial; ExpectedMarkerOrdinal = expectedMarkerOrdinal;
    }
    public ActionIdentity Action { get; }
    public DRPGBattleActionStage Stage { get; }
    public int StepOrdinal { get; }
    public long PlaybackSerial { get; }
    public int ExpectedMarkerOrdinal { get; }
    public bool IsValid => Action.IsValid && PlaybackSerial > 0;
    public bool Equals(AnimationPlaybackIdentity other) => Action == other.Action && Stage == other.Stage &&
        StepOrdinal == other.StepOrdinal && PlaybackSerial == other.PlaybackSerial && ExpectedMarkerOrdinal == other.ExpectedMarkerOrdinal;
    public override bool Equals(object obj) => obj is AnimationPlaybackIdentity other && Equals(other);
    public override int GetHashCode() { unchecked { return (((Action.GetHashCode() * 397 ^ (int)Stage) * 397 ^ StepOrdinal) * 397 ^ PlaybackSerial.GetHashCode()) * 397 ^ ExpectedMarkerOrdinal; } }
    public static bool operator ==(AnimationPlaybackIdentity a, AnimationPlaybackIdentity b) => a.Equals(b);
    public static bool operator !=(AnimationPlaybackIdentity a, AnimationPlaybackIdentity b) => !a.Equals(b);
}

public readonly struct BattleAnimationMarker
{
    public BattleAnimationMarker(AnimationPlaybackIdentity playback, string name, int ordinal)
    { Playback = playback; Name = name ?? string.Empty; Ordinal = ordinal; }
    public AnimationPlaybackIdentity Playback { get; }
    public string Name { get; }
    public int Ordinal { get; }
}

internal enum BasicAttackMarkerMode { AuthoredFallback, ExpectedMarker }

internal sealed class BattleAnimationPlaybackRouter
{
    private static long nextSerial;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly Func<AnimationPlaybackIdentity, BasicAttackWindupCommand, IDisposable> startPlayback;
    private PresentationReceipt active;
    private IDisposable playback;
    private BasicAttackMarkerMode mode;
    private double startedAt, minimum;
    private long startedFrame;
    private bool markerReceived, sealedPlayback;
    internal event Action<BattleAnimationMarker, string> MarkerObserved;
    internal BattleAnimationPlaybackRouter(IDRPGBattlePresentationClock clock,
        Func<AnimationPlaybackIdentity, BasicAttackWindupCommand, IDisposable> startPlayback)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.startPlayback = startPlayback ?? throw new ArgumentNullException(nameof(startPlayback));
    }
    internal AnimationPlaybackIdentity Current { get; private set; }
    internal string LastMarkerDisposition { get; private set; }
    internal PresentationReceipt Play(in BasicAttackWindupCommand command, BasicAttackMarkerMode mode = BasicAttackMarkerMode.AuthoredFallback, int ordinal = 0)
    {
        var receipt = new PresentationReceipt(command.Identity, DRPGBattleActionStage.Windup);
        if (active != null && !active.Completion.IsCompleted && !sealedPlayback)
        { receipt.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Playback already owned.")); return receipt; }
        Current = new AnimationPlaybackIdentity(command.Identity, DRPGBattleActionStage.Windup, 0,
            System.Threading.Interlocked.Increment(ref nextSerial), ordinal);
        active = receipt; this.mode = mode; markerReceived = false; sealedPlayback = false;
        startedAt = clock.Now; startedFrame = clock.RenderedFrame;
        minimum = Math.Max(0d, command.Snapshot.Timing.WindupMinimumSeconds);
        playback = null;
        try { playback = startPlayback(Current, command); }
        catch (Exception exception)
        { LastMarkerDisposition = "PlaybackStartFault: " + exception.Message; }
        return receipt;
    }
    internal void ReportMarker(BattleAnimationMarker marker)
    {
        if (marker.Playback != Current) Observe(marker, "stale-playback");
        else if (sealedPlayback || active == null || active.Completion.IsCompleted) Observe(marker, "late-after-seal");
        else if (mode != BasicAttackMarkerMode.ExpectedMarker || marker.Name != "Impact" || marker.Ordinal != Current.ExpectedMarkerOrdinal)
            Observe(marker, "wrong-marker");
        else if (markerReceived) Observe(marker, "duplicate-marker");
        else
        {
            markerReceived = true;
            Observe(marker, clock.Now - startedAt < minimum ? "early-marker-retained" : "marker-accepted");
        }
    }
    private void Observe(BattleAnimationMarker marker, string disposition)
    {
        LastMarkerDisposition = disposition;
        var observers = MarkerObserved;
        if (observers == null) return;
        foreach (Action<BattleAnimationMarker, string> observer in observers.GetInvocationList())
            try { observer(marker, disposition); } catch { /* Trace observers cannot drive playback. */ }
    }
    internal void Tick()
    {
        if (active == null || playback == null || active.Completion.IsCompleted || sealedPlayback || clock.Now - startedAt < minimum || clock.RenderedFrame <= startedFrame) return;
        if (mode == BasicAttackMarkerMode.ExpectedMarker && !markerReceived) return;
        try { Seal(); }
        catch (Exception exception)
        {
            active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Playback detach failed: " + exception.Message));
            return;
        }
        active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed,
            mode == BasicAttackMarkerMode.AuthoredFallback ? "AuthoredFallback" : "AnimationMarker"));
    }
    internal void Cancel(PresentationReceipt receipt, string reason)
    {
        if (!ReferenceEquals(active, receipt) || sealedPlayback) return;
        LastMarkerDisposition = reason;
        Seal();
    }
    private void Seal()
    {
        sealedPlayback = true;
        var owned = playback; playback = null;
        owned?.Dispose();
    }
}
