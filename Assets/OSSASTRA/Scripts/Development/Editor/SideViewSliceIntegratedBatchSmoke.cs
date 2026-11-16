/*
 * Independent batch Play integration: physical route to TowerRest, service checkpoint flush,
 * title cold read, elevator/doors to Boss, lethal formal CombatMeleeHit, completion cold read.
 * -executeMethod without -quit; never uses the user's normal save directory.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Commerce.Runtime;
using Ossastra.Enemies;
using Ossastra.SideViewWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Ossastra.SideViewWorld.SideViewTransitionCatalog;

namespace Ossastra.SideView.Slice.Editor
{
    [InitializeOnLoad]
    public static class SideViewSliceIntegratedBatchSmoke
    {
        private const string Prefix = "SV-SLICE-01.IntegratedBatch.";
        private const string Armed = Prefix + "Armed";
        private const string Started = Prefix + "Started";
        private const string ExitCode = Prefix + "ExitCode";
        private const string Deadline = Prefix + "DeadlineTicks";
        private const string Slot = "slice-slot-01";
        private static readonly Stack<IEnumerator> Work = new Stack<IEnumerator>();
        private static string testDirectory;

        static SideViewSliceIntegratedBatchSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Pump;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("集成 smoke 仅在 -batchmode 执行，避免关闭交互编辑器。");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("必须从 Edit 模式启动集成 smoke。");
                for (int i = 0; i < SceneManager.sceneCount; ++i)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("存在未保存场景：" + SceneManager.GetSceneAt(i).path);
                var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToHashSet(StringComparer.Ordinal);
                Require(enabled.Contains(SliceSceneCatalog.TitleScenePath), "Build Settings 缺独立标题");
                foreach (var scene in SliceSceneCatalog.GameplayScenes)
                    Require(enabled.Contains(scene.Path), "Build Settings 缺地图：" + scene.Path);
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SliceSceneCatalog.TitleScenePath) != null,
                    "独立标题场景未装配");
                EditorSceneManager.OpenScene(SliceSceneCatalog.TitleScenePath, OpenSceneMode.Single);
                SessionState.SetBool(Armed, true);
                SessionState.SetBool(Started, false);
                SessionState.SetInt(ExitCode, 1);
                SessionState.SetString(Deadline, DateTime.UtcNow.AddMinutes(9).Ticks.ToString());
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SV-SLICE-01 Integrated] PREPARE FAIL: " + ex);
                SessionState.SetBool(Armed, false);
                EditorApplication.Exit(2);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Armed, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) StartWork();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var code = SessionState.GetInt(ExitCode, 1);
                SessionState.SetBool(Armed, false);
                SessionState.SetBool(Started, false);
                if (Application.isBatchMode) EditorApplication.Exit(code);
            }
        }

        private static void StartWork()
        {
            if (SessionState.GetBool(Started, false)) return;
            SessionState.SetBool(Started, true);
            Work.Push(Play());
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(Armed, false)) return;
            if (!long.TryParse(SessionState.GetString(Deadline, ""), out var ticks) ||
                DateTime.UtcNow.Ticks > ticks)
            { Finish(false, "全程超时"); return; }
            if (!EditorApplication.isPlaying) return;
            if (!SessionState.GetBool(Started, false)) StartWork();
            if (Work.Count == 0) return;
            try
            {
                var current = Work.Peek();
                if (!current.MoveNext()) Work.Pop();
                else if (current.Current is IEnumerator nested) Work.Push(nested);
                if (Work.Count == 0) Finish(true, "检查点与 Boss 冷读完成");
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(false, ex.Message); }
        }

        private static void Finish(bool ok, string detail)
        {
            if (!SessionState.GetBool(Armed, false)) return;
            Work.Clear();
            SessionState.SetInt(ExitCode, ok ? 0 : 1);
            Debug.Log("[SV-SLICE-01 Integrated] " + (ok ? "PASS" : "FAIL") + ": " + detail +
                "；独立验证档：" + testDirectory);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else if (Application.isBatchMode)
            {
                SessionState.SetBool(Armed, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static IEnumerator Play()
        {
            var flow = SideViewSliceSessionFlow.Instance;
            Require(flow != null && flow.AtTitle, "标题会话根未就绪");
            testDirectory = Path.Combine(Path.GetTempPath(), "SV-SLICE-01-integrated-" + Guid.NewGuid().ToString("N"));
            Require(flow.ConfigureValidationDirectory(testDirectory), "不能隔离集成 smoke 档目录");
            Require(flow.InspectSlots()[0].State == SliceSlotState.Empty && flow.StartNew(Slot),
                "新建空研究槽失败：" + flow.Status);
            yield return AwaitMap(Tut01);
            yield return PhysicalDoor(Tut01Left3, Tut02);
            yield return PhysicalDoor(Tut02Tower, TowerRest);

            var rest = Component<SideViewSliceProgress>();
            Require(rest.HasAnchor(SliceSceneCatalog.RestCheckpointAnchorId), "塔房没有 tower-high 安全锚");
            var recordedActor = rest.party.Current.ActorId;
            Task<bool> save = rest.SaveCheckpointAsync(SliceSceneCatalog.RestCheckpointAnchorId);
            yield return WaitFor(() => save.IsCompleted, "保存椅服务层 flush 未完成", 40);
            Require(save.Status == TaskStatus.RanToCompletion && save.Result,
                "tower-high 未通过 Commerce 写盘屏障：" + rest.Diagnostic);
            Debug.Log("[SV-SLICE-01 Integrated] tower-high 服务层记录与真实 flush 通过；UI 交互另验");
            Require(flow.RequestReturnToTitle("integrated-checkpoint"), "检查点后不能切出会话：" + flow.Status);
            yield return WaitFor(() => flow.AtTitle && !flow.Busy, "检查点后未保存返回标题", 40);
            var preview = flow.InspectSlots()[0];
            Require(preview.State == SliceSlotState.Loadable &&
                preview.CheckpointMapId == SliceSceneCatalog.TowerRestMapId &&
                preview.CheckpointAnchorId == SliceSceneCatalog.RestCheckpointAnchorId,
                "标题预览未指向 tower-high");
            Require(flow.Continue(Slot), "标题继续 tower-high 失败：" + flow.Status);
            yield return AwaitMap(TowerRest);
            rest = Component<SideViewSliceProgress>();
            var anchor = rest.anchors.Single(a => a.anchorId == SliceSceneCatalog.RestCheckpointAnchorId);
            Require(rest.CheckpointAnchorId == SliceSceneCatalog.RestCheckpointAnchorId &&
                rest.CheckpointMapId == SliceSceneCatalog.TowerRestMapId &&
                rest.party.Current.ActorId == recordedActor &&
                Vector3.Distance(rest.party.Characters[0].transform.position, anchor.firstActorPosition) < .5f &&
                Vector3.Distance(rest.party.Characters[1].transform.position, anchor.secondActorPosition) < .5f,
                "tower-high 冷读未恢复地图、双人脚点和实控角色");
            Debug.Log("[SV-SLICE-01 Integrated] tower-high 跨标题冷读通过");

            yield return PhysicalDoor(TowerRestExit, TowerLift);
            yield return DescendElevator();
            yield return PhysicalDoor(TowerLiftLower, TowerCombat);
            yield return PhysicalDoor(TowerCombatExit, TowerBoss);
            yield return KillBossByFormalMelee();
            yield return WaitFor(() => flow.AtTitle && !flow.Busy, "Boss 击杀后未完成保存并返回标题", 35);
            preview = flow.InspectSlots()[0];
            Require(preview.State == SliceSlotState.Loadable && preview.IsCompleted &&
                preview.CheckpointMapId == SliceSceneCatalog.TowerRestMapId &&
                preview.CheckpointAnchorId == SliceSceneCatalog.RestCheckpointAnchorId,
                "通关档没有保留 tower-high 或完成标记");
            Require(flow.Continue(Slot), "通关档不能继续：" + flow.Status);
            yield return AwaitMap(TowerRest);
            rest = Component<SideViewSliceProgress>();
            Require(rest.IsBossDefeated && rest.IsCompleted &&
                rest.CheckpointAnchorId == SliceSceneCatalog.RestCheckpointAnchorId,
                "通关档冷读未恢复 Boss 和完成状态");
            Require(flow.RequestReturnToTitle("integrated-final"), "最终会话未能退到标题：" + flow.Status);
            yield return WaitFor(() => flow.AtTitle && !flow.Busy, "最终退到标题失败", 30);
        }

        private static IEnumerator KillBossByFormalMelee()
        {
            var scene = SceneManager.GetActiveScene();
            var encounter = Components<SVSliceBossEncounter>(scene).Single();
            var boss = encounter.enemy;
            var progress = encounter.progress;
            var party = encounter.party;
            Require(encounter.isActiveAndEnabled && boss != null && boss.Vitals != null &&
                boss.Vitals.IsAlive && progress != null && progress.Ready && party?.Current != null,
                "Boss 遭遇或正式生命对象未就绪");
            var actor = party.Current;
            var movement = actor.GetComponent<CharacterMovement>();
            var melee = actor.GetComponent<CombatMeleeHit>();
            Require(movement != null && melee != null && melee.isActiveAndEnabled,
                "主控角色缺正式近战命中组件");
            boss.AttacksEnabled = false; // Stabilize the isolated probe; the physical hit remains real.
            movement.Teleport(boss.transform.position + Vector3.left * .9f);
            Physics.SyncTransforms();
            bool receiptApplied = false;
            Guid receiptId = Guid.Empty;
            HitOutcome death = null;
            Action<Guid, bool> onResolved = (id, applied) => { receiptId = id; receiptApplied = applied; };
            Action<HitOutcome> onDied = result => death = result;
            melee.Resolved += onResolved;
            boss.Vitals.Died += onDied;
            try
            {
                var release = AttackRelease.ForEmission(Guid.NewGuid(), 0, actor.ActorId, 100000, 0);
                melee.Resolve(release, Vector3.right);
                Require(receiptApplied && receiptId == release.ActionId && death != null &&
                    death.ActionId == release.ActionId && boss.Vitals.DeathCount == 1 &&
                    progress.IsBossDefeated,
                    "Boss 未由同一次正式 CombatMeleeHit 释放击杀；" + melee.LastResult);
                Debug.Log("[SV-SLICE-01 Integrated] CombatMeleeHit → CombatVitals.Died → Boss 事实，ActionId=" +
                    release.ActionId.ToString("N"));
            }
            finally
            {
                if (melee != null) melee.Resolved -= onResolved;
                if (boss != null && boss.Vitals != null) boss.Vitals.Died -= onDied;
            }
            yield break;
        }

        private static IEnumerator DescendElevator()
        {
            var lift = Component<SVSliceElevator>();
            Require(!lift.AtLower && !lift.Moving, "升降机进入状态不在上站");
            var movers = lift.party.Characters.Select(actor => actor.GetComponent<CharacterMovement>()).ToArray();
            Set(movers[0], lift.upperStop.position + Vector3.left * .76f);
            Set(movers[1], lift.upperStop.position + Vector3.right * .76f);
            yield return WaitFor(() => movers.All(actor => actor.Grounded), "双人未在上站站稳");
            Require(lift.IsAccessible(lift.party.Current), "升降机上站不可交互");
            lift.Use(lift.party.Current);
            yield return WaitFor(() => lift.AtLower && !lift.Moving, "升降机未下行到底", 15);
            Require(lift.lowerGate.GetComponent<BoxCollider>().enabled, "下站门未解锁");
        }

        private static IEnumerator PhysicalDoor(string gateId, string destination)
        {
            var gate = Components<SideViewTransitionGate>(SceneManager.GetActiveScene())
                .Single(value => value.gateId == gateId);
            Set(gate.party.Current.GetComponent<CharacterMovement>(), gate.transform.position - Vector3.up * 1.42f);
            yield return AwaitMap(destination);
            Require(SideViewTransitionRuntime.Instance != null &&
                SideViewTransitionRuntime.Instance.DestinationHandle == SceneManager.GetActiveScene().handle,
                "实体门未完成独立 Scene 换区：" + gateId);
        }

        private static IEnumerator AwaitMap(string path)
        {
            yield return WaitFor(() =>
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != path || SideViewTransitionRuntime.Instance?.Busy == true) return false;
                return Components<SideViewSliceProgress>(scene).SingleOrDefault()?.Ready == true &&
                    Components<CommerceService>(scene).SingleOrDefault()?.Ready == true;
            }, "地图或共同快照未就绪：" + path, 30);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message, double seconds = 12)
        {
            var end = EditorApplication.timeSinceStartup + seconds;
            while (!condition() && EditorApplication.timeSinceStartup < end)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Require(condition(), message + "；会话=" + SideViewSliceSessionFlow.Instance?.Status +
                "；换区=" + SideViewTransitionRuntime.Instance?.Status);
        }

        private static void Set(CharacterMovement actor, Vector3 feet)
        {
            Require(actor != null, "角色运动组件缺失");
            actor.Teleport(feet + Vector3.up * .06f);
            Physics.SyncTransforms();
        }

        private static T Component<T>() where T : Component => Components<T>(SceneManager.GetActiveScene()).Single();
        private static IEnumerable<T> Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(false));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
