// 组合特效的作者窗口。预览对象只活在 PreviewRenderUtility，所有配置直接编辑选中组件。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.Combat.Editor
{
    public sealed partial class CombinedVfxEditorWindow : EditorWindow
    {
        private static readonly Color Panel = new Color(.115f, .13f, .15f);
        private static readonly Color Aqua = new Color(.36f, .82f, .8f);
        private static readonly Color Amber = new Color(1f, .74f, .39f);
        private static readonly string[] ToolLabels = { "整体", "路径", "宽度" };
        private static readonly int[] FrameRates = { 30, 60, 120 };

        [SerializeField] private CombinedVfx target;
        [SerializeField] private int selected;
        private readonly HashSet<int> locked = new HashSet<int>();
        private int solo = -1;
        private int anchorMotion;
        private int editTool = 1;
        private int fpsIndex = 1;
        private int seed = 173;
        private bool showFrames;
        private bool playing;
        private bool loop = true;
        private float zoom = 2.25f;
        private float cursor;
        private double lastTick;
        private Vector2 inspectorScroll;
        private Vector2 timelineScroll;
        private PreviewRenderUtility preview;
        private CombinedVfx previewEffect;
        private bool previewDirty = true;
        private string message;

        private VfxLayer SelectedLayer => target != null && target.layers != null &&
            selected >= 0 && selected < target.layers.Count ? target.layers[selected] : null;

        [MenuItem("OSSASTRA/战斗/2D 组合特效")]
        public static void Open() => Open(Selection.activeGameObject);

        public static void Open(GameObject prefab)
        {
            var window = GetWindow<CombinedVfxEditorWindow>("2D 组合特效");
            window.minSize = new Vector2(630, 590);
            if (prefab != null)
            {
                var effect = prefab.GetComponentInChildren<CombinedVfx>(true);
                if (effect != null) window.SetTarget(effect);
            }
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed += RefreshAfterUndo;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupPreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= RefreshAfterUndo;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
            EndDrag(false);
            CleanupPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.ExitingPlayMode) return;
            EndDrag(false);
            playing = false;
            CleanupPreview();
        }

        private void SetTarget(CombinedVfx value)
        {
            if (target == value) return;
            EndDrag(false);
            CleanupPreview();
            target = value;
            selected = 0;
            solo = -1;
            locked.Clear();
            initialPaths.Clear();
            initialShared = null;
            cursor = 0;
            playing = false;
            inspectorScroll = timelineScroll = Vector2.zero;
            message = null;
            Repaint();
        }

        private void RefreshAfterUndo()
        {
            if (target == null) return;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, target.layers.Count - 1));
            locked.RemoveWhere(index => index >= target.layers.Count);
            if (solo >= target.layers.Count) solo = -1;
            cursor = Mathf.Min(cursor, target.Duration);
            previewDirty = true;
            Repaint();
        }

        private void Tick()
        {
            if (!playing) return;
            if (target == null || target.Duration <= 0f)
            {
                playing = false;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            cursor += (float)(now - lastTick);
            lastTick = now;
            if (cursor >= target.Duration)
            {
                if (loop) cursor %= target.Duration;
                else
                {
                    cursor = target.Duration;
                    playing = false;
                }
            }
            Repaint();
        }

        private void OnGUI()
        {
            DrawTargetToolbar();
            if (target == null)
            {
                EditorGUILayout.HelpBox("把带 Combined Vfx 的 Prefab 或场景对象拖到「效果」。也可以在横版角色手感调试台打开绑定中的特效。", MessageType.Info);
                return;
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && dragMode != DragMode.None)
            {
                EndDrag(true);
                e.Use();
            }

            DrawPlayback();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawPreviewToolbar();
                    var rect = GUILayoutUtility.GetRect(220f, 320f, 225f, 285f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawPreview(rect);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width * .37f, 242f, 405f))))
                using (var scope = new EditorGUILayout.ScrollViewScope(inspectorScroll, GUILayout.Height(Mathf.Clamp(position.height * .5f, 255f, 460f))))
                {
                    inspectorScroll = scope.scrollPosition;
                    DrawInspector();
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                DrawTimeline();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.LabelField(message, EditorStyles.miniLabel);
        }

        private void DrawTargetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var value = (CombinedVfx)EditorGUILayout.ObjectField(target, typeof(CombinedVfx), true);
                if (value != target) SetTarget(value);
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                {
                    if (GUILayout.Button("使用当前选择", EditorStyles.toolbarButton))
                    {
                        var candidate = Selection.activeGameObject.GetComponentInChildren<CombinedVfx>(true);
                        if (candidate != null) SetTarget(candidate);
                        else message = "当前选择没有 Combined Vfx 组件。";
                    }
                }
                if (target == null) return;
                var stage = PrefabStageUtility.GetPrefabStage(target.gameObject);
                bool asset = EditorUtility.IsPersistent(target);
                string scope = asset ? "预制体资源" : stage == null ? "场景实例覆盖" : "预制体模式";
                GUILayout.Label(scope, EditorStyles.miniLabel, GUILayout.Width(103f));
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(52f))) SaveTarget();
            }
        }

        private void SaveTarget()
        {
            if (target == null) return;
            if (EditorUtility.IsPersistent(target))
            {
                EditorUtility.SetDirty(target);
                PrefabUtility.SavePrefabAsset(target.transform.root.gameObject, out bool saved);
                message = saved ? "已保存 " + AssetDatabase.GetAssetPath(target) : "当前预制体无法保存，请在预制体模式中核对。";
                return;
            }

            if (PrefabStageUtility.GetPrefabStage(target.gameObject) != null)
            {
                message = "在预制体模式中按 Ctrl+S 保存当前预制体。";
                return;
            }

            message = Application.isPlaying ? "播放期间的实例编辑仅供当前试玩。" : "已记录场景实例覆盖。在场景中按 Ctrl+S 保存。";
        }

        private void DrawPlayback()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(playing ? "暂停" : "播放", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    if (!playing && cursor >= target.Duration) cursor = 0;
                    playing = !playing;
                    lastTick = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("复位", EditorStyles.toolbarButton, GUILayout.Width(45f))) SetCursor(0f);
                if (GUILayout.Button("◀ 帧", EditorStyles.toolbarButton, GUILayout.Width(42f))) Step(-1);
                if (GUILayout.Button("帧 ▶", EditorStyles.toolbarButton, GUILayout.Width(42f))) Step(1);
                loop = GUILayout.Toggle(loop, "循环", EditorStyles.toolbarButton, GUILayout.Width(42f));
                GUILayout.Space(8f);
                showFrames = EditorGUILayout.Popup(showFrames ? 1 : 0, new[] { "毫秒", "参考帧" }, EditorStyles.toolbarPopup, GUILayout.Width(70f)) == 1;
                if (showFrames)
                    fpsIndex = EditorGUILayout.Popup(fpsIndex, new[] { "30 fps", "60 fps", "120 fps" }, EditorStyles.toolbarPopup, GUILayout.Width(72f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(TimeLabel(cursor) + " / " + TimeLabel(target.Duration), EditorStyles.miniLabel);
                GUILayout.Space(6f);
                GUILayout.Label("种子 " + seed, EditorStyles.miniLabel);
                if (GUILayout.Button("重新随机", EditorStyles.toolbarButton)) seed = System.Environment.TickCount & int.MaxValue;
            }
        }

        private string TimeLabel(float seconds) => showFrames
            ? Mathf.RoundToInt(seconds * FrameRates[fpsIndex]) + " 帧"
            : Mathf.RoundToInt(seconds * 1000f) + " ms";

        private void Step(int direction)
        {
            int fps = FrameRates[fpsIndex];
            SetCursor((Mathf.RoundToInt(cursor * fps) + direction) / (float)fps);
        }

        private void SetCursor(float value)
        {
            cursor = Mathf.Clamp(value, 0f, target.Duration);
            playing = false;
            Repaint();
        }

        private void DrawPreviewToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                editTool = GUILayout.Toolbar(editTool, ToolLabels, EditorStyles.toolbarButton, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();
                anchorMotion = EditorGUILayout.Popup(anchorMotion, new[] { "固定挂点", "移动挂点示范" }, EditorStyles.toolbarPopup, GUILayout.Width(113f));
                if (GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(26f))) zoom = Mathf.Min(zoom * 1.25f, 40f);
                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(26f))) zoom = Mathf.Max(zoom / 1.25f, .12f);
            }
        }

        private void DrawPreview(Rect rect)
        {
            if (rect.width < 4f || rect.height < 4f) return;
            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreview();
                if (previewDirty)
                {
                    previewEffect.ReleaseVisuals();
                    EditorUtility.CopySerialized(target, previewEffect);
                    previewDirty = false;
                }

                var camera = preview.camera;
                camera.orthographicSize = zoom;
                camera.aspect = rect.width / rect.height;
                previewEffect.Evaluate(cursor, seed, SampleAnchor, IsVisible);
                preview.BeginPreview(rect, GUIStyle.none);
                preview.Render(true);
                var image = preview.EndPreview();
                GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, false);
                DrawOrigin(rect);
            }
            DrawHandles(rect);
        }

        private void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.transform.position = new Vector3(0, 0, -10);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Panel;
            preview.camera.nearClipPlane = .05f;
            preview.camera.farClipPlane = 50f;
            var instance = new GameObject("组合特效预览") { hideFlags = HideFlags.HideAndDontSave };
            preview.AddSingleGO(instance);
            previewEffect = instance.AddComponent<CombinedVfx>();
            previewDirty = true;
        }

        private void CleanupPreview()
        {
            if (previewEffect != null) previewEffect.ReleaseVisuals();
            previewEffect = null;
            preview?.Cleanup();
            preview = null;
            previewDirty = true;
        }

        private bool IsVisible(int index) => solo < 0 || index == solo;

        private Matrix4x4 SampleAnchor(string bone, float seconds)
        {
            if (anchorMotion == 0) return Matrix4x4.identity;
            return Matrix4x4.Translate(new Vector3(Mathf.Sin(seconds * 4f) * .9f, Mathf.Sin(seconds * 3f) * .16f, 0f));
        }

        private void Change(string label, Action apply)
        {
            Undo.RecordObject(target, label);
            apply();
            MarkChanged();
        }

        private void MarkChanged()
        {
            EditorUtility.SetDirty(target);
            if (!EditorUtility.IsPersistent(target) && !Application.isPlaying)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                var scene = target.gameObject.scene;
                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            }
            previewDirty = true;
            Repaint();
        }
    }
}
