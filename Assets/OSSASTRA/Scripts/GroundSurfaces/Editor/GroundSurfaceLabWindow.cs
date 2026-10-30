/* 脚本：GroundSurfaceLabWindow
 * 功能：新高台样板的中文操作与观察入口，显示实际平台/缓存统计和来源。
 * 边界：只调用Authoring编排服务；不操作源Mesh/作者位置、单Map另存或旧角色试玩复制。
 * 协作：Runtime View保管临时显示；窗口用观察版本号只释放自己发起的观察。
 */
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.GroundSurfaces.Editor
{
    public sealed class GroundSurfaceLabWindow : EditorWindow
    {
        public GroundSurfaceBinding SelectedSurface { get; private set; }
        public string LastMessage { get; private set; }
        private GroundSurfaceBinding[] bindings = Array.Empty<GroundSurfaceBinding>();
        private GroundSurfaceLabView currentView, ownedView;
        private int ownedRevision, sourceLayer, sourceTriangle, variant;
        private string sourceDescription = "";
        private MessageType noticeType;
        private Vector2 scroll;
        private static readonly string[] modeNames = { "最终效果", "裸接缝", "看结构", "拆开看", "为何裁剪" };

        [MenuItem("Tools/OSSASTRA/高台与悬崖观察")]
        public static GroundSurfaceLabWindow Open()
        { var window = GetWindow<GroundSurfaceLabWindow>("高台与悬崖"); window.minSize = new Vector2(440, 480); window.Show(); return window; }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreOwned;
            EditorApplication.quitting += RestoreOwned;
            RefreshContext();
        }
        private void OnDisable()
        {
            RestoreOwned();
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload -= RestoreOwned;
            EditorApplication.quitting -= RestoreOwned;
        }
        private void OnLostFocus() { RestoreOwned(); Repaint(); }
        private void OnPlayMode(PlayModeStateChange state) { RestoreOwned(); RefreshContext(); Repaint(); }
        private void OnSceneChanged(Scene previous, Scene next) { RestoreOwned(); SelectedSurface = null; RefreshContext(); Repaint(); }
        private void OnSceneSaving(Scene scene, string path) { if (ownedView != null && ownedView.gameObject.scene == scene) RestoreOwned(); }
        private void OnInspectorUpdate() { RefreshContext(); Repaint(); }

        private void RefreshContext()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) { bindings = Array.Empty<GroundSurfaceBinding>(); currentView = null; return; }
            var views = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GroundSurfaceLabView>(false)).ToArray();
            currentView = views.Length == 1 ? views[0] : null;
            if (EditorApplication.isPlaying)
                bindings = currentView == null || currentView.surfaces == null ? Array.Empty<GroundSurfaceBinding>() : currentView.surfaces.Where(b => b != null && b.gameObject.scene == scene).ToArray();
            else
            {
                try { bindings = GroundSurfaceAuthoring.GetBindings() ?? Array.Empty<GroundSurfaceBinding>(); }
                catch (Exception error) { bindings = Array.Empty<GroundSurfaceBinding>(); if (string.IsNullOrEmpty(LastMessage)) Notice(error.GetBaseException().Message, MessageType.Info); }
            }
            if (SelectedSurface != null && !bindings.Contains(SelectedSurface)) { RestoreOwned(); SelectedSurface = null; sourceDescription = ""; }
        }
        public bool SelectSurface(GroundSurfaceBinding binding)
        {
            RefreshContext();
            if (binding == null || !bindings.Contains(binding)) { Notice("请选择当前新Lab中实际登记的高台。", MessageType.Warning); return false; }
            if (SelectedSurface != binding)
            { RestoreOwned(); SelectedSurface = binding; sourceLayer = 0; sourceTriangle = 0; variant = binding.currentModelVariant; sourceDescription = ""; }
            Selection.activeGameObject = binding.map == null ? binding.gameObject : binding.map.gameObject;
            Repaint(); return true;
        }
        private void OnSelectionChanged()
        {
            var selected = Selection.activeGameObject; if (selected == null) return;
            var direct = selected.GetComponentInParent<GroundSurfaceBinding>();
            if (direct != null && direct != SelectedSurface && bindings.Contains(direct)) { SelectSurface(direct); return; }
            var matches = bindings.Where(binding => binding != null && (binding.sideWalls ?? Array.Empty<MeshFilter>()).Any(wall => wall != null && (wall.gameObject == selected || selected.transform.IsChildOf(wall.transform)))).ToArray();
            if (matches.Length == 1 && matches[0] != SelectedSurface) SelectSurface(matches[0]);
        }
        public bool SetObservedMode(GroundSurfaceLabViewMode mode)
        {
            RefreshContext();
            if (currentView == null || bindings.Length == 0) { Notice("当前场景缺少唯一且已登记的高台观察入口；不会猜测或接管旧场。", MessageType.Warning); return false; }
            if (mode == GroundSurfaceLabViewMode.BeforeClip && Ossastra.GroundResearch.Editor.GroundWorkshopAuthoring.HasPreview)
            { Notice("先提交或取消配方预览，再看原生未裁剪对照；不会同时开启另一套临时显示。", MessageType.Warning); return false; }
            RestoreOwned();
            if (!currentView.SetMode(mode)) { Notice(currentView.LastMessage, MessageType.Warning); return false; }
            ownedView = currentView; ownedRevision = currentView.ObservationRevision;
            Notice(currentView.LastMessage, MessageType.Info); SceneView.RepaintAll(); return true;
        }
        private void RestoreOwned()
        {
            if (ownedView != null && ownedView.ObservationRevision == ownedRevision) ownedView.Restore();
            ownedView = null; ownedRevision = -1;
        }
        private bool CanAuthor => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            PrefabStageUtility.GetCurrentPrefabStage() == null && bindings.Length > 0;
        private void Notice(string text, MessageType type = MessageType.None) { LastMessage = text; noticeType = type; }
        private void RunAuthor(Action operation)
        {
            if (!CanAuthor) { Notice("作者操作只在新Lab的Edit ready、非Prefab Stage中可用。", MessageType.Warning); return; }
            RestoreOwned();
            try { operation(); RefreshContext(); SceneView.RepaintAll(); }
            catch (Exception error) { Notice(error.GetBaseException().Message, MessageType.Error); }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("高台与悬崖", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("观察只改变临时显示，不移动碰撞、作者或角色，不暂停世界。失焦、关窗、Play和保存时释放本窗口观察。", MessageType.None);
            if (!string.IsNullOrEmpty(LastMessage)) EditorGUILayout.HelpBox(LastMessage, noticeType);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSelection(); DrawAuthoring(); DrawObservation(); DrawStatistics(); DrawDecorationIssues(); DrawSource();
            EditorGUILayout.EndScrollView();
        }
        private void DrawSelection()
        {
            var labels = new[] { "请选择平台" }.Concat(bindings.Select(binding => binding.name + " · " + ShortId(binding.SurfaceId) + " · " + Height(binding).ToString("0.###") + "m")).ToArray();
            int selected = SelectedSurface == null ? 0 : Array.IndexOf(bindings, SelectedSurface) + 1;
            int next = EditorGUILayout.Popup("当前高台", selected, labels);
            if (next > 0 && next != selected) SelectSurface(bindings[next - 1]);
            if (bindings.Length == 0) EditorGUILayout.HelpBox("先打开本轮高台Lab。旧场保留原生工坊路径；此窗口不为旧场补造注册。", MessageType.Info);
            if (SelectedSurface == null) return;
            EditorGUILayout.SelectableLabel("surfaceId  " + SelectedSurface.SurfaceId, EditorStyles.miniLabel, GUILayout.Height(18));
            if (!string.IsNullOrEmpty(SelectedSurface.lastError)) EditorGUILayout.HelpBox(SelectedSurface.lastError, MessageType.Warning);
        }
        private void DrawAuthoring()
        {
            EditorGUILayout.Space(8); EditorGUILayout.LabelField("制作当前平台", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!CanAuthor || SelectedSurface == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("重取模型上沿")) RunAuthor(() => { GroundSurfaceAuthoring.Rebuild(SelectedSurface, true); Notice("已请求从实际侧壁重新提边并重建；作者格不重置。"); });
                if (GUILayout.Button("重建顶盖")) RunAuthor(() => { GroundSurfaceAuthoring.Rebuild(SelectedSurface, false); Notice("已重建所选平台派生显示。"); });
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("刷当前平台（打开原工坊）")) RunAuthor(() => { GroundSurfaceAuthoring.SelectForPainting(SelectedSurface); Notice("已交给原生画笔；只写明确选中的平台。"); });
                var variants = SelectedSurface == null ? null : SelectedSurface.modelVariants;
                if (variants != null && variants.Length > 0)
                {
                    variant = EditorGUILayout.Popup("已保存侧壁版本", Mathf.Clamp(variant, 0, variants.Length - 1), variants.Select((mesh,index) => index + " · " + (mesh == null ? "缺失" : mesh.name)).ToArray());
                    if (GUILayout.Button("应用侧壁版本并重新接合")) RunAuthor(() => { GroundSurfaceAuthoring.UseModelVariant(SelectedSurface, variant); Notice("已请求替换实际保存模型并重取上沿；域外作者格保留。"); });
                }
            }
            using (new EditorGUI.DisabledScope(!CanAuthor))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存本样板（全部平台）")) RunAuthor(() => { GroundSurfaceAuthoring.SaveLab(); Notice("已保存整个Lab；没有使用单Map另存或角色复制入口。"); });
                if (GUILayout.Button("保存并直接试玩")) RunAuthor(() => { GroundSurfaceAuthoring.PlayLab(); Notice("已请求直接试玩整个Lab；未克隆单Map或覆盖起始场设置。"); });
                EditorGUILayout.EndHorizontal();
            }
        }
        private void DrawObservation()
        {
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("看懂墙与顶盖", EditorStyles.boldLabel);
            int current = currentView == null ? 0 : (int)currentView.Mode;
            using (new EditorGUI.DisabledScope(currentView == null || bindings.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating))
            {
                int next = GUILayout.SelectionGrid(current, modeNames, 3);
                if (next != current) SetObservedMode((GroundSurfaceLabViewMode)next);
                if (GUILayout.Button("恢复本窗口观察")) { RestoreOwned(); SceneView.RepaintAll(); }
            }
            EditorGUILayout.LabelField("观察覆盖本Lab的高台；刷地只针对上方明确选择的平台。", EditorStyles.wordWrappedMiniLabel);
        }
        private void DrawStatistics()
        {
            if (SelectedSurface == null) return;
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("实际显示与接合数据", EditorStyles.boldLabel);
            var caches = SelectedSurface.GetComponentsInChildren<GroundSurfaceMeshCache>(true).Where(cache => cache.binding == SelectedSurface).ToArray();
            if (SelectedSurface.cacheRoot != null) caches = caches.Concat(SelectedSurface.cacheRoot.GetComponentsInChildren<GroundSurfaceMeshCache>(true).Where(cache => cache.binding == SelectedSurface)).Distinct().ToArray();
            if (caches.Length != 1) { EditorGUILayout.HelpBox("尚无唯一平台缓存，先重建。这里不以空统计表示已通过。", MessageType.Info); return; }
            var cache = caches[0]; var chunks = cache.chunks ?? Array.Empty<GroundSurfaceRenderChunk>();
            var renderers = chunks.Where(chunk => chunk != null && chunk.renderer != null).Select(chunk => chunk.renderer).Distinct().ToArray();
            int materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().Count();
            EditorGUILayout.LabelField("块 / Renderer / 材质", chunks.Length + " / " + renderers.Length + " / " + materials);
            EditorGUILayout.LabelField("输出三角形 / 顶点", cache.outputTriangles + " / " + cache.outputVertices);
            EditorGUILayout.LabelField("重建次数 / 最近单次", cache.rebuildCount + " / " + cache.lastBuildMilliseconds.ToString("0.###") + " ms");
            EditorGUILayout.LabelField("连接环点 / 版本", SelectedSurface.boundary == null ? "未取得" : (SelectedSurface.boundary.rim == null ? 0 : SelectedSurface.boundary.rim.Length) + " / " + SelectedSurface.boundary.revision);
            EditorGUILayout.LabelField("单次Editor数据不代表GPU/Player性能或裸接缝已经通过。", EditorStyles.wordWrappedMiniLabel);
        }
        private void DrawSource()
        {
            if (SelectedSurface == null || SelectedSurface.map == null || SelectedSurface.map.surface == null) return;
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("定位真实来源", EditorStyles.boldLabel);
            var layers = SelectedSurface.map.surface.layers;
            if (layers == null || layers.Length == 0) return;
            sourceLayer = EditorGUILayout.Popup("外观层", Mathf.Clamp(sourceLayer,0,layers.Length-1), layers.Select((layer,index) => index + " · " + (layer == null ? "缺失" : layer.displayName)).ToArray());
            sourceTriangle = Mathf.Max(0, EditorGUILayout.IntField("该层片段三角形", sourceTriangle));
            using (new EditorGUI.DisabledScope(!CanAuthor))
                if (GUILayout.Button("查看片段的Tile / Sprite / 配方 / 模型")) RunAuthor(() => sourceDescription = GroundSurfaceAuthoring.DescribeSource(SelectedSurface, sourceLayer, sourceTriangle));
            if (!string.IsNullOrEmpty(sourceDescription)) EditorGUILayout.TextArea(sourceDescription, GUILayout.MinHeight(75));
            using (new EditorGUI.DisabledScope(true))
            {
                if (layers[sourceLayer] != null) EditorGUILayout.ObjectField("作者Tilemap", layers[sourceLayer].tilemap, typeof(UnityEngine.Tilemaps.Tilemap), true);
                foreach (var wall in SelectedSurface.sideWalls ?? Array.Empty<MeshFilter>()) if (wall != null) EditorGUILayout.ObjectField("源侧壁Mesh", wall.sharedMesh, typeof(Mesh), false);
            }
        }
        private void DrawDecorationIssues()
        {
            if (SelectedSurface == null || !CanAuthor) return;
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("装饰脚点检查", EditorStyles.boldLabel);
            try
            {
                var issues = GroundSurfaceDecorationDiagnostics.Inspect(SelectedSurface);
                EditorGUILayout.LabelField("失效 / 退让不足", issues.Length.ToString());
                if (issues.Length == 0) return;
                EditorGUILayout.HelpBox("以下实例保留原位置。选中后可手动调整，或切回模型版本；锁定与手摆物件不会自动删除或重排。", MessageType.Warning);
                foreach (var issue in issues)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    var instance = issue.instance;
                    EditorGUILayout.LabelField(instance.name + (instance.isLocked ? " · 锁定" : "") + (instance.generated ? " · 生成" : " · 手摆"));
                    EditorGUILayout.LabelField(issue.reason, EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("选中此实例（保留原位置）")) GroundSurfaceDecorationDiagnostics.Select(SelectedSurface, instance);
                    EditorGUILayout.EndVertical();
                }
            }
            catch (Exception error) { EditorGUILayout.HelpBox("脚点状态未知：" + error.GetBaseException().Message, MessageType.Warning); }
        }
        private static string ShortId(string id) { return string.IsNullOrEmpty(id) ? "未登记" : id.Substring(0, Math.Min(8,id.Length)); }
        private static float Height(GroundSurfaceBinding binding)
        { return binding.boundary == null ? binding.transform.position.y : binding.transform.TransformPoint(new Vector3(0,binding.boundary.localTopY,0)).y; }
    }
}
#endif
