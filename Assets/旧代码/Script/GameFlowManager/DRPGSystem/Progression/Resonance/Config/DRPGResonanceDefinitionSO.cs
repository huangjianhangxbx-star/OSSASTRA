using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGResonanceDefinition",
    menuName = "ANKALUTE/DRPG/Resonance/Identity Definition")]
public sealed class DRPGResonanceDefinitionSO : ScriptableObject
{
    [SerializeField] private string identityId = "protagonist_resonance";
    [SerializeField] private DRPGCombatantProfileSO protagonistProfile;
    [SerializeField] private List<DRPGResonanceBranchSO> branches =
        new List<DRPGResonanceBranchSO>();

    public string IdentityId => identityId;
    public DRPGCombatantProfileSO ProtagonistProfile => protagonistProfile;
    public string ProtagonistCharacterId => protagonistProfile != null
        ? protagonistProfile.CombatantId
        : string.Empty;
    public IReadOnlyList<DRPGResonanceBranchSO> Branches =>
        branches ?? (IReadOnlyList<DRPGResonanceBranchSO>)
            Array.Empty<DRPGResonanceBranchSO>();

    public bool IsProtagonist(DRPGCombatantProfileSO profile)
    {
        return profile != null && protagonistProfile != null &&
            (ReferenceEquals(profile, protagonistProfile) ||
             string.Equals(
                 profile.CombatantId,
                 protagonistProfile.CombatantId,
                 StringComparison.Ordinal));
    }

    public DRPGResonanceBranchSO FindBranchByPartnerId(string partnerId)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
            return null;
        for (int i = 0; i < Branches.Count; i++)
        {
            DRPGResonanceBranchSO branch = Branches[i];
            if (branch != null && string.Equals(
                    branch.FriendCharacterId,
                    partnerId,
                    StringComparison.Ordinal))
            {
                return branch;
            }
        }
        return null;
    }

    public DRPGResonanceBranchSO FindBranch(string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
            return null;
        for (int i = 0; i < Branches.Count; i++)
        {
            DRPGResonanceBranchSO branch = Branches[i];
            if (branch != null && string.Equals(
                    branch.BranchId,
                    branchId,
                    StringComparison.Ordinal))
            {
                return branch;
            }
        }
        return null;
    }
}
