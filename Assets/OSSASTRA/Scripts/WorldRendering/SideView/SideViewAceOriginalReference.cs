using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SideViewAceOriginalReference : MonoBehaviour
    {
        [SerializeField] private SideViewAceOriginalReferenceData data;
        [SerializeField] private SideViewRenderComparison comparison;
        [SerializeField] private SideViewAceLight diagnosticLight;

        private MeshFilter shape;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private int clipIndex, paletteIndex, shownMesh = -1, shownPage = -1;
        private double cursor;
        private bool visible = true, playing = true, full = true, diagnostic;
        private Vector4 channels = new Vector4(1, 1, 1, 0);

        private static readonly int Main = Shader.PropertyToID("_MainTex");
        private static readonly int Extra = Shader.PropertyToID("_Extra");
        private static readonly int Extra2 = Shader.PropertyToID("_Extra2");
        private static readonly int ExtraTexel = Shader.PropertyToID("_Extra_TexelSize");
        private static readonly int Full = Shader.PropertyToID("_AceOriginalFull");
        private static readonly int Channels = Shader.PropertyToID("_AceOriginalChannels");
        private static readonly int Ambient = Shader.PropertyToID("_AmbientScale");
        private static readonly int EmissionIntensity = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int Emission = Shader.PropertyToID("_Emission");
        private static readonly int EmissionX = Shader.PropertyToID("_EmissionX");
        private static readonly int EmissionY = Shader.PropertyToID("_EmissionY");
        private static readonly int EmissionZ = Shader.PropertyToID("_EmissionZ");
        private static readonly int EmissionA = Shader.PropertyToID("_EmissionA");

        public bool VisibleRequested => visible;
        public bool Playing => playing;
        public bool FullMaterial => full;
        public bool DiagnosticLighting => diagnostic;
        public Vector4 ChannelWeights => channels;
        public int PaletteIndex => paletteIndex;
        public int ClipIndex => clipIndex;
        public int Tick => (int)cursor;
        public int TickCount => data != null && data.ClipCount > 0 ? data.ClipAt(clipIndex).totalTicks : 1;
        public string PaletteName => data != null ? data.PaletteAt(paletteIndex).name : string.Empty;
        public string ClipLabel => data != null ? data.ClipAt(clipIndex).label : string.Empty;
        public int CurrentPage => shownPage;

        public void Configure(SideViewAceOriginalReferenceData source, SideViewRenderComparison owner, SideViewAceLight lamp)
        {
            data = source;
            comparison = owner;
            diagnosticLight = lamp;
        }

        private void Awake()
        {
            shape = GetComponent<MeshFilter>();
            surface = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RefreshVisibility();
        }

        private void BeforeCamera(ScriptableRenderContext _, Camera camera)
        {
            if (comparison != null && camera.gameObject == comparison.gameObject) RefreshVisibility();
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if (surface != null) ReleaseVisual();
            if (diagnosticLight != null) diagnosticLight.enabled = false;
        }

        private void LateUpdate()
        {
            if (!RefreshVisibility()) return;
            var clip = data.ClipAt(clipIndex);
            if (playing)
            {
                cursor += Time.deltaTime * data.TicksPerSecond;
                if (cursor >= clip.totalTicks)
                {
                    cursor = clip.loop ? cursor % clip.totalTicks : clip.totalTicks - 1;
                    if (!clip.loop) playing = false;
                }
            }
            Present(false);
        }

        public void SetVisible(bool value) { visible = value; RefreshVisibility(); }
        public void SetPlaying(bool value)
        {
            if (value && !playing && Tick >= TickCount - 1) cursor = 0;
            playing = value;
        }

        public void SetClip(int index)
        {
            if (data == null || (uint)index >= data.ClipCount) return;
            clipIndex = index;
            cursor = 0;
            if (surface != null && surface.enabled) Present(false);
        }

        public void SeekTick(int tick)
        {
            cursor = Mathf.Clamp(tick, 0, TickCount - 1);
            playing = false;
            if (surface != null && surface.enabled) Present(false);
        }

        public void SetPalette(int index)
        {
            if (data == null || (uint)index >= data.PaletteCount) return;
            paletteIndex = index;
            if (surface != null && surface.enabled) Present(true);
        }

        public void SetFullMaterial(bool value)
        {
            full = value;
            if (surface != null && surface.enabled) Present(true);
        }

        public void SetDiagnosticLighting(bool value)
        {
            diagnostic = value;
            RefreshVisibility();
        }

        public void ToggleChannel(int index)
        {
            if ((uint)index >= 3) return;
            channels[index] = 1 - channels[index];
            if (surface != null && surface.enabled) Present(true);
        }

        private bool RefreshVisibility()
        {
            bool show = visible && data != null && data.ClipCount > 0 && data.PaletteCount > 0 &&
                comparison != null && comparison.Active == SideViewRenderComparison.Recipe.Ace && isActiveAndEnabled;
            if (diagnosticLight != null) diagnosticLight.enabled = show && diagnostic;
            if (surface == null || show == surface.enabled) return show;
            if (show)
            {
                surface.enabled = true;
                Present(true);
            }
            else ReleaseVisual();
            return show;
        }

        private void ReleaseVisual()
        {
            surface.enabled = false;
            surface.SetPropertyBlock(null);
            shape.sharedMesh = null;
            shownMesh = shownPage = -1;
        }

        private void Present(bool refreshMaterial)
        {
            var clip = data.ClipAt(clipIndex);
            var frame = clip.frames[clip.FrameAt(Tick)];
            if (frame.mesh != shownMesh)
            {
                shape.sharedMesh = data.MeshAt(frame.mesh);
                shownMesh = frame.mesh;
            }
            if (frame.page == shownPage && !refreshMaterial) return;
            shownPage = frame.page;
            var page = data.PageAt(shownPage);
            var palette = data.PaletteAt(paletteIndex);
            properties.SetTexture(Main, page.main[paletteIndex]);
            properties.SetTexture(Extra, page.extra);
            properties.SetTexture(Extra2, page.extra2);
            properties.SetVector(ExtraTexel, new Vector4(1f / page.extra.width, 1f / page.extra.height,
                page.extra.width, page.extra.height));
            properties.SetFloat(Full, full ? 1 : 0);
            properties.SetVector(Channels, channels);
            properties.SetFloat(Ambient, palette.ambientScale);
            properties.SetFloat(EmissionIntensity, palette.emissionIntensity);
            properties.SetVector(Emission, (Vector4)palette.emission);
            properties.SetVector(EmissionX, (Vector4)palette.emissionX);
            properties.SetVector(EmissionY, (Vector4)palette.emissionY);
            properties.SetVector(EmissionZ, (Vector4)palette.emissionZ);
            properties.SetVector(EmissionA, (Vector4)palette.emissionA);
            surface.SetPropertyBlock(properties);
        }
    }
}

