#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleUGUIProbe
{
    public static string Run()
    {
        GameObject viewport = new GameObject(
            "PortraitViewport",
            typeof(RectTransform),
            typeof(RectMask2D));
        GameObject imageObject = new GameObject(
            "FullPortrait",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(viewport.transform, false);

        Texture2D texture = new Texture2D(8, 8);
        Sprite portrait = Sprite.Create(
            texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        GameObject surfaceObject = new GameObject("UGUI Surface Probe");
        GameObject enemyObject = new GameObject(
            "Enemy Warning Probe",
            typeof(RectTransform));
        GameObject warningRoot = new GameObject(
            "WarningRoot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        warningRoot.transform.SetParent(enemyObject.transform, false);
        GameObject warningLabelObject = new GameObject(
            "WarningValue",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        warningLabelObject.transform.SetParent(warningRoot.transform, false);
        GameObject postureBreakRoot = new GameObject(
            "PostureBreakBadge",
            typeof(RectTransform));
        postureBreakRoot.transform.SetParent(enemyObject.transform, false);
        GameObject postureStabilityRoot = new GameObject(
            "PostureStabilityBadge",
            typeof(RectTransform));
        postureStabilityRoot.transform.SetParent(enemyObject.transform, false);
        GameObject postureStabilityLabelObject = new GameObject(
            "PostureStabilityLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        postureStabilityLabelObject.transform.SetParent(
            postureStabilityRoot.transform,
            false);

        try
        {
            SetField(profile, "fullPortrait", portrait);
            SetField(profile, "avatarZoom", 2.25f);
            var participant = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    0));
            var viewModel = new DRPGBattleParticipantViewModel(participant, 0f, true);
            Image image = imageObject.GetComponent<Image>();

            DRPGPortraitImageBinder.Bind(image, viewModel, true);
            AssertSame(portrait, image.sprite,
                "UGUI must display the same full portrait Sprite used by the profile");
            AssertTrue(viewport.GetComponent<RectMask2D>() != null,
                "Avatar cropping must use a UGUI mask hierarchy");
            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            AssertTrue(fitter == null || !fitter.enabled,
                "Avatar cropping must use an explicit masked portrait rect");
            AssertTrue(image.rectTransform.sizeDelta.x > viewport.GetComponent<RectTransform>().rect.width,
                "Avatar views must support a head-and-shoulders zoom independent of the full portrait");

            DRPGPortraitImageBinder.Bind(image, null);
            AssertFalse(image.enabled,
                "Missing portraits must be handled without throwing");

            DRPGBattleUGUISurface surface =
                surfaceObject.AddComponent<DRPGBattleUGUISurface>();
            AssertTrue(surface is IDRPGBattleTargetFeedbackSurface,
                "The stable UGUI surface must own semantic target feedback");

            var enemyParticipant = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            BattlefieldEnemyView enemyView =
                enemyObject.AddComponent<BattlefieldEnemyView>();
            Image warningBackground = warningRoot.GetComponent<Image>();
            TMP_Text warningLabel =
                warningLabelObject.GetComponent<TextMeshProUGUI>();
            SetField(enemyView, "warningRoot", warningRoot);
            SetField(enemyView, "warningBackground", warningBackground);
            SetField(enemyView, "warningLabel", warningLabel);
            SetField(enemyView, "warningPulseTarget",
                warningRoot.GetComponent<RectTransform>());
            SetField(enemyView, "postureBreakIndicator", postureBreakRoot);
            SetField(
                enemyView,
                "postureStabilityIndicator",
                postureStabilityRoot);
            TMP_Text postureStabilityLabel =
                postureStabilityLabelObject.GetComponent<TextMeshProUGUI>();
            SetField(
                enemyView,
                "postureStabilityLabel",
                postureStabilityLabel);

            enemyView.Bind(
                CreateEnemyWarningModel(enemyParticipant, 2),
                null);
            AssertTrue(warningRoot.activeSelf,
                "A windup enemy must expose its warning badge");
            AssertEqual("2", warningLabel.text,
                "The warning badge must show the sampled warning value");
            Color warningTwoColor = warningBackground.color;

            enemyView.Bind(
                CreateEnemyWarningModel(enemyParticipant, 0),
                null);
            AssertEqual("0", warningLabel.text,
                "A ready enemy must visibly expose warning zero");
            AssertTrue(warningBackground.color != warningTwoColor,
                "Warning zero must use a stronger visual tier than warning two");

            enemyView.Bind(
                new DRPGBattleParticipantViewModel(
                    enemyParticipant,
                    0f,
                    false),
                null);
            AssertFalse(warningRoot.activeSelf,
                "Enemies without a public attack warning must hide the badge");

            enemyParticipant.ReducePosture(
                enemyParticipant.CurrentPosture);
            enemyView.Bind(
                new DRPGBattleParticipantViewModel(
                    enemyParticipant,
                    0f,
                    false),
                null);
            AssertTrue(postureBreakRoot.activeSelf,
                "PostureBreak exposure must display a replaceable field badge");
            AssertFalse(postureStabilityRoot.activeSelf,
                "Exposure and recovery-stability badges must be exclusive");

            enemyView.Bind(
                new DRPGBattleParticipantViewModel(
                    enemyParticipant,
                    0f,
                    false),
                null);
            AssertTrue(postureBreakRoot.activeSelf,
                "Rebinding the same break state must preserve its badge");

            enemyParticipant.ResolvePostureBreakOwnTurnStart();
            enemyParticipant.ResolvePostureBreakOwnTurnStart();
            enemyView.Bind(
                new DRPGBattleParticipantViewModel(
                    enemyParticipant,
                    0f,
                    false),
                null);
            AssertFalse(postureBreakRoot.activeSelf,
                "Recovery must remove the exposure badge");
            AssertTrue(postureStabilityRoot.activeSelf,
                "Recovery must display a distinct stability badge");
            AssertEqual("STABLE 3", postureStabilityLabel.text,
                "The stability badge must expose own actions remaining");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(portrait);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(viewport);
            UnityEngine.Object.DestroyImmediate(surfaceObject);
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }

        return "PASS DRPG battle UGUI";
    }

    private static DRPGBattleParticipantViewModel CreateEnemyWarningModel(
        BattleParticipantRuntime participant,
        int warningValue)
    {
        DRPGEnemyPlanState state = warningValue == 0
            ? DRPGEnemyPlanState.AwaitingPlayerResponse
            : DRPGEnemyPlanState.Windup;
        var snapshot = new DRPGEnemyActionPlanSnapshot(
            1L,
            participant.RuntimeId,
            DRPGEnemyPlannedActionKind.Attack,
            state,
            warningValue,
            2,
            0);
        return new DRPGBattleParticipantViewModel(
            participant,
            0f,
            false,
            snapshot);
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

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
