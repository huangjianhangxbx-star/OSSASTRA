/* 功能：登记明确相机的 C 配方及作者灯源，管理相机租约。
 * 边界：不切换渲染方案、不移动灯和角色、不持有 GPU 资源。 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    [DisallowMultipleComponent]
    public sealed class SideViewAceLighting : MonoBehaviour
    {
        private static readonly Dictionary<Camera, SideViewAceLighting> Owners = new Dictionary<Camera, SideViewAceLighting>();
        internal static event Action<Camera> Released;

        [SerializeField] private Camera gameplayCamera = null;
        [SerializeField] private Shader writer = null;
        [SerializeField] private SideViewAceLightMapFeature rendererFeature = null;
        [SerializeField] private SideViewAceLight[] lights = Array.Empty<SideViewAceLight>();
        [SerializeField, Range(.25f, 1f)] private float resolutionScale = .5f;
        [SerializeField, ColorUsage(false, true)] private Color worldAmbient = new Color(.4f, .52941179f, .50159097f, 1f);
        [SerializeField, ColorUsage(false, true)] private Color characterAmbient = new Color(.43075827f, .51886791f, .45895335f, 1f);
        private Camera activeCamera;

        public int LastLightMapFrame { get; private set; } = -1;
        public int LastDrawCount { get; private set; }
        public int LastEdgeDrawCount { get; private set; }
        public int LastTextureCount { get; private set; }
        public Vector2Int LastTextureSize { get; private set; }
        public Texture AmbientPreview { get; private set; }
        public Texture EdgePreview { get; private set; }
        public Texture EdgeDirectionPreview { get; private set; }
        public bool EdgeMapsReady => EdgePreview != null && EdgeDirectionPreview != null;
        public bool CanUseEdgeMaps
        {
            get
            {
                if (writer == null || !writer.isSupported || !SideViewAceLightMapFeature.SupportsLightMaps) return false;
                var lightMode = new ShaderTagId("LightMode");
                var edgePass = new ShaderTagId("SideViewAceEdgeMaps");
                for (int index = 0; index < writer.passCount; index++)
                    if (writer.FindPassTagValue(index, lightMode) == edgePass) return true;
                return false;
            }
        }
        internal Shader Writer => writer;
        internal float ResolutionScale => resolutionScale;
        internal IReadOnlyList<SideViewAceLight> Lights => lights;
        // ACE maps carry display-encoded color as data in a non-sRGB float RT.
        internal Color Ambient(bool character) => character ? characterAmbient : worldAmbient;

        public bool CanActivate(Camera camera, out string reason)
        {
            if (camera == null || camera != gameplayCamera || camera.cameraType != CameraType.Game ||
                !isActiveAndEnabled || gameObject.scene != camera.gameObject.scene)
            {
                reason = "C 的光图需要同场景的指定游戏相机。";
                return false;
            }
            if (camera.rect != new Rect(0f, 0f, 1f, 1f) || camera.allowDynamicResolution || camera.stereoEnabled)
            {
                reason = "当前 C 光图仅接受完整 viewport 的非 XR 游戏相机，分辨率由相机实际目标决定。";
                return false;
            }
            if (writer == null || !writer.isSupported || rendererFeature == null || !rendererFeature.isActive ||
                !SideViewAceLightMapFeature.SupportsLightMaps)
            {
                reason = "C 的光图写入 Shader 或四路浮点混合目标不可用。";
                return false;
            }
            if (!SideViewAceLight.Valid(resolutionScale, .25f, 1f) || !SideViewAceLight.ValidColor(worldAmbient) ||
                !SideViewAceLight.ValidColor(characterAmbient) || lights == null || lights.Length == 0)
            {
                reason = "C 的分辨率、环境底色或作者灯表不完整。";
                return false;
            }
            var registered = new HashSet<SideViewAceLight>();
            foreach (var light in lights)
            {
                if (light == null || light.gameObject.scene != camera.gameObject.scene ||
                    !registered.Add(light) || !light.HasValidSettings)
                {
                    reason = "C 的作者灯表包含缺失、重复或无效项。";
                    return false;
                }
            }
            if (Owners.TryGetValue(camera, out var owner) && owner != this)
            {
                reason = "该相机已经绑定另一套 C 光图。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool TryActivate(Camera camera, out string reason)
        {
            if (!CanActivate(camera, out reason)) return false;
            if (activeCamera != null && activeCamera != camera) Deactivate(activeCamera);
            Owners[camera] = this;
            activeCamera = camera;
            return true;
        }

        public void Deactivate(Camera camera)
        {
            if (ReferenceEquals(camera, null) || !Owners.TryGetValue(camera, out var owner) || owner != this) return;
            Owners.Remove(camera);
            activeCamera = null;
            ClearFrame();
            Released?.Invoke(camera);
        }

        internal static bool TryGet(Camera camera, out SideViewAceLighting owner)
        {
            owner = null;
            return camera != null && Owners.TryGetValue(camera, out owner) && owner != null && owner.isActiveAndEnabled;
        }

        internal void RecordFrame(int draws, int edgeDraws, int width, int height, int textures, Texture ambientPreview,
            Texture edgePreview, Texture edgeDirectionPreview)
        {
            LastLightMapFrame = Time.frameCount;
            LastDrawCount = draws;
            LastEdgeDrawCount = edgeDraws;
            LastTextureCount = textures;
            LastTextureSize = new Vector2Int(width, height);
            AmbientPreview = ambientPreview;
            EdgePreview = edgePreview;
            EdgeDirectionPreview = edgeDirectionPreview;
        }

        internal void ClearFrame()
        {
            LastLightMapFrame = -1;
            LastDrawCount = LastEdgeDrawCount = LastTextureCount = 0;
            LastTextureSize = default;
            AmbientPreview = null;
            EdgePreview = EdgeDirectionPreview = null;
        }

        private void OnDisable() => Deactivate(activeCamera);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwners() => Owners.Clear();
    }
}
