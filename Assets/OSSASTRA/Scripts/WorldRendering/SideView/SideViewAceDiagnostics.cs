/* 功能：在 C 方案的折叠区调节代表灯，预览当前相机的光图与角色覆盖。
 * 边界：仅拥有折叠区需要的预览副本，原始光图仍由相机管理。 */
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Ossastra.Characters.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    [DisallowMultipleComponent]
    public sealed class SideViewAceDiagnostics : MonoBehaviour
    {
        [SerializeField] private SideViewAceLight representative;
        [SerializeField] private GameObject foldout;
        [SerializeField] private Button foldButton;
        [SerializeField] private Toggle lightEnabled;
        [SerializeField] private Slider intensity;
        [SerializeField] private SideViewAceLighting lighting;
        [SerializeField] private RawImage preview;
        [SerializeField] private SideViewCharacterRendering characters;
        [SerializeField] private Text innerLabel, outerLabel, colorLabel;
        private static readonly int PreviewSource = Shader.PropertyToID("_AcePreviewSource");
        private static readonly int PreviewDirection = Shader.PropertyToID("_AcePreviewDirection");
        private Material previewMaterial;
        private RenderTexture previewTarget;
        private int previewPass = -1;
        private int previewIndex;
        private int colorIndex;
        private bool inner = true, outer = true;

        public void ToggleInner()
        {
            inner = !inner;
            if (characters != null) characters.EnableAceInner(inner);
            if (innerLabel != null) innerLabel.text = inner ? "关闭内边光" : "开启内边光";
        }

        public void ToggleOuter()
        {
            outer = !outer;
            if (characters != null) characters.EnableAceOuter(outer);
            if (outerLabel != null) outerLabel.text = outer ? "关闭外柔光" : "开启外柔光";
        }

        public void CyclePreview()
        {
            previewIndex = (previewIndex + 1) % 3;
            RefreshPreview();
        }

        public void CycleColor()
        {
            colorIndex = (colorIndex + 1) % 4;
            var setting = colorIndex == 3 ? new Vector4(3, .55f, .55f, .55f) : new Vector4(colorIndex, 0, 0, 0);
            if (characters != null) characters.SetAceColorDiagnostic(setting);
            if (colorLabel != null) colorLabel.text = colorIndex switch
            {
                1 => "色彩诊断 · 白光",
                2 => "色彩诊断 · 原图",
                3 => "色彩诊断 · 灰光",
                _ => "色彩诊断 · 实际灯光"
            };
        }

        public void Configure(SideViewAceLight representative, GameObject foldout, Button foldButton,
            Toggle lightEnabled, Slider intensity, SideViewAceLighting lighting = null, RawImage preview = null)
        {
            bool rebind = Application.isPlaying && isActiveAndEnabled;
            if (rebind)
                OnDisable();

            this.representative = representative;
            this.foldout = foldout;
            this.foldButton = foldButton;
            this.lightEnabled = lightEnabled;
            this.intensity = intensity;
            this.lighting = lighting;
            this.preview = preview;

            if (rebind)
                OnEnable();
        }

        private void OnEnable()
        {
            SetOpen(false);
            if (preview != null)
                preview.raycastTarget = false;

            if (foldButton != null)
                foldButton.onClick.AddListener(ToggleOpen);

            if (lightEnabled != null)
                lightEnabled.onValueChanged.AddListener(SetLightEnabled);

            if (intensity != null)
            {
                intensity.wholeNumbers = false;
                intensity.minValue = 0f;
                intensity.maxValue = 4f;
                intensity.onValueChanged.AddListener(SetIntensity);
            }
        }

        private void OnDisable()
        {
            if (foldButton != null)
                foldButton.onClick.RemoveListener(ToggleOpen);

            if (lightEnabled != null)
                lightEnabled.onValueChanged.RemoveListener(SetLightEnabled);

            if (intensity != null)
                intensity.onValueChanged.RemoveListener(SetIntensity);

            SetOpen(false);
        }

        private void LateUpdate()
        {
            if (foldout != null && foldout.activeInHierarchy) RefreshPreview();
            else if (previewMaterial != null || previewTarget != null) ClearPreview();
        }

        private void ToggleOpen()
        {
            if (foldout != null && !transform.IsChildOf(foldout.transform))
                SetOpen(!foldout.activeSelf);
        }

        private void SetOpen(bool open)
        {
            if (foldout != null && !transform.IsChildOf(foldout.transform))
                foldout.SetActive(open);

            if (!open)
            {
                if (characters != null) characters.SetAceColorDiagnostic(Vector4.zero);
                colorIndex = 0;
                if (colorLabel != null) colorLabel.text = "色彩诊断 · 实际灯光";
                ClearPreview();
                return;
            }

            bool hasLight = representative != null;
            if (lightEnabled != null)
            {
                lightEnabled.interactable = hasLight;
                lightEnabled.SetIsOnWithoutNotify(hasLight && representative.enabled);
            }

            if (intensity != null)
            {
                intensity.interactable = hasLight;
                intensity.SetValueWithoutNotify(hasLight ? representative.Intensity : 0f);
            }

            RefreshPreview();
        }

        private void SetLightEnabled(bool value)
        {
            if (representative != null)
                representative.enabled = value;
        }

        private void SetIntensity(float value)
        {
            if (representative != null)
                representative.Intensity = value;
        }

        private void RefreshPreview()
        {
            if (preview == null || foldout == null || !foldout.activeInHierarchy)
            {
                ClearPreview();
                return;
            }
            if (previewIndex == 0)
            {
                ClearPreview();
                SetPreview(lighting != null ? lighting.AmbientPreview : null);
                return;
            }

            var source = previewIndex == 1 ? characters?.CoveragePreview : lighting?.EdgeDirectionPreview;
            var shader = lighting != null ? lighting.Writer : null;
            if (source == null || shader == null || !shader.isSupported)
            {
                ClearPreview();
                return;
            }
            if (previewMaterial == null)
            {
                previewMaterial = CoreUtils.CreateEngineMaterial(shader);
                previewPass = previewMaterial.FindPass("AceDebugPreview");
            }
            if (previewPass < 0) return;
            if (previewTarget == null)
            {
                previewTarget = RenderTexture.GetTemporary(160, 90, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                previewTarget.name = "ACE diagnostics preview";
                previewTarget.filterMode = FilterMode.Bilinear;
            }
            previewMaterial.SetTexture(PreviewSource, source);
            previewMaterial.SetFloat(PreviewDirection, previewIndex == 2 ? 1 : 0);
            var previous = RenderTexture.active;
            var commands = CommandBufferPool.Get("ACE diagnostic preview");
            try
            {
                commands.SetRenderTarget(previewTarget);
                commands.SetViewport(new Rect(0, 0, previewTarget.width, previewTarget.height));
                commands.DrawProcedural(Matrix4x4.identity, previewMaterial, previewPass, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                CommandBufferPool.Release(commands);
                RenderTexture.active = previous;
            }
            SetPreview(previewTarget);
        }

        private void ClearPreview()
        {
            SetPreview(null);
            if (previewTarget != null) RenderTexture.ReleaseTemporary(previewTarget);
            previewTarget = null;
            if (previewMaterial != null) CoreUtils.Destroy(previewMaterial);
            previewMaterial = null;
            previewPass = -1;
        }

        private void SetPreview(Texture texture)
        {
            if (preview == null)
                return;

            if (!ReferenceEquals(preview.texture, texture))
                preview.texture = texture;

            bool show = texture != null;
            if (preview.enabled != show)
                preview.enabled = show;
        }
    }
}
