using System;
using System.Collections.Generic;

[Serializable]
public sealed class DRPGResonanceSaveData
{
    public int version = 1;
    public string resonancePartnerCharacterId = string.Empty;
    public List<DRPGResonanceBranchSaveData> branches =
        new List<DRPGResonanceBranchSaveData>();
}

[Serializable]
public sealed class DRPGResonanceBranchSaveData
{
    public string branchId = string.Empty;
    public List<string> unlockedNodeIds = new List<string>();
    public string equippedActiveSkillId = string.Empty;
}
