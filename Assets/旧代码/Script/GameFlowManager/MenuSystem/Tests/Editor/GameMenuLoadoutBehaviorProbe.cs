#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GameMenuLoadoutBehaviorProbe
{
    private const string ProfilePath =
        "Assets/GameResources/DRPG/Config/Test/" +
        "DRPGCombatant_ankalute_party_vanguard.asset";

    public static string RunPoolExcludesEquipped()
    {
        Fixture fixture = CreateFixture();
        try
        {
            fixture.Page.Bind(fixture.Service, fixture.Profile, null);
            HashSet<string> equipped = CollectEquippedIds(
                fixture.Service.GetView(fixture.Profile));
            Assert(equipped.Count > 0,
                "The loadout fixture must contain at least one equipped skill");

            SkillCardView[] cards =
                fixture.Page.GetComponentsInChildren<SkillCardView>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                SkillCardView card = cards[i];
                if (card == null || card.SourceSlot >= 0 || card.Skill == null)
                    continue;
                Assert(!equipped.Contains(card.Skill.SkillId),
                    "Equipped skill remained in the pool: " +
                    card.Skill.SkillId);
            }

            return "PASS loadout pool excludes equipped skills";
        }
        finally
        {
            fixture.Dispose();
        }
    }

    public static string RunDropCleansGhostImmediately()
    {
        Fixture fixture = CreateFixture();
        try
        {
            fixture.Page.Bind(fixture.Service, fixture.Profile, null);
            DRPGCharacterProgressionView view =
                fixture.Service.GetView(fixture.Profile);
            HashSet<string> equipped = CollectEquippedIds(view);
            int emptySlot = FindEmptySlot(view);
            if (emptySlot < 0)
            {
                emptySlot = DRPGSkillLoadout.SlotCount - 1;
                DRPGProgressionOperationResult unequip =
                    fixture.Service.TryEquipSkill(
                        fixture.Profile,
                        null,
                        emptySlot);
                Assert(unequip.Success,
                    "The loadout fixture could not create an empty slot");
                fixture.Page.Refresh();
                view = fixture.Service.GetView(fixture.Profile);
                equipped = CollectEquippedIds(view);
            }
            Assert(emptySlot >= 0,
                "The loadout fixture must contain an empty slot");

            SkillCardView source = FindUnequippedPoolCard(
                fixture.Page,
                equipped);
            Assert(source != null,
                "The loadout fixture must contain an unequipped pool skill");

            fixture.Page.BeginDrag(
                source.Skill,
                source.SourceSlot,
                (RectTransform)source.transform);
            Assert(CountGhosts(fixture.Root) == 1,
                "Beginning a drag must create exactly one drag ghost");

            fixture.Page.DropOnSlot(emptySlot);
            Assert(CountGhosts(fixture.Root) == 0,
                "A successful drop must clean its drag ghost before refreshing UI");

            fixture.Page.EndDrag();
            Assert(CountGhosts(fixture.Root) == 0,
                "Ending an already handled drag must remain idempotent");
            return "PASS successful loadout drop cleans drag ghost";
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Fixture CreateFixture()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            GameMenuPrefabProbe.PrefabPath);
        Assert(prefab != null, "Game menu prefab is missing");
        DRPGCombatantProfileSO profile =
            AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>(ProfilePath);
        Assert(profile != null, "Loadout test profile is missing");

        GameObject root = UnityEngine.Object.Instantiate(prefab);
        root.hideFlags = HideFlags.HideAndDontSave;
        SkillLoadoutPageView page =
            root.GetComponentInChildren<SkillLoadoutPageView>(true);
        Assert(page != null, "SkillLoadoutPageView is missing");
        return new Fixture(
            root,
            page,
            profile,
            new DRPGProgressionService());
    }

    private static HashSet<string> CollectEquippedIds(
        DRPGCharacterProgressionView view)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < view.EquippedSkills.Count; i++)
        {
            DRPGSkillSO skill = view.EquippedSkills[i];
            if (skill != null)
                result.Add(skill.SkillId);
        }
        return result;
    }

    private static int FindEmptySlot(DRPGCharacterProgressionView view)
    {
        for (int i = 0; i < DRPGSkillLoadout.SlotCount; i++)
        {
            if (i >= view.EquippedSkills.Count || view.EquippedSkills[i] == null)
                return i;
        }
        return -1;
    }

    private static SkillCardView FindUnequippedPoolCard(
        SkillLoadoutPageView page,
        ISet<string> equipped)
    {
        SkillCardView[] cards =
            page.GetComponentsInChildren<SkillCardView>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            SkillCardView card = cards[i];
            if (card != null && card.SourceSlot < 0 && card.Skill != null &&
                !equipped.Contains(card.Skill.SkillId))
            {
                return card;
            }
        }
        return null;
    }

    private static int CountGhosts(GameObject root)
    {
        int count = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == "SkillDragGhost")
                count++;
        }
        return count;
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(
            GameObject root,
            SkillLoadoutPageView page,
            DRPGCombatantProfileSO profile,
            DRPGProgressionService service)
        {
            Root = root;
            Page = page;
            Profile = profile;
            Service = service;
        }

        public GameObject Root { get; }
        public SkillLoadoutPageView Page { get; }
        public DRPGCombatantProfileSO Profile { get; }
        public DRPGProgressionService Service { get; }

        public void Dispose()
        {
            if (Root != null)
                UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
#endif
