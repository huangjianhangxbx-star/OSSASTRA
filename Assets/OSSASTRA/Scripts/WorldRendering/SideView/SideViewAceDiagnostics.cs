/* 功能：在 C 方案的折叠区调节一盏代表灯，并预览当前相机的场景环境光图。
 * 边界：复用对照面板的 Canvas 和输入捕获，不持有或释放光图。 */
using UnityEngine;
using UnityEngine.UI;

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
            if (foldout != null && foldout.activeSelf)
                RefreshPreview();
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
                SetPreview(null);
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

        private void RefreshPreview() => SetPreview(lighting != null ? lighting.AmbientPreview : null);

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
