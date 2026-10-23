#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGResonanceMenuProbe
{
    private const string BattlePrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string MenuPrefabPath =
        "Assets/Resources/DRPG/UI/PF_GameMenu.prefab";
    private const string DefinitionPath =
        "Assets/GameResources/DRPG/Progression/Test/Resonance/" +
        "DRPGResonanceDefinition_Test.asset";
    private const string PartyPath =
        "Assets/GameResources/DRPG/Progression/Test/Resonance/" +
        "DRPGParty_ResonanceTest.asset";

    public static string Run()
    {
        Assert(Enum.IsDefined(typeof(GameMenuPageId), "Resonance"),
            "GameMenuPageId.Resonance is missing");

        GameObject battle = AssetDatabase.LoadAssetAtPath<GameObject>(
            BattlePrefabPath);
        Assert(battle != null, "Battle presentation prefab is missing");
        BattleCommandMenu commands =
            battle.GetComponentInChildren<BattleCommandMenu>(true);
        Assert(commands != null, "BattleCommandMenu is missing");
        Require(commands.transform, "CommandRoot/Resonance");
        Require(commands.transform, "ResonanceRoot/RootPanel/OpenSkillList");
        Require(commands.transform, "ResonanceRoot/RootPanel/CommandBackButton");
        Require(commands.transform, "ResonanceRoot/SkillListPanel/ActiveSkill");
        Require(commands.transform, "ResonanceRoot/SkillListPanel/SkillBackButton");
        Assert(
            battle.GetComponentInChildren<BattleResonanceMenu>(true) != null,
            "BattleResonanceMenu is missing");

        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(
            MenuPrefabPath);
        Assert(menu != null, "Game menu prefab is missing");
        Require(menu.transform, "Frame/ResonanceButton");
        Transform page = Require(menu.transform, "Frame/ResonancePage");
        Assert(page.GetComponent("ResonancePageView") != null,
            "ResonancePageView is missing");
        return "PASS resonance battle and game-menu assets";
    }

    public static string RunRightClickNavigation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            BattlePrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            BattleCommandMenu menu =
                instance.GetComponentInChildren<BattleCommandMenu>(true);
            Assert(menu != null, "BattleCommandMenu is missing");
            menu.gameObject.SetActive(true);
            SetField(menu, "inputEnabled", true);
            menu.ShowResonanceRoot();
            menu.ShowResonanceSkillList();

            Assert(menu.TryNavigateBack(),
                "First back must return from the skill list to resonance root");
            BattleResonanceMenu resonance =
                instance.GetComponentInChildren<BattleResonanceMenu>(true);
            Assert(resonance != null && !resonance.IsSkillListOpen,
                "First back did not restore the resonance root");
            Assert(menu.TryNavigateBack(),
                "Second back must return from resonance root to command root");
            GameObject commandRoot = GetField<GameObject>(menu, "commandRoot");
            Assert(commandRoot != null && commandRoot.activeSelf,
                "Second back did not restore the command root");
            return "PASS resonance two-level right-click navigation";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    public static string RunPageBehavior()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            MenuPrefabPath);
        DRPGResonanceDefinitionSO definition =
            AssetDatabase.LoadAssetAtPath<DRPGResonanceDefinitionSO>(
                DefinitionPath);
        DRPGPartySetupSO setup =
            AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>(PartyPath);
        Assert(prefab != null && definition != null && setup != null,
            "Resonance page fixtures are missing");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            ResonancePageView page =
                instance.GetComponentInChildren<ResonancePageView>(true);
            Assert(page != null, "ResonancePageView is missing");
            DRPGPartyCompositionState party =
                DRPGPartyCompositionState.FromSetup(setup);
            var service = new DRPGResonanceService(definition);
            page.gameObject.SetActive(true);
            page.Bind(
                service,
                party,
                definition.ProtagonistProfile,
                null);

            Button partnerButton = page.transform
                .Find("BranchPanel/PartnerBranch_0")
                .GetComponent<Button>();
            partnerButton.onClick.Invoke();
            string partnerId = service.State.ResonancePartnerCharacterId;
            Assert(!string.IsNullOrWhiteSpace(partnerId),
                "Selecting a friend branch did not select a partner");
            DRPGPartyRosterSnapshot snapshot = party.CreateSnapshot();
            Assert(snapshot.Count > 1 &&
                snapshot.OrderedProfiles[1].CombatantId == partnerId,
                "Selected resonance partner was not normalized to party slot 1");

            Button activeNode = page.transform
                .Find("NodeField/ResonanceNode_0")
                .GetComponent<Button>();
            activeNode.onClick.Invoke();
            Assert(service.GetEquippedActiveSkill(partnerId) != null,
                "Clicking the active node did not unlock and equip it");

            DRPGResonanceBranchSO branch =
                definition.FindBranchByPartnerId(partnerId);
            page.Bind(service, party, branch.FriendProfile, null);
            Assert(!partnerButton.gameObject.activeSelf,
                "Non-protagonist profiles must not expose resonance controls");
            return "PASS protagonist-only resonance page behavior";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root != null ? root.Find(path) : null;
        Assert(result != null, "Missing path: " + path);
        return result;
    }

    private static T GetField<T>(object owner, string name) where T : class
    {
        FieldInfo field = owner.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(owner) as T;
    }

    private static void SetField(object owner, string name, object value)
    {
        FieldInfo field = owner.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Missing private field: " + name);
        field.SetValue(owner, value);
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
