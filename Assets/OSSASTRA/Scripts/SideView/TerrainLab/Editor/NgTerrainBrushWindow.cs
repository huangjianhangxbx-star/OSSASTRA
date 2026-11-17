using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.SideView.TerrainLab
{
    public sealed class NgTerrainBrushWindow : EditorWindow
    {
        [SerializeField] private NgTerrainLayout layout;
        [SerializeField] private int material;
        [SerializeField] private bool editing, erase;
        private readonly HashSet<TerrainCell> stroke = new HashSet<TerrainCell>();
        private TerrainCell? last;
        private int dragControl;
        private bool eraseStroke;

        [MenuItem("OSSASTRA/横版/打开 Never Grave 地形画板")]
        private static void ShowBrush() => GetWindow<NgTerrainBrushWindow>("横版地形画板");

        private void OnEnable() => SceneView.duringSceneGui += DrawScene;

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScene;
            Cancel();
        }

        private void OnGUI()
        {
            var selected = (NgTerrainLayout)EditorGUILayout.ObjectField("地形", layout, typeof(NgTerrainLayout), true);
            if (selected != layout)
            {
                Cancel();
                layout = selected;
            }
            if (layout == null || !layout.gameObject.scene.path.Contains("/PrivateValidation/NG-TERRAIN-01/")) return;
            editing = EditorGUILayout.Toggle("启用画笔", editing);
            if (!editing && last.HasValue) Cancel();
            if (layout.Styles.Count > 0)
            {
                var names = layout.Styles.Select(style => style != null ? style.name : "缺少套件").ToArray();
                material = EditorGUILayout.Popup("套件", Mathf.Clamp(material, 0, layout.Styles.Count - 1), names);
            }
            erase = EditorGUILayout.Toggle("擦除", erase);
            EditorGUILayout.HelpBox("在 Scene 中左键拖动。Shift 临时擦除，Esc 取消，Ctrl+Z 撤销一笔。红色框为缺少图件。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("保存样板场景")) EditorSceneManager.SaveScene(layout.gameObject.scene);
            }
        }

        private void DrawScene(SceneView scene)
        {
            if (!editing || layout == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                !layout.gameObject.scene.path.Contains("/PrivateValidation/NG-TERRAIN-01/") || layout.Styles.Count == 0) return;
            var current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout && !current.alt) HandleUtility.AddDefaultControl(control);
            if (last.HasValue && (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape ||
                current.type == EventType.Ignore || current.type == EventType.MouseLeaveWindow))
            {
                Cancel();
                if (current.type == EventType.KeyDown) current.Use();
                scene.Repaint();
                return;
            }
            if (last.HasValue && GUIUtility.hotControl == dragControl && current.type == EventType.MouseUp && current.button == 0)
            {
                var cells = stroke.ToArray();
                int tool = eraseStroke ? NgTerrainGrid.Erase : material;
                Cancel();
                NgTerrainAuthoring.Paint(layout, cells, tool);
                current.Use();
                scene.Repaint();
                return;
            }

            var plane = new Plane(layout.transform.forward, layout.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!plane.Raycast(ray, out float distance)) return;
            var cell = layout.CellAt(ray.GetPoint(distance));
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && HandleUtility.nearestControl == control)
            {
                dragControl = control;
                GUIUtility.hotControl = control;
                eraseStroke = erase || current.shift;
                stroke.Add(cell);
                last = cell;
                current.Use();
            }
            else if (last.HasValue && GUIUtility.hotControl == dragControl && current.type == EventType.MouseDrag)
            {
                stroke.UnionWith(NgTerrainGrid.Stroke(last.Value, cell));
                last = cell;
                current.Use();
            }
            if (current.type == EventType.Repaint)
            {
                DrawGuide(cell);
                if (last.HasValue) NgTerrainBrushPreview.Draw(scene, layout, stroke, last.Value, eraseStroke ? NgTerrainGrid.Erase : material);
            }
            if (current.type == EventType.MouseMove || current.type == EventType.Used) scene.Repaint();
        }

        private void DrawGuide(TerrainCell hovered)
        {
            var selected = layout.Styles[Mathf.Clamp(material, 0, layout.Styles.Count - 1)];
            bool deleting = last.HasValue ? eraseStroke : erase || Event.current.shift;
            Color tint = deleting ? new Color(1, .22f, .17f, .65f) : selected != null ? selected.guideColor : Color.red;
            foreach (var cell in last.HasValue ? (IEnumerable<TerrainCell>)stroke : new[] { hovered })
            {
                var low = new Vector3(cell.x, cell.y, -.03f) * layout.CellSize;
                var right = Vector3.right * layout.CellSize;
                var up = Vector3.up * layout.CellSize;
                var corners = new[] { low, low + up, low + up + right, low + right }
                    .Select(layout.transform.TransformPoint).ToArray();
                Handles.DrawSolidRectangleWithOutline(corners, tint, new Color(tint.r, tint.g, tint.b, 1));
            }
        }

        private void Cancel()
        {
            if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragControl = 0;
            last = null;
            stroke.Clear();
        }
    }
}
