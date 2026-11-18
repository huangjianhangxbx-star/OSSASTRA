/*
 * Scene 工坊工具接收鼠标和选区，试拼与拖动用同一装配解算。释放鼠标后才写作者地图和碰撞。
 */
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed partial class TerrainWorkshopWindow
    {
        private readonly HashSet<TerrainPoint> stroke = new HashSet<TerrainPoint>();
        private readonly HashSet<TerrainPoint> selection = new HashSet<TerrainPoint>();
        private TerrainPoint? from;
        private TerrainPoint to;
        private TerrainGrid draft, baseGrid;
        private TerrainSolution preview, trial;
        private int capturedId;
        private bool temporaryErase, movingSelection, copyingSelection;
        private Transform hiddenBakedOutput;

        private void DrawScene(SceneView scene)
        {
            if (!CanEdit || !captureBrush || root.frozen)
            {
                if (from.HasValue) CancelStroke();
                return;
            }
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(id);
            if ((e.type == EventType.Ignore || e.type == EventType.MouseLeaveWindow) && from.HasValue) { CancelStroke(); scene.Repaint(); return; }
            if (e.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
            {
                if (e.keyCode == KeyCode.Escape && from.HasValue) { CancelStroke(); e.Use(); scene.Repaint(); return; }
                if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && brush == Brush.Select && selection.Count > 0)
                {
                    if (GroundLocked()) return;
                    Edit("删除所选实体", () => map.Edit(grid => grid.Paint(selection, null) > 0));
                    selection.Clear();
                    e.Use();
                    return;
                }
            }

            var plane = new Plane(root.transform.forward, root.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!plane.Raycast(ray, out float distance)) return;
            var mapped = root.transform.InverseTransformPoint(ray.GetPoint(distance));
            var cell = new TerrainPoint(Mathf.FloorToInt(mapped.x / map.cellSize), Mathf.FloorToInt(mapped.y / map.cellSize));
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == id)
            {
                if (brush == Brush.Inspect) Inspect(mapped);
                else if (brush == Brush.Stamp) PlaceStamp(mapped);
                else if (!GroundLocked() || brush == Brush.Select)
                {
                    if (brush != Brush.Select && brush != Brush.Erase && !e.shift && kit == null) status = "请先选择绘制套件。";
                    else BeginStroke(id, cell, e);
                }
                e.Use();
                scene.Repaint();
            }
            if (from.HasValue && GUIUtility.hotControl == capturedId && e.type == EventType.MouseDrag)
            {
                if (!cell.Equals(to)) UpdateStroke(cell);
                e.Use();
                scene.Repaint();
            }
            if (from.HasValue && GUIUtility.hotControl == capturedId && e.type == EventType.MouseUp && e.button == 0)
            {
                if (!cell.Equals(to)) UpdateStroke(cell);
                CommitStroke();
                e.Use();
                scene.Repaint();
            }
            if (e.type == EventType.MouseMove) scene.Repaint();
            if (e.type != EventType.Repaint) return;
            if (preview != null) TerrainWorkshopBake.DrawPreview(root.transform, preview);
            if (showGuides && result != null) DrawGuides();
            if (showCollision) DrawCollision(draft ?? map.Read());
            if (brush == Brush.Select)
                foreach (var point in selection) DrawCell(point, new Color(.37f, .85f, .95f, .22f));
            if (brush != Brush.Inspect && brush != Brush.Stamp)
                DrawCell(cell, temporaryErase || brush == Brush.Erase ? new Color(.95f, .36f, .29f, .35f) : new Color(.56f, .9f, .85f, .30f));
            if (brush == Brush.Stamp) DrawStampGuide(mapped);
            if (picked != null)
                DrawBounds(picked.Bounds, new Color(1, .76f, .28f, .13f), new Color(1, .80f, .35f, 1));
            if (issue != null)
                DrawBounds(new Rect(issue.Position - Vector2.one * .19f, Vector2.one * .38f), new Color(.9f, .24f, .21f, .5f), Color.red);
        }

        private bool GroundLocked() => map.Layer(TerrainLayerKind.Body)?.locked == true;

        private void BeginStroke(int id, TerrainPoint cell, Event e)
        {
            capturedId = id;
            GUIUtility.hotControl = id;
            from = to = cell;
            movingSelection = brush == Brush.Select && selection.Contains(cell) && !GroundLocked();
            copyingSelection = movingSelection && (e.control || e.command);
            temporaryErase = e.shift && brush != Brush.Select;
            baseGrid = map.Read();
            UpdateStroke(cell);
        }

        private void UpdateStroke(TerrainPoint current)
        {
            TerrainPoint start = from.Value;
            if (brush == Brush.Select && !movingSelection)
            {
                selection.Clear();
                foreach (var point in TerrainGrid.Rectangle(start, current))
                    if (baseGrid.TryGetKit(point, out _)) selection.Add(point);
                to = current;
                return;
            }
            draft = new TerrainGrid(baseGrid.Tiles);
            if (movingSelection)
            {
                draft.TransformSelection(selection, new TerrainPoint(current.x - start.x, current.y - start.y), copyingSelection);
            }
            else
            {
                if (brush == Brush.Rectangle || brush == Brush.Line) stroke.Clear();
                var points = brush == Brush.Rectangle ? TerrainGrid.Rectangle(start, current) :
                    TerrainGrid.Line(brush == Brush.Line ? start : to, current);
                foreach (var point in points)
                    for (int y = 0; y < brushWidth; y++)
                    for (int x = 0; x < brushWidth; x++)
                        stroke.Add(new TerrainPoint(point.x + x - brushWidth / 2, point.y + y - brushWidth / 2));
                draft.Paint(stroke, temporaryErase || brush == Brush.Erase ? null : kit?.Id, brush == Brush.Material);
            }
            to = current;
            var affected = (movingSelection
                ? selection.Concat(selection.Select(point => new TerrainPoint(point.x + current.x - start.x, point.y + current.y - start.y)))
                : stroke).ToArray();
            if (affected.Length > 0)
            {
                preview = TerrainAssembly.Solve(map, draft);
                if (hiddenBakedOutput == null)
                {
                    hiddenBakedOutput = root.transform;
                    TerrainWorkshopBake.SetBakedVisible(hiddenBakedOutput, false);
                }
            }
        }

        private void CommitStroke()
        {
            var start = from.Value;
            var last = to;
            var points = stroke.ToArray();
            bool move = movingSelection, copy = copyingSelection, erase = temporaryErase || brush == Brush.Erase;
            CancelStroke();
            if (brush == Brush.Select && !move) return;
            if (move)
            {
                var offset = new TerrainPoint(last.x - start.x, last.y - start.y);
                if (offset.x == 0 && offset.y == 0) return;
                var next = map.Read();
                if (!next.TransformSelection(selection, offset, copy)) { status = "目标区域已有未选择的实体。"; return; }
                Edit(copy ? "复制选区" : "移动选区", () => map.Edit(grid => grid.TransformSelection(selection, offset, copy)));
                var moved = selection.Select(point => new TerrainPoint(point.x + offset.x, point.y + offset.y)).ToArray();
                selection.Clear();
                selection.UnionWith(moved);
            }
            else if (points.Length > 0)
            {
                string id = erase ? null : kit?.Id;
                var probe = map.Read();
                if (probe.Paint(points, id, brush == Brush.Material) == 0) return;
                Edit(erase ? "擦除地形" : BrushLabels[(int)brush] + "地形", () => map.Edit(grid => grid.Paint(points, id, brush == Brush.Material) > 0));
            }
        }

        private void CancelStroke()
        {
            if (hiddenBakedOutput != null) TerrainWorkshopBake.SetBakedVisible(hiddenBakedOutput, true);
            hiddenBakedOutput = null;
            if (capturedId != 0 && GUIUtility.hotControl == capturedId) GUIUtility.hotControl = 0;
            capturedId = 0;
            from = null;
            draft = null;
            baseGrid = null;
            preview = null;
            stroke.Clear();
            movingSelection = copyingSelection = temporaryErase = false;
        }

        private void Inspect(Vector2 local)
        {
            picked = result?.Pick(local);
            issue = picked == null ? result?.Issues.OrderBy(item => (item.Position - local).sqrMagnitude).FirstOrDefault() : null;
            if (issue != null && (issue.Position - local).sqrMagnitude > map.cellSize * map.cellSize) issue = null;
            if (picked != null && map.Kit(picked.Kit) != null) kit = map.Kit(picked.Kit);
            tab = 2;
            Repaint();
        }

        private void PlaceStamp(Vector2 local)
        {
            var layer = map.Layer(stampLayer);
            var art = shelf.Item ?? layer?.art;
            if (layer == null || layer.locked || art == null || art.Image == null)
            { status = "请选择可用图件，并确认目标图层已解锁。"; Repaint(); return; }
            if (stampLayer == TerrainLayerKind.Body || stampLayer == TerrainLayerKind.Border)
            { status = "实体和饰边由地形规则生成。请选择布景或装饰层盖章。"; Repaint(); return; }
            var stamp = new TerrainStamp { art = art, layer = stampLayer, position = local / map.cellSize, size = StampSize(art) };
            if (stampLayer == TerrainLayerKind.Decor)
            {
                var support = result?.Pick(local, map.cellSize * .65f);
                if (support == null || support.Layer != TerrainLayerKind.Border)
                { status = "附着装饰需要靠近一个已生成的饰边片段。"; Repaint(); return; }
                stamp.anchor = support.Key;
            }
            Edit("盖章 " + art.name, () => map.stamps.Add(stamp));
        }

        private static Vector2 StampSize(TerrainArt art) => new Vector2(2.6f, 2.6f * art.PixelSize.y / Mathf.Max(1, art.PixelSize.x));

        private void DrawStampGuide(Vector2 local)
        {
            var layer = map.Layer(stampLayer);
            var art = shelf.Item ?? layer?.art;
            if (art == null || art.Image == null || layer == null || layer.locked) return;
            var size = StampSize(art) * map.cellSize;
            var rect = new Rect(local - size * .5f, size);
            var piece = new TerrainPart
            {
                Art = art, UV = art.UV, Tint = new Color(1, 1, 1, .65f), Layer = stampLayer,
                A = rect.min, B = new Vector2(rect.xMin, rect.yMax), C = rect.max, D = new Vector2(rect.xMax, rect.yMin)
            };
            TerrainWorkshopBake.DrawPreview(root.transform, new TerrainSolution(new List<TerrainPart> { piece }, new List<TerrainIssue>()));
        }

        private void DrawGuides()
        {
            using var scope = new Handles.DrawingScope(new Color(.52f, .86f, .98f), root.transform.localToWorldMatrix);
            foreach (var part in result.Parts.Where(item => item.Layer == TerrainLayerKind.Border && item.Start != item.End))
            {
                Handles.DrawLine(part.Start, part.End);
                Handles.DotHandleCap(0, part.Start, Quaternion.identity, .045f * map.cellSize, EventType.Repaint);
            }
        }

        private void DrawCollision(TerrainGrid grid)
        {
            foreach (var box in grid.CollisionBoxes())
                DrawBounds(new Rect(box.X * map.cellSize, box.Y * map.cellSize, box.Width * map.cellSize, box.Height * map.cellSize),
                    Color.clear, new Color(.52f, 1, .53f, .9f));
        }

        private void DrawCell(TerrainPoint cell, Color color) => DrawBounds(new Rect(cell.x * map.cellSize, cell.y * map.cellSize,
            map.cellSize, map.cellSize), color, new Color(color.r, color.g, color.b, .7f));

        private void DrawBounds(Rect rect, Color fill, Color border)
        {
            var points = new[]
            {
                new Vector3(rect.xMin, rect.yMin, -.1f), new Vector3(rect.xMin, rect.yMax, -.1f),
                new Vector3(rect.xMax, rect.yMax, -.1f), new Vector3(rect.xMax, rect.yMin, -.1f)
            };
            Handles.DrawSolidRectangleWithOutline(points.Select(root.transform.TransformPoint).ToArray(), fill, border);
        }

        private void DrawTrial()
        {
            if (kit == null) return;
            if (trial == null)
            {
                var board = CreateInstance<TerrainMap>();
                board.hideFlags = HideFlags.HideAndDontSave;
                board.kits = new[] { kit };
                var grid = board.Read();
                grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(0, 0), new TerrainPoint(10, 2)), kit.Id);
                grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(7, 3), new TerrainPoint(10, 4)), kit.Id);
                grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(2, 1), new TerrainPoint(4, 1)), null);
                grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(12, 0), new TerrainPoint(12, 0)), kit.Id);
                grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(14, 0), new TerrainPoint(15, 3)), kit.Id);
                trial = TerrainAssembly.Solve(board, grid);
                DestroyImmediate(board);
            }
            EditorGUILayout.LabelField("实时试拼 · 洞口、平台和薄墙", EditorStyles.boldLabel);
            var surface = GUILayoutUtility.GetRect(1, 164, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) DrawTrialParts(surface);
            if (trial.Issues.Count > 0) EditorGUILayout.LabelField($"试拼中有 {trial.Issues.Count} 个需要核对的片段。");
        }

        private void DrawTrialParts(Rect surface)
        {
            EditorGUI.DrawRect(surface, new Color(.14f, .20f, .26f));
            float scale = Mathf.Min(surface.width / 18, surface.height / 7);
            Vector2 Screen(Vector2 point) => new Vector2(surface.x + scale * (1 + point.x), surface.yMax - scale * (1 + point.y));
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                foreach (var part in trial.Parts)
                {
                    Vector2 basePoint = Screen(part.B);
                    Vector2 horizontal = Screen(part.C) - basePoint, vertical = Screen(part.A) - basePoint;
                    var matrix = Matrix4x4.identity;
                    matrix.SetColumn(0, new Vector4(horizontal.x, horizontal.y, 0, 0));
                    matrix.SetColumn(1, new Vector4(vertical.x, vertical.y, 0, 0));
                    matrix.SetColumn(3, new Vector4(basePoint.x, basePoint.y, 0, 1));
                    GUI.matrix = previousMatrix * matrix;
                    GUI.color = part.Tint;
                    Rect uv = part.UV;
                    if (part.FlipX) { uv.x += uv.width; uv.width = -uv.width; }
                    if (part.FlipY) { uv.y += uv.height; uv.height = -uv.height; }
                    GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1, 1),
                        part.Art == null ? Texture2D.whiteTexture : part.Art.Image, uv, true);
                }
            }
            finally { GUI.matrix = previousMatrix; GUI.color = previousColor; }
        }
    }
}
