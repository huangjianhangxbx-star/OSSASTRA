using System;
using System.Collections.Generic;

public readonly struct BattleTimelineTransitionMove
{
    public BattleTimelineTransitionMove(
        int runtimeId,
        int fromIndex,
        int toIndex,
        bool isCompletingActor)
    {
        RuntimeId = runtimeId;
        FromIndex = fromIndex;
        ToIndex = toIndex;
        IsCompletingActor = isCompletingActor;
    }

    public int RuntimeId { get; }
    public int FromIndex { get; }
    public int ToIndex { get; }
    public bool IsCompletingActor { get; }
}

public static class BattleTimelineTransitionPresenter
{
    public static IReadOnlyList<BattleTimelineTransitionMove> BuildMoves(
        IReadOnlyList<int> oldRuntimeIds,
        IReadOnlyList<int> newRuntimeIds)
    {
        oldRuntimeIds = oldRuntimeIds ?? Array.Empty<int>();
        newRuntimeIds = newRuntimeIds ?? Array.Empty<int>();
        int completingActorId = oldRuntimeIds.Count > 0
            ? oldRuntimeIds[0]
            : 0;
        var moves = new List<BattleTimelineTransitionMove>();

        for (int newIndex = 0; newIndex < newRuntimeIds.Count; newIndex++)
        {
            int runtimeId = newRuntimeIds[newIndex];
            int oldIndex = IndexOf(oldRuntimeIds, runtimeId);
            if (oldIndex < 0)
                continue;

            moves.Add(new BattleTimelineTransitionMove(
                runtimeId,
                oldIndex,
                newIndex,
                runtimeId == completingActorId && oldIndex == 0 && newIndex != 0));
        }

        return moves;
    }

    private static int IndexOf(IReadOnlyList<int> values, int value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
                return i;
        }

        return -1;
    }
}
