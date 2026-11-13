using System;
using Ossastra.CharacterLighting;
using Ossastra.CharacterLighting.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.Characters.Rendering
{
    internal sealed class SideViewOuterLight : IDisposable
    {
        private static readonly int FusionEnabled = Shader.PropertyToID("_SideViewAceOuterEnabled");
        private static readonly int FusionSlot = Shader.PropertyToID("_SideViewAceOuterSlot");
        private static readonly int FusionDepthAnchor = Shader.PropertyToID("_SideViewAceOuterDepthAnchor");
        private static readonly int FusionCoverage = Shader.PropertyToID("_SideViewAceOuterCoverage");
        private static readonly int FusionViewport = Shader.PropertyToID("_SideViewAceOuterViewport");
        private static readonly int FusionTexel = Shader.PropertyToID("_SideViewAceOuterAtlasTexel");
        private static readonly int FusionScreen = Shader.PropertyToID("_SideViewAceOuterScreen");
        private static readonly int FusionAtlas = Shader.PropertyToID("_SideViewAceOuterAtlas");
        private static readonly int FusionOtherScreen = Shader.PropertyToID("_SideViewAceOuterOtherScreen");
        private static readonly int FusionOtherAtlas = Shader.PropertyToID("_SideViewAceOuterOtherAtlas");
        private static readonly int FusionSettings = Shader.PropertyToID("_SideViewAceOuterSettings");
        private static readonly Vector3[] ScreenQuad = { Vector3.zero, Vector3.right, Vector3.up, Vector3.right + Vector3.up };
        // 两个固定角色槽由正式游戏相机的 Coverage Pass 按帧填写。
        private static readonly Vector4[] ScreenRects = new Vector4[CharacterCoverageCache.MaxActors];
        private static readonly Vector4[] AtlasRects = new Vector4[CharacterCoverageCache.MaxActors];
        private static readonly Vector4[] OtherScreenRects = new Vector4[CharacterCoverageCache.MaxActors];
        private static readonly Vector4[] OtherAtlasRects = new Vector4[CharacterCoverageCache.MaxActors];
        private static readonly Vector4[] Settings = new Vector4[CharacterCoverageCache.MaxActors];

        private readonly Transform actor;
        private readonly CharacterLightingReceiver receiver;
        private readonly CharacterMovement movement;
        private readonly MeshRenderer renderer;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly MaterialPropertyBlock fusionProperties = new MaterialPropertyBlock();
        private int fusionSlot = -1;

        public SideViewOuterLight(Transform actor, Shader shader)
        {
            this.actor = actor;
            receiver = actor.GetComponent<CharacterLightingReceiver>();
            movement = actor.GetComponent<CharacterMovement>();
            var child = new GameObject("Character outer light") { hideFlags = HideFlags.HideAndDontSave, layer = actor.gameObject.layer };
            child.transform.SetParent(actor, false);
            mesh = new Mesh { name = "Side view silhouette extents", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.vertices = corners;
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = short.MinValue;
            renderer.forceRenderingOff = true;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            renderer.sharedMaterial = material;
        }

        public void Present(Camera camera, Ossastra.CharacterLighting.Rendering.CharacterCoverageBinding coverage, MaterialPropertyBlock properties)
        {
            if (fusionSlot >= 0)
            {
                fusionSlot = -1;
                renderer.ResetBounds();
            }
            properties.SetFloat(FusionSlot, 0);
            var rect = coverage.ScreenRect;
            var size = coverage.ScreenSize;
            float pad = Mathf.Max(2, 4 * size.y / 1080f) + 1;
            var minimum = new Vector2(rect.x - pad * size.z, 1 - (rect.y + rect.w + pad * size.w));
            var maximum = new Vector2(rect.x + rect.z + pad * size.z, 1 - (rect.y - pad * size.w));
            float depth = camera.WorldToViewportPoint(renderer.transform.position).z;
            var transform = renderer.transform;
            corners[0] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(minimum.x, minimum.y, depth)));
            corners[1] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(maximum.x, minimum.y, depth)));
            corners[2] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(minimum.x, maximum.y, depth)));
            corners[3] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(maximum.x, maximum.y, depth)));
            mesh.vertices = corners;
            mesh.RecalculateBounds();
            renderer.SetPropertyBlock(properties);
            renderer.forceRenderingOff = false;
        }

        public void ArmFusion(Camera camera, int index)
        {
            if (camera == null || receiver == null || index < 0 || index >= CharacterCoverageCache.MaxActors)
            {
                Hide();
                return;
            }

            var offset = movement != null ? movement.PresentationOffset : Vector3.zero;
            var depthAnchor = actor.position + offset;
            var bounds = new Bounds(depthAnchor, Vector3.zero);
            var members = receiver.Members;
            for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                var member = members[memberIndex];
                if (member == null || !member.enabled || member.forceRenderingOff || !member.gameObject.activeInHierarchy) continue;
                var memberBounds = member.bounds;
                memberBounds.center += offset;
                bounds.Encapsulate(memberBounds);
            }

            // 仅为剔除保守扩大边界。实际柔光几何由同帧轮廓在 GPU 上裁到矩形。
            float depth = camera.WorldToViewportPoint(bounds.center).z + bounds.extents.magnitude;
            float viewHeight = camera.orthographic ? 2 * camera.orthographicSize :
                2 * Mathf.Max(camera.nearClipPlane, depth) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            bounds.Expand(Mathf.Max(.01f, viewHeight * (10f / 1080f)));
            if (fusionSlot < 0)
            {
                mesh.vertices = ScreenQuad;
                mesh.RecalculateBounds();
            }
            fusionSlot = index;
            renderer.bounds = bounds;
            fusionProperties.SetFloat(FusionSlot, index + 1);
            fusionProperties.SetVector(FusionDepthAnchor, depthAnchor);
            renderer.SetPropertyBlock(fusionProperties);
            renderer.forceRenderingOff = false;
        }

        internal static void ResetFusion(CommandBuffer cmd)
        {
            Array.Clear(Settings, 0, Settings.Length);
            cmd.SetGlobalFloat(FusionEnabled, 0);
            cmd.SetGlobalTexture(FusionCoverage, Texture2D.blackTexture);
        }

        internal void PresentFusion(CommandBuffer cmd, CharacterCoverageBinding coverage, Texture atlas, Vector4 settings,
            CharacterCoverageBinding other)
        {
            if (fusionSlot < 0 || coverage.Owner == null || atlas == null || settings.x <= 0 || settings.y <= 0 || settings.z <= 0)
                return;

            ScreenRects[fusionSlot] = coverage.ScreenRect;
            AtlasRects[fusionSlot] = coverage.AtlasRect;
            OtherScreenRects[fusionSlot] = other.Owner == null ? Vector4.zero : other.ScreenRect;
            OtherAtlasRects[fusionSlot] = other.Owner == null ? Vector4.zero : other.AtlasRect;
            Settings[fusionSlot] = settings;
            cmd.SetGlobalTexture(FusionCoverage, atlas);
            cmd.SetGlobalVector(FusionViewport, coverage.ScreenSize);
            cmd.SetGlobalVector(FusionTexel, new Vector4(1f / atlas.width, 1f / atlas.height, 0, 0));
            cmd.SetGlobalVectorArray(FusionScreen, ScreenRects);
            cmd.SetGlobalVectorArray(FusionAtlas, AtlasRects);
            cmd.SetGlobalVectorArray(FusionOtherScreen, OtherScreenRects);
            cmd.SetGlobalVectorArray(FusionOtherAtlas, OtherAtlasRects);
            cmd.SetGlobalVectorArray(FusionSettings, Settings);
            cmd.SetGlobalFloat(FusionEnabled, 1);
        }

        public void Hide() { if (renderer != null) renderer.forceRenderingOff = true; }

        public void Dispose()
        {
            Hide();
            if (renderer != null) CoreUtils.Destroy(renderer.gameObject);
            CoreUtils.Destroy(mesh);
            CoreUtils.Destroy(material);
        }
    }
}
