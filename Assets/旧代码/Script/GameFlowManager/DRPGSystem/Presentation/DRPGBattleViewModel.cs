using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattleParticipantViewModel
{
    public DRPGBattleParticipantViewModel(
        BattleParticipantRuntime participant,
        float scheduledTime,
        bool isCurrentActor,
        DRPGEnemyActionPlanSnapshot? enemyPlan = null)
    {
        Participant = participant ?? throw new ArgumentNullException(nameof(participant));
        Profile = participant.Profile;
        RuntimeId = participant.RuntimeId;
        DisplayName = Profile.DisplayName;
        FullPortrait = Profile.FullPortrait;
        AttackCutInPortrait = Profile.AttackCutInPortrait;
        PortraitFocus = Profile.PortraitFocus;
        PortraitScale = Profile.PortraitScale;
        AvatarZoom = Profile.AvatarZoom;
        CurrentHp = participant.CurrentHp;
        MaxHp = participant.EffectiveMaxHp;
        ProjectionBaseline = new BattleProjectedParticipant(RuntimeId, CurrentHp, MaxHp,
            participant.CurrentPosture, participant.EffectiveMaxPosture, participant.Statuses.Snapshot());
        LowHpState = participant.LowHpState;
        IsAlive = participant.IsAlive;
        IsEnemy = participant.Side == DRPGBattleSide.Enemy;
        ScheduledTime = scheduledTime;
        IsCurrentActor = isCurrentActor;
        FormationRow = participant.FormationRow;
        PreferredFormationRow = participant.PreferredFormationRow;
        FormationVisualWidth = Profile.FormationVisualWidth;
        FormationSlot = participant.FormationSlot;
        PartyOrderIndex = participant.PartyOrderIndex;
        PostureBreakState = participant.PostureBreak.State;
        IsPostureBreakExposed = participant.PostureBreak.IsExposed;
        PostureBreakIncomingDamageMultiplier =
            participant.PostureBreak.IncomingDamageMultiplier;
        PostureBreakIncomingAilmentMultiplier =
            participant.PostureBreak.IncomingAilmentBuildupMultiplier;
        PostureBreakIncomingPostureDamageMultiplier =
            participant.PostureBreak.IncomingPostureDamageMultiplier;
        PostureBreakStabilityOwnActionsRemaining =
            participant.PostureBreak.RemainingStabilityOwnActions;
        if (enemyPlan.HasValue)
        {
            DRPGEnemyActionPlanSnapshot snapshot = enemyPlan.Value;
            EnemyWarningValue = snapshot.WarningValue;
            HasEnemyWarning = snapshot.HasEnemyWarning;
            IsEnemyAttackReady = snapshot.IsEnemyAttackReady;
            DisclosedTargetRuntimeId = snapshot.DisclosedTargetRuntimeId;
            Intent = snapshot.HasEnemyWarning
                ? new BattleIntentSnapshot(
                    snapshot.SkillId,
                    string.IsNullOrWhiteSpace(snapshot.SkillDisplayName)
                        ? "攻击"
                        : snapshot.SkillDisplayName,
                    snapshot.SkillIcon,
                    snapshot.Attribute.ToString(),
                    snapshot.Panel,
                    snapshot.WarningValue,
                    snapshot.DisclosedTargetRuntimeId > 0
                        ? snapshot.DisclosedTargetRuntimeId
                        : (int?)null)
                : (BattleIntentSnapshot?)null;
        }
    }

    public BattleParticipantRuntime Participant { get; }
    public DRPGCombatantProfileSO Profile { get; }
    public int RuntimeId { get; }
    public string DisplayName { get; }
    public Sprite FullPortrait { get; }
    public Sprite AttackCutInPortrait { get; }
    public Sprite ResolvePortrait(DRPGBattlePortraitState state) =>
        Profile.ResolvePortrait(state);
    public Vector2 PortraitFocus { get; }
    public float PortraitScale { get; }
    public float AvatarZoom { get; }
    public float CurrentHp { get; }
    public float MaxHp { get; }
    internal BattleProjectedParticipant ProjectionBaseline { get; }
    public DRPGLowHpPresentationState LowHpState { get; }
    public bool IsAlive { get; }
    public float HpRatio => MaxHp > 0f
        ? CurrentHp / MaxHp
        : 0f;
    public float ScheduledTime { get; }
    public bool IsCurrentActor { get; }
    public bool IsEnemy { get; }
    public DRPGFormationRow FormationRow { get; }
    public DRPGFormationRow PreferredFormationRow { get; }
    public float FormationVisualWidth { get; }
    public int FormationSlot { get; }
    public int PartyOrderIndex { get; }
    public DRPGPostureBreakState PostureBreakState { get; }
    public bool IsPostureBreakExposed { get; }
    public float PostureBreakIncomingDamageMultiplier { get; }
    public float PostureBreakIncomingAilmentMultiplier { get; }
    public float PostureBreakIncomingPostureDamageMultiplier { get; }
    public int PostureBreakStabilityOwnActionsRemaining { get; }
    public int EnemyWarningValue { get; }
    public bool HasEnemyWarning { get; }
    public bool IsEnemyAttackReady { get; }
    public int DisclosedTargetRuntimeId { get; }
    public BattleIntentSnapshot? Intent { get; }
}

public sealed class DRPGBattleViewModel
{
    private readonly DRPGBattleSession session;

    public DRPGBattleViewModel(DRPGBattleSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public DRPGBattleParticipantViewModel CurrentActor =>
        session.CurrentActor != null
            ? new DRPGBattleParticipantViewModel(
                session.CurrentActor,
                session.Timeline.Count > 0
                    ? session.Timeline[0].ScheduledTime
                    : 0f,
                true,
                FindEnemyPlan(session.CurrentActor.RuntimeId))
            : null;

    public IReadOnlyList<DRPGBattleParticipantViewModel> PartyMembers
    {
        get
        {
            var result = new List<DRPGBattleParticipantViewModel>();
            IReadOnlyList<BattleParticipantRuntime> participants = session.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].AppearsInPartyHud)
                {
                    result.Add(new DRPGBattleParticipantViewModel(
                        participants[i],
                        FindScheduledTime(participants[i]),
                        ReferenceEquals(participants[i], session.CurrentActor)));
                }
            }

            string resonancePartnerId = session.ResonanceAccess != null
                ? session.ResonanceAccess.SelectedPartnerCharacterId
                : string.Empty;
            result.Sort((left, right) => ComparePartyPresentationOrder(
                left,
                right,
                resonancePartnerId));

            return result;
        }
    }

    public IReadOnlyList<DRPGBattleParticipantViewModel> Enemies
    {
        get
        {
            var result = new List<DRPGBattleParticipantViewModel>();
            IReadOnlyList<BattleParticipantRuntime> participants = session.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipantRuntime participant = participants[i];
                if (participant.Side == DRPGBattleSide.Enemy &&
                    participant.IsAlive && participant.IsDeployed)
                {
                    result.Add(new DRPGBattleParticipantViewModel(
                        participant,
                        FindScheduledTime(participant),
                        ReferenceEquals(participant, session.CurrentActor),
                        FindEnemyPlan(participant.RuntimeId)));
                }
            }

            return result;
        }
    }

    public IReadOnlyList<DRPGBattleParticipantViewModel> Timeline
    {
        get
        {
            var result = new List<DRPGBattleParticipantViewModel>();
            var seen = new HashSet<int>();
            if (session.CurrentActor != null && session.CurrentActor.IsAlive)
            {
                result.Add(new DRPGBattleParticipantViewModel(
                    session.CurrentActor,
                    0f,
                    true,
                    FindEnemyPlan(session.CurrentActor.RuntimeId)));
                seen.Add(session.CurrentActor.RuntimeId);
            }

            IReadOnlyList<DRPGEnemyActionPlanSnapshot> readyPlans =
                session.ReadyEnemyPlans;
            for (int i = 0; i < readyPlans.Count; i++)
            {
                BattleParticipantRuntime enemy = FindParticipant(
                    readyPlans[i].ActorRuntimeId);
                if (enemy == null || !enemy.IsAlive ||
                    !seen.Add(enemy.RuntimeId))
                {
                    continue;
                }
                result.Add(new DRPGBattleParticipantViewModel(
                    enemy,
                    0f,
                    false,
                    readyPlans[i]));
            }

            IReadOnlyList<DRPGTurnScheduleEntry> timeline = session.Timeline;
            for (int i = 0; i < timeline.Count; i++)
            {
                BattleParticipantRuntime participant = timeline[i].Participant;
                if (participant == null || !participant.IsAlive ||
                    participant.Side != DRPGBattleSide.Player ||
                    !seen.Add(participant.RuntimeId))
                {
                    continue;
                }
                result.Add(new DRPGBattleParticipantViewModel(
                    participant,
                    timeline[i].ScheduledTime,
                    false));
            }

            return result;
        }
    }

    public IReadOnlyList<DRPGSkillSO> CurrentSkills =>
        session.CurrentActor != null
            ? session.CurrentActor.Build.BattleSkills
            : Array.Empty<DRPGSkillSO>();

    public DRPGSkillSO CurrentBasicAttack =>
        session.CurrentActor != null
            ? session.CurrentActor.Build.BasicAttack
            : null;

    public bool HasIncomingEnemyWarning => session.HasIncomingEnemyWarning;
    public int ReserveEnemyCount => session.ReserveEnemyCount;

    private static int ComparePartyPresentationOrder(
        DRPGBattleParticipantViewModel left,
        DRPGBattleParticipantViewModel right,
        string resonancePartnerId)
    {
        int leftRank = GetPartyPresentationRank(left, resonancePartnerId);
        int rightRank = GetPartyPresentationRank(right, resonancePartnerId);
        int compared = leftRank.CompareTo(rightRank);
        return compared != 0
            ? compared
            : left.PartyOrderIndex.CompareTo(right.PartyOrderIndex);
    }

    private static int GetPartyPresentationRank(
        DRPGBattleParticipantViewModel member,
        string resonancePartnerId)
    {
        if (member.Participant.RosterRole ==
            DRPGBattleRosterRole.Protagonist)
        {
            return 0;
        }
        if (!string.IsNullOrWhiteSpace(resonancePartnerId) &&
            member.Profile != null &&
            string.Equals(
                member.Profile.CombatantId,
                resonancePartnerId,
                StringComparison.Ordinal))
        {
            return 1;
        }
        return 10 + Mathf.Max(0, member.PartyOrderIndex);
    }

    private float FindScheduledTime(BattleParticipantRuntime participant)
    {
        IReadOnlyList<DRPGTurnScheduleEntry> timeline = session.Timeline;
        for (int i = 0; i < timeline.Count; i++)
        {
            if (ReferenceEquals(timeline[i].Participant, participant))
                return timeline[i].ScheduledTime;
        }

        return 0f;
    }

    private DRPGEnemyActionPlanSnapshot? FindEnemyPlan(int runtimeId)
    {
        IReadOnlyList<DRPGEnemyActionPlanSnapshot> plans =
            session.EnemyPlans;
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].ActorRuntimeId == runtimeId)
                return plans[i];
        }
        return null;
    }

    private BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        IReadOnlyList<BattleParticipantRuntime> participants =
            session.Participants;
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].RuntimeId == runtimeId)
                return participants[i];
        }
        return null;
    }
}
