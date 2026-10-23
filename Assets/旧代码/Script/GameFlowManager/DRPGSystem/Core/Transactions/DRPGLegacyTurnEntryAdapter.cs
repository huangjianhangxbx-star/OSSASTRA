using System;

internal sealed class DRPGLegacyTurnEntryAdapter
{
    private readonly DRPGBattleIdentityAuthority identityAuthority;

    internal DRPGLegacyTurnEntryAdapter(
        DRPGBattleIdentityAuthority identityAuthority)
    {
        this.identityAuthority = identityAuthority ??
            throw new ArgumentNullException(nameof(identityAuthority));
    }

    internal bool TryIssue(
        BattleParticipantRuntime actor,
        out ActorTurnToken token,
        out string error)
    {
        token = default;
        if (actor == null)
        {
            error = "A current actor is required before issuing a turn token.";
            return false;
        }
        if (!actor.IsAlive || !actor.IsDeployed)
        {
            error = "Only a living deployed actor can receive a turn token.";
            return false;
        }

        token = identityAuthority.AllocateTurn(
            new ParticipantId(actor.RuntimeId));
        error = string.Empty;
        return true;
    }
}
