#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleQuoteAndAudioProbe
{
    public static string Run()
    {
        DRPGBattleQuoteSetSO quoteSet =
            ScriptableObject.CreateInstance<DRPGBattleQuoteSetSO>();
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        DRPGCombatantProfileSO enemyProfile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        GameObject presenterObject = null;
        GameObject driverObject = null;
        Texture2D texture = null;
        Sprite portrait = null;

        try
        {
            SetField(
                quoteSet,
                "turnBeginLines",
                new List<DRPGBattleQuoteLine>
                {
                    new DRPGBattleQuoteLine("轮到我了。", "voice_turn_01")
                });
            AssertTrue(quoteSet.TryGetRandom(
                    DRPGBattleQuoteContext.TurnBegin,
                    out DRPGBattleQuoteLine line),
                "A configured turn-begin quote must resolve");
            AssertFalse(string.IsNullOrWhiteSpace(line.Text),
                "A resolved battle quote must contain text");
            AssertEqual("voice_turn_01", line.VoiceCueId,
                "The quote must preserve its optional semantic voice cue ID");

            SetField(
                quoteSet,
                "defeatedLines",
                new List<DRPGBattleQuoteLine>
                {
                    new DRPGBattleQuoteLine("もう……動けない……", "voice_defeated_01")
                });
            AssertTrue(quoteSet.TryGetRandom(
                    DRPGBattleQuoteContext.Defeated,
                    out DRPGBattleQuoteLine defeatedLine),
                "A configured defeated quote must resolve");
            AssertEqual("voice_defeated_01", defeatedLine.VoiceCueId,
                "The defeated quote must preserve its semantic voice cue ID");

            NullBattleAudioCueSink.Instance.Play(
                new BattleAudioCueRequest(BattleAudioCue.Hit));

            SetField(playerProfile, "agility", 30f);
            SetField(enemyProfile, "agility", 20f);
            SetField(playerProfile, "skills", new List<DRPGSkillSO> { attack });
            SetField(enemyProfile, "skills", new List<DRPGSkillSO> { attack });
            var player = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            SetField(playerProfile, "battleQuotes", quoteSet);
            presenterObject = new GameObject("BattleTurnQuotePresenter_Probe");
            BattleTurnQuotePresenter presenter =
                presenterObject.AddComponent<BattleTurnQuotePresenter>();
            int voiceActorRuntimeId = 0;
            string requestedVoiceCueId = string.Empty;
            presenter.VoiceCueRequested += (runtimeId, cueId) =>
            {
                voiceActorRuntimeId = runtimeId;
                requestedVoiceCueId = cueId;
            };
            IBattlePresentationHandle quotePlayback = presenter.Show(
                player,
                DRPGBattleQuoteContext.Defeated);
            AssertFalse(quotePlayback.IsSettled,
                "A configured quote must return a pending tracked handle");
            AssertEqual(player.RuntimeId, voiceActorRuntimeId,
                "A voice request must identify the participant who owns the quote");
            AssertEqual("voice_defeated_01", requestedVoiceCueId,
                "The presenter must request the defeated semantic voice cue");
            presenter.Hide();
            AssertEqual(BattlePresentationOutcome.Cancelled, quotePlayback.Outcome,
                "Hiding a quote must cancel its pending handle");

            IBattlePresentationHandle disabledQuote = presenter.Show(
                player,
                DRPGBattleQuoteContext.Defeated);
            InvokePrivate(presenter, "OnDisable");
            AssertEqual(BattlePresentationOutcome.Cancelled, disabledQuote.Outcome,
                "Disabling the quote presenter must cancel quote playback");

            BattleParticipantRuntime withoutQuotes = new BattleParticipantRuntime(
                3,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.PartyMember,
                    1));
            IBattlePresentationHandle skippedQuote = presenter.Show(
                withoutQuotes,
                DRPGBattleQuoteContext.TurnBegin);
            AssertEqual(BattlePresentationOutcome.Skipped, skippedQuote.Outcome,
                "A participant without configured quotes must fail open");

            texture = new Texture2D(2, 2);
            portrait = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                Vector2.one * 0.5f);
            SetField(playerProfile, "fullPortrait", portrait);
            driverObject = new GameObject(
                "Sprite Actor Driver Probe",
                typeof(RectTransform),
                typeof(Image));
            SpriteBattleActorAnimationDriver driver =
                driverObject.AddComponent<SpriteBattleActorAnimationDriver>();
            driverObject.GetComponent<Image>().sprite = portrait;
            driver.BindActor(playerProfile);
            IBattlePresentationHandle actorPlayback = driver.PlayIntent(
                EAnimationLogicState.Skill_1);
            AssertFalse(actorPlayback.IsSettled,
                "Sprite actor motion must expose pending tween playback");
            Tween activeTween = GetField<Tween>(driver, "activeTween");
            activeTween.Complete(true);
            AssertEqual(BattlePresentationOutcome.Completed, actorPlayback.Outcome,
                "Completing the actor tween must complete its handle");
            IBattlePresentationHandle disabledActor = driver.PlayIntent(
                EAnimationLogicState.Skill_1);
            InvokePrivate(driver, "OnDisable");
            AssertEqual(BattlePresentationOutcome.Cancelled, disabledActor.Outcome,
                "Disabling the actor driver must cancel its active tween");
            var enemy = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });
            session.Start();
            AssertEqual(DRPGBattlePhase.AwaitingCommand, session.Phase,
                "A null audio sink must never advance or block combat state");
        }
        finally
        {
            if (driverObject != null)
                UnityEngine.Object.DestroyImmediate(driverObject);
            if (presenterObject != null)
                UnityEngine.Object.DestroyImmediate(presenterObject);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(quoteSet);
            if (portrait != null)
                UnityEngine.Object.DestroyImmediate(portrait);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
        }

        return "PASS DRPG turn quote and null audio seam";
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

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
