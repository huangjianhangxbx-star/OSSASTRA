// 可视图集选择和层属性。字段的 Undo 写到当前预制体组件或场景覆盖。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ossastra.Combat.Editor
{
    public sealed partial class CombinedVfxEditorWindow
    {
        private static readonly string[] KindLabels = { "笔触带", "贴片", "飞片" };
        private static readonly string[] AnchorLabels = { "持续跟随", "在层开始时固定" };
        private readonly Dictionary<VfxLayer,VfxPath> initialPaths = new Dictionary<VfxLayer,VfxPath>();
        private VfxPath initialShared;
        private int newKind;

        private void DrawInspector()
        {
            var serialized = new SerializedObject(target);
            var layer = SelectedLayer;
            EditorGUILayout.LabelField("整套效果", EditorStyles.boldLabel);
            Fields(serialized, "defaultTexture", "默认贴图", "material", "默认材质",
                "offset", "整体偏移", "rotation", "整体旋转", "scale", "整体缩放");
            EditorGUILayout.LabelField("偏移在特效内部，动作绑定位移在事件响应内。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("视觉层", EditorStyles.boldLabel);
                newKind = EditorGUILayout.Popup(newKind, KindLabels, GUILayout.Width(72f));
                if (GUILayout.Button("添加", GUILayout.Width(49f)))
                {
                    if (serialized.ApplyModifiedProperties()) MarkChanged();
                    AddLayer((VfxLayerKind)newKind);
                    return;
                }
            }

            if (layer == null)
            {
                if (serialized.ApplyModifiedProperties()) MarkChanged();
                return;
            }

            int fieldIndex = selected;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("复制"))
                {
                    if (serialized.ApplyModifiedProperties()) MarkChanged();
                    DuplicateLayer(fieldIndex);
                    return;
                }
                using (new EditorGUI.DisabledScope(locked.Contains(fieldIndex)))
                {
                    if (GUILayout.Button("删除"))
                    {
                        if (serialized.ApplyModifiedProperties()) MarkChanged();
                        DeleteLayer(fieldIndex);
                        return;
                    }
                }
            }
            var element = serialized.FindProperty("layers").GetArrayElementAtIndex(fieldIndex);
            using (new EditorGUI.DisabledScope(locked.Contains(fieldIndex)))
            {
                Fields(element, "name", "层名称", "visible", "显示");
                EnumPopup(element.FindPropertyRelative("kind"), "类型", KindLabels);
                Fields(element, "order", "渲染顺序", "texture", "本层单图／图集");
                var columns = element.FindPropertyRelative("columns");
                var rows = element.FindPropertyRelative("rows");
                bool atlas = columns.intValue > 1 || rows.intValue > 1;
                var newAtlas = EditorGUILayout.Popup("素材取样", atlas ? 1 : 0, new[] { "整张单图", "规则图集" }) == 1;
                if (newAtlas != atlas)
                {
                    columns.intValue = newAtlas ? 4 : 1;
                    rows.intValue = newAtlas ? 2 : 1;
                    element.FindPropertyRelative("tile").intValue = 0;
                }
                if (newAtlas) Fields(element, "columns", "图集列数", "rows", "图集行数");
                Fields(element, "color", "颜色与透明度", "offset", "层偏移", "rotation", "层旋转", "scale", "层缩放");
                EnumPopup(element.FindPropertyRelative("anchorMode"), "跟随方式", AnchorLabels);
                Fields(element, "anchor", "层挂点（空为动作默认）");
                if (layer.kind == VfxLayerKind.Particles)
                    EditorGUILayout.LabelField("飞片在各自发射时取位，离开挂点后独立运动。", EditorStyles.wordWrappedMiniLabel);

                Fields(element, "start", "层开始（秒）");
                if (layer.kind == VfxLayerKind.Particles)
                {
                    if (layer.particlesAlongPath) EditorGUILayout.LabelField("0° 沿路径方向，+90° 向路径左侧。", EditorStyles.wordWrappedMiniLabel);
                    Fields(element, "emission", "发射区间（秒）", "count", "飞片数量",
                        "direction", layer.particlesAlongPath ? "相对路径切线（°）" : "发射方向（°）",
                        "spread", "方向散布（°）", "speed", "速度范围",
                        "particleLife", "存活区间（秒）", "particleSize", "飞片大小范围",
                        "spin", "转速（°/秒）", "gravity", "重力", "particleAlpha", "存活透明度", "particlesAlongPath", "沿路径取发射点");
                }
                else
                {
                    Fields(element, "duration", "持续（秒）");
                    if (layer.kind == VfxLayerKind.Stamp) Fields(element, "size", "贴片大小");
                    DrawInterval(element, "fade", "整体淡出");
                    Fields(element, "fadeCurve", "整体透明度曲线");
                }

                if (layer.kind == VfxLayerKind.Ribbon || layer.particlesAlongPath)
                    Fields(element, "useSharedPath", "共用路径");
                if (layer.kind == VfxLayerKind.Ribbon)
                {
                    DrawInterval(element, "expand", "展开");
                    DrawInterval(element, "erase", "逐段消退");
                    EnumPopup(element.FindPropertyRelative("eraseFromEnd"), "消退起点", new[] { "从路径起点", "从路径终点" });
                    Fields(element, "softness", "边界柔和度", "normalOffset", "沿路径法线偏移",
                        "startWidth", "起点宽度", "middleWidth", "中段宽度", "endWidth", "终点宽度",
                        "widthCurve", "沿长度宽度曲线", "flipWidthUv", "反转宽向 UV");
                }
            }

            if (serialized.ApplyModifiedProperties()) MarkChanged();
            using (new EditorGUI.DisabledScope(locked.Contains(fieldIndex)))
            {
                if (layer.kind == VfxLayerKind.Ribbon || layer.particlesAlongPath) DrawPathControls(layer);
                DrawAtlas(layer);
            }
        }

        private static void Fields(SerializedObject source, params string[] pairs)
        {
            for (int i = 0; i < pairs.Length; i += 2)
                EditorGUILayout.PropertyField(source.FindProperty(pairs[i]), new GUIContent(pairs[i + 1]), true);
        }

        private static void Fields(SerializedProperty source, params string[] pairs)
        {
            for (int i = 0; i < pairs.Length; i += 2)
                EditorGUILayout.PropertyField(source.FindPropertyRelative(pairs[i]), new GUIContent(pairs[i + 1]), true);
        }

        private static void EnumPopup(SerializedProperty field, string label, string[] choices)
        {
            var current = field.propertyType == SerializedPropertyType.Boolean ? (field.boolValue ? 1 : 0) : field.enumValueIndex;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(label, current, choices);
            if (!EditorGUI.EndChangeCheck()) return;
            if (field.propertyType == SerializedPropertyType.Boolean) field.boolValue = next == 1;
            else field.enumValueIndex = next;
        }

        private static void DrawInterval(SerializedProperty source, string name, string label)
        {
            var field = source.FindPropertyRelative(name);
            var enabled = field.FindPropertyRelative("enabled");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(enabled, new GUIContent(label));
                if (enabled.boolValue)
                    Fields(field, "start", "开始（层内秒）", "end", "完成（层内秒）");
            }
        }

        private void AddLayer(VfxLayerKind kind)
        {
            Change("添加特效层", () =>
            {
                var layer = new VfxLayer { kind = kind, name = KindLabels[(int)kind] };
                target.layers.Add(layer);
                selected = target.layers.Count - 1;
            });
        }

        private void DuplicateLayer(int index)
        {
            var source = target.layers[index];
            var copy = JsonUtility.FromJson<VfxLayer>(JsonUtility.ToJson(source));
            copy.texture = source.texture;
            copy.name += " 副本";
            Change("复制特效层", () =>
            {
                target.layers.Add(copy);
                selected = target.layers.Count - 1;
            });
        }

        private void DeleteLayer(int index)
        {
            Change("删除特效层", () =>
            {
                initialPaths.Remove(target.layers[index]);
                target.layers.RemoveAt(index);
                selected = Mathf.Min(index, Mathf.Max(0, target.layers.Count - 1));
                if (solo == index) solo = -1;
                else if (solo > index) solo--;
                var priorLocked = new List<int>(locked);
                locked.Clear();
                foreach (int prior in priorLocked)
                    if (prior != index) locked.Add(prior > index ? prior - 1 : prior);
            });
        }

        private void DrawPathControls(VfxLayer layer)
        {
            var path = PathFor(layer);
            if (layer.useSharedPath && initialShared == null) initialShared = ClonePath(path);
            else if (!layer.useSharedPath && !initialPaths.ContainsKey(layer)) initialPaths[layer] = ClonePath(path);
            EditorGUILayout.LabelField(layer.useSharedPath ? "编辑整套公共路径" : "编辑此层独立路径", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("直刺")) SetPathPreset(layer, VfxPath.Line());
                if (GUILayout.Button("横劈弧")) SetPathPreset(layer, VfxPath.FromArc(new Vector2(1f, .72f), -72f, 68f, -10f));
                if (GUILayout.Button("上挑弧")) SetPathPreset(layer, VfxPath.FromArc(new Vector2(.85f, .98f), -79f, 35f, 24f));
            }
            if (!layer.useSharedPath && GUILayout.Button("复制公共路径到本层"))
                SetPathPreset(layer, ClonePath(target.sharedPath));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(path == null))
                {
                    if (GUILayout.Button("镜像")) Change("镜像特效路径", path.Mirror);
                    if (GUILayout.Button("反转")) Change("反转特效路径", path.Reverse);
                }
                if (GUILayout.Button("恢复形状"))
                {
                    var initial = layer.useSharedPath ? initialShared : initialPaths[layer];
                    Change("恢复特效路径", () => AssignPath(layer, ClonePath(initial)));
                }
            }
        }

        private void SetPathPreset(VfxLayer layer, VfxPath path)
        {
            Change("应用特效路径样式", () =>
            {
                AssignPath(layer, path);
                if (layer.useSharedPath) initialShared = ClonePath(path);
                else initialPaths[layer] = ClonePath(path);
            });
        }

        private void AssignPath(VfxLayer layer, VfxPath path)
        {
            if (layer.useSharedPath) target.sharedPath = path;
            else layer.path = path;
        }

        private static VfxPath ClonePath(VfxPath path)
            => path?.points == null ? VfxPath.Line() : new VfxPath { points = new List<Vector2>(path.points) };

        private void DrawAtlas(VfxLayer layer)
        {
            var texture = layer.texture == null ? target.defaultTexture : layer.texture;
            if (texture == null)
            {
                EditorGUILayout.HelpBox("选择默认贴图或本层贴图后，可以在这里点取区域。单图使用 1 × 1。", MessageType.None);
                return;
            }

            int columns = Mathf.Clamp(layer.columns, 1, 256);
            int rows = Mathf.Clamp(layer.rows, 1, 256);
            int tile = Mathf.Clamp(layer.tile, 0, columns * rows - 1);
            EditorGUILayout.LabelField(columns * rows == 1 ? "素材 · 全图" : "素材 · 区域 " + (tile + 1), EditorStyles.boldLabel);
            var box = GUILayoutUtility.GetRect(70f, 154f, GUILayout.ExpandWidth(true));
            if (box.width <= 1f) return;
            var image = Fit(box, texture.width / (float)texture.height);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(image, Panel);
                GUI.DrawTexture(image, texture, ScaleMode.StretchToFill, true);
            }

            float cellW = image.width / columns;
            float cellH = image.height / rows;
            var e = Event.current;
            var axis = new Color(.65f, .78f, .83f, .6f);
            for (int x = 0; x <= columns; x++) DrawLine(new Vector2(image.x + x * cellW, image.y), new Vector2(image.x + x * cellW, image.yMax), axis);
            for (int y = 0; y <= rows; y++) DrawLine(new Vector2(image.x, image.y + y * cellH), new Vector2(image.xMax, image.y + y * cellH), axis);
            var highlight = new Rect(image.x + tile % columns * cellW, image.y + tile / columns * cellH, cellW, cellH);
            if (e.type == EventType.Repaint)
                EditorGUI.DrawRect(highlight, new Color(Aqua.r, Aqua.g, Aqua.b, .2f));
            DrawLine(highlight.min, new Vector2(highlight.xMax, highlight.yMin), Aqua);
            DrawLine(highlight.min, new Vector2(highlight.xMin, highlight.yMax), Aqua);
            DrawLine(highlight.max, new Vector2(highlight.xMax, highlight.yMin), Aqua);
            DrawLine(highlight.max, new Vector2(highlight.xMin, highlight.yMax), Aqua);
            if (!GUI.enabled || e.type != EventType.MouseDown || e.button != 0 || !image.Contains(e.mousePosition)) return;
            var cell = e.mousePosition - image.position;
            int next = Mathf.Clamp(Mathf.FloorToInt(cell.x / cellW), 0, columns - 1) +
                columns * Mathf.Clamp(Mathf.FloorToInt(cell.y / cellH), 0, rows - 1);
            if (layer.tile != next) Change("选取图集区域", () => layer.tile = next);
            e.Use();
        }

        private static Rect Fit(Rect rect, float ratio)
        {
            float width = Mathf.Min(rect.width, rect.height * ratio);
            float height = width / ratio;
            return new Rect(rect.center.x - width * .5f, rect.center.y - height * .5f, width, height);
        }
    }
}
