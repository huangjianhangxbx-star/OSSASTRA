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
        [SerializeField] private Shader aceSpineShader;
        [SerializeField] private Shader aceSpriteShader;
        [SerializeField] private Texture2D aceStraightAtlas;
        [SerializeField] private Shader aceCoverageShader;
        [SerializeField] private Vector4 aceRim = new Vector4(2.5f, .9f, .8f, .32f);
        [SerializeField] private Vector4 aceCalibratedRim = new Vector4(4.5f, .9f, 2.4f, .55f);
        [SerializeField] private bool startWithCalibratedAceRim;
        [SerializeField] private Vector4 aceOuter = new Vector4(3.8f, .48f, .26f, 1.25f);
        [SerializeField] private Vector4 aceShoulder = new Vector4(.85f, 1.2f, .016f, .14f);

        private static readonly int PresentationOffsetId = Shader.PropertyToID("_SideViewPresentationOffset");
        private static readonly int OriginalTintId = Shader.PropertyToID("_SideViewOriginalHeroTint");
        private static readonly int FusionActorId = Shader.PropertyToID("_SideViewAceFusionActor");
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
        private readonly CharacterCoverageBinding[] fusionBindings = new CharacterCoverageBinding[2];
        private readonly Vector4[] fusionRects = new Vector4[2];
        private readonly Vector4[] fusionTiles = new Vector4[2];
        private bool innerEnabled = true, outerEnabled = true, wasFusion, useCalibratedAceRim;
        private Vector4 aceColorDiagnostic;
        public int LastCoverageActors => coverage == null ? 0 : coverage.ActiveActorCount;
        public int LastCoverageDraws => coverage == null ? 0 : coverage.DrawCount;
        public Texture CoveragePreview => coverage?.Atlas;
        public int LastBoundRenderers { get; private set; }
        public bool CanUseAceCharacterMaterial => aceSpineShader != null && aceSpineShader.isSupported &&
            aceSpriteShader != null && aceSpriteShader.isSupported;
        public bool CanUseAceFusion => CanUseAceCharacterMaterial && aceCoverageShader != null && aceCoverageShader.isSupported &&
            outerShader != null && outerShader.isSupported;
        public bool UsesCalibratedAceRim => useCalibratedAceRim;
        public void SelectAceRimCalibrated() => useCalibratedAceRim = true;
        public void SelectAceRimRestrained() => useCalibratedAceRim = false;
        public void EnableAceInner(bool enabled) => innerEnabled = enabled;
        public void EnableAceOuter(bool enabled) => outerEnabled = enabled;
        public void SetAceColorDiagnostic(Vector4 setting) => aceColorDiagnostic = setting;

        private void Awake()
        {
            useCalibratedAceRim = startWithCalibratedAceRim;
            camera = GetComponent<Camera>();
            comparison = GetComponent<SideViewRenderComparison>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += PublishPresentation;
            RenderPipelineManager.endCameraRendering += ClearAceCameraInputs;
        }

        private bool TryGetReceiver(CharacterRoot actor, out CharacterLightingReceiver receiver)
        {
            receiver = null;
            return actor != null && actor.TryGetComponent(out receiver) &&
                receiver.SourceAvailable && receiver.gameplayCamera == camera;
        }

        private void PublishPresentation(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;
            Shader.SetGlobalVector("_SideViewAceColorDiagnostic", comparison != null && comparison.UsesAceCharacterMaterial
                ? aceColorDiagnostic : Vector4.zero);
            PublishMaterialStrategy(comparison != null && comparison.UsesAceCharacterMaterial);
            // URP snapshots renderer properties for drawing before the coverage pass runs.
            stale.Clear();
            stale.AddRange(presentationOffsets.Keys);
            presentationOffsets.Clear();
            bool fusion = comparison != null && comparison.UsesAceFusionMaterial;
            if (!fusion && wasFusion)
            {
                foreach (var glow in outer.Values) glow.Dispose();
                outer.Clear();
            }
            wasFusion = fusion;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (!TryGetReceiver(actor, out var receiver)) continue;
                var move = actor.GetComponent<CharacterMovement>();
                Vector3 offset = move == null ? Vector3.zero : move.PresentationOffset;
                Color originalTint = comparison != null && comparison.IsSilksong ? comparison.HeroTint(actor) : Color.white;
                foreach (var renderer in receiver.Members)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    presentationOffsets[renderer] = offset;
                    WritePresentationOffset(renderer, offset, originalTint, fusion && i < fusionBindings.Length ? i + 1 : 0);
                    stale.Remove(renderer);
                }
                if (fusion && i < fusionBindings.Length && outerEnabled && aceOuter.y > 0 && aceOuter.z > 0)
                    GetOuter(actor).ArmFusion(camera, i);
            }
            foreach (var renderer in stale)
                if (renderer != null) WritePresentationOffset(renderer, Vector3.zero, Color.white);
        }

        internal void PublishMaterialStrategy(bool useAce)
        {
            useAce &= CanUseAceCharacterMaterial;
            foreach (var actor in actors)
                if (actor != null && actor.TryGetComponent<CharacterUprightDepth>(out var owner))
                    owner.SetSideViewAceMaterial(useAce, aceSpineShader, aceSpriteShader, aceStraightAtlas);
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
            bool lit = (comparison == null || !comparison.IsSilksong && !comparison.UsesAceCharacterMaterial) &&
                CharacterLightingSystem.TryGet(camera, out var active) && active == lighting;
            bool fusion = comparison != null && comparison.UsesAceFusionMaterial;
            var shader = fusion ? aceCoverageShader : lit && lighting.WantsCoverage ? lighting.coverageShader : null;
            if (shader != null)
            {
                if (coverage == null || coverageShader != shader)
                {
                    ReleaseCoverage();
                    coverageShader = shader;
                    coverage = new CharacterCoverageCache(coverageShader);
                }
                coverage.Append(cmd, camera, gpuViewProjection, width, height, ReadOffset);
                if (lighting != null) lighting.ReportCoverage(coverage);
            }
            else ReleaseCoverage();
            if (fusion) PublishFusionCoverage(cmd, width, height);

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
                    if (!fusion && coverage != null && coverage.TryGet(renderer, out silhouette)) outerBinding = silhouette;
                    WriteBlocks(renderer, parameters, silhouette);
                    stale.Remove(renderer);
                    bound.Add(renderer);
                }
                if (!fusion) SetOuter(actor, parameters, outerBinding);
            }
            foreach (var renderer in stale)
            {
                if (renderer != null) WriteBlocks(renderer, default, default);
                bound.Remove(renderer);
            }
            LastBoundRenderers = bound.Count;
        }

        private void PublishFusionCoverage(CommandBuffer cmd, int width, int height)
        {
            SideViewOuterLight.ResetFusion(cmd);
            var atlas = coverage?.Atlas;
            for (int i = 0; i < fusionBindings.Length; i++)
            {
                CharacterCoverageBinding binding = default;
                if (atlas != null && i < actors.Length && TryGetReceiver(actors[i], out var receiver))
                    foreach (var member in receiver.Members)
                        if (coverage.TryGet(member, out binding)) break;
                fusionBindings[i] = binding;
                fusionRects[i] = binding.ScreenRect;
                fusionTiles[i] = binding.AtlasRect;
            }
            cmd.SetGlobalTexture("_SideViewAceFusionCoverage", atlas != null ? (Texture)atlas : Texture2D.blackTexture);
            cmd.SetGlobalVectorArray("_SideViewAceFusionRect", fusionRects);
            cmd.SetGlobalVectorArray("_SideViewAceFusionTile", fusionTiles);
            cmd.SetGlobalVector("_SideViewAceFusionScreen", new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector("_SideViewAceFusionTexel", atlas != null ? new Vector4(1f / atlas.width, 1f / atlas.height, 0, 0) : Vector4.zero);
            Vector4 rim = useCalibratedAceRim ? aceCalibratedRim : aceRim;
            if (!innerEnabled) rim.z = 0;
            cmd.SetGlobalVector("_SideViewAceFusionRim", rim);
            cmd.SetGlobalVector("_SideViewAceFusionShoulder", aceShoulder);
            for (int i = 0; i < Mathf.Min(actors.Length, fusionBindings.Length); i++)
                if (actors[i] != null && outer.TryGetValue(actors[i], out var glow))
                {
                    var binding = fusionBindings[i];
                    if (outerEnabled && aceColorDiagnostic.x < .5f && atlas != null && binding.Owner != null)
                        glow.PresentFusion(cmd, binding, atlas, aceOuter, fusionBindings[1 - i]);
                    else glow.Hide();
                }
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

        private void WritePresentationOffset(Renderer renderer, Vector3 offset, Color originalTint, int fusionActor = 0)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(PresentationOffsetId, offset);
            block.SetVector(OriginalTintId, originalTint);
            block.SetFloat(FusionActorId, fusionActor);
            renderer.SetPropertyBlock(block);
            renderer.GetSharedMaterials(slots);
            for (int index = 0; index < slots.Count; index++)
            {
                renderer.GetPropertyBlock(block, index);
                if (block.isEmpty) continue;
                block.SetVector(PresentationOffsetId, offset);
                block.SetVector(OriginalTintId, originalTint);
                block.SetFloat(FusionActorId, fusionActor);
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
            block.Clear();
            WriteBlock(light, silhouette);
            block.SetVector("_SideViewOuter", light.outer);
            GetOuter(actor).Present(camera, silhouette, block);
        }

        private SideViewOuterLight GetOuter(CharacterRoot actor)
        {
            if (!outer.TryGetValue(actor, out var glow))
            {
                glow = new SideViewOuterLight(actor.transform, outerShader);
                outer.Add(actor, glow);
            }
            return glow;
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
            RenderPipelineManager.endCameraRendering -= ClearAceCameraInputs;
            ClearAceCameraInputs(default, camera);
            PublishMaterialStrategy(false);
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

        private void ClearAceCameraInputs(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;
            Shader.SetGlobalVector("_SideViewAceColorDiagnostic", Vector4.zero);
            Shader.SetGlobalTexture("_SideViewAceFusionCoverage", Texture2D.blackTexture);
        }

        private struct SurfaceLighting
        {
            public Vector4 controls, shoulder, tintA, tintB, rim, outer, lightPosition, lightColor;
            public float tintBlend;
        }
    }
}
