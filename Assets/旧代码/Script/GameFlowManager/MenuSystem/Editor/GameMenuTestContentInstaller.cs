#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameMenuTestContentInstaller
{
    private const string Root = "Assets/GameResources/DRPG/Progression/Test";
    private const string KatanaRoot = Root + "/Katana";
    private const string RevolverRoot = Root + "/Revolver";
    private const string MenuFolder = "Assets/Resources/DRPG/UI";
    private const string EquipmentRoot =
        "Assets/GameResources/DRPG/Equipment/Test";
    private const string FreeShotChargeResourcePath =
        "Assets/GameResources/DRPG/Config/Test/DRPGProgress_FreeShotCharge.asset";
    private const string MenuPrefabPath = MenuFolder + "/PF_GameMenu.prefab";
    private const string FontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";
    private const string ProfileFolder = "Assets/GameResources/DRPG/Config/Test";
    private const string MeleeProfilePath =
        ProfileFolder + "/DRPGCombatant_ankalute_party_vanguard.asset";
    private const string GunProfilePath =
        ProfileFolder + "/DRPGCombatant_ankalute_protagonist.asset";
    private const string AlchemistProfilePath =
        ProfileFolder + "/DRPGCombatant_ankalute_party_alchemist.asset";
    private const string PartyPath = ProfileFolder + "/DRPGParty_Test3.asset";
    private const string EncounterPath =
        ProfileFolder + "/DRPGEncounter_Test2.asset";
    private const string BattlePrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string FreeSlashPath = ProfileFolder + "/DRPGSkill_FreeSlash.asset";
    private const string FreeShotPath = ProfileFolder + "/DRPGSkill_FreeShot.asset";
    private static readonly Color Wine = new Color(0.17f, 0.008f, 0.045f, 0.98f);
    private static readonly Color Ink = new Color(0.018f, 0.018f, 0.028f, 0.97f);
    private static readonly Color Panel = new Color(0.035f, 0.022f, 0.040f, 0.95f);
    private static readonly Color Magenta = new Color(1f, 0.035f, 0.36f, 1f);
    private static readonly Color Pink = new Color(1f, 0.30f, 0.58f, 1f);
    private static readonly Color WarmWhite = new Color(0.96f, 0.94f, 0.93f, 1f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.16f, 1f);

    [MenuItem("Tools/ANKALUTE/Progression/Install Menu And Professions")]
    public static void InstallFromMenu()
    {
        Debug.Log(Install());
    }

    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "[GameMenuInstaller] REJECTED: exit Play Mode first.";

        EnsureFolder(Root);
        EnsureFolder(KatanaRoot);
        EnsureFolder(RevolverRoot);
        EnsureFolder(MenuFolder);
        EnsureFolder(EquipmentRoot);

        DRPGCombatantProfileSO melee = Require<DRPGCombatantProfileSO>(
            MeleeProfilePath);
        DRPGCombatantProfileSO gun = Require<DRPGCombatantProfileSO>(
            GunProfilePath);
        DRPGCombatantProfileSO alchemist = Require<DRPGCombatantProfileSO>(
            AlchemistProfilePath);

        DRPGProfessionSO katana = CreateProfession(
            KatanaRoot,
            "katana",
            "打刀",
            new Color(0.20f, 0.88f, 0.92f, 1f),
            CreateKatanaSkills(),
            CreatePassives(KatanaRoot, "katana", "剑理"));
        DRPGProfessionSO revolver = CreateProfession(
            RevolverRoot,
            "revolver",
            "左轮",
            new Color(0.92f, 0.22f, 0.48f, 1f),
            CreateRevolverSkills(),
            CreatePassives(RevolverRoot, "revolver", "枪术"));

        DRPGCharacterProgressionDefinitionSO meleeProgression =
            CreateCharacterProgression(
                KatanaRoot + "/DRPGProgression_MeleeProfile.asset",
                "progression_melee_profile",
                katana);
        DRPGCharacterProgressionDefinitionSO gunProgression =
            CreateCharacterProgression(
                RevolverRoot + "/DRPGProgression_GunProfile.asset",
                "progression_gun_profile",
                revolver);
        SetReference(melee, "progressionDefinition", meleeProgression);
        SetReference(gun, "progressionDefinition", gunProgression);
        EquipmentSampleSet equipmentSamples = CreateEquipmentSamples(
            melee,
            gun,
            alchemist,
            katana,
            revolver);
        ConfigureParty(melee, gun, alchemist);
        ConfigureEncounterEscape();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        CreateMenuPrefab(font, gun, melee);
        UpgradeBattleCommandPrefab(font);
        ConfigureOpenSceneEquipment(equipmentSamples);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "[GameMenuInstaller] PASS; katana=8+8; revolver=8+8; " +
               "roleMapping=melee-protagonist/gun-member; equipment=sampled; " +
               "renamed=false; prefab=" +
               MenuPrefabPath;
    }

    private static DRPGSkillSO[] CreateKatanaSkills()
    {
        DRPGSkillSO freeSlash = Require<DRPGSkillSO>(FreeSlashPath);
        ConfigureSkill(
            freeSlash,
            "free_slash",
            "自由斩击",
            "进入自由攻击时间，以横向斩线命中战场中的目标。",
            16f,
            DRPGSkillPurpose.SpecialAttack,
            DRPGSkillTargetRule.BattlefieldPoint,
            DRPGSkillExecutionMode.FreeStrike);
        return new[]
        {
            CreateSkill(KatanaRoot, "katana_iai", "居合一闪",
                "高速单体斩击，短回收并强调先手。", 42f),
            CreateSkill(KatanaRoot, "katana_crosscut", "十文字",
                "两段交叉斩，为多段事件演出预留子技能入口。", 30f),
            CreateSkill(KatanaRoot, "katana_guard", "流心架势",
                "主动架势技能；正式版可强化普通防御。", 0f,
                DRPGSkillPurpose.Defense, DRPGSkillTargetRule.Self),
            freeSlash,
            CreateSkill(KatanaRoot, "katana_riposte", "返刃",
                "特殊防御技能入口，可扩展完全防御、弹反或反伤。", 0f,
                DRPGSkillPurpose.SpecialDefense, DRPGSkillTargetRule.Self),
            CreateSkill(KatanaRoot, "katana_blood_flick", "血振",
                "稳定斩击；为出血与连续命中效果预留接口。", 36f),
            CreateSkill(KatanaRoot, "katana_shadow_step", "影踏",
                "调整行动延迟与站位的战术技能入口。", 0f,
                DRPGSkillPurpose.Utility, DRPGSkillTargetRule.Self),
            CreateSkill(KatanaRoot, "katana_moon_eclipse", "奥义·月蚀",
                "高威力终结斩，适合完整 Spine 事件演出。", 76f)
        };
    }

    private static DRPGSkillSO[] CreateRevolverSkills()
    {
        DRPGSkillSO freeShot = Require<DRPGSkillSO>(FreeShotPath);
        ConfigureSkill(
            freeShot,
            "free_shot",
            "自由射击",
            "进入自由攻击时间，以射线和准心命中战场中的目标。",
            12f,
            DRPGSkillPurpose.SpecialAttack,
            DRPGSkillTargetRule.BattlefieldPoint,
            DRPGSkillExecutionMode.FreeStrike);
        DRPGSkillSO quickDraw = CreateSkill(
            RevolverRoot, "revolver_quickdraw", "速射",
            "快速拔枪射击，短回收、低单发威力。", 28f);
        DRPGSkillSO fanFire = CreateSkill(
            RevolverRoot, "revolver_fan_fire", "扇击连射",
            "连续击锤射击，为多次 Spine 命中事件预留接口。", 22f);
        DRPGSkillSO reload = CreateSkill(
            RevolverRoot, "revolver_reload", "战术装填",
            "恢复弹药并可暂时替换四个技能按钮。", 0f,
            DRPGSkillPurpose.Utility, DRPGSkillTargetRule.Self);
        ConfigureExpansion(reload, quickDraw, fanFire);
        return new[]
        {
            quickDraw,
            CreateSkill(RevolverRoot, "revolver_aimed_shot", "定点射击",
                "高准度单点射击，适合瞄准弱点。", 45f),
            CreateSkill(RevolverRoot, "revolver_guard", "侧身警戒",
                "主动防御并为下次反击保留扩展接口。", 0f,
                DRPGSkillPurpose.Defense, DRPGSkillTargetRule.Self),
            freeShot,
            CreateSkill(RevolverRoot, "revolver_cover_counter", "掩护反击",
                "特殊防御技能入口，可由养成决定完美防御结果。", 0f,
                DRPGSkillPurpose.SpecialDefense, DRPGSkillTargetRule.Self),
            fanFire,
            reload,
            CreateSkill(RevolverRoot, "revolver_last_round", "终末弹",
                "最后一发高威力射击，为弹仓状态加成预留接口。", 72f)
        };
    }

    private static DRPGSkillSO CreateSkill(
        string folder,
        string id,
        string displayName,
        string description,
        float power,
        DRPGSkillPurpose purpose = DRPGSkillPurpose.Damage,
        DRPGSkillTargetRule target = DRPGSkillTargetRule.SingleEnemy)
    {
        string path = folder + "/DRPGSkill_" + id + ".asset";
        DRPGSkillSO skill = LoadOrCreate<DRPGSkillSO>(path);
        ConfigureSkill(
            skill,
            id,
            displayName,
            description,
            power,
            purpose,
            target,
            DRPGSkillExecutionMode.Standard);
        return skill;
    }

    private static void ConfigureSkill(
        DRPGSkillSO skill,
        string id,
        string displayName,
        string description,
        float power,
        DRPGSkillPurpose purpose,
        DRPGSkillTargetRule target,
        DRPGSkillExecutionMode execution)
    {
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("skillId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("power").floatValue = power;
        serialized.FindProperty("prepareDelay").floatValue = 0f;
        serialized.FindProperty("recoveryDelay").floatValue = 100f;
        serialized.FindProperty("purpose").enumValueIndex = (int)purpose;
        serialized.FindProperty("targetRule").enumValueIndex = (int)target;
        serialized.FindProperty("executionMode").enumValueIndex = (int)execution;
        serialized.FindProperty("presentationKey").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureExpansion(
        DRPGSkillSO skill,
        params DRPGSkillSO[] children)
    {
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("commandExpansionMode").enumValueIndex =
            (int)DRPGSkillCommandExpansionMode.ReplaceSkillSlotsUntilResolved;
        SerializedProperty childSkills = serialized.FindProperty("childSkills");
        childSkills.arraySize = children.Length;
        for (int i = 0; i < children.Length; i++)
            childSkills.GetArrayElementAtIndex(i).objectReferenceValue = children[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static DRPGPassiveModifierSO[] CreatePassives(
        string folder,
        string idPrefix,
        string displayPrefix)
    {
        string[] suffixes =
        {
            "基礎", "呼吸", "歩法", "鍛錬", "集中", "見切", "極意", "皆伝"
        };
        var result = new DRPGPassiveModifierSO[8];
        for (int i = 0; i < result.Length; i++)
        {
            string id = idPrefix + "_passive_" + (i + 1).ToString("00");
            string path = folder + "/DRPGPassive_" + id + ".asset";
            DRPGPassiveModifierSO passive =
                LoadOrCreate<DRPGPassiveModifierSO>(path);
            var serialized = new SerializedObject(passive);
            serialized.FindProperty("modifierId").stringValue = id;
            serialized.FindProperty("displayName").stringValue =
                displayPrefix + "·" + suffixes[i];
            serialized.FindProperty("description").stringValue =
                "职业技能树测试 Buff。正式数值可独立替换，不影响技能节点与装配关系。";
            SetFloat(serialized, "damageMultiplier", i == 0 || i == 6 ? 1.06f : 1f);
            SetFloat(serialized, "maxHpMultiplier", i == 3 ? 1.10f : 1f);
            SetFloat(serialized, "agilityMultiplier", i == 2 ? 1.08f : 1f);
            SetFloat(serialized, "recoveryDelayMultiplier", i == 4 ? 0.92f : 1f);
            SetFloat(serialized, "maxPostureMultiplier", i == 1 ? 1.12f : 1f);
            SetFloat(serialized, "postureRecoveryMultiplier", i == 5 ? 1.15f : 1f);
            SetFloat(serialized, "freeStrikeDurationBonus", i == 7 ? 0.35f : 0f);
            SetFloat(serialized, "freeStrikeCadenceMultiplier", 1f);
            SetFloat(serialized, "freeStrikePostureCostMultiplier", 1f);
            SetFloat(serialized, "freeStrikeReachMultiplier", i == 7 ? 1.12f : 1f);
            SetFloat(serialized, "guardWindowMultiplier", i == 5 ? 1.12f : 1f);
            SetFloat(serialized, "escapeChanceBonus", 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(passive);
            result[i] = passive;
        }
        return result;
    }

    private static DRPGProfessionSO CreateProfession(
        string folder,
        string id,
        string displayName,
        Color theme,
        IReadOnlyList<DRPGSkillSO> skills,
        IReadOnlyList<DRPGPassiveModifierSO> passives)
    {
        DRPGSkillSO basic = CreateSkill(
            folder,
            id + "_basic_attack",
            id == "katana" ? "打刀·通常斩" : "左轮·通常射击",
            "职业固有普通攻击，不占四个技能槽。",
            34f);
        DRPGSkillTreeSO tree = LoadOrCreate<DRPGSkillTreeSO>(
            folder + "/DRPGSkillTree_" + id + ".asset");
        ConfigureTree(tree, id, displayName + "技能树", skills, passives);

        DRPGProfessionSO profession = LoadOrCreate<DRPGProfessionSO>(
            folder + "/DRPGProfession_" +
            (id == "katana" ? "Katana" : "Revolver") + ".asset");
        var serialized = new SerializedObject(profession);
        serialized.FindProperty("professionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue =
            "测试职业定义：技能、Buff、基础攻击与四槽装配均由本资产聚合。";
        serialized.FindProperty("themeColor").colorValue = theme;
        serialized.FindProperty("basicAttack").objectReferenceValue = basic;
        serialized.FindProperty("skillTree").objectReferenceValue = tree;
        SerializedProperty granted = serialized.FindProperty("grantedNodeIds");
        granted.arraySize = 7;
        for (int i = 0; i < granted.arraySize; i++)
        {
            granted.GetArrayElementAtIndex(i).stringValue = NodeId(id, i);
        }
        SerializedProperty initial = serialized.FindProperty("initialLoadout");
        initial.arraySize = DRPGSkillLoadout.SlotCount;
        for (int i = 0; i < initial.arraySize; i++)
            initial.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profession);
        return profession;
    }

    private static void ConfigureTree(
        DRPGSkillTreeSO tree,
        string professionId,
        string displayName,
        IReadOnlyList<DRPGSkillSO> skills,
        IReadOnlyList<DRPGPassiveModifierSO> passives)
    {
        var serialized = new SerializedObject(tree);
        serialized.FindProperty("treeId").stringValue = professionId + "_tree";
        serialized.FindProperty("displayName").stringValue = displayName;
        SerializedProperty nodes = serialized.FindProperty("nodes");
        nodes.arraySize = 16;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            bool skillNode = i % 2 == 0;
            int rewardIndex = i / 2;
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("nodeId").stringValue = NodeId(professionId, i);
            node.FindPropertyRelative("displayName").stringValue = skillNode
                ? skills[rewardIndex].DisplayName
                : passives[rewardIndex].DisplayName;
            node.FindPropertyRelative("description").stringValue = skillNode
                ? skills[rewardIndex].Description
                : passives[rewardIndex].Description;
            int column = i % 4;
            int row = i / 4;
            node.FindPropertyRelative("layoutPosition").vector2Value =
                new Vector2(-570f + column * 380f, 300f - row * 205f);
            node.FindPropertyRelative("pointCost").intValue = 1;
            SerializedProperty prerequisites =
                node.FindPropertyRelative("prerequisiteNodeIds");
            prerequisites.arraySize = i == 0 ? 0 : 1;
            if (i > 0)
                prerequisites.GetArrayElementAtIndex(0).stringValue =
                    NodeId(professionId, i - 1);
            node.FindPropertyRelative("rewardKind").enumValueIndex = skillNode
                ? (int)DRPGSkillTreeRewardKind.Skill
                : (int)DRPGSkillTreeRewardKind.Passive;
            node.FindPropertyRelative("skill").objectReferenceValue =
                skillNode ? skills[rewardIndex] : null;
            node.FindPropertyRelative("passiveModifier").objectReferenceValue =
                skillNode ? null : passives[rewardIndex];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tree);
    }

    private static string NodeId(string professionId, int zeroBasedIndex)
    {
        bool skill = zeroBasedIndex % 2 == 0;
        return professionId + (skill ? "_skill_node_" : "_passive_node_") +
               (zeroBasedIndex / 2 + 1).ToString("00");
    }

    private static DRPGCharacterProgressionDefinitionSO CreateCharacterProgression(
        string path,
        string id,
        DRPGProfessionSO profession)
    {
        DRPGCharacterProgressionDefinitionSO definition =
            LoadOrCreate<DRPGCharacterProgressionDefinitionSO>(path);
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("progressionId").stringValue = id;
        serialized.FindProperty("defaultProfession").objectReferenceValue = profession;
        SerializedProperty available = serialized.FindProperty("availableProfessions");
        available.arraySize = 1;
        available.GetArrayElementAtIndex(0).objectReferenceValue = profession;
        serialized.FindProperty("startingLevel").intValue = 1;
        serialized.FindProperty("allowsLevelBreakthrough").boolValue = true;
        serialized.FindProperty("breakthroughUnlockedByDefault").boolValue = false;
        serialized.FindProperty("startingSkillPoints").intValue = 12;
        serialized.FindProperty("startingAttributePoints").intValue = 6;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private sealed class EquipmentSampleSet
    {
        public DRPGWeaponDefinitionSO[] Weapons;
        public DRPGOutfitDefinitionSO[] Outfits;
        public DRPGAccessoryDefinitionSO[] Accessories;
        public DRPGTraitDefinitionSO[] Traits;
        public DRPGWeaponAffixDefinitionSO[] Affixes;
        public DRPGWeaponCardDefinitionSO[] Cards;
        public DRPGWeaponAttachmentDefinitionSO[] Attachments;
        public DRPGStartingCharacterEquipment[] Characters;
        public DRPGStartingBackpackWeapon[] BackpackWeapons;
    }

    private static EquipmentSampleSet CreateEquipmentSamples(
        DRPGCombatantProfileSO melee,
        DRPGCombatantProfileSO gun,
        DRPGCombatantProfileSO employee,
        DRPGProfessionSO katana,
        DRPGProfessionSO revolver)
    {
        DRPGProgressResourceSO freeShotCharge =
            Require<DRPGProgressResourceSO>(FreeShotChargeResourcePath);
        DRPGSkillSO signatureSkill = CreateSkill(
            EquipmentRoot,
            "weapon_signature_moon_draw",
            "月影拔刀",
            "守的专武固有技。装备后自动追加到战斗技能列表。",
            58f);
        DRPGSkillSO redGoldSkill = CreateSkill(
            EquipmentRoot,
            "weapon_redgold_crimson_echo",
            "绯响追刃",
            "普通红金武器固有技；不占四个职业技能槽。",
            52f);

        DRPGPassiveModifierSO edgeModifier = CreateEquipmentPassive(
            "DRPGPassive_EquipmentEdge.asset",
            "equipment_edge",
            "锋锐校准",
            "伤害倍率 +4%。",
            1.04f,
            1f,
            1f);
        DRPGPassiveModifierSO postureModifier = CreateEquipmentPassive(
            "DRPGPassive_EquipmentPosture.asset",
            "equipment_posture",
            "稳固握柄",
            "架势上限 +8%。",
            1f,
            1.08f,
            1f);
        DRPGPassiveModifierSO cadenceModifier = CreateEquipmentPassive(
            "DRPGPassive_EquipmentCadence.asset",
            "equipment_cadence",
            "轻量轮组",
            "自由攻击节奏 +5%。",
            1f,
            1f,
            1.05f);

        DRPGWeaponAffixDefinitionSO affix =
            LoadOrCreate<DRPGWeaponAffixDefinitionSO>(
                EquipmentRoot + "/DRPGWeaponAffix_Crimson.asset");
        ConfigureNamedAsset(
            affix,
            "affixId",
            "affix_crimson_calibration",
            "绯红校准",
            "稳定提高武器伤害。",
            "conditional_damage:always");
        SetObjectArray(affix, "passiveModifiers", new[] { edgeModifier });

        DRPGWeaponCardDefinitionSO attributeCard =
            LoadOrCreate<DRPGWeaponCardDefinitionSO>(
                EquipmentRoot + "/DRPGWeaponCard_AttributeShift.asset");
        ConfigureWeaponCard(
            attributeCard,
            "card_attribute_shift",
            "赤热转换",
            "将攻击属性转换为测试用赤热属性。",
            2,
            "attack_attribute:heat",
            null,
            Array.Empty<DRPGPassiveModifierSO>());
        DRPGWeaponCardDefinitionSO conditionalCard =
            LoadOrCreate<DRPGWeaponCardDefinitionSO>(
                EquipmentRoot + "/DRPGWeaponCard_Conditional.asset");
        ConfigureWeaponCard(
            conditionalCard,
            "card_conditional_damage",
            "破绽追击",
            "目标架势崩解时造成额外伤害。",
            3,
            "conditional_damage:posture_broken",
            null,
            new[] { edgeModifier });

        DRPGWeaponAttachmentDefinitionSO grip =
            LoadOrCreate<DRPGWeaponAttachmentDefinitionSO>(
                EquipmentRoot + "/DRPGWeaponAttachment_Grip.asset");
        ConfigureAttachment(
            grip,
            "attachment_stable_grip",
            "稳固握柄",
            "提高架势上限。",
            new[] { postureModifier });
        DRPGWeaponAttachmentDefinitionSO action =
            LoadOrCreate<DRPGWeaponAttachmentDefinitionSO>(
                EquipmentRoot + "/DRPGWeaponAttachment_Action.asset");
        ConfigureAttachment(
            action,
            "attachment_light_action",
            "轻量轮组",
            "提高自由攻击节奏。",
            new[] { cadenceModifier });

        DRPGTraitDefinitionSO outfitTrait =
            LoadOrCreate<DRPGTraitDefinitionSO>(
                EquipmentRoot + "/DRPGTrait_FieldCoat.asset");
        ConfigureNamedAsset(
            outfitTrait,
            "traitId",
            "trait_field_coat",
            "地宫行装",
            "由服装提供的来源绑定词条，不占十个常规逻辑位。",
            null);
        DRPGOutfitDefinitionSO outfit =
            LoadOrCreate<DRPGOutfitDefinitionSO>(
                EquipmentRoot + "/DRPGOutfit_FieldCoat.asset");
        var outfitSerialized = new SerializedObject(outfit);
        outfitSerialized.FindProperty("outfitId").stringValue =
            "outfit_field_coat";
        outfitSerialized.FindProperty("displayName").stringValue = "交界地行装";
        outfitSerialized.FindProperty("grantedTrait").objectReferenceValue =
            outfitTrait;
        outfitSerialized.FindProperty("grantedTraitId").stringValue =
            outfitTrait.TraitId;
        outfitSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(outfit);

        DRPGAccessoryDefinitionSO[] accessories =
        {
            CreateAccessory("charm_guard", "守护挂坠", 0.2f),
            CreateAccessory("charm_focus", "聚焦耳坠", 0.1f),
            CreateAccessory("charm_signal", "回路徽记", 0.15f)
        };

        DRPGWeaponDefinitionSO signatureKatana = ConfigureWeapon(
            EquipmentRoot + "/DRPGWeapon_SignatureKatana.asset",
            "weapon_signature_katana_shu",
            "守·试作打刀",
            "不会永久破碎的角色专武。",
            DRPGEquipmentRarity.RedGold,
            DRPGWeaponOwnershipKind.Signature,
            melee.CombatantId,
            new[] { katana },
            4.2f,
            100,
            new[]
            {
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Strength, 8),
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Technique, 8)
            },
            katana.BasicAttack,
            signatureSkill,
            "weapon:katana:signature",
            new[] { affix });
        DRPGWeaponDefinitionSO generalRevolver = ConfigureWeapon(
            EquipmentRoot + "/DRPGWeapon_GeneralRevolver.asset",
            "weapon_general_revolver",
            "量产六式左轮",
            "亲友与雇员均可使用的通用左轮。",
            DRPGEquipmentRarity.Gold,
            DRPGWeaponOwnershipKind.General,
            string.Empty,
            new[] { revolver },
            2.8f,
            76,
            new[]
            {
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Technique, 8),
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Reaction, 8)
            },
            revolver.BasicAttack,
            null,
            "weapon:revolver:standard",
            Array.Empty<DRPGWeaponAffixDefinitionSO>());
        DRPGWeaponDefinitionSO redGoldKatana = ConfigureWeapon(
            EquipmentRoot + "/DRPGWeapon_RedGoldKatana.asset",
            "weapon_redgold_katana_echo",
            "无铭·绯响",
            "可被一般角色使用的红金打刀，附带固有技。",
            DRPGEquipmentRarity.RedGold,
            DRPGWeaponOwnershipKind.General,
            string.Empty,
            new[] { katana },
            5.1f,
            92,
            new[]
            {
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Strength, 9),
                new DRPGAttributeRequirement(DRPGCoreAttributeId.Technique, 10)
            },
            katana.BasicAttack,
            redGoldSkill,
            "weapon:katana:redgold",
            Array.Empty<DRPGWeaponAffixDefinitionSO>());

        ConfigureProgressCapacityModifiers(
            signatureKatana,
            Array.Empty<DRPGProgressCapacityModifierDefinition>());
        ConfigureProgressCapacityModifiers(
            generalRevolver,
            new[]
            {
                new DRPGProgressCapacityModifierDefinition(freeShotCharge, 6f)
            });
        ConfigureProgressCapacityModifiers(
            redGoldKatana,
            Array.Empty<DRPGProgressCapacityModifierDefinition>());

        DRPGCharacterProgressionDefinitionSO employeeProgression =
            CreateCharacterProgression(
                EquipmentRoot + "/DRPGProgression_Employee.asset",
                "progression_employee_test",
                revolver);
        var employeeSerialized = new SerializedObject(employeeProgression);
        employeeSerialized.FindProperty("personnelKind").enumValueIndex =
            (int)DRPGPersonnelKind.Employee;
        employeeSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(employeeProgression);
        SetReference(employee, "progressionDefinition", employeeProgression);

        return new EquipmentSampleSet
        {
            Weapons = new[] { signatureKatana, generalRevolver, redGoldKatana },
            Outfits = new[] { outfit },
            Accessories = accessories,
            Traits = new[] { outfitTrait },
            Affixes = new[] { affix },
            Cards = new[] { attributeCard, conditionalCard },
            Attachments = new[] { grip, action },
            Characters = new[]
            {
                StartingCharacter(
                    melee,
                    "weapon:shu:signature",
                    signatureKatana,
                    outfit,
                    accessories),
                StartingCharacter(
                    gun,
                    "weapon:ankalute:revolver",
                    generalRevolver,
                    outfit,
                    accessories),
                StartingCharacter(
                    employee,
                    "weapon:employee:revolver",
                    generalRevolver,
                    outfit,
                    accessories)
            },
            BackpackWeapons = new[]
            {
                StartingBackpackWeapon(
                    "weapon:backpack:redgold_katana",
                    redGoldKatana,
                    new[] { affix },
                    new[] { attributeCard, conditionalCard },
                    new[] { grip, action }),
                StartingBackpackWeapon(
                    "weapon:backpack:revolver",
                    generalRevolver,
                    Array.Empty<DRPGWeaponAffixDefinitionSO>(),
                    Array.Empty<DRPGWeaponCardDefinitionSO>(),
                    new[] { action })
            }
        };
    }

    private static DRPGWeaponDefinitionSO ConfigureWeapon(
        string path,
        string id,
        string displayName,
        string description,
        DRPGEquipmentRarity rarity,
        DRPGWeaponOwnershipKind ownership,
        string signatureCharacterId,
        IReadOnlyList<DRPGProfessionSO> professions,
        float weight,
        int durability,
        IReadOnlyList<DRPGAttributeRequirement> requirements,
        DRPGSkillSO basicAttack,
        DRPGSkillSO intrinsicSkill,
        string presentationKey,
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> fixedAffixes)
    {
        DRPGWeaponDefinitionSO weapon =
            LoadOrCreate<DRPGWeaponDefinitionSO>(path);
        var serialized = new SerializedObject(weapon);
        serialized.FindProperty("weaponId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
        serialized.FindProperty("ownershipKind").enumValueIndex = (int)ownership;
        serialized.FindProperty("signatureCharacterId").stringValue =
            signatureCharacterId ?? string.Empty;
        SetObjectReferences(
            serialized.FindProperty("compatibleProfessions"),
            professions);
        serialized.FindProperty("weight").floatValue = weight;
        serialized.FindProperty("baseDurability").intValue = durability;
        serialized.FindProperty("initialDurabilityFloorRatio").floatValue =
            ownership == DRPGWeaponOwnershipKind.Signature ? 1f : 0.78f;
        SerializedProperty requirementArray = serialized.FindProperty("requirements");
        requirementArray.arraySize = requirements.Count;
        for (int i = 0; i < requirements.Count; i++)
        {
            SerializedProperty entry = requirementArray.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("attribute").enumValueIndex =
                (int)requirements[i].Attribute;
            entry.FindPropertyRelative("minimumValue").intValue =
                requirements[i].MinimumValue;
        }
        serialized.FindProperty("basicAttackOverride").objectReferenceValue =
            basicAttack;
        serialized.FindProperty("intrinsicSkill").objectReferenceValue =
            intrinsicSkill;
        serialized.FindProperty("attackPresentationKey").stringValue =
            presentationKey ?? string.Empty;
        SetObjectReferences(
            serialized.FindProperty("fixedAffixes"),
            fixedAffixes);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
        weapon.ValidateOrThrow();
        return weapon;
    }

    private static void ConfigureProgressCapacityModifiers(
        DRPGWeaponDefinitionSO weapon,
        IReadOnlyList<DRPGProgressCapacityModifierDefinition> modifiers)
    {
        var serialized = new SerializedObject(weapon);
        SerializedProperty entries =
            serialized.FindProperty("progressCapacityModifiers");
        entries.arraySize = modifiers != null ? modifiers.Count : 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            DRPGProgressCapacityModifierDefinition modifier = modifiers[i];
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("resource").objectReferenceValue =
                modifier.Resource;
            entry.FindPropertyRelative("resourceId").stringValue =
                modifier.ResourceId;
            entry.FindPropertyRelative("capacityModifier").floatValue =
                modifier.CapacityModifier;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
        weapon.ValidateOrThrow();
    }

    private static DRPGPassiveModifierSO CreateEquipmentPassive(
        string fileName,
        string id,
        string displayName,
        string description,
        float damage,
        float posture,
        float cadence)
    {
        DRPGPassiveModifierSO passive =
            LoadOrCreate<DRPGPassiveModifierSO>(EquipmentRoot + "/" + fileName);
        var serialized = new SerializedObject(passive);
        serialized.FindProperty("modifierId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        SetFloat(serialized, "damageMultiplier", damage);
        SetFloat(serialized, "maxHpMultiplier", 1f);
        SetFloat(serialized, "agilityMultiplier", 1f);
        SetFloat(serialized, "recoveryDelayMultiplier", 1f);
        SetFloat(serialized, "maxPostureMultiplier", posture);
        SetFloat(serialized, "postureRecoveryMultiplier", 1f);
        SetFloat(serialized, "freeStrikeCadenceMultiplier", cadence);
        SetFloat(serialized, "freeStrikePostureCostMultiplier", 1f);
        SetFloat(serialized, "freeStrikeReachMultiplier", 1f);
        SetFloat(serialized, "guardWindowMultiplier", 1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(passive);
        return passive;
    }

    private static void ConfigureNamedAsset(
        UnityEngine.Object asset,
        string idProperty,
        string id,
        string displayName,
        string description,
        string effectKey)
    {
        var serialized = new SerializedObject(asset);
        serialized.FindProperty(idProperty).stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        SerializedProperty descriptionProperty =
            serialized.FindProperty("description");
        if (descriptionProperty != null)
            descriptionProperty.stringValue = description;
        SerializedProperty effect = serialized.FindProperty("effectKey");
        if (effect != null)
            effect.stringValue = effectKey ?? string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void ConfigureWeaponCard(
        DRPGWeaponCardDefinitionSO card,
        string id,
        string displayName,
        string description,
        int cost,
        string effectKey,
        DRPGSkillSO grantedSkill,
        IReadOnlyList<DRPGPassiveModifierSO> passives)
    {
        ConfigureNamedAsset(
            card, "cardId", id, displayName, description, effectKey);
        var serialized = new SerializedObject(card);
        serialized.FindProperty("cost").intValue = cost;
        serialized.FindProperty("grantedSkill").objectReferenceValue = grantedSkill;
        SetObjectReferences(serialized.FindProperty("passiveModifiers"), passives);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
    }

    private static void ConfigureAttachment(
        DRPGWeaponAttachmentDefinitionSO attachment,
        string id,
        string displayName,
        string description,
        IReadOnlyList<DRPGPassiveModifierSO> passives)
    {
        ConfigureNamedAsset(
            attachment,
            "attachmentId",
            id,
            displayName,
            description,
            null);
        var serialized = new SerializedObject(attachment);
        SetObjectReferences(serialized.FindProperty("passiveModifiers"), passives);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attachment);
    }

    private static DRPGAccessoryDefinitionSO CreateAccessory(
        string id,
        string displayName,
        float weight)
    {
        DRPGAccessoryDefinitionSO accessory =
            LoadOrCreate<DRPGAccessoryDefinitionSO>(
                EquipmentRoot + "/DRPGAccessory_" + id + ".asset");
        var serialized = new SerializedObject(accessory);
        serialized.FindProperty("accessoryId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("weight").floatValue = weight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(accessory);
        return accessory;
    }

    private static DRPGStartingCharacterEquipment StartingCharacter(
        DRPGCombatantProfileSO profile,
        string instanceId,
        DRPGWeaponDefinitionSO weapon,
        DRPGOutfitDefinitionSO outfit,
        IReadOnlyList<DRPGAccessoryDefinitionSO> accessories)
    {
        return new DRPGStartingCharacterEquipment
        {
            profile = profile,
            equippedInstanceId = instanceId,
            equippedWeapon = weapon,
            outfit = outfit,
            accessories = new[]
            {
                accessories.Count > 0 ? accessories[0] : null,
                accessories.Count > 1 ? accessories[1] : null,
                accessories.Count > 2 ? accessories[2] : null
            }
        };
    }

    private static DRPGStartingBackpackWeapon StartingBackpackWeapon(
        string instanceId,
        DRPGWeaponDefinitionSO weapon,
        DRPGWeaponAffixDefinitionSO[] affixes,
        DRPGWeaponCardDefinitionSO[] cards,
        DRPGWeaponAttachmentDefinitionSO[] attachments)
    {
        return new DRPGStartingBackpackWeapon
        {
            instanceId = instanceId,
            definition = weapon,
            affixes = affixes,
            cards = cards,
            attachments = attachments
        };
    }

    private static void ConfigureOpenSceneEquipment(EquipmentSampleSet samples)
    {
        GameFlowRoot flowRoot =
            UnityEngine.Object.FindObjectOfType<GameFlowRoot>(true);
        if (flowRoot == null || !flowRoot.gameObject.scene.IsValid() ||
            !flowRoot.gameObject.scene.isLoaded)
        {
            Debug.LogWarning(
                "[GameMenuInstaller] Open scene has no GameFlowRoot; " +
                "sample equipment assets were created but not scene-bound.");
            return;
        }
        if (flowRoot.gameObject.scene.isDirty)
        {
            Debug.LogWarning(
                "[GameMenuInstaller] Scene already had unsaved changes; " +
                "equipment bootstrap binding was skipped to preserve them.");
            return;
        }

        DRPGEquipmentRuntimeRoot runtime =
            flowRoot.GetComponent<DRPGEquipmentRuntimeRoot>();
        if (runtime == null)
            runtime = flowRoot.gameObject.AddComponent<DRPGEquipmentRuntimeRoot>();
        var serialized = new SerializedObject(runtime);
        serialized.FindProperty("backpackSlotCapacity").intValue = 24;
        serialized.FindProperty("backpackWeightCapacity").floatValue = 80f;
        ConfigureStartingCharacters(
            serialized.FindProperty("characters"),
            samples.Characters);
        ConfigureStartingBackpack(
            serialized.FindProperty("backpackWeapons"),
            samples.BackpackWeapons);
        SetObjectReferences(
            serialized.FindProperty("weaponDefinitions"), samples.Weapons);
        SetObjectReferences(
            serialized.FindProperty("outfitDefinitions"), samples.Outfits);
        SetObjectReferences(
            serialized.FindProperty("accessoryDefinitions"), samples.Accessories);
        SetObjectReferences(
            serialized.FindProperty("traitDefinitions"), samples.Traits);
        SetObjectReferences(
            serialized.FindProperty("affixDefinitions"), samples.Affixes);
        SetObjectReferences(
            serialized.FindProperty("cardDefinitions"), samples.Cards);
        SetObjectReferences(
            serialized.FindProperty("attachmentDefinitions"), samples.Attachments);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runtime);

        var flowSerialized = new SerializedObject(flowRoot);
        SerializedProperty equipmentReference =
            flowSerialized.FindProperty("<drpgEquipment>k__BackingField");
        if (equipmentReference != null)
        {
            equipmentReference.objectReferenceValue = runtime;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flowRoot);
        }
        EditorSceneManager.MarkSceneDirty(flowRoot.gameObject.scene);
        EditorSceneManager.SaveScene(flowRoot.gameObject.scene);
    }

    private static void ConfigureStartingCharacters(
        SerializedProperty array,
        IReadOnlyList<DRPGStartingCharacterEquipment> values)
    {
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            DRPGStartingCharacterEquipment value = values[i];
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("profile").objectReferenceValue = value.profile;
            entry.FindPropertyRelative("equippedInstanceId").stringValue =
                value.equippedInstanceId;
            entry.FindPropertyRelative("equippedWeapon").objectReferenceValue =
                value.equippedWeapon;
            entry.FindPropertyRelative("outfit").objectReferenceValue = value.outfit;
            SetObjectReferences(
                entry.FindPropertyRelative("accessories"),
                value.accessories);
        }
    }

    private static void ConfigureStartingBackpack(
        SerializedProperty array,
        IReadOnlyList<DRPGStartingBackpackWeapon> values)
    {
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            DRPGStartingBackpackWeapon value = values[i];
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("instanceId").stringValue = value.instanceId;
            entry.FindPropertyRelative("definition").objectReferenceValue =
                value.definition;
            SetObjectReferences(entry.FindPropertyRelative("affixes"), value.affixes);
            SetObjectReferences(entry.FindPropertyRelative("cards"), value.cards);
            SetObjectReferences(
                entry.FindPropertyRelative("attachments"), value.attachments);
        }
    }

    private static void SetObjectReferences<T>(
        SerializedProperty array,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        array.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < array.arraySize; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void ConfigureParty(
        DRPGCombatantProfileSO melee,
        DRPGCombatantProfileSO gun,
        DRPGCombatantProfileSO alchemist)
    {
        DRPGPartySetupSO party = Require<DRPGPartySetupSO>(PartyPath);
        var serialized = new SerializedObject(party);
        serialized.FindProperty("protagonist").objectReferenceValue = melee;
        SerializedProperty members = serialized.FindProperty("partyMembers");
        members.arraySize = 2;
        members.GetArrayElementAtIndex(0).objectReferenceValue = gun;
        members.GetArrayElementAtIndex(1).objectReferenceValue = alchemist;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(party);
    }

    private static void ConfigureEncounterEscape()
    {
        DRPGEncounterDefinitionSO encounter =
            Require<DRPGEncounterDefinitionSO>(EncounterPath);
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("allowEscape").boolValue = true;
        serialized.FindProperty("baseEscapeChance").floatValue = 0.35f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
    }

    private static void UpgradeBattleCommandPrefab(TMP_FontAsset font)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BattlePrefabPath);
        try
        {
            BattleSkillMenu skillMenu =
                root.GetComponentInChildren<BattleSkillMenu>(true);
            DRPGBattleUGUISurface surface =
                root.GetComponentInChildren<DRPGBattleUGUISurface>(true);
            DRPGBattleInteractionController interaction =
                root.GetComponentInChildren<DRPGBattleInteractionController>(true);
            if (skillMenu == null || surface == null || interaction == null)
            {
                throw new InvalidOperationException(
                    "Battle prefab is missing its existing UGUI interaction stack.");
            }

            Transform existing = root.transform.Find("BattleCommandSystem");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            Transform existingRecovery = root.transform.Find("BattleRecoverySystem");
            if (existingRecovery != null)
                UnityEngine.Object.DestroyImmediate(existingRecovery.gameObject);
            Transform oldBack = skillMenu.transform.Find("CommandBackButton");
            if (oldBack != null)
                UnityEngine.Object.DestroyImmediate(oldBack.gameObject);

            GameObject systemObject = CreateUIObject(
                "BattleCommandSystem",
                root.transform);
            Stretch(systemObject.GetComponent<RectTransform>());
            BattleCommandMenu commandMenu =
                systemObject.AddComponent<BattleCommandMenu>();
            RectTransform skillRect = skillMenu.transform as RectTransform;
            UpgradeDynamicSkillMenu(skillMenu, font);

            Image commandPanel = CreateImage(
                "CommandRoot",
                systemObject.transform,
                new Color(0.025f, 0.028f, 0.045f, 0.96f));
            CopyRect(skillRect, commandPanel.rectTransform);
            TMP_Text commandTitle = CreateText(
                "Title", commandPanel.transform, font, "BATTLE COMMAND", 28f,
                TextAlignmentOptions.Left, Color.white);
            SetRect(commandTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(-40f, 48f));

            Button basic = CreateCommandButton(
                "BasicAttack", commandPanel.transform, font, "普通攻击", 0);
            Button skill = CreateCommandButton(
                "Skill", commandPanel.transform, font, "技能", 1);
            Button item = CreateCommandButton(
                "Item", commandPanel.transform, font, "道具", 2);
            Button weapon = CreateCommandButton(
                "Weapon", commandPanel.transform, font, "武器", 3);
            Button defend = CreateCommandButton(
                "Defend", commandPanel.transform, font, "防御", 4);
            Button escape = CreateCommandButton(
                "Escape", commandPanel.transform, font, "逃跑", 5);

            Button skillBack = CreateButton(
                "CommandBackButton",
                skillMenu.transform,
                font,
                "◀ 返回指令",
                new Color(0.22f, 0.05f, 0.12f, 0.98f));
            SetRect(skillBack.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(-36f, 48f));
            skillBack.name = "SkillBackButton";

            Image itemPanel = CreateImage(
                "ItemRoot",
                systemObject.transform,
                new Color(0.025f, 0.028f, 0.045f, 0.96f));
            CopyRect(skillRect, itemPanel.rectTransform);
            TMP_Text itemTitle = CreateText(
                "Title", itemPanel.transform, font, "BATTLE ITEMS", 28f,
                TextAlignmentOptions.Left, Color.white);
            SetRect(itemTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(-40f, 48f));
            var itemButtons = new List<Button>();
            var itemLabels = new List<TMP_Text>();
            for (int i = 0; i < 4; i++)
            {
                Button entry = CreateCommandButton(
                    "ItemEntry_" + i,
                    itemPanel.transform,
                    font,
                    "ITEM",
                    i);
                itemButtons.Add(entry);
                itemLabels.Add(entry.transform.Find("Label")
                    .GetComponent<TMP_Text>());
            }
            TMP_Text empty = CreateText(
                "ItemEmpty", itemPanel.transform, font, "没有可在战斗中使用的道具", 24f,
                TextAlignmentOptions.Center, new Color(0.58f, 0.62f, 0.70f));
            SetRect(empty.rectTransform, Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(-36f, -160f));
            Button itemBack = CreateButton(
                "ItemBackButton", itemPanel.transform, font, "◀ 返回指令",
                new Color(0.22f, 0.05f, 0.12f, 0.98f));
            SetRect(itemBack.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(-36f, 48f));

            BattleWeaponMenu weaponMenu = CreateBattleWeaponMenu(
                systemObject.transform,
                skillRect,
                font,
                out GameObject weaponPanel,
                out Button weaponBack);
            BattleRecoveryBagView recoveryBagView =
                CreateBattleRecoverySystem(root.transform, font);

            SetReference(commandMenu, "commandRoot", commandPanel.gameObject);
            SetReference(commandMenu, "skillRoot", skillMenu.gameObject);
            SetReference(commandMenu, "itemRoot", itemPanel.gameObject);
            SetReference(commandMenu, "weaponRoot", weaponPanel);
            SetReference(commandMenu, "basicAttackButton", basic);
            SetReference(commandMenu, "skillButton", skill);
            SetReference(commandMenu, "itemButton", item);
            SetReference(commandMenu, "weaponButton", weapon);
            SetReference(commandMenu, "defendButton", defend);
            SetReference(commandMenu, "escapeButton", escape);
            SetReference(commandMenu, "skillBackButton", skillBack);
            SetReference(commandMenu, "itemBackButton", itemBack);
            SetReference(commandMenu, "weaponBackButton", weaponBack);
            SetReference(commandMenu, "weaponMenu", weaponMenu);
            SetObjectArray(commandMenu, "itemButtons", itemButtons);
            SetObjectArray(commandMenu, "itemLabels", itemLabels);
            SetReference(commandMenu, "itemEmptyText", empty);
            SetReference(surface, "commandMenu", commandMenu);
            SetReference(surface, "weaponMenu", weaponMenu);
            SetReference(surface, "recoveryBagView", recoveryBagView);
            SetReference(interaction, "commandMenu", commandMenu);
            SetReference(interaction, "weaponMenu", weaponMenu);

            commandPanel.gameObject.SetActive(true);
            skillMenu.gameObject.SetActive(false);
            skillBack.gameObject.SetActive(true);
            itemPanel.gameObject.SetActive(false);
            weaponPanel.SetActive(false);
            systemObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, BattlePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Button CreateCommandButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        int index)
    {
        Button button = CreateButton(
            name,
            parent,
            font,
            label,
            index == 0
                ? new Color(0.72f, 0.06f, 0.30f, 0.98f)
                : new Color(0.12f, 0.05f, 0.12f, 0.98f));
        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = Color.white;
        Color normal = index == 0
            ? new Color(0.72f, 0.06f, 0.30f, 0.98f)
            : new Color(0.12f, 0.05f, 0.12f, 0.98f);
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = new Color(1f, 0.20f, 0.52f, 1f);
        colors.pressedColor = new Color(0.45f, 0.02f, 0.16f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.07f, 0.07f, 0.09f, 0.62f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = colors;
        SetRect(button.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -84f - index * 60f),
            new Vector2(-36f, 52f));
        return button;
    }

    private static void UpgradeDynamicSkillMenu(
        BattleSkillMenu skillMenu,
        TMP_FontAsset font)
    {
        for (int i = 0; i < 12; i++)
        {
            Transform legacy = skillMenu.transform.Find("SkillButton_" + i);
            if (legacy != null)
                UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }
        Transform priorViewport = skillMenu.transform.Find("SkillViewport");
        if (priorViewport != null)
            UnityEngine.Object.DestroyImmediate(priorViewport.gameObject);

        Image viewport = CreateImage(
            "SkillViewport",
            skillMenu.transform,
            new Color(0.025f, 0.020f, 0.032f, 0.62f));
        SetRect(viewport.rectTransform, new Vector2(0f, 0f), Vector2.one,
            Vector2.one * 0.5f, new Vector2(0f, -20f), new Vector2(-36f, -150f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateUIObject("Content", viewport.transform)
            .GetComponent<RectTransform>();
        SetRect(content, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        BattleSkillMenuEntryView template = CreateBattleSkillEntry(
            "SkillEntryTemplate", content, font);
        template.gameObject.SetActive(false);
        SetReference(skillMenu, "contentRoot", content);
        SetReference(skillMenu, "entryTemplate", template);
        SetObjectArray(skillMenu, "buttons", Array.Empty<Button>());
        SetObjectArray(skillMenu, "labels", Array.Empty<TMP_Text>());
        SetObjectArray(
            skillMenu,
            "rangeIndicators",
            Array.Empty<BattleFormationRangeIndicator>());
    }

    private static BattleSkillMenuEntryView CreateBattleSkillEntry(
        string name,
        Transform parent,
        TMP_FontAsset font)
    {
        Image background = CreateImage(
            name, parent, new Color(0.12f, 0.035f, 0.075f, 0.98f));
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 72f;
        element.minHeight = 72f;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.035f, 0.075f, 0.98f);
        colors.highlightedColor = new Color(0.90f, 0.08f, 0.38f, 1f);
        colors.pressedColor = new Color(0.48f, 0.02f, 0.18f, 1f);
        colors.disabledColor = new Color(0.06f, 0.05f, 0.07f, 0.56f);
        button.colors = colors;
        TMP_Text label = CreateText(
            "Label", background.transform, font, "SKILL", 24f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(-210f, -8f));

        GameObject rangeRoot = CreateUIObject(
            "FormationRange", background.transform);
        SetRect(rangeRoot.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-18f, -14f),
            new Vector2(126f, 24f));
        Image[] chips = new Image[3];
        for (int i = 0; i < chips.Length; i++)
        {
            chips[i] = CreateImage(
                "Range_" + (i + 1), rangeRoot.transform,
                DRPGFormationColorPalette.Resolve((DRPGFormationRow)i));
            SetRect(chips[i].rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(i * 42f, 0f),
                new Vector2(34f, 10f));
            chips[i].raycastTarget = false;
        }
        BattleFormationRangeIndicator range =
            rangeRoot.AddComponent<BattleFormationRangeIndicator>();
        SetReference(range, "frontChip", chips[0]);
        SetReference(range, "middleChip", chips[1]);
        SetReference(range, "rearChip", chips[2]);

        Image badge = CreateImage(
            "GrantedBadge", background.transform,
            new Color(1f, 0.62f, 0.08f, 0.96f));
        SetRect(badge.rectTransform, new Vector2(1f, 1f), Vector2.one,
            Vector2.one, new Vector2(-18f, -7f), new Vector2(126f, 26f));
        TMP_Text badgeText = CreateText(
            "Label", badge.transform, font, "武器固有", 16f,
            TextAlignmentOptions.Center, Ink);
        Stretch(badgeText.rectTransform, 3f);
        BattleSkillMenuEntryView entry =
            background.gameObject.AddComponent<BattleSkillMenuEntryView>();
        SetReference(entry, "button", button);
        SetReference(entry, "label", label);
        SetReference(entry, "rangeIndicator", range);
        SetReference(entry, "grantedBadge", badge.gameObject);
        SetReference(entry, "grantedBadgeText", badgeText);
        return entry;
    }

    private static BattleWeaponMenu CreateBattleWeaponMenu(
        Transform parent,
        RectTransform sourceRect,
        TMP_FontAsset font,
        out GameObject weaponPanel,
        out Button backButton)
    {
        Image panel = CreateImage(
            "WeaponRoot", parent, new Color(0.025f, 0.018f, 0.032f, 0.98f));
        CopyRect(sourceRect, panel.rectTransform);
        weaponPanel = panel.gameObject;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.82f, 0.04f, 0.29f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);
        TMP_Text title = CreateText(
            "Title", panel.transform, font, "WEAPON SWITCH / 武器切换", 27f,
            TextAlignmentOptions.Left, Pink);
        SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(-48f, 46f));
        TMP_Text subtitle = CreateText(
            "Subtitle", panel.transform, font,
            "切换武器 = 切换职业 = 消耗本回合", 18f,
            TextAlignmentOptions.Left, new Color(0.78f, 0.72f, 0.75f));
        SetRect(subtitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(-48f, 28f));

        Image viewport = CreateImage(
            "Viewport", panel.transform, new Color(0.01f, 0.01f, 0.018f, 0.72f));
        SetRect(viewport.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.84f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-32f, -8f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateUIObject("Content", viewport.transform)
            .GetComponent<RectTransform>();
        SetRect(content, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(9, 9, 9, 9);
        layout.spacing = 8f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;

        BattleWeaponMenuEntryView template = CreateBattleWeaponEntry(
            "WeaponEntryTemplate", content, font);
        template.gameObject.SetActive(false);
        TMP_Text empty = CreateText(
            "WeaponEmpty", panel.transform, font, "背包中没有可切换武器", 22f,
            TextAlignmentOptions.Center, new Color(0.62f, 0.58f, 0.62f));
        SetRect(empty.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 0.58f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-40f, -8f));
        TMP_Text feedback = CreateText(
            "WeaponFeedback", panel.transform, font, string.Empty, 18f,
            TextAlignmentOptions.Center, new Color(1f, 0.38f, 0.58f));
        SetRect(feedback.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.16f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-40f, -8f));
        backButton = CreateButton(
            "WeaponBackButton", panel.transform, font, "◀ 返回指令",
            new Color(0.22f, 0.05f, 0.12f, 0.98f));
        SetRect(backButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 20f),
            new Vector2(-32f, 44f));
        BattleWeaponMenu menu = panel.gameObject.AddComponent<BattleWeaponMenu>();
        SetReference(menu, "contentRoot", content);
        SetReference(menu, "entryTemplate", template);
        SetReference(menu, "emptyText", empty);
        SetReference(menu, "feedbackText", feedback);
        return menu;
    }

    private static BattleWeaponMenuEntryView CreateBattleWeaponEntry(
        string name,
        Transform parent,
        TMP_FontAsset font)
    {
        Image background = CreateImage(
            name, parent, new Color(0.105f, 0.025f, 0.060f, 0.98f));
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 106f;
        element.minHeight = 106f;
        CanvasGroup group = background.gameObject.AddComponent<CanvasGroup>();
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.105f, 0.025f, 0.060f, 0.98f);
        colors.highlightedColor = new Color(0.68f, 0.055f, 0.26f, 1f);
        colors.pressedColor = new Color(0.38f, 0.018f, 0.12f, 1f);
        colors.disabledColor = new Color(0.08f, 0.06f, 0.07f, 0.62f);
        button.colors = colors;
        Image rail = CreateImage("RarityRail", background.transform, Magenta);
        SetRect(rail.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(7f, 0f));
        rail.raycastTarget = false;
        Image icon = CreateImage("Icon", background.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(76f, 76f));
        icon.raycastTarget = false;
        TMP_Text weaponName = CreateText(
            "Name", background.transform, font, "WEAPON", 22f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(weaponName.rectTransform, new Vector2(0f, 0.47f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(112f, -10f), new Vector2(-130f, -8f));
        TMP_Text detail = CreateText(
            "Detail", background.transform, font, "职业 / 耐久 / 负重", 16f,
            TextAlignmentOptions.Left, new Color(0.80f, 0.75f, 0.78f));
        SetRect(detail.rectTransform, Vector2.zero, new Vector2(1f, 0.52f),
            new Vector2(0f, 0f), new Vector2(112f, 8f), new Vector2(-130f, -8f));
        TMP_Text state = CreateText(
            "State", background.transform, font, "可装备", 15f,
            TextAlignmentOptions.Right, Gold);
        SetRect(state.rectTransform, new Vector2(0.55f, 0f), Vector2.one,
            new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(-12f, -10f));
        BattleWeaponMenuEntryView entry =
            background.gameObject.AddComponent<BattleWeaponMenuEntryView>();
        SetReference(entry, "button", button);
        SetReference(entry, "icon", icon);
        SetReference(entry, "rarityRail", rail);
        SetReference(entry, "nameText", weaponName);
        SetReference(entry, "detailText", detail);
        SetReference(entry, "stateText", state);
        SetReference(entry, "canvasGroup", group);
        return entry;
    }

    private static BattleRecoveryBagView CreateBattleRecoverySystem(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject system = CreateUIObject("BattleRecoverySystem", parent);
        Stretch(system.GetComponent<RectTransform>());
        BattleRecoveryBagView view = system.AddComponent<BattleRecoveryBagView>();

        Image panel = CreateImage(
            "RecoveryPanel",
            system.transform,
            new Color(0.025f, 0.016f, 0.030f, 0.94f));
        SetRect(
            panel.rectTransform,
            new Vector2(0.69f, 0.18f),
            new Vector2(0.95f, 0.56f),
            Vector2.one * 0.5f,
            Vector2.zero,
            Vector2.zero);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.08f, 0.42f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);
        CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        Image titleRail = CreateImage(
            "TitleRail", panel.transform, new Color(0.62f, 0.025f, 0.22f, 1f));
        SetRect(titleRail.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 68f));
        titleRail.raycastTarget = false;
        TMP_Text title = CreateText(
            "Title", titleRail.transform, font,
            "FIELD RECOVERY / 战场回收", 24f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(title.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(-32f, -8f));

        Image viewport = CreateImage(
            "Viewport", panel.transform, new Color(0.01f, 0.01f, 0.018f, 0.76f));
        SetRect(viewport.rectTransform, new Vector2(0f, 0.30f),
            new Vector2(1f, 0.82f), Vector2.one * 0.5f,
            Vector2.zero, new Vector2(-34f, -10f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateUIObject("Content", viewport.transform)
            .GetComponent<RectTransform>();
        SetRect(content, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        BattleRecoveryItemView template = CreateBattleRecoveryItemEntry(
            "RecoveryItemTemplate", content, font);
        template.gameObject.SetActive(false);
        TMP_Text empty = CreateText(
            "Empty", viewport.transform, font, "战场上没有待回收装备", 20f,
            TextAlignmentOptions.Center, new Color(0.62f, 0.58f, 0.64f));
        Stretch(empty.rectTransform, 24f);

        Image dropZone = CreateImage(
            "BackpackDropZone", panel.transform,
            new Color(0.16f, 0.035f, 0.095f, 0.98f));
        SetRect(dropZone.rectTransform, new Vector2(0f, 0.075f),
            new Vector2(1f, 0.27f), Vector2.one * 0.5f,
            Vector2.zero, new Vector2(-34f, -4f));
        Outline dropOutline = dropZone.gameObject.AddComponent<Outline>();
        dropOutline.effectColor = new Color(1f, 0.26f, 0.57f, 0.96f);
        dropOutline.effectDistance = new Vector2(2f, -2f);
        TMP_Text dropLabel = CreateText(
            "Label", dropZone.transform, font,
            "拖入此处回收至团队背包  /  点击条目可快速回收", 18f,
            TextAlignmentOptions.Center, WarmWhite);
        Stretch(dropLabel.rectTransform, 10f);
        dropLabel.raycastTarget = false;

        TMP_Text feedback = CreateText(
            "Feedback", panel.transform, font, string.Empty, 17f,
            TextAlignmentOptions.Center, new Color(1f, 0.52f, 0.70f));
        SetRect(feedback.rectTransform, Vector2.zero,
            new Vector2(1f, 0.075f), Vector2.one * 0.5f,
            Vector2.zero, new Vector2(-30f, -3f));

        RectTransform dragLayer = CreateUIObject("DragLayer", system.transform)
            .GetComponent<RectTransform>();
        Stretch(dragLayer);
        dragLayer.SetAsLastSibling();

        SetReference(view, "panelRoot", panel.gameObject);
        SetReference(view, "contentRoot", content);
        SetReference(view, "itemTemplate", template);
        SetReference(view, "dropZone", dropZone.rectTransform);
        SetReference(view, "dragLayer", dragLayer);
        SetReference(view, "titleText", title);
        SetReference(view, "emptyText", empty);
        SetReference(view, "feedbackText", feedback);
        SetReference(view, "canvasGroup", panelGroup);
        panel.gameObject.SetActive(false);
        return view;
    }

    private static BattleRecoveryItemView CreateBattleRecoveryItemEntry(
        string name,
        Transform parent,
        TMP_FontAsset font)
    {
        Image background = CreateImage(
            name, parent, new Color(0.11f, 0.025f, 0.065f, 0.98f));
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 82f;
        element.minHeight = 82f;
        CanvasGroup group = background.gameObject.AddComponent<CanvasGroup>();
        Outline outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.82f, 0.05f, 0.31f, 0.86f);
        outline.effectDistance = new Vector2(2f, -2f);
        Image icon = CreateImage("Icon", background.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(18f, 0f), new Vector2(58f, 58f));
        icon.raycastTarget = false;
        TMP_Text label = CreateText(
            "Name", background.transform, font, "RECOVERY ITEM", 20f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(label.rectTransform, new Vector2(0f, 0f), Vector2.one,
            new Vector2(0f, 0.5f), new Vector2(94f, 0f),
            new Vector2(-112f, -10f));
        TMP_Text kind = CreateText(
            "Kind", background.transform, font, "武器", 16f,
            TextAlignmentOptions.Right, Gold);
        SetRect(kind.rectTransform, new Vector2(0.68f, 0f), Vector2.one,
            new Vector2(1f, 0.5f), new Vector2(-14f, 0f),
            new Vector2(-12f, -10f));
        BattleRecoveryItemView view =
            background.gameObject.AddComponent<BattleRecoveryItemView>();
        SetReference(view, "icon", icon);
        SetReference(view, "label", label);
        SetReference(view, "kindLabel", kind);
        SetReference(view, "canvasGroup", group);
        return view;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void CreateMenuPrefab(
        TMP_FontAsset font,
        DRPGCombatantProfileSO gun,
        DRPGCombatantProfileSO melee)
    {
        GameObject root = new GameObject(
            "PF_GameMenu",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(GameMenuController));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The package canvas (1000) and battle canvas (4500) may both be
            // alive when this modal opens. The system menu must own the top
            // presentation layer so no unrelated controls leak through it.
            canvas.sortingOrder = 6000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();

            Image backdrop = CreateImage("Backdrop", root.transform, Wine);
            Stretch(backdrop.rectTransform);
            Image leftShadow = CreateImage(
                "LeftShadow", root.transform, new Color(0f, 0f, 0f, 0.56f));
            SetRect(leftShadow.rectTransform, Vector2.zero, new Vector2(0.22f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftShadow.raycastTarget = false;
            Image topSlash = CreateImage(
                "TopSlash", root.transform, new Color(0f, 0f, 0f, 0.72f));
            SetRect(topSlash.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(0f, 112f));
            topSlash.raycastTarget = false;

            Image frameImage = CreateImage("Frame", root.transform, Panel);
            Stretch(frameImage.rectTransform, 42f);
            Outline frameOutline = frameImage.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = Magenta;
            frameOutline.effectDistance = new Vector2(2f, -2f);
            RectTransform frame = frameImage.rectTransform;

            Image headerRail = CreateImage("HeaderRail", frame, Ink);
            SetRect(headerRail.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 158f));
            headerRail.raycastTarget = false;
            Image headerAccent = CreateImage("HeaderAccent", headerRail.transform, Magenta);
            SetRect(headerAccent.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 7f));
            headerAccent.raycastTarget = false;
            TMP_Text brand = CreateText(
                "Brand", headerRail.transform, font, "ANKALUTE // ARCHIVE", 24f,
                TextAlignmentOptions.Left, Pink);
            SetRect(brand.rectTransform, new Vector2(0f, 1f), new Vector2(0.25f, 1f),
                new Vector2(0f, 1f), new Vector2(34f, -18f), new Vector2(-20f, 36f));

            TMP_Text name = CreateText(
                "CharacterName", frame, font, "CHARACTER", 52f,
                TextAlignmentOptions.Left, WarmWhite);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0.34f, 1f),
                new Vector2(0f, 1f), new Vector2(54f, -62f), new Vector2(-20f, 74f));
            TMP_Text pageTitle = CreateText(
                "PageTitle", frame, font, "CHARACTER STATUS", 29f,
                TextAlignmentOptions.Right, Pink);
            SetRect(pageTitle.rectTransform, new Vector2(0.68f, 1f), Vector2.one,
                new Vector2(1f, 1f), new Vector2(-52f, -26f), new Vector2(-20f, 42f));

            Button previous = CreateButton(
                "PreviousCharacter", frame, font, "◀", Ink);
            SetRect(previous.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -124f),
                new Vector2(66f, 48f));
            Button next = CreateButton("NextCharacter", frame, font, "▶", Ink);
            SetRect(next.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -124f),
                new Vector2(66f, 48f));
            Button statusButton = CreateNavigationButton(
                "StatusButton", frame, font, "STATUS / 状态", 390f);
            Button treeButton = CreateNavigationButton(
                "SkillTreeButton", frame, font, "SKILL TREE / 技能树", 652f);
            Button loadoutButton = CreateNavigationButton(
                "LoadoutButton", frame, font, "LOADOUT / 技能配置", 914f);
            Button equipmentButton = CreateNavigationButton(
                "EquipmentButton", frame, font, "EQUIPMENT / 装备", 1176f);
            Button closeButton = CreateButton(
                "CloseButton", frame, font, "ESC / RMB  BACK",
                new Color(0.42f, 0.02f, 0.13f));
            SetRect(closeButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one,
                Vector2.one, new Vector2(-48f, -118f), new Vector2(250f, 50f));

            GameObject statusRoot = CreatePage("StatusPage", frame);
            CharacterStatusPageView statusView =
                CreateStatusPage(statusRoot.transform, font);
            GameObject treeRoot = CreatePage("SkillTreePage", frame);
            SkillTreePageView treeView = CreateSkillTreePage(treeRoot.transform, font);
            GameObject loadoutRoot = CreatePage("LoadoutPage", frame);
            SkillLoadoutPageView loadoutView =
                CreateLoadoutPage(loadoutRoot.transform, font, root.transform);
            GameObject equipmentRoot = CreatePage("EquipmentPage", frame);
            EquipmentPageView equipmentView =
                CreateEquipmentPage(equipmentRoot.transform, font);

            GameMenuTooltipView tooltip = CreateTooltip(root.transform, font);
            RectTransform dragLayer = CreateUIObject("DragLayer", root.transform)
                .GetComponent<RectTransform>();
            Stretch(dragLayer);
            dragLayer.SetAsLastSibling();
            SetReference(loadoutView, "dragLayer", dragLayer);

            GameMenuController controller = root.GetComponent<GameMenuController>();
            SetReference(controller, "canvasGroup", canvasGroup);
            SetReference(controller, "frame", frame);
            SetReference(controller, "characterNameText", name);
            SetReference(controller, "pageTitleText", pageTitle);
            SetReference(controller, "previousCharacterButton", previous);
            SetReference(controller, "nextCharacterButton", next);
            SetReference(controller, "statusButton", statusButton);
            SetReference(controller, "skillTreeButton", treeButton);
            SetReference(controller, "loadoutButton", loadoutButton);
            SetReference(controller, "equipmentButton", equipmentButton);
            SetReference(controller, "closeButton", closeButton);
            SetReference(controller, "statusPage", statusView);
            SetReference(controller, "skillTreePage", treeView);
            SetReference(controller, "loadoutPage", loadoutView);
            SetReference(controller, "equipmentPage", equipmentView);
            SetReference(controller, "tooltip", tooltip);
            SetObjectArray(controller, "selectableProfiles", new[] { gun, melee });
            SetInt(controller, "defaultProfileIndex", 0);

            statusRoot.SetActive(true);
            treeRoot.SetActive(false);
            loadoutRoot.SetActive(false);
            equipmentRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CharacterStatusPageView CreateStatusPage(
        Transform root,
        TMP_FontAsset font)
    {
        CharacterStatusPageView view = root.gameObject.AddComponent<CharacterStatusPageView>();
        Image infoPanel = CreateImage("GrowthPanel", root, Ink);
        SetRect(infoPanel.rectTransform, new Vector2(0.02f, 0.04f),
            new Vector2(0.66f, 0.96f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Image infoAccent = CreateImage("Accent", infoPanel.transform, Magenta);
        SetRect(infoAccent.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
        infoAccent.raycastTarget = false;

        Image portraitPanel = CreateImage(
            "PortraitPanel", root, new Color(0.08f, 0.01f, 0.035f, 0.55f));
        SetRect(portraitPanel.rectTransform, new Vector2(0.63f, 0f), Vector2.one,
            Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Image portrait = CreateImage("Portrait", portraitPanel.transform, Color.white);
        Stretch(portrait.rectTransform, 12f);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Image portraitLine = CreateImage("PortraitLine", portraitPanel.transform, Magenta);
        SetRect(portraitLine.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
        portraitLine.raycastTarget = false;

        TMP_Text profession = CreateText(
            "Profession", infoPanel.transform, font, "PROFESSION", 45f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(profession.rectTransform, new Vector2(0.04f, 0.84f),
            new Vector2(0.55f, 0.97f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        TMP_Text level = CreateText(
            "Level", infoPanel.transform, font, "LV 01", 32f,
            TextAlignmentOptions.Right, Gold);
        SetRect(level.rectTransform, new Vector2(0.55f, 0.86f),
            new Vector2(0.76f, 0.97f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        TMP_Text experience = CreateText(
            "Experience", infoPanel.transform, font, "EXP 0 / 0", 22f,
            TextAlignmentOptions.Right, Pink);
        SetRect(experience.rectTransform, new Vector2(0.75f, 0.87f),
            new Vector2(0.97f, 0.96f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        TMP_Text stats = CreateText(
            "Stats", infoPanel.transform, font, "HP / MP / POSTURE", 25f,
            TextAlignmentOptions.TopLeft, WarmWhite);
        SetRect(stats.rectTransform, new Vector2(0.04f, 0.70f),
            new Vector2(0.56f, 0.84f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        TMP_Text derived = CreateText(
            "DerivedPreview", infoPanel.transform, font,
            "SPEED / ACC / WEAK / GUARD", 22f,
            TextAlignmentOptions.TopLeft, new Color(0.84f, 0.78f, 0.80f));
        SetRect(derived.rectTransform, new Vector2(0.56f, 0.69f),
            new Vector2(0.97f, 0.84f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        TMP_Text attributePoints = CreateText(
            "AttributePoints", infoPanel.transform, font, "ATTRIBUTE POINTS 00", 25f,
            TextAlignmentOptions.Left, Gold);
        SetRect(attributePoints.rectTransform, new Vector2(0.04f, 0.61f),
            new Vector2(0.31f, 0.69f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        TMP_Text skillPoints = CreateText(
            "SkillPoints", infoPanel.transform, font, "SKILL POINTS 00", 25f,
            TextAlignmentOptions.Left, Pink);
        SetRect(skillPoints.rectTransform, new Vector2(0.32f, 0.61f),
            new Vector2(0.55f, 0.69f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        TMP_Text breakthrough = CreateText(
            "Breakthrough", infoPanel.transform, font, "NORMAL CAP // 80", 19f,
            TextAlignmentOptions.Right, new Color(0.82f, 0.68f, 0.72f, 1f));
        SetRect(breakthrough.rectTransform, new Vector2(0.56f, 0.61f),
            new Vector2(0.94f, 0.69f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

        var valueTexts = new TMP_Text[6];
        var plusButtons = new Button[6];
        var minusButtons = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            int column = i / 3;
            int row = i % 3;
            float xMin = 0.04f + column * 0.29f;
            float yMax = 0.59f - row * 0.105f;
            Image rowPanel = CreateImage(
                "AttributeRow_" + i,
                infoPanel.transform,
                new Color(0.11f, 0.035f, 0.065f, 0.96f));
            SetRect(rowPanel.rectTransform, new Vector2(xMin, yMax - 0.085f),
                new Vector2(xMin + 0.27f, yMax), Vector2.one * 0.5f,
                Vector2.zero, Vector2.zero);
            valueTexts[i] = CreateText(
                "Value", rowPanel.transform, font, "ATTRIBUTE 10", 24f,
                TextAlignmentOptions.Left, WarmWhite);
            SetRect(valueTexts[i].rectTransform, Vector2.zero, new Vector2(0.62f, 1f),
                new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(-8f, 0f));
            minusButtons[i] = CreateButton(
                "Minus", rowPanel.transform, font, "−", Ink);
            SetRect(minusButtons[i].GetComponent<RectTransform>(),
                new Vector2(0.64f, 0.12f), new Vector2(0.79f, 0.88f),
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            plusButtons[i] = CreateButton(
                "Plus", rowPanel.transform, font, "+", Magenta);
            SetRect(plusButtons[i].GetComponent<RectTransform>(),
                new Vector2(0.81f, 0.12f), new Vector2(0.96f, 0.88f),
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        }

        Button reset = CreateButton(
            "ResetDraft", infoPanel.transform, font,
            "RESET / 重置预览", new Color(0.18f, 0.04f, 0.08f, 1f));
        SetRect(reset.GetComponent<RectTransform>(), new Vector2(0.04f, 0.17f),
            new Vector2(0.30f, 0.245f), Vector2.one * 0.5f,
            Vector2.zero, Vector2.zero);
        Button confirm = CreateButton(
            "ConfirmAttributes", infoPanel.transform, font,
            "CONFIRM / 确认加点", Magenta);
        SetRect(confirm.GetComponent<RectTransform>(), new Vector2(0.32f, 0.17f),
            new Vector2(0.60f, 0.245f), Vector2.one * 0.5f,
            Vector2.zero, Vector2.zero);

        var skillTexts = new TMP_Text[4];
        for (int i = 0; i < skillTexts.Length; i++)
        {
            Image slot = CreateImage(
                "EquippedSkill_" + i, infoPanel.transform,
                i == 0
                    ? new Color(0.45f, 0.015f, 0.14f, 1f)
                    : new Color(0.08f, 0.035f, 0.06f, 0.98f));
            SetRect(slot.rectTransform, new Vector2(0.64f, 0.46f - i * 0.105f),
                new Vector2(0.96f, 0.54f - i * 0.105f), Vector2.one * 0.5f,
                Vector2.zero, Vector2.zero);
            skillTexts[i] = CreateText(
                "Label", slot.transform, font, "— EMPTY —", 22f,
                TextAlignmentOptions.Center, WarmWhite);
            Stretch(skillTexts[i].rectTransform, 10f);
        }
        SetReference(view, "portrait", portrait);
        SetReference(view, "professionText", profession);
        SetReference(view, "statText", stats);
        SetReference(view, "levelText", level);
        SetReference(view, "experienceText", experience);
        SetReference(view, "attributePointsText", attributePoints);
        SetReference(view, "skillPointsText", skillPoints);
        SetReference(view, "breakthroughText", breakthrough);
        SetReference(view, "derivedPreviewText", derived);
        SetObjectArray(view, "attributeValueTexts", valueTexts);
        SetObjectArray(view, "attributePlusButtons", plusButtons);
        SetObjectArray(view, "attributeMinusButtons", minusButtons);
        SetReference(view, "confirmAttributesButton", confirm);
        SetReference(view, "resetDraftButton", reset);
        SetObjectArray(view, "equippedSkillTexts", skillTexts);
        return view;
    }

    private static SkillTreePageView CreateSkillTreePage(
        Transform root,
        TMP_FontAsset font)
    {
        SkillTreePageView view = root.gameObject.AddComponent<SkillTreePageView>();
        Image treeField = CreateImage("TreeField", root, Ink);
        SetRect(treeField.rectTransform, new Vector2(0.15f, 0.16f),
            new Vector2(0.89f, 0.83f), Vector2.one * 0.5f,
            Vector2.zero, Vector2.zero);
        treeField.raycastTarget = false;
        Image treeAccent = CreateImage("TreeAccent", treeField.transform, Magenta);
        SetRect(treeAccent.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
        treeAccent.raycastTarget = false;
        TMP_Text points = CreateText(
            "Points", root, font, "SKILL POINTS", 30f,
            TextAlignmentOptions.Left, Gold);
        SetRect(points.rectTransform, new Vector2(0f, 1f), new Vector2(0.35f, 1f),
            new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(-20f, 52f));
        Button reset = CreateButton(
            "ResetButton", root, font, "RESET TREE", new Color(0.38f, 0.07f, 0.18f));
        SetRect(reset.GetComponent<RectTransform>(), new Vector2(1f, 1f), Vector2.one,
            Vector2.one, new Vector2(-30f, -18f), new Vector2(210f, 48f));
        TMP_Text feedback = CreateText(
            "Feedback", root, font, string.Empty, 22f,
            TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.68f));
        SetRect(feedback.rectTransform, new Vector2(0.35f, 1f),
            new Vector2(0.68f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -18f), new Vector2(0f, 48f));
        RectTransform connectionRoot = CreateUIObject("ConnectionRoot", root)
            .GetComponent<RectTransform>();
        Stretch(connectionRoot, 60f);
        RectTransform nodeRoot = CreateUIObject("NodeRoot", root)
            .GetComponent<RectTransform>();
        Stretch(nodeRoot, 60f);
        SkillTreeNodeView template = CreateNodeTemplate(nodeRoot, font);
        template.gameObject.SetActive(false);
        SetReference(view, "connectionRoot", connectionRoot);
        SetReference(view, "nodeRoot", nodeRoot);
        SetReference(view, "nodeTemplate", template);
        SetReference(view, "pointsText", points);
        SetReference(view, "feedbackText", feedback);
        SetReference(view, "resetButton", reset);
        return view;
    }

    private static SkillTreeNodeView CreateNodeTemplate(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject root = CreateUIObject("NodeTemplate", parent);
        SetRect(root.GetComponent<RectTransform>(), Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero,
            new Vector2(300f, 112f));
        SkillTreeNodeView view = root.AddComponent<SkillTreeNodeView>();
        Image glow = CreateImage("Glow", root.transform,
            new Color(1f, 0.06f, 0.38f, 0.62f));
        Stretch(glow.rectTransform, -7f);
        glow.raycastTarget = false;
        Image background = CreateImage("Background", root.transform,
            new Color(0.07f, 0.025f, 0.045f, 0.98f));
        Stretch(background.rectTransform);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        Image icon = CreateImage("Icon", root.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(76f, 76f));
        icon.raycastTarget = false;
        TMP_Text label = CreateText("Label", root.transform, font, "NODE", 23f,
            TextAlignmentOptions.Left, Color.white);
        SetRect(label.rectTransform, new Vector2(0f, 0f), Vector2.one,
            new Vector2(0f, 0.5f), new Vector2(106f, 0f), new Vector2(-145f, -16f));
        TMP_Text cost = CreateText("Cost", root.transform, font, "1", 20f,
            TextAlignmentOptions.BottomRight, Gold);
        Stretch(cost.rectTransform, 8f);
        SetReference(view, "button", button);
        SetReference(view, "background", background);
        SetReference(view, "glow", glow);
        SetReference(view, "icon", icon);
        SetReference(view, "label", label);
        SetReference(view, "costLabel", cost);
        return view;
    }

    private static SkillLoadoutPageView CreateLoadoutPage(
        Transform root,
        TMP_FontAsset font,
        Transform canvasRoot)
    {
        SkillLoadoutPageView view = root.gameObject.AddComponent<SkillLoadoutPageView>();
        Image equippedPanel = CreateImage("EquippedPanel", root, Ink);
        SetRect(equippedPanel.rectTransform, Vector2.zero, new Vector2(0.46f, 1f),
            new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-30f, -30f));
        Image equippedAccent = CreateImage("Accent", equippedPanel.transform, Magenta);
        SetRect(equippedAccent.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(7f, 0f));
        equippedAccent.raycastTarget = false;
        TMP_Text equippedTitle = CreateText(
            "Title", equippedPanel.transform, font, "EQUIPPED SKILLS", 30f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(equippedTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(-68f, 60f));
        Transform slotsRoot = CreateUIObject("Slots", equippedPanel.transform).transform;
        SetRect((RectTransform)slotsRoot, new Vector2(0f, 0f), Vector2.one,
            Vector2.one * 0.5f, new Vector2(0f, -35f), new Vector2(-50f, -150f));
        var slots = new SkillLoadoutSlotView[4];
        for (int i = 0; i < slots.Length; i++)
        {
            GameObject slotObject = CreateUIObject("LoadoutSlot_" + i, slotsRoot);
            SetRect(slotObject.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -25f - i * 175f),
                new Vector2(0f, 142f));
            SkillLoadoutSlotView slot = slotObject.AddComponent<SkillLoadoutSlotView>();
            SkillCardView card = CreateSkillCard("Card", slotObject.transform, font);
            Stretch(card.GetComponent<RectTransform>());
            SetReference(slot, "card", card);
            SetInt(slot, "slotIndex", i);
            slots[i] = slot;
        }

        Image poolPanel = CreateImage("SkillPoolPanel", root,
            new Color(0.075f, 0.02f, 0.045f, 0.98f));
        SetRect(poolPanel.rectTransform, new Vector2(0.48f, 0f), Vector2.one,
            Vector2.one * 0.5f, new Vector2(-10f, 0f), new Vector2(-40f, -30f));
        Image poolAccent = CreateImage("Accent", poolPanel.transform, Pink);
        SetRect(poolAccent.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 6f));
        poolAccent.raycastTarget = false;
        TMP_Text poolTitle = CreateText(
            "Title", poolPanel.transform, font, "UNLOCKED SKILL POOL", 30f,
            TextAlignmentOptions.Left, Pink);
        SetRect(poolTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(-68f, 60f));
        RectTransform poolRoot = CreateUIObject("PoolRoot", poolPanel.transform)
            .GetComponent<RectTransform>();
        SetRect(poolRoot, new Vector2(0f, 0f), Vector2.one,
            Vector2.one * 0.5f, new Vector2(0f, -35f), new Vector2(-50f, -150f));
        GridLayoutGroup grid = poolRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(360f, 116f);
        grid.spacing = new Vector2(22f, 22f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        SkillCardView template = CreateSkillCard("PoolCardTemplate", poolRoot, font);
        template.gameObject.SetActive(false);
        TMP_Text feedback = CreateText("Feedback", root, font, string.Empty, 24f,
            TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.68f));
        SetRect(feedback.rectTransform, new Vector2(0.35f, 0f),
            new Vector2(0.95f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f), new Vector2(0f, 52f));
        SetObjectArray(view, "slots", slots);
        SetReference(view, "poolRoot", poolRoot);
        SetReference(view, "poolCardTemplate", template);
        SetReference(view, "feedbackText", feedback);
        return view;
    }

    private static EquipmentPageView CreateEquipmentPage(
        Transform root,
        TMP_FontAsset font)
    {
        EquipmentPageView view = root.gameObject.AddComponent<EquipmentPageView>();

        Image currentPanel = CreateImage("CurrentPanel", root, Ink);
        SetRect(currentPanel.rectTransform, Vector2.zero, new Vector2(0.43f, 1f),
            new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-24f, -30f));
        Image currentAccent = CreateImage(
            "Accent", currentPanel.transform, Magenta);
        SetRect(currentAccent.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
        currentAccent.raycastTarget = false;
        TMP_Text currentTitle = CreateText(
            "Title", currentPanel.transform, font, "CURRENT LOADOUT / 当前装备", 30f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(currentTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(-58f, 58f));

        Image weaponFrame = CreateImage(
            "WeaponFrame", currentPanel.transform,
            new Color(0.085f, 0.018f, 0.045f, 1f));
        SetRect(weaponFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(34f, -104f), new Vector2(190f, 190f));
        Outline weaponOutline = weaponFrame.gameObject.AddComponent<Outline>();
        weaponOutline.effectColor = Pink;
        weaponOutline.effectDistance = new Vector2(3f, -3f);
        Image currentIcon = CreateImage("WeaponIcon", weaponFrame.transform, Color.white);
        Stretch(currentIcon.rectTransform, 18f);
        currentIcon.preserveAspect = true;
        currentIcon.raycastTarget = false;

        TMP_Text currentName = CreateText(
            "WeaponName", currentPanel.transform, font, "未装备武器", 38f,
            TextAlignmentOptions.Left, Pink);
        SetRect(currentName.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(258f, -102f), new Vector2(-292f, 54f));
        TMP_Text currentMeta = CreateText(
            "WeaponMeta", currentPanel.transform, font, "RARITY  DUR  WT", 23f,
            TextAlignmentOptions.TopLeft, Gold);
        SetRect(currentMeta.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(258f, -158f), new Vector2(-292f, 96f));

        Image buildPanel = CreateImage(
            "BuildPanel", currentPanel.transform,
            new Color(0.060f, 0.014f, 0.035f, 0.98f));
        SetRect(buildPanel.rectTransform, new Vector2(0f, 0.22f), new Vector2(1f, 0.69f),
            Vector2.one * 0.5f, new Vector2(0f, -8f), new Vector2(-68f, -12f));
        TMP_Text build = CreateText(
            "WeaponBuild", buildPanel.transform, font,
            "词条  —\n武装卡  —\n配件  — / —", 24f,
            TextAlignmentOptions.TopLeft, WarmWhite);
        Stretch(build.rectTransform, 24f);
        build.enableWordWrapping = true;
        TMP_Text outfit = CreateText(
            "Outfit", currentPanel.transform, font, "服装词条  —", 23f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(outfit.rectTransform, new Vector2(0f, 0.13f), new Vector2(1f, 0.22f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-68f, -8f));
        TMP_Text accessories = CreateText(
            "Accessories", currentPanel.transform, font, "挂饰  — / — / —", 23f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(accessories.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.13f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-68f, -8f));

        Image candidatePanel = CreateImage(
            "CandidatePanel", root, new Color(0.075f, 0.015f, 0.040f, 0.98f));
        SetRect(candidatePanel.rectTransform, new Vector2(0.45f, 0f), Vector2.one,
            Vector2.one * 0.5f, new Vector2(-10f, 0f), new Vector2(-30f, -30f));
        Image candidateAccent = CreateImage(
            "Accent", candidatePanel.transform, Pink);
        SetRect(candidateAccent.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 7f));
        candidateAccent.raycastTarget = false;
        TMP_Text candidateTitle = CreateText(
            "Title", candidatePanel.transform, font,
            "TEAM BACKPACK / 可切换武器", 30f,
            TextAlignmentOptions.Left, Pink);
        SetRect(candidateTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(-68f, 58f));

        Image viewport = CreateImage(
            "Viewport", candidatePanel.transform,
            new Color(0.018f, 0.018f, 0.028f, 0.76f));
        SetRect(viewport.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.91f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-64f, -12f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateUIObject("Content", viewport.transform)
            .GetComponent<RectTransform>();
        SetRect(content, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = candidatePanel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        GameMenuWeaponEntryView template = CreateGameMenuWeaponEntry(
            "WeaponCandidateTemplate", content, font);
        template.gameObject.SetActive(false);
        TMP_Text feedback = CreateText(
            "Feedback", candidatePanel.transform, font, string.Empty, 22f,
            TextAlignmentOptions.Center, new Color(1f, 0.43f, 0.64f));
        SetRect(feedback.rectTransform, Vector2.zero, new Vector2(1f, 0.08f),
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-64f, -8f));

        SetReference(view, "currentWeaponIcon", currentIcon);
        SetReference(view, "currentWeaponName", currentName);
        SetReference(view, "currentWeaponMeta", currentMeta);
        SetReference(view, "currentWeaponBuild", build);
        SetReference(view, "outfitText", outfit);
        SetReference(view, "accessoryText", accessories);
        SetReference(view, "candidateRoot", content);
        SetReference(view, "candidateTemplate", template);
        SetReference(view, "feedbackText", feedback);
        return view;
    }

    private static GameMenuWeaponEntryView CreateGameMenuWeaponEntry(
        string name,
        Transform parent,
        TMP_FontAsset font)
    {
        Image background = CreateImage(name, parent,
            new Color(0.10f, 0.025f, 0.055f, 0.98f));
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 126f;
        element.minHeight = 126f;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.10f, 0.025f, 0.055f, 0.98f);
        colors.highlightedColor = new Color(0.48f, 0.04f, 0.20f, 1f);
        colors.pressedColor = new Color(0.72f, 0.04f, 0.28f, 1f);
        colors.disabledColor = new Color(0.07f, 0.055f, 0.065f, 0.62f);
        button.colors = colors;
        Image rail = CreateImage("RarityRail", background.transform, Magenta);
        SetRect(rail.rectTransform, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
        rail.raycastTarget = false;
        Image icon = CreateImage("Icon", background.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(92f, 92f));
        icon.raycastTarget = false;
        TMP_Text weaponName = CreateText(
            "Name", background.transform, font, "WEAPON", 27f,
            TextAlignmentOptions.Left, WarmWhite);
        SetRect(weaponName.rectTransform, new Vector2(0f, 0.48f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(142f, -12f), new Vector2(-174f, -10f));
        TMP_Text detail = CreateText(
            "Detail", background.transform, font, "耐久 / 负重 / 职业", 20f,
            TextAlignmentOptions.Left, new Color(0.82f, 0.78f, 0.80f));
        SetRect(detail.rectTransform, Vector2.zero, new Vector2(1f, 0.52f),
            new Vector2(0f, 0f), new Vector2(142f, 10f), new Vector2(-174f, -10f));
        TMP_Text reason = CreateText(
            "State", background.transform, font, "可装备", 19f,
            TextAlignmentOptions.Right, Gold);
        SetRect(reason.rectTransform, new Vector2(0.56f, 0f), Vector2.one,
            new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(-20f, -16f));
        GameMenuWeaponEntryView view =
            background.gameObject.AddComponent<GameMenuWeaponEntryView>();
        SetReference(view, "button", button);
        SetReference(view, "icon", icon);
        SetReference(view, "nameText", weaponName);
        SetReference(view, "detailText", detail);
        SetReference(view, "reasonText", reason);
        SetReference(view, "rarityRail", rail);
        return view;
    }

    private static SkillCardView CreateSkillCard(
        string name,
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject root = CreateUIObject(name, parent);
        SkillCardView view = root.AddComponent<SkillCardView>();
        Image glow = CreateImage("Glow", root.transform,
            new Color(1f, 0.05f, 0.36f, 0.48f));
        Stretch(glow.rectTransform, -5f);
        glow.raycastTarget = false;
        Image background = CreateImage("Background", root.transform,
            new Color(0.09f, 0.025f, 0.052f, 0.98f));
        Stretch(background.rectTransform);
        Image icon = CreateImage("Icon", root.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(82f, 82f));
        icon.raycastTarget = false;
        TMP_Text label = CreateText("Label", root.transform, font, "SKILL", 25f,
            TextAlignmentOptions.Left, Color.white);
        SetRect(label.rectTransform, new Vector2(0f, 0f), Vector2.one,
            new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(-145f, -20f));
        SetReference(view, "background", background);
        SetReference(view, "glow", glow);
        SetReference(view, "icon", icon);
        SetReference(view, "label", label);
        return view;
    }

    private static GameMenuTooltipView CreateTooltip(
        Transform parent,
        TMP_FontAsset font)
    {
        Image panel = CreateImage("Tooltip", parent, Ink);
        SetRect(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-78f, 0f), new Vector2(520f, 430f));
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        TMP_Text title = CreateText("Title", panel.transform, font, "SKILL", 34f,
            TextAlignmentOptions.TopLeft, Pink);
        SetRect(title.rectTransform, new Vector2(0f, 0.76f), Vector2.one,
            new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(-60f, -30f));
        TMP_Text body = CreateText("Body", panel.transform, font, "DESCRIPTION", 23f,
            TextAlignmentOptions.TopLeft, Color.white);
        SetRect(body.rectTransform, Vector2.zero, new Vector2(1f, 0.76f),
            new Vector2(0f, 1f), new Vector2(30f, -8f), new Vector2(-60f, -30f));
        body.enableWordWrapping = true;
        GameMenuTooltipView view = panel.gameObject.AddComponent<GameMenuTooltipView>();
        SetReference(view, "canvasGroup", group);
        SetReference(view, "titleText", title);
        SetReference(view, "bodyText", body);
        panel.gameObject.SetActive(false);
        return view;
    }

    private static GameObject CreatePage(string name, Transform parent)
    {
        GameObject page = CreateUIObject(name, parent);
        SetRect(page.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.one * 0.5f, new Vector2(0f, -70f), new Vector2(-80f, -250f));
        return page;
    }

    private static Button CreateNavigationButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        float x)
    {
        Button button = CreateButton(
            name, parent, font, label, new Color(0.10f, 0.025f, 0.055f, 1f));
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -124f),
            new Vector2(246f, 50f));
        return button;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        Color color)
    {
        Image image = CreateImage(name, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        image.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Magenta;
        colors.pressedColor = new Color(0.48f, 0.01f, 0.15f, 1f);
        colors.selectedColor = Pink;
        colors.disabledColor = new Color(0.08f, 0.05f, 0.065f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        TMP_Text text = CreateText("Label", image.transform, font, label, 23f,
            TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray<T>(
        UnityEngine.Object target,
        string propertyName,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(
        UnityEngine.Object target,
        string propertyName,
        int value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Missing required asset: " + path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
