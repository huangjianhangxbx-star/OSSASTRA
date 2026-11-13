using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.Characters.Rendering;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ossastra.WorldRendering.SideView
{
    public static class SideViewAceOriginalAuthoring
    {
        private const string ScenePath = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string Local = "Assets/OSSASTRA/SideView/LocalReplica";
        private const string Folder = Local + "/OriginalRole";
        private const string DataPath = Folder + "/EsReference.asset";
        private const string MaterialPath = Folder + "/EsReference.mat";

        [Serializable] private sealed class Source
        {
            public string character;
            public int ticksPerSecond;
            public SideViewAceOriginalReferenceData.Palette[] palettes;
            public SideViewAceOriginalReferenceData.Clip[] clips;
            public SourceMesh[] meshes;
            public SourcePage[] pages;
        }

        [Serializable] private sealed class SourceMesh
        {
            public string name;
            public int page;
            public Vector3[] vertices;
            public Vector4[] uv;
            public int[] triangles;
        }

        [Serializable] private sealed class SourcePage
        {
            public int index, width, height;
            public string[] main;
            public string extra, extra2;
        }

        public static string Install(string sourceFolder)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在已保存的正式横版场编辑状态接入。");
            var comparison = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SideViewRenderComparison>(true)).Single();
            var coordinator = new SerializedObject(comparison);
            var host = Property(coordinator, "aceCharacterControls").objectReferenceValue as GameObject;
            var lights = Property(coordinator, "aceLighting").objectReferenceValue as SideViewAceLighting;
            var characters = comparison.GetComponent<SideViewCharacterRendering>();
            var shader = Shader.Find("Ossastra/SideView/AceOriginalRole");
            var option = host == null ? null : host.transform.Find("SelectFusion");
            var inheritedLabel = option == null ? null : option.GetComponentInChildren<Text>(true);
            var diagnosticPanel = host == null ? null : host.transform.Find("LightDiagnosticsPanel");
            if (host == null || lights == null || characters == null || shader == null || !shader.isSupported ||
                inheritedLabel == null || inheritedLabel.font == null || diagnosticPanel == null || host.transform.Find("OriginalReference") != null)
                throw new InvalidOperationException("缺少 ACE 融合、字体或参考材质，或参考角色已接入。");
            if (!AssetDatabase.IsValidFolder(Local) || AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("本机参考资产目录不存在或目标已占用 " + Folder);
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.BC7))
                throw new InvalidOperationException("当前设备不支持原始 BC7 图页。");

            string manifest = Path.Combine(sourceFolder, "EsReference.json");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(manifest));
            Validate(source, sourceFolder);
            AssetDatabase.CreateFolder(Local, "OriginalRole");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install ACE original visual reference");
            try
            {
                var data = CreateAssets(source, sourceFolder, shader, out var material);
                var root = Node("EsVisualReference", null, typeof(MeshFilter), typeof(MeshRenderer), typeof(SideViewAceOriginalReference));
                root.transform.position = new Vector3(46f, 3.035f, 0f);
                root.transform.localScale = Vector3.one * 3.15f;
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.enabled = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var lamp = AddDiagnosticLamp(lights, root.transform.position);
                var reference = root.GetComponent<SideViewAceOriginalReference>();
                reference.Configure(data, comparison, lamp);
                var roles = new SerializedObject(characters);
                Property(roles, "startWithCalibratedAceRim").boolValue = true;
                roles.ApplyModifiedProperties();

                BuildControls(host.transform, diagnosticPanel, inheritedLabel.font, reference, characters);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("正式横版场保存失败。");
                Undo.CollapseUndoOperations(group);
                return $"{source.character} · {source.clips.Length} clips · {source.palettes.Length} palettes · {source.meshes.Length} meshes";
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                AssetDatabase.DeleteAsset(Folder);
                throw;
            }
        }

        private static void Validate(Source source, string folder)
        {
            if (source == null || source.ticksPerSecond <= 0 || source.palettes == null || source.palettes.Length != 3 ||
                source.clips == null || source.clips.Length != 3 || source.pages == null || source.pages.Length == 0 ||
                source.meshes == null || source.meshes.Length == 0)
                throw new InvalidDataException("原作样本清单缺少三组配色、动作或网格。");
            var pages = source.pages.ToDictionary(page => page.index);
            foreach (var page in source.pages)
            {
                if (page.width < 4 || page.height < 4 || page.width % 4 != 0 || page.height % 4 != 0 ||
                    page.main.Length != source.palettes.Length)
                    throw new InvalidDataException("不完整的 BC7 图页 " + page.index);
                foreach (var file in page.main.Concat(new[] { page.extra, page.extra2 }))
                    if (new FileInfo(RawPath(folder, file)).Length != (long)page.width * page.height)
                        throw new InvalidDataException("源 BC7 字节数不匹配：" + file);
            }
            foreach (var mesh in source.meshes)
                if (!pages.ContainsKey(mesh.page) || mesh.vertices.Length < 3 || mesh.uv.Length != mesh.vertices.Length ||
                    mesh.triangles.Length % 3 != 0 || mesh.triangles.Any(index => index < 0 || index >= mesh.vertices.Length))
                    throw new InvalidDataException("源网格的页、UV 或三角索引不匹配：" + mesh.name);
            foreach (var clip in source.clips)
            {
                int previous = -1;
                foreach (var frame in clip.frames)
                {
                    if (frame.mesh < 0 || frame.mesh >= source.meshes.Length || source.meshes[frame.mesh].page != frame.page ||
                        frame.endTick <= previous)
                        throw new InvalidDataException("源帧映射有误：" + clip.name);
                    previous = frame.endTick;
                }
                if (previous < 0 || previous + 1 != clip.totalTicks)
                    throw new InvalidDataException("源动作长度不匹配：" + clip.name);
            }
        }

        private static string RawPath(string folder, string filename)
        {
            if (string.IsNullOrEmpty(filename) || Path.GetFileName(filename) != filename)
                throw new InvalidDataException("图页路径须指向样本目录内的文件。");
            return Path.Combine(folder, Path.ChangeExtension(filename, ".bc7"));
        }

        private static SideViewAceOriginalReferenceData CreateAssets(Source source, string folder, Shader shader, out Material material)
        {
            var data = ScriptableObject.CreateInstance<SideViewAceOriginalReferenceData>();
            AssetDatabase.CreateAsset(data, DataPath);
            var meshes = new Mesh[source.meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var original = source.meshes[i];
                var mesh = new Mesh {name = original.name, vertices = original.vertices, triangles = original.triangles};
                mesh.SetUVs(0, new List<Vector4>(original.uv));
                mesh.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(mesh, data);
                meshes[i] = mesh;
            }
            var pages = source.pages.Select(page => new SideViewAceOriginalReferenceData.Page
            {
                index = page.index, main = page.main.Select(file => ImportPage(file, page, folder, data)).ToArray(),
                extra = ImportPage(page.extra, page, folder, data),
                extra2 = ImportPage(page.extra2, page, folder, data)
            }).ToArray();
            data.Configure(source.ticksPerSecond, source.palettes, pages, source.clips, meshes);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            material = new Material(shader) {name = "Es · original PaletteAnimation receiver"};
            AssetDatabase.CreateAsset(material, MaterialPath);
            return data;
        }

        private static Texture2D ImportPage(string filename, SourcePage page, string folder,
            SideViewAceOriginalReferenceData owner)
        {
            // The original project samples these numeric BC7 values in Gamma. Composition decodes once at output.
            var texture = new Texture2D(page.width, page.height, TextureFormat.BC7, false, true)
            {
                name = Path.GetFileNameWithoutExtension(filename), filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.LoadRawTextureData(File.ReadAllBytes(RawPath(folder, filename)));
            texture.Apply(false, false);
            AssetDatabase.AddObjectToAsset(texture, owner);
            return texture;
        }

        private static SideViewAceLight AddDiagnosticLamp(SideViewAceLighting owner, Vector3 anchor)
        {
            var lamp = Node("OriginalRoleChannelDiagnostic", owner.transform, typeof(SideViewAceLight)).GetComponent<SideViewAceLight>();
            lamp.transform.position = anchor + new Vector3(-1.95f, 2.6f, -.72f);
            lamp.enabled = false;
            var input = new SerializedObject(lamp);
            Property(input, "groups").intValue = (int)SideViewAceLightGroups.Character;
            Property(input, "size").vector2Value = new Vector2(7, 6);
            Property(input, "intensity").floatValue = 1.2f;
            Property(input, "ambient").colorValue = new Color(.84f, .72f, .92f, .65f);
            Property(input, "bright").colorValue = new Color(.64f, .48f, .72f, .75f);
            Property(input, "highlight").colorValue = new Color(.83f, .68f, 1.05f, .85f);
            Property(input, "edgeOffset").floatValue = 1.4f;
            Property(input, "emission").floatValue = 1f;
            input.ApplyModifiedProperties();
            var registry = new SerializedObject(owner);
            var array = Property(registry, "lights");
            int index = array.arraySize;
            array.arraySize = index + 1;
            array.GetArrayElementAtIndex(index).objectReferenceValue = lamp;
            registry.ApplyModifiedProperties();
            return lamp;
        }

        private static void BuildControls(Transform host, Transform diagnostic, Font font,
            SideViewAceOriginalReference reference, SideViewCharacterRendering characters)
        {
            var branch = Node("OriginalReference", host, typeof(RectTransform), typeof(SideViewAceOriginalControls));
            Top(branch.GetComponent<RectTransform>(), 0, 0, 1, 1);
            var controls = branch.GetComponent<SideViewAceOriginalControls>();
            AddButton(branch.transform, font, "Open", "苍翼角色参考", 0, -112, 148, controls.TogglePanel);
            var panel = Node("ReferenceFoldout", branch.transform, typeof(RectTransform), typeof(Image));
            var panelRect = panel.GetComponent<RectTransform>();
            Top(panelRect, -360, -126, 450, 282);
            panelRect.pivot = new Vector2(.5f, 1);
            panel.GetComponent<Image>().color = new Color(.065f, .10f, .12f, .98f);
            var readout = AddLabel(panel.transform, font, "Status", "", 0, -27, 430, 44);
            readout.fontSize = 14;

            AddButton(panel.transform, font, "Visibility", "显示 / 隐藏", -146, -68, 136, controls.ToggleVisible);
            AddButton(panel.transform, font, "PlayPause", "播放 / 暂停", 0, -68, 136, controls.TogglePlayback);
            AddButton(panel.transform, font, "Material", "完整 / 基础", 146, -68, 136, controls.ToggleMaterial);
            AddButton(panel.transform, font, "Idle", "待机", -146, -105, 136, controls.SelectClip, 0);
            AddButton(panel.transform, font, "Run", "跑动", 0, -105, 136, controls.SelectClip, 1);
            AddButton(panel.transform, font, "Attack", "攻击", 146, -105, 136, controls.SelectClip, 2);
            AddButton(panel.transform, font, "SkinDefault", "默认色", -146, -142, 136, controls.SelectPalette, 0);
            AddButton(panel.transform, font, "Skin03", "原作配色 03", 0, -142, 136, controls.SelectPalette, 1);
            AddButton(panel.transform, font, "Skin04", "原作配色 04", 146, -142, 136, controls.SelectPalette, 2);
            AddButton(panel.transform, font, "LightPreset", "主场灯 / 诊断灯", -146, -178, 136, controls.ToggleLighting);
            AddButton(panel.transform, font, "Bright", "亮部贡献", 0, -178, 136, controls.ToggleChannel, 0);
            AddButton(panel.transform, font, "Highlight", "定向高亮", 146, -178, 136, controls.ToggleChannel, 1);
            AddButton(panel.transform, font, "Emission", "自发光", -146, -215, 136, controls.ToggleChannel, 2);
            AddButton(panel.transform, font, "ProjectRim", "项目内边光预设", 73, -215, 282, controls.ToggleRimPreset);
            var slider = AddSlider(panel.transform, font, controls);
            controls.Configure(reference, characters, panel, readout, slider);
            AddButton(diagnostic, font, "RimPreset", "内边光 · 克制 / 增强", 80, -157, 148, controls.ToggleRimPreset);
            panel.SetActive(false);
        }

        private static Slider AddSlider(Transform parent, Font font, SideViewAceOriginalControls controls)
        {
            AddLabel(parent, font, "TimelineLabel", "逐帧", -181, -253, 68, 22);
            var go = Node("Timeline", parent, typeof(RectTransform), typeof(Image), typeof(Slider));
            Top(go.GetComponent<RectTransform>(), 30, -253, 354, 19);
            go.GetComponent<Image>().color = new Color(.22f, .29f, .32f, 1);
            var area = Node("HandleTrack", go.transform, typeof(RectTransform)).GetComponent<RectTransform>();
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(7, 0);
            area.offsetMax = new Vector2(-7, 0);
            var handle = Node("Handle", area, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(14, 0);
            handle.color = new Color(.44f, .91f, .8f, 1);
            var slider = go.GetComponent<Slider>();
            slider.wholeNumbers = true;
            slider.maxValue = 263;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controls.Seek);
            return slider;
        }

        private static void AddButton(Transform parent, Font font, string name, string label, float x, float y,
            float width, UnityAction action)
        {
            var button = ButtonAt(parent, font, name, label, x, y, width);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static void AddButton(Transform parent, Font font, string name, string label, float x, float y,
            float width, UnityAction<int> action, int argument)
        {
            var button = ButtonAt(parent, font, name, label, x, y, width);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
        }

        private static Button ButtonAt(Transform parent, Font font, string name, string label, float x, float y, float width)
        {
            var item = Node(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Top(item.GetComponent<RectTransform>(), x, y, width, 31);
            var background = item.GetComponent<Image>();
            background.color = new Color(.12f, .22f, .25f, 1);
            var button = item.GetComponent<Button>();
            button.targetGraphic = background;
            var text = AddLabel(item.transform, font, "Label", label, 0, 0, width, 31).rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = text.offsetMax = Vector2.zero;
            return button;
        }

        private static Text AddLabel(Transform parent, Font font, string name, string value, float x, float y, float width, float height)
        {
            var text = Node(name, parent, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Top(text.rectTransform, x, y, width, height);
            text.font = font;
            text.text = value;
            text.color = new Color(.93f, .97f, .96f, 1);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 15;
            text.raycastTarget = false;
            return text;
        }

        private static void Top(RectTransform item, float x, float y, float width, float height)
        {
            item.anchorMin = item.anchorMax = new Vector2(.5f, 1);
            item.anchoredPosition = new Vector2(x, y);
            item.sizeDelta = new Vector2(width, height);
        }

        private static GameObject Node(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(go, "Add ACE visual reference");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Parent ACE visual reference");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static SerializedProperty Property(SerializedObject item, string name) =>
            item.FindProperty(name) ?? throw new InvalidOperationException("找不到 ACE 字段 " + name);
    }
}
