/*
 * 脚本：SideViewRenderComparison
 * 功能：提交横版三套渲染方案，并保有 C 的角色材质选择与相机资源租约。
 * 边界：使用现有角色表现、世界与相机，只在显式登记的场景工作。
 * 协作：B 的区域色和光图锚点取自同一帧的角色表现，外部 UI 仅提交切换请求。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterLighting;
using Ossastra.Characters.Rendering;
using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using SideViewActor = Ossastra.Characters.CharacterRoot;
using SideViewMovement = Ossastra.Characters.CharacterMovement;

namespace Ossastra.WorldRendering.SideView
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera), typeof(SideViewCharacterRendering))]
    public sealed class SideViewRenderComparison : MonoBehaviour
    {
        public enum Recipe { Legacy = 1, Silksong = 2, Ace = 3 }
        public enum AceCharacterMaterial { Original, Replica }

        [Serializable]
        private struct TintRegion
        {
            public Rect worldArea;
            public Color gammaTint;
            [Min(0)] public float seconds;
        }

        private static readonly int ModeId = Shader.PropertyToID("_SideViewRenderMode");
        private static readonly int SplitId = Shader.PropertyToID("_SideViewSplitBackground");
        private static readonly int AmbientId = Shader.PropertyToID("_SideViewAmbientGamma");
        private static readonly int LegacyCoverageId = Shader.PropertyToID("_SideViewWorldLookCoverage");
        private static readonly int AceSurfaceResponseId = Shader.PropertyToID("_SideViewAceSurfaceResponse");

        [SerializeField] private SideViewComparisonCardCatalog cards;
        [SerializeField] private Shader comparisonCard;
        [SerializeField] private SideViewComparisonLegacyWorldLook legacyWorldLook;
        [SerializeField] private SideViewComparisonLegacyEnvironment legacyEnvironment;
        [SerializeField] private EnvironmentRoot environment;
        [SerializeField] private EnvironmentSkyBinding sky;
        [SerializeField] private CharacterLightingReceiver blue;
        [SerializeField] private CharacterLightingReceiver freycia;
        [SerializeField] private Rect[] legacyInteriorRegions = Array.Empty<Rect>();
        [SerializeField] private BoxCollider[] legacyRainZones = Array.Empty<BoxCollider>();
        [SerializeField] private Material silksongLayers;
        [SerializeField] private Material silksongGrade;
        [SerializeField] private Texture2D heroLight;
        [SerializeField] private Texture2D vignette;
        [SerializeField] private Shader darknessShader;
        [SerializeField] private Texture2D ultrawideCutout;
        [SerializeField] private Texture2D whiteFader;
        [SerializeField] private Transform darknessSourceRoot;
        [SerializeField] private Text statusLabel;
        [SerializeField] private SideViewAceLighting aceLighting;
        [SerializeField] private GameObject aceCharacterControls;
        [SerializeField] private Vector2 heroLightSize = new Vector2(18.52f, 17.53f);
        [SerializeField] private Vector2 heroLightOffset = new Vector2(0, -.60f);
        [SerializeField] private Color heroLightGamma = new Color(.850522f, .860294f, .816014f, .722f);
        [SerializeField] private TintRegion[] tintRegions = Array.Empty<TintRegion>();

        private readonly Dictionary<Renderer, (Material source, Material owned)> assignments = new();
        private readonly Dictionary<(Material source, float role, float meta, float ambient, Vector4 tint, bool far, bool below, bool replace, float aceResponse), Material> variants = new();
        private readonly Dictionary<SideViewActor, TintState> heroTints = new(2);
        private Camera ownedCamera;
        private SideViewCharacterRendering characters;
        private Recipe requested = Recipe.Legacy;
        private Recipe aceReturnRecipe = Recipe.Legacy;
        private AceCharacterMaterial requestedAceMaterial = AceCharacterMaterial.Replica;
        private Action restoreLegacy;
        private Action restoreAceProfiles;
        private bool inRender;
        private bool silksongMsaaLease;
        private bool savedCameraMsaa;
        private bool aceRenderFailed;

        internal static event Action<int> Released;

        public Recipe? Active { get; private set; }
        public AceCharacterMaterial ActiveAceMaterial { get; private set; } = AceCharacterMaterial.Replica;
        public string Status { get; private set; }
        internal bool IsSilksong => Active == Recipe.Silksong;
        internal bool UsesAceCharacterMaterial => Active == Recipe.Ace && ActiveAceMaterial == AceCharacterMaterial.Replica;
        internal Material SilksongLayers => silksongLayers;
        internal Material SilksongGrade => silksongGrade;
        internal Texture2D HeroLight => heroLight;
        internal Texture2D Vignette => vignette;
        internal Color HeroLightGamma => heroLightGamma;
        internal SideViewComparisonDarkness CreateDarkness() =>
            new(darknessShader, ultrawideCutout, whiteFader, darknessSourceRoot);
        public int LastBackgroundFrame { get; internal set; } = -1;
        public int LastGradeFrame { get; internal set; } = -1;
        public int LastDarknessFrame { get; internal set; } = -1;

        public void SelectLegacy() => requested = Recipe.Legacy;
        public void SelectSilksong() => requested = Recipe.Silksong;
        public void SelectAce() => requested = Recipe.Ace;
        public void SelectAceOriginal() => requestedAceMaterial = AceCharacterMaterial.Original;
        public void SelectAceReplica() => requestedAceMaterial = AceCharacterMaterial.Replica;

        private void OnEnable()
        {
            ownedCamera = GetComponent<Camera>();
            characters = GetComponent<SideViewCharacterRendering>();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private bool TryPrepareCards()
        {
            if (assignments.Count != 0) return true;
            if (cards == null || cards.Entries.Count == 0 || comparisonCard == null || !comparisonCard.isSupported)
            {
                Status = "场景画片清单或对照 Shader 未就绪。";
                return false;
            }

            foreach (var card in cards.Entries)
            {
                if (card.Renderer == null || card.SourceMaterial == null || card.Renderer.sharedMaterial != card.SourceMaterial ||
                    card.Renderer.sharedMaterials.Length != 1 || assignments.ContainsKey(card.Renderer))
                {
                    Status = "画片绑定与作者清单不一致。";
                    ReleaseCards();
                    return false;
                }
                float aceResponse = card.SurfaceKind switch
                {
                    "Ground" or "Structure" => 1,
                    "Decoration" => .85f,
                    "NearBackground" => .55f,
                    "FarBackground" => .3f,
                    _ => 0
                };
                var key = (card.SourceMaterial, card.EnvironmentRole, card.MetadataRole, card.OriginalAmbient,
                    card.OriginalColorScale, card.FarBackground, card.BelowActors, card.ReplacementCoverage, aceResponse);
                if (!variants.TryGetValue(key, out var variant))
                {
                    variant = new Material(comparisonCard) { name = card.SourceMaterial.name + " (Side view comparison)", hideFlags = HideFlags.HideAndDontSave };
                    variants.Add(key, variant);
                    try
                    {
                        card.ConfigureVariant(variant);
                        variant.SetFloat(AceSurfaceResponseId, aceResponse);
                        variant.SetShaderPassEnabled("SideViewFarCard", card.FarBackground);
                        variant.SetShaderPassEnabled("SideViewUnderActorCard", card.BelowActors && !card.FarBackground);
                        variant.SetShaderPassEnabled("SideViewVisibleSurface", card.MetadataRole >= 0);
                    }
                    catch (ArgumentException error)
                    {
                        Status = error.Message;
                        ReleaseCards();
                        return false;
                    }
                }
                assignments.Add(card.Renderer, (card.SourceMaterial, variant));
            }

            foreach (var pair in assignments) pair.Key.sharedMaterial = pair.Value.owned;
            return true;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != ownedCamera || Active == null) return;
            if (Active == Recipe.Legacy)
            {
                if (!TryPublishLegacy(out var reason))
                {
                    Status = reason;
                    legacyWorldLook.Release(this, ownedCamera);
                    return;
                }
            }
            var cmd = CommandBufferPool.Get("Side view comparison camera scope");
            if (Active == Recipe.Silksong)
            {
                // 原作横版相机为单采样。B 的颜色链反复读取同一相机颜色，避免 URP 的多采样解析中途丢弃透明层。
                savedCameraMsaa = camera.allowMSAA;
                silksongMsaaLease = true;
                camera.allowMSAA = false;
            }
            cmd.SetGlobalFloat(ModeId, (float)Active.Value);
            cmd.SetGlobalFloat(SplitId, 0);
            cmd.SetGlobalFloat(LegacyCoverageId, Active == Recipe.Legacy ? 1 : 0);
            cmd.SetGlobalVector(AmbientId, new Vector4(.469823548f, .511372566f, .527352956f, 1));
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            inRender = true;
        }

        private void LateUpdate()
        {
            if (aceRenderFailed && requested == Recipe.Ace)
            {
                requested = aceReturnRecipe;
                TrySelect(requested);
                if (Active != Recipe.Ace) Status = "C 未生成当前相机的光图，已退出 C。";
            }
            aceRenderFailed = false;
            if (requested != Active && TryPrepareCards()) TrySelect(requested);
            if (Active == null) ReleaseCards();
            else if (IsSilksong) UpdateHeroTints();
            else if (Active == Recipe.Ace && requested == Active && ActiveAceMaterial != requestedAceMaterial)
                ApplyAceMaterialRequest();
            if (aceCharacterControls != null && aceCharacterControls.activeSelf != (Active == Recipe.Ace))
                aceCharacterControls.SetActive(Active == Recipe.Ace);
            if (statusLabel != null && statusLabel.text != Status) statusLabel.text = Status;
        }

        private void TrySelect(Recipe recipe)
        {
            if (!CanSelect(recipe, out var reason))
            {
                Status = reason;
                if (Active.HasValue) requested = Active.Value;
                return;
            }

            var previous = Active;
            ReleaseMode();
            if (TryAcquire(recipe, out reason))
            {
                if (recipe == Recipe.Ace && previous.HasValue) aceReturnRecipe = previous.Value;
                Active = recipe;
                Status = DescribeActive();
                return;
            }
            // 提交失败后重新取得上一方案的实际输入，而不是只改回显示标签。
            ReleaseMode();
            Active = null;
            if (previous.HasValue)
            {
                if (TryAcquire(previous.Value, out var recovery, recovering: true))
                {
                    Active = previous;
                    requested = previous.Value;
                }
                else reason += "；恢复失败：" + recovery;
            }
            Status = reason;
        }

        private bool CanSelect(Recipe recipe, out string reason)
        {
            reason = string.Empty;
            switch (recipe)
            {
                case Recipe.Legacy:
                    if (legacyWorldLook != null && legacyEnvironment != null) return true;
                    reason = "A 的旧主场配方未绑定。";
                    return false;
                case Recipe.Silksong:
                    if (silksongLayers != null && silksongLayers.shader.isSupported && silksongGrade != null &&
                        silksongGrade.shader.isSupported && heroLight != null && vignette != null && darknessShader != null &&
                        darknessShader.isSupported && ultrawideCutout != null && whiteFader != null && characters.session != null) return true;
                    reason = "B 的合成资源未绑定。";
                    return false;
                case Recipe.Ace:
                    if (aceLighting == null || legacyEnvironment == null || !characters.CanUseAceCharacterMaterial)
                    {
                        reason = "C 的光图或角色材质未绑定。";
                        return false;
                    }
                    return aceLighting.CanActivate(ownedCamera, out reason);
                default:
                    reason = "未知渲染方案。";
                    return false;
            }
        }

        private bool TryAcquire(Recipe recipe, out string reason, bool recovering = false)
        {
            reason = string.Empty;
            switch (recipe)
            {
                case Recipe.Legacy:
                    if (!legacyEnvironment.TryApply(ownedCamera, environment, sky, blue, freycia, out restoreLegacy, out reason))
                        return false;
                    return TryPublishLegacy(out reason);
                case Recipe.Silksong:
                    return true;
                case Recipe.Ace:
                    if (!aceLighting.TryActivate(ownedCamera, out reason)) return false;
                    if (!legacyEnvironment.TryApplyCharacterProfiles(ownedCamera, environment, blue, freycia,
                        out restoreAceProfiles, out reason)) return false;
                    if (!recovering) ActiveAceMaterial = requestedAceMaterial;
                    return true;
                default:
                    reason = "未知渲染方案。";
                    return false;
            }
        }

        private void ApplyAceMaterialRequest()
        {
            if (requestedAceMaterial == AceCharacterMaterial.Replica && !characters.CanUseAceCharacterMaterial)
            {
                requestedAceMaterial = ActiveAceMaterial;
                Status = "苍翼角色材质未就绪。";
                return;
            }
            ActiveAceMaterial = requestedAceMaterial;
            Status = DescribeActive();
        }

        private string DescribeActive() => Active switch
        {
            Recipe.Legacy => "A 旧主场完整渲染",
            Recipe.Silksong => "B 丝之歌渲染",
            Recipe.Ace => ActiveAceMaterial == AceCharacterMaterial.Original ? "C ACELight · 原有材质" : "C ACELight · 苍翼复刻材质",
            _ => string.Empty
        };

        private void ReleaseMode()
        {
            ReleaseLegacy();
            aceLighting?.Deactivate(ownedCamera);
            restoreAceProfiles?.Invoke();
            restoreAceProfiles = null;
        }

        private bool TryPublishLegacy(out string reason)
        {
            bool exterior = LegacyObserver(out var position);
            return legacyWorldLook.TryPublish(this, ownedCamera, position, exterior, out reason);
        }

        internal bool TryGetLegacyExteriorObserver(out Vector3 observer, out BoxCollider[] rainZones)
        {
            observer = default;
            rainZones = legacyRainZones;
            return Active == Recipe.Legacy && LegacyObserver(out observer);
        }

        private bool LegacyObserver(out Vector3 position)
        {
            var actor = characters.session == null ? null : characters.session.Current;
            position = actor == null ? ownedCamera.transform.position : PresentedPosition(actor);
            foreach (var region in legacyInteriorRegions)
                if (region.Contains(position)) return false;
            return true;
        }

        private static Vector3 PresentedPosition(SideViewActor actor)
        {
            var movement = actor.GetComponent<SideViewMovement>();
            return actor.transform.position + (movement == null ? Vector3.zero : movement.PresentationOffset);
        }

        internal bool TryGetHeroLightRect(out Vector4 viewport)
            => TryGetHeroRect(heroLightOffset, heroLightSize, 0.0312f, out viewport);

        internal bool TryGetVignetteRect(float scale, out Vector4 viewport)
            => TryGetHeroRect(new Vector2(0, -.58f), new Vector2(40.005868f, 26.625732f) * scale, -1, out viewport);

        internal bool TryGetHeroPosition(out Vector3 position)
        {
            position = default;
            var actor = characters.session.Current;
            if (actor == null) return false;
            position = PresentedPosition(actor);
            return true;
        }

        private bool TryGetHeroRect(Vector2 offset, Vector2 size, float depth, out Vector4 viewport)
        {
            viewport = default;
            if (!TryGetHeroPosition(out var position)) return false;
            Vector3 center = position + new Vector3(offset.x, offset.y, depth);
            Vector3 halfSize = (Vector3)size * .5f;
            var low = ownedCamera.WorldToViewportPoint(center - halfSize);
            var high = ownedCamera.WorldToViewportPoint(center + halfSize);
            if (low.z <= 0 || high.z <= 0) return false;
            viewport = new Vector4(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y),
                Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
            return viewport.z > 0 && viewport.w > 0;
        }

        internal Color HeroTint(SideViewActor actor) => heroTints.TryGetValue(actor, out var state) ? state.color : Color.white;

        private void UpdateHeroTints()
        {
            foreach (var actor in characters.actors)
            {
                if (actor == null) continue;
                if (!heroTints.TryGetValue(actor, out var state)) state = new TintState { color = Color.white, region = -1 };
                Vector2 position = PresentedPosition(actor);
                int region = -1;
                // 序列中后列区域的优先级更高。退出全部作者区域时回到原作的白色。
                for (int i = 0; i < tintRegions.Length; i++)
                    if (tintRegions[i].worldArea.Contains(position)) region = i;
                if (region != state.region)
                {
                    state.seconds = region >= 0 ? tintRegions[region].seconds : state.seconds;
                    state.from = state.color;
                    state.target = region >= 0 ? tintRegions[region].gammaTint : Color.white;
                    state.region = region;
                    state.start = Time.unscaledTime;
                }
                if (state.seconds > 0)
                    state.color = Color.Lerp(state.from, state.target, (Time.unscaledTime - state.start) / state.seconds);
                else if (state.region >= 0 || state.target == Color.white)
                    state.color = state.target;
                heroTints[actor] = state;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != ownedCamera || !inRender) return;
            var cmd = CommandBufferPool.Get("Side view comparison camera cleanup");
            cmd.SetGlobalFloat(ModeId, 0);
            cmd.SetGlobalFloat(SplitId, 0);
            cmd.SetGlobalFloat(LegacyCoverageId, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            if (Active == Recipe.Ace && aceLighting.LastLightMapFrame != Time.frameCount) aceRenderFailed = true;
            RestoreCameraSampling();
            inRender = false;
        }

        private void RestoreCameraSampling()
        {
            if (!silksongMsaaLease) return;
            if (ownedCamera != null) ownedCamera.allowMSAA = savedCameraMsaa;
            silksongMsaaLease = false;
        }

        private void ReleaseLegacy()
        {
            legacyWorldLook?.Release(this, ownedCamera);
            restoreLegacy?.Invoke();
            restoreLegacy = null;
        }

        private void ReleaseCards()
        {
            foreach (var pair in assignments)
                if (pair.Key != null && pair.Key.sharedMaterial == pair.Value.owned) pair.Key.sharedMaterial = pair.Value.source;
            assignments.Clear();
            foreach (var variant in variants.Values) Destroy(variant);
            variants.Clear();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            ReleaseMode();
            ReleaseCards();
            if (ownedCamera != null) Released?.Invoke(ownedCamera.GetInstanceID());
            heroTints.Clear();
            Active = null;
            aceRenderFailed = false;
            characters?.PublishMaterialStrategy(false);
            RestoreCameraSampling();
            if (aceCharacterControls != null) aceCharacterControls.SetActive(false);
            if (!inRender) return;
            Shader.SetGlobalFloat(ModeId, 0);
            Shader.SetGlobalFloat(SplitId, 0);
            Shader.SetGlobalFloat(LegacyCoverageId, 0);
            inRender = false;
        }

        private struct TintState
        {
            public Color color, from, target;
            public int region;
            public float seconds, start;
        }
    }
}
