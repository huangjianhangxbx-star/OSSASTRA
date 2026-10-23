using System;
using System.Collections.Generic;

public enum DRPGResonanceError
{
    None = 0,
    MissingDefinition = 1,
    NotProtagonist = 2,
    PartnerUnavailable = 3,
    PartnerNotFriend = 4,
    PartnerNotInParty = 5,
    BranchNotFound = 6,
    NodeNotFound = 7,
    NodeLocked = 8,
    ConditionNotMet = 9,
    InvalidReward = 10,
    InvalidSaveData = 11,
    PartyTransactionRejected = 12
}

public sealed class DRPGResonanceOperationResult
{
    private DRPGResonanceOperationResult(
        bool success,
        DRPGResonanceError error,
        string message)
    {
        Success = success;
        Error = error;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public DRPGResonanceError Error { get; }
    public string Message { get; }

    public static DRPGResonanceOperationResult Ok()
    {
        return new DRPGResonanceOperationResult(
            true,
            DRPGResonanceError.None,
            string.Empty);
    }

    public static DRPGResonanceOperationResult Fail(
        DRPGResonanceError error,
        string message)
    {
        return new DRPGResonanceOperationResult(false, error, message);
    }
}

public sealed class DRPGResonanceService
{
    private readonly DRPGResonanceDefinitionSO definition;
    private readonly IDRPGResonanceConditionProvider conditionProvider;

    public DRPGResonanceService(
        DRPGResonanceDefinitionSO definition,
        IDRPGResonanceConditionProvider conditionProvider = null)
    {
        this.definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        this.conditionProvider = conditionProvider ??
            new DRPGZeroResonanceConditionProvider();
        State = new DRPGResonanceState();
    }

    public DRPGResonanceDefinitionSO Definition => definition;
    public DRPGResonanceState State { get; }
    public event Action<string, string> PartnerChanged;

    public DRPGResonanceOperationResult TrySelectPartner(
        DRPGCombatantProfileSO partner,
        DRPGPartyCompositionState party)
    {
        DRPGResonanceOperationResult validation = ValidatePartner(
            partner,
            party,
            true);
        if (!validation.Success)
            return validation;

        if (!party.TrySetResonancePartner(partner, out string error))
        {
            return Fail(
                DRPGResonanceError.PartyTransactionRejected,
                error);
        }

        string previousPartnerId = State.ResonancePartnerCharacterId;
        State.SetPartner(partner.CombatantId);
        PublishPartnerChanged(previousPartnerId);
        return DRPGResonanceOperationResult.Ok();
    }

    public DRPGResonanceOperationResult TryUnlockNode(
        string partnerCharacterId,
        string nodeId)
    {
        DRPGResonanceBranchSO branch =
            definition.FindBranchByPartnerId(partnerCharacterId);
        if (branch == null)
            return Fail(DRPGResonanceError.BranchNotFound, "Resonance branch not found.");
        DRPGResonanceNodeDefinition node = branch.FindNode(nodeId);
        if (node == null)
            return Fail(DRPGResonanceError.NodeNotFound, "Resonance node not found.");
        if (State.IsNodeUnlocked(branch.BranchId, node.NodeId))
            return DRPGResonanceOperationResult.Ok();
        if (!node.HasValidReward)
            return Fail(DRPGResonanceError.InvalidReward, "Resonance node reward is invalid.");

        IReadOnlyList<string> prerequisites = node.PrerequisiteNodeIds;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!State.IsNodeUnlocked(branch.BranchId, prerequisites[i]))
            {
                return Fail(
                    DRPGResonanceError.NodeLocked,
                    "A resonance prerequisite is locked: " + prerequisites[i]);
            }
        }

        int condition = conditionProvider.GetConditionValue(
            definition.ProtagonistCharacterId,
            partnerCharacterId);
        if (condition < node.RequiredConditionValue)
        {
            return Fail(
                DRPGResonanceError.ConditionNotMet,
                "The relationship condition is not met.");
        }

        State.Unlock(branch.BranchId, node.NodeId);
        return DRPGResonanceOperationResult.Ok();
    }

    public DRPGResonanceOperationResult TryEquipActiveSkill(
        string partnerCharacterId,
        DRPGSkillSO skill)
    {
        DRPGResonanceBranchSO branch =
            definition.FindBranchByPartnerId(partnerCharacterId);
        if (branch == null)
            return Fail(DRPGResonanceError.BranchNotFound, "Resonance branch not found.");
        if (skill == null)
        {
            State.SetEquippedActive(partnerCharacterId, string.Empty);
            return DRPGResonanceOperationResult.Ok();
        }

        DRPGResonanceNodeDefinition node =
            branch.FindActiveSkillNode(skill.SkillId);
        if (node == null)
        {
            return Fail(
                DRPGResonanceError.InvalidReward,
                "The active skill does not belong to this resonance branch.");
        }
        if (!State.IsNodeUnlocked(branch.BranchId, node.NodeId))
        {
            return Fail(
                DRPGResonanceError.NodeLocked,
                "The resonance active skill is still locked.");
        }

        State.SetEquippedActive(partnerCharacterId, skill.SkillId);
        return DRPGResonanceOperationResult.Ok();
    }

    public DRPGSkillSO GetEquippedActiveSkill(string partnerCharacterId)
    {
        DRPGResonanceBranchSO branch =
            definition.FindBranchByPartnerId(partnerCharacterId);
        if (branch == null)
            return null;
        string skillId = State.GetEquippedActiveSkillId(partnerCharacterId);
        DRPGResonanceNodeDefinition node = branch.FindActiveSkillNode(skillId);
        return node != null ? node.ActiveSkill : null;
    }

    public DRPGResolvedIdentityContribution ResolveIdentityContribution(
        DRPGCombatantProfileSO profile)
    {
        if (!definition.IsProtagonist(profile))
            return DRPGResolvedIdentityContribution.Empty;

        var passives = new List<DRPGPassiveModifierSO>();
        IReadOnlyList<DRPGResonanceBranchSO> branches = definition.Branches;
        for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
        {
            DRPGResonanceBranchSO branch = branches[branchIndex];
            if (branch == null)
                continue;
            IReadOnlyList<DRPGResonanceNodeDefinition> nodes = branch.Nodes;
            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                DRPGResonanceNodeDefinition node = nodes[nodeIndex];
                if (node != null &&
                    node.RewardKind == DRPGResonanceNodeRewardKind.Passive &&
                    node.PassiveModifier != null &&
                    State.IsNodeUnlocked(branch.BranchId, node.NodeId))
                {
                    passives.Add(node.PassiveModifier);
                }
            }
        }
        return new DRPGResolvedIdentityContribution(
            passives.AsReadOnly(),
            Array.Empty<DRPGSkillSO>());
    }

    public DRPGResonanceSaveData CaptureSaveData()
    {
        var save = new DRPGResonanceSaveData
        {
            resonancePartnerCharacterId = State.ResonancePartnerCharacterId
        };
        IReadOnlyList<DRPGResonanceBranchSO> branches = definition.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            DRPGResonanceBranchSO branch = branches[i];
            if (branch == null)
                continue;
            var branchSave = new DRPGResonanceBranchSaveData
            {
                branchId = branch.BranchId,
                equippedActiveSkillId = State.GetEquippedActiveSkillId(
                    branch.FriendCharacterId)
            };
            branchSave.unlockedNodeIds.AddRange(
                State.GetUnlockedNodeIds(branch.BranchId));
            save.branches.Add(branchSave);
        }
        return save;
    }

    public DRPGResonanceOperationResult RestoreSaveData(
        DRPGResonanceSaveData save,
        DRPGPartyCompositionState party,
        IEnumerable<DRPGCombatantProfileSO> profiles)
    {
        DRPGResonanceOperationResult result = BuildRestoredState(
            save,
            profiles,
            party,
            out DRPGResonanceState candidate,
            out DRPGCombatantProfileSO partner);
        if (!result.Success)
            return result;

        if (partner != null &&
            !party.TrySetResonancePartner(partner, out string error))
        {
            return Fail(DRPGResonanceError.PartyTransactionRejected, error);
        }
        string previousPartnerId = State.ResonancePartnerCharacterId;
        State.CopyFrom(candidate);
        PublishPartnerChanged(previousPartnerId);
        return DRPGResonanceOperationResult.Ok();
    }

    public DRPGResonanceOperationResult RestoreSaveData(
        DRPGResonanceSaveData save,
        IEnumerable<DRPGCombatantProfileSO> profiles)
    {
        DRPGResonanceOperationResult result = BuildRestoredState(
            save,
            profiles,
            null,
            out DRPGResonanceState candidate,
            out _);
        if (!result.Success)
            return result;
        string previousPartnerId = State.ResonancePartnerCharacterId;
        State.CopyFrom(candidate);
        PublishPartnerChanged(previousPartnerId);
        return DRPGResonanceOperationResult.Ok();
    }

    private DRPGResonanceOperationResult BuildRestoredState(
        DRPGResonanceSaveData save,
        IEnumerable<DRPGCombatantProfileSO> profiles,
        DRPGPartyCompositionState party,
        out DRPGResonanceState candidate,
        out DRPGCombatantProfileSO partner)
    {
        candidate = null;
        partner = null;
        if (save == null || save.version != 1 || save.branches == null)
            return Fail(DRPGResonanceError.InvalidSaveData, "Invalid resonance save data.");

        var profilesById = new Dictionary<string, DRPGCombatantProfileSO>(
            StringComparer.Ordinal);
        if (profiles != null)
        {
            foreach (DRPGCombatantProfileSO profile in profiles)
            {
                if (profile != null && !string.IsNullOrWhiteSpace(profile.CombatantId))
                    profilesById[profile.CombatantId] = profile;
            }
        }

        var restored = new DRPGResonanceState();
        var seenBranches = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < save.branches.Count; i++)
        {
            DRPGResonanceBranchSaveData branchSave = save.branches[i];
            DRPGResonanceBranchSO branch = branchSave != null
                ? definition.FindBranch(branchSave.branchId)
                : null;
            if (branch == null || !seenBranches.Add(branch.BranchId))
                return Fail(DRPGResonanceError.InvalidSaveData, "Unknown or duplicate resonance branch.");

            var seenNodes = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> unlocked = branchSave.unlockedNodeIds ??
                (IReadOnlyList<string>)Array.Empty<string>();
            for (int nodeIndex = 0; nodeIndex < unlocked.Count; nodeIndex++)
            {
                DRPGResonanceNodeDefinition node = branch.FindNode(unlocked[nodeIndex]);
                if (node == null || !node.HasValidReward ||
                    !seenNodes.Add(node.NodeId))
                {
                    return Fail(DRPGResonanceError.InvalidSaveData, "Invalid resonance node record.");
                }
                restored.Unlock(branch.BranchId, node.NodeId);
            }

            if (!string.IsNullOrWhiteSpace(branchSave.equippedActiveSkillId))
            {
                DRPGResonanceNodeDefinition active =
                    branch.FindActiveSkillNode(branchSave.equippedActiveSkillId);
                if (active == null ||
                    !restored.IsNodeUnlocked(branch.BranchId, active.NodeId))
                {
                    return Fail(DRPGResonanceError.InvalidSaveData, "Saved resonance active is locked or foreign.");
                }
                restored.SetEquippedActive(
                    branch.FriendCharacterId,
                    active.ActiveSkill.SkillId);
            }
        }

        if (!string.IsNullOrWhiteSpace(save.resonancePartnerCharacterId))
        {
            if (!profilesById.TryGetValue(
                    save.resonancePartnerCharacterId,
                    out partner))
            {
                return Fail(DRPGResonanceError.InvalidSaveData, "Saved resonance partner is unknown.");
            }
            DRPGResonanceOperationResult validation = ValidatePartner(
                partner,
                party,
                party != null);
            if (!validation.Success)
                return validation;
            restored.SetPartner(partner.CombatantId);
        }

        candidate = restored;
        return DRPGResonanceOperationResult.Ok();
    }

    private DRPGResonanceOperationResult ValidatePartner(
        DRPGCombatantProfileSO partner,
        DRPGPartyCompositionState party,
        bool requirePartyMembership)
    {
        if (partner == null || definition.IsProtagonist(partner))
            return Fail(DRPGResonanceError.PartnerUnavailable, "A valid friend partner is required.");
        if (partner.ProgressionDefinition == null ||
            partner.ProgressionDefinition.PersonnelKind != DRPGPersonnelKind.Friend)
        {
            return Fail(DRPGResonanceError.PartnerNotFriend, "Only Friend personnel can resonate.");
        }
        if (definition.FindBranchByPartnerId(partner.CombatantId) == null)
            return Fail(DRPGResonanceError.BranchNotFound, "The friend has no resonance branch.");
        if (requirePartyMembership &&
            (party == null || !party.ContainsCharacterId(partner.CombatantId)))
        {
            return Fail(DRPGResonanceError.PartnerNotInParty, "The resonance partner is not in the party.");
        }
        return DRPGResonanceOperationResult.Ok();
    }

    private static DRPGResonanceOperationResult Fail(
        DRPGResonanceError error,
        string message)
    {
        return DRPGResonanceOperationResult.Fail(error, message);
    }

    private void PublishPartnerChanged(string previousPartnerId)
    {
        string nextPartnerId = State.ResonancePartnerCharacterId;
        if (!string.Equals(
                previousPartnerId,
                nextPartnerId,
                StringComparison.Ordinal))
        {
            PartnerChanged?.Invoke(
                previousPartnerId ?? string.Empty,
                nextPartnerId ?? string.Empty);
        }
    }
}
