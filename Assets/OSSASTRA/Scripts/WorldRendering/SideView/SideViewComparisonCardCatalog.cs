/*
 * 将正式横版场景卡片登记为直接引用。编辑期绑定由配套清单生成。
 * 运行时只消费已登记的 Renderer、原材质和着色参数。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    [DisallowMultipleComponent]
    public sealed class SideViewComparisonCardCatalog : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
            private static readonly int ColorProperty = Shader.PropertyToID("_Color");
            private static readonly int RendererColor = Shader.PropertyToID("_RendererColor");
            private static readonly int SourceBlend = Shader.PropertyToID("_SrcBlend");
            private static readonly int DestinationBlend = Shader.PropertyToID("_DstBlend");
            private static readonly int Premultiply = Shader.PropertyToID("_Premultiply");
            private static readonly int BlurPixels = Shader.PropertyToID("_BlurPixels");
            private static readonly int EnvironmentRoleProperty = Shader.PropertyToID("_SideViewEnvironmentRole");
            private static readonly int MetadataRoleProperty = Shader.PropertyToID("_SideViewMetadataRole");
            private static readonly int OriginalAmbientProperty = Shader.PropertyToID("_SideViewOriginalAmbient");
            private static readonly int OriginalColorScaleProperty = Shader.PropertyToID("_SideViewOriginalColorScale");
            private static readonly int FarBackgroundProperty = Shader.PropertyToID("_SideViewFarBackground");
            private static readonly int BelowActorsProperty = Shader.PropertyToID("_SideViewBelowActors");
            private static readonly int ReplacementCoverageProperty = Shader.PropertyToID("_SideViewReplacementCoverage");

            // Player 中由场景序列化器填充，编辑器使用下面的构造函数。
#pragma warning disable 0649
            [SerializeField] private Renderer renderer;
            [SerializeField] private Material sourceMaterial;
            [SerializeField] private bool belowActors;
            [SerializeField] private bool farBackground;
            [SerializeField] private bool replacementCoverage;
            [SerializeField] private bool projectLightMap;
            [SerializeField] private float environmentRole;
            [SerializeField] private float metadataRole;
            [SerializeField] private float originalAmbient;
            [SerializeField] private Vector4 originalColorScale = Vector4.one;
            [SerializeField] private string sourceShader;
            [SerializeField] private string surfaceKind;
#pragma warning restore 0649

            public Renderer Renderer => renderer;
            public Material SourceMaterial => sourceMaterial;
            public bool BelowActors => belowActors;
            public bool FarBackground => farBackground;
            public bool ReplacementCoverage => replacementCoverage;
            public bool ProjectLightMap => projectLightMap;
            public float EnvironmentRole => environmentRole;
            public float MetadataRole => metadataRole;
            public float OriginalAmbient => originalAmbient;
            public Vector4 OriginalColorScale => originalColorScale;
            public string SourceShader => sourceShader;
            public string SurfaceKind => surfaceKind;

#if UNITY_EDITOR
            public Entry(Renderer renderer, Material sourceMaterial, bool belowActors, bool farBackground,
                bool replacementCoverage, bool projectLightMap, float environmentRole, float metadataRole,
                float originalAmbient, Vector4 originalColorScale, string sourceShader, string surfaceKind)
            {
                this.renderer = renderer;
                this.sourceMaterial = sourceMaterial;
                this.belowActors = belowActors;
                this.farBackground = farBackground;
                this.replacementCoverage = replacementCoverage;
                this.projectLightMap = projectLightMap;
                this.environmentRole = environmentRole;
                this.metadataRole = metadataRole;
                this.originalAmbient = originalAmbient;
                this.originalColorScale = originalColorScale;
                this.sourceShader = sourceShader;
                this.surfaceKind = surfaceKind;
            }
#endif

            // 调用者持有 variant 并负责共享、挂接和销毁。这里不写 Renderer 或原材质。
            public void ConfigureVariant(Material variant)
            {
                if (sourceMaterial == null || variant == null || variant == sourceMaterial ||
                    !variant.HasProperty(OriginalColorScaleProperty) || !variant.HasProperty(FarBackgroundProperty) ||
                    !variant.HasProperty(BelowActorsProperty) || !variant.HasProperty(ReplacementCoverageProperty))
                    throw new ArgumentException("对照卡片需要完整的自有材质变体和有效源材质。", nameof(variant));

                bool legacySourceSprite = sourceMaterial.shader.name == "SceneLab/SourceSprite";
                Color tint = legacySourceSprite ? Color.white : sourceMaterial.GetColor(ColorProperty);
                if (!legacySourceSprite && sourceMaterial.HasProperty(RendererColor))
                    tint *= sourceMaterial.GetColor(RendererColor);

                variant.SetTexture(MainTexture, sourceMaterial.GetTexture(MainTexture));
                variant.SetColor(ColorProperty, tint);
                variant.SetFloat(SourceBlend, legacySourceSprite ? sourceMaterial.GetFloat(SourceBlend) : (float)BlendMode.One);
                variant.SetFloat(DestinationBlend, legacySourceSprite ? sourceMaterial.GetFloat(DestinationBlend) : (float)BlendMode.OneMinusSrcAlpha);
                variant.SetFloat(Premultiply, legacySourceSprite ? sourceMaterial.GetFloat(Premultiply) : 1f);
                variant.SetFloat(BlurPixels, legacySourceSprite ? sourceMaterial.GetFloat(BlurPixels) : 0f);
                variant.SetFloat(EnvironmentRoleProperty, environmentRole);
                variant.SetFloat(MetadataRoleProperty, metadataRole);
                variant.SetFloat(OriginalAmbientProperty, originalAmbient);
                variant.SetVector(OriginalColorScaleProperty, originalColorScale);
                variant.SetFloat(FarBackgroundProperty, farBackground ? 1f : 0f);
                variant.SetFloat(BelowActorsProperty, belowActors ? 1f : 0f);
                variant.SetFloat(ReplacementCoverageProperty, replacementCoverage ? 1f : 0f);
                variant.renderQueue = sourceMaterial.renderQueue;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public void SetAuthoringEntries(Entry[] value) => entries = value ?? throw new ArgumentNullException(nameof(value));
#endif
    }
}
