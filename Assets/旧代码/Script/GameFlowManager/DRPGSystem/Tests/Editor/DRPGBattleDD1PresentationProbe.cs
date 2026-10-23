#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DRPGBattleDD1PresentationProbe
{
    public static string Run()
    {
        VerifyActionPortraitContract();
        VerifyAttackFrameKeepsTheAuthoredFloorBaseline();
        VerifyRefreshDoesNotRebaseActiveIntent();
        VerifyEnemyNeverOwnsRightPortrait();
        VerifyBasicAttackUsesOneSymmetricWorldConflictPath();
        VerifyIntentHasReadableDuration();
        VerifyRestoreOwnsSmoothVisualRecovery();
        return "PASS DD1-style battle presentation contract";
    }

    private static void VerifyRestoreOwnsSmoothVisualRecovery()
    {
        Type actorState = typeof(BattleConflictPresentationContext)
            .GetNestedType("ActorState", BindingFlags.NonPublic);
        AssertNotNull(actorState,
            "Conflict presentation requires a captured actor-state boundary");
        MethodInfo appendRestore = actorState.GetMethod(
            "AppendRestoreTweens",
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);
        AssertNotNull(appendRestore,
            "ActionSettled must tween staged renderer colors back instead of snapping them at completion");
    }

    private static void VerifyIntentHasReadableDuration()
    {
        float anticipation = ReadDefaultFloat(
            typeof(BattleWorldActorSpriteDriver),
            "anticipationSeconds");
        float lunge = ReadDefaultFloat(
            typeof(BattleWorldActorSpriteDriver),
            "lungeSeconds");
        float poseHold = ReadDefaultFloat(
            typeof(BattleWorldActorSpriteDriver),
            "impactPoseHoldSeconds");
        AssertTrue(anticipation + lunge + poseHold >= 0.45f,
            "The shared player/enemy intent cannot finish as a sub-0.2-second blink");
    }

    private static void VerifyAttackFrameKeepsTheAuthoredFloorBaseline()
    {
        var texture = new Texture2D(4, 4);
        Sprite idle = Sprite.Create(texture, new Rect(0, 0, 2, 4),
            new Vector2(0.5f, 0f), 2f);
        Sprite attack = Sprite.Create(texture, new Rect(2, 0, 2, 4),
            new Vector2(0.5f, 0.5f), 2f);
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var root = new GameObject("DD1FloorBaselineProbe");
        try
        {
            SetField(profile, "fullPortrait", idle);
            SetField(profile, "attackCutInPortrait", attack);
            var participant = new BattleParticipantRuntime(
                51,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var viewModel = new DRPGBattleParticipantViewModel(
                participant, 0f, true);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            BattleWorldActorSpriteDriver driver =
                root.AddComponent<BattleWorldActorSpriteDriver>();
            driver.Configure(renderer, 2f);
            driver.Bind(viewModel);
            float idleFloor = renderer.bounds.min.y;

            driver.PlayIntent(EAnimationLogicState.Skill_1);
            float attackFloor = renderer.bounds.min.y;

            AssertNear(idleFloor, attackFloor,
                "A center-pivot attack frame must keep the idle frame's floor baseline");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(idle);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void VerifyBasicAttackUsesOneSymmetricWorldConflictPath()
    {
        AssertRecipeHasNoLegacyAttackCutIn(
            "Assets/GameResources/DRPG/Config/Test/Presentation/Recipes/" +
            "Recipe_ActionWindup_BasicAttack.asset");
        AssertRecipeHasNoLegacyAttackCutIn(
            "Assets/GameResources/DRPG/Config/Test/Presentation/Recipes/" +
            "Recipe_Impact_BasicAttack.asset");
    }

    private static void AssertRecipeHasNoLegacyAttackCutIn(string assetPath)
    {
        BattlePresentationRecipeSO recipe =
            AssetDatabase.LoadAssetAtPath<BattlePresentationRecipeSO>(assetPath);
        AssertNotNull(recipe, $"Missing basic attack recipe: {assetPath}");
        for (int beatIndex = 0; beatIndex < recipe.Beats.Count; beatIndex++)
        {
            var cues = recipe.Beats[beatIndex].Cues;
            for (int cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                BattlePresentationCueSO cue = cues[cueIndex].Cue;
                if (cue != null && cue.Kind == BattlePresentationCueKind.AttackCutIn)
                {
                    throw new InvalidOperationException(
                        $"Basic attack must use the shared world conflict path; " +
                        $"legacy AttackCutIn found in {assetPath}");
                }
            }
        }
    }

    private static void VerifyRefreshDoesNotRebaseActiveIntent()
    {
        var texture = new Texture2D(4, 4);
        Sprite idle = Sprite.Create(texture, new Rect(0, 0, 2, 4),
            new Vector2(0.5f, 0f), 2f);
        Sprite attack = Sprite.Create(texture, new Rect(2, 0, 2, 4),
            new Vector2(0.5f, 0f), 2f);
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var root = new GameObject("DD1RefreshDuringIntentProbe");
        try
        {
            SetField(profile, "fullPortrait", idle);
            SetField(profile, "attackCutInPortrait", attack);
            var participant = new BattleParticipantRuntime(
                41,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var viewModel = new DRPGBattleParticipantViewModel(
                participant, 0f, true);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            BattleWorldActorSpriteDriver driver =
                root.AddComponent<BattleWorldActorSpriteDriver>();
            driver.Configure(renderer, 2f);
            driver.Bind(viewModel);
            Vector3 canonicalHome = root.transform.localPosition;

            IBattlePresentationHandle handle =
                driver.PlayIntent(EAnimationLogicState.Skill_1);
            root.transform.localPosition = canonicalHome +
                new Vector3(0.47f, 0.19f, 0f);
            driver.Bind(viewModel);

            AssertSame(attack, renderer.sprite,
                "Refreshing an unchanged actor must not cut its attack frame");
            AssertTrue(handle != null && !handle.IsSettled,
                "Refreshing an unchanged actor must not settle its active intent");

            driver.RestoreVisual();
            AssertVectorNear(canonicalHome, root.transform.localPosition,
                "Restore must return to the authored home, not a transient lunge pose");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(idle);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void VerifyActionPortraitContract()
    {
        PropertyInfo actionPortrait = typeof(DRPGBattleParticipantViewModel)
            .GetProperty("AttackCutInPortrait", BindingFlags.Public | BindingFlags.Instance);
        AssertNotNull(actionPortrait,
            "World actors require an attack-frame portrait in the participant view model");

        MethodInfo restore = typeof(BattleWorldActorSpriteDriver).GetMethod(
            "RestoreVisual",
            BindingFlags.Public | BindingFlags.Instance);
        AssertNotNull(restore,
            "The DD1 action frame must have a deterministic public restore path");

        var texture = new Texture2D(4, 4);
        Sprite idle = Sprite.Create(texture, new Rect(0, 0, 2, 4),
            new Vector2(0.5f, 0f), 2f);
        Sprite attack = Sprite.Create(texture, new Rect(2, 0, 2, 4),
            new Vector2(0.5f, 0f), 2f);
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var root = new GameObject("DD1SpriteDriverProbe");
        try
        {
            SetField(profile, "fullPortrait", idle);
            SetField(profile, "attackCutInPortrait", attack);
            var participant = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var viewModel = new DRPGBattleParticipantViewModel(
                participant, 0f, true);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            BattleWorldActorSpriteDriver driver =
                root.AddComponent<BattleWorldActorSpriteDriver>();
            driver.Configure(renderer, 2f);
            driver.Bind(viewModel);
            AssertSame(idle, renderer.sprite,
                "Binding must start from the actor's idle frame");

            IBattlePresentationHandle handle =
                driver.PlayIntent(EAnimationLogicState.Skill_1);
            AssertSame(attack, renderer.sprite,
                "Windup must switch to the authored static attack frame immediately");
            AssertTrue(handle != null &&
                       handle.Outcome != BattlePresentationOutcome.Skipped,
                "An authored attack frame must participate in the presentation gate");

            restore.Invoke(driver, null);
            AssertSame(idle, renderer.sprite,
                "Restore must return the actor to the idle frame");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(idle);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void VerifyEnemyNeverOwnsRightPortrait()
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var root = new GameObject("RightPortraitPolicyProbe");
        try
        {
            BattleActorPresenter presenter = root.AddComponent<BattleActorPresenter>();
            var enemy = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            presenter.Bind(new DRPGBattleParticipantViewModel(enemy, 0f, true));
            AssertTrue(!root.activeSelf,
                "Enemy turns must never show the right-side current-actor portrait");

            var player = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            presenter.Bind(new DRPGBattleParticipantViewModel(player, 0f, true));
            AssertTrue(root.activeSelf,
                "A player actor may reactivate the right-side current-actor portrait");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
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

    private static float ReadDefaultFloat(Type componentType, string fieldName)
    {
        var root = new GameObject(componentType.Name + "DurationProbe");
        try
        {
            Component component = root.AddComponent(componentType);
            FieldInfo field = componentType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(componentType.Name, fieldName);
            return (float)field.GetValue(component);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new InvalidOperationException(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertSame(UnityEngine.Object expected,
        UnityEngine.Object actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException(message);
    }

    private static void AssertVectorNear(
        Vector3 expected,
        Vector3 actual,
        string message)
    {
        if ((expected - actual).sqrMagnitude > 0.000001f)
        {
            throw new InvalidOperationException(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new InvalidOperationException(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
