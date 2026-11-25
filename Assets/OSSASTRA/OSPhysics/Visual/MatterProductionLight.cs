/* 功能：让物质实验相机可选地使用正式侧视世界灯图写入器。
 * 边界：只接管实验相机与本次预览灯，未启用角色、分层合成或正式 D-New 调色。 */
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    public sealed class MatterProductionLight : IDisposable
    {
        private const string LabPath = "Assets/OSSASTRA/OSPhysics/Scenes/SC_DynamicMatterLab.unity";
        private const string SideView = "Ossastra.WorldRendering.SideView.";
        private const string CameraDataName = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";
        private const string WriterName = "Hidden/Ossastra/SideView/AceLightMapWriter";
        private const int FireBudget = 4;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Camera camera;
        private readonly int previewRenderer;
        private readonly MethodInfo setRenderer;
        private readonly FieldInfo rendererIndex;
        private readonly Type lightingType;
        private readonly Type lampType;
        private readonly Scene scene;
        private readonly Lamp[] fireLamps = new Lamp[FireBudget];
        private readonly Vector4[] lastFireRegions = new Vector4[FireBudget];
        private Vector2 lastOrigin;
        private float lastCellSize = -1f;
        private float lastMood = -1f;
        private float lastPreviewLampX = float.NaN;
        private bool lastPreviewLampEnabled;
        private bool ownsCameraData;
        private bool activated;
        private bool rendererSet;
        private bool disposed;
        private int previousRenderer;
        private Component cameraData;
        private Component lightOwner;
        private GameObject scope;
        private Lamp previewLamp;
        private FieldInfo worldAmbient;
        private MethodInfo deactivate;
        private PropertyInfo frameProperty;
        private PropertyInfo drawCountProperty;
        private PropertyInfo textureCountProperty;
        private PropertyInfo textureSizeProperty;

        private MatterProductionLight(Camera camera, int previewRenderer, Type cameraDataType, Type lightingType, Type lampType)
        {
            this.camera = camera;
            this.previewRenderer = previewRenderer;
            this.lightingType = lightingType;
            this.lampType = lampType;
            scene = camera.gameObject.scene;
            rendererIndex = RequiredField(cameraDataType, "m_RendererIndex");
            setRenderer = cameraDataType.GetMethod("SetRenderer", new[] { typeof(int) })
                ?? throw new MissingMethodException(cameraDataType.Name, "SetRenderer");
        }

        public float PreviewLampX { get; set; }
        public bool PreviewLampEnabled { get; set; } = true;
        public int DrawCount => Read(drawCountProperty, 0);
        public int TextureCount => Read(textureCountProperty, 0);
        public Vector2Int TextureSize => Read(textureSizeProperty, Vector2Int.zero);
        public bool IsLive
        {
            get
            {
                int frame = Read(frameProperty, -1);
                return frame >= 0 && Time.frameCount - frame <= 1;
            }
        }

        public static bool TryCreate(Camera camera, out MatterProductionLight bridge, out string reason)
        {
            bridge = null;
            reason = string.Empty;
            if (!Application.isPlaying || camera == null || camera.cameraType != CameraType.Game ||
                camera.gameObject.scene.path != LabPath)
            {
                reason = "正式世界灯图试照只用于实验场的运行中相机。";
                return false;
            }
            if (!camera.orthographic || camera.rect != new Rect(0, 0, 1, 1) ||
                camera.allowDynamicResolution || camera.stereoEnabled)
            {
                reason = "世界灯图需要全幅、正交且无需动态分辨率的相机。";
                return false;
            }

            var cameraType = FindType(CameraDataName);
            var lightingType = FindType(SideView + "SideViewAceLighting");
            var lampType = FindType(SideView + "SideViewAceLight");
            var comparisonType = FindType(SideView + "SideViewRenderComparison");
            if (cameraType == null || lightingType == null || lampType == null)
            {
                reason = "宿主工程缺少正式相机或世界灯图写入器。";
                return false;
            }
            if (comparisonType != null && camera.GetComponent(comparisonType) != null)
            {
                reason = "该相机已有正式渲染控制器。";
                return false;
            }

            var writer = Shader.Find(WriterName);
            if (writer == null || !writer.isSupported)
            {
                reason = "正式世界灯图写入 Shader 不可用。";
                return false;
            }

            if (!TryFindRenderer(out int slot, out var feature))
            {
                reason = "当前 URP 未配置启用的 SideViewAceLightMapFeature。";
                return false;
            }

            MatterProductionLight pending = null;
            try
            {
                pending = new MatterProductionLight(camera, slot, cameraType, lightingType, lampType);
                if (!pending.Start(writer, feature, cameraType, out reason))
                {
                    pending.Dispose();
                    return false;
                }
                bridge = pending;
                return true;
            }
            catch (Exception exception)
            {
                pending?.Dispose();
                reason = "正式世界灯图试照无法启动：" + (exception.InnerException ?? exception).Message;
                return false;
            }
        }

        private bool Start(Shader writer, ScriptableObject feature, Type cameraDataType, out string reason)
        {
            // 使用宿主已登记的 Renderer 槽位，可在不同分支安全回退，不更改 Pipeline Asset。
            worldAmbient = RequiredField(lightingType, "worldAmbient");
            deactivate = lightingType.GetMethod("Deactivate", new[] { typeof(Camera) })
                ?? throw new MissingMethodException(lightingType.Name, "Deactivate");
            var activate = lightingType.GetMethod("TryActivate", new[] { typeof(Camera), typeof(string).MakeByRefType() })
                ?? throw new MissingMethodException(lightingType.Name, "TryActivate");
            frameProperty = lightingType.GetProperty("LastLightMapFrame");
            drawCountProperty = lightingType.GetProperty("LastDrawCount");
            textureCountProperty = lightingType.GetProperty("LastTextureCount");
            textureSizeProperty = lightingType.GetProperty("LastTextureSize");
            cameraData = camera.GetComponent(cameraDataType);
            if (cameraData != null) previousRenderer = (int)rendererIndex.GetValue(cameraData);

            // 独立 Play 临时对象随租约销毁。禁用 HideFlags 位，确保正式写入器能按 Scene 归属验灯。
            scope = new GameObject("OSPhysics 正式灯图试照");
            SceneManager.MoveGameObjectToScene(scope, scene);
            lightOwner = scope.AddComponent(lightingType);
            previewLamp = CreateLamp("场内试照灯", new Vector2(15f, 10f), new Color(1.05f, .90f, .66f, .90f));
            var lamps = Array.CreateInstance(lampType, FireBudget + 1);
            lamps.SetValue(previewLamp.Component, 0);
            for (int i = 0; i < FireBudget; i++)
            {
                fireLamps[i] = CreateLamp("聚合燃烧光 " + (i + 1), Vector2.one, new Color(1.18f, .60f, .25f, .95f));
                lamps.SetValue(fireLamps[i].Component, i + 1);
            }
            var cameraBinding = RequiredField(lightingType, "gameplayCamera");
            cameraBinding.SetValue(lightOwner, camera);
            RequiredField(lightingType, "writer").SetValue(lightOwner, writer);
            RequiredField(lightingType, "rendererFeature").SetValue(lightOwner, feature);
            RequiredField(lightingType, "resolutionScale").SetValue(lightOwner, .375f);
            RequiredField(lightingType, "lights").SetValue(lightOwner, lamps);
            worldAmbient.SetValue(lightOwner, new Color(.66f, .70f, .76f, 1f));

            if (scope.scene != camera.gameObject.scene || !(lightOwner is Behaviour owner) || !owner.isActiveAndEnabled ||
                (Camera)cameraBinding.GetValue(lightOwner) != camera)
            {
                reason = $"世界灯图的相机租约未就绪（灯组 Scene {scope.scene.name}、相机 Scene {camera.gameObject.scene.name}、" +
                    $"启用 {scope.activeInHierarchy}）。";
                return false;
            }
            foreach (Component lamp in lamps)
            {
                if (lamp.gameObject.scene == scene && lamp is Behaviour light && light.isActiveAndEnabled) continue;
                reason = $"试照灯「{lamp.name}」不在活动的实验场 Scene（当前 {lamp.gameObject.scene.name}）。";
                return false;
            }

            var arguments = new object[] { camera, null };
            activated = (bool)activate.Invoke(lightOwner, arguments);
            reason = arguments[1] as string ?? string.Empty;
            if (!activated) return false;

            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent(cameraDataType);
                ownsCameraData = true;
                previousRenderer = (int)rendererIndex.GetValue(cameraData);
            }
            setRenderer.Invoke(cameraData, new object[] { previewRenderer });
            rendererSet = true;
            return true;
        }

        private Lamp CreateLamp(string name, Vector2 size, Color ambient)
        {
            var child = new GameObject(name);
            child.transform.SetParent(scope.transform, false);
            var component = child.AddComponent(lampType);
            var groups = RequiredField(lampType, "groups");
            groups.SetValue(component, Enum.Parse(groups.FieldType, "World"));
            var sizeField = RequiredField(lampType, "size");
            sizeField.SetValue(component, size);
            RequiredField(lampType, "ambient").SetValue(component, ambient);
            var intensity = lampType.GetProperty("Intensity") ?? throw new MissingMemberException(lampType.Name, "Intensity");
            intensity.SetValue(component, 0f);
            return new Lamp(component, sizeField, intensity);
        }

        public void Refresh(Vector4[] fireRegions, Vector2 origin, float cellSize, float mood)
        {
            if (disposed || !lightOwner || !camera || !(cellSize > 0) || float.IsInfinity(cellSize)) return;
            mood = Mathf.Clamp(mood, 0f, 2f);
            bool gridChanged = cellSize != lastCellSize || origin != lastOrigin;
            bool moodChanged = mood != lastMood;
            if (moodChanged)
            {
                var baseline = new Color(.66f, .70f, .76f, 1f);
                var ambient = mood < 1f
                    ? Color.Lerp(new Color(.23f, .29f, .39f, 1f), baseline, mood)
                    : Color.Lerp(baseline, new Color(.88f, .92f, .98f, 1f), mood - 1f);
                worldAmbient.SetValue(lightOwner, ambient);
                lastMood = mood;
            }
            if (moodChanged || PreviewLampEnabled != lastPreviewLampEnabled)
            {
                previewLamp.SetIntensity(PreviewLampEnabled ? Mathf.Lerp(.65f, 2f, mood * .5f) : 0);
                lastPreviewLampEnabled = PreviewLampEnabled;
            }
            if (PreviewLampX != lastPreviewLampX || gridChanged)
            {
                previewLamp.Transform.position = new Vector3(PreviewLampX, origin.y + 35 * cellSize, 0f);
                lastPreviewLampX = PreviewLampX;
            }
            for (int i = 0; i < FireBudget; i++)
            {
                var region = fireRegions != null && i < fireRegions.Length ? fireRegions[i] : Vector4.zero;
                if (gridChanged || lastFireRegions[i] != region)
                {
                    // 聚合区域以物理格为单位。灯随真实燃烧更新，全部熄灭即归零。
                    if (region.w > 0f)
                    {
                        fireLamps[i].Transform.position = new Vector3(origin.x + region.x * cellSize,
                            origin.y + region.y * cellSize, 0f);
                        float diameter = Mathf.Max(1f, region.z * cellSize * 2f);
                        fireLamps[i].SetSize(new Vector2(diameter, diameter));
                    }
                    fireLamps[i].SetIntensity(Mathf.Clamp(region.w * 2.8f, 0f, 4f));
                    lastFireRegions[i] = region;
                }
            }
            lastOrigin = origin;
            lastCellSize = cellSize;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (activated && lightOwner)
                    deactivate.Invoke(lightOwner, new object[] { camera });
            }
            finally
            {
                try
                {
                    // 外部切走后保留对方选择；预览从不改其他相机的数据。
                    if (rendererSet && cameraData && (int)rendererIndex.GetValue(cameraData) == previewRenderer)
                        setRenderer.Invoke(cameraData, new object[] { previousRenderer });
                }
                finally
                {
                    if (scope != null) DestroyOwned(scope);
                    if (ownsCameraData && cameraData != null) DestroyOwned(cameraData);
                }
            }
        }

        private T Read<T>(PropertyInfo property, T fallback) =>
            !disposed && lightOwner && property?.GetValue(lightOwner) is T value ? value : fallback;

        private static bool TryFindRenderer(out int slot, out ScriptableObject feature)
        {
            slot = -1;
            feature = null;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return false;
            var entries = FindField(pipeline.GetType(), "m_RendererDataList")?.GetValue(pipeline) as Array;
            if (entries == null) return false;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!(entries.GetValue(i) is ScriptableObject renderer) || renderer == null) continue;
                var features = FindField(renderer.GetType(), "m_RendererFeatures")?.GetValue(renderer) as IEnumerable;
                if (features == null) continue;
                foreach (var candidate in features)
                {
                    if (!(candidate is ScriptableObject value) || value == null ||
                        value.GetType().FullName != SideView + "SideViewAceLightMapFeature") continue;
                    if (!(value.GetType().GetProperty("isActive")?.GetValue(value) is bool active) || !active) continue;
                    slot = i;
                    feature = value;
                    return true;
                }
            }
            return false;
        }

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }

        private static FieldInfo RequiredField(Type type, string name) =>
            FindField(type, name) ?? throw new MissingFieldException(type.Name, name);

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, InstanceMembers | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static void DestroyOwned(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private sealed class Lamp
        {
            private readonly FieldInfo size;
            private readonly PropertyInfo intensity;

            public Lamp(Component component, FieldInfo size, PropertyInfo intensity)
            {
                Component = component;
                this.size = size;
                this.intensity = intensity;
            }

            public Component Component { get; }
            public Transform Transform => Component.transform;
            public void SetSize(Vector2 value) => size.SetValue(Component, value);
            public void SetIntensity(float value) => intensity.SetValue(Component, value);
        }
    }
}
