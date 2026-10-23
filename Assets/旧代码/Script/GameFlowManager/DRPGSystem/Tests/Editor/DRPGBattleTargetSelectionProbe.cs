#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleTargetSelectionProbe
{
    public static string Run()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(attack, 100f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, 10f);
        GameObject menuObject = new GameObject("TargetSelectionProbeMenu");
        GameObject commandRoot = new GameObject("CommandRoot", typeof(RectTransform));
        commandRoot.transform.SetParent(menuObject.transform, false);
        GameObject buttonObject = new GameObject(
            "OrdinaryAttack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(commandRoot.transform, false);
        ProbeBridgeDirector director = null;

        try
        {
            var player = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var enemy = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();

            GameObject skillRoot = new GameObject("SkillRoot", typeof(RectTransform));
            skillRoot.transform.SetParent(menuObject.transform, false);
            GameObject itemRoot = new GameObject("ItemRoot", typeof(RectTransform));
            itemRoot.transform.SetParent(menuObject.transform, false);
            BattleCommandMenu menu = menuObject.AddComponent<BattleCommandMenu>();
            SetField(menu, "commandRoot", commandRoot);
            SetField(menu, "skillRoot", skillRoot);
            SetField(menu, "itemRoot", itemRoot);
            SetField(menu, "basicAttackButton", buttonObject.GetComponent<Button>());
            SetField(menu, "skillButton", CreateButton(menuObject.transform, "Skill"));
            SetField(menu, "itemButton", CreateButton(menuObject.transform, "Item"));
            SetField(menu, "defendButton", CreateButton(menuObject.transform, "Defend"));
            SetField(menu, "escapeButton", CreateButton(menuObject.transform, "Escape"));
            DRPGBattleUGUISurface surface =
                menuObject.AddComponent<DRPGBattleUGUISurface>();
            SetField(surface, "commandMenu", menu);
            DRPGBattleInteractionController interaction =
                menuObject.AddComponent<DRPGBattleInteractionController>();
            SetField(interaction, "commandMenu", menu);
            director = new ProbeBridgeDirector();
            surface.Bind(session);
            surface.RenderInitial(
                DRPGBattlePresentationSnapshot.Capture(session));
            interaction.Configure(
                director,
                surface,
                NullBattleAudioCueSink.Instance,
                () => 1L);
            interaction.Bind(session, 1L);

            float hpBefore = enemy.CurrentHp;
            buttonObject.GetComponent<Button>().onClick.Invoke();

            AssertEqual(hpBefore, enemy.CurrentHp,
                "Choosing ordinary attack must not damage the first enemy automatically");
            AssertEqual(DRPGBattlePhase.TargetSelection, session.Phase,
                "Choosing ordinary attack must enter an explicit target-selection phase");
            AssertSame(player, session.CurrentActor,
                "The acting player must remain current until a target is confirmed");
            AssertFalse(commandRoot.activeSelf,
                "Command roots must hide while the player focuses on a target");

            AssertTrue(interaction.TryNavigateBack(),
                "Right-click navigation must cancel an unconfirmed target selection");
            AssertEqual(DRPGBattlePhase.AwaitingCommand, session.Phase,
                "Cancelling target selection returns to command choice");
            AssertTrue(commandRoot.activeSelf,
                "Cancelling target selection restores the command root");
        }
        finally
        {
            director?.Dispose();
            UnityEngine.Object.DestroyImmediate(menuObject);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS DRPG target selection";
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        return buttonObject.GetComponent<Button>();
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO skill,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }
}
#endif
