/*
 * Batch Play smoke for the normal physical transition path and platform contact.
 * Run separately from authoring; this test owns a disposable validation save directory.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Commerce.Runtime;
using Ossastra.SideViewWorld;
using Ossastra.WorldProps;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using static Ossastra.SideViewWorld.SideViewTransitionCatalog;

namespace Ossastra.SideView.Slice
{
    [InitializeOnLoad]
    public static class SVSliceRouteSmoke
    {
        private const string Armed = "SVSliceRouteSmoke.Armed";
        private const string Result = "SVSliceRouteSmoke.Result";
        private static readonly Stack<IEnumerator> Work = new();
        private static readonly HashSet<string> Rendered = new(StringComparer.Ordinal);

        static SVSliceRouteSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Pump;
        }

        public static void Run()
        {
            try
            {
                AuditAuthoring();
                var title = SliceSceneCatalog.TitleScenePath;
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(title) != null, "标题场不存在：" + title);
                EditorSceneManager.OpenScene(title, OpenSceneMode.Single);
                SessionState.SetBool(Armed, true);
                SessionState.SetInt(Result, 1);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void AuditAuthoring()
        {
            var paths = new[] { Tut01, Tut02, TowerRest, TowerLift, TowerCombat, TowerBoss };
            var registered = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToHashSet(StringComparer.Ordinal);
            Require(registered.Contains(SliceSceneCatalog.TitleScenePath), "Build Settings 未启用标题");
            var destinations = new List<(string scene, string gate, string target, string targetGate)>();
            foreach (var path in paths)
            {
                Require(registered.Contains(path), "Build Settings 未启用 " + path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var progress = Component<SideViewSliceProgress>(scene);
                Require(progress.mapId == MapIdForScene(path) && progress.HasAnchor(progress.initialAnchorId),
                    "进度身份、稳定锚点不匹配：" + path);
                Require(progress.commerce != null && progress.party != null &&
                    progress.anchors.All(anchor => anchor != null && HasSolidGround(anchor)),
                    "共同状态或安全脚点未绑定：" + path);
                foreach (var gate in Components<SideViewTransitionGate>(scene))
                {
                    var arrival = gate.GetComponent<SideViewTransitionArrival>();
                    Require(gate.TryGetComponent<BoxCollider>(out var trigger) && trigger.isTrigger &&
                        arrival != null && arrival.gateId == gate.gateId,
                        "实体门缺少触发器或落点：" + gate.gateId);
                    Require(IsRoute(path, gate.gateId, gate.destinationScenePath, gate.destinationGateId),
                        "目录未登记实体门：" + gate.gateId);
                    destinations.Add((path, gate.gateId, gate.destinationScenePath, gate.destinationGateId));
                }
            }
            Require(destinations.Count == 10, "应存在 10 个实体门，得到 " + destinations.Count);
            foreach (var link in destinations)
                Require(destinations.Contains((link.target, link.targetGate, link.scene, link.gate)),
                    "反向门缺失：" + link.gate);
            Debug.Log("[SVSliceRouteSmoke] 六场、十门及共同进度脚点读回通过");
        }

        private static bool HasSolidGround(SideViewSliceAnchor anchor)
        {
            Physics.SyncTransforms();
            return Lands(anchor.firstActorPosition) && Lands(anchor.secondActorPosition);

            static bool Lands(Vector3 feet)
                => Physics.Raycast(feet + Vector3.up * .30f, Vector3.down, .75f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Armed, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Work.Push(Play());
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var code = SessionState.GetInt(Result, 1);
                SessionState.SetBool(Armed, false);
                if (Application.isBatchMode) EditorApplication.Exit(code);
            }
        }

        private static void Pump()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Armed, false) || Work.Count == 0) return;
            try
            {
                var current = Work.Peek();
                if (!current.MoveNext()) Work.Pop();
                else if (current.Current is IEnumerator nested) Work.Push(nested);
                if (Work.Count == 0) Finish(true);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(false);
            }
        }

        private static void Finish(bool ok)
        {
            while (Work.Count > 0)
                if (Work.Pop() is IDisposable iterator) iterator.Dispose();
            SessionState.SetInt(Result, ok ? 0 : 1);
            Debug.Log("[SVSliceRouteSmoke] " + (ok ? "PASS" : "FAIL"));
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator Play()
        {
            Rendered.Clear();
            var flow = SideViewSliceSessionFlow.Instance;
            Require(flow != null, "标题未启动会话");
            var tempDir = Path.Combine(Path.GetTempPath(), "OSSASTRA-SVSlice-Route-" + Guid.NewGuid().ToString("N"));
            Require(flow.ConfigureValidationDirectory(tempDir), "不能启用一次性验证档位");
            var slot = flow.InspectSlots().First();
            Require(flow.StartNew(slot.ProfileId), "新档启动失败：" + flow.Status);
            yield return AwaitMap(Tut01);
            yield return PhysicalDoor(Tut01Left3, Tut02);
            yield return PhysicalDoor(Tut02Right2, Tut01);
            yield return PhysicalDoor(Tut01Left3, Tut02);
            yield return PhysicalDoor(Tut02Tower, TowerRest);
            var progress = Component<SideViewSliceProgress>(SceneManager.GetActiveScene());
            Require(progress.HasAnchor("tower-high"), "塔区休息房缺稳定检查点");
            yield return VerifyRest();
            yield return PhysicalDoor(TowerRestExit, TowerLift);
            yield return VerifyElevator();
            yield return PhysicalDoor(TowerLiftLower, TowerCombat);
            yield return PhysicalDoor(TowerCombatExit, TowerBoss);
            yield return PhysicalDoor(TowerBossEntry, TowerCombat);
            yield return PhysicalDoor(TowerCombatEntry, TowerLift);
            var elevator = Component<SVSliceElevator>(SceneManager.GetActiveScene());
            Require(elevator.AtLower && !elevator.Moving, "从战区回升降机没有停靠下站");
            Debug.Log("[SVSliceRouteSmoke] 六场共同保存与物理门往返通过；测试档 " + tempDir);
        }

        private static IEnumerator VerifyRest()
        {
            var rest = Component<SVSliceRestPoint>(SceneManager.GetActiveScene());
            var actor = rest.party.Current;
            Set(actor.GetComponent<CharacterMovement>(), rest.seat.position);
            yield return WaitFor(() => rest.IsAccessible(actor), "到了休息椅仍无法交互");
            rest.Use(actor);
            Require(rest.panel.activeInHierarchy, "休息交互未打开菜单");
            rest.saveButton.onClick.Invoke();
            yield return WaitFor(() => rest.saveButton.interactable && rest.statusLabel.text != "正在保存…",
                "保存椅等待按钮恢复超时");
            var status = rest.commerce.SaveStatus;
            Require(rest.statusLabel.text == "已保存" && rest.progress.CheckpointAnchorId == rest.anchorId,
                $"保存椅结果异常，UI={rest.statusLabel.text}，Progress={rest.progress.Diagnostic}，" +
                $"Checkpoint={rest.progress.CheckpointAnchorId}，Save={status?.PersistedRevision}/{status?.StateRevision}，Error={status?.LastError}");
            rest.leaveButton.onClick.Invoke();
            Require(!rest.panel.activeInHierarchy, "出发未关闭休息菜单");
            Debug.Log("[SVSliceRouteSmoke] 保存椅写盘及出发通过");
        }

        private static IEnumerator VerifyElevator()
        {
            var scene = SceneManager.GetActiveScene();
            var elevator = Component<SVSliceElevator>(scene);
            var movement = elevator.party.Characters.Select(actor => actor.GetComponent<CharacterMovement>()).ToArray();
            var trigger = elevator.lowerGate.GetComponent<BoxCollider>();
            Require(!elevator.AtLower && !trigger.enabled, "初次从上层进入时下出口未阻挡");
            var upper = elevator.upperStop.position;
            var lower = elevator.lowerStop.position;
            Set(movement[0], upper + new Vector3(-.76f, 0, 0));
            Set(movement[1], upper + new Vector3(.76f, 0, 0));
            yield return WaitFor(() => movement.All(actor => actor.Grounded), "双人未能站稳上层轿厢");
            Require(elevator.IsAccessible(elevator.party.Current), "站上轿厢仍不可交互");
            elevator.Use(elevator.party.Current);
            yield return WaitFor(() => elevator.Moving && elevator.car.position.y < upper.y - 1f, "下行未启动");
            var previous = elevator.party.Current;
            Require(elevator.party.SwitchNext() && previous != elevator.party.Current, "轿厢下行时切主控失败");
            yield return WaitFor(() => !elevator.Moving && elevator.AtLower, "下行未停靠");
            Require(trigger.enabled && movement.All(actor => Mathf.Abs(actor.transform.position.y - lower.y) < .32f),
                "下行结束没有带到双角色或解锁出口");

            var controlled = elevator.party.Current.GetComponent<CharacterMovement>();
            var standing = movement.Single(actor => actor != controlled);
            Set(controlled, lower + new Vector3(.65f, 0, 0));
            Set(standing, lower + new Vector3(5.4f, 0, 0));
            yield return WaitFor(() => controlled.Grounded && standing.Grounded, "上行前轿厢及月台站立失败");
            elevator.Use(elevator.party.Current);
            yield return WaitFor(() => !elevator.Moving && !elevator.AtLower, "上行未停靠");
            Require(Mathf.Abs(controlled.transform.position.y - upper.y) < .32f && Mathf.Abs(standing.transform.position.y - lower.y) < .32f,
                "单乘客上行未被搬运，或站在下站的角色被错误搬运");

            Set(controlled, upper + new Vector3(.65f, 0, 0));
            yield return WaitFor(() => controlled.Grounded, "再次下行前轿厢站立失败");
            elevator.Use(elevator.party.Current);
            yield return WaitFor(() => !elevator.Moving && elevator.AtLower, "再次下行未停靠");
            Debug.Log("[SVSliceRouteSmoke] 双人下行、中途切换、单人上行及下层门禁通过");
        }

        private static void Set(CharacterMovement actor, Vector3 feet)
        {
            actor.Teleport(feet + Vector3.up * .06f);
            Physics.SyncTransforms();
        }

        private static IEnumerator PhysicalDoor(string id, string destination)
        {
            var scene = SceneManager.GetActiveScene();
            var gate = Components<SideViewTransitionGate>(scene).Single(g => g.gateId == id);
            Set(gate.party.Current.GetComponent<CharacterMovement>(), gate.transform.position - Vector3.up * 1.42f);
            yield return AwaitMap(destination);
            var runtime = SideViewTransitionRuntime.Instance;
            Require(runtime.SourceHandle != runtime.DestinationHandle && runtime.DestinationHandle == SceneManager.GetActiveScene().handle,
                "物理门未发生独立 Scene 换区：" + id);
        }

        private static IEnumerator AwaitMap(string path)
        {
            yield return WaitFor(() =>
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != path || SideViewTransitionRuntime.Instance?.Busy == true) return false;
                return Components<SideViewSliceProgress>(scene).SingleOrDefault()?.Ready == true &&
                    Components<CommerceService>(scene).SingleOrDefault()?.Ready == true &&
                    Components<WorldPropsRoot>(scene).SingleOrDefault()?.Ready == true;
            }, "地图或聚合快照未就绪：" + path, 24d);
            Debug.Log("[SVSliceRouteSmoke] 进入 " + MapIdForScene(path));
            var renderPath = Environment.GetEnvironmentVariable("OSSASTRA_SVSLICE_RENDER_DIR");
            if (!string.IsNullOrWhiteSpace(renderPath) && Rendered.Add(path))
                yield return VerifyRender(path, renderPath);
        }

        private static IEnumerator VerifyRender(string path, string destination)
        {
            var scene = SceneManager.GetActiveScene();
            var comparison = Component<SideViewRenderComparison>(scene);
            var camera = comparison.GetComponent<Camera>();
            var ace = Component<SideViewAceLighting>(scene);
            var reference = Component<SideViewAceOriginalReference>(scene);
            var es = reference.GetComponent<MeshRenderer>();
            if (path == TowerRest)
            {
                var progress = Component<SideViewSliceProgress>(scene);
                var anchor = progress.anchors.Single(item => item.anchorId == "tower-high");
                Set(progress.party.Characters[0].GetComponent<CharacterMovement>(), anchor.firstActorPosition);
                Set(progress.party.Characters[1].GetComponent<CharacterMovement>(), anchor.secondActorPosition);
                yield return WaitFor(() => Mathf.Abs(camera.WorldToViewportPoint(anchor.firstActorPosition).x - .5f) < .13f,
                    "保存椅未进入相机中心");
            }
            var media = Path.Combine(Path.GetFullPath(destination), MapIdForScene(path));
            Directory.CreateDirectory(media);
            var baseline = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                comparison.SelectLegacy();
                yield return WaitFor(() => comparison.Active == SideViewRenderComparison.Recipe.Legacy,
                    "A 未就绪：" + path);
                var a = CaptureCamera(camera, media, "A");
                Require(es.enabled, "A 没有 ES：" + path);

                comparison.SelectSilksong();
                yield return WaitFor(() => comparison.Active == SideViewRenderComparison.Recipe.Silksong,
                    "B 未就绪：" + path);
                var b = CaptureCamera(camera, media, "B");
                Require(comparison.LastGradeFrame == Time.frameCount && es.enabled,
                    "B 尚无当前帧调色或 ES：" + path + " grade=" + comparison.LastGradeFrame);

                comparison.SelectAceFusion();
                comparison.SelectAce();
                yield return WaitFor(() => comparison.Active == SideViewRenderComparison.Recipe.Ace &&
                    comparison.ActiveAceMaterial == SideViewRenderComparison.AceCharacterMaterial.Fusion,
                    "C 融合未就绪：" + path);
                var c = CaptureCamera(camera, media, "C");
                Require(ace.EdgeMapsReady && es.enabled, "C 当前相机没有方向光图或 ES：" + path);
                Debug.Log("[SVSliceRouteSmoke] A/B/C 相机帧与 ES 已读回 " + MapIdForScene(path) +
                    "，PNG：" + string.Join(", ", a, b, c));
            }
            finally { Time.timeScale = baseline; }
        }

        private static string CaptureCamera(Camera camera, string folder, string mode)
        {
            const int width = 1600, height = 900;
            var priorActive = RenderTexture.active;
            var priorTarget = camera.targetTexture;
            var priorAspect = camera.aspect;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                camera.aspect = (float)width / height;
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                var filename = Path.Combine(folder, mode + ".png");
                var png = pixels.EncodeToPNG();
                if (png == null || png.Length == 0) throw new InvalidOperationException("未生成 " + mode + " 的相机画面");
                File.WriteAllBytes(filename, png);
                return filename;
            }
            finally
            {
                camera.targetTexture = priorTarget;
                camera.aspect = priorAspect;
                RenderTexture.active = priorActive;
                RenderTexture.ReleaseTemporary(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message, double seconds = 10d)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (!condition() && EditorApplication.timeSinceStartup < deadline)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Require(condition(), message + "；换区：" + SideViewTransitionRuntime.Instance?.Status);
        }

        private static T Component<T>(Scene scene) where T : Component => Components<T>(scene).Single();
        private static IEnumerable<T> Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(false));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}