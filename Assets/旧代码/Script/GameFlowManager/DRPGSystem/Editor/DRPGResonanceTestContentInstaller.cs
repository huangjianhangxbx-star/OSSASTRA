#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotent vertical-slice installer for the protagonist resonance layer.
/// Serialized assets and prefab graphs are edited only through Unity APIs.
/// </summary>
public static class DRPGResonanceTestContentInstaller
{
    private const string Root =
        "Assets/GameResources/DRPG/Progression/Test/Resonance";
    private const string ConfigRoot =
        "Assets/GameResources/DRPG/Config/Test";
    private const string BattlePrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string MenuPrefabPath =
        "Assets/Resources/DRPG/UI/PF_GameMenu.prefab";
    private const string FontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";
    private const string ProtagonistPath =
        ConfigRoot + "/DRPGCombatant_ankalute_protagonist.asset";
    private const string VanguardPath =
        ConfigRoot + "/DRPGCombatant_ankalute_party_vanguard.asset";
    private const string AlchemistPath =
        ConfigRoot + "/DRPGCombatant_ankalute_party_alchemist.asset";
    public const string PartyPath =
        Root + "/DRPGParty_ResonanceTest.asset";
    public const string DefinitionPath =
        Root + "/DRPGResonanceDefinition_Test.asset";

    private static readonly Color Ink =
        new Color(0.018f, 0.018f, 0.030f, 0.98f);
    private static readonly Color Panel =
        new Color(0.035f, 0.025f, 0.045f, 0.97f);
    private static readonly Color Magenta =
        new Color(1f, 0.035f, 0.36f, 1f);
    private static readonly Color Cyan =
        new Color(0.12f, 0.78f, 0.86f, 1f);
    private static readonly Color WarmWhite =
        new Color(0.96f, 0.94f, 0.93f, 1f);

    [MenuItem("Tools/ANKALUTE/DRPG/Install Resonance Vertical Slice")]
    public static void InstallFromMenu()
    {
        Debug.Log(Install());
    }

    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "[ResonanceInstaller] REJECTED: exit Play Mode first.";

        EnsureFolder(Root);
        DRPGCombatantProfileSO protagonist = Require<DRPGCombatantProfileSO>(
            ProtagonistPath);
        DRPGCombatantProfileSO vanguard = Require<DRPGCombatantProfileSO>(
            VanguardPath);
        DRPGCombatantProfileSO alchemist = Require<DRPGCombatantProfileSO>(
            AlchemistPath);
        DRPGPartySetupSO party = CreatePartySetup(
            protagonist,
            vanguard,
            alchemist);

        DRPGSkillSO vanguardSkill = CreateSkill(
            Root + "/DRPGSkill_ResonanceVanguard.asset",
            "resonance_vanguard_cross",
            "交叉调律·断界",
            "守与同伴完成条件后发动的同律攻击。该技能独立于职业四槽。",
            42f);
        DRPGSkillSO alchemistSkill = CreateSkill(
            Root + "/DRPGSkill_ResonanceAlchemist.asset",
            "resonance_alchemist_flux",
            "交叉调律·回路过载",
            "以异质回路共振制造一次强力术式冲击。",
            36f);
        DRPGPassiveModifierSO vanguardPassive = CreatePassive(
            Root + "/DRPGPassive_ResonanceVanguard.asset",
            "resonance_vanguard_passive",
            "并肩校准",
            "被动节点解锁后自动生效，不占职业技能槽。",
            1.03f);
        DRPGPassiveModifierSO alchemistPassive = CreatePassive(
            Root + "/DRPGPassive_ResonanceAlchemist.asset",
            "resonance_alchemist_passive",
            "回路互证",
            "同律被动贡献会并入主角身份层构筑。",
            1.02f);

        DRPGResonanceBranchSO vanguardBranch = CreateBranch(
            Root + "/DRPGResonanceBranch_Vanguard.asset",
            "resonance_branch_vanguard",
            vanguard,
            vanguardSkill,
            vanguardPassive,
            "断界协奏");
        DRPGResonanceBranchSO alchemistBranch = CreateBranch(
            Root + "/DRPGResonanceBranch_Alchemist.asset",
            "resonance_branch_alchemist",
            alchemist,
            alchemistSkill,
            alchemistPassive,
            "回路协奏");
        DRPGResonanceDefinitionSO definition = CreateDefinition(
            protagonist,
            vanguardBranch,
            alchemistBranch);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            FontPath);
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        UpgradeBattlePrefab(font);
        UpgradeGameMenuPrefab(font);
        bool sceneConfigured = ConfigureLoadedScene(
            protagonist,
            vanguard,
            alchemist,
            party,
            definition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "[ResonanceInstaller] PASS; branches=2; active=2; passive=2; " +
            "battleUI=true; menuUI=true; loadedScene=" + sceneConfigured;
    }

    private static DRPGSkillSO CreateSkill(
        string path,
        string id,
        string displayName,
        string description,
        float power)
    {
        DRPGSkillSO asset = LoadOrCreate<DRPGSkillSO>(path);
        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("skillId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("power").floatValue = power;
        so.FindProperty("prepareDelay").floatValue = 0f;
        so.FindProperty("recoveryDelay").floatValue = 100f;
        so.FindProperty("presentationKey").stringValue = id;
        so.FindProperty("sourceFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        so.FindProperty("targetFormationRows").intValue =
            (int)DRPGFormationRowMask.All;
        so.FindProperty("purpose").enumValueIndex =
            (int)DRPGSkillPurpose.Damage;
        so.FindProperty("targetRule").enumValueIndex =
            (int)DRPGSkillTargetRule.SingleEnemy;
        so.FindProperty("executionMode").enumValueIndex =
            (int)DRPGSkillExecutionMode.Standard;
        so.FindProperty("manaCost").floatValue = 0f;
        so.FindProperty("postureCost").floatValue = 12f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static DRPGPassiveModifierSO CreatePassive(
        string path,
        string id,
        string displayName,
        string description,
        float damageMultiplier)
    {
        DRPGPassiveModifierSO asset = LoadOrCreate<DRPGPassiveModifierSO>(path);
        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("modifierId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("damageMultiplier").floatValue = damageMultiplier;
        so.FindProperty("maxHpMultiplier").floatValue = 1f;
        so.FindProperty("agilityMultiplier").floatValue = 1f;
        so.FindProperty("recoveryDelayMultiplier").floatValue = 1f;
        so.FindProperty("maxPostureMultiplier").floatValue = 1f;
        so.FindProperty("postureRecoveryMultiplier").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static DRPGResonanceBranchSO CreateBranch(
        string path,
        string branchId,
        DRPGCombatantProfileSO friend,
        DRPGSkillSO active,
        DRPGPassiveModifierSO passive,
        string activeName)
    {
        DRPGResonanceBranchSO branch =
            LoadOrCreate<DRPGResonanceBranchSO>(path);
        SerializedObject so = new SerializedObject(branch);
        so.FindProperty("branchId").stringValue = branchId;
        so.FindProperty("friendProfile").objectReferenceValue = friend;
        SerializedProperty nodes = so.FindProperty("nodes");
        nodes.arraySize = 2;
        ConfigureNode(
            nodes.GetArrayElementAtIndex(0),
            branchId + "_active",
            activeName,
            "同伴行动两次后完成充能；使用后只清除此技能的进度。",
            DRPGResonanceNodeRewardKind.ActiveSkill,
            active,
            null,
            2,
            true);
        ConfigureNode(
            nodes.GetArrayElementAtIndex(1),
            branchId + "_passive",
            "法则共鸣",
            "解锁后自动贡献到主角身份层，不占主动技能槽。",
            DRPGResonanceNodeRewardKind.Passive,
            null,
            passive,
            1,
            false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(branch);
        return branch;
    }

    private static void ConfigureNode(
        SerializedProperty node,
        string id,
        string displayName,
        string description,
        DRPGResonanceNodeRewardKind reward,
        DRPGSkillSO active,
        DRPGPassiveModifierSO passive,
        int requiredProgress,
        bool addProgressSource)
    {
        node.FindPropertyRelative("nodeId").stringValue = id;
        node.FindPropertyRelative("displayName").stringValue = displayName;
        node.FindPropertyRelative("description").stringValue = description;
        node.FindPropertyRelative("requiredConditionValue").intValue = 0;
        node.FindPropertyRelative("prerequisiteNodeIds").arraySize = 0;
        node.FindPropertyRelative("rewardKind").enumValueIndex = (int)reward;
        node.FindPropertyRelative("activeSkill").objectReferenceValue = active;
        node.FindPropertyRelative("passiveModifier").objectReferenceValue =
            passive;
        node.FindPropertyRelative("requiredBattleProgress").intValue =
            Mathf.Max(1, requiredProgress);
        SerializedProperty sources = node.FindPropertyRelative(
            "progressSources");
        sources.arraySize = addProgressSource ? 1 : 0;
        if (addProgressSource)
        {
            SerializedProperty source = sources.GetArrayElementAtIndex(0);
            source.FindPropertyRelative("eventKind").enumValueIndex =
                (int)DRPGBattleEventKind.ActionImpactResolved;
            source.FindPropertyRelative("actorRule").enumValueIndex =
                (int)DRPGResonanceProgressActorRule.Partner;
            source.FindPropertyRelative("progressAmount").intValue = 1;
        }
    }

    private static DRPGResonanceDefinitionSO CreateDefinition(
        DRPGCombatantProfileSO protagonist,
        params DRPGResonanceBranchSO[] branches)
    {
        DRPGResonanceDefinitionSO definition =
            LoadOrCreate<DRPGResonanceDefinitionSO>(DefinitionPath);
        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("identityId").stringValue =
            "ankalute_protagonist_resonance";
        so.FindProperty("protagonistProfile").objectReferenceValue =
            protagonist;
        SerializedProperty list = so.FindProperty("branches");
        list.arraySize = branches.Length;
        for (int i = 0; i < branches.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = branches[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static DRPGPartySetupSO CreatePartySetup(
        DRPGCombatantProfileSO protagonist,
        params DRPGCombatantProfileSO[] friends)
    {
        DRPGPartySetupSO party = LoadOrCreate<DRPGPartySetupSO>(PartyPath);
        SerializedObject so = new SerializedObject(party);
        so.FindProperty("protagonist").objectReferenceValue = protagonist;
        SerializedProperty members = so.FindProperty("partyMembers");
        members.arraySize = friends != null ? friends.Length : 0;
        for (int i = 0; i < members.arraySize; i++)
        {
            members.GetArrayElementAtIndex(i).objectReferenceValue =
                friends[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(party);
        return party;
    }

    private static void UpgradeBattlePrefab(TMP_FontAsset font)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BattlePrefabPath);
        try
        {
            BattleCommandMenu commandMenu =
                root.GetComponentInChildren<BattleCommandMenu>(true);
            if (commandMenu == null)
                throw new InvalidOperationException("BattleCommandMenu missing.");
            Transform commands = commandMenu.transform;
            Transform commandRoot = commands.Find("CommandRoot");
            Transform old = commands.Find("ResonanceRoot");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform oldButton = commandRoot.Find("Resonance");
            if (oldButton != null)
                UnityEngine.Object.DestroyImmediate(oldButton.gameObject);

            Button sourceButton = commandRoot.Find("Skill").GetComponent<Button>();
            GameObject buttonObject = UnityEngine.Object.Instantiate(
                sourceButton.gameObject,
                commandRoot);
            buttonObject.name = "Resonance";
            RectTransform buttonRect = buttonObject.transform as RectTransform;
            buttonRect.anchoredPosition = new Vector2(0f, -324f);
            SetButtonLabel(buttonObject, "同律");
            Button resonanceButton = buttonObject.GetComponent<Button>();
            SetHoverPalette(resonanceButton);
            MoveY(commandRoot.Find("Defend") as RectTransform, -384f);
            MoveY(commandRoot.Find("Escape") as RectTransform, -444f);

            GameObject resonanceRoot = CreatePanel(
                "ResonanceRoot",
                commands,
                Panel);
            CopyRect(
                commands.Find("ItemRoot") as RectTransform,
                resonanceRoot.transform as RectTransform);
            BattleResonanceMenu resonanceMenu =
                resonanceRoot.AddComponent<BattleResonanceMenu>();
            GameObject rootPanel = CreateUIObject(
                "RootPanel",
                resonanceRoot.transform);
            Stretch(rootPanel.GetComponent<RectTransform>());
            GameObject skillPanel = CreateUIObject(
                "SkillListPanel",
                resonanceRoot.transform);
            Stretch(skillPanel.GetComponent<RectTransform>());

            CreateText("Title", rootPanel.transform, font,
                "RESONANCE / 同律", 29f,
                new Vector2(18f, -20f), new Vector2(450f, 48f),
                TextAlignmentOptions.Left, WarmWhite);
            TMP_Text partnerLabel = CreateText(
                "PartnerLabel", rootPanel.transform, font,
                "同律对象：未指定", 22f,
                new Vector2(18f, -78f), new Vector2(450f, 42f),
                TextAlignmentOptions.Left, Cyan);
            Image readyAccent = CreateImage(
                "ReadyAccent", rootPanel.transform, Magenta);
            SetRect(readyAccent.rectTransform,
                new Vector2(16f, -122f), new Vector2(6f, 248f));

            var partnerButtons = new List<Button>();
            var partnerLabels = new List<TMP_Text>();
            for (int i = 0; i < 4; i++)
            {
                Button button = CreateButton(
                    "Partner_" + i,
                    rootPanel.transform,
                    font,
                    "PARTNER " + (i + 1),
                    new Vector2(34f, -132f - i * 56f),
                    new Vector2(430f, 48f));
                partnerButtons.Add(button);
                partnerLabels.Add(button.GetComponentInChildren<TMP_Text>());
            }
            Button open = CreateButton(
                "OpenSkillList",
                rootPanel.transform,
                font,
                "同律技能",
                new Vector2(34f, -376f),
                new Vector2(430f, 58f));
            TMP_Text openLabel = open.GetComponentInChildren<TMP_Text>();
            Button commandBack = CreateButton(
                "CommandBackButton",
                rootPanel.transform,
                font,
                "返回战斗指令",
                new Vector2(34f, -542f),
                new Vector2(430f, 48f));

            CreateText("Title", skillPanel.transform, font,
                "RESONANCE SKILL / 同律技能", 29f,
                new Vector2(18f, -20f), new Vector2(450f, 48f),
                TextAlignmentOptions.Left, WarmWhite);
            Button active = CreateButton(
                "ActiveSkill",
                skillPanel.transform,
                font,
                "ACTIVE SKILL",
                new Vector2(34f, -110f),
                new Vector2(430f, 88f));
            TMP_Text activeLabel = active.GetComponentInChildren<TMP_Text>();
            Button skillBack = CreateButton(
                "SkillBackButton",
                skillPanel.transform,
                font,
                "返回同律对象",
                new Vector2(34f, -542f),
                new Vector2(430f, 48f));
            TMP_Text feedback = CreateText(
                "Feedback", resonanceRoot.transform, font,
                string.Empty, 18f,
                new Vector2(28f, -470f), new Vector2(440f, 58f),
                TextAlignmentOptions.Left, new Color(1f, 0.45f, 0.58f, 1f));

            SerializedObject resonanceSerialized =
                new SerializedObject(resonanceMenu);
            SetReference(resonanceSerialized, "resonanceRootPanel", rootPanel);
            SetReference(resonanceSerialized, "skillListPanel", skillPanel);
            SetReference(resonanceSerialized, "partnerLabel", partnerLabel);
            SetReference(resonanceSerialized, "feedbackText", feedback);
            SetReference(resonanceSerialized, "openSkillListButton", open);
            SetReference(resonanceSerialized, "openSkillListLabel", openLabel);
            SetReference(resonanceSerialized, "commandBackButton", commandBack);
            SetReference(resonanceSerialized, "skillListBackButton", skillBack);
            SetReference(resonanceSerialized, "activeSkillButton", active);
            SetReference(resonanceSerialized, "activeSkillLabel", activeLabel);
            SetReference(resonanceSerialized, "readyAccent", readyAccent);
            SetObjectList(resonanceSerialized, "partnerButtons", partnerButtons);
            SetObjectList(resonanceSerialized, "partnerButtonLabels", partnerLabels);
            resonanceSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject commandSerialized = new SerializedObject(commandMenu);
            SetReference(commandSerialized, "resonanceRoot", resonanceRoot);
            SetReference(commandSerialized, "resonanceButton", resonanceButton);
            SetReference(commandSerialized, "resonanceMenu", resonanceMenu);
            commandSerialized.ApplyModifiedPropertiesWithoutUndo();

            DRPGBattleUGUISurface surface =
                root.GetComponentInChildren<DRPGBattleUGUISurface>(true);
            DRPGBattleInteractionController interaction =
                root.GetComponentInChildren<DRPGBattleInteractionController>(true);
            SetComponentReference(surface, "resonanceMenu", resonanceMenu);
            SetComponentReference(interaction, "resonanceMenu", resonanceMenu);
            skillPanel.SetActive(false);
            resonanceRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, BattlePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeGameMenuPrefab(TMP_FontAsset font)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            GameMenuController controller = root.GetComponent<GameMenuController>();
            Transform frame = root.transform.Find("Frame");
            Transform oldButton = frame.Find("ResonanceButton");
            if (oldButton != null)
                UnityEngine.Object.DestroyImmediate(oldButton.gameObject);
            Transform oldPage = frame.Find("ResonancePage");
            if (oldPage != null)
                UnityEngine.Object.DestroyImmediate(oldPage.gameObject);

            GameObject buttonObject = UnityEngine.Object.Instantiate(
                frame.Find("EquipmentButton").gameObject,
                frame);
            buttonObject.name = "ResonanceButton";
            RectTransform navRect = buttonObject.transform as RectTransform;
            navRect.anchoredPosition = new Vector2(1440f, -124f);
            navRect.sizeDelta = new Vector2(200f, 50f);
            SetButtonLabel(buttonObject, "同律");
            Button resonanceButton = buttonObject.GetComponent<Button>();
            SetHoverPalette(resonanceButton);

            GameObject page = CreatePanel("ResonancePage", frame, Ink);
            CopyRect(
                frame.Find("StatusPage") as RectTransform,
                page.transform as RectTransform);
            ResonancePageView pageView = page.AddComponent<ResonancePageView>();
            Image left = CreateImage("BranchPanel", page.transform, Panel);
            SetRect(left.rectTransform,
                new Vector2(38f, -34f), new Vector2(410f, 680f));
            Image field = CreateImage("NodeField", page.transform, Panel);
            SetRect(field.rectTransform,
                new Vector2(480f, -34f), new Vector2(1200f, 680f));
            Image rail = CreateImage("IdentityRail", page.transform, Magenta);
            SetRect(rail.rectTransform,
                new Vector2(458f, -34f), new Vector2(8f, 680f));

            TMP_Text title = CreateText(
                "IdentityTitle", left.transform, font,
                "IDENTITY / 同律调律者", 28f,
                new Vector2(24f, -24f), new Vector2(354f, 52f),
                TextAlignmentOptions.Left, WarmWhite);
            TMP_Text partnerSummary = CreateText(
                "PartnerSummary", field.transform, font,
                "同律对象：未指定", 23f,
                new Vector2(28f, -24f), new Vector2(1110f, 52f),
                TextAlignmentOptions.Left, Cyan);
            TMP_Text feedback = CreateText(
                "Feedback", field.transform, font,
                string.Empty, 19f,
                new Vector2(28f, -604f), new Vector2(1110f, 54f),
                TextAlignmentOptions.Left, new Color(1f, 0.46f, 0.58f, 1f));

            var partnerButtons = new List<Button>();
            var partnerLabels = new List<TMP_Text>();
            for (int i = 0; i < 4; i++)
            {
                Button button = CreateButton(
                    "PartnerBranch_" + i,
                    left.transform,
                    font,
                    "FRIEND BRANCH " + (i + 1),
                    new Vector2(22f, -104f - i * 82f),
                    new Vector2(366f, 66f));
                partnerButtons.Add(button);
                partnerLabels.Add(button.GetComponentInChildren<TMP_Text>());
            }

            var nodeButtons = new List<Button>();
            var nodeLabels = new List<TMP_Text>();
            for (int i = 0; i < 8; i++)
            {
                int column = i % 2;
                int row = i / 2;
                Button button = CreateButton(
                    "ResonanceNode_" + i,
                    field.transform,
                    font,
                    "RESONANCE NODE",
                    new Vector2(28f + column * 548f, -112f - row * 116f),
                    new Vector2(520f, 96f));
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 20f;
                label.alignment = TextAlignmentOptions.Left;
                nodeButtons.Add(button);
                nodeLabels.Add(label);
            }

            SerializedObject pageSerialized = new SerializedObject(pageView);
            SetReference(pageSerialized, "identityTitle", title);
            SetReference(pageSerialized, "partnerSummary", partnerSummary);
            SetReference(pageSerialized, "feedbackText", feedback);
            SetObjectList(pageSerialized, "partnerButtons", partnerButtons);
            SetObjectList(pageSerialized, "partnerLabels", partnerLabels);
            SetObjectList(pageSerialized, "nodeButtons", nodeButtons);
            SetObjectList(pageSerialized, "nodeLabels", nodeLabels);
            pageSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSerialized =
                new SerializedObject(controller);
            SetReference(controllerSerialized, "resonanceButton", resonanceButton);
            SetReference(controllerSerialized, "resonancePage", pageView);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
            page.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool ConfigureLoadedScene(
        DRPGCombatantProfileSO protagonist,
        DRPGCombatantProfileSO vanguard,
        DRPGCombatantProfileSO alchemist,
        DRPGPartySetupSO party,
        DRPGResonanceDefinitionSO definition)
    {
        GameFlowRoot root = UnityEngine.Object.FindObjectOfType<GameFlowRoot>(
            true);
        if (root == null || !root.gameObject.scene.IsValid())
            return false;
        DRPGProgressionRoot progression =
            root.GetComponent<DRPGProgressionRoot>();
        if (progression == null)
            progression = root.gameObject.AddComponent<DRPGProgressionRoot>();
        SerializedObject so = new SerializedObject(progression);
        SerializedProperty registry = so.FindProperty("profileRegistry");
        registry.arraySize = 3;
        registry.GetArrayElementAtIndex(0).objectReferenceValue = protagonist;
        registry.GetArrayElementAtIndex(1).objectReferenceValue = vanguard;
        registry.GetArrayElementAtIndex(2).objectReferenceValue = alchemist;
        so.FindProperty("partySetup").objectReferenceValue = party;
        so.FindProperty("resonanceDefinition").objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();

        DRPGBattleDebugLauncher[] debugLaunchers =
            UnityEngine.Object.FindObjectsOfType<DRPGBattleDebugLauncher>(true);
        for (int i = 0; i < debugLaunchers.Length; i++)
        {
            SerializedObject launcherSo = new SerializedObject(debugLaunchers[i]);
            launcherSo.FindProperty("party").objectReferenceValue = party;
            launcherSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(debugLaunchers[i]);
        }

        DRPGNodeBattleCoordinator[] coordinators =
            UnityEngine.Object.FindObjectsOfType<DRPGNodeBattleCoordinator>(true);
        for (int i = 0; i < coordinators.Length; i++)
        {
            SerializedObject coordinatorSo = new SerializedObject(coordinators[i]);
            coordinatorSo.FindProperty("party").objectReferenceValue = party;
            coordinatorSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinators[i]);
        }
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(progression);
        Scene scene = root.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        Image image = CreateImage(name, parent, color);
        image.raycastTarget = true;
        return image.gameObject;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        Vector2 position,
        Vector2 size)
    {
        Image background = CreateImage(name, parent, Panel);
        SetRect(background.rectTransform, position, size);
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        SetHoverPalette(button);
        TMP_Text text = CreateText(
            "Label",
            background.transform,
            font,
            label,
            21f,
            Vector2.zero,
            Vector2.zero,
            TextAlignmentOptions.Center,
            WarmWhite);
        Stretch(text.rectTransform, 12f);
        return button;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
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
        Vector2 position,
        Vector2 dimensions,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        if (dimensions != Vector2.zero)
            SetRect(text.rectTransform, position, dimensions);
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 topLeftPosition,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeftPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void MoveY(RectTransform rect, float y)
    {
        if (rect == null)
            return;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private static void SetButtonLabel(GameObject owner, string value)
    {
        TMP_Text label = owner.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = value;
    }

    private static void SetHoverPalette(Button button)
    {
        if (button == null)
            return;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.09f, 0.05f, 0.08f, 1f);
        colors.highlightedColor = Magenta;
        colors.selectedColor = Magenta;
        colors.pressedColor = new Color(0.72f, 0.02f, 0.22f, 1f);
        colors.disabledColor = new Color(0.16f, 0.13f, 0.16f, 0.62f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private static void SetComponentReference(
        UnityEngine.Object owner,
        string property,
        UnityEngine.Object value)
    {
        if (owner == null)
            throw new InvalidOperationException(
                "Missing component while assigning " + property);
        SerializedObject so = new SerializedObject(owner);
        SetReference(so, property, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(
        SerializedObject so,
        string property,
        UnityEngine.Object value)
    {
        SerializedProperty field = so.FindProperty(property);
        if (field == null)
            throw new InvalidOperationException("Missing field: " + property);
        field.objectReferenceValue = value;
    }

    private static void SetObjectList<T>(
        SerializedObject so,
        string property,
        IList<T> values) where T : UnityEngine.Object
    {
        SerializedProperty list = so.FindProperty(property);
        if (list == null)
            throw new InvalidOperationException("Missing list: " + property);
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value == null)
            throw new InvalidOperationException("Required asset missing: " + path);
        return value;
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

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
#endif
