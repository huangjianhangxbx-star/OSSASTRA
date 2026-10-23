#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

internal static class DRPGSettlementWidgetsProbe
{
    internal static void ExpiredStatusesLeaveOnlyOnTheirOwnCursor()
    {
        Surfaces ui = null;
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, configureSurface: d => ui = new Surfaces(d), statusDuration: 1))
        {
            Check(f.Domain.Player.Statuses.Snapshot().Count == 0 && f.Domain.Enemy.Statuses.Snapshot().Count == 0,
                "The real rule ledger did not expire the fixture statuses.");
            ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|1"); ui.World.AssertRows("enemy-poison ×1|1");
            using (var sequence = f.Start())
            {
                ui.Party.AssertRows("party-regen ×1|1"); ui.World.AssertRows("enemy-poison ×1|1");
                f.Step(sequence, .451); ui.Party.AssertRows(); ui.World.AssertRows("enemy-poison ×1|1");
                f.Step(sequence, .451); ui.Enemy.AssertRows(); ui.World.AssertRows();
                f.Step(sequence, .451);
                Check(sequence.ReadyForDefeatFinalization && f.Completed == 0 && f.RuleLease.IsActive,
                    "Expiry bypassed the normal final settlement gate.");
            }
        }
    }

    // Break caught: real Stage9 cursors only update HP, skip an enemy leaf,
    // or a canonical refresh exposes future status durations before their beat.
    internal static void SettlementCursorsUpdateAllThreeWidgetSurfaces()
    {
        Surfaces ui = null;
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, configureSurface: d => ui = new Surfaces(d)))
        using (var sequence = f.Start())
        {
            Check(ui.Party.Posture.text == "架势 100 / 100", "Party posture widget was not projected.");
            ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|2");
            ui.Enemy.AssertRows("enemy-poison ×1|2"); ui.World.AssertRows("enemy-poison ×1|2");
            Check(ui.EnemyHp.text == "HP 100 / 100", "Enemy roster missed the primary HP cursor.");
            f.Domain.Bridge.FlushQueuedSignals();
            f.Domain.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(f.Domain.Session));
            ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|2");
            f.Step(sequence, .451);
            ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|1");
            ui.World.AssertRows("enemy-poison ×1|2");
            f.Step(sequence, .451);
            ui.Enemy.AssertRows("enemy-poison ×1|1"); ui.World.AssertRows("enemy-poison ×1|1");
            Check(ui.EnemyHp.text == "HP 93 / 100", "Enemy HP did not advance with its status cursor.");
        }
    }

    // Break caught: a direct pooled enemy Bind bypasses its roster's lease guard.
    internal static void DirectEnemyBindCannotEraseALeasedWidget()
    {
        Surfaces ui = null;
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, configureSurface: d => ui = new Surfaces(d)))
        {
            ui.EnemyView.Bind(null, null);
            Check(ui.EnemyView.gameObject.activeSelf && ui.EnemyView.BoundRuntimeId == f.Domain.Enemy.RuntimeId,
                "Direct enemy binding erased a leased participant.");
        }
    }

    // Break caught: posture changes animate later/read live HP, or enemy/world
    // leaves keep the old capacity while the same cursor updates the party.
    internal static void PostureCursorUpdatesTheBoundWidgetsWithoutChangingTheirPose()
    {
        Surfaces ui = null;
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, configureSurface: d => ui = new Surfaces(d)))
        {
            var pose = ui.EnemyView.transform.localScale;
            var before = f.Projection.GetPresented(f.Domain.Enemy.RuntimeId);
            var after = new BattleProjectedParticipant(before.RuntimeId, before.Hp, before.MaxHp, 30, 60, before.Statuses);
            Check(f.Projection.Apply(new BattleProjectionCursor(f.Projection.Owner, f.Projection.NextCursorOrdinal, before, after)).Accepted,
                "Posture fixture cursor was rejected.");
            Check(ui.Enemy.Posture.text == "架势 30 / 60" && ui.World.Posture.text == "架势 30 / 60" &&
                ui.Enemy.Fill.fillAmount == .5f && ui.World.Fill.fillAmount == .5f && ui.EnemyView.transform.localScale == pose,
                "The bound posture widgets did not synchronously consume their frozen cursor.");
        }
    }

    // Break caught: same-name sources collapse together, expired entries remain,
    // or reused items retain another instance's labels/icon/duration.
    internal static void StatusInstancesStaySeparateAndPooledItemsAreCleared()
    {
        Surfaces ui = null;
        using (var f = new DRPGSettlementPresentationProbe.Fixture(true, configureSurface: d => ui = new Surfaces(d)))
        {
            var before = f.Projection.GetPresented(f.Domain.Enemy.RuntimeId);
            var original = before.Statuses[0]; var owner = new ParticipantId(before.RuntimeId);
            var second = new DRPGStatusRuntimeSnapshot(original.Spec, 2, 3, "other-source-instance", owner,
                new ParticipantId(f.Domain.Player.RuntimeId), 2);
            var states = new DRPGStatusCollectionSnapshot(owner, 3, new[] { original, second });
            Apply(f, before, before.WithSettlement(before.Hp, before.Posture, states));
            ui.Enemy.AssertRows("enemy-poison ×1|2", "enemy-poison ×2|3");
            var activeRows = ui.Enemy.Rows.GetComponentsInChildren<TMP_Text>(false);
            int count = ui.Enemy.Rows.childCount;
            before = f.Projection.GetPresented(before.RuntimeId);
            Apply(f, before, before.WithSettlement(before.Hp, before.Posture, new DRPGStatusCollectionSnapshot(owner, 3, Array.Empty<DRPGStatusRuntimeSnapshot>())));
            ui.Enemy.AssertRows(); ui.World.AssertRows();
            foreach (var text in activeRows) Check(text.text == string.Empty, "An inactive pooled status retained its previous text.");
            before = f.Projection.GetPresented(before.RuntimeId);
            Apply(f, before, before.WithSettlement(before.Hp, before.Posture, states));
            Check(ui.Enemy.Rows.childCount == count, "Status refresh created new rows instead of reusing its pool.");
            ui.Enemy.AssertRows("enemy-poison ×1|2", "enemy-poison ×2|3");
        }
    }

    private static void Apply(DRPGSettlementPresentationProbe.Fixture f, BattleProjectedParticipant before, BattleProjectedParticipant after)
    { Check(f.Projection.Apply(new BattleProjectionCursor(f.Projection.Owner, f.Projection.NextCursorOrdinal, before, after)).Accepted, "Status cursor rejected."); }

    internal static System.Collections.IEnumerator ProductionWidgets()
    {
        foreach (bool lethal in new[] { false, true })
        {
            Surfaces ui = null;
            using (var f = new DRPGSettlementPresentationProbe.Fixture(true, lethalEnemy: lethal, configureSurface: d => ui = new Surfaces(d)))
            {
                var clock = f.Domain.Root.AddComponent<UnityRenderFrameFence>(); f.Clock.UseLiveClock(clock);
                double deadline = clock.Now + 6d, began = clock.Now;
                long frame = clock.RenderedFrame; int samples = 0;
                using (var sequence = f.Start())
                {
                    Check(f.Projection.GetPresented(f.Domain.Player.RuntimeId).Hp == 90 &&
                        f.Projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp == 99, "The first real settlement beat exposed future HP.");
                    ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|2");
                    ui.World.AssertRows("enemy-poison ×1|2");
                    var enqueue = typeof(DRPGBattlePresentationEventBridge).GetMethod("OnParticipantChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                    Check(enqueue != null, "Missing real participant notification handler.");
                    while (!sequence.ReadyForDefeatFinalization && clock.Now < deadline)
                    {
                        // Replay a delayed real-domain notification at the bridge
                        // boundary. It captures canonical final HP/status, then
                        // the actual LateUpdate must consume it without a rebind.
                        enqueue.Invoke(f.Domain.Bridge, new object[] { f.Domain.Player });
                        Check(f.Domain.Bridge.PendingSignalCount > 0, "The real bridge did not enqueue canonical state.");
                        yield return new WaitForEndOfFrame();
                        Check(f.Domain.Bridge.PendingSignalCount == 0, "LateUpdate did not consume the queued canonical signal.");
                        sequence.Tick();
                        Check(sequence.Fault.Length == 0, sequence.Fault);
                        samples++;
                        bool enemyResult = f.Projection.GetPresented(f.Domain.Enemy.RuntimeId).Hp < 99;
                        bool regenResult = f.Projection.GetPresented(f.Domain.Player.RuntimeId).Hp == 93;
                        ui.Party.AssertRows("party-poison ×2|1", "party-regen ×1|" + (regenResult ? "1" : "2"));
                        ui.Enemy.AssertRows("enemy-poison ×1|" + (enemyResult ? "1" : "2"));
                        ui.World.AssertRows("enemy-poison ×1|" + (enemyResult ? "1" : "2"));
                        Check(f.Domain.Pool.ReleaseCount == 0 && f.Completed == 0 && f.RuleLease.IsActive &&
                            f.Visuals.ActiveNumbers().Length <= 1, "Real-frame widgets advanced or released the action.");
                    }
                    Check(sequence.ReadyForDefeatFinalization && samples >= 3 && clock.RenderedFrame > frame && clock.Now - began >= 1.35d,
                        "Status widgets did not cross all three independently held real-frame results.");
                    Check(ui.Party.Posture.isActiveAndEnabled && ui.Party.Posture.GetComponentInParent<Canvas>() != null &&
                        ui.Party.Posture.rectTransform.rect.width > 0, "Party status text has no renderable Canvas/rect.");
                    if (lethal) Check(f.Visuals.ActiveDefeatMarkers().Length == 1, "The lethal status item lost its independent X.");
                }
            }
            yield return null;
        }
    }

    internal sealed class Surfaces
    {
        internal readonly Widgets Party, Enemy, World;
        internal readonly BattlefieldEnemyView EnemyView;
        internal readonly TMP_Text EnemyHp;
        internal Surfaces(DRPGBasicAttackProjectionProbe.Fixture d)
        {
            var uiRoot = Child("WidgetCanvas", d.Root.transform);
            uiRoot.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var partyView = Child("PartyMember", uiRoot.transform).AddComponent<BattlePartyMemberView>();
            ((RectTransform)partyView.transform).anchoredPosition = new Vector2(-300, -130);
            var hud = Child("PartyHud", uiRoot.transform).AddComponent<BattlePartyHud>();
            Set(hud, "memberViews", new List<BattlePartyMemberView> { partyView });
            Party = new Widgets(partyView.transform); SetOptional(partyView, "statusWidgets", Party.Component);
            EnemyView = Child("EnemyLeaf", uiRoot.transform).AddComponent<BattlefieldEnemyView>();
            EnemyHp = Label("Hp", EnemyView.transform); Set(EnemyView, "hpText", EnemyHp);
            Enemy = new Widgets(EnemyView.transform); SetOptional(EnemyView, "statusWidgets", Enemy.Component);
            var enemies = Child("EnemyRoster", uiRoot.transform).AddComponent<BattlefieldEnemyRosterView>();
            Set(enemies, "enemyViews", new List<BattlefieldEnemyView> { EnemyView });
            Set(d.Surface, "partyHud", hud); Set(d.Surface, "enemyRoster", enemies);
            var canvas = Child("WorldTargeting", uiRoot.transform);
            var canvasRect = (RectTransform)canvas.transform; canvasRect.anchorMin = Vector2.zero; canvasRect.anchorMax = Vector2.one; canvasRect.sizeDelta = Vector2.zero;
            var camera = Child("WidgetCamera", d.Root.transform).AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 5;
            var targeting = canvas.AddComponent<BattleWorldTargetingPresenter>();
            targeting.Configure(camera, d.Pool, (RectTransform)canvas.transform);
            d.Surface.ConfigureWorldPresentation(null, d.Roster, targeting);
            d.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(d.Session));
            var world = canvas.GetComponentInChildren<BattleWorldActorStatusView>();
            Check(world != null, "Missing real world status plate.");
            World = new Widgets(world.transform); SetOptional(world, "statusWidgets", World.Component);
            d.Surface.InstallProjectionGuard(d.Surface.ProjectionArbiter);
            d.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(d.Session));
        }
    }

    internal sealed class Widgets
    {
        internal readonly Component Component;
        internal readonly TMP_Text Posture;
        internal readonly UnityEngine.UI.Image Fill;
        internal readonly Transform Rows;
        internal Widgets(Transform owner)
        {
            var root = Child("SemanticWidgets", owner);
            Posture = Label("Posture", root.transform);
            Fill = Child("PostureFill", root.transform).AddComponent<UnityEngine.UI.Image>(); Fill.type = UnityEngine.UI.Image.Type.Filled;
            Fill.raycastTarget = false; Fill.rectTransform.anchoredPosition = new Vector2(0, -15); Fill.rectTransform.sizeDelta = new Vector2(180, 3);
            Rows = Child("StatusItems", root.transform).transform;
            ((RectTransform)Rows).anchoredPosition = new Vector2(0, -50);
            var layout = Rows.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth = false; layout.childControlHeight = false; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var template = Child("StatusTemplate", root.transform); template.SetActive(false);
            var name = Label("Name", template.transform); var duration = Label("Duration", template.transform);
            name.rectTransform.anchoredPosition = new Vector2(-15, 0); name.rectTransform.sizeDelta = new Vector2(150, 24);
            duration.rectTransform.anchoredPosition = new Vector2(80, 0); duration.rectTransform.sizeDelta = new Vector2(20, 24);
            var icon = Child("Icon", template.transform).AddComponent<UnityEngine.UI.Image>(); icon.raycastTarget = false;
            var assembly = typeof(BattlePartyMemberView).Assembly;
            var itemType = assembly.GetType("BattleStatusInstanceView");
            var widgetType = assembly.GetType("BattleParticipantStatusWidgets");
            if (itemType == null || widgetType == null) return; // Real pre-implementation UI remains blank: behavioral RED.
            var item = template.AddComponent(itemType); Set(item, "displayName", name); Set(item, "duration", duration); Set(item, "icon", icon);
            Component = root.AddComponent(widgetType);
            Set(Component, "postureText", Posture); Set(Component, "postureFill", Fill); Set(Component, "statusRoot", Rows); Set(Component, "statusTemplate", item);
        }
        internal void AssertRows(params string[] expected)
        {
            var rows = new List<string>();
            foreach (Transform child in Rows)
                if (child.gameObject.activeSelf) rows.Add(child.Find("Name").GetComponent<TMP_Text>().text + "|" + child.Find("Duration").GetComponent<TMP_Text>().text);
            Check(rows.Count == expected.Length, "Visible status instance count differs: " + string.Join(",", rows));
            for (int i = 0; i < expected.Length; i++) Check(rows[i] == expected[i], "Wrong current status row: " + rows[i] + " expected " + expected[i]);
        }
    }
    private static GameObject Child(string name, Transform owner)
    { var result = new GameObject(name, typeof(RectTransform)); result.transform.SetParent(owner, false); ((RectTransform)result.transform).sizeDelta = new Vector2(180, 24); return result; }
    private static TMP_Text Label(string name, Transform owner)
    { var text = Child(name, owner).AddComponent<TextMeshProUGUI>(); text.raycastTarget = false; text.fontSize = 14; return text; }
    private static void Set(object target, string field, object value) => DRPGBasicAttackPreludeProbe.Set(target, field, value);
    private static void SetOptional(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
