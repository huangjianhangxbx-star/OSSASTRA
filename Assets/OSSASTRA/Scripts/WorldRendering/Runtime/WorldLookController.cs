/* 功能：在原房间可视帧发布后，为明确 GameplayCamera 选择室内/外环境 Look。
 * 边界：不拥有 Current、房间归属、视野许可、灯光、材质、输入、业务或管线；不查全场，不另订阅相机绘制回调。
 * 协作：只读 SceneVisibilityRoot/RoomVisibilityModule 的已发布帧，旧 Profile 只作为数据源。
 * 生命周期：重绑、帧失效、禁用或销毁时撤销自己的相机登记，Feature 按对应释放事件归还 GPU 资源。
 */
using Ossastra.SceneVisibility;
using Ossastra.SceneVisibility.Rooms;
using Ossastra.SceneVisibility.Rooms.Rendering;
using Ossastra.EnvironmentSimulation;
using UnityEngine;
using XingHai.T7B;
using XingHai.T7C1;
using XingHai.T7C2;

namespace Ossastra.WorldRendering
{
    [DisallowMultipleComponent]
    public sealed class WorldLookController : MonoBehaviour
    {
        [Header("明确主场上下文；不自动寻找或接管相机")]
        public Camera gameplayCamera;
        public SceneVisibilityRoot visibilityRoot;
        public RoomVisibilityModule rooms;
        [Header("室外：旧档只读；材质角色色板需另外显式登记")]
        public HD2DPostProfile outdoorPresentation;
        public OutdoorAtmosphereProfile outdoorAtmosphere;
        public RegionalLookProfile outdoorPalette;
        [Header("室内：空空气档保留原照明，只使用明确展示档")]
        public HD2DPostProfile interiorPresentation;
        public OutdoorAtmosphereProfile interiorAtmosphere;
        public RegionalLookProfile interiorPalette;
        [Header("独立贡献开关；主开关关闭即原世界显示")]
        public WorldLookEffectOptions effects = new WorldLookEffectOptions();
        [Header("同相机昼夜适应；天气空气仅室外，室内保留原空气")]
        public bool useEnvironmentWeather = true;

        public WorldLookSettings Current { get; private set; }
        public bool Exterior { get; private set; }
        public string Status { get; private set; } = "等待合法房间帧";
        Camera boundCamera;
        bool subscribed;

        void OnEnable()
        {
            if (!subscribed)
            {
                RoomMaskCameraRegistry.FramePublished += OnFramePublished;
                EnvironmentCameraRegistry.Published += OnEnvironmentPublished;
                EnvironmentCameraRegistry.Released += OnEnvironmentReleased;
                subscribed = true;
            }
        }

        void OnEnvironmentPublished(Camera camera)
        {
            if (camera == gameplayCamera) OnFramePublished(camera);
        }

        void OnEnvironmentReleased(int cameraId)
        {
            // 子系统清空不一定仍有可用Camera参数；撤权后立即用原静态档重建，不能残留上一天气。
            if (gameplayCamera != null && gameplayCamera.GetInstanceID() == cameraId)
                OnFramePublished(gameplayCamera);
        }

        void OnFramePublished(Camera camera)
        {
            // 明确改绑时先归还旧 Camera。不能让新相机继承上一相机的帧或自有资源。
            if (!ReferenceEquals(boundCamera, null) && (boundCamera == null || boundCamera != gameplayCamera)) ReleaseBoundCamera();
            if (!isActiveAndEnabled || camera == null || camera != gameplayCamera) return;
            if (visibilityRoot == null || rooms == null || !visibilityRoot.isActiveAndEnabled ||
                !rooms.isActiveAndEnabled || !rooms.IsReady || visibilityRoot.gameplayCamera != camera ||
                visibilityRoot.roomModule != rooms || !RoomMaskCameraRegistry.TryGet(camera, out var frame) ||
                !frame.Enabled || !frame.PositiveRenderingEnabled || frame.TruthMode ||
                frame.PositiveSurfaces == null || frame.PositiveSurfaces.Length == 0)
            { ReleaseBoundCamera(); Status = "房间帧失效；已撤销环境 Look"; return; }

            Exterior = frame.ExteriorView;
            EnvironmentFrame environment = null;
            EnvironmentCameraRegistry.TryGet(camera, out environment);
            var settings = WorldLookSettings.Build(camera, frame.ObserverPosition,
                Exterior ? outdoorPresentation : interiorPresentation,
                Exterior ? outdoorAtmosphere : interiorAtmosphere,
                Exterior ? outdoorPalette : interiorPalette, effects, environment, Exterior && useEnvironmentWeather);
            if (!settings.Enabled)
            { ReleaseBoundCamera(); Status = "环境 Look 已关闭"; return; }
            WorldLookCameraRegistry.Publish(this, camera, settings);
            boundCamera = camera; Current = settings;
            Status = Exterior ? "室外 Look；同相机天气空气 / 原房间许可保持" : "室内 Look；原空气 / 原房间许可保持";
        }

        void ReleaseBoundCamera()
        {
            if (!ReferenceEquals(boundCamera, null)) WorldLookCameraRegistry.Release(this, boundCamera);
            boundCamera = null; Current = null;
        }

        void OnValidate()
        {
            if (!ReferenceEquals(boundCamera, null) && (boundCamera == null || boundCamera != gameplayCamera || (effects != null && !effects.enabled)))
                ReleaseBoundCamera();
        }

        void OnDisable()
        {
            if (subscribed)
            {
                RoomMaskCameraRegistry.FramePublished -= OnFramePublished;
                EnvironmentCameraRegistry.Published -= OnEnvironmentPublished;
                EnvironmentCameraRegistry.Released -= OnEnvironmentReleased;
                subscribed = false;
            }
            ReleaseBoundCamera(); Status = "环境 Look 已停用";
        }

        void OnDestroy() { OnDisable(); }
    }
}
