using System;
using System.Collections.Generic;

public sealed class DRPGBattleCompletionContext
{
    public DRPGBattleCompletionContext(
        DRPGBattleSession session,
        DRPGBattleStartRequest request,
        DRPGBattleResult result)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Request = request;
        Result = result;
    }

    public DRPGBattleSession Session { get; }
    public DRPGBattleStartRequest Request { get; }
    public DRPGBattleResult Result { get; }
    public IReadOnlyList<BattleParticipantRuntime> Participants =>
        Session.Participants;
}
