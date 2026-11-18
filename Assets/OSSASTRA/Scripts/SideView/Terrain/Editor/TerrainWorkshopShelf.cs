/* 素材架提供筛选、裁片预览和套件校准交互。接点拖动只预览，松手后由工坊提交派生重建。 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed class TerrainWorkshopShelf
    {
        private const string FavoriteKey = "Ossastra.TerrainWorkshop.Favorites";
        private static readonly string[] StateNames = { "未分类", "候选", "工具已验", "用户确认" };
        private static readonly TerrainSlot[] Slots = (TerrainSlot[])Enum.GetValues(typeof(TerrainSlot));
        private static readonly Color StartColor = new Color(.36f, .83f, .83f);
        private static readonly Color EndColor = new Color(.54f, .73f, .99f);
        private static readonly Color AnchorColor = new Color(1f, .73f, .37f);
        private readonly HashSet<string> favorites = new HashSet<string>(
            EditorPrefs.GetString(FavoriteKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        private string query = "", theme = "", purpose = "", origin = "";
        private string importMessage;
        private int stateFilter, previewBackground;
        private bool libraryExpanded = true, onlyFavorites, filtersExpanded, sourceExpanded;
        private bool cropMode, detailsExpanded;
        private TerrainKit[] referencedKits;
        private float tileWidth = 76;
        private Vector2 scroll;
        private Vector2 cropStart, cropEnd;
        private int cropControl;
        private TerrainGuideHandle activeHandle;
        private TerrainKit dragKit;
        private TerrainRule dragRule;
        private int dragControl, dragUndoGroup = -1;

        public TerrainArt Item { get; private set; }
        public TerrainSlot Slot { get; set; } = TerrainSlot.Top;
        public float PanelWidth { get; set; }

        public void Select(TerrainArt art)
        {
            if (art != Item) referencedKits = null;
            Item = art;
        }

        public void DrawLibrary(TerrainLibrary library, TerrainKit kit, Action changed)
        {
            libraryExpanded = EditorGUILayout.Foldout(libraryExpanded, "素材库", true, EditorStyles.foldoutHeader);
            if (!libraryExpanded) return;
            if (library == null)
            {
                EditorGUILayout.HelpBox("选择素材库即可浏览缩略图，也可从 Project 将原创 Sprite 拖入校准槽。", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                query = EditorGUILayout.TextField(query, EditorStyles.toolbarSearchField);
                onlyFavorites = GUILayout.Toggle(onlyFavorites, "收藏", EditorStyles.toolbarButton, GUILayout.Width(43));
                filtersExpanded = GUILayout.Toggle(filtersExpanded, "筛选", EditorStyles.toolbarButton, GUILayout.Width(43));
            }
            var items = library.items ?? Array.Empty<TerrainArt>();
            if (filtersExpanded)
            {
                theme = Filter("主题", theme, items, item => item.theme);
                purpose = Filter("用途", purpose, items, item => item.purpose);
                origin = Filter("来源", origin, items, item => item.origin);
                stateFilter = EditorGUILayout.Popup("校准状态", stateFilter,
                    new[] { "全部", "当前套件未使用", "当前套件候选", "当前套件已验", "当前套件已确认" });
                tileWidth = EditorGUILayout.Slider("缩略图", tileWidth, 56, 110);
            }
            var usedHere = new HashSet<TerrainArt>((kit?.rules ?? Array.Empty<TerrainRule>())
                .Where(rule => rule != null && rule.art != null).Select(rule => rule.art));
            var visible = items.Where(item => Matches(item, usedHere, kit)).ToArray();
            if (visible.Length == 0)
                EditorGUILayout.HelpBox("没有符合条件的素材。可清除筛选，或从 Project 添加原创图片。", MessageType.Info);
            else
                DrawGrid(visible);

            if (GUILayout.Button("添加 Project 中选中的原创 Sprite／图片／文件夹"))
                importMessage = TerrainWorkshopArtImport.ImportSelection(library, Select);
            if (!string.IsNullOrEmpty(importMessage))
                EditorGUILayout.LabelField(importMessage, EditorStyles.wordWrappedMiniLabel);
            if (Item != null)
                DrawSelection(library, kit, changed);
        }

        public void DrawCalibration(TerrainKit kit, Action changed, Action preview = null)
        {
            if (dragKit != null && dragKit != kit) EndDrag(false, changed);
            EditorGUILayout.LabelField("套件校准", EditorStyles.boldLabel);
            if (kit == null)
            {
                EditorGUILayout.HelpBox("选择一套预设后可以调整饰边角色与连接点。", MessageType.Info);
                return;
            }
            DrawKitState(kit, changed);
            for (int row = 0; row < Slots.Length; row += 3)
            {
                using (new EditorGUILayout.HorizontalScope())
                    for (int i = row; i < Math.Min(row + 3, Slots.Length); i++)
                        DrawSlot(kit, Slots[i], changed);
            }
            var rule = kit.Rule(Slot);
            if (rule == null || rule.art == null)
            {
                if (Slot == TerrainSlot.Fill)
                    EditorGUILayout.LabelField("纯色主体已就绪。可选用材质素材。", EditorStyles.wordWrappedMiniLabel);
                else
                    EditorGUILayout.HelpBox($"{Label(Slot)}缺少素材。可将缩略图拖到对应槽，或用素材库的「放入」按钮。", MessageType.Warning);
            }
            else DrawCalibrationImage(kit, rule, changed, preview);

            if (rule != null) DrawRuleFields(kit, rule, changed);
        }

        public bool AssignSelected(TerrainKit kit, TerrainSlot slot, Action changed) => Assign(kit, slot, Item, changed);

        public void EndDrag(bool cancel, Action changed)
        {
            if (cropControl != 0)
            {
                if (GUIUtility.hotControl == cropControl) GUIUtility.hotControl = 0;
                cropControl = 0;
            }
            if (dragControl == 0) return;
            if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragControl = 0;
            var group = dragUndoGroup;
            dragUndoGroup = -1;
            dragKit = null;
            dragRule = null;
            if (group == -1) return;
            if (cancel)
            {
                Undo.RevertAllDownToGroup(group);
                changed?.Invoke();
            }
            else
            {
                Undo.CollapseUndoOperations(group);
                changed?.Invoke();
            }
        }

        public static string Label(TerrainSlot slot) => slot switch
        {
            TerrainSlot.Fill => "内部填充",
            TerrainSlot.Top => "顶面",
            TerrainSlot.Left => "左墙",
            TerrainSlot.Right => "右墙",
            TerrainSlot.Ceiling => "洞顶／底面",
            TerrainSlot.Convex => "凸角",
            TerrainSlot.Concave => "凹角",
            TerrainSlot.Cap => "端头／短边",
            TerrainSlot.Attached => "附着装饰",
            _ => slot.ToString()
        };

        public static string FindUses(TerrainArt art, TerrainKit kit)
        {
            if (art == null || kit == null) return "";
            return string.Join("、", (kit.rules ?? Array.Empty<TerrainRule>())
                .Where(rule => rule != null && rule.art == art).Select(rule => Label(rule.slot)).Distinct());
        }

        private static string Filter(string label, string before, TerrainArt[] items, Func<TerrainArt, string> value)
        {
            var all = items.Where(item => item != null).Select(value).Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct().OrderBy(v => v, StringComparer.CurrentCulture).Prepend("").ToArray();
            int selected = Mathf.Max(0, Array.IndexOf(all, before));
            return all[EditorGUILayout.Popup(label, selected,
                all.Select((option, index) => index == 0 ? "全部" : option).ToArray())];
        }

        private bool Matches(TerrainArt art, HashSet<TerrainArt> used, TerrainKit kit)
        {
            if (art == null || onlyFavorites && !favorites.Contains(Key(art))) return false;
            if (!string.IsNullOrEmpty(query) &&
                !Contains(art.name, query) && !Contains(art.origin, query) &&
                !Contains(art.theme, query) && !Contains(art.purpose, query)) return false;
            if (theme.Length != 0 && art.theme != theme || purpose.Length != 0 && art.purpose != purpose ||
                origin.Length != 0 && art.origin != origin) return false;
            return stateFilter == 0 || stateFilter == 1 && !used.Contains(art) ||
                kit != null && stateFilter > 1 && stateFilter == kit.state + 1 && used.Contains(art);
        }

        private static bool Contains(string value, string word) => value?.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        private void DrawGrid(TerrainArt[] visible)
        {
            float usable = PanelWidth > 0 ? PanelWidth : EditorGUIUtility.currentViewWidth;
            int columns = Mathf.Max(1, Mathf.FloorToInt((usable - 30) / (tileWidth + 5)));
            float rowHeight = tileWidth + 18;
            int rows = (visible.Length + columns - 1) / columns;
            float height = Mathf.Min(190, rows * rowHeight + 5);
            var full = new Rect(0, 0, columns * (tileWidth + 5), rows * rowHeight);
            var area = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            using (var view = new GUI.ScrollViewScope(area, scroll, full))
            {
                scroll = view.scrollPosition;
                int rowStart = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowHeight) - 1);
                int rowEnd = Mathf.Min(rows, Mathf.CeilToInt((scroll.y + height) / rowHeight) + 1);
                for (int i = rowStart * columns; i < Math.Min(rowEnd * columns, visible.Length); i++)
                {
                    var card = new Rect(i % columns * (tileWidth + 5), i / columns * rowHeight, tileWidth, rowHeight - 3);
                    DrawCard(card, visible[i]);
                }
            }
        }

        private void DrawCard(Rect area, TerrainArt art)
        {
            if (art == Item) EditorGUI.DrawRect(area, new Color(.23f, .41f, .51f));
            var imageArea = new Rect(area.x + 2, area.y + 2, area.width - 4, area.width - 4);
            DrawArt(imageArea, art, false, 0);
            string name = favorites.Contains(Key(art)) ? "★ " + art.name : art.name;
            GUI.Label(new Rect(area.x + 3, area.yMax - 17, area.width - 6, 16), new GUIContent(name, art.name), EditorStyles.miniLabel);
            int control = GUIUtility.GetControlID(FocusType.Passive, area);
            var e = Event.current;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
            {
                Select(art);
                GUIUtility.hotControl = control;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { art };
                e.Use();
                GUI.changed = true;
            }
            else if (GUIUtility.hotControl == control && e.type == EventType.MouseDrag)
            {
                GUIUtility.hotControl = 0;
                DragAndDrop.StartDrag(art.name);
                e.Use();
            }
            else if (GUIUtility.hotControl == control && e.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private void DrawSelection(TerrainLibrary library, TerrainKit kit, Action changed)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string key = Key(Item);
                bool favorite = favorites.Contains(key);
                if (!string.IsNullOrEmpty(key) && GUILayout.Button(favorite ? "取消收藏" : "收藏所选", GUILayout.Width(75)))
                {
                    if (favorite) favorites.Remove(key);
                    else favorites.Add(key);
                    EditorPrefs.SetString(FavoriteKey, string.Join("\n", favorites));
                }
                using (new EditorGUI.DisabledScope(kit == null))
                    if (GUILayout.Button("放入 " + Label(Slot))) AssignSelected(kit, Slot, changed);
            }
            sourceExpanded = EditorGUILayout.Foldout(sourceExpanded, Item.name + " · 原页与裁片", true);
            if (!sourceExpanded) return;
            previewBackground = GUILayout.Toolbar(previewBackground, new[] { "透明底", "暗底", "亮底" });
            var area = GUILayoutUtility.GetRect(0, 126, GUILayout.ExpandWidth(true));
            var originalPage = DrawArt(area, Item, true, previewBackground);
            var uv = Item.UV;
            var pixels = Item.PixelSize;
            var texture = Item.Image;
            if (texture != null)
            {
                var bounds = new Rect(uv.x * texture.width, uv.y * texture.height, uv.width * texture.width, uv.height * texture.height);
                EditorGUILayout.LabelField($"原页 {texture.width}×{texture.height}  ·  裁片 {pixels.x:0}×{pixels.y:0}  ·  左下 ({bounds.x:0}, {bounds.y:0})",
                    EditorStyles.wordWrappedMiniLabel);
            }
            if (Item.sprite != null)
            {
                var sprite = Item.sprite;
                EditorGUILayout.LabelField($"基准 {sprite.pivot.x:0.#}, {sprite.pivot.y:0.#} px  ·  每单位 {sprite.pixelsPerUnit:0.#} px  ·  旋转 {sprite.packingRotation}",
                    EditorStyles.wordWrappedMiniLabel);
                if (sprite.packed) EditorGUILayout.LabelField($"透明裁边偏移 {sprite.textureRectOffset.x:0.#}, {sprite.textureRectOffset.y:0.#} px",
                    EditorStyles.wordWrappedMiniLabel);
            }
            string use = FindUses(Item, kit);
            if (use.Length > 0) EditorGUILayout.LabelField("当前套件使用  " + use, EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrWhiteSpace(Item.origin)) EditorGUILayout.LabelField("来源  " + Item.origin, EditorStyles.wordWrappedMiniLabel);
            detailsExpanded = EditorGUILayout.Foldout(detailsExpanded, "分类与套件引用", true);
            if (detailsExpanded) DrawItemDetails();
            string path = AssetDatabase.GetAssetPath(Item.sprite != null ? (UnityEngine.Object)Item.sprite : Item.texture);
            if (string.IsNullOrEmpty(path)) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("定位工程资源")) EditorGUIUtility.PingObject(Item.sprite != null ? (UnityEngine.Object)Item.sprite : Item.texture);
                if (GUILayout.Button("打开源文件位置")) EditorUtility.RevealInFinder(System.IO.Path.GetFullPath(path));
            }
            if (texture == null) return;
            cropMode = GUILayout.Toggle(cropMode, "从原页拖框建裁片", EditorStyles.miniButton);
            if (cropMode)
            {
                EditorGUILayout.LabelField("在上方原页拖出范围。Esc 取消本次拖框。", EditorStyles.wordWrappedMiniLabel);
                DrawCropDrag(library, originalPage, texture);
            }
        }

        private void DrawItemDetails()
        {
            string nextTheme = EditorGUILayout.DelayedTextField("主题", Item.theme ?? "");
            string nextPurpose = EditorGUILayout.DelayedTextField("用途", Item.purpose ?? "");
            string nextOrigin = EditorGUILayout.DelayedTextField("来源说明", Item.origin ?? "");
            if (!string.Equals(nextTheme, Item.theme ?? "") || !string.Equals(nextPurpose, Item.purpose ?? "") ||
                !string.Equals(nextOrigin, Item.origin ?? ""))
            {
                Undo.RecordObject(Item, "调整地形素材分类");
                Item.theme = nextTheme.Trim();
                Item.purpose = nextPurpose.Trim();
                Item.origin = nextOrigin.Trim();
                EditorUtility.SetDirty(Item);
            }
            if (GUILayout.Button("查找引用此图件的套件", EditorStyles.miniButton))
                referencedKits = AssetDatabase.FindAssets("t:TerrainKit", new[] { "Assets" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<TerrainKit>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(candidate => FindUses(Item, candidate).Length > 0).ToArray();
            if (referencedKits == null) return;
            if (referencedKits.Length == 0)
                EditorGUILayout.LabelField("本工程暂无套件引用。", EditorStyles.wordWrappedMiniLabel);
            foreach (var referenced in referencedKits)
            {
                if (referenced == null) continue;
                if (GUILayout.Button(referenced.name + " · " + FindUses(Item, referenced), EditorStyles.miniButton))
                    EditorGUIUtility.PingObject(referenced);
            }
        }

        private void DrawCropDrag(TerrainLibrary library, Rect page, Texture2D image)
        {
            if (page.width <= 0 || page.height <= 0) return;
            int control = GUIUtility.GetControlID(FocusType.Passive, page);
            var e = Event.current;
            EditorGUIUtility.AddCursorRect(page, MouseCursor.ArrowPlus);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && page.Contains(e.mousePosition))
            {
                cropStart = cropEnd = e.mousePosition;
                cropControl = control;
                GUIUtility.hotControl = control;
                e.Use();
            }
            if (cropControl == 0) return;
            if (e.type == EventType.Ignore || e.type == EventType.MouseLeaveWindow ||
                e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (GUIUtility.hotControl == cropControl) GUIUtility.hotControl = 0;
                cropControl = 0;
                if (e.type == EventType.KeyDown) e.Use();
                return;
            }
            if (e.type == EventType.MouseDrag)
            {
                cropEnd = new Vector2(Mathf.Clamp(e.mousePosition.x, page.xMin, page.xMax),
                    Mathf.Clamp(e.mousePosition.y, page.yMin, page.yMax));
                GUI.changed = true;
                e.Use();
            }
            var mark = Rect.MinMaxRect(Mathf.Min(cropStart.x, cropEnd.x), Mathf.Min(cropStart.y, cropEnd.y),
                Mathf.Max(cropStart.x, cropEnd.x), Mathf.Max(cropStart.y, cropEnd.y));
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(mark, new Color(.3f, .85f, .85f, .22f));
                StrokeRect(mark, StartColor);
            }
            if (e.type != EventType.MouseUp || e.button != 0) return;
            if (GUIUtility.hotControl == cropControl) GUIUtility.hotControl = 0;
            cropControl = 0;
            if (mark.width >= 2 && mark.height >= 2)
            {
                var pixels = Rect.MinMaxRect(
                    Mathf.RoundToInt((mark.xMin - page.xMin) / page.width * image.width),
                    Mathf.RoundToInt((page.yMax - mark.yMax) / page.height * image.height),
                    Mathf.RoundToInt((mark.xMax - page.xMin) / page.width * image.width),
                    Mathf.RoundToInt((page.yMax - mark.yMin) / page.height * image.height));
                Select(TerrainWorkshopArtImport.AddCrop(library, Item, pixels));
                cropMode = false;
            }
            e.Use();
        }

        private void DrawSlot(TerrainKit kit, TerrainSlot slot, Action changed)
        {
            var rule = kit.Rule(slot);
            string state = rule?.art != null ? "● " : slot == TerrainSlot.Fill ? "◐ " : "○ ";
            var area = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            if (GUI.Toggle(area, slot == Slot, new GUIContent(state + Label(slot), rule?.art != null ? rule.art.name : "拖入素材"), EditorStyles.miniButton))
                Slot = slot;
            var e = Event.current;
            if (!GUI.enabled || !area.Contains(e.mousePosition) || e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            var drop = DragAndDrop.objectReferences.FirstOrDefault(value => value is TerrainArt || value is Sprite || value is Texture2D);
            if (drop == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                var art = drop as TerrainArt ?? TerrainWorkshopArtImport.RegisterForKit(kit, drop);
                if (Assign(kit, slot, art, changed)) Select(art);
                DragAndDrop.AcceptDrag();
            }
            e.Use();
        }

        private void DrawKitState(TerrainKit kit, Action changed)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("校准状态  " + StateNames[Mathf.Clamp(kit.state, 0, 3)], GUILayout.ExpandWidth(true));
                using (new EditorGUI.DisabledScope(kit.state == 3))
                    if (GUILayout.Button("确认当前校准", GUILayout.Width(101)))
                    {
                        Undo.RecordObject(kit, "确认地形套件校准");
                        kit.state = 3;
                        Notify(kit, changed);
                    }
            }
            var fill = EditorGUILayout.ColorField("主体颜色", kit.fill);
            if (fill != kit.fill)
            {
                Undo.RecordObject(kit, "调整地形主体颜色");
                kit.fill = fill;
                kit.state = 1;
                Notify(kit, changed);
            }
        }

        private void DrawCalibrationImage(TerrainKit kit, TerrainRule rule, Action changed, Action preview)
        {
            if (rule.art.Image == null) return;
            var frame = GUILayoutUtility.GetRect(0, 181, GUILayout.ExpandWidth(true));
            // 布局阶段的 frame 还没有宽度。先为图例占位，所有事件必须经过相同的 GUILayout 控件。
            EditorGUILayout.LabelField("青色 起点　蓝色 终点　金色 贴附线", EditorStyles.centeredGreyMiniLabel);
            var art = DrawArt(frame, rule.art, false, 1);
            if (art.width <= 0 || art.height <= 0) return;
            bool wall = (Slot == TerrainSlot.Left || Slot == TerrainSlot.Right) && !rule.rotate;
            var start = HandleAt(art, rule.start, rule.anchor, wall);
            var end = HandleAt(art, rule.end, rule.anchor, wall);
            var anchor = wall
                ? new Vector2(start.x, art.center.y)
                : new Vector2(art.center.x, start.y);
            if (wall) EditorGUI.DrawRect(new Rect(start.x - 1, art.y, 2, art.height), AnchorColor);
            else EditorGUI.DrawRect(new Rect(art.x, start.y - 1, art.width, 2), AnchorColor);
            if (wall) EditorGUI.DrawRect(new Rect(start.x - 2, end.y, 4, start.y - end.y), StartColor);
            else EditorGUI.DrawRect(new Rect(start.x, start.y - 2, end.x - start.x, 4), StartColor);
            var a = Mark(anchor, AnchorColor, "贴附线", MouseCursor.ResizeVertical, wall, true);
            var s = Mark(start, StartColor, "起点", MouseCursor.ResizeHorizontal, wall, false);
            var n = Mark(end, EndColor, "终点", MouseCursor.ResizeHorizontal, wall, false);
            int control = GUIUtility.GetControlID(FocusType.Passive, frame);
            DragCalibration(control, kit, rule, art, wall, a, s, n, changed, preview);
        }

        private void DragCalibration(int control, TerrainKit kit, TerrainRule rule, Rect image, bool wall,
            Rect anchor, Rect start, Rect end, Action changed, Action preview)
        {
            var e = Event.current;
            var pointer = e.mousePosition;
            if (GUI.enabled && dragControl == 0 && e.type == EventType.MouseDown && e.button == 0)
            {
                if (start.Contains(pointer)) activeHandle = TerrainGuideHandle.Start;
                else if (end.Contains(pointer)) activeHandle = TerrainGuideHandle.End;
                else if (anchor.Contains(pointer)) activeHandle = TerrainGuideHandle.Anchor;
                else return;
                dragKit = kit;
                dragRule = rule;
                dragControl = control;
                GUIUtility.hotControl = control;
                e.Use();
            }
            if (dragControl == 0) return;
            if (e.type == EventType.Ignore || e.type == EventType.MouseLeaveWindow ||
                e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                EndDrag(true, changed);
                if (e.type == EventType.KeyDown) e.Use();
                return;
            }
            if (dragRule != rule || e.type == EventType.MouseUp && e.button == 0)
            {
                EndDrag(dragRule != rule, changed);
                if (e.type == EventType.MouseUp) e.Use();
                return;
            }
            if (e.type != EventType.MouseDrag || GUIUtility.hotControl != dragControl) return;
            var guide = new TerrainGuide(rule.start, rule.end, rule.anchor);
            var result = guide.Move(activeHandle, (pointer.x - image.x) / image.width, (pointer.y - image.y) / image.height, wall);
            if (result.Start != guide.Start || result.End != guide.End || result.Anchor != guide.Anchor)
            {
                if (dragUndoGroup < 0)
                {
                    Undo.IncrementCurrentGroup();
                    dragUndoGroup = Undo.GetCurrentGroup();
                    Undo.RegisterCompleteObjectUndo(kit, "拖动地形接点");
                }
                rule.start = result.Start;
                rule.end = result.End;
                rule.anchor = result.Anchor;
                kit.state = 1;
                Notify(kit, preview);
                GUI.changed = true;
            }
            e.Use();
        }

        private static Vector2 HandleAt(Rect rect, float along, float across, bool wall) => wall
            ? new Vector2(rect.x + across * rect.width, rect.yMax - along * rect.height)
            : new Vector2(rect.x + along * rect.width, rect.yMax - across * rect.height);

        private static Rect Mark(Vector2 center, Color tint, string tooltip, MouseCursor horizontal, bool wall, bool anchor)
        {
            float size = anchor ? 12 : 10;
            var mark = new Rect(center.x - size * .5f, center.y - size * .5f, size, size);
            EditorGUI.DrawRect(mark, new Color(.13f, .18f, .21f));
            EditorGUI.DrawRect(new Rect(mark.x + 2, mark.y + 2, mark.width - 4, mark.height - 4), tint);
            var target = new Rect(mark.x - 4, mark.y - 4, mark.width + 8, mark.height + 8);
            EditorGUIUtility.AddCursorRect(target, wall
                ? horizontal == MouseCursor.ResizeHorizontal ? MouseCursor.ResizeVertical : MouseCursor.ResizeHorizontal
                : horizontal);
            GUI.Label(target, new GUIContent("", tooltip));
            return target;
        }

        private static void DrawRuleFields(TerrainKit kit, TerrainRule rule, Action changed)
        {
            EditorGUI.BeginChangeCheck();
            int mode = Mathf.Clamp((int)rule.placement, 0, 2);
            var placement = (TerrainPlacement)EditorGUILayout.Popup("铺设方式", mode,
                new[] { "重复边带", "完整跨格构件", "附着散布" });
            float start = Mathf.Clamp(EditorGUILayout.DelayedFloatField("连接起点", rule.start), 0, Mathf.Max(0, rule.end - .02f));
            float end = Mathf.Clamp(EditorGUILayout.DelayedFloatField("连接终点", rule.end), Mathf.Min(1, start + .02f), 1);
            float anchor = Mathf.Clamp01(EditorGUILayout.DelayedFloatField("贴附线", rule.anchor));
            float span = Mathf.Max(.05f, EditorGUILayout.FloatField("有效跨度（格）", rule.span));
            float spacing = EditorGUILayout.FloatField("额外间距（格）", rule.spacing);
            float overlap = Mathf.Max(0, EditorGUILayout.FloatField("重叠余量（格）", rule.overlap));
            float offset = EditorGUILayout.FloatField("垂直边线偏移（格）", rule.offset);
            var size = EditorGUILayout.Vector2Field("尺寸（格，0 随比例）", rule.size);
            float minLength = Mathf.Max(0, EditorGUILayout.FloatField("最短适用边（格）", rule.minLength));
            float weight = Mathf.Max(0, EditorGUILayout.FloatField("变体权重", rule.weight));
            using (new EditorGUILayout.HorizontalScope())
            {
                bool flip = EditorGUILayout.ToggleLeft("允许翻转", rule.flip);
                bool rotate = EditorGUILayout.ToggleLeft("旋转铺设", rule.rotate);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(kit, "调整地形饰边规则");
                    rule.placement = placement;
                    rule.start = start;
                    rule.end = end;
                    rule.anchor = anchor;
                    rule.span = span;
                    rule.spacing = spacing;
                    rule.overlap = overlap;
                    rule.offset = offset;
                    rule.size = Vector2.Max(size, Vector2.zero);
                    rule.minLength = minLength;
                    rule.weight = weight;
                    rule.flip = flip;
                    rule.rotate = rotate;
                    kit.state = 1;
                    Notify(kit, changed);
                }
            }
        }

        private static Rect DrawArt(Rect area, TerrainArt item, bool full, int background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var dark = new Color(.15f, .18f, .21f);
                EditorGUI.DrawRect(area, background == 2 ? new Color(.79f, .81f, .83f) : dark);
                if (background == 0)
                {
                    const int step = 12;
                    var square = new Color(.23f, .26f, .30f);
                    for (int y = 0; y < area.height; y += step)
                    for (int x = ((y / step) % 2) * step; x < area.width; x += step * 2)
                        EditorGUI.DrawRect(new Rect(area.x + x, area.y + y,
                            Mathf.Min(step, area.width - x), Mathf.Min(step, area.height - y)), square);
                }
            }
            var texture = item.Image;
            if (texture == null) return default;
            var uv = item.UV;
            var pixels = full ? new Vector2(texture.width, texture.height) : item.PixelSize;
            if (pixels.x <= 0 || pixels.y <= 0) return default;
            var fit = Fit(area, pixels);
            if (Event.current.type != EventType.Repaint) return fit;
            GUI.DrawTextureWithTexCoords(fit, texture, full ? new Rect(0, 0, 1, 1) : uv);
            if (full)
            {
                var crop = new Rect(fit.x + uv.xMin * fit.width, fit.yMax - uv.yMax * fit.height, uv.width * fit.width, uv.height * fit.height);
                StrokeRect(crop, StartColor);
            }
            return fit;
        }

        private static Rect Fit(Rect box, Vector2 size)
        {
            float scale = Mathf.Min(box.width / size.x, box.height / size.y);
            size *= scale;
            return new Rect(box.center - size * .5f, size);
        }

        private static void StrokeRect(Rect rect, Color tint)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1), tint);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1, rect.width, 1), tint);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1, rect.height), tint);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.yMin, 1, rect.height), tint);
        }

        private bool Assign(TerrainKit kit, TerrainSlot slot, TerrainArt art, Action changed)
        {
            if (kit == null || art == null) return false;
            var rule = kit.Rule(slot);
            if (rule?.art == art) return false;
            Undo.RecordObject(kit, "分配地形饰边素材");
            if (rule == null)
            {
                rule = new TerrainRule { slot = slot, start = .06f, end = .94f, anchor = .5f, span = 1, weight = 1 };
                kit.rules = (kit.rules ?? Array.Empty<TerrainRule>()).Append(rule).ToArray();
            }
            rule.art = art;
            Slot = slot;
            referencedKits = null;
            kit.state = 1;
            Notify(kit, changed);
            return true;
        }

        private static void Notify(TerrainKit kit, Action changed)
        {
            EditorUtility.SetDirty(kit);
            changed?.Invoke();
        }

        private static string Key(TerrainArt art) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(art));
    }
}
