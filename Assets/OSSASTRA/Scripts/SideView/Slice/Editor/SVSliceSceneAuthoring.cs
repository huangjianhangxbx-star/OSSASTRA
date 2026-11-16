/*
 * Reproducible scene authoring for the approved side-view route.
 * Execute through Unity Editor with all other scenes saved.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.Inventory;
using Ossastra.SideViewWorld;
using Ossastra.WorldProps;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Ossastra.SideViewWorld.SideViewTransitionCatalog;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.Slice
{
    public static partial class SVSliceSceneAuthoring
    {
        private const string SliceFolder = "Assets/OSSASTRA/SideView/Slice";
        private const string CameraProfilePath = SliceFolder + "/CameraProfile_SVSliceTower.asset";
        private const string BossId = "slice-boss-mossbone-mother";
        private const string RestAnchor = "tower-high";
        private const string AuthoredRoot = "SV-SLICE-01";
        private const float GroundedOffset = .055f;

        [MenuItem("OSSASTRA/Side View Slice/Assemble Tower Route")]
        public static void Assemble()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在 Edit mode 装配横版路线。");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("存在未保存的场景：" + SceneManager.GetSceneAt(i).path);
            var activePath = SceneManager.GetActiveScene().path;
            EnsureFolder("Assets/OSSASTRA/SideView", "Slice");
            EnsureFolder(SliceFolder, "GeneratedArt");
            var profile = GetCameraProfile();
            var paths = new[] { TowerRest, TowerLift, TowerCombat, TowerBoss };
            var optionalArt = LoadLocalTowerArt();
            foreach (var path in paths) AssembleTowerRoom(path, profile, optionalArt);
            PatchOpening(Tut01);
            PatchOpening(Tut02);
            IncludeScenesInBuild(paths.Append(Tut01).Append(Tut02));
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(activePath)) EditorSceneManager.OpenScene(activePath);
            Debug.Log("[SV-SLICE-01] 装配完成：两张开场与四张塔区，待 Play 验证。");
        }

        private static void AssembleTowerRoom(string path, SideViewCameraProfile profile, LocalTowerArt art)
        {
            // Unity copies the hierarchy and fixes all scene-internal references.
            var template = EditorSceneManager.OpenScene(Tut02);
            if (!EditorSceneManager.SaveScene(template, path, true))
                throw new InvalidOperationException("无法复制横版会话模板：" + path);
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var gate in Components<SideViewTransitionGate>(scene))
                Object.DestroyImmediate(gate.gameObject);
            foreach (var view in Components<SideViewTransitionDebugView>(scene))
                view.gameObject.SetActive(false);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == AuthoredRoot)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                if (IsOldDressing(root.name)) root.SetActive(false);
                if (root.name == "atmosphere")
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
            }
            var context = new RoomContext(scene, MapIdForScene(path));
            var deck = path == TowerRest ? ConfigureRest(context, art) :
                path == TowerLift ? ConfigureLift(context, art) :
                path == TowerCombat ? ConfigureCombat(context, art) :
                ConfigureBoss(context, art);
            SetCameraProfile(context, profile);
            WireComparison(context, deck);
            EditorSceneManager.SaveScene(scene);
        }

        private static bool IsOldDressing(string name)
        {
            return name == "background" || name == "foreground" || name == "core" ||
                name == "decor" || name.StartsWith("Tut_02 入口地形", StringComparison.Ordinal);
        }

        private static RoomDeck ConfigureRest(RoomContext context, LocalTowerArt art)
        {
            var deck = new RoomDeck(context.Root, 39, 0f, art);
            deck.FillArchitecture();
            deck.Platform("休息房地面", -19.5f, 19.5f, 0);
            deck.EndWall(-19.5f, 0);
            deck.EndWall(19.5f, 0);
            Gate(context, TowerRestEntry, Tut02, Tut02Tower, -17.8f, 0, 1);
            Gate(context, TowerRestExit, TowerLift, TowerLiftUpper, 17.8f, 0, -1);
            var arrival = Anchor(context, "door-safe", -14.7f, -13.15f, 0, 1);
            var bench = Anchor(context, RestAnchor, -.25f, -1.8f, 0, 1);
            DefaultParty(context, arrival);
            var progress = ConfigureProgress(context, arrival, bench);
            AddRestPoint(context, deck, progress);
            return deck;
        }

        private static RoomDeck ConfigureLift(RoomContext context, LocalTowerArt art)
        {
            const float lowerY = -10.8f;
            var deck = new RoomDeck(context.Root, 39f, lowerY * .45f, art);
            deck.FillArchitecture();
            deck.Platform("上层前厅", -19.5f, -2.15f, 0);
            deck.Platform("下层出口", 2.15f, 19.5f, lowerY);
            deck.Platform("井底安全台", -2.3f, 2.3f, lowerY - .02f);
            deck.Wall("上层右侧井壁", new Vector3(2.5f, 4f, 0), new Vector3(.6f, 8.1f, 3f));
            deck.Wall("下层左侧井壁", new Vector3(-2.45f, -5.4f, 0), new Vector3(.6f, 10.5f, 3f));
            deck.EndWall(-19.5f, 0);
            deck.EndWall(19.5f, lowerY);
            Gate(context, TowerLiftUpper, TowerRest, TowerRestExit, -17.8f, 0, 1);
            var exit = Gate(context, TowerLiftLower, TowerCombat, TowerCombatEntry, 17.8f, lowerY, -1);
            var upper = NewChild(context.Root, "升降机上站", new Vector3(0f, 0f, 0f));
            var lower = NewChild(context.Root, "升降机下站", new Vector3(0f, lowerY, 0f));
            var car = deck.ElevatorCar(upper.position);
            var elevator = NewChild(context.Root, "升降机启动", upper.position).gameObject.AddComponent<SVSliceElevator>();
            elevator.party = context.Party;
            elevator.car = car;
            elevator.upperStop = upper;
            elevator.lowerStop = lower;
            elevator.lowerGate = exit;
            elevator.lowerArrival = exit.GetComponent<SideViewTransitionArrival>();
            var upperCall = NewChild(context.Root, "上层升降机呼叫", new Vector3(-3.45f, .2f, 0f)).gameObject.AddComponent<SVSliceElevatorCall>();
            upperCall.elevator = elevator;
            var lowerCall = NewChild(context.Root, "下层升降机呼叫", new Vector3(3.45f, lowerY + .2f, 0f)).gameObject.AddComponent<SVSliceElevatorCall>();
            lowerCall.elevator = elevator;
            lowerCall.lowerLanding = true;
            SetTargets(context, elevator, upperCall, lowerCall);
            var entry = Anchor(context, "upper-safe", -14.7f, -13.15f, 0, 1);
            DefaultParty(context, entry);
            ConfigureProgress(context, entry);
            return deck;
        }

        private static RoomDeck ConfigureCombat(RoomContext context, LocalTowerArt art)
        {
            var deck = new RoomDeck(context.Root, 69f, 0f, art);
            deck.FillArchitecture();
            deck.Platform("锁之塔战区", -34.5f, 34.5f, 0);
            deck.EndWall(-34.5f, 0);
            deck.EndWall(34.5f, 0);
            deck.Crate(new Vector3(13f, 0, 0));
            deck.Crate(new Vector3(15.1f, 0, 0));
            Gate(context, TowerCombatEntry, TowerLift, TowerLiftLower, -32.8f, 0, 1);
            Gate(context, TowerCombatExit, TowerBoss, TowerBossEntry, 32.8f, 0, -1);
            Spawn(context, "tower-ss-01", "SS-N01", -21.5f, 0, false);
            Spawn(context, "tower-gang-01", "EM-e0161", -10f, 0, false);
            Spawn(context, "tower-mimic-01", "EM-e0100", 14.8f, 0, false);
            Spawn(context, "tower-ss-02", "SS-N02", 24.5f, 0, false);
            var entry = Anchor(context, "west-safe", -29.7f, -28.4f, 0, 1);
            DefaultParty(context, entry);
            ConfigureProgress(context, entry);
            return deck;
        }

        private static RoomDeck ConfigureBoss(RoomContext context, LocalTowerArt art)
        {
            var deck = new RoomDeck(context.Root, 42f, 0f, art);
            deck.FillArchitecture();
            deck.Platform("首领场地", -21, 21, 0);
            deck.EndWall(-21f, 0);
            deck.EndWall(21f, 0);
            Gate(context, TowerBossEntry, TowerCombat, TowerCombatExit, -19.3f, 0, 1);
            Spawn(context, BossId, "SS-B01", 8f, 0, true);
            var entry = Anchor(context, "arena-entrance-safe", -16.2f, -14.65f, 0, 1);
            DefaultParty(context, entry);
            ConfigureProgress(context, entry);
            return deck;
        }

        private static void PatchOpening(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == AuthoredRoot);
            if (existing != null) Object.DestroyImmediate(existing);
            var context = new RoomContext(scene, MapIdForScene(path));
            var defaultFirst = context.Party.Characters[0].transform.position;
            var defaultSecond = context.Party.Characters[1].transform.position;
            var anchorId = SliceSceneCatalog.OpeningAnchorId;
            var anchor = Anchor(context, anchorId, defaultFirst.x, defaultSecond.x, defaultFirst.y - GroundedOffset, path == Tut02 ? -1 : 1);
            anchor.secondActorPosition = defaultSecond;
            if (path == Tut02)
            {
                Gate(context, Tut02Tower, TowerRest, TowerRestEntry, 129.4f, 10f, 1);
                Spawn(context, "tut-02-ss-03", "SS-N03", 137f, 10f, false);
                var es = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "EsVisualReference");
                if (es != null) es.transform.position = new Vector3(140f, 10.05f, 0f);
            }
            if (path == Tut01)
            {
                var fromTut02 = Components<SideViewTransitionArrival>(scene).Single(a => a.gateId == Tut01Left3);
                var returnAnchor = Anchor(context, "tut01-return-from-02",
                    fromTut02.firstActorPosition.x, fromTut02.secondActorPosition.x,
                    fromTut02.firstActorPosition.y - GroundedOffset, 1);
                returnAnchor.secondActorPosition = fromTut02.secondActorPosition;
                ConfigureProgress(context, anchor, returnAnchor);
            }
            else ConfigureProgress(context, anchor);
            EnableRenderControls(context);
            EditorSceneManager.SaveScene(scene);
        }

        private static SideViewTransitionGate Gate(RoomContext context, string id, string destination, string target,
            float x, float floor, int intoRoom)
        {
            var host = NewChild(context.Root, id, new Vector3(x, floor + 1.42f, 0)).gameObject;
            var gate = host.AddComponent<SideViewTransitionGate>();
            var trigger = host.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(.8f, 2.85f, 3f);
            var body = host.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            gate.gateId = id;
            gate.destinationScenePath = destination;
            gate.destinationGateId = target;
            gate.commerce = context.Commerce;
            gate.inventory = context.Commerce.inventory;
            gate.party = context.Party;
            var arrival = host.AddComponent<SideViewTransitionArrival>();
            arrival.gateId = id;
            arrival.party = context.Party;
            arrival.firstActorPosition = GroundedAt(x + intoRoom * 3.1f, floor);
            arrival.secondActorPosition = GroundedAt(x + intoRoom * 4.65f, floor);
            if (!IsRoute(context.Scene.path, id, destination, target))
                throw new InvalidOperationException("作者路由与实际地图不匹配：" + id);
            return gate;
        }

        private static SideViewSliceAnchor Anchor(RoomContext context, string id, float first, float second, float floor, int facing)
        {
            var value = NewChild(context.Root, id, GroundedAt(first, floor)).gameObject.AddComponent<SideViewSliceAnchor>();
            value.anchorId = id;
            value.firstActorPosition = GroundedAt(first, floor);
            value.secondActorPosition = GroundedAt(second, floor);
            value.facingX = facing;
            return value;
        }

        private static SideViewSliceProgress ConfigureProgress(RoomContext context, SideViewSliceAnchor first, params SideViewSliceAnchor[] extra)
        {
            var progress = context.Root.gameObject.AddComponent<SideViewSliceProgress>();
            progress.commerce = context.Commerce;
            progress.party = context.Party;
            progress.mapId = context.MapId;
            progress.initialAnchorId = first.anchorId;
            progress.anchors = new[] { first }.Concat(extra).ToArray();
            progress.bossId = BossId;
            return progress;
        }

        private static void Spawn(RoomContext context, string id, string candidate, float x, float floor, bool boss)
        {
            var marker = NewChild(context.Root, id, GroundedAt(x, floor)).gameObject.AddComponent<SVSliceEnemySpawn>();
            marker.stableId = id;
            marker.candidateId = candidate;
            marker.isBoss = boss;
        }

        private static void DefaultParty(RoomContext context, SideViewSliceAnchor anchor)
        {
            context.Party.Characters[0].transform.position = anchor.firstActorPosition;
            context.Party.Characters[1].transform.position = anchor.secondActorPosition;
        }

        private static void SetTargets(RoomContext context, params SVSliceWorldInteractable[] targets)
        {
            var interaction = MustFind<InventoryInteraction>(context.Scene);
            var serialized = new SerializedObject(interaction);
            var list = serialized.FindProperty("sliceTargets");
            if (list == null) throw new InvalidOperationException("缺少 InventoryInteraction.sliceTargets 接线。");
            list.arraySize = targets.Length;
            for (var i = 0; i < targets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddRestPoint(RoomContext context, RoomDeck deck, SideViewSliceProgress progress)
        {
            deck.Bench();
            var point = NewChild(context.Root, "破落的房间 · 保存椅", new Vector3(0f, .2f, 0f)).gameObject.AddComponent<SVSliceRestPoint>();
            point.progress = progress;
            point.commerce = context.Commerce;
            point.party = context.Party;
            point.anchorId = RestAnchor;
            point.seat = NewChild(context.Root, "入座脚点", GroundedAt(-.25f, 0));
            BuildRestMenu(point);
            SetTargets(context, point);
        }

        private static void SetCameraProfile(RoomContext context, SideViewCameraProfile profile)
        {
            var follow = MustFind<SideViewCameraFollow>(context.Scene);
            var so = new SerializedObject(follow);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SideViewCameraProfile GetCameraProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SideViewCameraProfile>(CameraProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SideViewCameraProfile>();
                AssetDatabase.CreateAsset(profile, CameraProfilePath);
            }
            var so = new SerializedObject(profile);
            so.FindProperty("worldBounds").rectValue = new Rect(-42, -17f, 84f, 29f);
            so.FindProperty("verticalOffset").floatValue = 4.0f;
            so.FindProperty("fallingSpeedThreshold").floatValue = 16f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void IncludeScenesInBuild(IEnumerable<string> paths)
        {
            var wanted = new HashSet<string>(paths, StringComparer.Ordinal);
            var entries = EditorBuildSettings.scenes.ToList();
            for (var i = 0; i < entries.Count; i++)
                if (wanted.Remove(entries[i].path)) entries[i] = new EditorBuildSettingsScene(entries[i].path, true);
            entries.AddRange(wanted.Select(path => new EditorBuildSettingsScene(path, true)));
            EditorBuildSettings.scenes = entries.ToArray();
        }

        private static IEnumerable<T> Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T MustFind<T>(Scene scene) where T : Component
            => Components<T>(scene).SingleOrDefault() ?? throw new InvalidOperationException(scene.path + " 缺少 " + typeof(T).Name);

        private static Vector3 GroundedAt(float x, float floor) => new Vector3(x, floor + GroundedOffset, 0);

        private static Transform NewChild(Transform parent, string name, Vector3 localPosition)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPosition;
            return host.transform;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class RoomContext
        {
            public readonly Scene Scene;
            public readonly string MapId;
            public readonly Transform Root;
            public readonly CharacterControlCoordinator Party;
            public readonly CommerceService Commerce;

            public RoomContext(Scene scene, string mapId)
            {
                Scene = scene;
                MapId = mapId;
                Root = new GameObject(AuthoredRoot).transform;
                SceneManager.MoveGameObjectToScene(Root.gameObject, scene);
                Party = MustFind<CharacterControlCoordinator>(scene);
                Commerce = MustFind<CommerceService>(scene);
                Commerce.mapId = mapId;
                MustFind<WorldPropsRoot>(scene).mapId = mapId;
            }
        }
    }
}