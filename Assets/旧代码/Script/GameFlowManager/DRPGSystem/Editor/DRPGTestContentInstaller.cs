#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class DRPGTestContentInstaller
{
    private const float DefaultTargetConfirmSeconds = 0.18f;
    private const float DefaultWindupSeconds = 0.14f;
    private const float DefaultHitFeedbackSeconds = 1.00f;
    private const float DefaultTimelineTransitionSeconds = 0.32f;
    private const float DefaultAttackCutInOpenSeconds = 0.14f;
    private const float DefaultAttackCutInImpactSeconds = 0.16f;
    private const float DefaultAttackCutInCloseSeconds = 0.18f;
    private const string RootFolder = "Assets/GameResources/DRPG";
    private const string ArtFolder = RootFolder + "/Art";
    private const string PortraitFolder = ArtFolder + "/Portraits";
    private const string AttackCutInFolder = PortraitFolder + "/AttackCutIns";
    private const string BackgroundFolder = ArtFolder + "/Backgrounds";
    private const string GeneratedUIFolder = RootFolder + "/UI/Generated";
    private const string ConfigFolder = RootFolder + "/Config/Test";
    private const string PresentationFolder = ConfigFolder + "/Presentation";
    private const string PresentationCueFolder = PresentationFolder + "/Cues";
    private const string PresentationRecipeFolder = PresentationFolder + "/Recipes";
    private const string PrefabFolder = RootFolder + "/Prefabs";
    private const string GalFolder = RootFolder + "/GalActors";
    private const string CharacterFolder = RootFolder + "/Characters";
    private const string AudioFolder = RootFolder + "/Audio/Test";
    private const string FontFolder = RootFolder + "/Fonts";
    private const string MixerPath =
        "Assets/GameResources/Audio/ANKALUTE_AudioMixer.mixer";

    private const string ProtagonistPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Protagonist.png";
    private const string Party01PortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party01.png";
    private const string Party02PortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party02.png";
    private const string ProtagonistAttackCutInPath =
        PortraitFolder + "/AttackCutIns/DRPG_AttackCutIn_Protagonist.png";
    private const string Party01AttackCutInPath =
        PortraitFolder + "/AttackCutIns/DRPG_AttackCutIn_Party01.png";
    private const string Party02AttackCutInPath =
        AttackCutInFolder + "/DRPG_AttackCutIn_Party02_DD1.png";
    private const string Enemy01AttackCutInPath =
        AttackCutInFolder + "/DRPG_AttackCutIn_Enemy01_DD1.png";
    private const string Enemy02AttackCutInPath =
        AttackCutInFolder + "/DRPG_AttackCutIn_Enemy02_DD1.png";
    private const string ProtagonistHitPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Protagonist_Hit.png";
    private const string ProtagonistDamageBrokenPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Protagonist_Broken.png";
    private const string Party01HitPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party01_Hit.png";
    private const string Party01DamageBrokenPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party01_Broken.png";
    private const string Party02HitPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party02_Hit.png";
    private const string Party02DamageBrokenPortraitPath =
        PortraitFolder + "/DRPG_Portrait_Party02_Broken.png";
    private const string Enemy01PortraitPath =
        PortraitFolder + "/DRPG_Portrait_Enemy01.png";
    private const string Enemy02PortraitPath =
        PortraitFolder + "/DRPG_Portrait_Enemy02.png";
    private const string BackgroundPath =
        BackgroundFolder + "/DRPG_BG_TestBattle.png";
    private const string InteractiveDefenseCrestPath =
        GeneratedUIFolder + "/DRPG_InteractiveDefenseCrest.png";
    private const string FontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";
    private const string JapaneseFontSourcePath =
        "Assets/ExternalResource/Fonts/Fonts/NotoSansSC-Regular.otf";
    private const string JapaneseFontPath =
        FontFolder + "/NotoSansSC_DRPG_Dynamic.asset";
    private const string JapaneseQuoteCharacters =
        "僕の番だ。行こう。これで決める！まだ……終われない。ごめん……もう、動けない……。" +
        "私の出番ね。そこっ！まだ……負けない！ここまで、なの……？" +
        "準備はできています。行きます！くっ……まだです。申し訳……ありません……。" +
        "標的を確認。排除する。機能低下……。任務……失敗……。" +
        "殲滅を開始する。逃がさない。損傷、危険域。機能……停止……。" +
        "損傷を確認。まだ終わってない……！「」　";

    private const string SkillPath = ConfigFolder + "/DRPGSkill_BasicAttack.asset";
    private const string FreeShotSkillPath =
        ConfigFolder + "/DRPGSkill_FreeShot.asset";
    private const string FreeSlashSkillPath =
        ConfigFolder + "/DRPGSkill_FreeSlash.asset";
    private const string FreeShotChargeResourcePath =
        ConfigFolder + "/DRPGProgress_FreeShotCharge.asset";
    private const string EnemySpecialSkillPath =
        ConfigFolder + "/DRPGSkill_EnemySpecial.asset";
    private const string DebugBreakSkillPath =
        ConfigFolder + "/DRPGSkill_DebugBreak.asset";
    private const string PresentationPath =
        ConfigFolder + "/DRPGBattlePresentation_Test.asset";
    private const string PresentationRecipeSetPath =
        PresentationFolder + "/BattlePresentationRecipeSet_Test.asset";
    private const string AudioPalettePath =
        AudioFolder + "/DRPGBattleAudioPalette_Test.asset";
    private const string PartyPath = ConfigFolder + "/DRPGParty_Test3.asset";
    private const string EncounterPath = ConfigFolder + "/DRPGEncounter_Test2.asset";
    private const string DebugBreakPartyPath =
        ConfigFolder + "/DRPGParty_DebugBreak.asset";
    private const string DebugBreakEncounterPath =
        ConfigFolder + "/DRPGEncounter_DebugBreak.asset";
    private const string DebugDefeatEncounterPath =
        ConfigFolder + "/DRPGEncounter_DebugDefeat.asset";
    private const string PrefabPath =
        PrefabFolder + "/PF_DRPGBattlePresentation.prefab";

    private static readonly string[] CharacterIds =
    {
        "ankalute_protagonist",
        "ankalute_party_vanguard",
        "ankalute_party_alchemist",
        "ankalute_enemy_warden",
        "ankalute_enemy_sentinel"
    };

    private static readonly string[] BattleVoiceProfileIds =
    {
        "ankalute_protagonist",
        "ankalute_party_vanguard",
        "ankalute_party_alchemist",
        "ankalute_enemy_warden",
        "ankalute_enemy_sentinel",
        "ankalute_debug_enemy_warden",
        "ankalute_debug_enemy_sentinel",
        "ankalute_debug_defeat_warden",
        "ankalute_debug_defeat_sentinel"
    };

    private static readonly string[] DisplayNames =
    {
        "伊诺",
        "莱莎",
        "西安",
        "裂面守望者",
        "黑铁哨兵"
    };

    private static readonly string[] PortraitPaths =
    {
        ProtagonistPortraitPath,
        Party01PortraitPath,
        Party02PortraitPath,
        Enemy01PortraitPath,
        Enemy02PortraitPath
    };

    private static readonly string[] HitPortraitPaths =
    {
        ProtagonistHitPortraitPath,
        Party01HitPortraitPath,
        Party02HitPortraitPath,
        null,
        null
    };

    private static readonly string[] DamageBrokenPortraitPaths =
    {
        ProtagonistDamageBrokenPortraitPath,
        Party01DamageBrokenPortraitPath,
        Party02DamageBrokenPortraitPath,
        null,
        null
    };

    private static readonly string[] AttackCutInPortraitPaths =
    {
        ProtagonistAttackCutInPath,
        Party01AttackCutInPath,
        Party02AttackCutInPath,
        Enemy01AttackCutInPath,
        Enemy02AttackCutInPath
    };

    [MenuItem("Tools/ANKALUTE/DRPG/Install 3v2 Test Vertical Slice")]
    public static void InstallFromMenu()
    {
        Debug.Log(Install());
    }

    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "[DRPGInstaller] REJECTED: exit Play Mode before installing.";

        try
        {
            EnsureFolder(RootFolder);
            EnsureFolder(ArtFolder);
            EnsureFolder(PortraitFolder);
            EnsureFolder(AttackCutInFolder);
            EnsureFolder(BackgroundFolder);
            EnsureFolder(GeneratedUIFolder);
            EnsureFolder(ConfigFolder);
            EnsureFolder(PresentationFolder);
            EnsureFolder(PresentationCueFolder);
            EnsureFolder(PresentationRecipeFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(GalFolder);
            EnsureFolder(CharacterFolder);
            EnsureFolder(AudioFolder);
            EnsureFolder(FontFolder);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < PortraitPaths.Length; i++)
            {
                ConfigureSpriteImporter(PortraitPaths[i], true, new Vector2(0.5f, 0f));
                if (!string.IsNullOrWhiteSpace(HitPortraitPaths[i]))
                    ConfigureSpriteImporter(HitPortraitPaths[i], true, new Vector2(0.5f, 0f));
                if (!string.IsNullOrWhiteSpace(DamageBrokenPortraitPaths[i]))
                    ConfigureSpriteImporter(DamageBrokenPortraitPaths[i], true, new Vector2(0.5f, 0f));
                if (!string.IsNullOrWhiteSpace(AttackCutInPortraitPaths[i]))
                {
                    ConfigureSpriteImporter(
                        AttackCutInPortraitPaths[i],
                        true,
                        new Vector2(0.5f, 0.5f));
                }
            }
            ConfigureSpriteImporter(BackgroundPath, false, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter(
                InteractiveDefenseCrestPath,
                true,
                new Vector2(0.5f, 0.5f));

            Sprite[] portraits = new Sprite[PortraitPaths.Length];
            Sprite[] hitPortraits = new Sprite[PortraitPaths.Length];
            Sprite[] damageBrokenPortraits = new Sprite[PortraitPaths.Length];
            Sprite[] attackCutInPortraits = new Sprite[PortraitPaths.Length];
            for (int i = 0; i < PortraitPaths.Length; i++)
            {
                portraits[i] = LoadRequired<Sprite>(PortraitPaths[i]);
                hitPortraits[i] = LoadOptional<Sprite>(HitPortraitPaths[i]);
                damageBrokenPortraits[i] = LoadOptional<Sprite>(DamageBrokenPortraitPaths[i]);
                attackCutInPortraits[i] =
                    LoadOptional<Sprite>(AttackCutInPortraitPaths[i]);
            }
            Sprite background = LoadRequired<Sprite>(BackgroundPath);
            TMP_FontAsset font = LoadRequired<TMP_FontAsset>(FontPath);
            TMP_FontAsset japaneseFont = CreateOrUpdateJapaneseFont();

            DRPGProgressResourceSO freeShotCharge =
                CreateOrUpdateFreeShotChargeResource();
            DRPGSkillSO attack = CreateOrUpdateAttack(freeShotCharge);
            DRPGSkillSO freeShot = CreateOrUpdateFreeStrike(
                FreeShotSkillPath,
                "free_shot",
                "自由射击",
                "开启 5 秒自由射击窗口。移动鼠标瞄准，左键连续射击；命中弱点造成 1.8 倍伤害。",
                12f,
                DRPGFreeStrikeShape.Point,
                DRPGFreeStrikeContactPolicy.FirstContact,
                "free_shot",
                freeShotCharge);
            DRPGSkillSO freeSlash = CreateOrUpdateFreeStrike(
                FreeSlashSkillPath,
                "free_slash",
                "自由斩击",
                "开启 5 秒自由斩击窗口。移动鼠标决定斩线位置，左键连续挥斩；斩过弱点造成 1.8 倍伤害。",
                16f,
                DRPGFreeStrikeShape.Line,
                DRPGFreeStrikeContactPolicy.AllContacts,
                "free_slash",
                null);
            DRPGSkillSO enemySpecial = CreateOrUpdateEnemySpecial();
            DRPGSkillSO debugBreakAttack = CreateOrUpdateDebugBreakAttack();
            DRPGBattleAudioPaletteSO audioPalette =
                CreateOrUpdateAudioPalette();
            BattlePresentationRecipeSetSO recipeSet =
                CreateOrUpdatePresentationRecipeSet();
            DRPGBattlePresentationSO presentation =
                CreateOrUpdatePresentation(background, audioPalette, recipeSet);
            CharacterSO[] characters = new CharacterSO[CharacterIds.Length];
            DRPGCombatantProfileSO[] profiles =
                new DRPGCombatantProfileSO[CharacterIds.Length];
            GalActorSO[] galActors = new GalActorSO[CharacterIds.Length];

            for (int i = 0; i < CharacterIds.Length; i++)
            {
                bool enemy = i >= 3;
                characters[i] = CreateOrUpdateCharacter(
                    CharacterIds[i], DisplayNames[i], portraits[i], enemy);
                profiles[i] = CreateOrUpdateProfile(
                    CharacterIds[i], DisplayNames[i], portraits[i],
                    hitPortraits[i], damageBrokenPortraits[i], attackCutInPortraits[i],
                    characters[i], attack, i);
                galActors[i] = CreateOrUpdateGalActor(
                    CharacterIds[i], DisplayNames[i], portraits[i],
                    hitPortraits[i], damageBrokenPortraits[i]);
                DRPGBattleQuoteSetSO quotes = CreateOrUpdateBattleQuotes(
                    CharacterIds[i], DisplayNames[i], i);
                LinkProfilePresentationData(profiles[i], galActors[i], quotes);
            }

            SetProfileSkills(profiles[0], attack, freeShot);
            SetProfileSkills(profiles[1], attack, freeSlash);
            SetProfileSkills(profiles[2], attack);
            SetProfileSkills(profiles[3], enemySpecial, attack);
            SetProfileSkills(profiles[4], attack);

            DRPGPartySetupSO party = CreateOrUpdateParty(
                profiles[1], profiles[0], profiles[2]);
            DRPGEncounterDefinitionSO encounter = CreateOrUpdateEncounter(
                profiles[3], profiles[4], background, presentation);
            DRPGCombatantProfileSO debugEnemy01 =
                CreateOrUpdateDebugBreakProfile(
                    "ankalute_debug_enemy_warden",
                    DisplayNames[3],
                    portraits[3],
                    hitPortraits[3],
                    damageBrokenPortraits[3],
                    characters[3],
                    debugBreakAttack,
                    3,
                    galActors[3],
                    CreateOrUpdateBattleQuotes(
                        "ankalute_debug_enemy_warden",
                        DisplayNames[3],
                        3));
            DRPGCombatantProfileSO debugEnemy02 =
                CreateOrUpdateDebugBreakProfile(
                    "ankalute_debug_enemy_sentinel",
                    DisplayNames[4],
                    portraits[4],
                    hitPortraits[4],
                    damageBrokenPortraits[4],
                    characters[4],
                    debugBreakAttack,
                    4,
                    galActors[4],
                    CreateOrUpdateBattleQuotes(
                        "ankalute_debug_enemy_sentinel",
                        DisplayNames[4],
                        4));
            DRPGPartySetupSO debugBreakParty = CreateOrUpdateDebugBreakParty(
                profiles[1],
                profiles[0],
                profiles[2]);
            DRPGEncounterDefinitionSO debugBreakEncounter =
                CreateOrUpdateDebugBreakEncounter(
                    debugEnemy01,
                    debugEnemy02,
                    background,
                    presentation);
            DRPGCombatantProfileSO debugDefeatEnemy01 =
                CreateOrUpdateDebugDefeatProfile(
                    "ankalute_debug_defeat_warden",
                    DisplayNames[3],
                    portraits[3],
                    characters[3],
                    debugBreakAttack,
                    3,
                    galActors[3],
                    CreateOrUpdateBattleQuotes(
                        "ankalute_debug_defeat_warden",
                        DisplayNames[3],
                        3));
            DRPGCombatantProfileSO debugDefeatEnemy02 =
                CreateOrUpdateDebugDefeatProfile(
                    "ankalute_debug_defeat_sentinel",
                    DisplayNames[4],
                    portraits[4],
                    characters[4],
                    debugBreakAttack,
                    4,
                    galActors[4],
                    CreateOrUpdateBattleQuotes(
                        "ankalute_debug_defeat_sentinel",
                        DisplayNames[4],
                        4));
            DRPGEncounterDefinitionSO debugDefeatEncounter =
                CreateOrUpdateDebugDefeatEncounter(
                    debugDefeatEnemy01,
                    debugDefeatEnemy02,
                    background,
                    presentation);
            GameObject presentationPrefab =
                CreatePresentationPrefab(font, japaneseFont);
            bool sceneChanged = EnsureSceneConnection(
                presentationPrefab,
                party,
                encounter,
                debugDefeatEncounter,
                out string scenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return "[DRPGInstaller] PASS" +
                "; party=3; enemies=10; initialEnemies=6; activeEnemyCap=15" +
                "; interactiveSkills=freeShot,freeSlash,enemySpecial" +
                "; debugBreakParty=" + DebugBreakPartyPath +
                "; debugBreakEncounter=" + DebugBreakEncounterPath +
                "; debugDefeatEncounter=" + DebugDefeatEncounterPath +
                "; sharedGalPortraits=5" +
                "; japaneseFont=" + JapaneseFontPath +
                "; prefab=" + PrefabPath +
                "; sceneConnection=" + sceneChanged +
                "; scenePath=" + scenePath +
                "; sceneSaved=false";
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return "[DRPGInstaller] FAILED: " + exception.Message;
        }
    }

    private static DRPGProgressResourceSO CreateOrUpdateFreeShotChargeResource()
    {
        DRPGProgressResourceSO resource =
            LoadOrCreate<DRPGProgressResourceSO>(FreeShotChargeResourcePath);
        var serialized = new SerializedObject(resource);
        serialized.FindProperty("resourceId").stringValue =
            "free_shot_chamber";
        serialized.FindProperty("displayName").stringValue = "特殊弹药";
        serialized.FindProperty("scope").enumValueIndex =
            (int)DRPGProgressScope.Battle;
        serialized.FindProperty("baseCapacity").floatValue = 0f;
        serialized.FindProperty("initialValue").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(resource);
        resource.ValidateOrThrow();
        return resource;
    }

    private static DRPGSkillSO CreateOrUpdateAttack(
        DRPGProgressResourceSO freeShotCharge)
    {
        DRPGSkillSO skill = LoadOrCreate<DRPGSkillSO>(SkillPath);
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("skillId").stringValue = "basic_attack";
        serialized.FindProperty("displayName").stringValue = "普通攻击";
        serialized.FindProperty("description").stringValue =
            "基础单体攻击。它和其他技能走同一条指令与行动延迟管线。";
        serialized.FindProperty("power").floatValue = 34f;
        serialized.FindProperty("prepareDelay").floatValue = 0f;
        serialized.FindProperty("recoveryDelay").floatValue = 120f;
        serialized.FindProperty("animationIntent").intValue =
            (int)EAnimationLogicState.Skill_1;
        serialized.FindProperty("presentationKey").stringValue = "basic_attack";
        serialized.FindProperty("sourceFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        serialized.FindProperty("targetFormationRows").intValue =
            (int)DRPGFormationRowMask.Front;
        serialized.FindProperty("enemyWarningSteps").intValue = 2;
        serialized.FindProperty("enemyTargetDisclosureMode").intValue =
            (int)DRPGEnemyTargetDisclosureMode.Hidden;
        SerializedProperty gainRules =
            serialized.FindProperty("progressGainRules");
        gainRules.arraySize = 1;
        SerializedProperty gainRule = gainRules.GetArrayElementAtIndex(0);
        gainRule.FindPropertyRelative("ruleId").stringValue =
            "basic_attack_charge_free_shot";
        gainRule.FindPropertyRelative("resource").objectReferenceValue =
            freeShotCharge;
        gainRule.FindPropertyRelative("eventKind").enumValueIndex =
            (int)DRPGBattleEventKind.ActionImpactResolved;
        gainRule.FindPropertyRelative("amount").floatValue = 1f;
        gainRule.FindPropertyRelative("ownerSelection").enumValueIndex =
            (int)DRPGProgressOwnerSelection.Self;
        gainRule.FindPropertyRelative("explicitCharacterId").stringValue =
            string.Empty;
        gainRule.FindPropertyRelative("includeReactionDepth").boolValue = false;
        gainRule.FindPropertyRelative("oncePerRootAction").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static DRPGSkillSO CreateOrUpdateDebugBreakAttack()
    {
        DRPGSkillSO skill = LoadOrCreate<DRPGSkillSO>(DebugBreakSkillPath);
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("skillId").stringValue = "debug_break_strike";
        serialized.FindProperty("displayName").stringValue = "大破测试攻击";
        serialized.FindProperty("description").stringValue =
            "仅用于演出验证：让队友稳定跨过大破阈值，不参与正式遭遇配置。";
        serialized.FindProperty("power").floatValue = 50f;
        serialized.FindProperty("prepareDelay").floatValue = 0f;
        serialized.FindProperty("recoveryDelay").floatValue = 120f;
        serialized.FindProperty("animationIntent").intValue =
            (int)EAnimationLogicState.Skill_1;
        serialized.FindProperty("presentationKey").stringValue =
            "debug_break_strike";
        serialized.FindProperty("sourceFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        serialized.FindProperty("targetFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static DRPGSkillSO CreateOrUpdateFreeStrike(
        string path,
        string id,
        string displayName,
        string description,
        float power,
        DRPGFreeStrikeShape shape,
        DRPGFreeStrikeContactPolicy contactPolicy,
        string presentationKey,
        DRPGProgressResourceSO inputProgressResource)
    {
        DRPGSkillSO skill = LoadOrCreate<DRPGSkillSO>(path);
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("skillId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("power").floatValue = power;
        serialized.FindProperty("prepareDelay").floatValue = 0f;
        serialized.FindProperty("recoveryDelay").floatValue = 145f;
        serialized.FindProperty("animationIntent").intValue =
            (int)EAnimationLogicState.Skill_1;
        serialized.FindProperty("presentationKey").stringValue =
            presentationKey;
        serialized.FindProperty("executionMode").intValue =
            (int)DRPGSkillExecutionMode.FreeStrike;
        serialized.FindProperty("sourceFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        serialized.FindProperty("targetFormationRows").intValue =
            shape == DRPGFreeStrikeShape.Point
                ? (int)DRPGFormationRowMask.All
                : (int)(DRPGFormationRowMask.Front |
                    DRPGFormationRowMask.Middle);
        serialized.FindProperty("freeStrikeDuration").floatValue = 5f;
        serialized.FindProperty("freeStrikeCadence").floatValue = 0.18f;
        serialized.FindProperty("freeStrikePostureCost").floatValue = 12f;
        serialized.FindProperty("freeStrikeShape").intValue = (int)shape;
        serialized.FindProperty("freeStrikeLineLength").floatValue = 0.62f;
        serialized.FindProperty("freeStrikeLineWidth").floatValue = 0.09f;
        serialized.FindProperty("freeStrikeContactPolicy").intValue =
            (int)contactPolicy;
        serialized.FindProperty("freeStrikeMaxPrimaryContacts").intValue = 1;
        serialized.FindProperty("freeStrikeSecondaryImpactKind").intValue =
            (int)DRPGSecondaryImpactKind.None;
        serialized.FindProperty("freeStrikeSecondaryRadius").floatValue = 0f;
        serialized.FindProperty("freeStrikeSecondaryDamageMultiplier").floatValue =
            0.35f;
        SerializedProperty inputCost =
            serialized.FindProperty("inputProgressCost");
        inputCost.FindPropertyRelative("resource").objectReferenceValue =
            inputProgressResource;
        inputCost.FindPropertyRelative("resourceId").stringValue =
            inputProgressResource != null
                ? inputProgressResource.ResourceId
                : string.Empty;
        inputCost.FindPropertyRelative("amount").floatValue =
            inputProgressResource != null ? 1f : 0f;
        serialized.FindProperty("selfPostureDepletionPolicy").enumValueIndex =
            (int)DRPGSelfPostureDepletionPolicy.ResolveImpactThenEnterPostureBreak;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static DRPGSkillSO CreateOrUpdateEnemySpecial()
    {
        DRPGSkillSO skill = LoadOrCreate<DRPGSkillSO>(EnemySpecialSkillPath);
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("skillId").stringValue =
            "enemy_special_triple";
        serialized.FindProperty("displayName").stringValue = "裂界三连袭";
        serialized.FindProperty("description").stringValue =
            "预备一回合并锁定单体目标；下次行动释放三段攻击。按 Space 或 E 防御，临近命中时输入可触发完美防御。";
        serialized.FindProperty("power").floatValue = 20f;
        serialized.FindProperty("prepareDelay").floatValue = 0f;
        serialized.FindProperty("recoveryDelay").floatValue = 165f;
        serialized.FindProperty("animationIntent").intValue =
            (int)EAnimationLogicState.Skill_1;
        serialized.FindProperty("presentationKey").stringValue =
            "enemy_special_triple";
        serialized.FindProperty("executionMode").intValue =
            (int)DRPGSkillExecutionMode.TelegraphedSpecial;
        serialized.FindProperty("sourceFormationRows").intValue =
            (int)(DRPGFormationRowMask.Middle | DRPGFormationRowMask.Rear);
        serialized.FindProperty("targetFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        SerializedProperty impactTimes =
            serialized.FindProperty("specialImpactTimes");
        impactTimes.arraySize = 3;
        impactTimes.GetArrayElementAtIndex(0).floatValue = 0.65f;
        impactTimes.GetArrayElementAtIndex(1).floatValue = 1.10f;
        impactTimes.GetArrayElementAtIndex(2).floatValue = 1.55f;
        serialized.FindProperty("guardLeadSeconds").floatValue = 0.42f;
        serialized.FindProperty("perfectLeadSeconds").floatValue = 0.12f;
        serialized.FindProperty("guardedDamageMultiplier").floatValue = 0.45f;
        serialized.FindProperty("missedGuardPostureDamage").floatValue = 34f;
        serialized.FindProperty("guardedPostureDamage").floatValue = 22f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static BattlePresentationRecipeSetSO
        CreateOrUpdatePresentationRecipeSet()
    {
        BattleTimelineCueSO timeline = CreateOrUpdateCue<BattleTimelineCueSO>(
            "Cue_Timeline_Default",
            "timeline_default",
            DefaultTimelineTransitionSeconds);
        BattleTargetFeedbackCueSO targetSelected =
            CreateOrUpdateTargetFeedbackCue(
                "Cue_Target_Selected",
                "target_selected",
                DRPGBattleTargetFeedback.Selected);
        BattleTargetFeedbackCueSO targetEnemyIntent =
            CreateOrUpdateTargetFeedbackCue(
                "Cue_Target_EnemyIntent",
                "target_enemy_intent",
                DRPGBattleTargetFeedback.EnemyIntent);
        BattleActorMotionCueSO actorMotion =
            CreateOrUpdateCue<BattleActorMotionCueSO>(
                "Cue_ActorMotion_Default",
                "actor_motion_default",
                DefaultWindupSeconds);
        BattleAttackCutInCueSO attackCutInOpen =
            CreateOrUpdateAttackCutInCue(
                "Cue_AttackCutIn_Open",
                "attack_cutin_open",
                BattleAttackCutInStage.Open,
                DefaultAttackCutInOpenSeconds);
        BattleAttackCutInCueSO attackCutInImpact =
            CreateOrUpdateAttackCutInCue(
                "Cue_AttackCutIn_Impact",
                "attack_cutin_impact",
                BattleAttackCutInStage.Impact,
                DefaultAttackCutInImpactSeconds);
        BattleAttackCutInCueSO attackCutInClose =
            CreateOrUpdateAttackCutInCue(
                "Cue_AttackCutIn_Close",
                "attack_cutin_close",
                BattleAttackCutInStage.Close,
                DefaultAttackCutInCloseSeconds);
        BattleImpactCueSO impact = CreateOrUpdateImpactCue();
        BattleDamageBrokenCutInCueSO damageBrokenCutIn =
            CreateOrUpdateCue<BattleDamageBrokenCutInCueSO>(
                "Cue_DamageBrokenCutIn_Default",
                "break_cutin_default",
                0f);
        BattleQuoteCueSO quoteTurn = CreateOrUpdateQuoteCue(
            "Cue_Quote_Turn", "quote_turn", DRPGBattleQuoteContext.TurnBegin);
        BattleQuoteCueSO quoteAttack = CreateOrUpdateQuoteCue(
            "Cue_Quote_Attack", "quote_attack", DRPGBattleQuoteContext.Attack);
        CreateOrUpdateQuoteCue(
            "Cue_Quote_Break", "quote_break", DRPGBattleQuoteContext.Break);
        CreateOrUpdateQuoteCue(
            "Cue_Quote_Defeated", "quote_defeated",
            DRPGBattleQuoteContext.Defeated);
        BattleAudioPresentationCueSO audioSkill = CreateOrUpdateAudioCue(
            "Cue_Audio_SkillSelected", "audio_skill_selected",
            BattleAudioCue.SkillSelected);
        BattleAudioPresentationCueSO audioAttack = CreateOrUpdateAudioCue(
            "Cue_Audio_Attack", "audio_attack", BattleAudioCue.Attack);
        BattleAudioPresentationCueSO audioHit = CreateOrUpdateAudioCue(
            "Cue_Audio_Hit", "audio_hit", BattleAudioCue.Hit);
        BattleAudioPresentationCueSO audioBreakEnter = CreateOrUpdateAudioCue(
            "Cue_Audio_BreakEnter", "audio_break_enter",
            BattleAudioCue.BreakEnter);
        BattleAudioPresentationCueSO audioBreakExit = CreateOrUpdateAudioCue(
            "Cue_Audio_BreakExit", "audio_break_exit",
            BattleAudioCue.BreakExit);

        BattlePresentationRecipeSO turnActivated = CreateOrUpdateRecipe(
            "Recipe_TurnActivated_Default",
            "turn_activated_default",
            BattlePresentationMoment.TurnActivated,
            BattlePresentationGateKind.Input,
            5f,
            new RecipeBeatSeed(
                "activate",
                0f,
                0f,
                true,
                new RecipeCueSeed(
                    timeline,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Timeline,
                    true),
                new RecipeCueSeed(
                    quoteTurn,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Voice,
                    true)));
        BattlePresentationRecipeSO commandOpened = CreateOrUpdateRecipe(
            "Recipe_CommandOpened_Default",
            "command_opened_default",
            BattlePresentationMoment.CommandOpened,
            BattlePresentationGateKind.None,
            1f,
            new RecipeBeatSeed(
                "command",
                0f,
                0f,
                false,
                new RecipeCueSeed(
                    audioSkill,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Sfx,
                    false)));
        BattlePresentationRecipeSO targeting = CreateOrUpdateRecipe(
            "Recipe_Targeting_Default",
            "targeting_default",
            BattlePresentationMoment.TargetingChanged,
            BattlePresentationGateKind.Input,
            1f,
            new RecipeBeatSeed(
                "confirm",
                0f,
                DefaultTargetConfirmSeconds,
                true,
                new RecipeCueSeed(
                    targetSelected,
                    BattlePresentationCondition.ActorIsPlayer,
                    BattlePresentationLaneKind.TargetFeedback,
                    true),
                new RecipeCueSeed(
                    targetEnemyIntent,
                    BattlePresentationCondition.ActorIsEnemy,
                    BattlePresentationLaneKind.TargetFeedback,
                    true)));
        BattlePresentationRecipeSO windup = CreateOrUpdateRecipe(
            "Recipe_ActionWindup_Default",
            "action_windup_default",
            BattlePresentationMoment.ActionWindup,
            BattlePresentationGateKind.ActionFlow,
            5f,
            new RecipeBeatSeed(
                "windup",
                0f,
                DefaultWindupSeconds,
                true,
                new RecipeCueSeed(
                    actorMotion,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.ActiveActor,
                    true),
                new RecipeCueSeed(
                    quoteAttack,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Voice,
                    false),
                new RecipeCueSeed(
                    audioAttack,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Sfx,
                    false)));
        BattlePresentationRecipeSO impactRecipe = CreateOrUpdateRecipe(
            "Recipe_Impact_Default",
            "impact_default",
            BattlePresentationMoment.ImpactResolved,
            BattlePresentationGateKind.ActionFlow,
            8f,
            new RecipeBeatSeed(
                "impact",
                0f,
                DefaultHitFeedbackSeconds,
                true,
                new RecipeCueSeed(
                    impact,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Participant,
                    true),
                new RecipeCueSeed(
                    audioHit,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Sfx,
                    false)));
        BattlePresentationRecipeSO breakOverlay = CreateOrUpdateRecipe(
            "Recipe_Impact_BreakOverlay",
            "impact_break_overlay",
            BattlePresentationMoment.ImpactResolved,
            BattlePresentationGateKind.None,
            8f,
            new RecipeBeatSeed(
                "break_enter",
                0f,
                0f,
                false,
                new RecipeCueSeed(
                    audioBreakEnter,
                    BattlePresentationCondition.AnyHitTriggeredDamageBroken,
                    BattlePresentationLaneKind.Sfx,
                    false),
                new RecipeCueSeed(
                    damageBrokenCutIn,
                    BattlePresentationCondition.AnyHitTriggeredDamageBroken,
                    BattlePresentationLaneKind.ScreenModal,
                    true)),
            new RecipeBeatSeed(
                "break_exit",
                0f,
                0f,
                false,
                new RecipeCueSeed(
                    audioBreakExit,
                    BattlePresentationCondition.AnyHitTriggeredDamageBroken,
                    BattlePresentationLaneKind.Sfx,
                    false)));
        BattlePresentationRecipeSO settled = CreateOrUpdateRecipe(
            "Recipe_ActionSettled_Default",
            "action_settled_default",
            BattlePresentationMoment.ActionSettled,
            BattlePresentationGateKind.None,
            1f,
            new RecipeBeatSeed("settled", 0f, 0f, false));
        BattlePresentationRecipeSO basicAttackWindup = CreateOrUpdateRecipe(
            "Recipe_ActionWindup_BasicAttack",
            "action_windup_basic_attack",
            BattlePresentationMoment.ActionWindup,
            BattlePresentationGateKind.ActionFlow,
            3f,
            new RecipeBeatSeed(
                "cutin_windup",
                0f,
                DefaultWindupSeconds,
                true,
                new RecipeCueSeed(
                    actorMotion,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.ActiveActor,
                    true),
                new RecipeCueSeed(
                    quoteAttack,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Voice,
                    false),
                new RecipeCueSeed(
                    audioAttack,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Sfx,
                    false),
                new RecipeCueSeed(
                    attackCutInOpen,
                    BattlePresentationCondition.ActorIsPlayer,
                    BattlePresentationLaneKind.ScreenFX,
                    true)));
        BattlePresentationRecipeSO basicAttackImpact = CreateOrUpdateRecipe(
            "Recipe_Impact_BasicAttack",
            "impact_basic_attack",
            BattlePresentationMoment.ImpactResolved,
            BattlePresentationGateKind.ActionFlow,
            4f,
            new RecipeBeatSeed(
                "synchronized_impact",
                0f,
                DefaultHitFeedbackSeconds,
                false,
                new RecipeCueSeed(
                    impact,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Participant,
                    true),
                new RecipeCueSeed(
                    audioHit,
                    BattlePresentationCondition.Always,
                    BattlePresentationLaneKind.Sfx,
                    false),
                new RecipeCueSeed(
                    attackCutInImpact,
                    BattlePresentationCondition.ActorIsPlayer,
                    BattlePresentationLaneKind.ScreenFX,
                    true)),
            new RecipeBeatSeed(
                "cutin_close",
                0f,
                0f,
                true,
                new RecipeCueSeed(
                    attackCutInClose,
                    BattlePresentationCondition.ActorIsPlayer,
                    BattlePresentationLaneKind.ScreenFX,
                    true)));

        BattlePresentationRecipeSetSO recipeSet =
            LoadOrCreate<BattlePresentationRecipeSetSO>(
                PresentationRecipeSetPath);
        var serialized = new SerializedObject(recipeSet);
        SerializedProperty baseRecipes = serialized.FindProperty("baseRecipes");
        BattlePresentationRecipeSO[] baseValues =
        {
            turnActivated,
            commandOpened,
            targeting,
            windup,
            impactRecipe,
            settled
        };
        baseRecipes.arraySize = baseValues.Length;
        for (int i = 0; i < baseValues.Length; i++)
        {
            baseRecipes.GetArrayElementAtIndex(i).objectReferenceValue =
                baseValues[i];
        }
        SerializedProperty skillOverrides =
            serialized.FindProperty("skillOverrides");
        skillOverrides.arraySize = 2;
        SetSkillRecipeOverride(
            skillOverrides.GetArrayElementAtIndex(0),
            "basic_attack",
            BattlePresentationMoment.ActionWindup,
            basicAttackWindup,
            BattlePresentationSkillRecipeMode.ReplaceBase);
        SetSkillRecipeOverride(
            skillOverrides.GetArrayElementAtIndex(1),
            "basic_attack",
            BattlePresentationMoment.ImpactResolved,
            basicAttackImpact,
            BattlePresentationSkillRecipeMode.ReplaceBase);
        serialized.FindProperty("breakOverlay").objectReferenceValue =
            breakOverlay;
        serialized.FindProperty("defeatedOverlay").objectReferenceValue = null;
        serialized.FindProperty("redirectedHitOverlay").objectReferenceValue =
            null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipeSet);
        return recipeSet;
    }

    private static T CreateOrUpdateCue<T>(
        string assetName,
        string cueId,
        float durationSeconds)
        where T : BattlePresentationCueSO
    {
        string path = PresentationCueFolder + "/" + assetName + ".asset";
        T cue = LoadOrCreate<T>(path);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("cueId").stringValue = cueId;
        serialized.FindProperty("durationSeconds").floatValue =
            Mathf.Max(0f, durationSeconds);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static BattleTargetFeedbackCueSO CreateOrUpdateTargetFeedbackCue(
        string assetName,
        string cueId,
        DRPGBattleTargetFeedback feedback)
    {
        BattleTargetFeedbackCueSO cue =
            CreateOrUpdateCue<BattleTargetFeedbackCueSO>(assetName, cueId, 0f);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("feedback").enumValueIndex = (int)feedback;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static BattleAttackCutInCueSO CreateOrUpdateAttackCutInCue(
        string assetName,
        string cueId,
        BattleAttackCutInStage stage,
        float durationSeconds)
    {
        BattleAttackCutInCueSO cue =
            CreateOrUpdateCue<BattleAttackCutInCueSO>(
                assetName,
                cueId,
                durationSeconds);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("stage").enumValueIndex = (int)stage;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static void SetSkillRecipeOverride(
        SerializedProperty property,
        string presentationKey,
        BattlePresentationMoment moment,
        BattlePresentationRecipeSO recipe,
        BattlePresentationSkillRecipeMode mode)
    {
        property.FindPropertyRelative("presentationKey").stringValue =
            presentationKey;
        property.FindPropertyRelative("moment").enumValueIndex = (int)moment;
        property.FindPropertyRelative("recipe").objectReferenceValue = recipe;
        property.FindPropertyRelative("mode").enumValueIndex = (int)mode;
    }

    private static BattleImpactCueSO CreateOrUpdateImpactCue()
    {
        BattleImpactCueSO cue = CreateOrUpdateCue<BattleImpactCueSO>(
            "Cue_Impact_Default",
            "impact_default",
            DefaultHitFeedbackSeconds);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("holdSeconds").floatValue =
            DefaultHitFeedbackSeconds;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static BattleQuoteCueSO CreateOrUpdateQuoteCue(
        string assetName,
        string cueId,
        DRPGBattleQuoteContext context)
    {
        BattleQuoteCueSO cue = CreateOrUpdateCue<BattleQuoteCueSO>(
            assetName, cueId, 0f);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("context").enumValueIndex = (int)context;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static BattleAudioPresentationCueSO CreateOrUpdateAudioCue(
        string assetName,
        string cueId,
        BattleAudioCue audioCue)
    {
        BattleAudioPresentationCueSO cue =
            CreateOrUpdateCue<BattleAudioPresentationCueSO>(
                assetName, cueId, 0f);
        var serialized = new SerializedObject(cue);
        serialized.FindProperty("audioCue").enumValueIndex = (int)audioCue;
        serialized.FindProperty("semanticCueId").stringValue = cueId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cue);
        return cue;
    }

    private static BattlePresentationRecipeSO CreateOrUpdateRecipe(
        string assetName,
        string recipeId,
        BattlePresentationMoment moment,
        BattlePresentationGateKind gateKind,
        float maximumGateSeconds,
        params RecipeBeatSeed[] beatSeeds)
    {
        string path = PresentationRecipeFolder + "/" + assetName + ".asset";
        BattlePresentationRecipeSO recipe =
            LoadOrCreate<BattlePresentationRecipeSO>(path);
        var serialized = new SerializedObject(recipe);
        serialized.FindProperty("recipeId").stringValue = recipeId;
        serialized.FindProperty("moment").enumValueIndex = (int)moment;
        serialized.FindProperty("gateKind").enumValueIndex = (int)gateKind;
        serialized.FindProperty("maximumGateSeconds").floatValue =
            Mathf.Max(0.1f, maximumGateSeconds);
        SerializedProperty beats = serialized.FindProperty("beats");
        beats.arraySize = beatSeeds != null ? beatSeeds.Length : 0;
        for (int beatIndex = 0; beatIndex < beats.arraySize; beatIndex++)
        {
            RecipeBeatSeed seed = beatSeeds[beatIndex];
            SerializedProperty beat = beats.GetArrayElementAtIndex(beatIndex);
            beat.FindPropertyRelative("beatId").stringValue = seed.BeatId;
            beat.FindPropertyRelative("delayBeforeSeconds").floatValue =
                seed.DelayBeforeSeconds;
            beat.FindPropertyRelative("minimumDurationSeconds").floatValue =
                seed.MinimumDurationSeconds;
            beat.FindPropertyRelative("releasesGate").boolValue =
                seed.ReleasesGate;
            SerializedProperty cues = beat.FindPropertyRelative("cues");
            cues.arraySize = seed.Cues.Length;
            for (int cueIndex = 0; cueIndex < seed.Cues.Length; cueIndex++)
            {
                RecipeCueSeed cueSeed = seed.Cues[cueIndex];
                SerializedProperty entry =
                    cues.GetArrayElementAtIndex(cueIndex);
                entry.FindPropertyRelative("cue").objectReferenceValue =
                    cueSeed.Cue;
                entry.FindPropertyRelative("condition").enumValueIndex =
                    (int)cueSeed.Condition;
                entry.FindPropertyRelative("lane").enumValueIndex =
                    (int)cueSeed.Lane;
                entry.FindPropertyRelative("conflictPolicy").enumValueIndex =
                    (int)BattlePresentationConflictPolicy.Replace;
                entry.FindPropertyRelative("blocksBeat").boolValue =
                    cueSeed.BlocksBeat;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static DRPGBattlePresentationSO CreateOrUpdatePresentation(
        Sprite background,
        DRPGBattleAudioPaletteSO audioPalette,
        BattlePresentationRecipeSetSO recipeSet)
    {
        DRPGBattlePresentationSO presentation =
            LoadOrCreate<DRPGBattlePresentationSO>(PresentationPath);
        var serialized = new SerializedObject(presentation);
        serialized.FindProperty("charcoal").colorValue = new Color32(17, 20, 28, 245);
        serialized.FindProperty("wineRed").colorValue = new Color32(84, 17, 36, 245);
        serialized.FindProperty("accentMagenta").colorValue =
            new Color32(239, 35, 112, 255);
        serialized.FindProperty("metallicWhite").colorValue =
            new Color32(226, 229, 236, 255);
        serialized.FindProperty("fallbackBattleBackground").objectReferenceValue =
            background;
        serialized.FindProperty("audioPalette").objectReferenceValue = audioPalette;
        serialized.FindProperty("recipeSet").objectReferenceValue = recipeSet;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
        return presentation;
    }

    private static TMP_FontAsset CreateOrUpdateJapaneseFont()
    {
        TMP_FontAsset fontAsset =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontPath);
        if (fontAsset != null)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            EnsureJapaneseQuoteGlyphs(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        Font sourceFont = LoadRequired<Font>(JapaneseFontSourcePath);
        fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            64,
            8,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);
        if (fontAsset == null)
            throw new InvalidOperationException("Failed to create the DRPG Japanese font asset.");

        fontAsset.name = "NotoSansSC_DRPG_Dynamic";
        fontAsset.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(fontAsset, JapaneseFontPath);
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        Texture2D[] atlases = fontAsset.atlasTextures;
        for (int i = 0; i < atlases.Length; i++)
        {
            if (atlases[i] != null && !AssetDatabase.Contains(atlases[i]))
                AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
        }
        EnsureJapaneseQuoteGlyphs(fontAsset);
        EditorUtility.SetDirty(fontAsset);
        return fontAsset;
    }

    private static void EnsureJapaneseQuoteGlyphs(TMP_FontAsset fontAsset)
    {
        fontAsset.TryAddCharacters(JapaneseQuoteCharacters, out _);
        var missing = new HashSet<char>();
        for (int i = 0; i < JapaneseQuoteCharacters.Length; i++)
        {
            char character = JapaneseQuoteCharacters[i];
            if (!fontAsset.HasCharacter(character))
                missing.Add(character);
        }
        if (missing.Count == 0)
            return;

        throw new InvalidOperationException(
            "The configured Japanese font is missing quote glyphs: " +
            string.Join(string.Empty, missing));
    }

    private static DRPGBattleAudioPaletteSO CreateOrUpdateAudioPalette()
    {
        AudioEventSO battleBgm = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_BattleBgm.asset",
            "Assets/Resources/Music/BK/Black.mp3",
            "Music",
            0.55f);
        AudioEventSO skillSelected = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_SkillSelected.asset",
            "Assets/Plugins/Pixel Crushers/Dialogue System/Prefabs/Art/Shared Audio/click.ogg",
            "UI",
            0.8f);
        AudioEventSO attack = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Attack.asset",
            "Assets/Gun Sounds Pack Vol 1/Gun Shot/Gunshot 4-6.wav",
            "SFX",
            0.45f);
        AudioEventSO hit = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Hit.asset",
            "Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianHit.wav",
            "SFX",
            0.75f);
        AudioEventSO breakEnter = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_BreakEnter.asset",
            "Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianThunder.wav",
            "SFX",
            0.65f);
        AudioEventSO breakExit = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_BreakExit.asset",
            "Assets/Feel/NiceVibrations/HapticSamples/ApplicationUX/Button1.wav",
            "UI",
            0.75f);
        AudioEventSO victory = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Victory.asset",
            "Assets/Plugins/Pixel Crushers/Quest Machine/Demo/Art/Sounds/fanfare.ogg",
            "UI",
            0.8f);
        AudioEventSO defeat = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Defeat.asset",
            "Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckDenied.wav",
            "UI",
            0.8f);
        AudioEventSO specialWarning = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_SpecialWarning.asset",
            "Assets/Feel/NiceVibrations/HapticSamples/ApplicationUX/Alert1.wav",
            "UI",
            0.92f);
        AudioEventSO interactiveWindowOpen = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_InteractiveWindowOpen.asset",
            "Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckWoosh.wav",
            "UI",
            0.72f);
        AudioEventSO guard = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Guard.asset",
            "Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianHit.wav",
            "SFX",
            0.78f);
        AudioEventSO perfectGuard = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_PerfectGuard.asset",
            "Assets/Feel/FeelDemosURP/Strike/Sounds/FeelStrikeSuccess.wav",
            "SFX",
            0.86f);
        AudioEventSO miss = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_Miss.asset",
            "Assets/Feel/FeelDemosURP/Strike/Sounds/FeelStrikeNoSuccess.wav",
            "UI",
            0.70f);
        AudioEventSO weakHit = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_WeakHit.asset",
            "Assets/Feel/FeelDemosURP/Strike/Sounds/FeelStrikeHit.wav",
            "SFX",
            0.82f);
        AudioEventSO interactiveWindowClose = CreateOrUpdateAudioEvent(
            AudioFolder + "/AE_DRPG_InteractiveWindowClose.asset",
            "Assets/Feel/NiceVibrations/HapticSamples/ApplicationUX/Button2.wav",
            "UI",
            0.64f);

        DRPGBattleAudioPaletteSO palette =
            LoadOrCreate<DRPGBattleAudioPaletteSO>(AudioPalettePath);
        var serialized = new SerializedObject(palette);
        serialized.FindProperty("battleBgm").objectReferenceValue = battleBgm;
        serialized.FindProperty("skillSelected").objectReferenceValue = skillSelected;
        serialized.FindProperty("defaultAttack").objectReferenceValue = attack;
        serialized.FindProperty("defaultHit").objectReferenceValue = hit;
        serialized.FindProperty("breakEnter").objectReferenceValue = breakEnter;
        serialized.FindProperty("breakExit").objectReferenceValue = breakExit;
        serialized.FindProperty("victory").objectReferenceValue = victory;
        serialized.FindProperty("defeat").objectReferenceValue = defeat;
        serialized.FindProperty("specialWarning").objectReferenceValue =
            specialWarning;
        serialized.FindProperty("interactiveWindowOpen").objectReferenceValue =
            interactiveWindowOpen;
        serialized.FindProperty("guard").objectReferenceValue = guard;
        serialized.FindProperty("perfectGuard").objectReferenceValue =
            perfectGuard;
        serialized.FindProperty("miss").objectReferenceValue = miss;
        serialized.FindProperty("weakHit").objectReferenceValue = weakHit;
        serialized.FindProperty("interactiveWindowClose").objectReferenceValue =
            interactiveWindowClose;
        serialized.FindProperty("skillOverrides").arraySize = 0;
        SetVoiceCuePlaceholders(serialized.FindProperty("voiceCues"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        return palette;
    }

    private static void SetVoiceCuePlaceholders(SerializedProperty voiceCues)
    {
        string[] suffixes = { "turn_1", "attack_1", "break_1", "defeated_1" };
        voiceCues.arraySize = BattleVoiceProfileIds.Length * suffixes.Length;
        int index = 0;
        for (int profileIndex = 0;
             profileIndex < BattleVoiceProfileIds.Length;
             profileIndex++)
        {
            for (int suffixIndex = 0; suffixIndex < suffixes.Length; suffixIndex++)
            {
                SerializedProperty entry = voiceCues.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("semanticCueId").stringValue =
                    BattleVoiceProfileIds[profileIndex] + "_" + suffixes[suffixIndex];
                entry.FindPropertyRelative("audioEvent").objectReferenceValue = null;
            }
        }
    }

    private static AudioEventSO CreateOrUpdateAudioEvent(
        string assetPath,
        string clipPath,
        string mixerGroupName,
        float volume)
    {
        AudioClip clip = LoadRequired<AudioClip>(clipPath);
        AudioEventSO audioEvent = LoadOrCreate<AudioEventSO>(assetPath);
        var serialized = new SerializedObject(audioEvent);
        SerializedProperty clips = serialized.FindProperty("clips");
        clips.arraySize = 1;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        AudioMixerGroup group = null;
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(mixerGroupName);
            if (groups != null && groups.Length > 0)
                group = groups[0];
        }
        serialized.FindProperty("outputGroup").objectReferenceValue = group;
        serialized.FindProperty("volume").floatValue = Mathf.Clamp01(volume);
        serialized.FindProperty("volumeRandomRange").floatValue = 0.03f;
        serialized.FindProperty("pitch").floatValue = 1f;
        serialized.FindProperty("pitchRandomRange").floatValue = 0.02f;
        serialized.FindProperty("spatialBlend").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(audioEvent);
        return audioEvent;
    }

    private static CharacterSO CreateOrUpdateCharacter(
        string id,
        string displayName,
        Sprite portrait,
        bool enemy)
    {
        string path = CharacterFolder + "/Character_" + id + ".asset";
        CharacterSO character = LoadOrCreate<CharacterSO>(path);
        character.characterName = displayName;
        character.characterIcon = portrait;
        character.battleCamp = enemy ? BattleCamp.EnemySide : BattleCamp.MateSide;
        character.maxHp = enemy ? 140f : 120f;
        EditorUtility.SetDirty(character);
        return character;
    }

    private static DRPGCombatantProfileSO CreateOrUpdateProfile(
        string id,
        string displayName,
        Sprite portrait,
        Sprite hitPortrait,
        Sprite damageBrokenPortrait,
        Sprite attackCutInPortrait,
        CharacterSO character,
        DRPGSkillSO attack,
        int index)
    {
        string path = ConfigFolder + "/DRPGCombatant_" + id + ".asset";
        DRPGCombatantProfileSO profile =
            LoadOrCreate<DRPGCombatantProfileSO>(path);
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("combatantId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("explorationCharacter").objectReferenceValue = character;
        serialized.FindProperty("fullPortrait").objectReferenceValue = portrait;
        serialized.FindProperty("hitPortrait").objectReferenceValue = hitPortrait;
        serialized.FindProperty("damageBrokenPortrait").objectReferenceValue = damageBrokenPortrait;
        serialized.FindProperty("attackCutInPortrait").objectReferenceValue =
            attackCutInPortrait;
        serialized.FindProperty("portraitFocus").vector2Value =
            index >= 3 ? new Vector2(0.5f, 0.72f) : new Vector2(0.5f, 0.80f);
        serialized.FindProperty("portraitScale").floatValue = index == 4 ? 1.08f : 1f;
        serialized.FindProperty("avatarZoom").floatValue = index >= 3 ? 1.9f : 2.25f;
        serialized.FindProperty("preferredFormationRow").intValue = index == 3
            ? (int)DRPGFormationRow.Rear
            : index == 4
                ? (int)DRPGFormationRow.Front
                : index == 2
                    ? (int)DRPGFormationRow.Middle
                    : (int)DRPGFormationRow.Front;
        serialized.FindProperty("formationVisualWidth").floatValue = index == 4
            ? 2.15f
            : index == 3 ? 1.25f : 1f;
        serialized.FindProperty("maxHp").floatValue = index >= 3 ? 140f : 120f;
        serialized.FindProperty("damageBrokenHpRatio").floatValue = 0.35f;
        serialized.FindProperty("agility").floatValue =
            new[] { 14f, 17f, 12f, 13f, 9f }[index];
        serialized.FindProperty("baseTimelineDelay").floatValue = 1000f;
        serialized.FindProperty("baseRecoveryDelay").floatValue = 80f;
        serialized.FindProperty("maxPosture").floatValue = 100f;
        serialized.FindProperty("postureRecoveryPerTurn").floatValue = 15f;
        serialized.FindProperty("postureBreakRecoveryRatio").floatValue = 0.5f;
        serialized.FindProperty("freeStrikeBodyBounds").rectValue =
            new Rect(0.08f, 0.05f, 0.84f, 0.90f);
        SerializedProperty skills = serialized.FindProperty("skills");
        skills.arraySize = 1;
        skills.GetArrayElementAtIndex(0).objectReferenceValue = attack;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static DRPGCombatantProfileSO CreateOrUpdateDebugBreakProfile(
        string id,
        string displayName,
        Sprite portrait,
        Sprite hitPortrait,
        Sprite damageBrokenPortrait,
        CharacterSO character,
        DRPGSkillSO attack,
        int index,
        GalActorSO galActor,
        DRPGBattleQuoteSetSO quotes)
    {
        DRPGCombatantProfileSO profile = CreateOrUpdateProfile(
            id,
            displayName,
            portrait,
            hitPortrait,
            damageBrokenPortrait,
            null,
            character,
            attack,
            index);
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("maxHp").floatValue = 999f;
        serialized.FindProperty("damageBrokenHpRatio").floatValue = 0.35f;
        serialized.FindProperty("baseRecoveryDelay").floatValue = 80f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        LinkProfilePresentationData(profile, galActor, quotes);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void SetProfileSkills(
        DRPGCombatantProfileSO profile,
        params DRPGSkillSO[] configuredSkills)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var serialized = new SerializedObject(profile);
        SerializedProperty skills = serialized.FindProperty("skills");
        int count = configuredSkills != null ? configuredSkills.Length : 0;
        skills.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            skills.GetArrayElementAtIndex(i).objectReferenceValue =
                configuredSkills[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static DRPGCombatantProfileSO CreateOrUpdateDebugDefeatProfile(
        string id,
        string displayName,
        Sprite portrait,
        CharacterSO character,
        DRPGSkillSO attack,
        int index,
        GalActorSO galActor,
        DRPGBattleQuoteSetSO quotes)
    {
        DRPGCombatantProfileSO profile = CreateOrUpdateProfile(
            id,
            displayName,
            portrait,
            null,
            null,
            null,
            character,
            attack,
            index);
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("maxHp").floatValue = 999999f;
        serialized.FindProperty("damageBrokenHpRatio").floatValue = 0.35f;
        serialized.FindProperty("baseRecoveryDelay").floatValue = 80f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        LinkProfilePresentationData(profile, galActor, quotes);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static GalActorSO CreateOrUpdateGalActor(
        string id,
        string displayName,
        Sprite portrait,
        Sprite hitPortrait,
        Sprite damageBrokenPortrait)
    {
        string path = GalFolder + "/GalActor_" + id + ".asset";
        GalActorSO actor = LoadOrCreate<GalActorSO>(path);
        var serialized = new SerializedObject(actor);
        serialized.FindProperty("actorId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("defaultPortrait").objectReferenceValue = portrait;
        SetGalPortraitVariants(
            serialized.FindProperty("portraitVariants"),
            hitPortrait,
            damageBrokenPortrait);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actor);
        return actor;
    }

    private static void SetGalPortraitVariants(
        SerializedProperty variants,
        Sprite hitPortrait,
        Sprite damageBrokenPortrait)
    {
        int count = hitPortrait != null && damageBrokenPortrait != null
            ? 2
            : hitPortrait != null || damageBrokenPortrait != null
                ? 1
                : 0;
        variants.arraySize = count;
        int index = 0;
        if (hitPortrait != null)
        {
            SerializedProperty hit = variants.GetArrayElementAtIndex(index++);
            hit.FindPropertyRelative("key").stringValue = "hit";
            hit.FindPropertyRelative("sprite").objectReferenceValue = hitPortrait;
        }
        if (damageBrokenPortrait != null)
        {
            SerializedProperty broken = variants.GetArrayElementAtIndex(index);
            broken.FindPropertyRelative("key").stringValue = "broken";
            broken.FindPropertyRelative("sprite").objectReferenceValue = damageBrokenPortrait;
        }
    }

    private static DRPGBattleQuoteSetSO CreateOrUpdateBattleQuotes(
        string id,
        string displayName,
        int index)
    {
        string path = ConfigFolder + "/DRPGBattleQuotes_" + id + ".asset";
        DRPGBattleQuoteSetSO quotes = LoadOrCreate<DRPGBattleQuoteSetSO>(path);
        var serialized = new SerializedObject(quotes);
        SetQuoteLines(
            serialized.FindProperty("turnBeginLines"),
            new[] { ResolveJapaneseQuote(index, DRPGBattleQuoteContext.TurnBegin) },
            id + "_turn");
        SetQuoteLines(
            serialized.FindProperty("attackLines"),
            new[] { ResolveJapaneseQuote(index, DRPGBattleQuoteContext.Attack) },
            id + "_attack");
        SetQuoteLines(
            serialized.FindProperty("hitLines"),
            new[] { index >= 3 ? "損傷を確認。" : "まだ終わってない……！" },
            id + "_hit");
        SetQuoteLines(
            serialized.FindProperty("breakLines"),
            new[] { ResolveJapaneseQuote(index, DRPGBattleQuoteContext.Break) },
            id + "_break");
        SetQuoteLines(
            serialized.FindProperty("defeatedLines"),
            new[] { ResolveJapaneseQuote(index, DRPGBattleQuoteContext.Defeated) },
            id + "_defeated");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quotes);
        return quotes;
    }

    private static string ResolveJapaneseQuote(
        int index,
        DRPGBattleQuoteContext context)
    {
        string[,] lines =
        {
            { "僕の番だ。行こう。", "これで決める！", "まだ……終われない。", "ごめん……もう、動けない……。" },
            { "私の出番ね。", "そこっ！", "まだ……負けない！", "ここまで、なの……？" },
            { "準備はできています。", "行きます！", "くっ……まだです。", "申し訳……ありません……。" },
            { "標的を確認。", "排除する。", "機能低下……。", "任務……失敗……。" },
            { "殲滅を開始する。", "逃がさない。", "損傷、危険域。", "機能……停止……。" }
        };
        int row = Mathf.Clamp(index, 0, lines.GetLength(0) - 1);
        int column = context == DRPGBattleQuoteContext.TurnBegin
            ? 0
            : context == DRPGBattleQuoteContext.Attack
                ? 1
                : context == DRPGBattleQuoteContext.Break
                    ? 2
                    : 3;
        return lines[row, column];
    }

    private static void LinkProfilePresentationData(
        DRPGCombatantProfileSO profile,
        GalActorSO galActor,
        DRPGBattleQuoteSetSO quotes)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("galActor").objectReferenceValue = galActor;
        serialized.FindProperty("battleQuotes").objectReferenceValue = quotes;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void SetQuoteLines(
        SerializedProperty property,
        IReadOnlyList<string> lines,
        string voicePrefix)
    {
        property.arraySize = lines.Count;
        for (int i = 0; i < lines.Count; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("text").stringValue = lines[i];
            element.FindPropertyRelative("voiceCueId").stringValue =
                voicePrefix + "_" + (i + 1);
        }
    }

    private static DRPGPartySetupSO CreateOrUpdateParty(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO party01,
        DRPGCombatantProfileSO party02)
    {
        DRPGPartySetupSO party = LoadOrCreate<DRPGPartySetupSO>(PartyPath);
        var serialized = new SerializedObject(party);
        serialized.FindProperty("protagonist").objectReferenceValue = protagonist;
        SerializedProperty members = serialized.FindProperty("partyMembers");
        members.arraySize = 2;
        members.GetArrayElementAtIndex(0).objectReferenceValue = party01;
        members.GetArrayElementAtIndex(1).objectReferenceValue = party02;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(party);
        return party;
    }

    private static DRPGPartySetupSO CreateOrUpdateDebugBreakParty(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO party01,
        DRPGCombatantProfileSO party02)
    {
        DRPGPartySetupSO party = LoadOrCreate<DRPGPartySetupSO>(DebugBreakPartyPath);
        var serialized = new SerializedObject(party);
        serialized.FindProperty("protagonist").objectReferenceValue = protagonist;
        SerializedProperty members = serialized.FindProperty("partyMembers");
        members.arraySize = 2;
        members.GetArrayElementAtIndex(0).objectReferenceValue = party01;
        members.GetArrayElementAtIndex(1).objectReferenceValue = party02;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(party);
        return party;
    }

    private static DRPGEncounterDefinitionSO CreateOrUpdateEncounter(
        DRPGCombatantProfileSO enemy01,
        DRPGCombatantProfileSO enemy02,
        Sprite background,
        DRPGBattlePresentationSO presentation)
    {
        DRPGEncounterDefinitionSO encounter =
            LoadOrCreate<DRPGEncounterDefinitionSO>(EncounterPath);
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("encounterId").stringValue = "drpg_test_3v2";
        SerializedProperty enemies = serialized.FindProperty("enemies");
        enemies.arraySize = 2;
        enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy01;
        enemies.GetArrayElementAtIndex(1).objectReferenceValue = enemy02;
        SetEnemyDeployments(
            serialized,
            new[]
            {
                enemy02, enemy01, enemy02, enemy01, enemy02,
                enemy01, enemy02, enemy01, enemy02, enemy01
            },
            new[]
            {
                DRPGFormationRow.Front,
                DRPGFormationRow.Middle,
                DRPGFormationRow.Rear,
                DRPGFormationRow.Front,
                DRPGFormationRow.Middle,
                DRPGFormationRow.Rear,
                DRPGFormationRow.Front,
                DRPGFormationRow.Middle,
                DRPGFormationRow.Rear,
                DRPGFormationRow.Front
            });
        serialized.FindProperty("battleBackground").objectReferenceValue = background;
        serialized.FindProperty("presentation").objectReferenceValue = presentation;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static DRPGEncounterDefinitionSO CreateOrUpdateDebugBreakEncounter(
        DRPGCombatantProfileSO enemy01,
        DRPGCombatantProfileSO enemy02,
        Sprite background,
        DRPGBattlePresentationSO presentation)
    {
        DRPGEncounterDefinitionSO encounter =
            LoadOrCreate<DRPGEncounterDefinitionSO>(DebugBreakEncounterPath);
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("encounterId").stringValue =
            "drpg_debug_break_presentation";
        SerializedProperty enemies = serialized.FindProperty("enemies");
        enemies.arraySize = 2;
        enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy01;
        enemies.GetArrayElementAtIndex(1).objectReferenceValue = enemy02;
        SetEnemyDeployments(
            serialized,
            new[] { enemy01, enemy02 },
            new[] { DRPGFormationRow.Front, DRPGFormationRow.Middle });
        serialized.FindProperty("battleBackground").objectReferenceValue = background;
        serialized.FindProperty("presentation").objectReferenceValue = presentation;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static DRPGEncounterDefinitionSO CreateOrUpdateDebugDefeatEncounter(
        DRPGCombatantProfileSO enemy01,
        DRPGCombatantProfileSO enemy02,
        Sprite background,
        DRPGBattlePresentationSO presentation)
    {
        DRPGEncounterDefinitionSO encounter =
            LoadOrCreate<DRPGEncounterDefinitionSO>(DebugDefeatEncounterPath);
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("encounterId").stringValue =
            "drpg_debug_defeat_presentation";
        SerializedProperty enemies = serialized.FindProperty("enemies");
        enemies.arraySize = 2;
        enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy01;
        enemies.GetArrayElementAtIndex(1).objectReferenceValue = enemy02;
        SetEnemyDeployments(
            serialized,
            new[] { enemy01, enemy02 },
            new[] { DRPGFormationRow.Front, DRPGFormationRow.Middle });
        serialized.FindProperty("battleBackground").objectReferenceValue = background;
        serialized.FindProperty("presentation").objectReferenceValue = presentation;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static void SetEnemyDeployments(
        SerializedObject serialized,
        IReadOnlyList<DRPGCombatantProfileSO> profiles,
        IReadOnlyList<DRPGFormationRow> rows)
    {
        if (profiles == null || rows == null || profiles.Count != rows.Count)
        {
            throw new ArgumentException(
                "Enemy deployment profiles and rows must have matching counts.");
        }
        SerializedProperty deployments =
            serialized.FindProperty("enemyDeployments");
        deployments.arraySize = profiles.Count;
        for (int i = 0; i < profiles.Count; i++)
        {
            SerializedProperty entry = deployments.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("profile").objectReferenceValue = profiles[i];
            entry.FindPropertyRelative("preferredRow").intValue = (int)rows[i];
        }

        SerializedProperty rules = serialized.FindProperty("formationRules");
        rules.FindPropertyRelative("initialEnemyLimit").intValue = 6;
        rules.FindPropertyRelative("activeEnemyLimit").intValue = 6;
        rules.FindPropertyRelative("perRowLimit").intValue = 2;
        rules.FindPropertyRelative("reinforcementDelayActions").intValue = 1;
    }

    private static GameObject CreatePresentationPrefab(
        TMP_FontAsset font,
        TMP_FontAsset japaneseFont)
    {
        var root = new GameObject(
            "PF_DRPGBattlePresentation",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(DRPGBattlePresentationRoot));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4500;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject visualRoot = CreateUIObject("VisualRoot", root.transform);
            Stretch(visualRoot.GetComponent<RectTransform>());

            Image backgroundImage = CreateImage(
                "BattleBackground",
                visualRoot.transform,
                Color.white);
            Stretch(backgroundImage.rectTransform);
            backgroundImage.raycastTarget = false;
            AspectRatioFitter fitter =
                backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var backgroundPresenter =
                backgroundImage.gameObject.AddComponent<BattleBackgroundPresenter>();
            SetReference(backgroundPresenter, "background", backgroundImage);

            Image dimmer = CreateImage(
                "AtmosphereDimmer",
                visualRoot.transform,
                new Color(0.025f, 0.03f, 0.05f, 0.28f));
            Stretch(dimmer.rectTransform);
            dimmer.raycastTarget = false;

            BattleTimelineView timeline = CreateTimeline(visualRoot.transform);
            BattleSkillMenu skillMenu = CreateSkillMenu(visualRoot.transform, font);
            BattlefieldEnemyRosterView enemyRoster =
                CreateEnemyBattlefield(visualRoot.transform, font);
            TMP_Text targetPrompt = CreateTargetPrompt(visualRoot.transform, font);
            BattleTurnQuotePresenter turnQuote =
                CreateTurnQuote(
                    visualRoot.transform,
                    japaneseFont != null ? japaneseFont : font);
            BattlePartyHud partyHud = CreatePartyHud(visualRoot.transform, font);
            BattleFormationOverlayController formationOverlay =
                root.AddComponent<BattleFormationOverlayController>();
            BattleActorPresenter actor = CreateActorPresenter(visualRoot.transform, font);
            BattleAttackCutInPresenter attackCutIn =
                CreateAttackCutIn(visualRoot.transform, font);
            SetReference(attackCutIn, "idleActorPresenter", actor);
            BattleImpactFeedbackPresenter impactFeedback = CreateImpactFeedback(
                visualRoot.transform,
                font,
                partyHud,
                enemyRoster);
            BattleInteractiveCombatView interactiveCombat =
                CreateInteractiveCombatView(visualRoot.transform, font);
            BattleDamageBrokenCutInPresenter damageBrokenCutIn =
                CreateDamageBrokenCutIn(visualRoot.transform, font);
            DRPGBattleAudioCueSink audioSink =
                root.AddComponent<DRPGBattleAudioCueSink>();
            DRPGBattlePresentationDirector director =
                root.AddComponent<DRPGBattlePresentationDirector>();
            DRPGBattleActionTransactionCoordinator coordinator =
                root.AddComponent<DRPGBattleActionTransactionCoordinator>();
            DRPGBattleUGUISurface surface =
                root.AddComponent<DRPGBattleUGUISurface>();
            DRPGBattleInteractionController interaction =
                root.AddComponent<DRPGBattleInteractionController>();
            DRPGBattlePresentationEventBridge eventBridge =
                root.AddComponent<DRPGBattlePresentationEventBridge>();

            BattleTimelineCueExecutor timelineExecutor =
                root.AddComponent<BattleTimelineCueExecutor>();
            BattleTargetFeedbackCueExecutor targetFeedbackExecutor =
                root.AddComponent<BattleTargetFeedbackCueExecutor>();
            BattleActorMotionCueExecutor actorMotionExecutor =
                root.AddComponent<BattleActorMotionCueExecutor>();
            BattleAttackCutInCueExecutor attackCutInExecutor =
                root.AddComponent<BattleAttackCutInCueExecutor>();
            BattleImpactCueExecutor impactExecutor =
                root.AddComponent<BattleImpactCueExecutor>();
            BattleDamageBrokenCutInCueExecutor breakExecutor =
                root.AddComponent<BattleDamageBrokenCutInCueExecutor>();
            BattleQuoteCueExecutor quoteExecutor =
                root.AddComponent<BattleQuoteCueExecutor>();
            BattleAudioPresentationCueExecutor audioExecutor =
                root.AddComponent<BattleAudioPresentationCueExecutor>();

            DRPGBattlePresentationRoot presentation =
                root.GetComponent<DRPGBattlePresentationRoot>();
            SetReference(presentation, "visibleRoot", visualRoot);
            SetReference(presentation, "surface", surface);
            SetReference(presentation, "director", director);
            SetReference(presentation, "coordinator", coordinator);
            SetReference(presentation, "interaction", interaction);
            SetReference(presentation, "eventBridge", eventBridge);

            SetReference(surface, "backgroundPresenter", backgroundPresenter);
            SetReference(surface, "timelineView", timeline);
            SetReference(surface, "partyHud", partyHud);
            SetReference(surface, "actorPresenter", actor);
            SetReference(surface, "skillMenu", skillMenu);
            SetReference(surface, "enemyRoster", enemyRoster);
            SetReference(surface, "impactFeedbackPresenter", impactFeedback);
            SetReference(surface, "attackCutInPresenter", attackCutIn);
            SetReference(surface, "damageBrokenCutInPresenter", damageBrokenCutIn);
            SetReference(surface, "turnQuotePresenter", turnQuote);
            SetReference(surface, "interactiveCombatView", interactiveCombat);
            SetReference(surface, "audioCueSinkComponent", audioSink);
            SetReference(surface, "targetPrompt", targetPrompt);
            SetReference(surface, "formationOverlayController", formationOverlay);

            SetReference(formationOverlay, "enemyRoster", enemyRoster);
            SetReference(formationOverlay, "partyHud", partyHud);

            SetReference(coordinator, "directorComponent", director);
            SetReference(coordinator, "interactiveView", interactiveCombat);
            SetReference(interaction, "directorComponent", director);
            SetReference(interaction, "surface", surface);
            SetReference(interaction, "skillMenu", skillMenu);
            SetReference(interaction, "enemyRoster", enemyRoster);
            SetReference(eventBridge, "directorComponent", director);
            SetReference(eventBridge, "surface", surface);
            SetReference(eventBridge, "interaction", interaction);

            SetReference(timelineExecutor, "timelineView", timeline);
            SetReference(targetFeedbackExecutor, "surfaceComponent", surface);
            SetReference(actorMotionExecutor, "presenter", actor);
            SetReference(attackCutInExecutor, "presenter", attackCutIn);
            SetReference(impactExecutor, "presenter", impactFeedback);
            SetReference(breakExecutor, "presenter", damageBrokenCutIn);
            SetReference(quoteExecutor, "presenter", turnQuote);
            SetReference(audioExecutor, "audioCueSinkComponent", audioSink);
            SetObjectList(
                director,
                "cueExecutorBehaviours",
                new MonoBehaviour[]
                {
                    timelineExecutor,
                    targetFeedbackExecutor,
                    actorMotionExecutor,
                    attackCutInExecutor,
                    impactExecutor,
                    breakExecutor,
                    quoteExecutor,
                    audioExecutor
                });
            visualRoot.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Could not save " + PrefabPath);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static BattleInteractiveCombatView CreateInteractiveCombatView(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject layer = CreateUIObject("InteractiveCombatLayer", parent);
        Stretch(layer.GetComponent<RectTransform>());
        layer.transform.SetAsLastSibling();
        BattleInteractiveCombatView view =
            layer.AddComponent<BattleInteractiveCombatView>();

        Image panel = CreateImage(
            "InteractionPanel",
            layer.transform,
            Color.clear);
        Stretch(panel.rectTransform);
        panel.raycastTarget = false;
        CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        Image veil = CreateImage(
            "InteractionVeil",
            panel.transform,
            new Color(0.015f, 0.018f, 0.05f, 0.24f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = false;

        Image headerPlate = CreateImage(
            "InteractionHeaderPlate",
            panel.transform,
            new Color32(8, 9, 20, 232));
        SetRect(
            headerPlate.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -176f),
            new Vector2(1220f, 214f));
        headerPlate.raycastTarget = false;
        Outline plateOutline = headerPlate.gameObject.AddComponent<Outline>();
        plateOutline.effectColor = new Color32(239, 35, 112, 238);
        plateOutline.effectDistance = new Vector2(4f, -4f);

        Image frame = CreateImage(
            "SignalRule",
            headerPlate.transform,
            new Color32(239, 35, 112, 255));
        SetRect(
            frame.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 8f));
        frame.raycastTarget = false;

        Image leftNotch = CreateImage(
            "LeftNotch",
            headerPlate.transform,
            new Color32(239, 35, 112, 255));
        SetRect(
            leftNotch.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(-18f, 0f),
            new Vector2(18f, 126f));
        leftNotch.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, -8f);
        leftNotch.raycastTarget = false;

        TMP_Text alert = CreateText(
            "Alert",
            headerPlate.transform,
            font,
            "SPECIAL ATTACK  //  INTERACTION",
            31f,
            TextAlignmentOptions.Left,
            new Color32(255, 239, 246, 255));
        SetRect(
            alert.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(36f, -22f),
            new Vector2(-72f, 42f));
        alert.fontStyle = FontStyles.Bold;
        alert.characterSpacing = 2.4f;

        TMP_Text mode = CreateText(
            "Mode",
            headerPlate.transform,
            font,
            "交互窗口",
            45f,
            TextAlignmentOptions.Left,
            new Color32(255, 255, 255, 255));
        SetRect(
            mode.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0.42f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(34f, 8f),
            new Vector2(-68f, 62f));
        mode.fontStyle = FontStyles.Bold;

        TMP_Text target = CreateText(
            "Target",
            headerPlate.transform,
            font,
            "TARGET",
            25f,
            TextAlignmentOptions.Left,
            new Color32(191, 204, 224, 255));
        SetRect(
            target.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.60f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(34f, 30f),
            new Vector2(-68f, 38f));

        TMP_Text countdown = CreateText(
            "Countdown",
            headerPlate.transform,
            font,
            "3.00",
            74f,
            TextAlignmentOptions.Center,
            new Color32(255, 255, 255, 255));
        SetRect(
            countdown.rectTransform,
            new Vector2(0.72f, 0.5f),
            new Vector2(0.92f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -2f),
            new Vector2(220f, 92f));
        countdown.fontStyle = FontStyles.Bold;

        TMP_Text prompt = CreateText(
            "Prompt",
            headerPlate.transform,
            font,
            "SPACE  //  防御",
            24f,
            TextAlignmentOptions.Center,
            new Color32(24, 217, 255, 255));
        SetRect(
            prompt.rectTransform,
            new Vector2(0.67f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-22f, 28f),
            new Vector2(-44f, 38f));
        prompt.fontStyle = FontStyles.Bold;

        Image timerTrack = CreateImage(
            "TimerTrack",
            headerPlate.transform,
            new Color32(45, 48, 62, 245));
        SetRect(
            timerTrack.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 5f),
            new Vector2(-18f, 10f));
        timerTrack.raycastTarget = false;
        Image timerFill = CreateImage(
            "TimerFill",
            timerTrack.transform,
            new Color32(239, 35, 112, 255));
        Stretch(timerFill.rectTransform, 1f);
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        timerFill.fillAmount = 1f;
        timerFill.raycastTarget = false;

        Image postureTrack = CreateImage(
            "PostureTrack",
            panel.transform,
            new Color32(24, 27, 38, 238));
        SetRect(
            postureTrack.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 118f),
            new Vector2(760f, 18f));
        postureTrack.raycastTarget = false;
        Image postureFill = CreateImage(
            "PostureFill",
            postureTrack.transform,
            new Color32(255, 208, 77, 255));
        Stretch(postureFill.rectTransform, 2f);
        postureFill.type = Image.Type.Filled;
        postureFill.fillMethod = Image.FillMethod.Horizontal;
        postureFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        postureFill.fillAmount = 1f;
        postureFill.raycastTarget = false;

        TMP_Text feedback = CreateText(
            "InteractionFeedback",
            panel.transform,
            font,
            string.Empty,
            86f,
            TextAlignmentOptions.Center,
            Color.white);
        SetRect(
            feedback.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.one * 0.5f,
            new Vector2(0f, -56f),
            new Vector2(1100f, 140f));
        feedback.fontStyle = FontStyles.Bold;
        Outline feedbackOutline = feedback.gameObject.AddComponent<Outline>();
        feedbackOutline.effectColor = new Color32(10, 8, 18, 255);
        feedbackOutline.effectDistance = new Vector2(5f, -5f);

        TMP_Text resource = CreateText(
            "InputResource",
            panel.transform,
            font,
            "CHARGE  —",
            23f,
            TextAlignmentOptions.Center,
            new Color32(255, 208, 77, 255));
        SetRect(
            resource.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-310f, 151f),
            new Vector2(300f, 34f));
        resource.fontStyle = FontStyles.Bold;

        TMP_Text postureRisk = CreateText(
            "InputPostureRisk",
            panel.transform,
            font,
            "POSTURE  —",
            23f,
            TextAlignmentOptions.Center,
            new Color32(255, 239, 246, 255));
        SetRect(
            postureRisk.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 151f),
            new Vector2(300f, 34f));
        postureRisk.fontStyle = FontStyles.Bold;

        TMP_Text durabilityRisk = CreateText(
            "InputDurabilityRisk",
            panel.transform,
            font,
            "DURABILITY  —",
            23f,
            TextAlignmentOptions.Center,
            new Color32(24, 217, 255, 255));
        SetRect(
            durabilityRisk.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(310f, 151f),
            new Vector2(300f, 34f));
        durabilityRisk.fontStyle = FontStyles.Bold;

        Image defenseCrest = CreateImage(
            "DefenseCrest",
            panel.transform,
            new Color(1f, 1f, 1f, 0.82f));
        defenseCrest.sprite =
            LoadOptional<Sprite>(InteractiveDefenseCrestPath);
        defenseCrest.preserveAspect = true;
        SetRect(
            defenseCrest.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(430f, 430f));
        defenseCrest.raycastTarget = false;

        Image pulse = CreateImage(
            "DefenseTimingPulse",
            panel.transform,
            new Color(0.10f, 0.82f, 1f, 0.82f));
        pulse.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/Knob.psd");
        SetRect(
            pulse.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(190f, 190f));
        pulse.raycastTarget = false;
        Image pulseCore = CreateImage(
            "PulseCore",
            pulse.transform,
            new Color(0.025f, 0.03f, 0.07f, 0.82f));
        pulseCore.sprite = pulse.sprite;
        SetRect(
            pulseCore.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(142f, 142f));
        pulseCore.raycastTarget = false;

        GameObject crosshairObject = CreateUIObject("FreeStrikeCrosshair", panel.transform);
        RectTransform crosshair = crosshairObject.GetComponent<RectTransform>();
        SetRect(
            crosshair,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(118f, 118f));
        CreateReticleLine("Top", crosshair, new Vector2(0f, 39f), new Vector2(5f, 34f));
        CreateReticleLine("Bottom", crosshair, new Vector2(0f, -39f), new Vector2(5f, 34f));
        CreateReticleLine("Left", crosshair, new Vector2(-39f, 0f), new Vector2(34f, 5f));
        CreateReticleLine("Right", crosshair, new Vector2(39f, 0f), new Vector2(34f, 5f));
        Image reticleCore = CreateImage(
            "Core",
            crosshair,
            new Color32(255, 208, 77, 255));
        SetRect(
            reticleCore.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(12f, 12f));
        reticleCore.raycastTarget = false;

        Image slash = CreateImage(
            "FreeSlashPreview",
            panel.transform,
            new Color(1f, 0.05f, 0.48f, 0.72f));
        SetRect(
            slash.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            new Vector2(420f, 12f));
        slash.raycastTarget = false;
        Shadow slashGlow = slash.gameObject.AddComponent<Shadow>();
        slashGlow.effectColor = new Color(1f, 0.30f, 0.62f, 0.75f);
        slashGlow.effectDistance = new Vector2(0f, 7f);

        Image telegraph = CreateImage(
            "SpecialTelegraphBanner",
            layer.transform,
            new Color32(50, 5, 24, 246));
        SetRect(
            telegraph.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -152f),
            new Vector2(1120f, 82f));
        telegraph.raycastTarget = false;
        Outline telegraphOutline = telegraph.gameObject.AddComponent<Outline>();
        telegraphOutline.effectColor = new Color32(242, 12, 56, 255);
        telegraphOutline.effectDistance = new Vector2(4f, -4f);
        CanvasGroup telegraphGroup =
            telegraph.gameObject.AddComponent<CanvasGroup>();
        telegraphGroup.interactable = false;
        telegraphGroup.blocksRaycasts = false;
        TMP_Text telegraphText = CreateText(
            "TelegraphText",
            telegraph.transform,
            font,
            "⚠ ENEMY 锁定 TARGET  //  SPECIAL",
            30f,
            TextAlignmentOptions.Center,
            new Color32(255, 238, 244, 255));
        Stretch(telegraphText.rectTransform, 16f);
        telegraphText.fontStyle = FontStyles.Bold;
        telegraphText.characterSpacing = 1.2f;

        SetReference(view, "panelGroup", panelGroup);
        SetReference(view, "panelTransform", panel.rectTransform);
        SetReference(view, "veilImage", veil);
        SetReference(view, "frameImage", frame);
        SetReference(view, "alertText", alert);
        SetReference(view, "modeText", mode);
        SetReference(view, "targetText", target);
        SetReference(view, "countdownText", countdown);
        SetReference(view, "promptText", prompt);
        SetReference(view, "feedbackText", feedback);
        SetReference(view, "resourceText", resource);
        SetReference(view, "postureRiskText", postureRisk);
        SetReference(view, "durabilityRiskText", durabilityRisk);
        SetReference(view, "timerFill", timerFill);
        SetReference(view, "postureFill", postureFill);
        SetReference(view, "defenseCrest", defenseCrest);
        SetReference(view, "timingPulse", pulse.rectTransform);
        SetReference(view, "timingPulseImage", pulse);
        SetReference(view, "crosshair", crosshair);
        SetReference(view, "slashPreview", slash.rectTransform);
        SetReference(view, "telegraphGroup", telegraphGroup);
        SetReference(view, "telegraphText", telegraphText);

        panel.gameObject.SetActive(false);
        telegraph.gameObject.SetActive(false);
        return view;
    }

    private static void CreateReticleLine(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        Image line = CreateImage(
            name,
            parent,
            new Color32(255, 45, 126, 245));
        SetRect(
            line.rectTransform,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            position,
            size);
        line.raycastTarget = false;
    }

    private static BattleTimelineView CreateTimeline(Transform parent)
    {
        Image panel = CreateImage(
            "ActionTimeline",
            parent,
            new Color32(17, 20, 28, 232));
        SetRect(panel.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -20f),
            new Vector2(1320f, 132f));
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(239, 35, 112, 220);
        outline.effectDistance = new Vector2(0f, -3f);
        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var entries = new List<BattleTimelineEntryView>();
        for (int i = 0; i < 20; i++)
        {
            Image cell = CreateImage(
                "TimelineEntry_" + i,
                panel.transform,
                new Color32(35, 38, 49, 255));
            LayoutElement element = cell.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 56f;
            element.preferredHeight = 106f;

            GameObject viewport = CreateUIObject("PortraitMask", cell.transform);
            Stretch(viewport.GetComponent<RectTransform>(), 5f);
            viewport.AddComponent<RectMask2D>();
            Image portrait = CreateImage("Portrait", viewport.transform, Color.white);
            Stretch(portrait.rectTransform, -16f);
            portrait.raycastTarget = false;

            Image frame = cell;
            frame.raycastTarget = false;

            Image targetOverlay = CreateTargetFeedbackFrame(
                cell.transform, out Outline targetOutline);

            Image marker = CreateImage(
                "CurrentMarker", cell.transform,
                new Color32(239, 35, 112, 255));
            SetRect(marker.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 6f));
            marker.raycastTarget = false;

            BattleTimelineEntryView view =
                cell.gameObject.AddComponent<BattleTimelineEntryView>();
            SetReference(view, "portrait", portrait);
            SetReference(view, "frame", frame);
            SetReference(view, "currentActorMarker", marker.gameObject);
            SetReference(view, "targetOutline", targetOutline);
            SetReference(view, "targetOverlay", targetOverlay);
            entries.Add(view);
        }

        Image transitionPulse = CreateImage(
            "TimelineTransitionPulse",
            panel.transform,
            new Color32(255, 79, 145, 0));
        Stretch(transitionPulse.rectTransform, -4f);
        transitionPulse.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");
        transitionPulse.type = Image.Type.Sliced;
        transitionPulse.fillCenter = false;
        transitionPulse.raycastTarget = false;
        LayoutElement pulseLayout =
            transitionPulse.gameObject.AddComponent<LayoutElement>();
        pulseLayout.ignoreLayout = true;
        transitionPulse.transform.SetAsLastSibling();

        BattleTimelineView timeline = panel.gameObject.AddComponent<BattleTimelineView>();
        SetObjectList(timeline, "entries", entries);
        SetReference(timeline, "layoutGroup", layout);
        SetReference(timeline, "transitionPulse", transitionPulse);
        return timeline;
    }

    private static BattleSkillMenu CreateSkillMenu(Transform parent, TMP_FontAsset font)
    {
        Image panel = CreateImage(
            "CommandPanel",
            parent,
            new Color32(17, 20, 28, 238));
        SetRect(panel.rectTransform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(36f, 55f),
            new Vector2(500f, 650f));
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(84, 17, 36, 255);
        outline.effectDistance = new Vector2(4f, -4f);

        TMP_Text header = CreateText(
            "Header", panel.transform, font, "BATTLE COMMAND", 30f,
            TextAlignmentOptions.Left, new Color32(226, 229, 236, 255));
        SetRect(header.rectTransform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(24f, -18f),
            new Vector2(-48f, 48f));

        var buttons = new List<Button>();
        var labels = new List<TMP_Text>();
        var rangeIndicators = new List<BattleFormationRangeIndicator>();
        for (int i = 0; i < 6; i++)
        {
            Image buttonImage = CreateImage(
                "SkillButton_" + i,
                panel.transform,
                i == 0
                    ? new Color32(239, 35, 112, 235)
                    : new Color32(46, 47, 58, 235));
            SetRect(buttonImage.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(24f, -88f - i * 84f),
                new Vector2(-48f, 66f));
            Button button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color32(255, 82, 145, 255);
            colors.pressedColor = new Color32(132, 20, 60, 255);
            button.colors = colors;

            TMP_Text label = CreateText(
                "Label", buttonImage.transform, font,
                i == 0 ? "普通攻击" : "技能槽", 27f,
                TextAlignmentOptions.MidlineLeft,
                new Color32(240, 241, 245, 255));
            SetRect(
                label.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.one * 0.5f,
                new Vector2(20f, 0f),
                new Vector2(-168f, -10f));

            GameObject rangeRoot = CreateUIObject(
                "FormationRange",
                buttonImage.transform);
            SetRect(
                rangeRoot.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(126f, 30f));
            Image[] chips = new Image[3];
            for (int chipIndex = 0; chipIndex < chips.Length; chipIndex++)
            {
                chips[chipIndex] = CreateImage(
                    "Range_" + (chipIndex + 1),
                    rangeRoot.transform,
                    DRPGFormationColorPalette.Resolve(
                        (DRPGFormationRow)chipIndex));
                SetRect(
                    chips[chipIndex].rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(chipIndex * 42f, 0f),
                    new Vector2(34f, 12f));
                chips[chipIndex].raycastTarget = false;
            }
            BattleFormationRangeIndicator range =
                rangeRoot.AddComponent<BattleFormationRangeIndicator>();
            SetReference(range, "frontChip", chips[0]);
            SetReference(range, "middleChip", chips[1]);
            SetReference(range, "rearChip", chips[2]);
            labels.Add(label);
            buttons.Add(button);
            rangeIndicators.Add(range);
        }

        TMP_Text feedback = CreateText(
            "Feedback", panel.transform, font, string.Empty, 20f,
            TextAlignmentOptions.BottomLeft, new Color32(255, 124, 154, 255));
        SetRect(feedback.rectTransform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(24f, 18f),
            new Vector2(-48f, 48f));

        BattleSkillMenu menu = panel.gameObject.AddComponent<BattleSkillMenu>();
        SetObjectList(menu, "buttons", buttons);
        SetObjectList(menu, "labels", labels);
        SetObjectList(menu, "rangeIndicators", rangeIndicators);
        SetReference(menu, "feedbackText", feedback);
        return menu;
    }

    private static BattlefieldEnemyRosterView CreateEnemyBattlefield(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject root = CreateUIObject("EnemyBattlefield", parent);
        SetRect(root.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(92f, 18f),
            new Vector2(1220f, 710f));

        TMP_Text reinforcementWarning = CreateText(
            "ReinforcementWarning",
            root.transform,
            font,
            "其余怪物即将来袭",
            30f,
            TextAlignmentOptions.Center,
            new Color32(255, 210, 86, 255));
        SetRect(
            reinforcementWarning.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 24f),
            new Vector2(720f, 58f));
        reinforcementWarning.fontStyle = FontStyles.Bold;
        reinforcementWarning.raycastTarget = false;
        reinforcementWarning.gameObject.SetActive(false);

        var views = new List<BattlefieldEnemyView>();
        for (int i = 0; i < 15; i++)
        {
            Image card = CreateImage(
                "EnemyTarget_" + i,
                root.transform,
                new Color32(12, 14, 21, 112));
            SetRect(
                card.rectTransform,
                Vector2.one * 0.5f,
                Vector2.one * 0.5f,
                Vector2.one * 0.5f,
                Vector2.zero,
                new Vector2(210f, 500f));

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color32(210, 251, 255, 255);
            buttonColors.pressedColor = new Color32(255, 92, 151, 255);
            buttonColors.disabledColor = new Color32(255, 255, 255, 210);
            buttonColors.colorMultiplier = 1f;
            button.colors = buttonColors;

            Image targetOverlay = CreateTargetFeedbackFrame(
                card.transform, out Outline targetOutline);

            Image topRule = CreateImage(
                "EnemyRule", card.transform,
                new Color32(239, 35, 112, 230));
            SetRect(topRule.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(0f, 5f));
            topRule.raycastTarget = false;

            GameObject viewport = CreateUIObject("FullPortraitMask", card.transform);
            SetRect(viewport.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 34f),
                new Vector2(-18f, -136f));
            viewport.AddComponent<RectMask2D>();
            Image portrait = CreateImage("FullPortrait", viewport.transform, Color.white);
            Stretch(portrait.rectTransform, -34f);
            portrait.raycastTarget = false;
            Image formationTint = CreateImage(
                "FormationTint",
                viewport.transform,
                Color.clear);
            Stretch(formationTint.rectTransform, -34f);
            formationTint.preserveAspect = portrait.preserveAspect;
            formationTint.raycastTarget = false;
            formationTint.enabled = false;

            Image footer = CreateImage(
                "EnemyData", card.transform,
                new Color32(17, 20, 28, 238));
            SetRect(footer.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 122f));
            footer.raycastTarget = false;

            TMP_Text name = CreateText(
                "EnemyName", footer.transform, font, "ENEMY", 24f,
                TextAlignmentOptions.Center, new Color32(241, 241, 245, 255));
            SetRect(name.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -10f),
                new Vector2(-18f, 40f));

            Slider hp = CreateSlider("EnemyHP", footer.transform);
            SetRect(hp.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(-30f, 12f));
            TMP_Text hpText = CreateText(
                "EnemyHPText", footer.transform, font, "HP 0 / 0", 17f,
                TextAlignmentOptions.Center, new Color32(226, 229, 236, 255));
            SetRect(hpText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 45f),
                new Vector2(-18f, 28f));

            Image warningBackground = CreateImage(
                "EnemyWarningBadge",
                card.transform,
                new Color32(70, 167, 204, 245));
            SetRect(
                warningBackground.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(76f, 58f));
            warningBackground.raycastTarget = false;
            Outline warningOutline =
                warningBackground.gameObject.AddComponent<Outline>();
            warningOutline.effectColor = new Color32(7, 10, 18, 245);
            warningOutline.effectDistance = new Vector2(4f, -4f);

            Image warningAccent = CreateImage(
                "WarningAccent",
                warningBackground.transform,
                new Color32(255, 255, 255, 225));
            SetRect(
                warningAccent.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(-10f, 5f));
            warningAccent.raycastTarget = false;

            TMP_Text warningLabel = CreateText(
                "WarningValue",
                warningBackground.transform,
                font,
                "2",
                34f,
                TextAlignmentOptions.Center,
                new Color32(255, 255, 255, 255));
            SetRect(
                warningLabel.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.one * 0.5f,
                new Vector2(0f, 2f),
                new Vector2(-8f, -6f));
            warningLabel.fontStyle = FontStyles.Bold;
            warningLabel.raycastTarget = false;
            warningBackground.gameObject.SetActive(false);

            Image postureBreakBadge = CreateImage(
                "PostureBreakBadge",
                card.transform,
                new Color32(239, 35, 112, 248));
            SetRect(
                postureBreakBadge.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -8f),
                new Vector2(92f, 38f));
            postureBreakBadge.raycastTarget = false;
            TMP_Text postureBreakLabel = CreateText(
                "Label",
                postureBreakBadge.transform,
                font,
                "BREAK",
                18f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(postureBreakLabel.rectTransform, -4f);
            postureBreakLabel.fontStyle = FontStyles.Bold;
            postureBreakLabel.raycastTarget = false;
            postureBreakBadge.gameObject.SetActive(false);

            Image postureStabilityBadge = CreateImage(
                "PostureStabilityBadge",
                card.transform,
                new Color32(42, 158, 181, 242));
            SetRect(
                postureStabilityBadge.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -8f),
                new Vector2(112f, 38f));
            postureStabilityBadge.raycastTarget = false;
            TMP_Text postureStabilityLabel = CreateText(
                "Label",
                postureStabilityBadge.transform,
                font,
                "STABLE 3",
                16f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(postureStabilityLabel.rectTransform, -4f);
            postureStabilityLabel.fontStyle = FontStyles.Bold;
            postureStabilityLabel.raycastTarget = false;
            postureStabilityBadge.gameObject.SetActive(false);

            BattlefieldEnemyView view =
                card.gameObject.AddComponent<BattlefieldEnemyView>();
            SetReference(view, "portrait", portrait);
            SetReference(view, "displayName", name);
            SetReference(view, "hpText", hpText);
            SetReference(view, "hpBar", hp);
            SetReference(view, "targetButton", button);
            SetReference(view, "targetOutline", targetOutline);
            SetReference(view, "targetOverlay", targetOverlay);
            SetReference(view, "scaleTarget", card.rectTransform);
            SetReference(view, "formationTint", formationTint);
            SetReference(view, "formationRule", topRule);
            SetReference(view, "warningRoot", warningBackground.gameObject);
            SetReference(view, "warningBackground", warningBackground);
            SetReference(view, "warningLabel", warningLabel);
            SetReference(
                view,
                "warningPulseTarget",
                warningBackground.rectTransform);
            SetReference(
                view,
                "postureBreakIndicator",
                postureBreakBadge.gameObject);
            SetReference(
                view,
                "postureStabilityIndicator",
                postureStabilityBadge.gameObject);
            SetReference(
                view,
                "postureStabilityLabel",
                postureStabilityLabel);
            views.Add(view);
        }

        BattlefieldEnemyRosterView roster =
            root.AddComponent<BattlefieldEnemyRosterView>();
        SetObjectList(roster, "enemyViews", views);
        SetReference(roster, "layoutRoot", root.GetComponent<RectTransform>());
        SetReference(roster, "reinforcementWarning", reinforcementWarning);
        return roster;
    }

    private static TMP_Text CreateTargetPrompt(Transform parent, TMP_FontAsset font)
    {
        TMP_Text prompt = CreateText(
            "TargetPrompt", parent, font, string.Empty, 30f,
            TextAlignmentOptions.Center, new Color32(247, 241, 246, 255));
        SetRect(prompt.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(92f, -184f),
            new Vector2(760f, 52f));
        prompt.fontStyle = FontStyles.Bold;
        Outline outline = prompt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(84, 17, 36, 255);
        outline.effectDistance = new Vector2(3f, -3f);
        prompt.gameObject.SetActive(false);
        return prompt;
    }

    private static BattleTurnQuotePresenter CreateTurnQuote(
        Transform parent,
        TMP_FontAsset font)
    {
        Image panel = CreateImage(
            "TurnQuote",
            parent,
            new Color32(17, 20, 28, 225));
        SetRect(panel.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -170f),
            new Vector2(980f, 72f));
        panel.raycastTarget = false;
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        TMP_Text text = CreateText(
            "QuoteText", panel.transform, font, string.Empty, 28f,
            TextAlignmentOptions.Center, new Color32(247, 241, 246, 255));
        Stretch(text.rectTransform, 10f);
        text.gameObject.SetActive(false);

        BattleTurnQuotePresenter presenter =
            panel.gameObject.AddComponent<BattleTurnQuotePresenter>();
        SetReference(presenter, "quoteText", text);
        SetReference(presenter, "canvasGroup", group);
        return presenter;
    }

    private static BattlePartyHud CreatePartyHud(Transform parent, TMP_FontAsset font)
    {
        GameObject root = CreateUIObject("PartyHud", parent);
        SetRect(root.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(36f, 28f),
            new Vector2(1510f, 230f));
        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var views = new List<BattlePartyMemberView>();
        for (int i = 0; i < 5; i++)
        {
            Image card = CreateImage(
                "PartyMember_" + i,
                root.transform,
                new Color32(17, 20, 28, 238));
            LayoutElement element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 286f;
            element.preferredHeight = 214f;

            Image formationUnderlay = CreateImage(
                "FormationUnderlay",
                card.transform,
                Color.clear);
            Stretch(formationUnderlay.rectTransform, -7f);
            formationUnderlay.raycastTarget = false;
            formationUnderlay.enabled = false;
            formationUnderlay.transform.SetAsFirstSibling();

            GameObject viewport = CreateUIObject("AvatarMask", card.transform);
            SetRect(viewport.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(10f, 0f),
                new Vector2(126f, -20f));
            viewport.AddComponent<RectMask2D>();
            Image portrait = CreateImage("Portrait", viewport.transform, Color.white);
            Stretch(portrait.rectTransform, -20f);
            portrait.raycastTarget = false;

            TMP_Text name = CreateText(
                "Name", card.transform, font, "MEMBER", 22f,
                TextAlignmentOptions.Left, new Color32(226, 229, 236, 255));
            SetRect(name.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(146f, -22f),
                new Vector2(-160f, 38f));

            Slider hp = CreateSlider("HP", card.transform);
            SetRect(hp.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(146f, 48f),
                new Vector2(-160f, 18f));
            TMP_Text hpText = CreateText(
                "HPText", card.transform, font, "0 / 0", 18f,
                TextAlignmentOptions.Left, new Color32(226, 229, 236, 255));
            SetRect(hpText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(146f, 70f),
                new Vector2(-160f, 30f));

            Image marker = CreateImage(
                "CurrentMarker", card.transform,
                new Color32(239, 35, 112, 255));
            SetRect(marker.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 6f));

            Image targetOverlay = CreateTargetFeedbackFrame(
                card.transform, out Outline targetOutline);

            Image postureBreakBadge = CreateImage(
                "PostureBreakBadge",
                card.transform,
                new Color32(239, 35, 112, 248));
            SetRect(
                postureBreakBadge.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-10f, -10f),
                new Vector2(94f, 34f));
            postureBreakBadge.raycastTarget = false;
            TMP_Text postureBreakLabel = CreateText(
                "Label",
                postureBreakBadge.transform,
                font,
                "BREAK",
                17f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(postureBreakLabel.rectTransform, -4f);
            postureBreakLabel.fontStyle = FontStyles.Bold;
            postureBreakLabel.raycastTarget = false;
            postureBreakBadge.gameObject.SetActive(false);

            Image postureStabilityBadge = CreateImage(
                "PostureStabilityBadge",
                card.transform,
                new Color32(42, 158, 181, 242));
            SetRect(
                postureStabilityBadge.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-10f, -10f),
                new Vector2(112f, 34f));
            postureStabilityBadge.raycastTarget = false;
            TMP_Text postureStabilityLabel = CreateText(
                "Label",
                postureStabilityBadge.transform,
                font,
                "STABLE 3",
                15f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(postureStabilityLabel.rectTransform, -4f);
            postureStabilityLabel.fontStyle = FontStyles.Bold;
            postureStabilityLabel.raycastTarget = false;
            postureStabilityBadge.gameObject.SetActive(false);

            BattlePartyMemberView view =
                card.gameObject.AddComponent<BattlePartyMemberView>();
            SetReference(view, "portrait", portrait);
            SetReference(view, "displayName", name);
            SetReference(view, "hpText", hpText);
            SetReference(view, "hpBar", hp);
            SetReference(view, "currentActorMarker", marker.gameObject);
            SetReference(view, "targetOutline", targetOutline);
            SetReference(view, "targetOverlay", targetOverlay);
            SetReference(view, "formationUnderlay", formationUnderlay);
            SetReference(
                view,
                "postureBreakIndicator",
                postureBreakBadge.gameObject);
            SetReference(
                view,
                "postureStabilityIndicator",
                postureStabilityBadge.gameObject);
            SetReference(
                view,
                "postureStabilityLabel",
                postureStabilityLabel);
            views.Add(view);
        }

        BattlePartyHud hud = root.AddComponent<BattlePartyHud>();
        SetObjectList(hud, "memberViews", views);
        return hud;
    }

    private static BattleAttackCutInPresenter CreateAttackCutIn(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject overlay = CreateUIObject("AttackCutInOverlay", parent);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        Stretch(overlayRect);
        overlay.transform.SetAsLastSibling();
        CanvasGroup overlayGroup = overlay.AddComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        Image dimmer = CreateImage(
            "CinematicDimmer",
            overlay.transform,
            new Color(0.008f, 0.006f, 0.012f, 0.42f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = false;

        Image wedge = CreateImage(
            "TrapezoidWedge",
            overlay.transform,
            new Color32(91, 11, 44, 248));
        SetRect(
            wedge.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-760f, 22f),
            new Vector2(220f, 1080f));
        wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        wedge.raycastTarget = false;

        Image panel = CreateImage(
            "TrapezoidPanel",
            overlay.transform,
            new Color32(15, 13, 24, 218));
        SetRect(
            panel.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(54f, 24f),
            new Vector2(820f, 900f));
        panel.raycastTarget = false;

        Image upperAccent = CreateImage(
            "UpperAccent",
            panel.transform,
            new Color32(255, 38, 118, 255));
        SetRect(
            upperAccent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -12f),
            new Vector2(0f, 18f));
        upperAccent.raycastTarget = false;

        Image lowerAccent = CreateImage(
            "LowerAccent",
            panel.transform,
            new Color32(116, 14, 56, 255));
        SetRect(
            lowerAccent.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 12f),
            new Vector2(0f, 30f));
        lowerAccent.raycastTarget = false;

        Image portrait = CreateImage(
            "AttackPortrait",
            overlay.transform,
            Color.white);
        SetRect(
            portrait.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-34f, -20f),
            new Vector2(980f, 1240f));
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        Image namePlate = CreateImage(
            "AttackActorNamePlate",
            overlay.transform,
            new Color32(112, 12, 51, 238));
        SetRect(
            namePlate.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-76f, 88f),
            new Vector2(480f, 88f));
        namePlate.raycastTarget = false;
        TMP_Text actorName = CreateText(
            "AttackActorName",
            namePlate.transform,
            font,
            "ATTACK",
            42f,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(actorName.rectTransform, 12f);

        Image flash = CreateImage(
            "ImpactFlash",
            overlay.transform,
            new Color(1f, 0.18f, 0.42f, 0.34f));
        Stretch(flash.rectTransform);
        flash.raycastTarget = false;

        GameObject slashRoot = CreateUIObject("SlashStreaks", overlay.transform);
        Stretch(slashRoot.GetComponent<RectTransform>());
        CanvasGroup slashGroup = slashRoot.AddComponent<CanvasGroup>();
        slashGroup.interactable = false;
        slashGroup.blocksRaycasts = false;
        var streaks = new RectTransform[3];
        Color32[] streakColors =
        {
            new Color32(255, 250, 252, 255),
            new Color32(255, 38, 118, 255),
            new Color32(143, 22, 74, 255)
        };
        for (int i = 0; i < streaks.Length; i++)
        {
            Image streak = CreateImage(
                "Slash_" + i,
                slashRoot.transform,
                streakColors[i]);
            SetRect(
                streak.rectTransform,
                Vector2.one * 0.5f,
                Vector2.one * 0.5f,
                Vector2.one * 0.5f,
                new Vector2(170f + i * 78f, 150f - i * 120f),
                new Vector2(1380f - i * 150f, 22f - i * 4f));
            streak.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 22f + i * 2f);
            streak.raycastTarget = false;
            streaks[i] = streak.rectTransform;
        }

        BattleAttackCutInPresenter presenter =
            parent.gameObject.AddComponent<BattleAttackCutInPresenter>();
        SetReference(presenter, "visibleRoot", overlay);
        SetReference(presenter, "overlayGroup", overlayGroup);
        SetReference(presenter, "dimmer", dimmer);
        SetReference(presenter, "panel", panel.rectTransform);
        SetReference(presenter, "panelWedge", wedge.rectTransform);
        SetReference(presenter, "portrait", portrait);
        SetReference(presenter, "portraitTransform", portrait.rectTransform);
        SetReference(presenter, "actorName", actorName);
        SetReference(presenter, "impactFlash", flash);
        SetReference(presenter, "slashGroup", slashGroup);
        SetObjectList(presenter, "slashStreaks", streaks);
        overlay.SetActive(false);
        return presenter;
    }

    private static BattleImpactFeedbackPresenter CreateImpactFeedback(
        Transform parent,
        TMP_FontAsset font,
        BattlePartyHud partyHud,
        BattlefieldEnemyRosterView enemyRoster)
    {
        GameObject layer = CreateUIObject("ImpactFeedbackLayer", parent);
        RectTransform layerRect = layer.GetComponent<RectTransform>();
        Stretch(layerRect);
        layer.transform.SetAsLastSibling();

        TMP_Text label = CreateText(
            "DamageNumberTemplate", layer.transform, font, "-0", 58f,
            TextAlignmentOptions.Center, new Color32(255, 238, 242, 255));
        SetRect(label.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.one * 0.5f, Vector2.zero,
            new Vector2(240f, 86f));
        label.fontStyle = FontStyles.Bold;
        Outline outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(84, 17, 36, 255);
        outline.effectDistance = new Vector2(3f, -3f);
        CanvasGroup canvasGroup = label.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        BattleDamageNumberView numberView =
            label.gameObject.AddComponent<BattleDamageNumberView>();
        SetReference(numberView, "rectTransform", label.rectTransform);
        SetReference(numberView, "valueText", label);
        SetReference(numberView, "canvasGroup", canvasGroup);
        label.gameObject.SetActive(false);

        BattleImpactFeedbackPresenter presenter =
            layer.AddComponent<BattleImpactFeedbackPresenter>();
        SetReference(presenter, "partyHud", partyHud);
        SetReference(presenter, "enemyRoster", enemyRoster);
        SetReference(presenter, "feedbackLayer", layerRect);
        SetReference(presenter, "damageNumberTemplate", numberView);
        return presenter;
    }

    private static BattleDamageBrokenCutInPresenter CreateDamageBrokenCutIn(
        Transform parent,
        TMP_FontAsset font)
    {
        Image overlay = CreateImage(
            "DamageBrokenCutInOverlay",
            parent,
            new Color(0.015f, 0.008f, 0.018f, 0.72f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        overlay.transform.SetAsLastSibling();
        CanvasGroup blocker = overlay.gameObject.AddComponent<CanvasGroup>();
        blocker.interactable = true;
        blocker.blocksRaycasts = true;

        var cutInViews = new List<BattleDamageBrokenCutInView>();
        for (int i = 0; i < 5; i++)
        {
            Image panel = CreateImage(
                "DamageBrokenCutIn_" + i,
                overlay.transform,
                new Color32(52, 12, 30, 252));
            SetRect(panel.rectTransform,
                Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero,
                new Vector2(440f, 900f));
            Outline panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color32(255, 48, 120, 255);
            panelOutline.effectDistance = new Vector2(5f, -5f);
            CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            GameObject portraitMask = CreateUIObject("PortraitMask", panel.transform);
            Stretch(portraitMask.GetComponent<RectTransform>(), 10f);
            portraitMask.AddComponent<RectMask2D>();
            Image portrait = CreateImage(
                "DamageBrokenPortrait", portraitMask.transform, Color.white);
            Stretch(portrait.rectTransform, -28f);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            Image namePlate = CreateImage(
                "NamePlate", panel.transform,
                new Color32(112, 12, 51, 245));
            SetRect(namePlate.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 48f),
                new Vector2(-28f, 92f));
            TMP_Text name = CreateText(
                "Name", namePlate.transform, font, "BROKEN", 34f,
                TextAlignmentOptions.Center, Color.white);
            Stretch(name.rectTransform, 10f);

            BattleDamageBrokenCutInView view =
                panel.gameObject.AddComponent<BattleDamageBrokenCutInView>();
            SetReference(view, "panel", panel.rectTransform);
            SetReference(view, "portrait", portrait);
            SetReference(view, "displayName", name);
            SetReference(view, "canvasGroup", panelGroup);
            panel.gameObject.SetActive(false);
            cutInViews.Add(view);
        }

        BattleDamageBrokenCutInPresenter presenter =
            parent.gameObject.AddComponent<BattleDamageBrokenCutInPresenter>();
        SetReference(presenter, "visibleRoot", overlay.gameObject);
        SetReference(presenter, "safeArea", overlay.rectTransform);
        SetReference(presenter, "inputBlocker", blocker);
        SetObjectList(presenter, "views", cutInViews);
        overlay.gameObject.SetActive(false);
        return presenter;
    }

    private static BattleActorPresenter CreateActorPresenter(
        Transform parent,
        TMP_FontAsset font)
    {
        Image panel = CreateImage(
            "CurrentActorPanel",
            parent,
            new Color32(12, 14, 21, 218));
        SetRect(panel.rectTransform,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(-30f, 0f),
            new Vector2(720f, -150f));

        GameObject viewport = CreateUIObject("FullPortraitMask", panel.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 18f);
        viewport.AddComponent<RectMask2D>();
        Image portrait = CreateImage("FullPortrait", viewport.transform, Color.white);
        Stretch(portrait.rectTransform, -80f);
        portrait.raycastTarget = false;
        SpriteBattleActorAnimationDriver driver =
            portrait.gameObject.AddComponent<SpriteBattleActorAnimationDriver>();
        SetReference(driver, "portraitImage", portrait);

        Image namePlate = CreateImage(
            "NamePlate", panel.transform,
            new Color32(84, 17, 36, 245));
        SetRect(namePlate.rectTransform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 28f),
            new Vector2(-60f, 96f));
        TMP_Text name = CreateText(
            "ActorName", namePlate.transform, font, "CURRENT ACTOR", 42f,
            TextAlignmentOptions.Center, Color.white);
        Stretch(name.rectTransform, 14f);

        TMP_Text hp = CreateText(
            "ActorHP", panel.transform, font, "HP", 24f,
            TextAlignmentOptions.Right, new Color32(226, 229, 236, 255));
        SetRect(hp.rectTransform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 130f),
            new Vector2(-60f, 44f));

        Image accent = CreateImage(
            "Accent", panel.transform,
            new Color32(239, 35, 112, 255));
        SetRect(accent.rectTransform,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(8f, 0f));

        Image targetOverlay = CreateTargetFeedbackFrame(
            panel.transform, out Outline targetOutline);

        BattleActorPresenter presenter =
            panel.gameObject.AddComponent<BattleActorPresenter>();
        SetReference(presenter, "portrait", portrait);
        SetReference(presenter, "actorName", name);
        SetReference(presenter, "hpText", hp);
        SetReference(presenter, "accent", accent);
        SetReference(presenter, "animationDriverComponent", driver);
        SetReference(presenter, "targetOutline", targetOutline);
        SetReference(presenter, "targetOverlay", targetOverlay);
        return presenter;
    }

    private static bool EnsureSceneConnection(
        GameObject presentationPrefab,
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGEncounterDefinitionSO defeatTestEncounter,
        out string scenePath)
    {
        Scene scene = SceneManager.GetActiveScene();
        scenePath = scene.path;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameFlowRoot gameFlowRoot =
            UnityEngine.Object.FindObjectOfType<GameFlowRoot>(true);
        if (gameFlowRoot == null)
        {
            var rootObject = new GameObject("GameFlowRoot");
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            gameFlowRoot = rootObject.AddComponent<GameFlowRoot>();
        }

        DRPGBattleFlowRoot battleFlow =
            gameFlowRoot.GetComponent<DRPGBattleFlowRoot>();
        if (battleFlow == null)
            battleFlow = gameFlowRoot.gameObject.AddComponent<DRPGBattleFlowRoot>();

        Transform existingPresentation =
            gameFlowRoot.transform.Find("DRPGBattlePresentation");
        DRPGBattlePresentationRoot presentation;
        if (existingPresentation == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                presentationPrefab,
                scene);
            instance.name = "DRPGBattlePresentation";
            instance.transform.SetParent(gameFlowRoot.transform, false);
            presentation = instance.GetComponent<DRPGBattlePresentationRoot>();
        }
        else
        {
            presentation =
                existingPresentation.GetComponent<DRPGBattlePresentationRoot>();
        }

        if (presentation == null)
            throw new InvalidOperationException("Scene battle presentation is missing.");

        var flowSerialized = new SerializedObject(battleFlow);
        flowSerialized.FindProperty("presentationComponent").objectReferenceValue =
            presentation;
        flowSerialized.ApplyModifiedPropertiesWithoutUndo();

        DRPGBattleDebugLauncher launcher =
            gameFlowRoot.GetComponent<DRPGBattleDebugLauncher>();
        if (launcher == null)
            launcher = gameFlowRoot.gameObject.AddComponent<DRPGBattleDebugLauncher>();
        var launcherSerialized = new SerializedObject(launcher);
        launcherSerialized.FindProperty("battleFlow").objectReferenceValue = battleFlow;
        launcherSerialized.FindProperty("party").objectReferenceValue = party;
        launcherSerialized.FindProperty("encounter").objectReferenceValue = encounter;
        launcherSerialized.FindProperty("defeatTestParty").objectReferenceValue = party;
        launcherSerialized.FindProperty("defeatTestEncounter").objectReferenceValue =
            defeatTestEncounter;
        launcherSerialized.ApplyModifiedPropertiesWithoutUndo();

        presentation.gameObject.SetActive(true);
        Transform visualRoot = presentation.transform.Find("VisualRoot");
        if (visualRoot != null)
            visualRoot.gameObject.SetActive(false);
        EditorUtility.SetDirty(gameFlowRoot);
        EditorUtility.SetDirty(battleFlow);
        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static Slider CreateSlider(string name, Transform parent)
    {
        GameObject root = CreateUIObject(name, parent);
        Slider slider = root.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;

        Image background = CreateImage(
            "Background", root.transform, new Color32(42, 45, 57, 255));
        Stretch(background.rectTransform);
        GameObject fillArea = CreateUIObject("Fill Area", root.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 2f);
        Image fill = CreateImage(
            "Fill", fillArea.transform, new Color32(239, 35, 112, 255));
        Stretch(fill.rectTransform);
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = background;
        return slider;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image CreateTargetFeedbackFrame(
        Transform parent,
        out Outline outline)
    {
        Image frame = CreateImage("TargetFeedback", parent, Color.clear);
        Stretch(frame.rectTransform, 3f);
        frame.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");
        frame.type = Image.Type.Sliced;
        frame.fillCenter = false;
        frame.enabled = false;
        frame.raycastTarget = false;

        outline = frame.gameObject.AddComponent<Outline>();
        outline.enabled = false;
        outline.useGraphicAlpha = false;
        return frame;
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

    private readonly struct RecipeBeatSeed
    {
        public RecipeBeatSeed(
            string beatId,
            float delayBeforeSeconds,
            float minimumDurationSeconds,
            bool releasesGate,
            params RecipeCueSeed[] cues)
        {
            BeatId = beatId ?? string.Empty;
            DelayBeforeSeconds = Mathf.Max(0f, delayBeforeSeconds);
            MinimumDurationSeconds = Mathf.Max(0f, minimumDurationSeconds);
            ReleasesGate = releasesGate;
            Cues = cues ?? Array.Empty<RecipeCueSeed>();
        }

        public string BeatId { get; }
        public float DelayBeforeSeconds { get; }
        public float MinimumDurationSeconds { get; }
        public bool ReleasesGate { get; }
        public RecipeCueSeed[] Cues { get; }
    }

    private readonly struct RecipeCueSeed
    {
        public RecipeCueSeed(
            BattlePresentationCueSO cue,
            BattlePresentationCondition condition,
            BattlePresentationLaneKind lane,
            bool blocksBeat)
        {
            Cue = cue;
            Condition = condition;
            Lane = lane;
            BlocksBeat = blocksBeat;
        }

        public BattlePresentationCueSO Cue { get; }
        public BattlePresentationCondition Condition { get; }
        public BattlePresentationLaneKind Lane { get; }
        public bool BlocksBeat { get; }
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + "." + propertyName + " was not found.");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectList<T>(
        UnityEngine.Object target,
        string propertyName,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(propertyName + " was not found.");
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T LoadRequired<T>(string path)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }

    private static T LoadOptional<T>(string path)
        where T : UnityEngine.Object
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void ConfigureSpriteImporter(
        string path,
        bool hasAlpha,
        Vector2 pivot)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            throw new InvalidOperationException("Texture importer not found: " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaSource = hasAlpha
            ? TextureImporterAlphaSource.FromInput
            : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = hasAlpha;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 100f;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
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
