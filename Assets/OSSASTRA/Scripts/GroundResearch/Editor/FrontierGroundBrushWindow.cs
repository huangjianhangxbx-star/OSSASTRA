/*
 * 脚本：FrontierGroundBrushWindow
 * 功能：以中文图层/缩略图配置现有Tile Palette，保存原生Tilemap并查看实际地块来源。
 * 边界：不维护独立地图数据、不重导出媒体；示范重置显式确认，缓存重建只调用Builder刷新入口。
 * 生命周期：关闭时完成当前笔画；只恢复仍由本窗持有的Palette、Scene可见性与线框状态。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch.Editor
{
    public sealed class FrontierGroundBrushWindow : EditorWindow
    {
        [SerializeField] private GroundResearchSurface surface;
        [SerializeField] private int layerIndex, diameter = 1;
        private FrontierGroundBrush brush;
        private GridBrushBase previousBrush;
        private GameObject previousTarget;
        private Vector2 scroll;
        private string status = "选择本轮地表后，点选中文图层并使用原生Tile Palette拖刷。";
        private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, bool> writtenVisibility = new Dictionary<GameObject, bool>();
        private SceneView wireView;
        private SceneView.CameraMode previousCameraMode, writtenCameraMode;
        private bool ownsWireMode;

        [MenuItem("Tools/OSSASTRA/地表研究/刷地与来源")]
        public static void Open() => GetWindow<FrontierGroundBrushWindow>("地表刷地与来源");
        private void OnEnable() { Selection.selectionChanged += OnSelectionChanged; Undo.undoRedoPerformed += OnAuthorUndoRedo; }
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnAuthorUndoRedo;
            RestoreView();
            if (brush == null) return;
            brush.FinishStroke(false);
            if (GridPaintingState.gridBrush == brush)
            {
                GridPaintingState.gridBrush = previousBrush;
                GridPaintingState.scenePaintTarget = previousTarget;
                if (ToolManager.activeToolType != null && typeof(TilemapEditorTool).IsAssignableFrom(ToolManager.activeToolType)) ToolManager.RestorePreviousTool();
            }
            DestroyImmediate(brush); brush = null;
        }
        private void OnSelectionChanged()
        {
            var selected = Selection.activeGameObject;
            var candidate = selected == null ? null : selected.GetComponentInParent<GroundResearchSurface>();
            if (candidate != null && candidate != surface)
            { if (brush != null) brush.FinishStroke(false); RestoreView(); surface = candidate; layerIndex = 0; if (brush != null) brush.Configure(surface, layerIndex, diameter); }
            Repaint();
        }
        private void OnInspectorUpdate() { if (brush != null && brush.LastPickedMap != null) Repaint(); }
        private void OnAuthorUndoRedo()
        {
            if (CanAuthorSurface()) FrontierGroundSceneBuilder.RebuildDisplay(surface);
            Repaint();
        }
        private bool CanAuthorSurface() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && PrefabStageUtility.GetCurrentPrefabStage() == null && surface != null &&
            !EditorUtility.IsPersistent(surface) && surface.gameObject.scene.IsValid() && surface.gameObject.scene.isLoaded &&
            surface.gameObject.scene.path == FrontierGroundSceneBuilder.ScenePath && surface.layers != null && surface.layers.Length == 7 &&
            surface.layers.All(layer => layer != null && layer.paintTile != null && surface.Owns(layer.tilemap));
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("作者数据就是场景里的原生Tilemap。画笔实时更新、松手一笔；Esc取消整笔，离开Scene/失焦提交已刷部分。", MessageType.Info);
            var selected = (GroundResearchSurface)EditorGUILayout.ObjectField("本轮地表", surface, typeof(GroundResearchSurface), true);
            if (selected != surface)
            { if (brush != null) brush.FinishStroke(false); RestoreView(); surface = selected; layerIndex = 0; if (brush != null) brush.Configure(surface, layerIndex, diameter); }
            if (surface == null || surface.layers == null || surface.layers.Length == 0)
            { EditorGUILayout.HelpBox("在Hierarchy选择新研究场的GroundResearchSurface。旧地图没有该作者标记，不能被本画笔写入。", MessageType.Warning); return; }
            var size = surface.WorldBounds.size;
            EditorGUILayout.LabelField("有效XZ范围", size.x.ToString("0.###") + " × " + size.z.ToString("0.###") + " 米；" + surface.WorldArea.ToString("0.###") + " 平方米");
            EditorGUILayout.LabelField("作者格 / 渲染", surface.EditableCells.size.x + " × " + surface.EditableCells.size.y + "；原生TilemapRenderer分块");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            layerIndex = Mathf.Clamp(layerIndex, 0, surface.layers.Length - 1);
            for (int i = 0; i < surface.layers.Length; i++)
            {
                var layer = surface.layers[i]; if (layer == null) continue;
                EditorGUILayout.BeginHorizontal();
                Rect preview = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48));
                if (layer.detailTexture != null) GUI.DrawTexture(preview, layer.detailTexture, ScaleMode.ScaleToFit, true);
                else DrawSprite(preview, layer.preview);
                if (GUILayout.Toggle(layerIndex == i, new GUIContent(string.IsNullOrEmpty(layer.displayName) ? "未命名图层" : layer.displayName, "选择这一层作为唯一刷地目标"), "Button", GUILayout.Height(44)) && layerIndex != i)
                { if (brush != null) brush.FinishStroke(false); layerIndex = i; if (brush != null) Activate(CurrentBrushTool()); }
                bool visible = layer.tilemap != null && !SceneVisibilityManager.instance.IsHidden(layer.tilemap.gameObject);
                bool changed = GUILayout.Toggle(visible, "Scene显示", GUILayout.Width(85));
                if (changed != visible && layer.tilemap != null) SetVisible(layer.tilemap.gameObject, changed);
                EditorGUILayout.EndHorizontal();
            }
            var chosen = surface.layers[layerIndex];
            if (chosen == null || chosen.tilemap == null)
            { EditorGUILayout.HelpBox("所选图层缺少Tilemap引用；保留现场，不尝试补建。", MessageType.Error); EditorGUILayout.EndScrollView(); return; }
            int nextDiameter = EditorGUILayout.IntSlider("画笔边长（格）", diameter, 1, 8);
            if (nextDiameter != diameter) { diameter = nextDiameter; if (brush != null && CanAuthorSurface()) brush.Configure(surface, layerIndex, diameter); }
            if (!CanAuthorSurface()) EditorGUILayout.HelpBox("只在本轮Lab场景、Edit ready且非PrefabStage时写入作者数据。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!CanAuthorSurface()))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("拖刷")) Activate(typeof(PaintTool));
                if (GUILayout.Button("擦除 / 恢复基础")) Activate(typeof(EraseTool));
                if (GUILayout.Button("矩形填充")) Activate(typeof(BoxTool));
                if (GUILayout.Button("点选来源（吸管）")) Activate(typeof(PickingTool));
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("保存作者数据和场景")) SaveCurrent();
                if (GUILayout.Button("重建显示缓存（不改手绘）"))
                { if (brush != null) brush.FinishStroke(false); FrontierGroundSceneBuilder.RebuildDisplay(surface); status = "已刷新原生显示，作者格子未重置。"; }
                if (GUILayout.Button("重置为本轮示范布局…") && EditorUtility.DisplayDialog("替换当前手绘地面", "此操作会把该地表所有作者格替换为本轮示范布局，可用一次Ctrl+Z撤销。重建显示不需要使用此操作。", "替换为示范", "保留手绘"))
                { if (brush != null) brush.FinishStroke(false); FrontierGroundSceneBuilder.ResetDemonstration(surface); status = "已显式重置示范；Ctrl+Z撤销。"; }
            }
            using (new EditorGUI.DisabledScope(!CanAuthorSurface()))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Scene线框")) SetWireView();
                if (GUILayout.Button("只看所选组成"))
                { foreach (var layer in surface.layers) if (layer != null && layer.tilemap != null) SetVisible(layer.tilemap.gameObject, layer == chosen); }
                if (GUILayout.Button("恢复查看状态")) RestoreView();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Scene显示/线框只用于Editor观察，不写作者数据；不改变角色、碰撞、时间或Game View。分层不代表原作每层都是独立Mesh。", MessageType.None);
            DrawSource(chosen);
            if (brush != null && !string.IsNullOrEmpty(brush.LastStrokeNotice)) EditorGUILayout.HelpBox(brush.LastStrokeNotice, MessageType.Warning);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void Activate(Type tool)
        {
            if (!CanAuthorSurface()) { status = "当前不是本轮Lab的可写Edit上下文，未启动画笔。"; return; }
            if (brush == null)
            {
                previousBrush = GridPaintingState.gridBrush; previousTarget = GridPaintingState.scenePaintTarget;
                brush = CreateInstance<FrontierGroundBrush>(); brush.name = "本轮地表受限画笔"; brush.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!brush.Configure(surface, layerIndex, diameter)) { status = "该地表/图层缺引用、处于PrefabStage或不允许编辑。"; return; }
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            // Native目标列表缓存属于BrushEditor；切层后重建这份编辑器缓存，不能沿用另一层validTargets。
            if (GridPaintingState.gridBrush == brush) GridPaintingState.gridBrush = null;
            GridPaintingState.gridBrush = brush; GridPaintingState.scenePaintTarget = surface.layers[layerIndex].tilemap.gameObject;
            Selection.activeGameObject = surface.layers[layerIndex].tilemap.gameObject;
            ToolManager.SetActiveTool(tool);
            status = tool == typeof(PickingTool) ? "在Scene点选所选层的实际格子查看来源；切换图层后可点选另外组成。" : "在Scene拖刷当前层。Alt/中右键交给导航；Esc取消本笔，Ctrl+Z/Shift+Ctrl+Z撤销重做。";
        }
        private static Type CurrentBrushTool() => ToolManager.activeToolType != null && typeof(TilemapEditorTool).IsAssignableFrom(ToolManager.activeToolType)
            ? ToolManager.activeToolType : typeof(PaintTool);
        private void SaveCurrent()
        {
            if (!CanAuthorSurface()) { status = "当前上下文不可保存本轮作者数据。"; return; }
            if (brush != null) brush.FinishStroke(false);
            RestoreView();
            FrontierGroundSceneBuilder.RebuildDisplay(surface);
            var scene = surface.gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) { status = "场景没有保存路径，请先另存研究场。"; return; }
            status = EditorSceneManager.SaveScene(scene) ? "已保存同一份Tilemap作者数据与场景。" : "保存失败，已保留当前编辑。";
        }
        private void DrawSource(GroundLayer layer)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("所选组成来源", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("实际Tilemap", layer.tilemap, typeof(Tilemap), true);
            EditorGUILayout.ObjectField("绘制Tile / RuleTile", layer.paintTile, typeof(TileBase), false);
            EditorGUILayout.ObjectField("底纹来源", layer.detailTexture, typeof(Texture2D), false);
            EditorGUILayout.ObjectField("边缘遮罩来源", layer.preview, typeof(Sprite), false);
            using (new EditorGUI.DisabledScope(!CanAuthorSurface()))
            {
                float nextPeriod = EditorGUILayout.DelayedFloatField("候选世界周期（米）", layer.worldRepeatPeriod);
                if (nextPeriod != layer.worldRepeatPeriod && !float.IsNaN(nextPeriod) && !float.IsInfinity(nextPeriod))
                {
                    if (brush != null) brush.FinishStroke(false);
                    var materials = surface.layers.Select(l => l.tilemap.GetComponent<TilemapRenderer>()).Select(r => r == null ? null : r.sharedMaterial).Distinct().ToArray();
                    if (materials.Any(m => m == null || m.shader == null || m.shader.name != "Ossastra/GroundResearch/MaskWorldTile" ||
                        !AssetDatabase.GetAssetPath(m).StartsWith(GroundSourceImporter.LocalRoot + "/", StringComparison.Ordinal)))
                        status = "候选材质不属于本轮自有目录，未修改周期。";
                    else
                    {
                        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("调整候选世界纹理周期");
                        try
                        {
                            Undo.RecordObjects(materials.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[] { surface }).ToArray(), "调整候选世界纹理周期");
                            layer.worldRepeatPeriod = Mathf.Max(0.1f, nextPeriod);
                            EditorUtility.SetDirty(surface); EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                            FrontierGroundSceneBuilder.RebuildDisplay(surface); Undo.FlushUndoRecordObjects();
                            if (Undo.GetCurrentGroup() == group) Undo.CollapseUndoOperations(group);
                        }
                        catch (Exception e)
                        {
                            Undo.FlushUndoRecordObjects(); if (Undo.GetCurrentGroup() == group) Undo.RevertAllDownToGroup(group);
                            status = "周期更新未完成：" + e.Message; Debug.LogException(e);
                        }
                        finally { Undo.IncrementCurrentGroup(); }
                    }
                }
            }
            EditorGUILayout.HelpBox("该周期是本轮自有显示候选（默认16米），原作Shader精确采样尚未证实；不是原作参数结论。", MessageType.None);
            EditorGUILayout.SelectableLabel("ID: " + layer.sourceId + "\n容器: " + layer.sourceContainer + "\n对象: " + layer.sourceObjectId, GUILayout.Height(52));
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(layer.evidence) ? "原作机制证据尚未填入；不从名字猜用途。" : layer.evidence, MessageType.None);
            if (GUILayout.Button("在Project定位该Tile")) { EditorGUIUtility.PingObject(layer.paintTile); }
            if (brush == null || brush.LastPickedMap == null || !surface.Owns(brush.LastPickedMap)) return;
            var map = brush.LastPickedMap; var cell = brush.LastPickedCell;
            EditorGUILayout.LabelField("实际点选格", map.name + "  " + cell);
            EditorGUILayout.ObjectField("该格实际Tile", map.GetTile(cell), typeof(TileBase), false);
            var sprite = map.GetSprite(cell);
            EditorGUILayout.ObjectField("规则选出的实际Sprite", sprite, typeof(Sprite), false);
            if (sprite != null)
            {
                Rect preview = GUILayoutUtility.GetRect(100, 90); DrawSprite(preview, sprite);
                EditorGUILayout.LabelField("图集Rect / PPU", sprite.rect + " / " + sprite.pixelsPerUnit.ToString("0.###"));
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(sprite), GUILayout.Height(34));
            }
        }
        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) { EditorGUI.LabelField(rect, "无预览"); return; }
            Rect r = sprite.rect; var texture = sprite.texture;
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height), true);
        }
        private void SetVisible(GameObject targetObject, bool visible)
        {
            if (!CanAuthorSurface()) return;
            if (!previousVisibility.ContainsKey(targetObject)) previousVisibility[targetObject] = SceneVisibilityManager.instance.IsHidden(targetObject);
            if (visible) SceneVisibilityManager.instance.Show(targetObject, false); else SceneVisibilityManager.instance.Hide(targetObject, false);
            writtenVisibility[targetObject] = !visible; SceneView.RepaintAll();
        }
        private void SetWireView()
        {
            if (!CanAuthorSurface()) return;
            var view = SceneView.lastActiveSceneView; if (view == null) { status = "先打开Scene视图。"; return; }
            if (!ownsWireMode) { wireView = view; previousCameraMode = view.cameraMode; }
            writtenCameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.TexturedWire); wireView.cameraMode = writtenCameraMode; ownsWireMode = true;
        }
        private void RestoreView()
        {
            foreach (var pair in previousVisibility)
            {
                if (pair.Key == null || !writtenVisibility.TryGetValue(pair.Key, out bool written) || SceneVisibilityManager.instance.IsHidden(pair.Key) != written) continue;
                if (pair.Value) SceneVisibilityManager.instance.Hide(pair.Key, false); else SceneVisibilityManager.instance.Show(pair.Key, false);
            }
            previousVisibility.Clear(); writtenVisibility.Clear();
            if (ownsWireMode && wireView != null && wireView.cameraMode.Equals(writtenCameraMode)) wireView.cameraMode = previousCameraMode;
            wireView = null; ownsWireMode = false; SceneView.RepaintAll();
        }
    }
}
