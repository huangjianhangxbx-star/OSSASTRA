/*
 * MCP-callable Play Mode probe: isolated title -> empty slot -> Tut01 -> saved title
 * -> Continue -> Tut01 checkpoint -> saved title. It never uses the normal save path.
 */
using System;
using System.IO;
using Ossastra.Commerce.Runtime;
using Ossastra.SideView.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.Slice.Editor
{
    public static class SideViewSliceSmokeProbe
    {
        private enum Stage { Idle, WaitNew, WaitFirstTitle, WaitContinue, WaitFinalTitle }
        private static Stage stage;
        private static DateTime deadline;
        private static string testDirectory;
        private const string Slot = "slice-slot-01";

        public static string Result { get; private set; } = "NOT RUN";
        public static string TestDirectory => testDirectory;

        /// <summary>Call only after entering Play Mode in the authored title scene.</summary>
        public static string Run()
        {
            if (stage != Stage.Idle) return Result;
            if (!EditorApplication.isPlaying ||
                SceneManager.GetActiveScene().path != SliceSceneCatalog.TitleScenePath)
                return Result = "FAIL: Enter Play Mode in the independent Slice title scene first.";
            var flow = UnityEngine.Object.FindObjectOfType<SideViewSliceSessionFlow>(true);
            if (flow == null || !flow.AtTitle)
                return Result = "FAIL: Title session flow is missing.";
            testDirectory = Path.Combine(Path.GetTempPath(), "SV-SLICE-01-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!flow.ConfigureValidationDirectory(testDirectory))
                    return Result = "FAIL: Could not isolate validation profile directory: " + flow.Status;
                if (flow.InspectSlots()[0].State != SliceSlotState.Empty || !flow.StartNew(Slot))
                    return Result = "FAIL: Empty slot did not start Tut01: " + flow.Status;
                Result = "RUNNING: opening Tut01 from a new isolated slot";
                deadline = DateTime.UtcNow.AddSeconds(180);
                stage = Stage.WaitNew;
                EditorApplication.update += Advance;
                return Result;
            }
            catch (Exception ex) { return Result = "FAIL: " + ex; }
        }

        private static void Advance()
        {
            if (!EditorApplication.isPlaying) { Finish("FAIL: Play Mode stopped before probe completed."); return; }
            if (DateTime.UtcNow > deadline) { Finish("FAIL: Timed out at " + stage); return; }
            try
            {
                var flow = SideViewSliceSessionFlow.Instance;
                if (flow == null) { Finish("FAIL: Session flow was destroyed."); return; }
                if (flow.Busy) return;
                if (flow.Status.IndexOf("失败", StringComparison.Ordinal) >= 0)
                { Finish("FAIL: " + flow.Status); return; }
                switch (stage)
                {
                    case Stage.WaitNew:
                        if (!flow.GameActive || SceneManager.GetActiveScene().path != SliceSceneCatalog.Tut01ScenePath)
                            return;
                        var progress = UnityEngine.Object.FindObjectOfType<SideViewSliceProgress>(true);
                        var commerce = UnityEngine.Object.FindObjectOfType<CommerceService>(true);
                        if (progress == null || commerce == null || !progress.Ready ||
                            commerce.profileId != Slot || commerce.saveDirectory != testDirectory ||
                            !progress.RecordCheckpoint(SliceSceneCatalog.OpeningAnchorId))
                        { Finish("FAIL: New game did not bind isolated Commerce and Tut01 progress."); return; }
                        if (!flow.RequestReturnToTitle("smoke-new"))
                        { Finish("FAIL: New game could not request a saved return: " + flow.Status); return; }
                        Result = "RUNNING: saving new game and returning to title";
                        stage = Stage.WaitFirstTitle;
                        break;
                    case Stage.WaitFirstTitle:
                        if (!flow.AtTitle) return;
                        var preview = flow.InspectSlots()[0];
                        if (preview.State != SliceSlotState.Loadable ||
                            preview.CheckpointMapId != SliceSceneCatalog.Tut01MapId ||
                            preview.CheckpointAnchorId != SliceSceneCatalog.OpeningAnchorId ||
                            !flow.Continue(Slot))
                        { Finish("FAIL: Saved slot is not resumable from Tut01: " + flow.Status); return; }
                        Result = "RUNNING: cold loading Continue into Tut01";
                        stage = Stage.WaitContinue;
                        break;
                    case Stage.WaitContinue:
                        if (!flow.GameActive || SceneManager.GetActiveScene().path != SliceSceneCatalog.Tut01ScenePath)
                            return;
                        var resumed = UnityEngine.Object.FindObjectOfType<SideViewSliceProgress>(true);
                        var loaded = UnityEngine.Object.FindObjectOfType<CommerceService>(true);
                        if (resumed == null || loaded == null || !resumed.Ready ||
                            loaded.profileId != Slot || loaded.saveDirectory != testDirectory ||
                            resumed.CheckpointMapId != SliceSceneCatalog.Tut01MapId ||
                            resumed.CheckpointAnchorId != SliceSceneCatalog.OpeningAnchorId ||
                            !resumed.Diagnostic.StartsWith("已从检查点恢复", StringComparison.Ordinal))
                        { Finish("FAIL: Continue did not restore a fresh Tut01 checkpoint session."); return; }
                        if (!flow.RequestReturnToTitle("smoke-continue"))
                        { Finish("FAIL: Continue could not request a saved return: " + flow.Status); return; }
                        Result = "RUNNING: saving resumed session and returning to title";
                        stage = Stage.WaitFinalTitle;
                        break;
                    case Stage.WaitFinalTitle:
                        if (!flow.AtTitle) return;
                        if (flow.InspectSlots()[0].State != SliceSlotState.Loadable)
                        { Finish("FAIL: Final title does not show the loadable slot."); return; }
                        Finish("PASS: title -> empty slot -> Tut01 -> saved title -> Continue -> checkpoint -> saved title; "
                            + testDirectory);
                        break;
                }
            }
            catch (Exception ex) { Finish("FAIL: " + ex); }
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Advance;
            stage = Stage.Idle;
            Result = result;
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log("[SV-SLICE-01 Smoke] " + result);
            else Debug.LogError("[SV-SLICE-01 Smoke] " + result);
        }
    }
}
