#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattleImpactFeedbackProbe
{
    public static string Run()
    {
        Texture2D texture = new Texture2D(2, 2);
        Sprite normalPortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        Sprite hitPortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        Sprite damageBrokenPortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        GameObject root = new GameObject("DRPG Impact Feedback Probe");

        try
        {
            SetField(profile, "fullPortrait", normalPortrait);
            AssertSame(
                normalPortrait,
                profile.ResolvePortrait(DRPGBattlePortraitState.Hit),
                "A missing hit portrait must fall back to the normal portrait");
            AssertSame(
                normalPortrait,
                profile.ResolvePortrait(DRPGBattlePortraitState.Broken),
                "A missing broken portrait must fall back to the normal portrait");
            SetField(profile, "hitPortrait", hitPortrait);
            SetField(profile, "damageBrokenPortrait", damageBrokenPortrait);
            SetField(profile, "damageBrokenHpRatio", 0.5f);

            BattleParticipantRuntime party = CreateParticipant(
                1,
                profile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(
                2,
                profile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            party.ApplyDamage(60f);

            BattlePartyMemberView partyView = CreateChild<BattlePartyMemberView>(
                root.transform,
                "Party");
            Image partyPortrait = partyView.gameObject.AddComponent<Image>();
            SetField(partyView, "portrait", partyPortrait);
            partyView.Bind(CreateViewModel(party));
            BattlePartyHud partyHud = root.AddComponent<BattlePartyHud>();
            SetField(
                partyHud,
                "memberViews",
                new List<BattlePartyMemberView> { partyView });

            BattlefieldEnemyView enemyView = CreateChild<BattlefieldEnemyView>(
                root.transform,
                "Enemy");
            Image enemyPortrait = enemyView.gameObject.AddComponent<Image>();
            SetField(enemyView, "portrait", enemyPortrait);
            enemyView.Bind(CreateViewModel(enemy), null);
            BattlefieldEnemyRosterView enemyRoster =
                root.AddComponent<BattlefieldEnemyRosterView>();
            SetField(
                enemyRoster,
                "enemyViews",
                new List<BattlefieldEnemyView> { enemyView });

            BattleImpactFeedbackPresenter presenter =
                root.AddComponent<BattleImpactFeedbackPresenter>();
            SetField(presenter, "partyHud", partyHud);
            SetField(presenter, "enemyRoster", enemyRoster);

            GameObject numberObject = new GameObject(
                "Damage Number Template",
                typeof(RectTransform),
                typeof(CanvasGroup));
            numberObject.transform.SetParent(root.transform, false);
            BattleDamageNumberView numberTemplate =
                numberObject.AddComponent<BattleDamageNumberView>();
            SetField(numberTemplate, "rectTransform",
                numberObject.GetComponent<RectTransform>());
            SetField(numberTemplate, "canvasGroup",
                numberObject.GetComponent<CanvasGroup>());
            SetField(presenter, "feedbackLayer", root.transform as RectTransform);
            SetField(presenter, "damageNumberTemplate", numberTemplate);

            MethodInfo preparePool = typeof(BattleImpactFeedbackPresenter)
                .GetMethod("PrepareForBattle", BindingFlags.Instance |
                    BindingFlags.NonPublic);
            if (preparePool == null)
            {
                throw new Exception(
                    "Battle bind has no damage-number prewarm seam; the first real hit must instantiate TMP/Canvas feedback on its impact frame.");
            }
            preparePool.Invoke(presenter, null);
            List<BattleDamageNumberView> prewarmed =
                GetField<List<BattleDamageNumberView>>(
                    presenter,
                    "pooledNumbers");
            AssertEqual(1, prewarmed.Count,
                "Battle preparation must retain one inactive damage number");
            int prewarmedInstanceId = prewarmed[0].GetInstanceID();

            AssertAnchor(
                presenter,
                party.RuntimeId,
                BattleImpactAnchorKind.PartyHud);
            AssertAnchor(
                presenter,
                enemy.RuntimeId,
                BattleImpactAnchorKind.EnemyField);

            var hit = new BattleHitPresentationSnapshot(
                enemy.RuntimeId,
                party.RuntimeId,
                "probe_hit",
                100f,
                60f,
                40f,
                true,
                false,
                false,
                DRPGLowHpPresentationState.DamageBroken,
                DRPGBattleRosterRole.PartyMember);
            IBattlePresentationHandle playback = presenter.Present(
                new[] { hit },
                1f);
            AssertFalse(playback.IsSettled,
                "Impact playback must remain pending while child feedback is active");
            AssertSame(hitPortrait, partyPortrait.sprite,
                "Impact playback must temporarily select the hit portrait");

            List<Sequence> impacts = GetField<List<Sequence>>(
                presenter,
                "impactSequences");
            List<BattleDamageNumberView> numbers =
                GetField<List<BattleDamageNumberView>>(
                    presenter,
                    "activeNumbers");
            AssertEqual(prewarmedInstanceId, numbers[0].GetInstanceID(),
                "The first hit must reuse the prewarmed damage number");
            Sequence impactSequence = impacts[0];
            BattleDamageNumberView number = numbers[0];
            Sequence numberSequence = GetField<Sequence>(number, "sequence");

            impactSequence.Complete(true);
            AssertFalse(playback.IsSettled,
                "The composite handle must wait for the damage-number lifetime");
            numberSequence.Complete(true);
            AssertEqual(BattlePresentationOutcome.Completed, playback.Outcome,
                "Impact playback must complete after every child completes");
            AssertSame(damageBrokenPortrait, partyPortrait.sprite,
                "A broken participant must restore its permanent broken portrait");

            IBattlePresentationHandle cancelled = presenter.Present(
                new[] { hit },
                1f);
            presenter.CancelAll(BattlePresentationCancelReason.ScopeEnded);
            AssertEqual(BattlePresentationOutcome.Cancelled, cancelled.Outcome,
                "Cancelling impact feedback must cancel every pending child");
            AssertEqual(0, GetField<List<BattleDamageNumberView>>(
                    presenter,
                    "activeNumbers").Count,
                "Cancellation must return every damage-number view to the pool");

            var enemyHit = new BattleHitPresentationSnapshot(
                party.RuntimeId,
                enemy.RuntimeId,
                "enemy_proxy_probe",
                100f,
                20f,
                80f,
                true,
                false,
                false,
                DRPGLowHpPresentationState.Normal,
                DRPGBattleRosterRole.Enemy);
            IBattlePresentationHandle enemyPlayback = presenter.Present(
                new[] { enemyHit },
                1f);
            AssertSame(normalPortrait, enemyPortrait.sprite,
                "Legacy enemy feedback must not swap the world actor proxy " +
                "to its hit portrait; the semantic world layer owns that reaction");
            AssertEqual(0, GetField<List<Sequence>>(
                    presenter,
                    "impactSequences").Count,
                "Legacy enemy HUD feedback must not add a second flash; " +
                "the semantic world actor owns hit motion and tint");
            presenter.CancelAll(BattlePresentationCancelReason.ScopeEnded);
            AssertEqual(BattlePresentationOutcome.Cancelled, enemyPlayback.Outcome,
                "Enemy damage-number feedback must remain cancellable");

            IBattlePresentationHandle skipped = presenter.Present(
                Array.Empty<BattleHitPresentationSnapshot>(),
                1f);
            AssertEqual(BattlePresentationOutcome.Skipped, skipped.Outcome,
                "An impact request without hits must skip immediately");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(damageBrokenPortrait);
            UnityEngine.Object.DestroyImmediate(hitPortrait);
            UnityEngine.Object.DestroyImmediate(normalPortrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        return "PASS DRPG impact anchors and portrait fallback";
    }

    private static DRPGBattleParticipantViewModel CreateViewModel(
        BattleParticipantRuntime participant)
    {
        return new DRPGBattleParticipantViewModel(participant, 0f, false);
    }

    private static BattleParticipantRuntime CreateParticipant(
        int id,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            id,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static T CreateChild<T>(Transform parent, string name)
        where T : Component
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }

    private static void AssertAnchor(
        IBattleParticipantAnchorResolver resolver,
        int runtimeId,
        BattleImpactAnchorKind expectedKind)
    {
        if (!resolver.TryResolve(runtimeId, out BattleImpactAnchor anchor))
            throw new Exception("Impact anchor was not resolved for " + runtimeId);
        if (anchor == null || anchor.Kind != expectedKind)
        {
            throw new Exception(
                $"Wrong impact anchor. Expected={expectedKind}, Actual={anchor?.Kind}");
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

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }
}
#endif
