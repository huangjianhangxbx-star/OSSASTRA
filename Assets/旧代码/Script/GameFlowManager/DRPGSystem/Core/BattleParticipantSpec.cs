using System;

[Serializable]
public sealed class BattleParticipantSpec
{
    public BattleParticipantSpec(
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole rosterRole,
        int formationSlot,
        DRPGResolvedCombatantBuild build = null,
        DRPGFormationRow initialFormationRow = DRPGFormationRow.Front,
        DRPGFormationRow preferredFormationRow = DRPGFormationRow.Front,
        bool initiallyDeployed = true,
        int partyOrderIndex = -1,
        float? initialHitPoints = null)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        if (formationSlot < 0)
            throw new ArgumentOutOfRangeException(nameof(formationSlot));

        bool enemyRoleMatchesSide =
            rosterRole == DRPGBattleRosterRole.Enemy
                ? side == DRPGBattleSide.Enemy
                : side == DRPGBattleSide.Player;

        if (!enemyRoleMatchesSide)
        {
            throw new ArgumentException(
                $"Roster role {rosterRole} is incompatible with side {side}.");
        }

        Profile = profile;
        Side = side;
        RosterRole = rosterRole;
        FormationSlot = formationSlot;
        Build = build ?? DRPGResolvedCombatantBuild.CreateLegacy(profile);
        InitialFormationRow = initialFormationRow;
        PreferredFormationRow = preferredFormationRow;
        InitiallyDeployed = initiallyDeployed;
        PartyOrderIndex = side == DRPGBattleSide.Player
            ? (partyOrderIndex >= 0 ? partyOrderIndex : formationSlot)
            : -1;
        InitialHitPoints = initialHitPoints;
    }

    public DRPGCombatantProfileSO Profile { get; }
    public DRPGBattleSide Side { get; }
    public DRPGBattleRosterRole RosterRole { get; }
    public int FormationSlot { get; }
    public DRPGResolvedCombatantBuild Build { get; }
    public DRPGFormationRow InitialFormationRow { get; }
    public DRPGFormationRow PreferredFormationRow { get; }
    public bool InitiallyDeployed { get; }
    public int PartyOrderIndex { get; }
    public float? InitialHitPoints { get; }
}
