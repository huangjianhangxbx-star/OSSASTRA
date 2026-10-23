#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DRPGInPlaceRegressionProbe
{
    public static string SpriteDefeatVacatesHomeBeforeReflow()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(true))
        {
            f.Defeated.ConfigureSpriteParty(f.Domain.Domain.Roster.ActiveViews);
            f.Hit();
            if (!f.Original.gameObject.activeInHierarchy || f.Visuals.ActiveDefeatMarkers().Length != 1)
                throw new InvalidOperationException("Death must retain body and X through impact.");
            f.RenderHome();
            var result = f.Adapter.CompleteDefeats(f.Departures);
            f.Advance(.01);
            if (f.Defeated.Count != 0 || f.Visuals.ActiveDefeatMarkers().Length != 0 || f.Original.gameObject.activeSelf)
                throw new InvalidOperationException("Dead Sprite/X still occupies the home released for survivor reflow.");
            if (result.Receipt.Completion.IsCompleted)
                throw new InvalidOperationException("Reflow was released before the empty home was rendered.");
            f.Advance(.01);
            if (!result.Receipt.Completion.IsCompleted || result.Receipt.Completion.Result.Kind != PresentationReceiptTerminalKind.Completed)
                throw new InvalidOperationException("Sprite departure did not finish after rendering the vacated home.");
        }
        return "PASS Sprite death retains X through return, then vacates home before reflow";
    }

    public static string StaleEnemyRepositionBecomesWait()
    {
        using(var f=new DRPGBasicAttackProjectionProbe.Fixture(false, enemyFirst:true))
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(DRPGBattleSession).GetField("currentEnemyPlan",flags).SetValue(f.Session,
                DRPGEnemyActionPlan.CreateReposition(1,f.Enemy.RuntimeId,0,1,DRPGFormationRow.Rear));
            var args=new object[]{f.Enemy,null};
            var materialize=typeof(DRPGBattleSession).GetMethod("TryMaterializeCurrentEnemyPlanCommand",flags);
            if(!(bool)materialize.Invoke(f.Session,args) || ((DRPGBattleCommand)args[1]).Kind!=DRPGBattleCommandKind.Wait)
                throw new InvalidOperationException("Invalid committed reposition is retried forever instead of consuming a wait turn.");
        }
        return "PASS stale enemy reposition resolves as wait";
    }

    public static string ConflictDoesNotTintInactivePool()
    {
        var root=new GameObject("ConflictPoolTest");
        try
        {
            var inactive=new GameObject("Pooled",typeof(SpriteRenderer));inactive.transform.SetParent(root.transform);inactive.SetActive(false);
            var tint=typeof(BattleStandardPresentationBinding).GetMethod("DimSpectators",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            if(tint==null)throw new InvalidOperationException("Formal conflict has no active-only tint boundary.");
            tint.Invoke(null,new object[]{root.transform,1,2});
            if(inactive.GetComponent<SpriteRenderer>().color!=Color.white)
                throw new InvalidOperationException("Conflict darkened an inactive pooled actor before its next wave.");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        return "PASS inactive pool retains authored color across conflicts";
    }

    public static string BorrowedReleaseRestoresFittedVisualPose()
    {
        var root = new GameObject("BorrowedVisualPose");
        try
        {
            var view=root.AddComponent<BattleWorldActorView>(); view.EnsureAnchors();
            var sprite=view.VisualRoot.gameObject.AddComponent<SpriteRenderer>();
            sprite.transform.localScale=Vector3.one*.27f; sprite.transform.localPosition=new Vector3(0,2.1f,0);
            using(var lease=new BattleBorrowedActorLease(view))
            { sprite.transform.localScale=Vector3.one; sprite.transform.localPosition=Vector3.zero; }
            if(Vector3.Distance(sprite.transform.localScale,Vector3.one*.27f)>.0001f ||
                Vector3.Distance(sprite.transform.localPosition,new Vector3(0,2.1f,0))>.0001f)
                throw new InvalidOperationException("Borrowed release lost the fitted exploration Sprite pose.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        return "PASS borrowed release restores fitted visual transform";
    }

    public static string BorrowedDefeatDoesNotReactivateOriginal()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(true, configureScene: d =>
        {
            d.Roster.Clear();
            var party = new GameObject("BorrowedParty").AddComponent<DRPGNodePartyRoot>();
            party.transform.SetParent(d.Root.transform);
            var member = new GameObject("BorrowedHero"); member.transform.SetParent(party.transform);
            var view = member.AddComponent<BattleWorldActorView>(); view.EnsureAnchors();
            var profile = new SerializedObject(d.Player.Profile); profile.FindProperty("combatantId").stringValue="borrowed-defeat"; profile.ApplyModifiedPropertiesWithoutUndo();
            member.AddComponent<DRPGScenePartyMember>().ConfigureForRuntime("borrowed-defeat", null, view);
            party.RebuildFromChildren(); d.Pool.ConfigureBorrowedParty(party);
            d.Roster.Bind(d.Initial.PartyMembers,d.Initial.Enemies,true);
        }))
        {
            f.Hit(); f.RenderHome(); var result=f.Adapter.CompleteDefeats(f.Departures); f.Advance(.01);
            if (f.Original.gameObject.activeSelf || !result.Receipt.Completion.IsCompleted ||
                result.Receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
                throw new InvalidOperationException("Borrowed departure resurrected the original alongside the incap model.");
        }
        return "PASS borrowed defeat hides original after independent incap transfer";
    }

    public static string FormalSpritePartyHasDefeatBindings()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            f.Pool.TryGet(f.Player.RuntimeId, out var playerView);
            var visual = new GameObject("AuthoredSprite", typeof(SpriteRenderer));
            visual.transform.SetParent(playerView.VisualRoot, false);
            visual.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/GameResources/DRPG/Art/Portraits/DRPG_Portrait_Protagonist_Broken.png");
            var roster = f.Root.AddComponent<BattleDefeatedActorRoster>();
            var prepare = typeof(BattleDefeatedActorRoster).GetMethod("ConfigureSpriteParty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (prepare == null) throw new InvalidOperationException("Formal Sprite party has no defeat-binding preparation.");
            prepare.Invoke(roster, new object[] { f.Roster.ActiveViews });
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var id = new ParticipantId(f.Player.RuntimeId);
                if (!roster.CanTransfer(id) || !roster.HasIndependentAnchor(id, lease))
                    throw new InvalidOperationException("Formal party cannot transfer its defeated Sprite after return.");
            }
        }
        return "PASS formal Sprite party prepares independent defeat bindings";
    }

    public static string PublishedNodeHomeSurvivesBattleExit()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            f.Roster.Clear();
            var partyObject = new GameObject("PersistentPartyHome"); partyObject.transform.SetParent(f.Root.transform);
            var party = partyObject.AddComponent<DRPGNodePartyRoot>();
            var memberObject = new GameObject("Hero"); memberObject.transform.SetParent(party.transform);
            var view = memberObject.AddComponent<BattleWorldActorView>(); view.EnsureAnchors();
            var profile = f.Player.Profile; var data = new SerializedObject(profile);
            data.FindProperty("combatantId").stringValue = "persistent-home"; data.ApplyModifiedPropertiesWithoutUndo();
            memberObject.AddComponent<DRPGScenePartyMember>().ConfigureForRuntime("persistent-home", null, view);
            party.RebuildFromChildren(); view.transform.position = new Vector3(23,4,2);
            f.Pool.ConfigureBorrowedParty(party); f.Roster.SetInPlaceSorting(true);
            f.Roster.Bind(f.Initial.PartyMembers, f.Initial.Enemies, true);
            using (var lease = f.Surface.AcquireProjectionLease(f.Owner))
            {
                var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
                var motion = new BattleActorMotionController(lease, clock);
                var target = new DRPGWorldFormationPlacement(f.Player.RuntimeId, DRPGFormationRow.Front, new Vector3(25,4,2), 1, 1, 300);
                var receipt = motion.MoveFormation(f.Owner.Action, target, 0);
                clock.Now = .31; clock.RenderedFrame = 1; motion.Tick();
                clock.Now = .32; clock.RenderedFrame = 2; motion.Tick();
                motion.SealFormationHomes(new[]{target});
                if (!receipt.Completion.IsCompleted) throw new InvalidOperationException("Formation did not complete.");
            }
            f.Roster.Clear();
            if ((view.transform.position - new Vector3(25,4,2)).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Borrowed lease discarded the published formation home on exit: " + view.transform.position);
            party.CaptureCommittedBattleState(f.Session);
            if ((view.transform.position - new Vector3(25,4,2)).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Node state capture re-laid out the published formation home.");
            return "PASS published node home survives pool release and battle state capture";
        }
    }

    public static string TargetProjectionSurvivesOffscreenReturn()
    {
        var root = new GameObject("OffscreenTargetRegression");
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(.5f,.5f));
        try
        {
            var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0,0,-10);
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform);
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            visual.transform.position = new Vector3(100,0,0);
            var proxy = new GameObject("Proxy", typeof(RectTransform), typeof(BattleActorScreenHitProxy), typeof(BattleActorScreenProjector));
            proxy.transform.SetParent(root.transform);
            var projector = proxy.GetComponent<BattleActorScreenProjector>();
            projector.Configure(camera, renderer, (RectTransform)proxy.transform);
            projector.SendMessage("LateUpdate");
            if (!projector.isActiveAndEnabled)
                throw new InvalidOperationException("Offscreen projection disabled its own updater; blood bar and target can never return.");
            var visibility = proxy.GetComponent<CanvasGroup>();
            if (visibility == null || visibility.alpha != 0 || visibility.blocksRaycasts)
                throw new InvalidOperationException("Offscreen target must be hidden and noninteractive.");
            visual.transform.position = Vector3.zero;
            projector.SendMessage("LateUpdate");
            if (visibility.alpha != 1 || !visibility.blocksRaycasts || !projector.isActiveAndEnabled)
                throw new InvalidOperationException("Returning onscreen did not restore target visibility and raycasts.");
            return "PASS offscreen target and status recover without disabling projection";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(sprite); }
    }

    public static string ExplorationCameraIgnoresUnboundCameraDrift()
    {
        var root = new GameObject("NodeCameraDriftRegression");
        try
        {
            var party = root.AddComponent<DRPGNodePartyRoot>();
            var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.transform.position = new Vector3(10, 4, -30);
            var context = root.AddComponent<DRPGNodeBattleContext>();
            context.ConfigureForRuntime("probe", DRPGNodeBattleMode.ExplorationEncounter, party, camera,
                root.transform, root.transform, new DRPGNodeBattleBounds(-100,100), 1, 1);
            camera.transform.position = new Vector3(10, -30, -30);
            if (Mathf.Abs(context.ExplorationCameraTarget.position.y - 4f) > .001f)
                throw new InvalidOperationException("Node camera inherited an unbound title camera's vertical drift.");
            return "PASS exploration camera uses authored vertical framing";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string EnemyRootMirrorsPartyOnSameGround()
    {
        var root = new GameObject("NodeCompositionRegression");
        try
        {
            var partyObject = new GameObject("PartyRoot"); partyObject.transform.SetParent(root.transform);
            var party = partyObject.AddComponent<DRPGNodePartyRoot>();
            party.transform.position = new Vector3(20, 4, 2);
            var cameraObject = new GameObject("SceneCamera"); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(25, 8, -30);
            var origin = new GameObject("Origin").transform; origin.SetParent(root.transform);
            var enemy = new GameObject("EnemyRoot").transform; enemy.SetParent(root.transform);
            var context = root.AddComponent<DRPGNodeBattleContext>();
            context.ConfigureForRuntime("probe", DRPGNodeBattleMode.ExplorationEncounter, party, camera,
                origin, enemy, new DRPGNodeBattleBounds(-100, 100), 1, 1);
            context.CaptureCurrentSceneComposition();
            if ((enemy.position - new Vector3(30, 4, 2)).sqrMagnitude > .000001f ||
                party.transform.position != new Vector3(20, 4, 2) || camera.transform.position != new Vector3(25, 8, -30))
                throw new InvalidOperationException("Enemy root must mirror the unchanged party about camera X on the same ground: " + enemy.position);
            return "PASS node enemy root mirrors party without moving camera or party";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string FormalEnemyHasSameBaseHeight()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/DRPG/Prefabs/PF_DRPGBattleWorldActor.prefab");
        var profile = AssetDatabase.LoadAssetAtPath<DRPGCombatantProfileSO>("Assets/GameResources/DRPG/Config/Test/DRPGCombatant_ankalute_enemy_sentinel.asset");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var view = instance.GetComponent<BattleWorldActorView>();
            var actor = new BattleParticipantRuntime(5002, new BattleParticipantSpec(profile, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
            view.Bind(actor.RuntimeId, new DRPGBattleParticipantViewModel(actor, 0, false));
            float height = view.GetComponentInChildren<SpriteRenderer>().bounds.size.y;
            if (Mathf.Abs(height - 4.2f) > .001f)
                throw new InvalidOperationException("Formal enemy base height differs from node party height 4.2: " + height);
            return "PASS formal enemy shares node party base height";
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    public static string HostUsesNodeEnemyRoot()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var root = new GameObject("NodeHostRegression");
            var world = new GameObject("NodeHostRegressionWorld");
            try
            {
                var pool = world.AddComponent<BattleWorldActorPool>();
                var ownedCamera = world.AddComponent<Camera>();
                var presenter = world.AddComponent<BattleCameraPresenter>();
                presenter.Configure(ownedCamera, world.transform);
                var roster = world.AddComponent<BattleWorldRosterPresenter>(); roster.Configure(pool, null, null);
                var host = root.AddComponent<BattleWorldRuntimeHost>();
                typeof(BattleWorldRuntimeHost).GetField("worldInstance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(host, world);
                var partyObject = new GameObject("Party"); partyObject.transform.SetParent(root.transform);
                var party = partyObject.AddComponent<DRPGNodePartyRoot>();
                var member = new GameObject("Member"); member.transform.SetParent(party.transform);
                member.AddComponent<DRPGScenePartyMember>().ConfigureForRuntime("hero", null, null); party.RebuildFromChildren();
                party.transform.position = new Vector3(20, 4, 2);
                var camera = root.AddComponent<Camera>(); camera.transform.position = new Vector3(25, 8, -30);
                var origin = new GameObject("Origin").transform; origin.SetParent(root.transform); origin.position = new Vector3(25, 4, 2);
                var enemy = new GameObject("EnemyRoot").transform; enemy.SetParent(root.transform); enemy.position = new Vector3(30, 4, 2);
                var context = root.AddComponent<DRPGNodeBattleContext>();
                context.ConfigureForRuntime("probe", DRPGNodeBattleMode.ExplorationEncounter, party, camera, origin, enemy, new DRPGNodeBattleBounds(-100, 100), 1, 1);
                host.BeginInPlaceBattle(context);
                foreach (bool orthographic in new[] { true, false })
                {
                    camera.orthographic = orthographic; camera.orthographicSize = 9f; camera.fieldOfView = 10f;
                    var before = presenter.Capture();
                    presenter.Play(new BattleCameraShotDefinition("legacy-special", new Vector3(0, 1, -3), Vector3.zero, 38f, 0), true);
                    if (camera.orthographic != orthographic || camera.transform.position != before.Position ||
                        camera.orthographicSize != 9f || camera.fieldOfView != 10f)
                        throw new InvalidOperationException("Legacy special shot changed borrowed node camera projection/framing.");
                }
                roster.Bind(null, f.Initial.Enemies, true);
                if (!pool.TryGet(f.Enemy.RuntimeId, out var view) || (view.transform.position - new Vector3(30, 4, 2)).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Host still used the independent stage origin instead of node enemy root.");
                host.EndInPlaceBattle();
                return "PASS runtime host binds the actual node enemy root";
            }
            finally { UnityEngine.Object.DestroyImmediate(root); if (world != null) UnityEngine.Object.DestroyImmediate(world); }
        }
    }

    public static string PreservePlaceholderVisualOnBind()
    {
        var marker = new GameObject("NodePlaceholderRegression");
        var profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), .1f);
        try
        {
            var data = new SerializedObject(profile);
            data.FindProperty("fullPortrait").objectReferenceValue = sprite;
            data.ApplyModifiedPropertiesWithoutUndo();
            var method = typeof(DRPGInPlaceBattleInstaller).GetMethod("BuildPersistentPlaceholder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var view = (BattleWorldActorView)method.Invoke(null, new object[] { marker.transform, profile });
            var renderer = view.GetComponentInChildren<SpriteRenderer>();
            Bounds before = renderer.bounds;
            var participant = new BattleParticipantRuntime(5001,
                new BattleParticipantSpec(profile, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
            view.Bind(5001, new DRPGBattleParticipantViewModel(participant, 0, true));
            if ((renderer.bounds.center - before.center).sqrMagnitude > .000001f ||
                (renderer.bounds.size - before.size).sqrMagnitude > .000001f)
                throw new InvalidOperationException("First battle bind changed the persistent preview's visible bounds.");
            return "PASS persistent sprite keeps visible bounds on battle bind";
        }
        finally { UnityEngine.Object.DestroyImmediate(marker); UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(sprite); }
    }

    public static string ConflictKeepsGroundPlane()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            var clock = new DRPGBasicAttackPreludeProbe.ProbeClock();
            var visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(f.Root, clock);
            var camera = visuals.Camera;
            camera.transform.position = new Vector3(25, 8, -30);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = false; camera.fieldOfView = 10;
            var host = f.Root.AddComponent<BattleWorldRuntimeHost>();
            typeof(BattleWorldRuntimeHost).GetField("sceneCameraOverride",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(host, camera);
            f.Surface.ConfigureWorldPresentation(host, f.Roster, null);
            f.Roster.SetInPlaceSorting(true);
            typeof(DRPGBattleUGUISurface).GetField("impactFeedbackPresenter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(f.Surface, visuals.Feedback);
            f.Pool.TryGet(f.Player.RuntimeId, out var actor);
            actor.transform.position = new Vector3(23, 4, 2);
            f.Target.transform.position = new Vector3(29, 4, 2);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab, f.Root.transform);
            var binding = instance.GetComponent<BattleStandardPresentationBinding>();
            var port = binding.CreateAdapter(f.Surface, clock, null);
            if (!port.TryCaptureFrozenSnapshot(new DRPGBasicAttackPresentationFreezeRequest(f.Session.BattleGeneration,
                new ParticipantId(f.Player.RuntimeId), new ParticipantId(f.Enemy.RuntimeId), "probe", "probe"), out var frozen))
                throw new InvalidOperationException("Actual conflict binding did not freeze.");
            if (Mathf.Abs(frozen.ActorConflictPosition.Y - 4) > .00001f ||
                Mathf.Abs(frozen.TargetConflictPosition.Y - 4) > .00001f ||
                Mathf.Abs(frozen.ActorConflictPosition.Z - 2) > .00001f)
                throw new InvalidOperationException("Conflict staging moved actors off their actual ground plane.");
            return "PASS conflict stages horizontally on the actual ground plane";
        }
    }

    public static string PreserveHomeAfterAction()
    {
        using (var f = new DRPGBasicAttackDefeatTransferProbe.Fixture(false, lethal: false,
            configureScene: domain =>
            {
                domain.Roster.SetInPlaceSorting(true);
                domain.Pool.TryGet(domain.Player.RuntimeId, out var actor);
                actor.transform.position = new Vector3(23f, 4f, 2f);
            }))
        {
            f.Hit(); f.RenderHome();
            f.Adapter.CompleteDefeats(f.Departures);
            if (!f.Domain.Prepared.TryReconcileFormationAfterReturn(out var rules, out string error))
                throw new InvalidOperationException(error);
            var receipt = f.Adapter.ReconcileFormation(rules);
            f.Advance(.31d); f.Advance(.01d);
            f.Domain.Domain.Pool.TryGet(f.Domain.Domain.Player.RuntimeId, out var actor);
            if ((actor.transform.position - new Vector3(23f, 4f, 2f)).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Post-action formation replaced the actual node home: " + actor.transform.position);
            if (!receipt.Completion.IsCompleted || receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted)
                throw new InvalidOperationException("Node home reconciliation failed.");
            return "PASS unchanged formation preserves node home after action";
        }
    }

    public static string RejectForeignCharacter()
    {
        var root = new GameObject("InPlaceIdentityRegression");
        root.SetActive(false);
        try
        {
            var party = root.AddComponent<DRPGNodePartyRoot>();
            var marker = new GameObject("ExplicitHeroSlot");
            marker.transform.SetParent(root.transform, false);
            var member = marker.AddComponent<DRPGScenePartyMember>();
            member.ConfigureForRuntime("hero", null, null);
            party.RebuildFromChildren();
            var context = root.AddComponent<DRPGNodeBattleContext>();
            context.ConfigureForRuntime("probe", DRPGNodeBattleMode.ExplorationEncounter,
                party, null, root.transform, root.transform,
                new DRPGNodeBattleBounds(-100, 100), 1, 1);
            var data = new SerializedObject(context);
            data.FindProperty("protagonistStableId").stringValue = "hero";
            data.ApplyModifiedPropertiesWithoutUndo();
            var foreign = new GameObject("UnregisteredLegacyFPS");
            foreign.SetActive(false);
            foreign.transform.SetParent(root.transform, false);
            var character = foreign.AddComponent<CharacterRoot>();
            if (context.AttachExplorationProtagonist(character) || member.Character == character ||
                character.GetComponent<BattleWorldActorView>() != null)
                throw new InvalidOperationException("Unregistered legacy CharacterRoot was adopted as hero.");
            return "PASS foreign character is not a party member";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string PreserveBorrowedHome()
    {
        using (var f = new DRPGBasicAttackProjectionProbe.Fixture(false))
        {
            f.Roster.Clear();
            var partyObject = new GameObject("NodeParty");
            partyObject.transform.SetParent(f.Root.transform, false);
            var party = partyObject.AddComponent<DRPGNodePartyRoot>();
            var marker = new GameObject("ExplicitHero");
            marker.transform.SetParent(partyObject.transform, false);
            var view = marker.AddComponent<BattleWorldActorView>();
            view.EnsureAnchors();
            var profile = f.Initial.PartyMembers[0].Profile;
            var data = new SerializedObject(profile);
            data.FindProperty("combatantId").stringValue = "home-regression-hero";
            data.ApplyModifiedPropertiesWithoutUndo();
            marker.AddComponent<DRPGScenePartyMember>().ConfigureForRuntime(profile.CombatantId, null, view);
            party.RebuildFromChildren();
            marker.transform.position = new Vector3(23f, 4f, 2f);
            marker.transform.localScale = Vector3.one * 1.3f;
            f.Pool.ConfigureBorrowedParty(party);
            f.Roster.SetInPlaceSorting(true);
            f.Roster.Bind(f.Initial.PartyMembers, f.Initial.Enemies, true);
            if (!f.Pool.TryGet(f.Player.RuntimeId, out var bound) || bound != view)
                throw new InvalidOperationException("Battle replaced the node-owned party actor.");
            if ((view.transform.position - new Vector3(23f, 4f, 2f)).sqrMagnitude > .000001f ||
                (view.transform.localScale - Vector3.one * 1.3f).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Battle bind overwrote the exploration home or scale: " + view.transform.position);
            return "PASS borrowed actor retains exploration home and scale";
        }
    }
}
#endif
