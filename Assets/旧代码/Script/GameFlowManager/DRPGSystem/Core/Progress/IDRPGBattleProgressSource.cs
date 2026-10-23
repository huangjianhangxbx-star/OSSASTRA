using System.Collections.Generic;

public interface IDRPGBattleProgressSource
{
    string SourceId { get; }

    void Collect(
        DRPGBattleProgressContext context,
        ICollection<DRPGProgressMutationRequest> requests);
}
