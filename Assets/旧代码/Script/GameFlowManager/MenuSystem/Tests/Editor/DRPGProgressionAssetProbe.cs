#if UNITY_EDITOR
using System;
using UnityEditor;

public static class DRPGProgressionAssetProbe
{
    private const string Root = "Assets/GameResources/DRPG/Progression/Test/";

    public static string Run()
    {
        VerifyProfession("Katana/DRPGProfession_Katana.asset", "katana");
        VerifyProfession("Revolver/DRPGProfession_Revolver.asset", "revolver");

        DRPGPartySetupSO party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>(
            "Assets/GameResources/DRPG/Config/Test/DRPGParty_Test3.asset");
        Assert(party != null, "The 3-person test party is missing");
        Assert(party.Protagonist != null &&
               party.Protagonist.CombatantId == "ankalute_party_vanguard",
            "Current melee profile must occupy the protagonist role");
        Assert(party.PartyMembers.Count >= 1 &&
               party.PartyMembers[0].CombatantId == "ankalute_protagonist",
            "Current gun profile must be an ordinary party member");

        return "PASS two profession trees and conceptual role mapping";
    }

    private static void VerifyProfession(string relativePath, string id)
    {
        DRPGProfessionSO profession =
            AssetDatabase.LoadAssetAtPath<DRPGProfessionSO>(Root + relativePath);
        Assert(profession != null, "Profession is missing: " + id);
        Assert(profession.ProfessionId == id, "Profession ID changed: " + id);
        Assert(profession.BasicAttack != null, "Basic attack is missing: " + id);
        Assert(profession.SkillTree != null, "Skill tree is missing: " + id);
        profession.SkillTree.ValidateOrThrow();
        Assert(profession.SkillTree.Nodes.Count == 16,
            "Profession tree must contain 8 skills and 8 passives: " + id);
        int skillCount = 0;
        int passiveCount = 0;
        for (int i = 0; i < profession.SkillTree.Nodes.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = profession.SkillTree.Nodes[i];
            if (node.RewardKind == DRPGSkillTreeRewardKind.Skill)
                skillCount++;
            else
                passiveCount++;
            Assert((i % 2 == 0) ==
                   (node.RewardKind == DRPGSkillTreeRewardKind.Skill),
                "Test tree must alternate skill/passive rewards: " + id);
        }
        Assert(skillCount == 8 && passiveCount == 8,
            "Profession reward counts are invalid: " + id);
        Assert(profession.InitialLoadout.Count == DRPGSkillLoadout.SlotCount,
            "Profession must seed four equipped skills: " + id);
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
