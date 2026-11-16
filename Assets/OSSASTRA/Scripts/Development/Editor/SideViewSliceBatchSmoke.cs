/*
 * Standalone -executeMethod entry. Do not pass -quit: this runner enters Play, survives
 * assembly reload through SessionState, waits for the title probe, exits Play, then
 * closes the batch Editor with a truthful process exit code.
 */
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.Slice.Editor
{
    [InitializeOnLoad]
    public static class SideViewSliceBatchSmoke
    {
        private const string Prefix = "SV-SLICE-01.TitleBatch.";
        private const string PhaseKey = Prefix + "Phase";
        private const string DeadlineKey = Prefix + "DeadlineUtcTicks";
        private const string OutcomeKey = Prefix + "Outcome";
        private const string ExitCodeKey = Prefix + "ExitCode";

        // 0 idle, 1 entering Play, 2 title probe running, 3 exiting Play.
        static SideViewSliceBatchSmoke() => EditorApplication.update += Pump;

        public static string Outcome => SessionState.GetString(OutcomeKey, "NOT RUN");

        /// <summary>Batch CLI: -batchmode -nographics -executeMethod Ossastra.SideView.Slice.Editor.SideViewSliceBatchSmoke.Run, without -quit.</summary>
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("独立标题批处理只在 -batchmode 运行，避免关闭交互编辑器。");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("启动标题 smoke 前必须处于 Edit 模式。");
                for (int i = 0; i < SceneManager.sceneCount; ++i)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("有未保存场景，拒绝切场：" + SceneManager.GetSceneAt(i).path);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SliceSceneCatalog.TitleScenePath) == null)
                    throw new InvalidOperationException("独立标题场景尚未装配。");
                if (!EditorBuildSettings.scenes.Any(scene => scene.enabled &&
                    scene.path == SliceSceneCatalog.TitleScenePath))
                    throw new InvalidOperationException("Build Settings 未启用独立标题场景。");
                SessionState.SetString(OutcomeKey, "RUNNING: entering title Play Mode");
                SessionState.SetInt(ExitCodeKey, 1);
                SessionState.SetString(DeadlineKey, DateTime.UtcNow.AddMinutes(7).Ticks.ToString());
                SessionState.SetInt(PhaseKey, 1);
                EditorSceneManager.OpenScene(SliceSceneCatalog.TitleScenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex)
            {
                SessionState.SetInt(PhaseKey, 0);
                SessionState.SetString(OutcomeKey, "FAIL: " + ex);
                Debug.LogError("[SV-SLICE-01 TitleBatch] " + Outcome);
                EditorApplication.Exit(2);
            }
        }

        private static void Pump()
        {
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) return;
            if (phase == 3)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (long.TryParse(SessionState.GetString(DeadlineKey, ""), out long exitDeadline) &&
                        DateTime.UtcNow.Ticks > exitDeadline)
                        EditorApplication.Exit(1);
                    return;
                }
                int code = SessionState.GetInt(ExitCodeKey, 1);
                string outcome = Outcome;
                SessionState.SetInt(PhaseKey, 0);
                Debug.Log("[SV-SLICE-01 TitleBatch] Exit " + code + ": " + outcome);
                EditorApplication.Exit(code);
                return;
            }
            if (!long.TryParse(SessionState.GetString(DeadlineKey, ""), out long deadlineTicks) ||
                DateTime.UtcNow.Ticks > deadlineTicks)
            {
                Complete(false, "FAIL: batch title smoke timed out during phase " + phase);
                return;
            }
            try
            {
                if (phase == 1)
                {
                    if (!EditorApplication.isPlaying ||
                        SceneManager.GetActiveScene().path != SliceSceneCatalog.TitleScenePath ||
                        SideViewSliceSessionFlow.Instance == null) return;
                    string result = SideViewSliceSmokeProbe.Run();
                    if (result.StartsWith("FAIL:", StringComparison.Ordinal))
                    { Complete(false, result); return; }
                    SessionState.SetInt(PhaseKey, 2);
                    SessionState.SetString(OutcomeKey, result);
                }
                else if (phase == 2)
                {
                    string result = SideViewSliceSmokeProbe.Result;
                    if (result.StartsWith("RUNNING:", StringComparison.Ordinal))
                    { SessionState.SetString(OutcomeKey, result); return; }
                    Complete(result.StartsWith("PASS:", StringComparison.Ordinal), result);
                }
            }
            catch (Exception ex) { Complete(false, "FAIL: " + ex); }
        }

        private static void Complete(bool ok, string result)
        {
            SessionState.SetString(OutcomeKey, result);
            SessionState.SetInt(ExitCodeKey, ok ? 0 : 1);
            SessionState.SetInt(PhaseKey, 3);
            if (ok) Debug.Log("[SV-SLICE-01 TitleBatch] " + result);
            else Debug.LogError("[SV-SLICE-01 TitleBatch] " + result);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
