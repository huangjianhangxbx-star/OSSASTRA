using System;
using System.Collections.Generic;
using System.Threading;

public sealed class DRPGBattlePresentationSnapshot
{
    private DRPGBattlePresentationSnapshot(
        DRPGBattleParticipantViewModel currentActor,
        IReadOnlyList<DRPGBattleParticipantViewModel> partyMembers,
        IReadOnlyList<DRPGBattleParticipantViewModel> enemies,
        IReadOnlyList<DRPGBattleParticipantViewModel> timeline,
        IReadOnlyList<DRPGSkillSO> currentSkills,
        IReadOnlyList<DRPGBattleSkillAvailability> currentSkillAvailability,
        bool hasIncomingEnemyWarning,
        int reserveEnemyCount,
        DRPGBattlePhase phase,
        DRPGBattleOutcome outcome,
        bool isActive)
    {
        CurrentActor = currentActor;
        PartyMembers = Copy(partyMembers);
        Enemies = Copy(enemies);
        Timeline = Copy(timeline);
        CurrentSkills = Copy(currentSkills);
        CurrentSkillAvailability = Copy(currentSkillAvailability);
        HasIncomingEnemyWarning = hasIncomingEnemyWarning;
        ReserveEnemyCount = Math.Max(0, reserveEnemyCount);
        Phase = phase;
        Outcome = outcome;
        IsActive = isActive;
    }

    public DRPGBattleParticipantViewModel CurrentActor { get; }
    public IReadOnlyList<DRPGBattleParticipantViewModel> PartyMembers { get; }
    public IReadOnlyList<DRPGBattleParticipantViewModel> Enemies { get; }
    public IReadOnlyList<DRPGBattleParticipantViewModel> Timeline { get; }
    public IReadOnlyList<DRPGSkillSO> CurrentSkills { get; }
    public IReadOnlyList<DRPGBattleSkillAvailability>
        CurrentSkillAvailability { get; }
    public bool HasIncomingEnemyWarning { get; }
    public int ReserveEnemyCount { get; }
    public DRPGBattlePhase Phase { get; }
    public DRPGBattleOutcome Outcome { get; }
    public bool IsActive { get; }

    public static DRPGBattlePresentationSnapshot Capture(
        DRPGBattleSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        var model = new DRPGBattleViewModel(session);
        return new DRPGBattlePresentationSnapshot(
            model.CurrentActor,
            model.PartyMembers,
            model.Enemies,
            model.Timeline,
            model.CurrentSkills,
            CaptureSkillAvailability(session, model.CurrentSkills),
            model.HasIncomingEnemyWarning,
            model.ReserveEnemyCount,
            session.Phase,
            session.Outcome,
            session.IsActive);
    }

    private static IReadOnlyList<DRPGBattleSkillAvailability>
        CaptureSkillAvailability(
            DRPGBattleSession session,
            IReadOnlyList<DRPGSkillSO> skills)
    {
        if (skills == null || skills.Count == 0)
            return Array.Empty<DRPGBattleSkillAvailability>();
        var result = new DRPGBattleSkillAvailability[skills.Count];
        for (int i = 0; i < skills.Count; i++)
            result[i] = session.GetSkillAvailability(skills[i]);
        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<T>();
        var copy = new T[values.Count];
        for (int i = 0; i < values.Count; i++)
            copy[i] = values[i];
        return Array.AsReadOnly(copy);
    }
}

public static class BattlePresentationRequestIds
{
    private static long nextRequestId;

    public static long Next()
    {
        long value = Interlocked.Increment(ref nextRequestId);
        if (value > 0L)
            return value;

        Interlocked.Exchange(ref nextRequestId, 1L);
        return 1L;
    }
}
