/*
 * Scene-local rendering bindings and functional rest menu for the Tower rooms.
 * Local art conversion is optional. Placement and collision are authored independently.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.WorldRendering.SideView;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.Slice
{
    public static partial class SVSliceSceneAuthoring
    {
        private static readonly Color Shadow = new Color(.12f, .052f, .075f);
        private static readonly Color Stone = new Color(.68f, .43f, .47f);
        private static readonly Color Rubble = new Color(.75f, .51f, .46f);
        private static readonly Color Floor = new Color(.83f, .48f, .49f);
        private static readonly Color BlueLamp = new Color(.28f, .85f, .91f);
        private static readonly Color RedLamp = new Color(.83f, .22f, .26f);
        private static readonly Color FloorSide = new Color(.26f, .104f, .13f);

        private static void EnableRenderControls(RoomContext context)
        {
            var comparison = MustFind<SideViewRenderComparison>(context.Scene);
            if (!comparison.TryGetComponent<SVSliceRenderSelection>(out _))
                comparison.gameObject.AddComponent<SVSliceRenderSelection>();
            var serialized = new SerializedObject(comparison);
            if (serialized.FindProperty("environment").objectReferenceValue is Component environment)
                environment.gameObject.SetActive(true);
            foreach (var root in context.Scene.GetRootGameObjects())
                if (root.name == "SideViewComparisonControls" || root.name == "EsVisualReference")
                    root.SetActive(true);
            var reference = MustFind<SideViewAceOriginalReference>(context.Scene);
            var referenceSettings = new SerializedObject(reference);
            referenceSettings.FindProperty("showInAllRecipes").boolValue = true;
            referenceSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireComparison(RoomContext context, RoomDeck deck)
        {
            EnableRenderControls(context);
            var comparison = MustFind<SideViewRenderComparison>(context.Scene);
            MustFind<SideViewComparisonCardCatalog>(context.Scene).SetAuthoringEntries(deck.Cards.ToArray());
            var so = new SerializedObject(comparison);
            // Tower dressing is sheltered throughout and owns its own foreground silhouettes.
            var areas = so.FindProperty("legacyInteriorRegions");
            areas.arraySize = 1;
            areas.GetArrayElementAtIndex(0).rectValue = new Rect(-42f, -15f, 84f, 30f);
            so.FindProperty("darknessSourceRoot").objectReferenceValue = deck.Presentation;
            so.FindProperty("useAuthoredSilksongMasks").boolValue = false;
            var points = so.FindProperty("legacyEnvironment").FindPropertyRelative("localLights");
            points.arraySize = deck.Lamps.Count;
            for (var i = 0; i < deck.Lamps.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = deck.Lamps[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            var ace = MustFind<SideViewAceLighting>(context.Scene);
            var aceSo = new SerializedObject(ace);
            var array = aceSo.FindProperty("lights");
            var lights = deck.AceLights.ToList();
            var reference = new SerializedObject(MustFind<SideViewAceOriginalReference>(context.Scene));
            if (reference.FindProperty("diagnosticLight").objectReferenceValue is SideViewAceLight diagnostic)
                lights.Add(diagnostic);
            array.arraySize = lights.Count;
            for (var i = 0; i < lights.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            aceSo.FindProperty("worldAmbient").colorValue = new Color(.77f, .63f, .65f);
            aceSo.FindProperty("characterAmbient").colorValue = new Color(.64f, .54f, .56f);
            aceSo.ApplyModifiedPropertiesWithoutUndo();
            var es = context.Scene.GetRootGameObjects().FirstOrDefault(o => o.name == "EsVisualReference");
            if (es != null)
                es.transform.position = context.MapId == SliceSceneCatalog.TowerRestMapId
                    ? new Vector3(-4.5f, 0f, 0f)
                    : new Vector3(-deck.Width / 2 + 10f, deck.BaseY, 0f);
        }

        private static void BuildRestMenu(SVSliceRestPoint point) => RebuildRestMenu(point);

        public static void RebuildRestMenu(SVSliceRestPoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (point.panel != null && point.panel.name == "保存椅正式休息界面")
            {
                BindRestFocus(point.saveButton);
                BindRestFocus(point.leaveButton);
                return;
            }
            if (point.panel != null && (point.panel.transform.parent != point.transform || point.panel.name != "保存椅 UI"))
                throw new InvalidOperationException("保存椅的现有界面不是已识别的旧装配：" + point.panel.name);

            var theme = AssetDatabase.LoadAssetAtPath<Ossastra.UI.GameMenu.GameMenuTheme>(
                "Assets/OSSASTRA/SideView/GameMenu/ReferenceTheme.asset");
            var font = theme != null ? theme.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ui = SlicePresentation.Canvas(point.transform, "保存椅正式休息界面", 300, true);
            Undo.RegisterCreatedObjectUndo(ui.gameObject, "Build rest menu");
            Undo.RecordObject(point, "Bind rest menu");
            if (point.panel != null) Undo.DestroyObjectImmediate(point.panel);

            var shade = SlicePresentation.ArtImage(ui, "左侧渐隐", new Rect(0, -60, 860, 1040),
                new Color(0, 0, 0, .72f), "TalkGlow");
            shade.uvRect = new Rect(.5f, 0, .5f, 1);
            SlicePresentation.Label(ui, "区域", new Rect(52, 53, 440, 40), "锁之塔", 22, font,
                SlicePresentation.AccentColor, TextAnchor.MiddleLeft);
            SlicePresentation.ArtImage(ui, "保存点徽记", new Rect(50, 98, 49, 49), Color.white, "RestIcon");
            var title = SlicePresentation.Label(ui, "保存点", new Rect(108, 85, 530, 68), "破落的房间", 42, font,
                SlicePresentation.TextColor, TextAnchor.MiddleLeft);
            SlicePresentation.Shadow(title);
            SlicePresentation.Rule(ui, "地点分隔线", new Rect(36, 164, 533, 2), new Color(1, 1, 1, .4f));

            point.saveButton = BuildRestButton(ui, font, "保存", 186);
            point.leaveButton = BuildRestButton(ui, font, "出发", 256);
            ConnectRestNavigation(point.saveButton, point.leaveButton);
            ConnectRestNavigation(point.leaveButton, point.saveButton);
            point.statusLabel = SlicePresentation.Label(ui, "保存状态", new Rect(84, 335, 410, 48), "", 24,
                font, SlicePresentation.AccentColor, TextAnchor.MiddleLeft);
            SlicePresentation.Shadow(point.statusLabel);

            var footer = SlicePresentation.Node(ui, "操作提示", new Rect(0, 0, 300, 52));
            footer.anchorMin = footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(1, 0);
            footer.anchoredPosition = new Vector2(-24, 21);
            point.confirmGlyph = SlicePresentation.ArtImage(footer, "确认键", new Rect(9, 8, 52, 33), Color.white, "Enter");
            point.cancelGlyph = SlicePresentation.ArtImage(footer, "关闭键", new Rect(150, 8, 52, 33), Color.white, "Escape");
            point.confirmLabel = SlicePresentation.Label(footer, "确定", new Rect(61, 0, 83, 49), "确定", 24, font,
                SlicePresentation.TextColor);
            point.cancelLabel = SlicePresentation.Label(footer, "关闭", new Rect(202, 0, 83, 49), "关闭", 24, font,
                SlicePresentation.TextColor);
            point.hud = point.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Ossastra.UserInterface.CharacterHudView>(true))
                .FirstOrDefault();
            point.panel = ui.gameObject;
            point.panel.SetActive(false);
            EditorUtility.SetDirty(point);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
        }

        private static Button BuildRestButton(RectTransform panel, Font font, string text, float y)
        {
            var baseButton = SlicePresentation.Image(panel, text, new Rect(0, y, 531, 56), Color.white);
            baseButton.raycastTarget = true;
            var button = baseButton.gameObject.AddComponent<Button>();
            button.targetGraphic = baseButton;
            var colors = button.colors;
            colors.normalColor = new Color(1, 1, 1, .01f);
            colors.highlightedColor = new Color(.52f, .83f, .88f, .46f);
            colors.selectedColor = new Color(.52f, .83f, .88f, .82f);
            colors.pressedColor = new Color(.52f, .83f, .88f, .98f);
            colors.disabledColor = new Color(1, 1, 1, .01f);
            button.colors = colors;
            BindRestFocus(button);
            var label = SlicePresentation.Label(baseButton.transform, "文字", new Rect(103, 0, 340, 56), text, 34,
                font, SlicePresentation.AccentColor, TextAnchor.MiddleLeft);
            SlicePresentation.Shadow(label);
            return button;
        }

        private static void ConnectRestNavigation(Button button, Button neighbor)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = neighbor, selectOnDown = neighbor };
        }

        private static void BindRestFocus(Button button)
        {
            if (button == null || !(button.targetGraphic is RawImage image)) return;
            var texture = SlicePresentation.Art("RestFocus") ?? SlicePresentation.Art("ButtonLight");
            if (image.texture == texture) return;
            Undo.RecordObject(image, "Bind rest focus");
            image.texture = texture;
            EditorUtility.SetDirty(image);
        }

        private sealed class RoomDeck
        {
            private readonly Transform physics;
            private readonly LocalTowerArt art;
            public readonly Transform Presentation;
            public readonly float Width;
            public readonly float BaseY;
            public readonly List<SideViewComparisonCardCatalog.Entry> Cards = new();
            public readonly List<Light> Lamps = new();
            public readonly List<SideViewAceLight> AceLights = new();

            public RoomDeck(Transform parent, float width, float baseY, LocalTowerArt art)
            {
                Width = width;
                BaseY = baseY;
                this.art = art;
                physics = NewChild(parent, "横版碰撞", Vector3.zero);
                Presentation = NewChild(parent, "塔区地景", Vector3.zero);
            }

            public void FillArchitecture()
            {
                Card(Presentation, "塔背红雾", new Vector3(0, BaseY + 3, 16), new Vector2(Width + 31, 41),
                    new Color(.48f, .19f, .27f), "FarBackground", 1, true);
                Card(Presentation, "远层墙面", new Vector3(0, BaseY + 3, 12), new Vector2(Width + 20, 33),
                    new Color(.68f, .42f, .47f, .52f), "FarBackground", 2, true, art?.Masonry);
                for (var x = -Width / 2 - 3; x < Width / 2 + 6; x += 8.4f)
                {
                    Card(Presentation, "暗拱远底", new Vector3(x, BaseY + 5.5f, 10.5f), new Vector2(7.9f, 18f),
                        new Color(.51f, .28f, .37f, .78f), "NearBackground", 3, true, art?.Masonry);
                    AddArch(x);
                    Card(Presentation, "低层暗壁", new Vector3(x, BaseY - 1.4f, 5), new Vector2(7.15f, 4.7f),
                        Stone, "NearBackground", 7, true, art?.Masonry);
                }
                for (var i = 0; i < 3; i++)
                {
                    var x = (i - 1) * Width * .27f;
                    var hue = i == 1 ? BlueLamp : RedLamp;
                    AddLamp(new Vector3(x, BaseY + 4.6f, -.5f), hue);
                }
                Card(Presentation, "上缘暗轮廓", new Vector3(0, BaseY + 13.4f, -2f), new Vector2(Width + 12, 5.4f),
                    Shadow, "Structure", 47, false);
            }

            private void AddArch(float x)
            {
                Card(Presentation, "左石柱", new Vector3(x - 3.1f, BaseY + 4, 4.6f), new Vector2(1.20f, 11.3f),
                    Stone, "Structure", 9, true, art?.Masonry);
                Card(Presentation, "右石柱", new Vector3(x + 3.1f, BaseY + 4, 4.6f), new Vector2(1.20f, 11.3f),
                    Stone, "Structure", 9, true, art?.Masonry);
                Card(Presentation, "门拱", new Vector3(x, BaseY + 8.15f, 4.55f), Vector2.one,
                    Stone, "Structure", 11, true, art?.Masonry, ArchMesh());
                Card(Presentation, "柱头", new Vector3(x + 3.08f, BaseY + 6.8f, 4.4f), new Vector2(1.7f, .32f),
                    Rubble, "Structure", 12, true, art?.Masonry);
            }

            public void Platform(string name, float left, float right, float height)
            {
                var width = right - left;
                var center = (left + right) * .5f;
                Wall(name, new Vector3(center, height - .6f, 0), new Vector3(width, 1.2f, 3f));
                Card(Presentation, name + " · 石体", new Vector3(center, height - 2.3f, .7f), new Vector2(width, 4.6f),
                    FloorSide, "Ground", 19, true);
                Card(Presentation, name + " · 踏面", new Vector3(center, height - .14f, -.08f),
                    new Vector2(width, .31f), Floor, "Ground", 22, true, art?.Ground,
                    art?.Ground == null ? null : RepeatQuad(Mathf.Max(1f, width / 1.9f)));
            }

            public void EndWall(float x, float height)
            {
                Wall("场景边界", new Vector3(x, height + 3.8f, 0), new Vector3(.78f, 13f, 3f));
                Card(Presentation, "尽头暗柱", new Vector3(x, height + 5.25f, -1f), new Vector2(1.88f, 12.8f),
                    Shadow, "Structure", 40, false, art?.Masonry);
            }

            public void Wall(string name, Vector3 position, Vector3 size)
            {
                var wall = NewChild(physics, name, position).gameObject.AddComponent<BoxCollider>();
                wall.size = size;
            }

            public Transform ElevatorCar(Vector3 position)
            {
                var host = NewChild(Presentation, "笼式升降机", position);
                host.gameObject.AddComponent<SideViewMovingSupport>();
                var collision = NewChild(host, "承重踏板", new Vector3(0f, -.18f, 0f)).gameObject.AddComponent<BoxCollider>();
                collision.size = new Vector3(4.55f, .36f, 3f);
                var body = host.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                Card(host, "铁制轿厢背板", new Vector3(0f, 1.66f, .35f), new Vector2(4.6f, 3.46f),
                    new Color(.46f, .34f, .34f, .53f), "Structure", 21, true, art?.Iron);
                Card(host, "轿厢托盘", new Vector3(0, -.16f, -.65f), new Vector2(4.75f, .37f),
                    Floor, "Structure", 40, false, art?.Iron);
                if (art?.ElevatorMesh != null)
                {
                    Card(host, "有盖铁笼 · 原作网格", new Vector3(0f, 1.98f, -.6f), new Vector2(1.19f, .65f),
                        Color.white, "Structure", 41, false, art.Elevator, art.ElevatorMesh);
                }
                else
                {
                    Card(host, "铁笼弧顶", new Vector3(0, 2.7f, -.57f), Vector2.one,
                        new Color(.41f, .34f, .33f), "Structure", 40, false, art?.Iron, ArchMesh(.66f, .5f));
                    for (var i = -4; i <= 4; i++)
                        Card(host, "铁笼直杆", new Vector3(i * .52f, 1.48f, -.56f), new Vector2(.095f, 2.97f),
                            new Color(.34f, .30f, .31f), "Structure", 40, false, art?.Iron);
                    Card(host, "铁笼横栏", new Vector3(0f, 2.42f, -.6f), new Vector2(4.4f, .09f),
                        new Color(.38f, .31f, .32f), "Structure", 41, false, art?.Iron);
                }
                Card(host, "黄铜操控座", new Vector3(.05f, 1.44f, -.75f), new Vector2(.40f, .53f),
                    Color.white, "Decoration", 42, false, art?.Switch);
                return host;
            }

            public void Bench()
            {
                Card(Presentation, "长椅横板", new Vector3(0f, .73f, .26f), new Vector2(4.4f, .36f),
                    Rubble, "Decoration", 20, true, art?.Masonry);
                Card(Presentation, "长椅井架", new Vector3(-1.62f, .28f, .28f), new Vector2(.43f, .6f),
                    Stone, "Decoration", 19, true);
                Card(Presentation, "长椅井架", new Vector3(1.62f, .28f, .28f), new Vector2(.43f, .6f),
                    Stone, "Decoration", 19, true);
                if (art?.RestMesh != null)
                    Card(Presentation, "青灯底座 · 原作网格", new Vector3(2.67f, 1.6f, .16f), Vector2.one,
                        Color.white, "Decoration", 23, true, art.Rest, art.RestMesh);
                else
                    Card(Presentation, "青灯光核", new Vector3(2.72f, 1.95f, .22f), new Vector2(.63f, 1.1f),
                        BlueLamp, "Decoration", 25, true);
            }

            public void Crate(Vector3 position)
            {
                Card(Presentation, "旧木箱", position + new Vector3(0, .56f, .4f), new Vector2(1.3f, 1.14f),
                    new Color(.35f, .25f, .19f), "Decoration", 18, true, art?.Iron);
                Card(Presentation, "旧木箱饰板", position + new Vector3(0, .56f, .29f), new Vector2(.99f, .70f),
                    new Color(.23f, .14f, .14f), "Decoration", 19, true, art?.Masonry);
            }

            private void AddLamp(Vector3 place, Color color)
            {
                var loc = NewChild(Presentation, "塔区灯", place);
                var light = loc.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = 10.5f;
                light.intensity = 1.35f;
                Lamps.Add(light);
                var ace = loc.gameObject.AddComponent<SideViewAceLight>();
                var so = new SerializedObject(ace);
                so.FindProperty("ambient").colorValue = color * new Color(.8f, .8f, .8f, 1f);
                so.FindProperty("bright").colorValue = color;
                so.FindProperty("highlight").colorValue = Color.Lerp(color, Color.white, .45f);
                so.FindProperty("size").vector2Value = new Vector2(19f, 14f);
                so.FindProperty("intensity").floatValue = 1.1f;
                so.ApplyModifiedPropertiesWithoutUndo();
                AceLights.Add(ace);
                Card(Presentation, "灯花", place + new Vector3(0, 0, .8f), new Vector2(.68f, .75f),
                    Color.Lerp(color, Color.white, .58f), "Decoration", 24, true, mesh: Halo());
                Card(Presentation, "灯晕", place + new Vector3(0, 0, .9f), new Vector2(6.5f, 6.5f),
                    color * new Color(1f, 1f, 1f, .33f), "Decoration", 23, true, mesh: Halo());
            }

            private void Card(Transform parent, string name, Vector3 position, Vector2 size, Color tint,
                string kind, int order, bool below, Texture2D texture = null, Mesh mesh = null)
            {
                var transform = NewChild(parent, name, position);
                transform.localScale = new Vector3(size.x, size.y, 1);
                var filter = transform.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh != null ? mesh : QuadMesh();
                var renderer = transform.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CardMaterial(tint, texture);
                renderer.sortingOrder = below ? order - 32 : order;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                bool far = kind == "FarBackground";
                Cards.Add(new SideViewComparisonCardCatalog.Entry(renderer, renderer.sharedMaterial,
                    below, far, false, !far, far ? -1f : 0f, far ? -1f : below ? 1f : 0f, .22f,
                    Vector4.one, renderer.sharedMaterial.shader.name, kind));
            }
        }

        private static Mesh QuadMesh() => RepeatQuad(1f);

        private static Mesh RepeatQuad(float repeat)
        {
            var name = "quad_" + Mathf.RoundToInt(repeat * 100);
            return Shape(name, new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) },
                new[] { new Vector2(0, 0), new Vector2(repeat, 0), new Vector2(repeat, 1), new Vector2(0, 1) },
                new[] { 0, 2, 1, 0, 3, 2 });
        }

        private static Mesh ArchMesh(float radiusScale = 1f, float heightScale = 1f)
        {
            const int segments = 22;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                var unit = i / (float)segments;
                var x = Mathf.Cos(unit * Mathf.PI);
                var y = Mathf.Sin(unit * Mathf.PI);
                vertices[i * 2] = new Vector3(x * 3.10f * radiusScale, y * 2.65f * heightScale);
                vertices[i * 2 + 1] = new Vector3(x * 3.64f * radiusScale, y * 3.22f * heightScale);
                uv[i * 2] = new Vector2(unit * 3f, 0f);
                uv[i * 2 + 1] = new Vector2(unit * 3f, 1f);
                if (i == segments) continue;
                var at = i * 6;
                var vi = i * 2;
                triangles[at] = vi;
                triangles[at + 1] = vi + 2;
                triangles[at + 2] = vi + 1;
                triangles[at + 3] = vi + 1;
                triangles[at + 4] = vi + 2;
                triangles[at + 5] = vi + 3;
            }
            return Shape("arch_" + Mathf.RoundToInt(radiusScale * 100) + "_" + Mathf.RoundToInt(heightScale * 100), vertices, uv, triangles);
        }

        private static Mesh Halo()
        {
            const int slices = 24;
            var vertices = new Vector3[slices + 1];
            var uv = new Vector2[vertices.Length];
            var colors = Enumerable.Repeat(new Color32(255, 255, 255, 0), vertices.Length).ToArray();
            var triangles = new int[slices * 3];
            colors[0].a = 255;
            uv[0] = new Vector2(.5f, .5f);
            for (var i = 0; i < slices; i++)
            {
                var angle = i * Mathf.PI * 2f / slices;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
                uv[i + 1] = (Vector2)vertices[i + 1] + uv[0];
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % slices + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            return Shape("halo", vertices, uv, triangles, colors);
        }

        private static Mesh Shape(string name, Vector3[] vertices, Vector2[] uv, int[] triangles, Color32[] colors = null)
        {
            var path = SliceFolder + "/GeneratedArt/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                if (existing.colors32.Length != existing.vertexCount)
                {
                    existing.colors32 = colors ?? WhiteVertices(existing.vertexCount);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.colors32 = colors ?? WhiteVertices(vertices.Length);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Color32[] WhiteVertices(int count)
            => Enumerable.Repeat(new Color32(255, 255, 255, 255), count).ToArray();

        private static Material CardMaterial(Color tint, Texture2D texture)
        {
            var key = ColorUtility.ToHtmlStringRGBA(tint) + "_" + (texture == null ? "Solid" : texture.name);
            var folder = texture == null ? "/GeneratedArt/" : "/LocalArt/";
            var path = SliceFolder + folder + "SV_Tower_" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("未找到 Sprites/Default 背景卡片 Shader。");
            var material = new Material(shader) { color = tint, mainTexture = texture != null ? texture : Texture2D.whiteTexture };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
