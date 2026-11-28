// 分层时间条与区间拖动。发射区间和尾粒存活区间分别显示。
using UnityEditor;
using UnityEngine;

namespace Ossastra.Combat.Editor
{
    public sealed partial class CombinedVfxEditorWindow
    {
        private const float TimelineLabels = 176f;
        private static readonly Color StampColor = new Color(.84f, .52f, .36f);
        private static readonly Color ParticleColor = new Color(.64f, .54f, .84f);
        private static readonly Color TrackColor = new Color(.105f, .12f, .14f);
        private static readonly Color GridColor = new Color(.25f, .28f, .31f);
        private VfxInterval dragExpand, dragFade, dragErase;
        private float dragTimelineSpan;

        private void DrawTimeline()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("分层时间轴", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("实色 发射　浅色 余粒　细线 展开 / 整体淡出 / 逐段消退", EditorStyles.miniLabel);
            }
            float height = Mathf.Clamp(position.height * .27f, 120f, 310f);
            float lastRow = target.Duration;
            foreach (VfxLayer layer in target.layers)
                if (layer != null) lastRow = Mathf.Max(lastRow, layer.EndTime);
            float nominal = IsTimelineDrag(dragMode) ? dragTimelineSpan
                : Mathf.Ceil(Mathf.Max(.3f, lastRow * 1.2f) * 10f) / 10f;
            using (var scope = new EditorGUILayout.ScrollViewScope(timelineScroll, GUILayout.Height(height)))
            {
                timelineScroll = scope.scrollPosition;
                var ruler = GUILayoutUtility.GetRect(200f, 24f, GUILayout.ExpandWidth(true));
                var axis = new Rect(ruler.x + TimelineLabels, ruler.y, Mathf.Max(1f, ruler.width - TimelineLabels - 8f), ruler.height);
                DrawRuler(axis, nominal);
                for (int i = 0; i < target.layers.Count; i++) DrawTimelineRow(i, nominal);
            }
        }

        private void DrawRuler(Rect axis, float duration)
        {
            var e = Event.current;
            var background = new Rect(axis.x - TimelineLabels, axis.y, axis.width + TimelineLabels, axis.height);
            int id = GUIUtility.GetControlID(FocusType.Passive, axis);
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(background, TrackColor);
                for (int i = 0; i <= 4; i++)
                {
                    float x = axis.x + axis.width * i / 4f;
                    EditorGUI.DrawRect(new Rect(x, axis.yMax - 5f, 1, 5), GridColor);
                    var label = new Rect(Mathf.Min(x + 3f, axis.xMax - 48f), axis.y + 2f, 46f, axis.height - 4f);
                    GUI.Label(label, TimeLabel(duration * i / 4f), EditorStyles.miniLabel);
                }
            }
            float totalLimit = PositionAt(axis, duration, target.Duration);
            if (e.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(totalLimit, axis.yMax - 4f, 1.5f, 4f), Amber);
            DrawPlayhead(axis, duration);
            if (e.type == EventType.MouseDown && e.button == 0 && axis.Contains(e.mousePosition))
            {
                StartDrag(DragMode.Cursor, id, -1);
                SetCursor(DurationAt(axis, duration, e.mousePosition.x));
                e.Use();
            }
            else if (dragMode == DragMode.Cursor && DragEvent(e) && GUIUtility.hotControl == id)
            {
                SetCursor(DurationAt(axis, duration, e.mousePosition.x));
                e.Use();
            }
        }

        private void DrawTimelineRow(int index, float duration)
        {
            var layer = target.layers[index];
            if (layer == null) return;
            var bounds = GUILayoutUtility.GetRect(200f, 37f, GUILayout.ExpandWidth(true));
            var axis = new Rect(bounds.x + TimelineLabels, bounds.y + 1f, Mathf.Max(1f, bounds.width - TimelineLabels - 8f), bounds.height - 1f);
            bool isLocked = locked.Contains(index);
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, axis);
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(bounds, selected == index ? new Color(.2f, .27f, .29f) :
                    index % 2 == 0 ? TrackColor : Panel);
                for (int i = 0; i <= 4; i++)
                    EditorGUI.DrawRect(new Rect(axis.x + axis.width * i / 4f, axis.y, 1f, axis.height), GridColor);
            }

            var label = string.IsNullOrWhiteSpace(layer.name) ? KindLabels[(int)layer.kind] : layer.name;
            var select = new Rect(bounds.x + 4, bounds.y + 4, 92, 23);
            if (GUI.Button(select, new GUIContent(label, label), EditorStyles.miniButton)) selected = index;
            var showBox = new Rect(bounds.x + 97, bounds.y + 7, 19, 23);
            bool shown = GUI.Toggle(showBox, layer.visible, new GUIContent("", "显示该层"));
            if (shown != layer.visible && !isLocked) Change("切换特效层显示", () => layer.visible = shown);
            DrawToggle(new Rect(bounds.x + 117, bounds.y + 6, 26, 22), solo == index, "独", "独显此层", () => solo = solo == index ? -1 : index);
            DrawToggle(new Rect(bounds.x + 144, bounds.y + 6, 26, 22), isLocked, "锁", "锁定此层", () =>
            {
                if (isLocked) locked.Remove(index);
                else locked.Add(index);
            });

            Color color = layer.kind == VfxLayerKind.Ribbon ? Aqua : layer.kind == VfxLayerKind.Stamp ? StampColor : ParticleColor;
            if (!layer.visible || solo >= 0 && solo != index) color.a = .32f;
            float start = layer.BeginTime;
            float end = layer.EndTime;
            bool particles = layer.kind == VfxLayerKind.Particles;
            float coreEnd = particles ? start + Mathf.Max(0, layer.emission) : end;
            var bar = Bar(axis, duration, start, coreEnd, 3f, 14f);
            var tail = Bar(axis, duration, coreEnd, end, 3f, 14f);
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(bar, color);
                if (end > coreEnd)
                    EditorGUI.DrawRect(tail, new Color(color.r, color.g, color.b, color.a * .42f));
                if (bar.width > 39f)
                    GUI.Label(new Rect(bar.x + 4f, bar.y - 1f, bar.width - 4f, 16f), particles ? "发射" : KindLabels[(int)layer.kind], EditorStyles.miniLabel);
                if (particles && tail.width > 42f)
                    GUI.Label(new Rect(tail.x + 4f, tail.y - 1f, tail.width - 4f, 16f), "余粒", EditorStyles.miniLabel);
            }
            if (!isLocked) DrawBarHandles(axis, bar, tail, duration, layer, index, id);
            if (layer.kind == VfxLayerKind.Ribbon)
                DrawIntervalBar(axis, duration, index, id, layer.expand, DragMode.ExpandStart, DragMode.ExpandEnd, 19f, Aqua, "展开", isLocked);
            if (!particles)
                DrawIntervalBar(axis, duration, index, id, layer.fade, DragMode.FadeStart, DragMode.FadeEnd, 24f, Amber, "整体淡出", isLocked);
            if (layer.kind == VfxLayerKind.Ribbon)
                DrawIntervalBar(axis, duration, index, id, layer.erase, DragMode.EraseStart, DragMode.EraseEnd, 29f, StampColor, "逐段消退", isLocked);
            DrawPlayhead(axis, duration);
            if (selected == index && dragLayer == index && IsTimelineDrag(dragMode) && DragEvent(e))
                UpdateTimelineDrag(layer, e);
        }

        private static void DrawToggle(Rect rect, bool enabled, string label, string tooltip, System.Action toggle)
        {
            bool next = GUI.Toggle(rect, enabled, new GUIContent(label, tooltip), EditorStyles.miniButton);
            if (next != enabled) toggle();
        }

        private static bool IsTimelineDrag(DragMode mode) => (int)mode >= (int)DragMode.Start;

        private void DrawBarHandles(Rect axis, Rect bar, Rect tail, float duration, VfxLayer layer, int index, int id)
        {
            var hitLeft = new Rect(bar.x - 3f, bar.y, 7f, bar.height);
            var hitRight = new Rect(bar.xMax - 4f, bar.y, 7f, bar.height);
            EditorGUIUtility.AddCursorRect(hitLeft, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(hitRight, MouseCursor.ResizeHorizontal);
            var e = Event.current;
            if (!IsTimelineMouseDown(axis, e)) return;
            var point = e.mousePosition;
            if (hitRight.Contains(point)) StartTimeDrag(DragMode.RightEdge, point.x);
            else if (hitLeft.Contains(point)) StartTimeDrag(DragMode.LeftEdge, point.x);
            else if (bar.Contains(point) || tail.Contains(point)) StartTimeDrag(DragMode.Start, point.x);

            void StartTimeDrag(DragMode mode, float x)
            {
                selected = index;
                StartDrag(mode, id, index);
                dragScreen.x = x;
                dragScale = duration / axis.width;
                dragStart = layer.BeginTime;
                dragTimelineSpan = duration;
                dragLength = layer.kind == VfxLayerKind.Particles ? Mathf.Max(0f, layer.emission) : layer.ActiveDuration;
                dragExpand = layer.expand;
                dragFade = layer.fade;
                dragErase = layer.erase;
                e.Use();
            }
        }

        private void DrawIntervalBar(Rect axis, float duration, int index, int id, VfxInterval interval,
            DragMode start, DragMode end, float y, Color color, string label, bool isLocked)
        {
            if (!interval.enabled) return;
            float offset = target.layers[index].BeginTime;
            var bar = Bar(axis, duration, offset + interval.start, offset + interval.EndTime, y, 4f);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(bar, color);
            var left = new Rect(bar.x - 3f, bar.y - .75f, 7f, 5.5f);
            var right = new Rect(bar.xMax - 4f, bar.y - .75f, 7f, 5.5f);
            if (isLocked) return;
            GUI.Label(left, new GUIContent("", label + "开始"));
            GUI.Label(right, new GUIContent("", label + "完成"));
            EditorGUIUtility.AddCursorRect(left, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(right, MouseCursor.ResizeHorizontal);
            var e = Event.current;
            if (!IsTimelineMouseDown(axis, e) || !left.Contains(e.mousePosition) && !right.Contains(e.mousePosition)) return;
            selected = index;
            bool first = left.Contains(e.mousePosition) &&
                (!right.Contains(e.mousePosition) || e.mousePosition.x <= bar.center.x);
            StartDrag(first ? start : end, id, index);
            dragScreen.x = e.mousePosition.x;
            dragScale = duration / axis.width;
            dragStart = interval.start;
            dragLength = Mathf.Max(interval.start, interval.end);
            dragTimelineSpan = duration;
            e.Use();
        }

        private static bool IsTimelineMouseDown(Rect axis, Event e) => e.type == EventType.MouseDown && e.button == 0 && axis.Contains(e.mousePosition);

        private void UpdateTimelineDrag(VfxLayer layer, Event e)
        {
            float delta = (e.mousePosition.x - dragScreen.x) * dragScale;
            BeginUndo("调整特效层时间");
            switch (dragMode)
            {
                case DragMode.Start:
                    layer.start = Mathf.Max(0f, dragStart + delta);
                    break;
                case DragMode.LeftEdge:
                    layer.start = Mathf.Clamp(dragStart + delta, 0f, dragStart + dragLength);
                    SetLayerLength(layer, dragStart + dragLength - layer.start);
                    break;
                case DragMode.RightEdge:
                    SetLayerLength(layer, Mathf.Max(0f, dragLength + delta));
                    break;
                case DragMode.ExpandStart:
                case DragMode.ExpandEnd:
                    layer.expand = ModifyInterval(layer.expand, dragMode == DragMode.ExpandStart, delta);
                    break;
                case DragMode.FadeStart:
                case DragMode.FadeEnd:
                    layer.fade = ModifyInterval(layer.fade, dragMode == DragMode.FadeStart, delta);
                    break;
                case DragMode.EraseStart:
                case DragMode.EraseEnd:
                    layer.erase = ModifyInterval(layer.erase, dragMode == DragMode.EraseStart, delta);
                    break;
            }
            cursor = Mathf.Min(cursor, target.Duration);
            MarkChanged();
            e.Use();
        }

        private VfxInterval ModifyInterval(VfxInterval interval, bool first, float delta)
        {
            if (first) interval.start = Mathf.Clamp(dragStart + delta, 0f, dragLength);
            else interval.end = Mathf.Max(dragStart, dragLength + delta);
            return interval;
        }

        private void SetLayerLength(VfxLayer layer, float value)
        {
            if (layer.kind == VfxLayerKind.Particles)
            {
                layer.emission = value;
                return;
            }

            layer.duration = value;
            if (dragLength <= .0001f) return;
            // 主块负责整个视觉层时长。按拖动起点的快照同比调整三个窗口，避免尾部把缩短后的主块重新撑开。
            float ratio = value / dragLength;
            layer.expand = ScaleWindow(dragExpand, ratio);
            layer.fade = ScaleWindow(dragFade, ratio);
            layer.erase = ScaleWindow(dragErase, ratio);
        }

        private static VfxInterval ScaleWindow(VfxInterval interval, float ratio)
        {
            interval.start *= ratio;
            interval.end *= ratio;
            return interval;
        }

        private static float PositionAt(Rect axis, float duration, float time) => axis.x + axis.width * (time / duration);
        private static float DurationAt(Rect axis, float duration, float x) => Mathf.Clamp01((x - axis.x) / axis.width) * duration;

        private static Rect Bar(Rect axis, float duration, float begin, float end, float y, float height)
        {
            float x = PositionAt(axis, duration, begin);
            return new Rect(x, axis.y + y, Mathf.Max(3f, PositionAt(axis, duration, end) - x), height);
        }

        private void DrawPlayhead(Rect axis, float duration)
        {
            if (Event.current.type != EventType.Repaint) return;
            float x = PositionAt(axis, duration, cursor);
            EditorGUI.DrawRect(new Rect(x, axis.y, 1.5f, axis.height), Amber);
        }
    }
}
