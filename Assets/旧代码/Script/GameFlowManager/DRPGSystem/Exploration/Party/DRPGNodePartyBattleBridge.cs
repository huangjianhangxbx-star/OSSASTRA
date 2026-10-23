using System;

/// <summary>
/// Boundary object that transfers committed party state between a node and a
/// battle session without becoming a second combat rules engine.
/// </summary>
public sealed class DRPGNodePartyBattleBridge : IDisposable
{
    private readonly DRPGNodePartyRoot party;
    private DRPGBattleSession session;

    public DRPGNodePartyBattleBridge(DRPGNodePartyRoot nodeParty)
    {
        party = nodeParty != null
            ? nodeParty
            : throw new ArgumentNullException(nameof(nodeParty));
    }

    public void Begin(DRPGBattleSession value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (session != null)
            throw new InvalidOperationException("Node battle bridge is already active.");
        session = value;
    }

    public void CaptureCommittedState()
    {
        if (session == null)
            return;
        party.CaptureCommittedBattleState(session);
    }

    public void Dispose()
    {
        session = null;
    }
}
