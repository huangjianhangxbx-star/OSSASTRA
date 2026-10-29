/*
 * 脚本：FrontierGroundBrush
 * 功能：为原生Tile Palette限定地表、格范围和单笔Undo事务，实际作者数据仍为Tilemap。
 * 边界：不持有第二份格数据、不实例化材质或推进角色；拖动补点由现有原生Palette负责。
 * 协作：Window显式配置图层；GridBrushEditor的原生入口开始事务，Esc取消，失焦提交已刷内容。
 */
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch.Editor
{
    [CustomGridBrush(false, false, false, "研究地表受限画笔")]
    [BrushTools(typeof(PaintTool), typeof(EraseTool), typeof(BoxTool), typeof(PickingTool))]
    public sealed class FrontierGroundBrush : GridBrush
    {
        [NonSerialized] private GroundResearchSurface surface;
        [NonSerialized] private int selectedLayer;
        [NonSerialized] private Tilemap strokeTarget;
        [NonSerialized] private int undoGroup = -1;
        public bool IsStrokeActive => strokeTarget != null && undoGroup >= 0;
        public string LastStrokeNotice { get; private set; }
        public GroundResearchSurface Surface => surface;
        public Tilemap LastPickedMap { get; private set; }
        public Vector3Int LastPickedCell { get; private set; }
        public GroundLayer Layer => surface != null && surface.layers != null && selectedLayer >= 0 &&
            selectedLayer < surface.layers.Length ? surface.layers[selectedLayer] : null;

        public bool Configure(GroundResearchSurface selected, int layerIndex, int diameter)
        {
            FinishStroke(false);
            surface = selected; selectedLayer = layerIndex;
            var layer = Layer;
            if (layer == null || layer.paintTile == null || !CanPaintTarget(layer.tilemap == null ? null : layer.tilemap.gameObject)) return false;
            diameter = Mathf.Clamp(diameter, 1, 16);
            Init(new Vector3Int(diameter, diameter, 1), new Vector3Int(diameter / 2, diameter / 2, 0));
            foreach (var position in new BoundsInt(Vector3Int.zero, size).allPositionsWithin) SetTile(position, layer.paintTile);
            canChangeZPosition = false;
            return true;
        }

        public bool CanPaintTarget(GameObject target)
        {
            if (Application.isPlaying || surface == null || !surface.isActiveAndEnabled || target == null ||
                EditorUtility.IsPersistent(target) || PrefabStageUtility.GetCurrentPrefabStage() != null ||
                target.scene != surface.gameObject.scene) return false;
            var map = target.GetComponent<Tilemap>();
            return map != null && surface.Owns(map) && Layer != null && Layer.tilemap == map;
        }

        public bool BeginStroke(Tilemap target)
        {
            if (!CanPaintTarget(target == null ? null : target.gameObject)) return false;
            if (IsStrokeActive && strokeTarget == target) return true;
            FinishStroke(false);
            LastStrokeNotice = null;
            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("刷地：" + Layer.displayName);
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { target, target.gameObject }, "刷地：" + Layer.displayName);
            strokeTarget = target;
            return true;
        }

        public void FinishStroke(bool cancel)
        {
            if (undoGroup < 0) { strokeTarget = null; return; }
            var group = undoGroup; var map = strokeTarget;
            strokeTarget = null; undoGroup = -1;
            Undo.FlushUndoRecordObjects();
            if (Undo.GetCurrentGroup() == group)
            {
                if (cancel) Undo.RevertAllDownToGroup(group); else Undo.CollapseUndoOperations(group);
            }
            else
            {
                LastStrokeNotice = "检测到其他Editor操作创建了Undo组；本笔已安全结束并保留已画内容，没有回滚或合并其他操作。";
                Debug.LogWarning(LastStrokeNotice, map);
            }
            Undo.IncrementCurrentGroup();
            if (map != null && map.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            SceneView.RepaintAll();
        }

        public override void Paint(GridLayout gridLayout, GameObject brushTarget, Vector3Int position) =>
            Apply(brushTarget, new BoundsInt(position - pivot, size), false);
        public override void Erase(GridLayout gridLayout, GameObject brushTarget, Vector3Int position) =>
            Apply(brushTarget, new BoundsInt(position - pivot, size), true);
        public override void BoxFill(GridLayout gridLayout, GameObject brushTarget, BoundsInt position) => Apply(brushTarget, position, false);
        public override void BoxErase(GridLayout gridLayout, GameObject brushTarget, BoundsInt position) => Apply(brushTarget, position, true);

        private void Apply(GameObject target, BoundsInt area, bool erase)
        {
            if (!CanPaintTarget(target)) return;
            var map = target.GetComponent<Tilemap>();
            var legal = surface.EditableCells;
            int minX = Mathf.Max(area.xMin, legal.xMin), maxX = Mathf.Min(area.xMax, legal.xMax);
            int minY = Mathf.Max(area.yMin, legal.yMin), maxY = Mathf.Min(area.yMax, legal.yMax);
            if (minX >= maxX || minY >= maxY || area.zMin > 0 || area.zMax <= 0) return;
            var tile = erase ? Layer.eraseReplacement : Layer.paintTile;
            for (int y = minY; y < maxY; y++) for (int x = minX; x < maxX; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (map.GetTile(cell) == tile) continue;
                if (!IsStrokeActive && !BeginStroke(map)) return;
                map.SetTile(cell, tile);
            }
        }

        public override void Pick(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, Vector3Int pickStart)
        {
            if (!CanPaintTarget(brushTarget) || !surface.ContainsCell(position.min)) return;
            LastPickedMap = brushTarget.GetComponent<Tilemap>(); LastPickedCell = position.min;
        }

        // 当前参考中的方向变体由已验证RuleTile决定；不额外旋转/镜像烘焙图案。
        public override void Rotate(RotationDirection direction, Grid.CellLayout layout) { }
        public override void Flip(FlipAxis flip, Grid.CellLayout layout) { }
        private void OnDisable() => FinishStroke(false);
    }

    [CustomEditor(typeof(FrontierGroundBrush))]
    public sealed class FrontierGroundBrushEditor : GridBrushEditor
    {
        private readonly List<Vector3Int> previewCells = new List<Vector3Int>();
        private Tilemap previewMap;
        private EditorWindow strokeWindow;
        private SceneView gestureView;
        private int ownedHotControl;
        private bool gestureActive, requiresNewDown = true, pendingRelease, sawMouseUp;
        private FrontierGroundBrush GroundBrush => target as FrontierGroundBrush;
        protected override void OnEnable() { base.OnEnable(); EditorApplication.update += ObserveFocus; }
        protected override void OnDisable()
        {
            EndGesture(false);
            EditorApplication.update -= ObserveFocus; base.OnDisable();
        }
        public override GameObject[] validTargets => GroundBrush == null || GroundBrush.Layer == null ||
            !GroundBrush.CanPaintTarget(GroundBrush.Layer.tilemap == null ? null : GroundBrush.Layer.tilemap.gameObject)
            ? Array.Empty<GameObject>() : new[] { GroundBrush.Layer.tilemap.gameObject };
        public override void RegisterUndo(GameObject brushTarget, GridBrushBase.Tool tool)
        {
            if (GroundBrush != null && GroundBrush.CanPaintTarget(brushTarget))
            {
                GroundBrush.BeginStroke(brushTarget.GetComponent<Tilemap>());
                strokeWindow = EditorWindow.focusedWindow;
                gestureView = SceneView.currentDrawingSceneView;
                gestureActive = true; requiresNewDown = false;
            }
        }
        public override void OnSceneGUI(GridLayout gridLayout, GameObject brushTarget)
        {
            var brush = GroundBrush; var e = Event.current;
            if (brush == null || e == null) return;
            sawMouseUp = e.type == EventType.MouseUp || e.rawType == EventType.MouseUp;
            ReleasePendingControl();
            if (!brush.CanPaintTarget(brushTarget)) { EndGesture(false); ReleasePendingControl(); return; }
            if ((e.type == EventType.KeyDown || e.rawType == EventType.KeyDown) && e.keyCode == KeyCode.Escape &&
                (gestureActive || brush.IsStrokeActive || ownedHotControl != 0))
            { EndGesture(true); ReleasePendingControl(); e.Use(); return; }
            if (e.alt && (gestureActive || brush.IsStrokeActive))
            { EndGesture(false); ReleasePendingControl(); }
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.control)
            {
                gestureView = SceneView.currentDrawingSceneView; strokeWindow = EditorWindow.focusedWindow;
                gestureActive = true; requiresNewDown = false;
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt && !e.control && requiresNewDown)
                e.Use();
            // MouseUp不能在这里提交或释放：Native Box在随后OnGUI的MouseUp阶段才RegisterUndo/填充。
        }
        public override void OnPaintSceneGUI(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, bool executing)
        {
            if (GroundBrush == null || !GroundBrush.CanPaintTarget(brushTarget)) { ClearPreview(); return; }
            if (executing && gestureActive)
            {
                ownedHotControl = GUIUtility.hotControl;
                gestureView = SceneView.currentDrawingSceneView;
                if (strokeWindow == null) strokeWindow = EditorWindow.focusedWindow;
            }
            if (sawMouseUp) { sawMouseUp = false; EndGesture(false); ReleasePendingControl(); }
            if (tool == GridBrushBase.Tool.Box && executing && requiresNewDown) { ClearPreview(); return; }
            base.OnPaintSceneGUI(gridLayout, brushTarget, position, tool, executing);
        }
        public override void OnMouseLeave() { EndGesture(false); ReleasePendingControl(); base.OnMouseLeave(); }
        public override void OnToolDeactivated(GridBrushBase.Tool tool)
        { EndGesture(false); ReleasePendingControl(); base.OnToolDeactivated(tool); }
        public override void OnPaintInspectorGUI() => EditorGUILayout.HelpBox("在地表刷地窗口选择中文图层与大小。Esc取消本笔；失焦提交已刷部分；Ctrl+Z撤销。", MessageType.Info);
        private void ObserveFocus()
        {
            if (GroundBrush != null && (GroundBrush.IsStrokeActive || gestureActive) &&
                (Application.isPlaying || strokeWindow != null && EditorWindow.focusedWindow != strokeWindow)) EndGesture(false);
        }
        private void EndGesture(bool cancel)
        {
            bool hadGesture = gestureActive || ownedHotControl != 0 || GroundBrush != null && GroundBrush.IsStrokeActive;
            if (GroundBrush != null) GroundBrush.FinishStroke(cancel);
            gestureActive = false; requiresNewDown = true;
            pendingRelease = ownedHotControl != 0;
            if (!pendingRelease) gestureView = null;
            if (hadGesture) SceneView.RepaintAll();
        }
        private void ReleasePendingControl()
        {
            if (!pendingRelease || Event.current == null || SceneView.currentDrawingSceneView != gestureView) return;
            if (GUIUtility.hotControl == ownedHotControl) GUIUtility.hotControl = 0;
            ownedHotControl = 0; pendingRelease = false; gestureView = null;
        }
        public override void PaintPreview(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
        { if (GroundBrush != null) Preview(brushTarget, new BoundsInt(position - GroundBrush.pivot, GroundBrush.size)); }
        public override void BoxFillPreview(GridLayout gridLayout, GameObject brushTarget, BoundsInt position) => Preview(brushTarget, position);
        private void Preview(GameObject targetObject, BoundsInt area)
        {
            ClearPreview();
            if (GroundBrush == null || !GroundBrush.CanPaintTarget(targetObject)) return;
            previewMap = targetObject.GetComponent<Tilemap>();
            var legal = GroundBrush.Surface.EditableCells;
            int minX = Mathf.Max(area.xMin, legal.xMin), maxX = Mathf.Min(area.xMax, legal.xMax);
            int minY = Mathf.Max(area.yMin, legal.yMin), maxY = Mathf.Min(area.yMax, legal.yMax);
            for (int y = minY; y < maxY; y++) for (int x = minX; x < maxX; x++)
            { var cell = new Vector3Int(x, y, 0); previewMap.SetEditorPreviewTile(cell, GroundBrush.Layer.paintTile); previewCells.Add(cell); }
        }
        public override void ClearPreview()
        {
            if (previewMap != null) foreach (var cell in previewCells) previewMap.SetEditorPreviewTile(cell, null);
            previewCells.Clear(); previewMap = null; base.ClearPreview();
        }
    }
}
