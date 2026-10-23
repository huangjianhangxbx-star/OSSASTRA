#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

public static class BattleV2AcceptanceProbe
{
    public static void RunAll()
    {
        FormalProfilesUseSmallBattleDefaults();
        FormalSkillsHaveExplicitCostsAndSafeModes();
        FormalProfessionsExposeFourStandardAttacks();
        EnemyIntentCannotExposeStamina();
        FinalLayeredExampleMatchesApprovedRule();
    }

    private static void FormalProfessionsExposeFourStandardAttacks()
    {
        string[] paths =
        {
            "Assets/GameResources/DRPG/Progression/Test/Revolver/DRPGProfession_Revolver.asset",
            "Assets/GameResources/DRPG/Progression/Test/Katana/DRPGProfession_Katana.asset"
        };
        for (int i = 0; i < paths.Length; i++)
        {
            DRPGProfessionSO profession =
                AssetDatabase.LoadAssetAtPath<DRPGProfessionSO>(paths[i]);
            Check(profession != null && profession.InitialLoadout.Count == 4,
                paths[i] + " does not provide exactly four explicit slots");
            var grantedSkills = new HashSet<DRPGSkillSO>();
            for (int nodeIndex = 0;
                nodeIndex < profession.GrantedNodeIds.Count;
                nodeIndex++)
            {
                DRPGSkillTreeNodeDefinition node = profession.SkillTree.FindNode(
                    profession.GrantedNodeIds[nodeIndex]);
                if (node != null && node.Skill != null)
                    grantedSkills.Add(node.Skill);
            }
            for (int slot = 0; slot < profession.InitialLoadout.Count; slot++)
            {
                DRPGSkillSO skill = profession.InitialLoadout[slot];
                Check(DRPGBattleSkillModePolicy.IsAllowed(skill),
                    paths[i] + " slot " + slot + " is not a standard attack");
                Check(grantedSkills.Contains(skill),
                    paths[i] + " slot " + slot +
                    " is not unlocked by its profession grants");
            }
        }
    }

    private static void FormalProfilesUseSmallBattleDefaults()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:DRPGCombatantProfileSO",
            new[] { "Assets/GameResources/DRPG/Config/Test" });
        int players = 0;
        int enemies = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int j = 0; j < assets.Length; j++)
            {
                DRPGCombatantProfileSO profile =
                    assets[j] as DRPGCombatantProfileSO;
                if (profile == null || !profile.CombatantId.StartsWith(
                        "ankalute_", StringComparison.Ordinal))
                    continue;
                bool enemy = profile.CombatantId.Contains("enemy") ||
                    profile.CombatantId.Contains("sentinel") ||
                    profile.CombatantId.Contains("warden");
                Check(profile.MaxPosture == (enemy ? 5f : 10f),
                    profile.CombatantId + " has an unmigrated posture default");
                Check(profile.PostureRecoveryPerTurn == 0f,
                    profile.CombatantId + " still passively restores posture");
                if (enemy) enemies++; else players++;
            }
        }
        Check(players >= 3 && enemies >= 2,
            "formal F8 combatant profiles were not found");
    }

    private static void FormalSkillsHaveExplicitCostsAndSafeModes()
    {
        DRPGSkillSO quickdraw = FindSkill("revolver_quickdraw");
        DRPGSkillSO aimed = FindSkill("revolver_aimed_shot");
        DRPGSkillSO guard = FindSkill("revolver_guard");
        DRPGSkillSO free = FindSkill("free_shot");
        Check(quickdraw.StaminaCost == 1 && aimed.StaminaCost == 2,
            "formal revolver costs are not explicit");
        Check(DRPGBattleSkillModePolicy.IsAllowed(quickdraw) &&
            DRPGBattleSkillModePolicy.IsAllowed(aimed),
            "standard attacks were blocked");
        Check(!DRPGBattleSkillModePolicy.IsAllowed(guard) &&
            !DRPGBattleSkillModePolicy.IsAllowed(free),
            "legacy guard/free-strike leaked into the standard attack root");
    }

    private static void EnemyIntentCannotExposeStamina()
    {
        Check(typeof(DRPGEnemyActionPlanSnapshot).GetProperty("Stamina") == null &&
            typeof(DRPGEnemyActionPlanSnapshot).GetProperty("StaminaCapacity") == null,
            "enemy intent exposes hidden stamina state");
        Check(typeof(DRPGEnemyActionPlanSnapshot).GetProperty("Panel") != null &&
            typeof(DRPGEnemyActionPlanSnapshot).GetProperty("SkillId") != null,
            "enemy intent is missing its public skill panel");
    }

    private static void FinalLayeredExampleMatchesApprovedRule()
    {
        BattleDamageResult result = BattleDamageMath.Resolve(12, 0, 5, 10);
        Check(result.PostureAfter == 0 && result.HpAfter == 3,
            "approved 12/5/10 example regressed");
    }

    private static DRPGSkillSO FindSkill(string skillId)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:DRPGSkillSO",
            new[] { "Assets/GameResources/DRPG" });
        for (int i = 0; i < guids.Length; i++)
        {
            DRPGSkillSO skill = AssetDatabase.LoadAssetAtPath<DRPGSkillSO>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (skill != null && skill.SkillId == skillId)
                return skill;
        }
        throw new InvalidOperationException("Missing skill " + skillId);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
