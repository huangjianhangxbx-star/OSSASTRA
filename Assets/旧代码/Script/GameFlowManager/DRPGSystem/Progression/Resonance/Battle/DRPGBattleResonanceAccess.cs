using System;
using System.Collections.Generic;

public sealed class DRPGBattleResonanceAccess : IDRPGBattleResonanceAccess
{
    private readonly DRPGResonanceService service;
    private readonly DRPGResonanceExpeditionState expedition;
    private readonly DRPGPartyCompositionState party;

    public DRPGBattleResonanceAccess(
        DRPGResonanceService service,
        DRPGResonanceExpeditionState expedition,
        DRPGPartyCompositionState party = null)
    {
        this.service = service ??
            throw new ArgumentNullException(nameof(service));
        this.expedition = expedition ??
            throw new ArgumentNullException(nameof(expedition));
        this.party = party;
        this.service.PartnerChanged += OnPartnerChanged;
    }

    public DRPGResonanceService Service => service;
    public DRPGResonanceExpeditionState Expedition => expedition;
    internal DRPGActionResonanceRules CaptureActionRules(IReadOnlyList<BattleParticipantRuntime> participants)
    {
        string partner = SelectedPartnerCharacterId;
        var branch = service.Definition.FindBranchByPartnerId(partner);
        var skill = service.GetEquippedActiveSkill(partner);
        var node = branch != null && skill != null ? branch.FindActiveSkillNode(skill.SkillId) : null;
        return new DRPGActionResonanceRules(this, service.State.Revision, partner, skill?.SkillId ?? string.Empty,
            node?.RequiredBattleProgress ?? 0, FindProtagonist(participants)?.RuntimeId ?? 0,
            FindParticipantByCharacterId(participants, partner)?.RuntimeId ?? 0, node?.ProgressSources);
    }
    public string SelectedPartnerCharacterId =>
        service.State.ResonancePartnerCharacterId;

    public IReadOnlyList<DRPGCombatantProfileSO> GetSelectablePartners()
    {
        if (party == null)
            return Array.Empty<DRPGCombatantProfileSO>();
        var result = new List<DRPGCombatantProfileSO>();
        IReadOnlyList<DRPGResonanceBranchSO> branches =
            service.Definition.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            DRPGCombatantProfileSO candidate = branches[i] != null
                ? branches[i].FriendProfile
                : null;
            if (candidate != null &&
                party.ContainsCharacterId(candidate.CombatantId))
            {
                result.Add(candidate);
            }
        }
        return result.AsReadOnly();
    }

    public bool TrySelectPartner(
        DRPGCombatantProfileSO partner,
        out string error)
    {
        if (party == null)
        {
            error = "Runtime party composition is unavailable.";
            return false;
        }
        DRPGResonanceOperationResult result =
            service.TrySelectPartner(partner, party);
        error = result.Success ? string.Empty : result.Message;
        return result.Success;
    }

    public IReadOnlyList<DRPGSkillSO> GetVisibleSkills(
        BattleParticipantRuntime actor,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        if (!IsProtagonist(actor))
            return Array.Empty<DRPGSkillSO>();
        string partnerId = service.State.ResonancePartnerCharacterId;
        DRPGSkillSO active = service.GetEquippedActiveSkill(partnerId);
        return active != null
            ? (IReadOnlyList<DRPGSkillSO>)new[] { active }
            : Array.Empty<DRPGSkillSO>();
    }

    public DRPGResonanceSkillAvailability GetAvailability(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        string partnerId = service.State.ResonancePartnerCharacterId;
        DRPGResonanceNodeDefinition node = FindSelectedActiveNode(skill);
        int required = node != null ? node.RequiredBattleProgress : 1;
        int current = expedition.GetProgress(
            partnerId,
            skill != null ? skill.SkillId : string.Empty);

        if (!IsProtagonist(actor))
            return Unavailable(skill, partnerId, current, required,
                "Only the protagonist can use resonance skills.");
        if (string.IsNullOrWhiteSpace(partnerId))
            return Unavailable(skill, partnerId, current, required,
                "No resonance partner is selected.");
        if (node == null)
            return Unavailable(skill, partnerId, current, required,
                "The resonance skill is not equipped for the selected partner.");

        BattleParticipantRuntime partner = FindParticipantByCharacterId(
            participants,
            partnerId);
        if (partner == null || !partner.IsDeployed)
            return Unavailable(skill, partnerId, current, required,
                "The resonance partner is not on the battlefield.");
        if (!partner.IsAlive)
            return Unavailable(skill, partnerId, current, required,
                "The resonance partner is incapacitated.");
        if (current < required)
            return Unavailable(skill, partnerId, current, required,
                "Resonance conditions are not yet complete.");

        return new DRPGResonanceSkillAvailability(
            skill,
            partnerId,
            current,
            required,
            true,
            string.Empty);
    }

    public void ObserveBattleEvents(
        IReadOnlyList<DRPGBattleEventBatch> batches,
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        if (batches == null || participants == null || batches.Count == 0)
            return;

        string partnerId = service.State.ResonancePartnerCharacterId;
        DRPGResonanceBranchSO branch =
            service.Definition.FindBranchByPartnerId(partnerId);
        DRPGSkillSO skill = service.GetEquippedActiveSkill(partnerId);
        DRPGResonanceNodeDefinition node = branch != null && skill != null
            ? branch.FindActiveSkillNode(skill.SkillId)
            : null;
        if (node == null)
            return;

        BattleParticipantRuntime protagonist = FindProtagonist(participants);
        BattleParticipantRuntime partner = FindParticipantByCharacterId(
            participants,
            partnerId);
        int protagonistRuntimeId = protagonist != null
            ? protagonist.RuntimeId
            : 0;
        int partnerRuntimeId = partner != null ? partner.RuntimeId : 0;
        IReadOnlyList<DRPGResonanceProgressSource> sources =
            node.ProgressSources;

        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            DRPGBattleEventBatch batch = batches[batchIndex];
            if (batch == null)
                continue;
            for (int eventIndex = 0;
                 eventIndex < batch.Events.Count;
                 eventIndex++)
            {
                DRPGBattleEvent battleEvent = batch.Events[eventIndex];
                for (int sourceIndex = 0;
                     sourceIndex < sources.Count;
                     sourceIndex++)
                {
                    DRPGResonanceProgressSource source = sources[sourceIndex];
                    if (source == null || !source.Matches(
                            battleEvent,
                            protagonistRuntimeId,
                            partnerRuntimeId))
                    {
                        continue;
                    }
                    expedition.AddProgress(
                        partnerId,
                        skill.SkillId,
                        source.ProgressAmount,
                        node.RequiredBattleProgress);
                }
            }
        }
    }

    public bool TryConsume(
        BattleParticipantRuntime actor,
        DRPGSkillSO skill,
        IReadOnlyList<BattleParticipantRuntime> participants,
        out string error)
    {
        DRPGResonanceSkillAvailability availability = GetAvailability(
            actor,
            skill,
            participants);
        if (!availability.IsAvailable)
        {
            error = availability.Reason;
            return false;
        }

        expedition.Consume(
            availability.PartnerCharacterId,
            skill.SkillId);
        error = string.Empty;
        return true;
    }

    private bool IsProtagonist(BattleParticipantRuntime actor)
    {
        return actor != null &&
            actor.RosterRole == DRPGBattleRosterRole.Protagonist &&
            service.Definition.IsProtagonist(actor.Profile);
    }

    private DRPGResonanceNodeDefinition FindSelectedActiveNode(
        DRPGSkillSO skill)
    {
        if (skill == null)
            return null;
        string partnerId = service.State.ResonancePartnerCharacterId;
        DRPGResonanceBranchSO branch =
            service.Definition.FindBranchByPartnerId(partnerId);
        if (branch == null)
            return null;
        DRPGSkillSO equipped = service.GetEquippedActiveSkill(partnerId);
        return equipped != null && string.Equals(
                equipped.SkillId,
                skill.SkillId,
                StringComparison.Ordinal)
            ? branch.FindActiveSkillNode(skill.SkillId)
            : null;
    }

    private BattleParticipantRuntime FindProtagonist(
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        if (participants == null)
            return null;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (IsProtagonist(participant))
                return participant;
        }
        return null;
    }

    private static BattleParticipantRuntime FindParticipantByCharacterId(
        IReadOnlyList<BattleParticipantRuntime> participants,
        string characterId)
    {
        if (participants == null || string.IsNullOrWhiteSpace(characterId))
            return null;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant != null && participant.Profile != null &&
                string.Equals(
                    participant.Profile.CombatantId,
                    characterId,
                    StringComparison.Ordinal))
            {
                return participant;
            }
        }
        return null;
    }

    private static DRPGResonanceSkillAvailability Unavailable(
        DRPGSkillSO skill,
        string partnerId,
        int current,
        int required,
        string reason)
    {
        return new DRPGResonanceSkillAvailability(
            skill,
            partnerId,
            current,
            required,
            false,
            reason);
    }

    private void OnPartnerChanged(string previousPartnerId, string nextPartnerId)
    {
        if (!string.IsNullOrWhiteSpace(previousPartnerId) &&
            !string.Equals(
                previousPartnerId,
                nextPartnerId,
                StringComparison.Ordinal))
        {
            expedition.ClearPartner(previousPartnerId);
        }
    }
}
