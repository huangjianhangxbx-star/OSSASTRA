/*
 * 脚本：GroundWorkshopWindow
 * 功能：统一中文地图、配方、素材与帮助入口；复用原生Tilemap画笔与工坊作者服务。
 * 边界：不保存第二份格子数据、不直接写场景/配方资产、不依赖角色系统或原研究Builder。
 * 生命周期：切图、关窗、Play前完成笔画并取消自有预览；只恢复仍由本窗持有的观察与Palette状态。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch.Editor
{
    public sealed class GroundWorkshopWindow : EditorWindow
    {
        public const string HelpRootPreference = "Ossastra.GroundWorkshop.HelpRoot";
        public const string CatalogPathPreference = "Ossastra.GroundWorkshop.CatalogPath";
        private static readonly string[] Pages = { "制作地图", "地表配方", "素材", "帮助" };
        private static readonly int DebugMaskId = Shader.PropertyToID("_DebugMask");
        [SerializeField] private GroundResearchSurface surface;
        [SerializeField] private GroundTheme newMapTheme;
        [SerializeField] private int page, layerIndex, diameter = 1;
        [SerializeField] private Vector2Int mapSize = new Vector2Int(16, 12);
        [SerializeField] private string mapName = "我的地表", recipeName = "我的地表配方", themeName = "我的地表主题";
        [SerializeField] private GroundRecipe selectedRecipe;
        [SerializeField] private Texture2D draftTexture;
        [SerializeField] private GroundEdgeFamily draftFamily;
        [SerializeField] private float draftPeriod = 16f, draftCutoff = 0.05f;
        [SerializeField] private string draftDetailAssetId = "";
        [SerializeField] private int replacementScope, decorationIndex, seed = 17;
        [SerializeField] private float densityPer100 = 8f, minimumSpacing = 1f;
        [SerializeField] private bool avoidRoads = true, wholeDecorationArea = true;
        [SerializeField] private Vector2Int decorationMinimum, decorationSize = new Vector2Int(8, 8);
        [SerializeField] private string search = "";
        private Vector2 scroll;
        private string status = "新建地图，或选择已注册的工坊地图。旧研究场只读保留。";
        private MessageType statusType = MessageType.Info;
        private FrontierGroundBrush brush;
        private GridBrushBase previousBrush;
        private GameObject previousTarget, writtenTarget;
        private Type writtenTool;
        private bool decorationPointMode;
        private GroundRecipeChange pendingChange;
        private GroundWorkshopMap previewMap;
        private GroundCatalogDocument catalog;
        private string loadedCatalogPath;
        private string[] catalogKinds;
        private GroundCatalogEntry[] filteredCatalog;
        private string filteredSearch;
        private int filteredKind = -1;
        private int catalogPage, kindIndex;
        private GroundTheme themeDraftSource;
        private GroundThemeLayer[] themeDraftLayers;
        private GroundDecorationDefinition[] themeDraftDecorations;
        private GroundDecorationDefinition additionalDecoration;
        private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, bool> writtenVisibility = new Dictionary<GameObject, bool>();
        private readonly Dictionary<TilemapRenderer, MaskState> maskStates = new Dictionary<TilemapRenderer, MaskState>();
        private readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();
        private readonly Queue<string> thumbnailOrder = new Queue<string>();
        [NonSerialized] private MaterialPropertyBlock readBlock;
        private GUIStyle wrapped;

        private sealed class MaskState
        {
            public readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
            public float written;
        }

        public GroundResearchSurface SelectedSurface => surface;
        public int SelectedLayerIndex => layerIndex;
        public GroundWorkshopMap SelectedMap => surface == null ? null : surface.GetComponent<GroundWorkshopMap>();

        [MenuItem("Tools/OSSASTRA/星骸地表工坊")]
        public static void Open()
        {
            var window = GetWindow<GroundWorkshopWindow>("星骸地表工坊");
            window.minSize = new Vector2(640f, 480f);
            window.SelectActiveMapIfAvailable();
            window.Show();
        }

        private void OnEnable()
        {
            EnsureObservationBuffer();
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAuthoring;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            OnSelectionChanged();
            SelectActiveMapIfAvailable();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAuthoring;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            ReleaseAuthoring();
            foreach (var texture in thumbnails.Values) if (texture != null) DestroyImmediate(texture);
            thumbnails.Clear(); thumbnailOrder.Clear();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode) ReleaseAuthoring();
            Repaint();
        }

        private void OnSceneClosing(Scene scene, bool removingScene)
        {
            if (surface != null && surface.gameObject.scene == scene) ReleaseAuthoring();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ReleaseAuthoring(); surface = null; layerIndex = 0; SelectActiveMapIfAvailable(); Repaint();
        }

        private void SelectActiveMapIfAvailable()
        {
            if (!GroundWorkshopOwnership.ContextReady(out _)) return;
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return;
            if (surface != null && surface.gameObject.scene == active && CanAuthorSurface(out _)) return;
            var map = active.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GroundWorkshopMap>(false))
                .FirstOrDefault(candidate => candidate.surface != null && GroundWorkshopAuthoring.CanEdit(candidate, out _));
            if (map != null) SelectSurface(map.surface);
        }

        private void OnSelectionChanged()
        {
            var selected = Selection.activeGameObject;
            var candidate = selected == null ? null : selected.GetComponentInParent<GroundResearchSurface>();
            if (candidate != null && candidate != surface) SelectSurface(candidate);
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (CanAuthorSurface(out _)) GroundWorkshopAuthoring.Rebuild(SelectedMap);
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (AssetPreview.IsLoadingAssetPreviews() || brush != null && brush.LastPickedMap != null) Repaint();
        }

        public void SelectSurface(GroundResearchSurface selected)
        {
            if (surface == selected) return;
            ReleaseAuthoring(); surface = selected; layerIndex = 0;
            var map = SelectedMap;
            if (map != null && map.theme != null)
            {
                newMapTheme = map.theme;
                seed = map.theme.seed; densityPer100 = map.theme.densityPer100;
                minimumSpacing = map.theme.minimumSpacing;
            }
            ReadLayerRecipe(); Repaint();
        }

        public bool SelectLayer(int index)
        {
            if (surface == null || surface.layers == null || index < 0 || index >= surface.layers.Length || surface.layers[index] == null) return false;
            if (index == layerIndex) return true;
            Type tool = writtenTool;
            bool wasActive = brush != null && GridPaintingState.gridBrush == brush;
            ReleaseBrush(); CancelOwnedPreview(); decorationPointMode = false;
            layerIndex = index; ReadLayerRecipe();
            if (wasActive && tool != null) ActivateBrush(tool);
            Repaint(); return true;
        }

        public bool CanAuthorSurface(out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { reason = "只在Edit ready时制作；请退出试玩并等待编译/导入完成。"; return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            { reason = "Prefab模式只读；请返回已注册的工坊地图场景。"; return false; }
            if (surface == null) { reason = "请选择工坊地图，或用下方新建地图。"; return false; }
            var map = SelectedMap;
            if (map == null || map.surface != surface)
            { reason = "此地表未注册为工坊地图。旧参考场保持只读，请新建工坊地图。"; return false; }
            return GroundWorkshopAuthoring.CanEdit(map, out reason);
        }

        public bool ActivateBrush(Type tool)
        {
            if (tool != typeof(PaintTool) && tool != typeof(EraseTool) && tool != typeof(BoxTool) && tool != typeof(PickingTool))
            { Notice("请选择刷地、擦除、矩形或吸管。", MessageType.Warning); return false; }
            if (!CanAuthorSurface(out string reason)) { Notice(reason, MessageType.Warning); return false; }
            var layer = CurrentLayer;
            if (layer == null || layer.isLocked || layer.tilemap == null)
            { Notice("当前图层已锁定或缺少Tilemap；先选择未锁定的完整图层。", MessageType.Warning); return false; }
            CancelOwnedPreview(); decorationPointMode = false;
            if (brush != null && GridPaintingState.gridBrush != brush) ReleaseBrush();
            if (brush == null)
            {
                previousBrush = GridPaintingState.gridBrush; previousTarget = GridPaintingState.scenePaintTarget;
                brush = CreateInstance<FrontierGroundBrush>(); brush.name = "星骸地表工坊画笔"; brush.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!brush.Configure(surface, layerIndex, diameter))
            { ReleaseBrush(); Notice("地图/图层配置无效，未启动画笔。", MessageType.Warning); return false; }
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            if (GridPaintingState.gridBrush == brush) GridPaintingState.gridBrush = null;
            GridPaintingState.gridBrush = brush; writtenTarget = layer.tilemap.gameObject;
            GridPaintingState.scenePaintTarget = writtenTarget; writtenTool = tool;
            Selection.activeGameObject = writtenTarget; ToolManager.SetActiveTool(tool);
            Notice(tool == typeof(PickingTool) ? "在Scene点选当前层，查看实际Tile、Sprite与资源来源。" : "在Scene左键拖刷。Alt/中右键导航；Esc取消本笔；Ctrl+Z撤销整笔。");
            return true;
        }

        private void ReleaseBrush()
        {
            if (brush == null) return;
            brush.FinishStroke(false);
            // Native brush/tool setters may select another default target. Take ownership before invoking them.
            var targetBeforeRelease = GridPaintingState.scenePaintTarget;
            bool ownedBrush = GridPaintingState.gridBrush == brush;
            bool ownedTarget = targetBeforeRelease == writtenTarget;
            var targetToRestore = ownedTarget ? previousTarget : targetBeforeRelease;
            bool restoreEmptyTarget = targetToRestore == null;
            if (ownedBrush)
            {
                GridPaintingState.gridBrush = previousBrush;
                if (writtenTool != null && ToolManager.activeToolType == writtenTool) ToolManager.RestorePreviousTool();
            }
            // Never destroy a brush that a synchronous Native callback unexpectedly left active.
            if (GridPaintingState.gridBrush == brush) GridPaintingState.gridBrush = null;
            if (GridPaintingState.gridBrush == brush) return;
            DestroyImmediate(brush); brush = null;
            var currentTarget = GridPaintingState.scenePaintTarget;
            bool nativeFallback = currentTarget == null || currentTarget == writtenTarget || currentTarget == previousTarget;
            bool validTarget = restoreEmptyTarget ? targetToRestore == null : targetToRestore != null && targetToRestore.scene.IsValid() && targetToRestore.scene.isLoaded;
            // Preserve a third target installed by another callback; only repair our own known fallback values.
            if ((ownedTarget || ownedBrush) && nativeFallback && validTarget && currentTarget != targetToRestore)
                GridPaintingState.scenePaintTarget = targetToRestore;
            previousBrush = null; previousTarget = writtenTarget = null; writtenTool = null;
        }

        public void ReleaseAuthoring()
        {
            decorationPointMode = false; ReleaseBrush(); CancelOwnedPreview(); RestoreObservation();
        }

        private void CancelOwnedPreview()
        {
            if (pendingChange == null) return;
            GroundWorkshopAuthoring.CancelPreview(pendingChange); pendingChange = null; previewMap = null;
        }

        private GroundLayer CurrentLayer => surface != null && surface.layers != null && layerIndex >= 0 && layerIndex < surface.layers.Length ? surface.layers[layerIndex] : null;

        private void ReadLayerRecipe()
        {
            var map = SelectedMap;
            selectedRecipe = map != null && map.layerBindings != null && layerIndex >= 0 && layerIndex < map.layerBindings.Length && map.layerBindings[layerIndex] != null ? map.layerBindings[layerIndex].recipe : null;
            if (selectedRecipe != null) ReadRecipeDraft(selectedRecipe);
        }

        private void ReadRecipeDraft(GroundRecipe recipe)
        {
            if (recipe == null) return;
            recipeName = recipe.displayName; draftTexture = recipe.detailTexture; draftFamily = recipe.edgeFamily;
            draftPeriod = recipe.worldRepeatPeriod; draftCutoff = recipe.cutoff; draftDetailAssetId = recipe.detailAssetId;
        }

        private void Notice(string message, MessageType type = MessageType.Info) { status = message; statusType = type; Repaint(); }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { Notice(exception.GetBaseException().Message, MessageType.Warning); }
        }

        private void OnGUI()
        {
            if (wrapped == null) wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("星骸地表工坊", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("选主题 → 刷地与道路 → 布置花草 → 保存 → 试玩", wrapped);
            page = GUILayout.Toolbar(Mathf.Clamp(page, 0, Pages.Length - 1), Pages, GUILayout.Height(28));
            var selected = (GroundResearchSurface)EditorGUILayout.ObjectField("当前地图", surface, typeof(GroundResearchSurface), true);
            if (selected != surface) SelectSurface(selected);
            bool editable = CanAuthorSurface(out string reason);
            if (pendingChange != null && GroundWorkshopAuthoring.ActivePreview != pendingChange)
            { pendingChange = null; previewMap = null; Notice("预览已被其他作者操作替换或关闭；需要应用时请重新预检。", MessageType.Warning); }
            EditorGUILayout.HelpBox(editable ? "可制作：原生Tilemap是地面格子的唯一作者数据。" : reason, editable ? MessageType.Info : MessageType.Warning);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (page == 0) DrawMapPage(editable);
            else if (page == 1) DrawRecipePage(editable);
            else if (page == 2) DrawCatalogPage();
            else DrawHelpPage();
            EditorGUILayout.Space(8); EditorGUILayout.HelpBox(status, statusType);
            if (brush != null && !string.IsNullOrEmpty(brush.LastStrokeNotice)) EditorGUILayout.HelpBox(brush.LastStrokeNotice, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMapPage(bool editable)
        {
            EditorGUILayout.LabelField("新建与保存", EditorStyles.boldLabel);
            newMapTheme = (GroundTheme)EditorGUILayout.ObjectField("地表主题", newMapTheme, typeof(GroundTheme), false);
            mapName = EditorGUILayout.TextField("新地图名称", mapName);
            mapSize = EditorGUILayout.Vector2IntField("作者范围（格）", mapSize);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!GroundWorkshopOwnership.ContextReady(out _)))
                if (GUILayout.Button("使用原创教学主题")) Run(() => { newMapTheme = GroundWorkshopOriginalAssets.EnsureSamples(); Notice("已选择原创技术样本主题；不依赖原作媒体。"); });
            using (new EditorGUI.DisabledScope(newMapTheme == null || !GroundWorkshopOwnership.ContextReady(out _)))
            {
                if (GUILayout.Button("新建地图…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("新建自有工坊地图", string.IsNullOrWhiteSpace(mapName) ? "我的地表" : mapName, "unity", "请保存到GroundWorkshop/Maps或Samples；旧研究场保持只读。", GroundWorkshopOwnership.Root + "/Maps");
                    if (string.IsNullOrEmpty(path)) return;
                    ReleaseAuthoring(); var created = GroundWorkshopMaps.CreateMap(newMapTheme, path, mapSize);
                    SelectSurface(created.surface); Notice("地图已创建并保存；选择图层开始制作。");
                });
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!editable))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存当前地图")) Run(() => { ReleaseAuthoring(); GroundWorkshopMaps.Save(SelectedMap); Notice("已保存当前地图、原生Tilemap与装饰；没有保存其他用户场景。"); });
                if (GUILayout.Button("地图另存为…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("地图另存为", "我的地表副本", "unity", "请保存到GroundWorkshop/Maps或Samples，创建独立地图副本。", GroundWorkshopOwnership.Root + "/Maps");
                    if (string.IsNullOrEmpty(path)) return;
                    ReleaseAuthoring(); var copy = GroundWorkshopMaps.SaveAs(SelectedMap, path); SelectSurface(copy.surface); Notice("已另存并切换到地图副本。");
                });
                if (GUILayout.Button("主题另存副本并绑定")) Run(() => { ReleaseAuthoring(); newMapTheme = GroundWorkshopMaps.CopyTheme(SelectedMap); Notice("当前地图已绑定主题副本；其他地图与原主题保持原值。"); });
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("保存并进入角色试玩")) Run(() =>
                {
                    ReleaseAuthoring(); GroundWorkshopMaps.Save(SelectedMap);
                    if (!GroundWorkshopPlayHooks.TryPlayCurrentMap())
                        Notice("此独立插件包未安装游戏角色试玩适配；地图仍已保存，可继续制作与另存。完整项目可通过双角色工坊场试玩。", MessageType.Warning);
                    else Notice("已请求角色试玩；使用临时副本承接现有角色与镜头，退出后返回作者地图。");
                });
            }
            if (surface == null || surface.layers == null || surface.layers.Length == 0) return;
            EditorGUILayout.Space(8); EditorGUILayout.LabelField("地表与道路", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("范围", surface.EditableCells.size.x + " × " + surface.EditableCells.size.y + " 格 · " + surface.WorldArea.ToString("0.##") + " m²");
            DrawLayers(editable);
            int nextDiameter = EditorGUILayout.IntSlider("画笔边长（格）", diameter, 1, 16);
            if (nextDiameter != diameter)
            {
                diameter = nextDiameter;
                if (brush != null && editable && CurrentLayer != null && !CurrentLayer.isLocked) brush.Configure(surface, layerIndex, diameter);
            }
            using (new EditorGUI.DisabledScope(!editable || CurrentLayer == null || CurrentLayer.isLocked))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("刷地 / 道路")) ActivateBrush(typeof(PaintTool));
                if (GUILayout.Button("擦除")) ActivateBrush(typeof(EraseTool));
                if (GUILayout.Button("矩形")) ActivateBrush(typeof(BoxTool));
                if (GUILayout.Button("吸管 / 来源")) ActivateBrush(typeof(PickingTool));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("结束画笔与观察")) { ReleaseAuthoring(); Notice("已释放本窗的画笔、预览与临时观察状态。"); }
            using (new EditorGUI.DisabledScope(!editable))
            {
                if (GUILayout.Button("只刷新显示")) Run(() => { FinishCurrentStroke(); GroundWorkshopAuthoring.Rebuild(SelectedMap); Notice("已刷新显示；作者手绘未重置。"); });
            }
            EditorGUILayout.EndHorizontal();
            DrawPickedSource();
            DrawDecorations(editable);
        }

        private void DrawLayers(bool editable)
        {
            layerIndex = Mathf.Clamp(layerIndex, 0, surface.layers.Length - 1);
            for (int i = 0; i < surface.layers.Length; i++)
            {
                var layer = surface.layers[i]; if (layer == null) continue;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                Rect icon = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44));
                if (layer.detailTexture != null) GUI.DrawTexture(icon, layer.detailTexture, ScaleMode.ScaleToFit, true); else DrawSprite(icon, layer.preview);
                if (GUILayout.Toggle(layerIndex == i, string.IsNullOrEmpty(layer.displayName) ? "未命名图层" : layer.displayName, "Button", GUILayout.Height(40)) && layerIndex != i) SelectLayer(i);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    bool locked = GUILayout.Toggle(layer.isLocked, "锁定", GUILayout.Width(50));
                    if (locked != layer.isLocked)
                    {
                        int selectedIndex = i;
                        Run(() => { ReleaseBrush(); CancelOwnedPreview(); GroundWorkshopAuthoring.SetLayerLocked(SelectedMap, selectedIndex, locked); });
                    }
                    bool visible = layer.tilemap != null && !SceneVisibilityManager.instance.IsHidden(layer.tilemap.gameObject);
                    bool nextVisible = GUILayout.Toggle(visible, "Scene可见", GUILayout.Width(85));
                    if (nextVisible != visible) SetLayerVisible(i, nextVisible);
                }
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(!editable))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("只看当前层")) for (int i = 0; i < surface.layers.Length; i++) SetLayerVisible(i, i == layerIndex);
                if (GUILayout.Button("查看边缘 alpha")) SetMaskDebug(true);
                if (GUILayout.Button("恢复正常查看")) RestoreObservation();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Scene可见/alpha只用于观察，不写地图显隐。基础层擦除恢复覆盖；覆盖与道路只擦本层。", MessageType.None);
        }

        private void DrawPickedSource()
        {
            var layer = CurrentLayer; if (layer == null) return;
            EditorGUILayout.LabelField("当前组成来源", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel("assetId: " + layer.sourceId, GUILayout.Height(18));
            EditorGUILayout.ObjectField("实际Tile", layer.paintTile, typeof(TileBase), false);
            EditorGUILayout.ObjectField("底纹", layer.detailTexture, typeof(Texture2D), false);
            if (brush != null && brush.LastPickedMap != null && surface.Owns(brush.LastPickedMap))
            {
                var map = brush.LastPickedMap; var cell = brush.LastPickedCell;
                EditorGUILayout.LabelField("吸管格", map.name + " / " + cell);
                EditorGUILayout.ObjectField("该格实际Tile", map.GetTile(cell), typeof(TileBase), false);
                EditorGUILayout.ObjectField("规则选出的Sprite", map.GetSprite(cell), typeof(Sprite), false);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在Project定位Tile") && layer.paintTile != null) EditorGUIUtility.PingObject(layer.paintTile);
            if (GUILayout.Button("在素材页查看来源"))
            {
                var map = SelectedMap;
                var recipe = map != null && map.layerBindings != null && layerIndex < map.layerBindings.Length ? map.layerBindings[layerIndex]?.recipe : null;
                search = recipe != null && !string.IsNullOrEmpty(recipe.detailAssetId) ? recipe.detailAssetId : layer.sourceId ?? "";
                catalogPage = 0; page = 2;
            }
            if (GUILayout.Button("绘制规范")) OpenHelpFile("04-art-guide/素材绘制规范.html");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipePage(bool editable)
        {
            EditorGUILayout.LabelField("选择已保存配方", EditorStyles.boldLabel);
            var next = (GroundRecipe)EditorGUILayout.ObjectField("地表配方", selectedRecipe, typeof(GroundRecipe), false);
            if (next != selectedRecipe) { CancelOwnedPreview(); selectedRecipe = next; ReadRecipeDraft(next); }
            EditorGUILayout.HelpBox("配方定义底纹、边缘与世界周期；地图位置仍保存于原生Tilemap。修改以下草稿只在另存后生效，不修改共享配方。", MessageType.Info);
            recipeName = EditorGUILayout.TextField("新配方名称", recipeName);
            draftTexture = (Texture2D)EditorGUILayout.ObjectField("底纹", draftTexture, typeof(Texture2D), false);
            using (new EditorGUI.DisabledScope(!GroundWorkshopOwnership.ContextReady(out _)))
            {
                if (GUILayout.Button("导入自绘PNG底纹…")) Run(() =>
                {
                    string path = EditorUtility.OpenFilePanel("导入自有底纹PNG（16—4096像素）", "", "png");
                    if (string.IsNullOrEmpty(path)) return;
                    draftTexture = GroundWorkshopAuthoring.ImportDetailTexture(path); draftDetailAssetId = "";
                    recipeName = Path.GetFileNameWithoutExtension(path);
                    Notice("已导入受控Repeat副本；原文件不变。选择边缘族，检查平铺后保存新配方。");
                });
            }
            draftFamily = (GroundEdgeFamily)EditorGUILayout.ObjectField("已验证边缘族", draftFamily, typeof(GroundEdgeFamily), false);
            draftPeriod = EditorGUILayout.FloatField("世界重复周期（米）", draftPeriod);
            draftCutoff = EditorGUILayout.Slider("边缘 alpha 裁切", draftCutoff, 0.01f, 0.99f);
            if (draftTexture != null)
            {
                Rect preview = GUILayoutUtility.GetRect(180, 180, GUILayout.Height(180));
                float aspect = (float)draftTexture.width / Mathf.Max(1, draftTexture.height);
                float width = Mathf.Min(preview.width, preview.height * aspect);
                Rect tile = new Rect(preview.x + (preview.width - width) * .5f, preview.y, width / 3f, width / aspect / 3f);
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                    GUI.DrawTexture(new Rect(tile.x + tile.width * x, tile.y + tile.height * y, tile.width, tile.height), draftTexture, ScaleMode.StretchToFill, false);
                EditorGUILayout.LabelField("底纹3×3检查；世界尺度请在地图上预览。", wrapped);
            }
            using (new EditorGUI.DisabledScope(!GroundWorkshopOwnership.ContextReady(out _) || draftTexture == null || draftFamily == null))
            {
                if (GUILayout.Button("保存为新的自有配方…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("保存自有配方", string.IsNullOrWhiteSpace(recipeName) ? "我的配方" : recipeName, "asset", "保存独立配方，不覆盖共享配置。", GroundWorkshopOwnership.Root + "/Palettes");
                    if (string.IsNullOrEmpty(path)) return;
                    selectedRecipe = GroundWorkshopAuthoring.CreateRecipe(recipeName, draftTexture, draftFamily, draftPeriod, draftCutoff, path, draftDetailAssetId);
                    Notice("已保存独立配方；接下来选择作用范围并预览地图变化。");
                });
            }
            EditorGUILayout.Space(8); EditorGUILayout.LabelField("应用到当前层", EditorStyles.boldLabel);
            replacementScope = EditorGUILayout.Popup("作用范围", replacementScope, new[] { "仅后续：新增变体层，保留当前层旧格", "当前层已有同类地块 + 后续笔画" });
            EditorGUILayout.HelpBox(replacementScope == 0 ? "Tilemap同一层共享材质。为了让旧地面保持原样，后续模式会新增可绘制的变体层；不是在一个层中混用两种底纹。" : "只替换目标层已识别的同类Tile；不迁移手工异种Tile，不改变道路与手摆物件的位置。", MessageType.None);
            using (new EditorGUI.DisabledScope(!editable || selectedRecipe == null || CurrentLayer == null || CurrentLayer.isLocked))
            {
                if (GUILayout.Button("预检并预览影响")) Run(() =>
                {
                    FinishCurrentStroke(); CancelOwnedPreview(); RestoreObservation();
                    var change = GroundWorkshopAuthoring.PrepareReplacement(SelectedMap, layerIndex, selectedRecipe, replacementScope == 0 ? GroundReplacementScope.FutureOnly : GroundReplacementScope.ExistingAndFuture);
                    if (!change.IsValid) { Notice(change.description + "\n" + string.Join("\n", change.unknownTiles ?? Array.Empty<string>()), MessageType.Warning); return; }
                    GroundWorkshopAuthoring.Preview(change); pendingChange = change; previewMap = SelectedMap;
                    Notice(change.description + "\n已识别 " + change.knownCount + " 格；确认后才提交作者数据。");
                });
            }
            if (pendingChange != null)
            {
                EditorGUILayout.HelpBox(pendingChange.description + "\n已识别格数：" + pendingChange.knownCount, MessageType.Info);
                if (pendingChange.unknownTiles != null && pendingChange.unknownTiles.Length > 0) EditorGUILayout.HelpBox(string.Join("\n", pendingChange.unknownTiles), MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!editable || previewMap != SelectedMap))
                {
                    if (GUILayout.Button("提交这次变更")) Run(() =>
                    {
                        int newIndex = GroundWorkshopAuthoring.Commit(pendingChange); pendingChange = null; previewMap = null;
                        layerIndex = Mathf.Clamp(newIndex, 0, surface.layers.Length - 1); ReadLayerRecipe();
                        Notice("配方与实际显示已同步；Ctrl+Z撤销本次变更。请保存当前地图。");
                    });
                }
                if (GUILayout.Button("取消预览")) { CancelOwnedPreview(); Notice("已取消预览，地图保持提交前状态。"); }
                EditorGUILayout.EndHorizontal();
            }
            DrawThemeCreator();
        }

        private void DrawThemeCreator()
        {
            EditorGUILayout.Space(8); EditorGUILayout.LabelField("保存可复用主题", EditorStyles.boldLabel);
            var source = (GroundTheme)EditorGUILayout.ObjectField("主题槽位模板", newMapTheme, typeof(GroundTheme), false);
            if (source != newMapTheme) { newMapTheme = source; themeDraftSource = null; }
            if (newMapTheme == null || newMapTheme.layers == null) return;
            if (themeDraftSource != newMapTheme || themeDraftLayers == null)
            {
                themeDraftSource = newMapTheme;
                themeDraftLayers = newMapTheme.layers.Select(slot => slot == null ? null : new GroundThemeLayer { slotId = slot.slotId, displayName = slot.displayName, recipe = slot.recipe, purpose = slot.purpose, locked = slot.locked }).ToArray();
                themeDraftDecorations = (newMapTheme.decorations ?? Array.Empty<GroundDecorationDefinition>()).ToArray();
            }
            themeName = EditorGUILayout.TextField("新主题名称", themeName);
            for (int i = 0; i < themeDraftLayers.Length; i++)
            {
                var slot = themeDraftLayers[i]; if (slot == null) continue;
                EditorGUILayout.BeginHorizontal();
                slot.displayName = EditorGUILayout.TextField(slot.displayName, GUILayout.Width(130));
                slot.recipe = (GroundRecipe)EditorGUILayout.ObjectField(slot.recipe, typeof(GroundRecipe), false);
                if (i == 0) EditorGUILayout.LabelField("基础地面", GUILayout.Width(70));
                else slot.purpose = EditorGUILayout.Popup(slot.purpose == GroundLayerPurpose.Road ? 1 : 0, new[] { "覆盖", "道路" }, GUILayout.Width(70)) == 1 ? GroundLayerPurpose.Road : GroundLayerPurpose.Overlay;
                slot.locked = GUILayout.Toggle(slot.locked, "锁", GUILayout.Width(32));
                using (new EditorGUI.DisabledScope(i < 2))
                    if (GUILayout.Button("↑", GUILayout.Width(24))) { var other = themeDraftLayers[i - 1]; themeDraftLayers[i - 1] = slot; themeDraftLayers[i] = other; }
                using (new EditorGUI.DisabledScope(i == 0 || i == themeDraftLayers.Length - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(24))) { var other = themeDraftLayers[i + 1]; themeDraftLayers[i + 1] = slot; themeDraftLayers[i] = other; }
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("−", GUILayout.Width(24))) { themeDraftLayers = themeDraftLayers.Where((item, index) => index != i).ToArray(); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(themeDraftLayers.Length >= 16))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("新增覆盖槽位")) AddThemeSlot(GroundLayerPurpose.Overlay);
                if (GUILayout.Button("新增道路槽位")) AddThemeSlot(GroundLayerPurpose.Road);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("主题花草组合", EditorStyles.boldLabel);
            for (int i = 0; i < themeDraftDecorations.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                themeDraftDecorations[i] = (GroundDecorationDefinition)EditorGUILayout.ObjectField(themeDraftDecorations[i], typeof(GroundDecorationDefinition), false);
                if (GUILayout.Button("移除", GUILayout.Width(48)))
                { themeDraftDecorations = themeDraftDecorations.Where((item, index) => index != i).ToArray(); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            additionalDecoration = (GroundDecorationDefinition)EditorGUILayout.ObjectField("加入装饰", additionalDecoration, typeof(GroundDecorationDefinition), false);
            using (new EditorGUI.DisabledScope(additionalDecoration == null))
                if (GUILayout.Button("加入", GUILayout.Width(48)))
                { if (!themeDraftDecorations.Contains(additionalDecoration)) themeDraftDecorations = themeDraftDecorations.Concat(new[] { additionalDecoration }).ToArray(); additionalDecoration = null; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("主题保存资源顺序与默认花草，不保存整张地图的格子布局。这里另存新主题，不修改模板。", MessageType.None);
            using (new EditorGUI.DisabledScope(!GroundWorkshopOwnership.ContextReady(out _)))
            {
                if (GUILayout.Button("保存新主题…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("保存自有主题", themeName, "asset", "主题不含地图布局。", GroundWorkshopOwnership.Root + "/Palettes");
                    if (string.IsNullOrEmpty(path)) return;
                    newMapTheme = GroundWorkshopAuthoring.CreateTheme(themeName, themeDraftLayers, themeDraftDecorations.Where(item => item != null).ToArray(), path);
                    themeDraftSource = null; Notice("已保存新主题，可在制作地图页用于下一张地图。");
                });
            }
        }

        private void AddThemeSlot(GroundLayerPurpose purpose)
        {
            var fallback = selectedRecipe != null ? selectedRecipe : themeDraftLayers.FirstOrDefault(slot => slot != null)?.recipe;
            themeDraftLayers = themeDraftLayers.Concat(new[] { new GroundThemeLayer
            { slotId = "slot-" + Guid.NewGuid().ToString("N"), displayName = purpose == GroundLayerPurpose.Road ? "道路层" : "覆盖层", recipe = fallback, purpose = purpose } }).ToArray();
        }

        private void DrawDecorations(bool editable)
        {
            var map = SelectedMap;
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("花草与地面装饰", EditorStyles.boldLabel);
            var variants = map == null || map.theme == null ? null : map.theme.decorations;
            if (variants == null || variants.Length == 0)
            { EditorGUILayout.HelpBox("当前主题没有装饰组。可先选原创教学主题创建地图。", MessageType.None); return; }
            decorationIndex = Mathf.Clamp(decorationIndex, 0, variants.Length - 1);
            decorationIndex = EditorGUILayout.Popup("点放素材", decorationIndex, variants.Select(d => d == null ? "缺少定义" : d.displayName).ToArray());
            var definition = variants[decorationIndex];
            if (definition != null)
            {
                Rect icon = GUILayoutUtility.GetRect(80, 80, GUILayout.Height(80)); DrawSprite(icon, definition.sprite);
                EditorGUILayout.LabelField("摆放方式", definition.mode.ToString());
                EditorGUILayout.SelectableLabel("assetId: " + definition.assetId, GUILayout.Height(18));
            }
            wholeDecorationArea = EditorGUILayout.Toggle("区域：整个地图", wholeDecorationArea);
            if (!wholeDecorationArea)
            {
                decorationMinimum = EditorGUILayout.Vector2IntField("区域最小格", decorationMinimum);
                decorationSize = EditorGUILayout.Vector2IntField("区域尺寸（格）", decorationSize);
            }
            seed = EditorGUILayout.IntField("稳定种子", seed);
            densityPer100 = EditorGUILayout.FloatField("每100m²数量", densityPer100);
            minimumSpacing = EditorGUILayout.FloatField("最小间距（米）", minimumSpacing);
            avoidRoads = EditorGUILayout.Toggle("避让道路", avoidRoads);
            using (new EditorGUI.DisabledScope(!editable || definition == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(decorationPointMode ? "结束点放" : "在Scene点放"))
                {
                    ReleaseBrush(); CancelOwnedPreview(); decorationPointMode = !decorationPointMode;
                    Notice(decorationPointMode ? "Scene左键在地表范围点放；Alt/中右键导航，Esc结束。手摆物件不会被重撒或自动擦除。" : "已结束点放。");
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("用主题变体撒布此区域")) Run(() =>
                {
                    ReleaseBrush(); CancelOwnedPreview();
                    int count = GroundWorkshopDecorations.Scatter(map, variants, DecorationArea(), seed, densityPer100, minimumSpacing, avoidRoads);
                    Notice("区域撒布完成：" + count + " 个；种子/区域/配置相同时稳定，手摆与锁定项受保护。");
                });
                if (GUILayout.Button("擦除此区域生成项")) Run(() =>
                {
                    ReleaseBrush(); CancelOwnedPreview();
                    int count = GroundWorkshopDecorations.EraseGenerated(map, DecorationArea());
                    Notice("已擦除 " + count + " 个工具生成项；手摆与锁定项保留。");
                });
                EditorGUILayout.EndHorizontal();
            }
            var selected = Selection.activeGameObject;
            var instance = selected == null ? null : selected.GetComponentInParent<GroundDecorationInstance>();
            if (instance != null && map != null && map.decorationRoot != null && instance.transform.IsChildOf(map.decorationRoot))
            {
                EditorGUILayout.LabelField("当前选中装饰", instance.name + (instance.generated ? "（自动生成）" : "（手摆）"));
                using (new EditorGUI.DisabledScope(!editable))
                {
                    bool next = EditorGUILayout.Toggle("保护此物件", instance.isLocked);
                    if (next != instance.isLocked) Run(() => GroundWorkshopDecorations.SetProtected(instance, next));
                }
            }
            EditorGUILayout.HelpBox("脚点是实例根Transform；默认无玩法碰撞。更改道路后需显式重撒所选区域，不会自动重铺全图。", MessageType.None);
        }

        private BoundsInt DecorationArea()
        {
            if (surface == null) return new BoundsInt();
            if (wholeDecorationArea) return surface.EditableCells;
            return new BoundsInt(decorationMinimum.x, decorationMinimum.y, 0, decorationSize.x, decorationSize.y, 1);
        }

        private void OnSceneGUI(SceneView view)
        {
            var draggedDefinition = DragAndDrop.objectReferences.OfType<GroundDecorationDefinition>().FirstOrDefault();
            bool definitionDrag = draggedDefinition != null && Event.current != null &&
                (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform);
            if (!decorationPointMode && !definitionDrag) return;
            if (!CanAuthorSurface(out _)) { decorationPointMode = false; return; }
            var e = Event.current; if (e == null) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { decorationPointMode = false; e.Use(); Repaint(); return; }
            if (e.alt || e.button == 1 || e.button == 2 || e.control || e.command) return;
            var map = SelectedMap; var definitions = map.theme == null ? null : map.theme.decorations;
            GroundDecorationDefinition definition = definitionDrag ? draggedDefinition :
                definitions == null || decorationIndex < 0 || decorationIndex >= definitions.Length ? null : definitions[decorationIndex];
            if (definition == null) return;
            var plane = new Plane(Vector3.up, new Vector3(0f, surface.WorldBounds.center.y, 0f));
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            Vector3Int cell = surface.grid.WorldToCell(point);
            if (!surface.ContainsCell(cell)) return;
            if (definitionDrag)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Run(() => { var placed = GroundWorkshopDecorations.Place(map, definition, point, true); Selection.activeGameObject = placed.gameObject; Notice("已在当前工坊地表拖放装饰定义；实例按手摆保护。"); });
                }
                e.Use(); return;
            }
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.Repaint)
            {
                Color previous = Handles.color; Handles.color = new Color(0.4f, 1f, 0.65f, 0.9f);
                Handles.DrawWireDisc(point, Vector3.up, 0.25f); Handles.color = previous;
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Run(() => { var placed = GroundWorkshopDecorations.Place(map, definition, point, true); Selection.activeGameObject = placed.gameObject; Notice("已手摆一个装饰；可直接移动其根Transform，保存地图后保留。"); });
                e.Use(); view.Repaint();
            }
        }

        private void DrawCatalogPage()
        {
            EditorGUILayout.LabelField("同源素材目录", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选择素材manifest…"))
            {
                string path = EditorUtility.OpenFilePanel("选择工坊共用资源目录", "", "json");
                if (!string.IsNullOrEmpty(path)) { EditorPrefs.SetString(CatalogPathPreference, path); LoadCatalog(path); }
            }
            if (GUILayout.Button("打开完整离线图鉴")) OpenHelpFile("02-catalog/场景素材图鉴.html");
            EditorGUILayout.EndHorizontal();
            string preferred = EditorPrefs.GetString(CatalogPathPreference, "");
            if (catalog == null && !string.IsNullOrEmpty(preferred) && preferred != loadedCatalogPath) LoadCatalog(preferred);
            if (catalog == null) { EditorGUILayout.HelpBox("请选择本机图鉴manifest；原作媒体不会随公开插件一起分发。原创示例不依赖该目录。", MessageType.Info); return; }
            EditorGUILayout.LabelField("目录版本", catalog.catalogId + " / " + catalog.version);
            string nextSearch = EditorGUILayout.TextField("搜索名称 / assetId", search);
            if (nextSearch != search) { search = nextSearch; catalogPage = 0; }
            var entries = catalog.entries ?? Array.Empty<GroundCatalogEntry>();
            if (catalogKinds == null) catalogKinds = new[] { "全部类型" }.Concat(entries.Where(entry => entry != null).Select(entry => entry.kind ?? "未知").Distinct().OrderBy(value => value)).ToArray();
            string[] kinds = catalogKinds;
            kindIndex = Mathf.Clamp(kindIndex, 0, kinds.Length - 1);
            int nextKind = EditorGUILayout.Popup("类型", kindIndex, kinds);
            if (nextKind != kindIndex) { kindIndex = nextKind; catalogPage = 0; }
            if (filteredCatalog == null || filteredSearch != search || filteredKind != kindIndex)
            {
                filteredCatalog = entries.Where(entry => entry != null && (kindIndex == 0 || entry.kind == kinds[kindIndex]) && (string.IsNullOrWhiteSpace(search) || ((entry.displayName ?? "") + " " + entry.name + " " + entry.assetId).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                filteredSearch = search; filteredKind = kindIndex;
            }
            var filtered = filteredCatalog;
            int pages = Math.Max(1, (filtered.Length + 39) / 40); catalogPage = Mathf.Clamp(catalogPage, 0, pages - 1);
            EditorGUILayout.LabelField("筛选结果", filtered.Length + " 项 · 第 " + (catalogPage + 1) + "/" + pages + " 页");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(catalogPage == 0)) if (GUILayout.Button("上一页")) catalogPage--;
            int requestedPage = EditorGUILayout.DelayedIntField("跳到页", catalogPage + 1);
            if (requestedPage != catalogPage + 1) catalogPage = Mathf.Clamp(requestedPage - 1, 0, pages - 1);
            using (new EditorGUI.DisabledScope(catalogPage == pages - 1)) if (GUILayout.Button("下一页")) catalogPage++;
            if (GUILayout.Button("导入HTML选择集…")) Run(() =>
            {
                string path = EditorUtility.OpenFilePanel("选择图鉴导出的选择集", "", "json");
                if (string.IsNullOrEmpty(path)) return;
                var imported = GroundWorkshopCatalog.ImportSelection(catalog, path); Notice("选择集已预检并导入 " + imported.Length + " 个受控资产。");
            });
            EditorGUILayout.EndHorizontal();
            foreach (var entry in filtered.Skip(catalogPage * 40).Take(40)) DrawCatalogEntry(entry);
        }

        private void LoadCatalog(string path)
        {
            loadedCatalogPath = path; catalog = null; catalogKinds = null; filteredCatalog = null;
            Run(() => { catalog = GroundWorkshopCatalog.Load(path); catalogPage = kindIndex = 0; Notice("已载入共用目录；兼容状态由来源证据决定，不将可浏览等同已适配。"); });
        }

        private void DrawCatalogEntry(GroundCatalogEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.BeginHorizontal();
            Rect icon = GUILayoutUtility.GetRect(62, 62, GUILayout.Width(62));
            var thumbnail = Thumbnail(entry); if (thumbnail != null) GUI.DrawTexture(icon, thumbnail, ScaleMode.ScaleToFit, true);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(entry.displayName) ? entry.name : entry.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(entry.kind + " · " + entry.width + "×" + entry.height + " · " + entry.compatibility, wrapped);
            EditorGUILayout.SelectableLabel(entry.assetId + " @ " + entry.revision, GUILayout.Height(18));
            EditorGUILayout.LabelField("证据：" + entry.evidenceLevel + " · 方式：" + entry.placementMode, wrapped);
            EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("图鉴来源")) Run(() => GroundWorkshopCatalog.OpenEntry(catalog, entry));
            if (GUILayout.Button("定位本机文件")) Run(() => GroundWorkshopCatalog.RevealEntry(catalog, entry));
            using (new EditorGUI.DisabledScope(!GroundWorkshopOwnership.ContextReady(out _)))
            {
                if (GUILayout.Button("导入到工程")) Run(() =>
                {
                    UnityEngine.Object imported = GroundWorkshopCatalog.ImportEntry(catalog, entry);
                    if (imported == null) { Notice("该项目尚未适配为可导入资产；可继续在图鉴浏览。", MessageType.Warning); return; }
                    EditorGUIUtility.PingObject(imported);
                    if (imported is Texture2D texture)
                    { draftTexture = texture; draftDetailAssetId = entry.assetId; recipeName = string.IsNullOrEmpty(entry.displayName) ? entry.name : entry.displayName; page = 1; }
                    else if (imported is GroundDecorationDefinition definition) { additionalDecoration = definition; page = 1; }
                    Notice("已导入受控资产；底纹会填入配方草稿，保存新配方后再应用到地图。");
                });
            }
            EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
        }

        private Texture2D Thumbnail(GroundCatalogEntry entry)
        {
            // 不以原图代替缺失缩略图：压缩文件很小也可能解码成大幅纹理。
            if (string.IsNullOrEmpty(entry.thumbnailPath)) return null;
            string path;
            try { path = GroundWorkshopCatalog.ResolveMedia(catalog, entry, true); }
            catch { return null; }
            if (string.IsNullOrEmpty(path)) return null;
            if (thumbnails.TryGetValue(path, out var cached)) return cached;
            Texture2D texture = null;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length <= 2 * 1024 * 1024)
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (!texture.LoadImage(File.ReadAllBytes(path), true) || texture.width > 512 || texture.height > 512)
                    { DestroyImmediate(texture); texture = null; }
                }
            }
            catch { if (texture != null) DestroyImmediate(texture); texture = null; }
            thumbnails[path] = texture; thumbnailOrder.Enqueue(path);
            while (thumbnailOrder.Count > 80)
            {
                string old = thumbnailOrder.Dequeue();
                if (thumbnails.TryGetValue(old, out var oldTexture) && oldTexture != null) DestroyImmediate(oldTexture);
                thumbnails.Remove(old);
            }
            return texture;
        }

        private void DrawHelpPage()
        {
            EditorGUILayout.LabelField("从这里开始", EditorStyles.boldLabel);
            if (GUILayout.Button("开始使用与交付状态")) OpenHelpFile("开始使用.html");
            if (GUILayout.Button("地面组合逻辑教程")) OpenHelpFile("03-tutorial/地面组合逻辑教程.html");
            if (GUILayout.Button("素材绘制规范与模板")) OpenHelpFile("04-art-guide/素材绘制规范.html");
            if (GUILayout.Button("离线素材图鉴")) OpenHelpFile("02-catalog/场景素材图鉴.html");
            if (GUILayout.Button("打开双角色工坊场（完整游戏项目）")) Run(() =>
            {
                ReleaseAuthoring();
                if (!GroundWorkshopPlayHooks.TryOpenGameplayLab())
                    Notice("此独立插件包未安装游戏试玩适配；原创地图制作与保存可独立使用。", MessageType.Warning);
            });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("设置本机资料根目录…"))
            {
                string selected = EditorUtility.OpenFolderPanel("选择包含开始使用.html的KT-GROUND-02目录", EditorPrefs.GetString(HelpRootPreference, ""), "");
                if (!string.IsNullOrEmpty(selected)) EditorPrefs.SetString(HelpRootPreference, selected);
            }
            if (GUILayout.Button("在Project定位当前地图") && surface != null) { Selection.activeGameObject = surface.gameObject; EditorGUIUtility.PingObject(surface.gameObject); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.SelectableLabel(EditorPrefs.GetString(HelpRootPreference, "尚未设置本机资料根"), GUILayout.Height(34));
            EditorGUILayout.HelpBox("优先打开本机资料根中的实际文件；未提供时使用项目Documentation内的自有手册。参考图鉴与本机交付状态不随纯插件包分发。", MessageType.None);
            EditorGUILayout.HelpBox("操作：Scene左键刷地，Alt/中右键导航，Esc取消本笔，Ctrl+Z/Shift+Ctrl+Z撤销重做。花草点放Esc退出。Play期间工具只读；退出试玩继续同一份地图。", MessageType.Info);
            EditorGUILayout.HelpBox("原作已核实：指定地图原生Tilemap、RuleTile与素材引用。UNITY_ADAPTATION：本工程world-XZ底纹、候选周期、自有Unlit与工坊。原Shader精确公式、完整光照与原作者编辑器仍UNKNOWN。", MessageType.None);
            EditorGUILayout.HelpBox("公开插件与原创技术样本可独立运行；参考素材、裁片、图鉴媒体与含参考截图只在本机。原创技术样本用于兼容验证，不代表正式美术风格。", MessageType.None);
        }

        public bool OpenHelpFile(string relativePath)
        {
            if (!TryResolveHelpPath(relativePath, out string fullPath))
            {
                bool catalogRequest = (relativePath ?? "").Replace('\\', '/').StartsWith("02-catalog/", StringComparison.OrdinalIgnoreCase);
                Notice(catalogRequest ? "本机参考图鉴未提供；公开插件只包含自有教程与绘制规范。有本机资源库时，请在帮助页设置资料根目录。" :
                    "本机资料根和项目Documentation均未提供此文件：" + relativePath + "。纯插件可直接打开教程与绘制规范；本机交付状态需另设资料根。", MessageType.Warning);
                return false;
            }
            try
            {
                Application.OpenURL(new Uri(fullPath).AbsoluteUri); return true;
            }
            catch (Exception exception) { Notice(exception.Message, MessageType.Warning); return false; }
        }

        /// <summary>实际文档导航路径：本机资料优先，随后项目内自有Documentation；不猜空目录或访问任意绝对路径。</summary>
        public bool TryResolveHelpPath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return false;
            string projectDocumentation = Path.GetFullPath(Path.Combine(Application.dataPath, "..", GroundWorkshopOwnership.Root, "Documentation"));
            foreach (string root in new[] { EditorPrefs.GetString(HelpRootPreference, ""), projectDocumentation })
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                try
                {
                    string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
                    if (candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                    { fullPath = candidate; return true; }
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
            }
            return false;
        }

        public bool SetLayerVisible(int index, bool visible)
        {
            if (!CanAuthorSurface(out _) || surface.layers == null || index < 0 || index >= surface.layers.Length) return false;
            var layer = surface.layers[index];
            if (layer == null || layer.tilemap == null || !surface.Owns(layer.tilemap)) return false;
            var target = layer.tilemap.gameObject;
            if (!previousVisibility.ContainsKey(target)) previousVisibility[target] = SceneVisibilityManager.instance.IsHidden(target);
            if (visible) SceneVisibilityManager.instance.Show(target, false); else SceneVisibilityManager.instance.Hide(target, false);
            writtenVisibility[target] = !visible; GroundWorkshopDisplayEvents.Notify(SelectedMap); SceneView.RepaintAll(); return true;
        }

        public bool SetMaskDebug(bool enabled)
        {
            if (!CanAuthorSurface(out _)) return false;
            EnsureObservationBuffer();
            foreach (var layer in surface.layers)
            {
                if (layer == null || layer.tilemap == null || !surface.Owns(layer.tilemap)) continue;
                var renderer = layer.tilemap.GetComponent<TilemapRenderer>(); if (renderer == null) continue;
                if (!maskStates.TryGetValue(renderer, out var state))
                { state = new MaskState(); renderer.GetPropertyBlock(state.original); maskStates[renderer] = state; }
                renderer.GetPropertyBlock(readBlock); state.written = enabled ? 1f : 0f;
                readBlock.SetFloat(DebugMaskId, state.written); renderer.SetPropertyBlock(readBlock);
            }
            GroundWorkshopDisplayEvents.Notify(SelectedMap); SceneView.RepaintAll(); return true;
        }

        private void RestoreObservation()
        {
            EnsureObservationBuffer();
            foreach (var pair in previousVisibility)
            {
                if (pair.Key == null || !writtenVisibility.TryGetValue(pair.Key, out bool written) || SceneVisibilityManager.instance.IsHidden(pair.Key) != written) continue;
                if (pair.Value) SceneVisibilityManager.instance.Hide(pair.Key, false); else SceneVisibilityManager.instance.Show(pair.Key, false);
            }
            previousVisibility.Clear(); writtenVisibility.Clear();
            foreach (var pair in maskStates)
            {
                if (pair.Key == null) continue;
                pair.Key.GetPropertyBlock(readBlock);
                if (Mathf.Approximately(readBlock.GetFloat(DebugMaskId), pair.Value.written)) pair.Key.SetPropertyBlock(pair.Value.original.isEmpty ? null : pair.Value.original);
            }
            maskStates.Clear(); GroundWorkshopDisplayEvents.Notify(SelectedMap); SceneView.RepaintAll();
        }

        private void EnsureObservationBuffer()
        {
            // EditorWindow创建/反序列化后非序列化的原生缓冲可能为空；在实际使用的主线程入口创建。
            if (readBlock == null) readBlock = new MaterialPropertyBlock();
        }

        private void FinishCurrentStroke() { if (brush != null) brush.FinishStroke(false); }
        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) { EditorGUI.LabelField(rect, "无预览"); return; }
            var texture = AssetPreview.GetAssetPreview(sprite);
            if (texture != null) { GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true); return; }
            Rect r = sprite.rect; float aspect = r.width / Mathf.Max(1f, r.height);
            var draw = rect;
            if (draw.width / Mathf.Max(1f, draw.height) > aspect) { draw.width = draw.height * aspect; draw.x += (rect.width - draw.width) * 0.5f; }
            else { draw.height = draw.width / aspect; draw.y += (rect.height - draw.height) * 0.5f; }
            GUI.DrawTextureWithTexCoords(draw, sprite.texture, new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height), true);
        }
    }
}
