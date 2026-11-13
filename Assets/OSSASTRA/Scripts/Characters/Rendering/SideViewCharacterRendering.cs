using System;
using System.Collections.Generic;
using Ossastra.CharacterLighting;
using Ossastra.CharacterLighting.Rendering;
using Ossastra.WorldRendering.SideView;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.Characters.Rendering
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class SideViewCharacterRendering : MonoBehaviour
    {
        public CharacterControlCoordinator session;
        public CharacterRoot[] actors = Array.Empty<CharacterRoot>();
        public Transform[] replicaArt = Array.Empty<Transform>();
        public CharacterLightingSystem lighting;
        public Shader outerShader;
        public Transform solidGeometry;

        private static readonly int PresentationOffsetId = Shader.PropertyToID("_SideViewPresentationOffset");
        private static readonly int OriginalTintId = Shader.PropertyToID("_SideViewOriginalHeroTint");
        private MaterialPropertyBlock block;
        private readonly List<Material> slots = new List<Material>(8);
        private readonly Dictionary<Renderer, Vector3> presentationOffsets = new Dictionary<Renderer, Vector3>(8);
        private readonly HashSet<Renderer> bound = new HashSet<Renderer>();
        private readonly List<Renderer> stale = new List<Renderer>(8);
        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private readonly Dictionary<CharacterRoot, SideViewOuterLight> outer = new Dictionary<CharacterRoot, SideViewOuterLight>(2);
        private CharacterCoverageCache coverage;
        private Shader coverageShader;
        private Camera camera;
        private SideViewRenderComparison comparison;
        public int LastCoverageActors => coverage == null ? 0 : coverage.ActiveActorCount;
        public int LastBoundRenderers { get; private set; }

        private void Awake()
        {
            camera = GetComponent<Camera>();
            comparison = GetComponent<SideViewRenderComparison>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += PublishPresentation;

        private bool TryGetReceiver(CharacterRoot actor, out CharacterLightingReceiver receiver)
        {
            receiver = null;
            return actor != null && actor.TryGetComponent(out receiver) &&
                receiver.SourceAvailable && receiver.gameplayCamera == camera;
        }

        private void PublishPresentation(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;
            // URP snapshots renderer properties for drawing before the coverage pass runs.
            stale.Clear();
            stale.AddRange(presentationOffsets.Keys);
            presentationOffsets.Clear();
            foreach (var actor in actors)
            {
                if (!TryGetReceiver(actor, out var receiver)) continue;
                var move = actor.GetComponent<CharacterMovement>();
                Vector3 offset = move == null ? Vector3.zero : move.PresentationOffset;
                Color originalTint = comparison != null && comparison.IsSilksong ? comparison.HeroTint(actor) : Color.white;
                foreach (var renderer in receiver.Members)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    presentationOffsets[renderer] = offset;
                    WritePresentationOffset(renderer, offset, originalTint);
                    stale.Remove(renderer);
                }
            }
            foreach (var renderer in stale)
                if (renderer != null) WritePresentationOffset(renderer, Vector3.zero, Color.white);
        }

        private void LateUpdate()
        {
            foreach (var actor in actors)
            {
                if (actor == null || !actor.TryGetComponent<SortingGroup>(out var group)) continue;
                var order = session != null && session.Current == actor ? 2 : 1;
                if (group.sortingOrder != order) group.sortingOrder = order;
            }
        }

        internal void Prepare(CommandBuffer cmd, Camera renderingCamera, Matrix4x4 gpuViewProjection, int width, int height)
        {
            if (renderingCamera != camera) return;
            bool lit = (comparison == null || !comparison.IsSilksong) &&
                CharacterLightingSystem.TryGet(camera, out var active) && active == lighting;
            if (lit && lighting.WantsCoverage && lighting.coverageShader != null)
            {
                if (coverage == null || coverageShader != lighting.coverageShader)
                {
                    ReleaseCoverage();
                    coverageShader = lighting.coverageShader;
                    coverage = new CharacterCoverageCache(coverageShader);
                }
                coverage.Append(cmd, camera, gpuViewProjection, width, height, ReadOffset);
                lighting.ReportCoverage(coverage);
            }
            else ReleaseCoverage();

            stale.Clear();
            stale.AddRange(bound);
            foreach (var actor in actors)
            {
                if (!TryGetReceiver(actor, out var receiver))
                {
                    if (actor != null && outer.TryGetValue(actor, out var hidden)) hidden.Hide();
                    continue;
                }
                var parameters = ReadLighting(receiver, lit);
                CharacterCoverageBinding outerBinding = default;
                foreach (var renderer in receiver.Members)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    CharacterCoverageBinding silhouette = default;
                    if (coverage != null && coverage.TryGet(renderer, out silhouette)) outerBinding = silhouette;
                    WriteBlocks(renderer, parameters, silhouette);
                    stale.Remove(renderer);
                    bound.Add(renderer);
                }
                SetOuter(actor, parameters, outerBinding);
            }
            foreach (var renderer in stale)
            {
                if (renderer != null) WriteBlocks(renderer, default, default);
                bound.Remove(renderer);
            }
            LastBoundRenderers = bound.Count;
        }

        private Vector3 ReadOffset(Renderer renderer) =>
            presentationOffsets.TryGetValue(renderer, out var offset) ? offset : Vector3.zero;

        private SurfaceLighting ReadLighting(CharacterLightingReceiver receiver, bool lit)
        {
            if (!lit) return default;
            var settings = receiver.Settings;
            settings.Sanitize();
            var sky = lighting.environment;
            var frame = sky.environment.Current;
            var tintA = sky.ResolveStylizedSkyTint(frame);
            var tintB = sky.ResolveCharacterEnvironmentTint(frame);
            float tint = settings.environmentTint ? settings.tintStrength : 0;
            tintA.w *= tint;
            tintB.w = Mathf.Clamp01(tintB.w * tint * Mathf.Max(1, settings.environmentBlend));
            var result = new SurfaceLighting
            {
                controls = new Vector4(1, settings.rim ? settings.rimStrength : 0,
                    settings.highlightProtection ? 1 : 0, settings.maxRimGain),
                shoulder = new Vector4(settings.knee, settings.ceiling, 0, 0),
                tintA = tintA,
                tintB = tintB,
                tintBlend = settings.environmentTint && settings.enhancedEnvironmentTint ? Mathf.Clamp01(settings.environmentBlend) : 0,
                rim = new Vector4(settings.innerWidth, settings.softness, 0, 0),
                outer = settings.outerGlow && !settings.outerDiagnostic
                    ? new Vector4(settings.outerWidth, settings.outerStrength, settings.maxOuterGain, settings.outerSoftness) : Vector4.zero
            };
            BindLocalLamp(receiver, ref result);
            return result;
        }

        private void BindLocalLamp(CharacterLightingReceiver receiver, ref SurfaceLighting result)
        {
            if (solidGeometry == null) return;
            var center = receiver.Owner.transform.position + Vector3.up
                + (receiver.Owner.TryGetComponent<CharacterMovement>(out var move) ? move.PresentationOffset : Vector3.zero);
            float bestScore = 0;
            foreach (var light in lighting.environment.localLights)
            {
                if (light == null || !light.isActiveAndEnabled || light.type != LightType.Point || light.range <= 0) continue;
                var position = light.transform.position;
                float distance = (position - center).sqrMagnitude;
                float falloff = Mathf.Clamp01(1 - distance / (light.range * light.range));
                var color = light.color.linear * light.intensity;
                float score = color.grayscale * falloff * falloff;
                if (score <= bestScore || !ClearToLight(center, position) ||
                    !ClearToLight(center + Vector3.up * .2f, position) || !ClearToLight(center - Vector3.up * .2f, position)) continue;
                bestScore = score;
                result.lightPosition = new Vector4(position.x, position.y, position.z, light.range);
                result.lightColor = new Vector4(color.r, color.g, color.b, 1);
            }
        }

        private bool ClearToLight(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length < .001f) return true;
            var physics = gameObject.scene.GetPhysicsScene();
            int count = physics.Raycast(from, delta / length, rayHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= rayHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (rayHits[i].collider != null && rayHits[i].collider.transform.IsChildOf(solidGeometry)) return false;
            return true;
        }

        private void WritePresentationOffset(Renderer renderer, Vector3 offset, Color originalTint)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(PresentationOffsetId, offset);
            block.SetVector(OriginalTintId, originalTint);
            renderer.SetPropertyBlock(block);
            renderer.GetSharedMaterials(slots);
            for (int index = 0; index < slots.Count; index++)
            {
                renderer.GetPropertyBlock(block, index);
                if (block.isEmpty) continue;
                block.SetVector(PresentationOffsetId, offset);
                block.SetVector(OriginalTintId, originalTint);
                renderer.SetPropertyBlock(block, index);
            }
        }

        private void WriteBlocks(Renderer renderer, SurfaceLighting parameters, CharacterCoverageBinding silhouette)
        {
            renderer.GetPropertyBlock(block);
            WriteBlock(parameters, silhouette);
            renderer.SetPropertyBlock(block);
            renderer.GetSharedMaterials(slots);
            for (int index = 0; index < slots.Count; index++)
            {
                renderer.GetPropertyBlock(block, index);
                if (block.isEmpty) continue;
                WriteBlock(parameters, silhouette);
                renderer.SetPropertyBlock(block, index);
            }
        }

        private void WriteBlock(SurfaceLighting light, CharacterCoverageBinding silhouette)
        {
            // 由本场 EnvironmentRoot 的角色可读性保底；材质模板的默认 1 会抹平洞窟底光。
            block.SetFloat("_EnvironmentMinimumOverride", 0f);
            block.SetVector("_EnvironmentStylizedSkyTint", light.tintA);
            block.SetVector("_CharacterEnvironmentTint", light.tintB);
            block.SetFloat("_CharacterEnvironmentBlend", light.tintBlend);
            block.SetVector("_CharacterLightControls", light.controls);
            block.SetVector("_CharacterLightShoulder", light.shoulder);
            block.SetVector("_CharacterRimShape", light.rim);
            block.SetVector("_CharacterRimLocalPosition", light.lightPosition);
            block.SetVector("_CharacterRimLocalColor", light.lightColor);
            bool hasCoverage = silhouette.Owner != null && coverage != null && coverage.Atlas != null;
            block.SetFloat("_CharacterCoverageReady", hasCoverage ? 1 : 0);
            block.SetTexture("_CharacterCoverageTexture", hasCoverage ? coverage.Atlas : Texture2D.blackTexture);
            block.SetVector("_CharacterCoverageRect", silhouette.ScreenRect);
            block.SetVector("_CharacterCoverageAtlasRect", silhouette.AtlasRect);
            block.SetVector("_CharacterCoverageScreenSize", silhouette.ScreenSize);
            block.SetVector("_CharacterCoverageTexelSize", hasCoverage
                ? new Vector4(1f / coverage.Atlas.width, 1f / coverage.Atlas.height, 0, 0) : Vector4.zero);
        }

        private void SetOuter(CharacterRoot actor, SurfaceLighting light, CharacterCoverageBinding silhouette)
        {
            if (outerShader == null || !outerShader.isSupported || silhouette.Owner == null || light.outer.y <= 0 ||
                light.outer.z <= 0 || light.lightColor.w == 0)
            {
                if (outer.TryGetValue(actor, out var existing)) existing.Hide();
                return;
            }
            if (!outer.TryGetValue(actor, out var glow))
            {
                glow = new SideViewOuterLight(actor.transform, outerShader);
                outer.Add(actor, glow);
            }
            block.Clear();
            WriteBlock(light, silhouette);
            block.SetVector("_SideViewOuter", light.outer);
            glow.Present(camera, silhouette, block);
        }

        private void ReleaseCoverage()
        {
            if (coverage == null) return;
            coverage.Dispose();
            coverage = null;
            coverageShader = null;
            if (lighting != null) lighting.ReportCoverage(null);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= PublishPresentation;
            foreach (var renderer in presentationOffsets.Keys)
                if (renderer != null) WritePresentationOffset(renderer, Vector3.zero, Color.white);
            presentationOffsets.Clear();
            ReleaseCoverage();
            foreach (var renderer in bound) if (renderer != null) WriteBlocks(renderer, default, default);
            bound.Clear();
            foreach (var glow in outer.Values) glow.Dispose();
            outer.Clear();
            LastBoundRenderers = 0;
        }

        private struct SurfaceLighting
        {
            public Vector4 controls, shoulder, tintA, tintB, rim, outer, lightPosition, lightColor;
            public float tintBlend;
        }
    }
}
