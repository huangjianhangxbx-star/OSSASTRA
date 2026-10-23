using System;
using System.Collections.Generic;

/// <summary>
/// Node-local durable combat state. Battle sessions remain the only writers
/// while active; this object receives their committed result at the boundary.
/// </summary>
public sealed class DRPGNodePartyBattleState
{
    private readonly Dictionary<string, float> hitPoints =
        new Dictionary<string, float>(StringComparer.Ordinal);

    public long Revision { get; private set; }

    public bool TryGetHitPoints(string stableId, out float value) =>
        hitPoints.TryGetValue(stableId ?? string.Empty, out value);

    public void Capture(DRPGBattleSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        bool changed = false;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant == null || participant.Side != DRPGBattleSide.Player ||
                participant.Profile == null ||
                string.IsNullOrWhiteSpace(participant.Profile.CombatantId))
            {
                continue;
            }

            string id = participant.Profile.CombatantId;
            float hp = participant.CurrentHp;
            if (!hitPoints.TryGetValue(id, out float before) ||
                Math.Abs(before - hp) > 0.0001f)
            {
                hitPoints[id] = hp;
                changed = true;
            }
        }

        if (changed)
            Revision++;
    }
}
