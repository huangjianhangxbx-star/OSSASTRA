#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleTargetFeedbackProbe
{
    public static string Run()
    {
        Texture2D texture = new Texture2D(8, 8);
        Sprite portrait = Sprite.Create(
            texture, new Rect(0f, 0f, 8f, 8f), Vector2.one * 0.5f);
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(attack, portrait, 100f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, portrait, 10f);
        GameObject timelineObject = new GameObject(
            "TargetFeedbackTimeline",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(BattleTimelineEntryView));

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
            var defeatedEnemy = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    1));
            defeatedEnemy.ApplyDamage(defeatedEnemy.CurrentHp);
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy, defeatedEnemy });
            session.Start();
            var viewModel = new DRPGBattleViewModel(session);

            PropertyInfo enemiesProperty = typeof(DRPGBattleViewModel).GetProperty(
                "Enemies",
                BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(enemiesProperty != null,
                "The battle view model must expose living enemies for the central battlefield");
            IList enemies = enemiesProperty.GetValue(viewModel) as IList;
            AssertTrue(enemies != null && enemies.Count == 1,
                "The central battlefield must receive only living enemy models");

            BattleTimelineEntryView timelineView =
                timelineObject.GetComponent<BattleTimelineEntryView>();
            Image frame = timelineObject.GetComponent<Image>();
            Outline targetOutline = timelineObject.GetComponent<Outline>();
            targetOutline.enabled = false;
            SetField(timelineView, "frame", frame);
            SetField(timelineView, "targetOutline", targetOutline);
            timelineView.Bind(new DRPGBattleParticipantViewModel(enemy, 0f, false));

            MethodInfo feedbackMethod = typeof(BattleTimelineEntryView).GetMethod(
                "SetTargetFeedback",
                BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(feedbackMethod != null,
                "Timeline entries must expose the shared target-feedback behavior");
            Type feedbackType = feedbackMethod.GetParameters()[0].ParameterType;
            feedbackMethod.Invoke(timelineView, new[] { Enum.ToObject(feedbackType, 2) });
            AssertTrue(targetOutline.enabled,
                "Selected target feedback must make the outline visible");
            AssertTrue(targetOutline.effectDistance.sqrMagnitude >= 16f,
                "Selected target feedback must be visibly stronger than a hover hint");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(timelineObject);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(portrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        return "PASS DRPG target feedback";
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO skill,
        Sprite portrait,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "fullPortrait", portrait);
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
