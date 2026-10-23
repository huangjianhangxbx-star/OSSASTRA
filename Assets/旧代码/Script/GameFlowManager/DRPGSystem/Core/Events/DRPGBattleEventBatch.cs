using System;
using System.Collections.Generic;

public sealed class DRPGBattleEventBatch
{
    public DRPGBattleEventBatch(
        long rootActionId,
        int depth,
        IReadOnlyList<DRPGBattleEvent> events)
    {
        if (rootActionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rootActionId));
        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth));
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var copy = new DRPGBattleEvent[events.Count];
        long previousEventId = 0L;
        for (int i = 0; i < events.Count; i++)
        {
            DRPGBattleEvent battleEvent = events[i];
            if (battleEvent == null)
            {
                throw new ArgumentException(
                    "Battle event batches cannot contain null.",
                    nameof(events));
            }
            if (battleEvent.RootActionId != rootActionId ||
                battleEvent.Depth != depth)
            {
                throw new ArgumentException(
                    "Every event must belong to the batch root and depth.",
                    nameof(events));
            }
            if (battleEvent.EventId <= previousEventId)
            {
                throw new ArgumentException(
                    "Battle events must be ordered by strictly increasing ID.",
                    nameof(events));
            }

            previousEventId = battleEvent.EventId;
            copy[i] = battleEvent;
        }

        RootActionId = rootActionId;
        Depth = depth;
        Events = copy.Length == 0
            ? Array.Empty<DRPGBattleEvent>()
            : Array.AsReadOnly(copy);
    }

    public long RootActionId { get; }
    public int Depth { get; }
    public IReadOnlyList<DRPGBattleEvent> Events { get; }
    public long FirstEventId => Events.Count > 0 ? Events[0].EventId : 0L;
    public long LastEventId =>
        Events.Count > 0 ? Events[Events.Count - 1].EventId : 0L;
}
