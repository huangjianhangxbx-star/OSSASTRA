using System.Collections.Generic;

public interface IDRPGBattleResonanceAccess
{
    string SelectedPartnerCharacterId { get; }

    IReadOnlyList<DRPGCombatantProfileSO> GetSelectablePartners();

    bool TrySelectPartner(
        DRPGCombatantProfileSO partner,
        out string error);

    IReadOnlyList<DRPGSkillSO> GetVisibleSkills(
        BattleParticipantRuntime actor,
        IReadOnlyList<BattleParticipantRuntime> participants);

    DRPGResonanceSkillAvailability GetAvailability(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<BattleParticipantRuntime> participants);

    void ObserveBattleEvents(
        IReadOnlyList<DRPGBattleEventBatch> batches,
        IReadOnlyList<BattleParticipantRuntime> participants);

    bool TryConsume(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<BattleParticipantRuntime> participants,
        out string error);
}
