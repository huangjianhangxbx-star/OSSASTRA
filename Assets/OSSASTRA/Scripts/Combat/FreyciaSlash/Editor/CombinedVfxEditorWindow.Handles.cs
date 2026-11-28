// 预览区手柄和时间条共用一次拖动一次撤销的输入状态。
using UnityEditor;
using UnityEngine;

namespace Ossastra.Combat.Editor
{
    public sealed partial class CombinedVfxEditorWindow
    {
        private enum DragMode
        {
            None, Offset, Rotation, Scale, Point, Width, Cursor,
            Start, LeftEdge, RightEdge, ExpandStart, ExpandEnd, FadeStart, FadeEnd, EraseStart, EraseEnd
        }

        private DragMode dragMode;
        private int dragControl;
        private int dragUndo = -1;
        private int dragLayer;
        private int dragIndex;
        private Vector2 dragScreen;
        private Vector2 dragValue;
        private float dragSeconds;
        private float dragStart;
        private float dragLength;
        private float dragScale;
        private Matrix4x4 dragFrame;

        private VfxPath PathFor(VfxLayer layer) => layer.useSharedPath ? target.sharedPath : layer.path;

        private Matrix4x4 EffectFrame(VfxLayer layer)
        {
            var anchorTime = layer != null && layer.anchorMode == VfxAnchorMode.FreezeAtStart
                ? layer.start : cursor;
            return SampleAnchor(layer?.anchor, anchorTime);
        }

        private static Matrix4x4 Transform2D(Vector2 offset, float rotation, Vector2 scale)
            => Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, rotation), new Vector3(scale.x, scale.y, 1f));

        private Matrix4x4 LayerFrame(VfxLayer layer)
            => EffectFrame(layer) * Transform2D(target.offset, target.rotation, target.scale) *
               Transform2D(layer.offset, layer.rotation, layer.scale);

        private Vector2 ToGui(Rect frame, Vector3 world)
        {
            float pixelsPerUnit = frame.height / (2f * zoom);
            return new Vector2(frame.center.x + world.x * pixelsPerUnit, frame.center.y - world.y * pixelsPerUnit);
        }

        private Vector3 ToWorld(Rect frame, Vector2 gui)
        {
            float unitsPerPixel = 2f * zoom / frame.height;
            return new Vector3((gui.x - frame.center.x) * unitsPerPixel, (frame.center.y - gui.y) * unitsPerPixel, 0f);
        }

        private void DrawOrigin(Rect frame)
        {
            var center = frame.center;
            var ink = new Color(.58f, .66f, .71f, .46f);
            EditorGUI.DrawRect(new Rect(center.x - 6, center.y, 13, 1), ink);
            EditorGUI.DrawRect(new Rect(center.x, center.y - 6, 1, 13), ink);
        }

        private void DrawHandles(Rect frame)
        {
            var layer = SelectedLayer;
            var editable = layer != null && !locked.Contains(selected);
            GUI.BeginGroup(frame);
            try
            {
                var local = new Rect(0f, 0f, frame.width, frame.height);
                int control = GUIUtility.GetControlID(FocusType.Passive, local);
                if (dragMode == DragMode.Offset || dragMode == DragMode.Rotation || dragMode == DragMode.Scale ||
                    dragMode == DragMode.Point || dragMode == DragMode.Width)
                    UpdatePreviewDrag(local, layer);
                if (!editable) return;
                if (editTool == 0) DrawTransformHandles(local, layer, control);
                else if (layer.kind == VfxLayerKind.Ribbon || layer.particlesAlongPath)
                    DrawPathHandles(local, layer, control);
            }
            finally { GUI.EndGroup(); }
        }

        private void DrawTransformHandles(Rect frame, VfxLayer layer, int control)
        {
            var basis = EffectFrame(layer);
            var center = ToGui(frame, basis.MultiplyPoint3x4(target.offset));
            float angle = target.rotation * Mathf.Deg2Rad;
            var arm = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * 45f;
            var rotatePoint = center + arm;
            var scalePoint = center + new Vector2(-arm.y, arm.x) * .78f;
            DrawLine(center, rotatePoint, Aqua);
            DrawLine(center, scalePoint, Amber);
            var moveHit = Mark(center, Color.white, "移动整套效果");
            var rotationHit = Mark(rotatePoint, Aqua, "旋转整套效果");
            var scaleHit = Mark(scalePoint, Amber, "缩放整套效果");
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !frame.Contains(e.mousePosition)) return;
            var mouse = e.mousePosition;
            if (moveHit.Contains(mouse))
            {
                StartDrag(DragMode.Offset, control, selected);
                dragValue = target.offset;
            }
            else if (rotationHit.Contains(mouse))
            {
                StartDrag(DragMode.Rotation, control, selected);
                dragSeconds = target.rotation;
            }
            else if (scaleHit.Contains(mouse))
            {
                StartDrag(DragMode.Scale, control, selected);
                dragValue = target.scale;
            }
            else return;
            dragScreen = mouse;
            dragFrame = basis;
            e.Use();
        }

        private void DrawPathHandles(Rect frame, VfxLayer layer, int control)
        {
            var path = PathFor(layer);
            if (path?.points == null || path.points.Count < 4 || (path.points.Count - 1) % 3 != 0) return;

            var matrix = LayerFrame(layer);
            if (Mathf.Abs(matrix.determinant) < .000001f) return;
            if (Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                var points = new Vector3[51];
                for (int i = 0; i < points.Length; i++)
                    points[i] = ToGui(frame, matrix.MultiplyPoint3x4(path.Sample(i / (float)(points.Length - 1))));
                Handles.color = Aqua;
                Handles.DrawAAPolyLine(1.7f, points);
                DrawDirection(points);
                Handles.EndGUI();
            }

            if (editTool == 2)
            {
                if (layer.kind == VfxLayerKind.Ribbon) DrawWidths(frame, matrix, layer, path, control);
                return;
            }

            var e = Event.current;
            for (int i = 0; i < path.points.Count; i++)
            {
                var p = ToGui(frame, matrix.MultiplyPoint3x4(path.points[i]));
                bool tangent = i % 3 != 0;
                if (tangent)
                {
                    int knotIndex = i % 3 == 1 ? i - 1 : i + 1;
                    var knot = ToGui(frame, matrix.MultiplyPoint3x4(path.points[knotIndex]));
                    DrawLine(knot, p, new Color(.6f, .7f, .74f, .7f));
                }
                var label = i == 0 ? "起点" : i == path.points.Count - 1 ? "终点" : tangent ? "曲线切线" : "路径节点";
                var hit = Mark(p, tangent ? Amber : Aqua, label, tangent ? 7f : 10f);
                if (e.type != EventType.MouseDown || e.button != 0 || !frame.Contains(e.mousePosition) || !hit.Contains(e.mousePosition)) continue;
                StartDrag(DragMode.Point, control, selected);
                dragIndex = i;
                dragScreen = e.mousePosition;
                dragValue = path.points[i];
                dragFrame = matrix;
                e.Use();
            }
        }

        private static void DrawDirection(Vector3[] points)
        {
            var tip = points[41];
            var direction = (tip - points[38]).normalized;
            if (direction.sqrMagnitude == 0f) return;
            var back = tip - direction * 11f;
            var cross = new Vector3(-direction.y, direction.x, 0f) * 4.5f;
            Handles.DrawAAPolyLine(2f, back + cross, tip, back - cross);
        }

        private void DrawWidths(Rect frame, Matrix4x4 matrix, VfxLayer layer, VfxPath path, int control)
        {
            var e = Event.current;
            for (int i = 0; i < 3; i++)
            {
                float t = i * .5f;
                float width = i == 0 ? layer.startWidth : i == 1 ? layer.middleWidth : layer.endWidth;
                var normal = path.Normal(t);
                var p = path.Sample(t) + normal * layer.normalOffset;
                var center = ToGui(frame, matrix.MultiplyPoint3x4(p));
                float factor = WidthFactor(layer, t);
                var edge = p + normal * (.5f * width * factor);
                var handle = ToGui(frame, matrix.MultiplyPoint3x4(edge));
                var other = ToGui(frame, matrix.MultiplyPoint3x4(p - normal * (.5f * width * factor)));
                var guiNormal = (ToGui(frame, matrix.MultiplyPoint3x4(p + normal)) - center).normalized;
                if ((handle - center).sqrMagnitude < 49f) handle = center + guiNormal * 9f;
                DrawLine(other, handle, Amber);
                var label = i == 0 ? "起点宽度" : i == 1 ? "中段宽度" : "终点宽度";
                var hit = Mark(handle, Amber, label);
                if (e.type != EventType.MouseDown || e.button != 0 || !frame.Contains(e.mousePosition) || !hit.Contains(e.mousePosition)) continue;
                StartDrag(DragMode.Width, control, selected);
                dragIndex = i;
                dragScreen = e.mousePosition;
                dragSeconds = width;
                dragFrame = matrix;
                e.Use();
            }
        }

        private static float WidthFactor(VfxLayer layer, float t)
            => Mathf.Max(.01f, layer.widthCurve != null && layer.widthCurve.length > 0 ? layer.widthCurve.Evaluate(t) : 1f);

        private void UpdatePreviewDrag(Rect frame, VfxLayer layer)
        {
            var e = Event.current;
            if (!DragEvent(e)) return;
            if (layer == null || selected != dragLayer || locked.Contains(selected))
            {
                EndDrag(false);
                return;
            }
            var inverse = dragFrame.inverse;
            var begin = inverse.MultiplyPoint3x4(ToWorld(frame, dragScreen));
            var pointer = inverse.MultiplyPoint3x4(ToWorld(frame, e.mousePosition));
            var delta = (Vector2)(pointer - begin);
            switch (dragMode)
            {
                case DragMode.Offset:
                    BeginUndo("移动组合特效");
                    target.offset = dragValue + delta;
                    break;
                case DragMode.Rotation:
                    BeginUndo("旋转组合特效");
                    target.rotation = dragSeconds + Vector2.SignedAngle((Vector2)begin - target.offset, (Vector2)pointer - target.offset);
                    break;
                case DragMode.Scale:
                    BeginUndo("缩放组合特效");
                    target.scale = dragValue * Mathf.Clamp(Vector2.Distance(pointer, target.offset) /
                        Mathf.Max(.001f, Vector2.Distance(begin, target.offset)), .025f, 50f);
                    break;
                case DragMode.Point:
                    BeginUndo("移动特效路径控制点");
                    PathFor(layer).points[dragIndex] = dragValue + delta;
                    break;
                case DragMode.Width:
                    BeginUndo("调整笔触宽度");
                    float t = dragIndex * .5f;
                    float width = Mathf.Max(0f, dragSeconds + 2f * Vector2.Dot(delta, PathFor(layer).Normal(t)) / WidthFactor(layer, t));
                    if (dragIndex == 0) layer.startWidth = width;
                    else if (dragIndex == 1) layer.middleWidth = width;
                    else layer.endWidth = width;
                    break;
            }
            MarkChanged();
            e.Use();
        }

        private void StartDrag(DragMode mode, int control, int layer)
        {
            playing = false;
            dragMode = mode;
            dragControl = control;
            dragLayer = layer;
            GUIUtility.hotControl = control;
        }

        private bool DragEvent(Event e)
        {
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                EndDrag(false);
                e.Use();
                return false;
            }
            if (e.type == EventType.Ignore)
            {
                EndDrag(false);
                return false;
            }
            return e.type == EventType.MouseDrag && GUIUtility.hotControl == dragControl;
        }

        private void BeginUndo(string label)
        {
            if (dragUndo >= 0) return;
            Undo.IncrementCurrentGroup();
            dragUndo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(target, label);
        }

        private void EndDrag(bool revert)
        {
            if (dragMode == DragMode.None) return;
            if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            var group = dragUndo;
            dragUndo = -1;
            dragMode = DragMode.None;
            if (group < 0) return;
            if (revert) Undo.RevertAllDownToGroup(group);
            else Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            previewDirty = true;
            Repaint();
        }

        private static Rect Mark(Vector2 center, Color tint, string label, float size = 9f)
        {
            var draw = new Rect(center.x - size * .5f, center.y - size * .5f, size, size);
            var hit = new Rect(draw.x - 4f, draw.y - 4f, draw.width + 8f, draw.height + 8f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(draw, Panel);
                EditorGUI.DrawRect(new Rect(draw.x + 1f, draw.y + 1f, draw.width - 2f, draw.height - 2f), tint);
            }
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.MoveArrow);
            GUI.Label(hit, new GUIContent("", label));
            return hit;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            Handles.BeginGUI();
            Handles.color = tint;
            Handles.DrawAAPolyLine(1.35f, from, to);
            Handles.EndGUI();
        }
    }
}
