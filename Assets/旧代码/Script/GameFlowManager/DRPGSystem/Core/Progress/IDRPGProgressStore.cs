using System.Collections.Generic;

public interface IDRPGProgressStore
{
    DRPGProgressScope Scope { get; }

    DRPGProgressMutationResult Apply(DRPGProgressMutationRequest request);

    bool TryGetSnapshot(
        string ownerStableId,
        string resourceId,
        out DRPGProgressSnapshot snapshot);

    IReadOnlyList<DRPGProgressSnapshot> CreateSnapshot();

    void ResetAll();
}
