using Ossastra.Characters.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewAceOriginalControls : MonoBehaviour
    {
        [SerializeField] private SideViewAceOriginalReference reference;
        [SerializeField] private SideViewCharacterRendering characters;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text status;
        [SerializeField] private Slider scrubber;

        private void OnDisable()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (panel == null || !panel.activeSelf || reference == null) return;
            var tick = reference.Tick;
            var limit = reference.TickCount - 1;
            if (scrubber.maxValue != limit)
            {
                scrubber.SetValueWithoutNotify(0);
                scrubber.maxValue = limit;
            }
            scrubber.SetValueWithoutNotify(tick);
            Vector4 channels = reference.ChannelWeights;
            string details = $"Es · {reference.ClipLabel} {tick + 1}/{limit + 1} · 配色 {reference.PaletteName} · {(reference.VisibleRequested ? "显示" : "隐藏")} / {(reference.Playing ? "播放" : "暂停")}";
            details += $"\n{(reference.FullMaterial ? "完整接收" : "基础受光")} · {(reference.DiagnosticLighting ? "通道诊断灯" : "主场灯")} · {(characters.UsesCalibratedAceRim ? "增强内边" : "克制内边")}";
            if (reference.FullMaterial)
                details += $" · 亮部{OnOff(channels.x)} 高亮{OnOff(channels.y)} 发光{OnOff(channels.z)}";
            if (details != status.text) status.text = details;
        }

        private static string OnOff(float value) => value > .5f ? "开" : "关";

        public void Configure(SideViewAceOriginalReference sample, SideViewCharacterRendering roles,
            GameObject foldout, Text readout, Slider playback)
        {
            reference = sample;
            characters = roles;
            panel = foldout;
            status = readout;
            scrubber = playback;
        }

        public void TogglePanel() => panel.SetActive(!panel.activeSelf);
        public void ToggleVisible() => reference.SetVisible(!reference.VisibleRequested);
        public void TogglePlayback() => reference.SetPlaying(!reference.Playing);
        public void SelectClip(int index) { reference.SetClip(index); reference.SetPlaying(true); }
        public void SelectPalette(int index) => reference.SetPalette(index);
        public void Seek(float tick) => reference.SeekTick(Mathf.RoundToInt(tick));
        public void ToggleMaterial() => reference.SetFullMaterial(!reference.FullMaterial);
        public void ToggleLighting() => reference.SetDiagnosticLighting(!reference.DiagnosticLighting);
        public void ToggleChannel(int index) => reference.ToggleChannel(index);
        public void ToggleRimPreset()
        {
            if (characters.UsesCalibratedAceRim) characters.SelectAceRimRestrained();
            else characters.SelectAceRimCalibrated();
        }
    }
}

