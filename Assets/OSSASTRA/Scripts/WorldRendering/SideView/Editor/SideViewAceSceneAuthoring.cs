/*
 * 功能：为已打开的正式横版场登记 ACE 光图、作者灯和材质对照按钮。
 * 边界：原生编辑期接线，只允许已保存的正式场和其独占 Renderer。
 */
using System;
using System.Linq;
using Ossastra.Characters.Rendering;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ossastra.WorldRendering.SideView
{
    public static class SideViewAceSceneAuthoring
    {
        private const string ScenePath = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string RendererPath = "Assets/OSSASTRA/SideView/Rendering/SideViewCharacterRenderer.asset";

        public static string Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("需要已保存的正式横版场，并处于编辑状态。");

            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SideViewRenderComparison>(true)).ToArray();
            var comparison = components.Single();
            var camera = comparison.GetComponent<Camera>();
            var characters = comparison.GetComponent<SideViewCharacterRendering>();
            var environment = scene.GetRootGameObjects().Single(root => root.name == "SideViewEnvironment");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (camera == null || characters == null || renderer == null || EditorUtility.IsDirty(renderer) ||
                renderer.rendererFeatures.Any(feature => feature is SideViewAceLightMapFeature))
                throw new InvalidOperationException("缺少可编辑的横版相机或独占 Renderer，或 C 已接入。");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline) ||
                !camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                throw new InvalidOperationException("正式横版相机未使用 URP。");
            var pipelineData = new SerializedObject(pipeline);
            var renderers = Property(pipelineData, "m_RendererDataList");
            int rendererIndex = Property(new SerializedObject(cameraData), "m_RendererIndex").intValue;
            if (rendererIndex < 0) rendererIndex = Property(pipelineData, "m_DefaultRendererIndex").intValue;
            if (rendererIndex < 0 || rendererIndex >= renderers.arraySize || renderers.GetArrayElementAtIndex(rendererIndex).objectReferenceValue != renderer)
                throw new InvalidOperationException("正式横版相机未绑定目标 Renderer。");
            for (int i = 0; i < renderers.arraySize; i++)
                if (i != rendererIndex && renderers.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                    throw new InvalidOperationException("目标 Renderer 在当前管线中与其他索引共享。");

            var status = Property(new SerializedObject(comparison), "statusLabel").objectReferenceValue as UnityEngine.UI.Text;
            var canvas = status == null ? null : status.GetComponentInParent<Canvas>();
            if (status == null || !(status.transform.parent is RectTransform row) || canvas == null ||
                !canvas.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out _))
                throw new InvalidOperationException("现有 A/B 面板尚未登记。");
            var buttons = row.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var a = buttons.Single(button => button.onClick.GetPersistentMethodName(0) == "SelectLegacy");
            var b = buttons.Single(button => button.onClick.GetPersistentMethodName(0) == "SelectSilksong");
            var writer = FindShader("Hidden/Ossastra/SideView/AceLightMapWriter");
            var material = CoreUtils.CreateEngineMaterial(writer);
            try
            {
                if (material.FindPass("AceLightMap") < 0 || material.FindPass("AceClearMaps") < 0)
                    throw new InvalidOperationException("C 光图写入 Shader 缺少所需 Pass。");
            }
            finally { CoreUtils.Destroy(material); }
            var spine = FindShader("Ossastra/Characters/SideViewAceSpine");
            var sprite = FindShader("Ossastra/Characters/SideViewAceSprite");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install side-view ACE lighting");
            try
            {
                var lightRoot = Create("SideViewAceLighting", environment.transform, typeof(SideViewAceLighting));
                var lighting = lightRoot.GetComponent<SideViewAceLighting>();
                var lights = new[]
                {
                    AddLight(lightRoot.transform, "Cage warm key", new Vector3(48f, 6f, -.72f), new Vector2(16, 12),
                        new Color(1.17f, 1.02f, .81f, .9f), 1.3f),
                    AddLight(lightRoot.transform, "West steps soft key", new Vector3(25.2f, 5.6f, -.72f), new Vector2(18, 14),
                        new Color(1.04f, .92f, .72f, .85f), 1.4f),
                    AddLight(lightRoot.transform, "East alcove teal key", new Vector3(72.7f, 11.05f, -1.02f), new Vector2(16, 12),
                        new Color(.68f, 1.04f, 1.15f, .92f), 1.4f),
                    AddLight(lightRoot.transform, "Dark route reading fill", new Vector3(86f, 18.3f, -.6f), new Vector2(9, 7),
                        new Color(.58f, .71f, .80f, .55f), 2.2f),
                };
                var data = new SerializedObject(lighting);
                Property(data, "gameplayCamera").objectReferenceValue = camera;
                Property(data, "writer").objectReferenceValue = writer;
                var slots = Property(data, "lights");
                slots.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                data.ApplyModifiedProperties();

                SetRect(a.GetComponent<RectTransform>(), -158, -20, 148, 32);
                SetRect(b.GetComponent<RectTransform>(), 0, -20, 148, 32);
                CreateButton(row, status.font, "SelectC", "C  ACELight", 158, -20, comparison.SelectAce);
                var sub = Create("AceCharacterOptions", row, typeof(RectTransform));
                SetRect(sub.GetComponent<RectTransform>(), 0, 0, 380, 1);
                CreateButton(sub.transform, status.font, "SelectOriginal", "原有材质", -80, -73, comparison.SelectAceOriginal);
                CreateButton(sub.transform, status.font, "SelectReplica", "苍翼复刻材质", 80, -73, comparison.SelectAceReplica);
                BuildDiagnostics(sub, status.font, lights[0], lighting);
                sub.SetActive(false);

                var binding = new SerializedObject(characters);
                Property(binding, "aceSpineShader").objectReferenceValue = spine;
                Property(binding, "aceSpriteShader").objectReferenceValue = sprite;
                binding.ApplyModifiedProperties();
                var owner = new SerializedObject(comparison);
                Property(owner, "aceLighting").objectReferenceValue = lighting;
                Property(owner, "aceCharacterControls").objectReferenceValue = sub;
                owner.ApplyModifiedProperties();

                var renderData = new SerializedObject(renderer);
                var features = Property(renderData, "m_RendererFeatures");
                var featureMap = Property(renderData, "m_RendererFeatureMap");
                if (features.arraySize != featureMap.arraySize) throw new InvalidOperationException("Renderer Feature 索引表损坏。");
                var feature = ScriptableObject.CreateInstance<SideViewAceLightMapFeature>();
                feature.name = "Side view ACE light maps";
                Undo.RegisterCreatedObjectUndo(feature, "Register ACE feature");
                AssetDatabase.AddObjectToAsset(feature, renderer);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id) || id == 0)
                    throw new InvalidOperationException("ACE Feature 未取得序列化 ID。");
                int index = features.arraySize;
                features.arraySize = featureMap.arraySize = index + 1;
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                featureMap.GetArrayElementAtIndex(index).longValue = id;
                renderData.ApplyModifiedProperties();
                Property(data, "rendererFeature").objectReferenceValue = feature;
                data.ApplyModifiedProperties();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                return "ACE lights=" + lights.Length + ", renderer features=" + (index + 1);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static SideViewAceLight AddLight(Transform parent, string name, Vector3 worldPosition, Vector2 size,
            Color ambient, float falloff)
        {
            var item = Create(name, parent, typeof(SideViewAceLight));
            item.transform.position = worldPosition;
            var light = item.GetComponent<SideViewAceLight>();
            var data = new SerializedObject(light);
            Property(data, "size").vector2Value = size;
            Property(data, "ambient").colorValue = ambient;
            Property(data, "falloff").floatValue = falloff;
            data.ApplyModifiedProperties();
            return light;
        }

        private static Button CreateButton(Transform parent, Font font, string name, string title, float x, float y, UnityAction callback = null)
        {
            var item = Create(name, parent, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            SetRect(item.GetComponent<RectTransform>(), x, y, 148, 32);
            var image = item.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.10f, .16f, .18f, .96f);
            var button = item.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            if (callback != null) UnityEventTools.AddPersistentListener(button.onClick, callback);
            var text = Create("Label", item.transform, typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.text = title;
            text.color = new Color(.94f, .97f, .96f, 1);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 15;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void BuildDiagnostics(GameObject host, Font font, SideViewAceLight lamp, SideViewAceLighting lighting)
        {
            var button = CreateButton(host.transform, font, "LightDiagnostics", "光图调试", 241, -73);
            var panel = Create("LightDiagnosticsPanel", host.transform, typeof(RectTransform), typeof(Image));
            SetRect(panel.GetComponent<RectTransform>(), 139, -150, 352, 118);
            panel.GetComponent<Image>().color = new Color(.1f, .16f, .18f, .97f);

            var toggleNode = Create("CageLightToggle", panel.transform, typeof(RectTransform), typeof(Image), typeof(Toggle));
            SetRect(toggleNode.GetComponent<RectTransform>(), -148, -23, 19, 19);
            var box = toggleNode.GetComponent<Image>();
            box.color = new Color(.27f, .38f, .37f);
            var mark = Create("Mark", toggleNode.transform, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.rectTransform.sizeDelta = new Vector2(11, 11);
            mark.color = new Color(.56f, .87f, .73f);
            mark.raycastTarget = false;
            var toggle = toggleNode.GetComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = mark;

            var label = Create("CageLightLabel", panel.transform, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            SetRect(label.rectTransform, -58, -23, 150, 20);
            label.font = font;
            label.fontSize = 14;
            label.text = "笼旁暖灯 · 强度";
            label.color = Color.white;
            label.raycastTarget = false;

            var track = Create("CageLightIntensity", panel.transform, typeof(RectTransform), typeof(Image), typeof(Slider));
            SetRect(track.GetComponent<RectTransform>(), -74, -58, 167, 18);
            var background = track.GetComponent<Image>();
            background.color = box.color;
            var handle = Create("Handle", track.transform, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.rectTransform.sizeDelta = new Vector2(10, 0);
            handle.color = mark.color;
            var slider = track.GetComponent<Slider>();
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;

            var preview = Create("SceneAmbientMap", panel.transform, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            SetRect(preview.rectTransform, 80, -57, 150, 84);
            preview.raycastTarget = false;
            Undo.AddComponent<SideViewAceDiagnostics>(host).Configure(lamp, panel, button, toggle, slider, lighting, preview);
            panel.SetActive(false);
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            Undo.RecordObject(rect, "Arrange render comparison controls");
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static GameObject Create(string name, Transform parent, params Type[] components)
        {
            var item = new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(item, "Create ACE lighting object");
            Undo.SetTransformParent(item.transform, parent, "Parent ACE lighting object");
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one;
            return item;
        }

        private static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("缺少可用 Shader " + name);
            return shader;
        }

        private static SerializedProperty Property(SerializedObject owner, string name) =>
            owner.FindProperty(name) ?? throw new InvalidOperationException(owner.targetObject.name + " 缺少字段 " + name);
    }
}
