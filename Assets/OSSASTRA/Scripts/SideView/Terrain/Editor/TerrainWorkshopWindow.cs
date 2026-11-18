/*
 * 原生地形工坊，连接素材校准、Scene 画布、图层和片段检查。
 * 所有提交经同一地图事务并重建派生结果。拖动期间只处理临时副本。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed partial class TerrainWorkshopWindow : EditorWindow
    {
        private enum Brush { Paint, Erase, Rectangle, Line, Material, Select, Inspect, Stamp }
        private static readonly string[] BrushLabels = { "画", "擦", "矩形", "直线", "换材", "选区", "检查", "盖章" };
        private static readonly string[] Tabs = { "素材与校准", "图层", "检查与问题", "帮助" };
        [SerializeField] private TerrainWorkshopRoot root;
        [SerializeField] private TerrainMap map;
        [SerializeField] private TerrainKit kit;
        [SerializeField] private TerrainLayerKind stampLayer = TerrainLayerKind.Near;
        [SerializeField] private Brush brush;
        private TerrainWorkshopShelf shelf;
        private TerrainSolution result;
        private TerrainPart picked;
        private TerrainIssue issue;
        private Vector2 scroll;
        private int tab, brushWidth = 1;
        private bool captureBrush, showGuides, showCollision;
        private string status, action;

        [MenuItem("OSSASTRA/地形工坊/打开工坊")]
        public static TerrainWorkshopWindow Open()
        {
            var window = GetWindow<TerrainWorkshopWindow>("星骸·横版地形工坊");
            window.minSize = new Vector2(420, 330);
            return window;
        }

        private void OnEnable()
        {
            shelf = new TerrainWorkshopShelf();
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            if (root == null) Connect(TerrainWorkshopScenes.Find(SceneManager.GetActiveScene()));
            else Refresh(false);
        }

        private void OnDisable()
        {
            shelf?.EndDrag(true, null);
            CancelStroke();
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private void OnLostFocus() { if (EditorApplication.isPlayingOrWillChangePlaymode) CancelStroke(); }
        private void OnPlayMode(PlayModeStateChange state)
        {
            shelf?.EndDrag(true, null);
            CancelStroke();
            if (state == PlayModeStateChange.EnteredEditMode) Connect(TerrainWorkshopScenes.Find(SceneManager.GetActiveScene()));
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => Connect(TerrainWorkshopScenes.Find(scene));

        private void Connect(TerrainWorkshopRoot next)
        {
            CancelStroke();
            status = null;
            root = next;
            if (root != null) map = root.map;
            picked = null;
            issue = null;
            if (map != null && !map.kits.Contains(kit)) kit = map.kits.FirstOrDefault(item => item != null);
            Refresh(false);
        }

        private void OnUndoRedo()
        {
            if (root != null) map = root.map;
            if (map != null && !map.kits.Contains(kit)) kit = map.kits.FirstOrDefault(item => item != null);
            picked = null;
            issue = null;
            Refresh(true);
        }

        private void Refresh(bool bake)
        {
            trial = null;
            draft = null;
            if (map != null)
            {
                try
                {
                    result = bake && CanEdit && !root.frozen ? TerrainWorkshopBake.Rebuild(root) : TerrainAssembly.Solve(map);
                }
                catch (Exception error)
                {
                    status = error.Message;
                    Debug.LogException(error);
                }
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private bool CanEdit => root != null && map != null && root.map == map && !root.locked &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("横版地形工坊", EditorStyles.boldLabel);
            DrawFiles();
            if (map == null)
            {
                EditorGUILayout.HelpBox("打开示例查看配好的材料和场景，或新建地图。", MessageType.Info);
                return;
            }
            bool changed = EditorUtility.IsDirty(map) || (map.library != null && EditorUtility.IsDirty(map.library)) ||
                map.kits.Any(item => item != null && EditorUtility.IsDirty(item)) ||
                (root != null && root.gameObject.scene.isDirty);
            EditorGUILayout.LabelField(map.name + (changed ? " · 有未保存修改" : " · 已保存"), EditorStyles.miniBoldLabel);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            if (root == null) EditorGUILayout.HelpBox("地图资产已选中。连接带工坊地形根的场景，或新建试玩场后即可在 Scene 里绘制。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                brush = (Brush)GUILayout.Toolbar((int)brush, BrushLabels, EditorStyles.miniButton);
                using (new EditorGUILayout.HorizontalScope())
                {
                    captureBrush = GUILayout.Toggle(captureBrush, "启用 Scene 画笔", EditorStyles.miniButton);
                    showGuides = GUILayout.Toggle(showGuides, "边界", EditorStyles.miniButton);
                    showCollision = GUILayout.Toggle(showCollision, "碰撞", EditorStyles.miniButton);
                    if (GUILayout.Button("正视", EditorStyles.miniButton)) FrameScene();
                    brushWidth = EditorGUILayout.IntSlider(brushWidth, 1, 7, GUILayout.MinWidth(45));
                }
            }
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawMaterials();
            if (tab == 1) DrawLayers();
            if (tab == 2) DrawInspection();
            if (tab == 3) DrawHelp();
            EditorGUILayout.EndScrollView();
        }

        private void DrawFiles()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开示例")) Later(() => { Connect(TerrainWorkshopScenes.OpenExample()); FrameScene(); });
                    if (GUILayout.Button("新建地图")) Guard(NewMap);
                    if (GUILayout.Button("连接当前地图")) Guard(BindCurrent);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(map == null))
                    {
                        if (GUILayout.Button("保存")) Guard(() => { if (TerrainWorkshopScenes.Save(CanEdit ? root : null, map)) status = "地图和套件已保存。"; });
                        if (GUILayout.Button("新建试玩场")) Later(NewScene);
                    }
                    if (GUILayout.Button("试玩")) Guard(() => status = TerrainWorkshopScenes.Play(root));
                    if (GUILayout.Button("帮助", GUILayout.Width(46))) tab = 3;
                }
            }
        }

        private void NewMap()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建横版地图", "新地形地图", "asset", "选择作者地图的位置。");
            if (string.IsNullOrEmpty(path)) return;
            var next = TerrainWorkshopScenes.CreateMap(path, map ?? AssetDatabase.LoadAssetAtPath<TerrainMap>(TerrainWorkshopScenes.ExampleFolder + "/高台遗址.asset"));
            if (next == null) { status = "无法新建地图。请选择未使用的资产名。"; return; }
            if (CanEdit && EditorUtility.DisplayDialog("连接新地图", "将当前工坊场连接到刚创建的空白地图？原地图资产仍可重新连接。", "连接", "仅创建"))
                Bind(next);
            else Connect(null);
            map = next;
            kit = map.kits.FirstOrDefault();
            Refresh(false);
        }

        private void BindCurrent()
        {
            var next = TerrainWorkshopScenes.Find(SceneManager.GetActiveScene());
            if (next == null) { status = "当前场没有工坊地形根。请新建试玩场。"; return; }
            Connect(next);
            status = next.locked ? "当前地形根为只读。" : "已连接场景地形根。";
        }

        private void Bind(TerrainMap next)
        {
            if (root == null || root.locked || next == null) return;
            Undo.RecordObject(root, "连接地图");
            root.map = next;
            picked = null;
            issue = null;
            map = next;
            Refresh(true);
        }

        private void NewScene()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建横版试玩场", "SC_TerrainWorkshop", "unity", "选择新场景的位置。");
            if (string.IsNullOrEmpty(path)) return;
            var next = TerrainWorkshopScenes.CreatePlayScene(map, path);
            if (next != null) { Connect(next); FrameScene(); }
        }

        private void DrawMaterials()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var chosenMap = (TerrainMap)EditorGUILayout.ObjectField("作者地图", map, typeof(TerrainMap), false);
                if (chosenMap != null && chosenMap != map && !CanEdit)
                {
                    map = chosenMap;
                    kit = map.kits.FirstOrDefault();
                    Refresh(false);
                }
                else if (chosenMap != null && chosenMap != map && CanEdit)
                {
                    Bind(chosenMap);
                    kit = map.kits.FirstOrDefault();
                    Refresh(false);
                }
                var chosenKit = (TerrainKit)EditorGUILayout.ObjectField("当前套件", kit, typeof(TerrainKit), false);
                if (chosenKit != kit && chosenKit != null)
                {
                    if (!map.kits.Contains(chosenKit)) Edit("登记套件", () => map.kits = map.kits.Concat(new[] { chosenKit }).ToArray());
                    kit = chosenKit;
                    trial = null;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (map.kits.Length > 1)
                    {
                        var available = map.kits.Where(item => item != null).ToArray();
                        int index = EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(available, kit)), available.Select(item => item.name).ToArray());
                        if (index >= 0 && index < available.Length && kit != available[index]) { kit = available[index]; trial = null; }
                    }
                    if (GUILayout.Button("复制套件", GUILayout.Width(78))) Guard(CopyKit);
                    if (GUILayout.Button("保存套件", GUILayout.Width(78)) && kit != null) AssetDatabase.SaveAssetIfDirty(kit);
                    if (GUILayout.Button("读取已保存套件", GUILayout.Width(104)) && kit != null)
                    {
                        var path = AssetDatabase.GetAssetPath(kit);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        Refresh(true);
                    }
                }
                var library = (TerrainLibrary)EditorGUILayout.ObjectField("素材库", map.library, typeof(TerrainLibrary), false);
                if (library != map.library) Edit("设置素材库", () => map.library = library);
                using (new EditorGUI.DisabledScope(root != null && root.locked))
                {
                    shelf.DrawLibrary(map.library, kit, () => Refresh(true));
                    shelf.DrawCalibration(kit, () => Refresh(true), () => { trial = null; Repaint(); });
                }
                DrawMapUsages();
                DrawTrial();
            }
        }

        private TerrainSolution usageSolution;
        private TerrainArt usageArt;
        private TerrainPart[] usages = Array.Empty<TerrainPart>();
        private int usageIndex;

        private void DrawMapUsages()
        {
            if (root == null || result == null || shelf.Item == null) return;
            if (usageSolution != result || usageArt != shelf.Item)
            {
                usageSolution = result;
                usageArt = shelf.Item;
                usages = result.Parts.Where(part => part.Art == usageArt).ToArray();
                usageIndex = 0;
            }
            EditorGUILayout.LabelField($"当前地图使用「{usageArt.name}」共 {usages.Length} 处");
            if (usages.Length == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一处")) usageIndex = (usageIndex + usages.Length - 1) % usages.Length;
                GUILayout.Label($"{usageIndex + 1} / {usages.Length}");
                if (GUILayout.Button("下一处")) usageIndex = (usageIndex + 1) % usages.Length;
                if (GUILayout.Button("定位"))
                {
                    picked = usages[usageIndex];
                    issue = null;
                    var view = SceneView.lastActiveSceneView;
                    if (view != null)
                    {
                        view.pivot = root.transform.TransformPoint(picked.Bounds.center);
                        view.Repaint();
                    }
                }
            }
            EditorGUILayout.LabelField(usages[usageIndex].Key, EditorStyles.wordWrappedMiniLabel);
        }

        private void CopyKit()
        {
            if (kit == null) { status = "先选择一个套件。"; return; }
            string path = EditorUtility.SaveFilePanelInProject("复制套件", kit.name + "副本", "asset", "为本地图建立独立套件。");
            if (string.IsNullOrEmpty(path)) return;
            var next = TerrainWorkshopScenes.CopyKit(kit, path);
            if (next == null) return;
            Edit("登记套件副本", () => map.kits = map.kits.Concat(new[] { next }).ToArray());
            kit = next;
            trial = null;
        }

        private void DrawLayers()
        {
            foreach (var layer in map.layers)
            {
                using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = EditorGUILayout.DelayedTextField(layer.label);
                    bool visible = GUILayout.Toggle(layer.visible, "显示", GUILayout.Width(48));
                    bool locked = GUILayout.Toggle(layer.locked, "锁定", GUILayout.Width(48));
                    bool solo = GUILayout.Toggle(layer.solo, "独显", GUILayout.Width(48));
                    if (label != layer.label || visible != layer.visible || locked != layer.locked || solo != layer.solo)
                        Edit("调整图层", () => { layer.label = label; layer.visible = visible; layer.locked = locked; layer.solo = solo; });
                }
                using (new EditorGUI.DisabledScope(layer.locked))
                {
                    var library = (TerrainLibrary)EditorGUILayout.ObjectField("图层素材库", layer.library, typeof(TerrainLibrary), false);
                    var art = (TerrainArt)EditorGUILayout.ObjectField("指定底层图件", layer.art, typeof(TerrainArt), false);
                    var order = EditorGUILayout.DelayedIntField("绘制顺序", layer.order);
                    float parallax = EditorGUILayout.DelayedFloatField("视差", layer.parallax);
                    float light = EditorGUILayout.Slider("受光比例", layer.light, 0, 1);
                    if (library != layer.library || art != layer.art || order != layer.order || parallax != layer.parallax || light != layer.light)
                        Edit("配置图层", () => { layer.library = library; layer.art = art; layer.order = order; layer.parallax = parallax; layer.light = light; });
                }
                if (GUILayout.Button("在此层盖章", EditorStyles.miniButton))
                { stampLayer = layer.kind; brush = Brush.Stamp; captureBrush = true; tab = 0; }
            }
        }

        private void DrawInspection()
        {
            if (picked == null && issue == null) EditorGUILayout.HelpBox("启用“检查”工具，再点击 Scene 中的饰边。", MessageType.Info);
            if (picked != null)
            {
                EditorGUILayout.LabelField(TerrainWorkshopShelf.Label(picked.Slot), EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(Describe(), EditorStyles.textArea, GUILayout.Height(96));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开对应规则"))
                    {
                        kit = map.Kit(picked.Kit);
                        shelf.Slot = picked.Slot;
                        if (picked.Art != null) shelf.Select(picked.Art);
                        tab = 0;
                    }
                    using (new EditorGUI.DisabledScope(!CanEdit || picked.Slot == TerrainSlot.Fill))
                    {
                        if (GUILayout.Button("锁定此段")) Override(picked.Art, true);
                        if (GUILayout.Button("返回自动")) Override(null, false);
                    }
                }
                using (new EditorGUI.DisabledScope(!CanEdit || picked.Slot == TerrainSlot.Fill))
                {
                    var replacement = (TerrainArt)EditorGUILayout.ObjectField("只替换此段", picked.Art, typeof(TerrainArt), false);
                    if (replacement != picked.Art && replacement != null) Override(replacement, picked.Locked);
                }
            }
            if (issue != null)
            {
                EditorGUILayout.HelpBox(issue.Message, MessageType.Warning);
                if (GUILayout.Button("校准缺失槽"))
                {
                    kit = map.Kit(issue.Kit);
                    shelf.Slot = issue.Slot;
                    tab = 0;
                }
                if (CanEdit && map.overrides.Any(item => item.key == issue.Key) && GUILayout.Button("解除冲突覆盖"))
                {
                    string key = issue.Key;
                    Edit("解除冲突覆盖", () => map.overrides.RemoveAll(item => item.key == key));
                    issue = null;
                }
            }
            if ((issue != null || picked != null) && GUILayout.Button("复制问题描述")) EditorGUIUtility.systemCopyBuffer = Describe();
            if (result == null) return;
            EditorGUILayout.LabelField($"装配问题 · {result.Issues.Count}", EditorStyles.boldLabel);
            foreach (var warning in result.Issues.Take(30))
                if (GUILayout.Button(warning.Message, EditorStyles.miniButton)) { issue = warning; picked = null; SceneView.RepaintAll(); }
        }

        private string Describe()
        {
            if (picked == null) return $"{map.name} | {issue?.Key}\n{issue?.Message}\n上次操作 {action}";
            var rule = picked.Rule;
            string measures = rule == null ? "" : $"\n跨度 {rule.span:F2}  间距 {rule.spacing:F2}  偏移 {rule.offset:F2}  接点 {rule.start:F2}–{rule.end:F2}";
            return $"{map.name} | {picked.Key}\n套件 {map.Kit(picked.Kit)?.name} | {TerrainWorkshopShelf.Label(picked.Slot)} | {picked.Reason}\n" +
                $"图件 {picked.Art?.name}  {picked.Art?.origin} | 锁定 {picked.Locked}{measures}\n上次操作 {action}";
        }

        private void Override(TerrainArt art, bool locked)
        {
            var part = picked;
            if (part == null) return;
            Edit("修改片段覆盖", () =>
            {
                map.overrides.RemoveAll(item => item.key == part.Key);
                if (art != null || locked)
                {
                    var snapshot = locked && part.Rule != null ? JsonUtility.FromJson<TerrainRule>(JsonUtility.ToJson(part.Rule)) : null;
                    map.overrides.Add(new TerrainOverride { key = part.Key, art = art, locked = locked, rule = snapshot });
                }
            });
            picked = result?.Parts.FirstOrDefault(item => item.Key == part.Key);
        }

        private void DrawHelp()
        {
            EditorGUILayout.HelpBox("先选套件和绘制工具。Scene 左键绘制，Shift 临时擦除，Esc 取消当前笔，Alt 按原方式导航。检查工具会定位画片和规则。", MessageType.Info);
            EditorGUILayout.LabelField("选区", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("拖框选择。拖动选区内部可移动，Ctrl 拖动复制，Delete 删除。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("冻结显示", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("冻结后保留当前网格与碰撞，可关闭窗口后试玩。解冻后继续编辑。", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(root == null || EditorApplication.isPlayingOrWillChangePlaymode || (root != null && root.locked)))
                if (GUILayout.Button(root != null && root.frozen ? "解冻并刷新" : "冻结当前显示"))
                {
                    Undo.RecordObject(root, "冻结地形显示");
                    root.frozen = !root.frozen;
                    EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                    if (!root.frozen) Refresh(true);
                }
        }

        private void Edit(string label, Action apply)
        {
            if (map == null || EditorApplication.isPlayingOrWillChangePlaymode || (root != null && root.locked)) return;
            Undo.RecordObject(map, label);
            apply();
            EditorUtility.SetDirty(map);
            action = label;
            Refresh(true);
        }

        private void Guard(Action run)
        {
            try { status = null; run(); }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { status = error.Message; Debug.LogException(error); }
        }

        private void Later(Action run)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Guard(run);
                Repaint();
            };
        }

        private void FrameScene()
        {
            var scene = SceneView.lastActiveSceneView;
            if (scene == null) return;
            scene.LookAt(new Vector3(2, 3, 0), Quaternion.identity, 13, true, true);
            scene.in2DMode = true;
            scene.Repaint();
        }
    }
}
