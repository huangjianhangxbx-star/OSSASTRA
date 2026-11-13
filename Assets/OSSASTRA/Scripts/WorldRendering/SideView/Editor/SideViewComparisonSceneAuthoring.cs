/*
 * 显式编辑期接入。此文件应放入 Editor 目录，不自动打开正式场景或保存。
 * 旧场仅在预览 Scene 中读取，所有修改限定于显式传入的正式横版相机或 Renderer 19。
 */
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Ossastra.CharacterLighting;
using Ossastra.Characters.Rendering;
using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using Ossastra.SideView.Rendering;
using Ossastra.WorldRendering.Rendering;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ossastra.WorldRendering.SideView
{
    public static class SideViewComparisonSceneAuthoring
    {
        private const string TargetScene = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        private const string TargetRenderer = "Assets/OSSASTRA/SideView/Rendering/SideViewCharacterRenderer.asset";
        private const string PipelinePath = "Assets/XingHaiHuiLang/Rendering/XH_URP3D.asset";
        private const int RendererIndex = 19;
        private const string SourceScene = "Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity";
        private const string SourceRenderer = "Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset";
        private const string SourceBlueProfile = "Assets/OSSASTRA/CharacterLighting/Profiles/BlueEnvironmentFusionB.asset";
        private const string SourceFreyciaProfile = "Assets/OSSASTRA/CharacterLighting/Profiles/FreyciaEnvironmentFusionB.asset";
        private const string TargetBlueProfile = "Assets/OSSASTRA/SideView/Rendering/BlueCaveLighting.asset";
        private const string TargetFreyciaProfile = "Assets/OSSASTRA/SideView/Rendering/FreyciaCaveLighting.asset";
        private const string LightingHost = "SideViewComparisonLegacyLighting";
        private const string ControlsName = "SideViewComparisonControls";
        private const string LocalComparison = "Assets/OSSASTRA/SideView/LocalReplica/Comparison";

        public static SideViewRenderComparison InstallPreparedOnOpenScene(Camera camera, Rect[] interiorRegions)
        {
            RequireIdleEditor();
            if (camera == null || camera.gameObject.scene.path != TargetScene || camera.gameObject.scene.isDirty)
                throw new InvalidOperationException("请先打开已保存的正式横版场。");

            var card = FindShader("Ossastra/SideView/ComparisonCard");
            var layerShader = FindShader("Hidden/Ossastra/SideView/ComparisonSilksongLayers");
            var gradeShader = FindShader(SideViewComparisonSilksongGradeAssets.ShaderName);
            var darkness = FindShader("Hidden/Ossastra/SideView/ComparisonDarkness");
            var catalog = LoadLocal<TextAsset>("SideViewComparisonCardCatalogManifest.json");
            var hero = LoadLocalMask("SilksongHeroLight395x374.png", true);
            var vignette = LoadLocalMask("SilksongVignette.png", false);
            var cutout = LoadLocalMask("SilksongCutout.png", false);
            var fader = LoadLocalMask("SilksongWhiteFader.png", false);
            var lutPath = LocalComparison + "/MossCaveRGB.asset";
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(lutPath);
            if (lut == null)
            {
                lut = SideViewComparisonSilksongGradeAssets.CreateMossCaveRgbLut(makeNoLongerReadable: false);
                lut.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(lut, lutPath);
            }
            var layers = GetOrCreateLocalMaterial("SilksongLayers.mat", layerShader);
            var grade = GetOrCreateLocalMaterial("SilksongGrade.mat", gradeShader);
            grade.SetTexture("_SvRgbLut", lut);
            EditorUtility.SetDirty(grade);
            return ApplyToOpenScene(camera, catalog, card, layers, grade, hero, interiorRegions,
                vignette: vignette, darknessShader: darkness, ultrawideCutout: cutout, whiteFader: fader);
        }

        private static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Shader 尚未导入或不支持。 " + name);
            return shader;
        }

        private static T LoadLocal<T>(string file) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(LocalComparison + "/" + file);
            if (asset == null) throw new InvalidOperationException("本机对照资产尚未导入。 " + file);
            return asset;
        }

        private static Texture2D LoadLocalMask(string file, bool color)
        {
            var importer = AssetImporter.GetAtPath(LocalComparison + "/" + file) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少本机光图导入配置。 " + file);
            if (importer.npotScale != TextureImporterNPOTScale.None || importer.mipmapEnabled || importer.sRGBTexture != color ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = color;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return LoadLocal<Texture2D>(file);
        }

        private static Material GetOrCreateLocalMaterial(string file, Shader shader)
        {
            string path = LocalComparison + "/" + file;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) throw new InvalidOperationException("本机材质使用了不同 Shader。 " + file);
            return material;
        }

        public static SideViewRenderComparison ApplyToOpenScene(Camera camera, TextAsset cardManifest,
            Shader comparisonCard, Material silksongLayers, Material silksongGrade, Texture2D heroLight,
            Rect[] legacyInteriorRegions, Canvas canvas = null, Font font = null, Texture2D vignette = null,
            Shader darknessShader = null, Texture2D ultrawideCutout = null, Texture2D whiteFader = null,
            Transform darknessSourceRoot = null)
        {
            RequireIdleEditor();
            if (camera == null || camera.gameObject.scene.path != TargetScene || camera.gameObject.scene.isDirty)
                throw new InvalidOperationException("请在已打开且已保存的正式横版场景指定玩法相机。");

            Scene target = camera.gameObject.scene;
            var additional = camera.GetComponent<UniversalAdditionalCameraData>();
            var characters = camera.GetComponent<SideViewCharacterRendering>();
            if (additional == null || Property(new SerializedObject(additional), "m_RendererIndex").intValue != RendererIndex ||
                additional.renderPostProcessing || characters == null || characters.replicaArt.Length != 5 ||
                camera.GetComponent<SideViewRenderComparison>() != null || camera.GetComponent<SideViewComparisonCardCatalog>() != null)
                throw new InvalidOperationException("横版相机或画片根状态与接入前提不符。");

            ReadTargetRenderer();
            if (cardManifest == null || comparisonCard == null || silksongLayers == null || silksongGrade == null ||
                heroLight == null || vignette == null || darknessShader == null || ultrawideCutout == null ||
                whiteFader == null || legacyInteriorRegions == null)
                throw new ArgumentException("需要显式提交卡片清单、A/B Shader 与材质、角色光图、暗区纹理和旧环境区域。");
            if (canvas != null && (canvas.gameObject.scene != target || !canvas.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out _)))
                throw new ArgumentException("指定 Canvas 必须在横版场景并具有 GraphicRaycaster。", nameof(canvas));

            var eventSystem = Single<UnityEngine.EventSystems.EventSystem>(target, _ => true, "横版 EventSystem");
            if (!eventSystem.TryGetComponent<UnityEngine.EventSystems.BaseInputModule>(out _))
                throw new InvalidOperationException("横版 EventSystem 缺少已有输入模块。");
            var environment = Single<EnvironmentRoot>(target, item => item.gameplayCamera == camera, "横版环境根");
            var sky = Single<EnvironmentSkyBinding>(target, item => item.environment == environment, "横版天空绑定");
            var blue = Receiver(target, camera, TargetBlueProfile);
            var freycia = Receiver(target, camera, TargetFreyciaProfile);
            if (environment.lightingBinding != null || environment.transform.Find(LightingHost) != null)
                throw new InvalidOperationException("横版环境已有灯光绑定或已有候选对象。");

            // 位置来自现有横版场景，A 的颜色、强度、范围由旧场对应角色在运行时借用。
            Light[] localLights =
            {
                LocalLamp(sky, "West steps bounced light"),
                LocalLamp(sky, "Cage bounced light"),
                LocalLamp(sky, "East alcove bounced light")
            };
            var uiFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null) throw new InvalidOperationException("对照按钮缺少可序列化字体。");

            Scene preview = OpenLegacyPreview();
            try
            {
                var sourceCamera = Single<Camera>(preview, item => item.name == "GameplayCamera", "旧玩法相机");
                var sourceEnvironment = Single<EnvironmentRoot>(preview, item => item.gameplayCamera == sourceCamera, "旧环境根");
                var sourceSky = Single<EnvironmentSkyBinding>(preview, item => item.environment == sourceEnvironment, "旧天空绑定");
                var sourceLook = Single<WorldLookController>(preview, item => item.gameplayCamera == sourceCamera, "旧 WorldLook");
                var sourceBlue = Receiver(preview, sourceCamera, SourceBlueProfile);
                var sourceFreycia = Receiver(preview, sourceCamera, SourceFreyciaProfile);
                ValidateSource(sourceEnvironment, sourceSky, sourceLook);
                if (target.isDirty)
                    throw new InvalidOperationException("读取旧场期间横版场景有未保存修改。");

                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Install Side View Rendering Comparison");
                try
                {
                    var catalog = Undo.AddComponent<SideViewComparisonCardCatalog>(camera.gameObject);
                    if (SideViewComparisonCardCatalogAuthoring.Apply(catalog, cardManifest, characters.replicaArt) != 826)
                        throw new InvalidOperationException("正式场景卡片登记数变化。");
                    var binding = CreateLegacyBinding(environment, sourceEnvironment.lightingBinding);
                    var comparison = Undo.AddComponent<SideViewRenderComparison>(camera.gameObject);
                    var serialized = new SerializedObject(comparison);
                    Set(serialized, "cards", catalog);
                    Set(serialized, "comparisonCard", comparisonCard);
                    Set(serialized, "environment", environment);
                    Set(serialized, "sky", sky);
                    Set(serialized, "blue", blue);
                    Set(serialized, "freycia", freycia);
                    Set(serialized, "silksongLayers", silksongLayers);
                    Set(serialized, "silksongGrade", silksongGrade);
                    Set(serialized, "heroLight", heroLight);
                    Set(serialized, "vignette", vignette);
                    Set(serialized, "darknessShader", darknessShader);
                    Set(serialized, "ultrawideCutout", ultrawideCutout);
                    Set(serialized, "whiteFader", whiteFader);
                    Set(serialized, "darknessSourceRoot", darknessSourceRoot);
                    Property(serialized, "heroLightSize").vector2Value = new Vector2(395f / 64f * 3f, 374f / 64f * 3f);
                    Property(serialized, "heroLightOffset").vector2Value = new Vector2(0f, -.60f);
                    var regions = Property(serialized, "legacyInteriorRegions");
                    regions.arraySize = legacyInteriorRegions.Length;
                    for (int i = 0; i < legacyInteriorRegions.Length; i++)
                        regions.GetArrayElementAtIndex(i).rectValue = legacyInteriorRegions[i];

                    var oldEnvironment = Property(serialized, "legacyEnvironment");
                    Set(oldEnvironment, "dayCycle", sourceEnvironment.dayCycle);
                    Set(oldEnvironment, "nativeLighting", binding);
                    Set(oldEnvironment, "blueProfile", sourceBlue.profile);
                    Set(oldEnvironment, "freyciaProfile", sourceFreycia.profile);
                    Set(oldEnvironment, "skyShader", sourceSky.skyShader);
                    Set(oldEnvironment, "reflectionSkyShader", sourceSky.reflectionSkyShader);
                    Set(oldEnvironment, "rainShader", sourceSky.rainShader);
                    Set(oldEnvironment, "rainDepthCopyShader", sourceSky.rainDepthCopyShader);
                    var lamps = Relative(oldEnvironment, "localLights");
                    lamps.arraySize = localLights.Length;
                    for (int i = 0; i < localLights.Length; i++)
                        lamps.GetArrayElementAtIndex(i).objectReferenceValue = localLights[i];

                    var oldLook = Property(serialized, "legacyWorldLook");
                    Set(oldLook, "outdoorPresentation", sourceLook.outdoorPresentation);
                    Set(oldLook, "outdoorAtmosphere", sourceLook.outdoorAtmosphere);
                    Set(oldLook, "outdoorPalette", sourceLook.outdoorPalette);
                    Set(oldLook, "indoorPresentation", sourceLook.interiorPresentation);
                    var status = BuildControls(target, canvas, uiFont, font != null, comparison);
                    Set(serialized, "statusLabel", status);
                    serialized.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(target);
                    Undo.CollapseUndoOperations(undo);
                    return comparison;
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undo);
                    throw;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static UniversalRendererData RegisterRenderer19Features()
        {
            RequireIdleEditor();
            var renderer = ReadTargetRenderer();
            if (EditorUtility.IsDirty(renderer) || !AssetDatabase.IsOpenForEdit(renderer))
                throw new InvalidOperationException("Renderer 19 有未保存修改或当前不可编辑。");

            var serialized = new SerializedObject(renderer);
            var features = Property(serialized, "m_RendererFeatures");
            var featureMap = Property(serialized, "m_RendererFeatureMap");
            if (features.arraySize != 1 || featureMap.arraySize != 1 ||
                features.GetArrayElementAtIndex(0).objectReferenceValue is not SideViewCharacterRendererFeature)
                throw new InvalidOperationException("Renderer 19 的原 Feature 序列与已审计磁盘值不一致。");

            var oldRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SourceRenderer);
            var source = oldRenderer == null ? null : oldRenderer.rendererFeatures.OfType<WorldLookRendererFeature>().SingleOrDefault();
            if (source == null || !source.isActive || source.environmentLookShader == null || source.legalDepthCopyShader == null)
                throw new InvalidOperationException("旧 Renderer 15 的实际 WorldLook Shader 引用缺失。");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Side View Comparison Renderer Features");
            try
            {
                var look = ScriptableObject.CreateInstance<WorldLookRendererFeature>();
                var lookData = new SerializedObject(look);
                Set(lookData, "environmentLookShader", source.environmentLookShader);
                Set(lookData, "legalDepthCopyShader", source.legalDepthCopyShader);
                lookData.ApplyModifiedPropertiesWithoutUndo();

                // 事件时点由 Feature 自己负责；同一时点按 Renderer 中的登记顺序。
                AppendFeature(renderer, features, featureMap, ScriptableObject.CreateInstance<SideViewComparisonMetadataFeature>());
                AppendFeature(renderer, features, featureMap, ScriptableObject.CreateInstance<SideViewComparisonLegacyWeatherFeature>());
                AppendFeature(renderer, features, featureMap, look);
                AppendFeature(renderer, features, featureMap, ScriptableObject.CreateInstance<SideViewComparisonSilksongFeature>());
                serialized.ApplyModifiedProperties();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
                Undo.CollapseUndoOperations(undo);
                return renderer;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        private static void AppendFeature(UniversalRendererData renderer, SerializedProperty features,
            SerializedProperty featureMap, ScriptableRendererFeature feature)
        {
            feature.name = feature.GetType().Name;
            Undo.RegisterCreatedObjectUndo(feature, "Add Side View Renderer Feature");
            AssetDatabase.AddObjectToAsset(feature, renderer);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId) || localId == 0)
                throw new InvalidOperationException("Renderer Feature 未得到可序列化的本地 ID。");
            int index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            featureMap.arraySize = index + 1;
            featureMap.GetArrayElementAtIndex(index).longValue = localId;
        }

        private static UniversalRendererData ReadTargetRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(TargetRenderer);
            if (pipeline == null || renderer == null)
                throw new InvalidOperationException("缺少已审计的横版 URP 和 RendererData。");
            var entries = Property(new SerializedObject(pipeline), "m_RendererDataList");
            if (entries.arraySize <= RendererIndex || entries.GetArrayElementAtIndex(RendererIndex).objectReferenceValue != renderer)
                throw new InvalidOperationException("RendererData 当前未登记在横版专用索引 19。");
            for (int i = 0; i < entries.arraySize; i++)
                if (i != RendererIndex && entries.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                    throw new InvalidOperationException("Renderer 19 资产与其他索引共享。");
            return renderer;
        }

        private static void ValidateSource(EnvironmentRoot environment, EnvironmentSkyBinding sky, WorldLookController look)
        {
            var lighting = environment.lightingBinding;
            if (environment.dayCycle == null || lighting == null || lighting.sun == null || lighting.fill == null ||
                lighting.sun.type != LightType.Directional || lighting.fill.type != LightType.Directional ||
                sky.skyShader == null || sky.reflectionSkyShader == null || sky.rainShader == null || sky.rainDepthCopyShader == null ||
                look.outdoorPresentation == null || look.outdoorAtmosphere == null || look.outdoorPalette == null ||
                look.interiorPresentation == null || look.stylizedLook != null)
                throw new InvalidOperationException("旧场实际 A 配方未能完整读取。");
        }

        private static EnvironmentLightingBinding CreateLegacyBinding(EnvironmentRoot environment, EnvironmentLightingBinding source)
        {
            var host = CreateObject(LightingHost, environment.gameObject.scene, environment.transform);
            var binding = Undo.AddComponent<EnvironmentLightingBinding>(host);
            var data = new SerializedObject(binding);
            Set(data, "root", environment);
            Set(data, "sun", CopyDirectional(source.sun, host.transform));
            Set(data, "fill", CopyDirectional(source.fill, host.transform));
            data.ApplyModifiedProperties();
            return binding;
        }

        private static Light CopyDirectional(Light source, Transform host)
        {
            var target = Undo.AddComponent<Light>(CreateObject(source.name, host.gameObject.scene, host));
            var sourceData = new SerializedObject(source);
            var targetData = new SerializedObject(target);
            foreach (string field in new[] { "m_Type", "m_Color", "m_Intensity", "m_BounceIntensity",
                "m_RenderMode", "m_CullingMask", "m_RenderingLayerMask", "m_Shadows.m_Type", "m_Shadows.m_Resolution",
                "m_Shadows.m_CustomResolution", "m_Shadows.m_Strength", "m_Shadows.m_Bias", "m_Shadows.m_NormalBias",
                "m_Shadows.m_NearPlane" })
            {
                var property = sourceData.FindProperty(field);
                if (property != null) targetData.CopyFromSerializedProperty(property);
            }
            Property(targetData, "m_Enabled").boolValue = false;
            targetData.ApplyModifiedProperties();
            return target;
        }

        private static UnityEngine.UI.Text BuildControls(Scene scene, Canvas canvas, Font font, bool localized,
            SideViewRenderComparison comparison)
        {
            if (canvas == null)
            {
                var panel = CreateObject(ControlsName, scene, null, typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = panel.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 105;
            }
            var row = CreateObject("RenderComparison", scene, canvas.transform, typeof(RectTransform)).GetComponent<RectTransform>();
            SetRect(row, new Vector2(.5f, 1f), new Vector2(0f, -32f), new Vector2(340f, 58f));
            CreateButton(row, font, localized ? "A 旧主场" : "A  Legacy", -79f, comparison.SelectLegacy);
            CreateButton(row, font, localized ? "B 丝之歌" : "B  Silksong", 79f, comparison.SelectSilksong);
            var status = CreateText(row, font, "ComparisonStatus", string.Empty);
            SetRect(status.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -48f), new Vector2(340f, 20f));
            return status;
        }

        private static void CreateButton(RectTransform parent, Font font, string label, float x, UnityAction action)
        {
            var node = CreateObject("Select" + label[0], parent.gameObject.scene, parent,
                typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            SetRect(node.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(x, -20f), new Vector2(148f, 32f));
            var image = node.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.10f, .16f, .18f, .96f);
            var button = node.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var text = CreateText(node.transform, font, "Label", label);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static UnityEngine.UI.Text CreateText(Transform parent, Font font, string name, string content)
        {
            var node = CreateObject(name, parent.gameObject.scene, parent, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            var text = node.GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = 15;
            text.color = new Color(.94f, .97f, .96f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static GameObject CreateObject(string name, Scene scene, Transform parent, params Type[] components)
        {
            var result = new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(result, "Create Side View Comparison Object");
            if (result.scene != scene) SceneManager.MoveGameObjectToScene(result, scene);
            if (parent != null) Undo.SetTransformParent(result.transform, parent, "Parent Side View Comparison Object");
            result.transform.localPosition = Vector3.zero;
            result.transform.localRotation = Quaternion.identity;
            result.transform.localScale = Vector3.one;
            return result;
        }

        private static Light LocalLamp(EnvironmentSkyBinding sky, string name)
        {
            var lamp = sky.localLights.SingleOrDefault(item => item != null && item.name == name && item.type == LightType.Point);
            return lamp != null ? lamp : throw new InvalidOperationException("缺少已登记的横版点灯锚点 " + name);
        }

        private static CharacterLightingReceiver Receiver(Scene scene, Camera camera, string profilePath) =>
            Single<CharacterLightingReceiver>(scene, item => item.gameplayCamera == camera && item.profile != null &&
                AssetDatabase.GetAssetPath(item.profile) == profilePath, profilePath);

        private static T Single<T>(Scene scene, Func<T, bool> predicate, string description) where T : Component
        {
            var result = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Where(predicate).SingleOrDefault();
            return result != null ? result : throw new InvalidOperationException("缺少 " + description);
        }

        private static void RequireIdleEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请在编辑器稳定的编辑状态调用接入入口。");
        }

        private static Scene OpenLegacyPreview()
        {
            // Tuanjie 2022.3.62t16 在 EditorSceneManager 内提供此入口。
            // 缺少入口时停在读取阶段，保留已打开的普通场景。
            var open = typeof(EditorSceneManager).GetMethod("OpenPreviewScene",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: new[] { typeof(string) }, modifiers: null);
            if (open?.Invoke(null, new object[] { SourceScene }) is not Scene preview ||
                !preview.IsValid() || !EditorSceneManager.IsPreviewScene(preview))
                throw new InvalidOperationException("旧场只读预览无法打开。");
            return preview;
        }

        private static SerializedProperty Property(SerializedObject owner, string path) =>
            owner.FindProperty(path) ?? throw new InvalidOperationException("序列化字段不存在 " + path);

        private static SerializedProperty Relative(SerializedProperty owner, string path) =>
            owner.FindPropertyRelative(path) ?? throw new InvalidOperationException("序列化字段不存在 " + owner.propertyPath + "." + path);

        private static void Set(SerializedObject owner, string path, UnityEngine.Object value) =>
            Property(owner, path).objectReferenceValue = value;

        private static void Set(SerializedProperty owner, string path, UnityEngine.Object value) =>
            Relative(owner, path).objectReferenceValue = value;
    }
}
#endif
