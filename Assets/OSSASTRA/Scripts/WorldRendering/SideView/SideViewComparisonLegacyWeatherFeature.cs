using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using Ossastra.WorldRendering.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewComparisonLegacyWeatherFeature : ScriptableRendererFeature
    {
        private SkyPass sky;
        private RainPass rain;

        public override void Create()
        {
            sky = new SkyPass();
            rain = new RainPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (!TryGetExterior(data.cameraData.camera, out _, out var zones)) return;

            renderer.EnqueuePass(sky);
            if (zones != null && zones.Length > 0 &&
                EnvironmentCameraRegistry.TryGet(data.cameraData.camera, out var frame) && frame.Enabled && frame.Rain > .001f)
                renderer.EnqueuePass(rain);
        }

        private static bool TryGetExterior(Camera camera, out Vector3 observer, out BoxCollider[] zones)
        {
            observer = default;
            zones = null;
            return camera != null && camera.cameraType == CameraType.Game &&
                camera.TryGetComponent<SideViewRenderComparison>(out var comparison) && comparison.isActiveAndEnabled &&
                comparison.TryGetLegacyExteriorObserver(out observer, out zones);
        }

        private static Matrix4x4 InverseViewProjection(CameraData camera) =>
            (camera.GetGPUProjectionMatrix() * camera.GetViewMatrix()).inverse;

        private sealed class SkyPass : ScriptableRenderPass
        {
            private static readonly int InverseViewProjectionId = Shader.PropertyToID("_EnvironmentSideViewInverseViewProjection");

            internal SkyPass() => renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data) =>
                ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle, data.cameraData.renderer.cameraDepthTargetHandle);

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var camera = data.cameraData.camera;
                if (!TryGetExterior(camera, out _, out _) ||
                    !EnvironmentSkyBinding.TryPrepareSideViewSky(camera, out var material, out int pass)) return;

                material.SetMatrix(InverseViewProjectionId, InverseViewProjection(data.cameraData));
                var cmd = CommandBufferPool.Get("Side view A sky behind transparent art");
                try
                {
                    cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
        }

        private sealed class RainPass : ScriptableRenderPass
        {
            private const int MaxZones = 8;
            private static readonly int InverseViewProjectionId = Shader.PropertyToID("_EnvironmentSideViewInverseViewProjection");
            private static readonly int ObserverId = Shader.PropertyToID("_EnvironmentSideViewRainObserver");
            private static readonly int PermissionId = Shader.PropertyToID("_EnvironmentSideViewRainPermission");
            private static readonly int DepthId = Shader.PropertyToID("_EnvironmentSideViewRainDepth");
            private static readonly int RasterId = Shader.PropertyToID("_EnvironmentSideViewRainRaster");
            private static readonly int ZoneCountId = Shader.PropertyToID("_EnvironmentSideViewRainZoneCount");
            private static readonly int ZoneMatricesId = Shader.PropertyToID("_EnvironmentSideViewRainZoneMatrices");
            private static readonly int ZoneExtentsId = Shader.PropertyToID("_EnvironmentSideViewRainZoneExtents");

            private readonly Matrix4x4[] zoneMatrices = new Matrix4x4[MaxZones];
            private readonly Vector4[] zoneExtents = new Vector4[MaxZones];

            // In RendererData this feature follows SideViewComparisonMetadataFeature at this event.
            internal RainPass() => renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data) =>
                ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle);

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var cameraData = data.cameraData;
                var camera = cameraData.camera;
                if (!TryGetExterior(camera, out var observer, out var zones) || !PrepareZones(camera, zones) ||
                    !WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out var permission, out var depth) ||
                    !TryPrepareRaster(cameraData, permission, depth, out var raster) ||
                    !EnvironmentSkyBinding.TryPrepareSideViewRain(camera, out var material, out int pass, out int count)) return;

                material.SetMatrix(InverseViewProjectionId, InverseViewProjection(cameraData));
                material.SetVector(ObserverId, observer);
                material.SetTexture(PermissionId, permission.rt);
                material.SetTexture(DepthId, depth.rt);
                material.SetVector(RasterId, raster);
                material.SetInt(ZoneCountId, zones.Length);
                material.SetMatrixArray(ZoneMatricesId, zoneMatrices);
                material.SetVectorArray(ZoneExtentsId, zoneExtents);

                var cmd = CommandBufferPool.Get("Side view A rain with current visible depth");
                try
                {
                    cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 6, count);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            private bool PrepareZones(Camera camera, BoxCollider[] zones)
            {
                if (zones == null || zones.Length < 1 || zones.Length > MaxZones) return false;
                for (int i = 0; i < zones.Length; i++)
                {
                    var zone = zones[i];
                    if (zone == null || zone.gameObject.scene != camera.gameObject.scene) return false;
                    var extent = zone.size * .5f;
                    var matrix = Matrix4x4.Translate(-zone.center) * zone.transform.worldToLocalMatrix;
                    if (!(extent.x > 0 && extent.y > 0 && extent.z > 0) ||
                        !Finite(extent.x) || !Finite(extent.y) || !Finite(extent.z) ||
                        !Finite(matrix.m03) || !Finite(matrix.m13) || !Finite(matrix.m23) || !Finite(matrix.determinant) ||
                        Mathf.Abs(matrix.determinant) < .000001f) return false;
                    zoneMatrices[i] = matrix;
                    zoneExtents[i] = extent;
                }
                return true;
            }

            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

            private static bool TryPrepareRaster(CameraData camera, RTHandle permission, RTHandle depth, out Vector4 raster)
            {
                raster = default;
                var color = camera.renderer.cameraColorTargetHandle;
                if (!camera.isDefaultViewport || color?.rt == null || !color.rt.IsCreated() || permission?.rt == null || depth?.rt == null ||
                    permission.rt.dimension != TextureDimension.Tex2D || depth.rt.dimension != TextureDimension.Tex2D ||
                    permission.rt.antiAliasing != 1 || depth.rt.antiAliasing != 1 ||
                    permission.rt.width != color.rt.width || permission.rt.height != color.rt.height ||
                    depth.rt.width != color.rt.width || depth.rt.height != color.rt.height ||
                    camera.IsHandleYFlipped(permission) != camera.IsHandleYFlipped(depth)) return false;

                bool flip = camera.IsHandleYFlipped(color) != camera.IsHandleYFlipped(depth);
                raster = new Vector4(1f / color.rt.width, 1f / color.rt.height, flip ? -1f : 1f, flip ? 1f : 0f);
                return true;
            }
        }
    }
}
