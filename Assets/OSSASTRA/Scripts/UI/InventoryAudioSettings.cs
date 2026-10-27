/* 脚本：InventoryAudioSettings（InventoryView的声音菜单部分）
 * 功能：复用现有窗口、字体与细边，实时调节唯一音频根的主音量、音乐、音效和可选环境层。
 * 边界：不创建播放器、不写Mixer、不暂停世界；只在明确调节结束、推荐按钮或关闭时保存用户设置。
 * 协作：仍由原menuPanel拥有输入捕获；返回、Esc、失焦和禁用共用原菜单退出路径，无第二套按键。
 */
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ossastra.UserInterface
{
    public sealed partial class InventoryView
    {
        private RectTransform audioSettingsPanel;
        private UnityEngine.UI.Text audioSettingsNote;
        private UnityEngine.UI.Text audioMuteLabel;
        private UnityEngine.UI.Button audioMuteButton, audioRecommendedButton, audioReturnButton;
        private readonly UnityEngine.UI.Slider[] audioVolumeSliders = new UnityEngine.UI.Slider[4];
        private readonly UnityEngine.UI.Text[] audioVolumeValues = new UnityEngine.UI.Text[4];
        private bool audioSettingsChanged, audioSettingsRestoreInventoryToggle;

        public bool AudioSettingsOpen => audioSettingsPanel != null && audioSettingsPanel.gameObject.activeSelf;

        /// <summary>入口仅来自已经打开的系统菜单；首次创建后复用，不另占一份输入捕获。</summary>
        public void OpenAudioSettings()
        {
            if (menuPanel == null || !menuPanel.gameObject.activeSelf || windows == null || AudioSettingsOpen) return;
            if (audioSettingsPanel == null) BuildAudioSettings();
            RefreshAudioSettings();
            audioSettingsPanel.gameObject.SetActive(true);
            audioSettingsPanel.SetAsLastSibling();
            // 背包Action属于本View的克隆；设置期间不让Tab打开另一库存表面，退出恢复原启用状态。
            audioSettingsRestoreInventoryToggle = toggleAction.enabled;
            if (audioSettingsRestoreInventoryToggle) toggleAction.Disable();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(audioVolumeSliders[0].interactable
                    ? audioVolumeSliders[0].gameObject : audioReturnButton.gameObject);
        }

        public void CloseAudioSettings(bool returnToMenu = true)
        {
            SaveAudioSettings();
            if (audioSettingsPanel != null) audioSettingsPanel.gameObject.SetActive(false);
            if (audioSettingsRestoreInventoryToggle && isActiveAndEnabled) toggleAction.Enable();
            audioSettingsRestoreInventoryToggle = false;
            if (returnToMenu && menuPanel != null && menuPanel.gameObject.activeSelf && EventSystem.current != null)
            {
                var entry = menuPanel.Find("AudioSettings");
                if (entry != null) EventSystem.current.SetSelectedGameObject(entry.gameObject);
            }
        }

        private void BuildAudioSettings()
        {
            audioSettingsPanel = Window("AudioSettingsPanel", new Rect(467, 110, 500, 500));
            Header(audioSettingsPanel, "声音设置", 500, () => CloseAudioSettings());
            audioSettingsNote = Label(audioSettingsPanel, "Description", new Rect(20, 54, 460, 44),
                "环境声为可选层，默认关闭，可单独开启。\n调节即时生效，结束调节或关闭时保存。", 13, gold);
            var labels = new[] { "总音量", "背景音乐", "音效", "环境声" };
            var names = new[] { "MasterVolume", "BgmVolume", "SfxVolume", "AmbienceVolume" };
            for (int i = 0; i < audioVolumeSliders.Length; i++)
            {
                int index = i;
                var row = Node(audioSettingsPanel, names[i] + "Row", new Rect(20, 112 + i * 56, 460, 44));
                Label(row, "Name", new Rect(0, 0, 120, 34), labels[i], 15, null, TextAnchor.MiddleLeft);
                audioVolumeValues[i] = Label(row, "Value", new Rect(410, 0, 50, 34), "0%", 14, gold, TextAnchor.MiddleRight);
                var slider = AudioSlider(row, names[i], new Rect(135, 0, 260, 34));
                slider.onValueChanged.AddListener(value => ChangeAudioValue(index, value));
                slider.interactionFinished = SaveAudioSettings;
                audioVolumeSliders[i] = slider;
            }
            audioMuteButton = Button(audioSettingsPanel, "MuteToggle", new Rect(20, 336, 460, 36), "", ToggleAudioMute);
            audioMuteLabel = audioMuteButton.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
            audioRecommendedButton = Button(audioSettingsPanel, "RecommendedAudio", new Rect(20, 394, 222, 36), "使用推荐音量", ApplyRecommendedAudio);
            audioReturnButton = Button(audioSettingsPanel, "ReturnToMenu", new Rect(258, 394, 222, 36), "返回菜单", () => CloseAudioSettings());
            Label(audioSettingsPanel, "Controls", new Rect(20, 446, 460, 40),
                "方向键 / 手柄：上下选择，左右调节。\nEsc / Select 关闭菜单；游戏继续运行。", 12, gold);
            ConfigureAudioNavigation();
            audioSettingsPanel.gameObject.SetActive(false);
        }

        private InventoryAudioSlider AudioSlider(Transform parent, string name, Rect rect)
        {
            var area = Image(parent, name, rect, new Color(0, 0, 0, .01f), null, true);
            var background = Image(area.transform, "Background", new Rect(0, 12, rect.width, 10), new Color32(44, 20, 30, 255), skin.slotFill);
            Image(background.transform, "Top", new Rect(0, 0, rect.width, 1), edge);
            Image(background.transform, "Bottom", new Rect(0, 9, rect.width, 1), edge);

            var fillArea = Node(area.transform, "FillArea", new Rect(7, 12, rect.width - 14, 10));
            var fill = Image(fillArea, "Fill", new Rect(0, 0, rect.width - 14, 10), edge, skin.slotFill);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Node(area.transform, "HandleArea", new Rect(7, 0, rect.width - 14, rect.height));
            var handle = Image(handleArea, "Handle", new Rect(0, 0, 14, 26), Color.white, skin.slotFill);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            var slider = area.gameObject.AddComponent<InventoryAudioSlider>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            var colors = slider.colors;
            colors.normalColor = new Color32(210, 190, 128, 255);
            colors.highlightedColor = colors.selectedColor = new Color32(255, 242, 164, 255);
            colors.pressedColor = Color.white;
            slider.colors = colors;
            return slider;
        }

        private void ConfigureAudioNavigation()
        {
            var sequence = new UnityEngine.UI.Selectable[]
            { audioVolumeSliders[0], audioVolumeSliders[1], audioVolumeSliders[2], audioVolumeSliders[3],
                audioMuteButton, audioRecommendedButton, audioReturnButton };
            for (int i = 0; i < sequence.Length; i++)
            {
                var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit };
                navigation.selectOnUp = sequence[(i + sequence.Length - 1) % sequence.Length];
                navigation.selectOnDown = sequence[(i + 1) % sequence.Length];
                if (sequence[i] == audioRecommendedButton) navigation.selectOnRight = audioReturnButton;
                if (sequence[i] == audioReturnButton) navigation.selectOnLeft = audioRecommendedButton;
                sequence[i].navigation = navigation;
            }
        }

        private Ossastra.Audio.GameAudioMixService AudioMix()
        {
            var root = Ossastra.Audio.GameAudioRoot.Instance;
            return root == null || root.UnifiedMix == null || !root.UnifiedMix.IsConfigured ? null : root.UnifiedMix;
        }

        private void RefreshAudioSettings()
        {
            var mix = AudioMix();
            for (int i = 0; i < audioVolumeSliders.Length; i++) audioVolumeSliders[i].interactable = mix != null;
            audioMuteButton.interactable = audioRecommendedButton.interactable = mix != null;
            if (mix == null)
            {
                audioSettingsNote.text = "音频尚未就绪，可返回菜单后重试。";
                audioMuteLabel.text = "静音：不可用";
                return;
            }
            audioSettingsNote.text = "环境声为可选层，默认关闭，可单独开启。\n调节即时生效，结束调节或关闭时保存。";
            var values = new[] { mix.MasterVolume, mix.BgmVolume, mix.SfxVolume, mix.GetBusVolume(Ossastra.Audio.SfxBus.Ambience) };
            for (int i = 0; i < values.Length; i++)
            {
                audioVolumeSliders[i].SetValueWithoutNotify(values[i]);
                audioVolumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%";
            }
            audioMuteLabel.text = mix.Muted ? "静音：开启（点击关闭）" : "静音：关闭（点击开启）";
        }

        private void ChangeAudioValue(int index, float value)
        {
            var mix = AudioMix();
            if (mix == null) return;
            if (index == 0) mix.MasterVolume = value;
            else if (index == 1) mix.BgmVolume = value;
            else if (index == 2) mix.SfxVolume = value;
            else mix.SetBusVolume(Ossastra.Audio.SfxBus.Ambience, value);
            audioVolumeValues[index].text = Mathf.RoundToInt(value * 100) + "%";
            audioSettingsChanged = true;
        }

        private void ToggleAudioMute()
        {
            var mix = AudioMix();
            if (mix == null) return;
            mix.Muted = !mix.Muted;
            audioSettingsChanged = true;
            RefreshAudioSettings(); SaveAudioSettings();
        }

        private void ApplyRecommendedAudio()
        {
            var mix = AudioMix();
            if (mix == null) return;
            // 明确操作只改这四项；其他声音总线和用户静音选择保持原值。
            mix.MasterVolume = 1; mix.BgmVolume = .65f; mix.SfxVolume = 1;
            mix.SetBusVolume(Ossastra.Audio.SfxBus.Ambience, 0);
            audioSettingsChanged = true;
            RefreshAudioSettings(); SaveAudioSettings();
        }

        private void SaveAudioSettings()
        {
            if (!audioSettingsChanged) return;
            var root = Ossastra.Audio.GameAudioRoot.Instance;
            if (root != null && root.Mix != null) root.SaveUserSettings();
            audioSettingsChanged = false;
        }
    }

    /// <summary>沿用uGUI滑条的真实拖动/导航，只在松手、失选或导航停止后提交持久化。</summary>
    public sealed class InventoryAudioSlider : UnityEngine.UI.Slider
    {
        public Action interactionFinished;
        private bool navigationPending;
        private float navigationFinishedAt;

        public override void OnPointerUp(PointerEventData eventData)
        { base.OnPointerUp(eventData); FinishInteraction(); }

        public override void OnDeselect(BaseEventData eventData)
        { base.OnDeselect(eventData); FinishInteraction(); }

        public override void OnMove(AxisEventData eventData)
        {
            float before = value;
            base.OnMove(eventData);
            if (!Mathf.Approximately(before, value))
            { navigationPending = true; navigationFinishedAt = Time.unscaledTime + .25f; }
        }

        protected override void Update()
        {
            base.Update();
            if (navigationPending && Time.unscaledTime >= navigationFinishedAt) FinishInteraction();
        }

        protected override void OnDisable()
        { FinishInteraction(); base.OnDisable(); }

        private void FinishInteraction()
        { navigationPending = false; interactionFinished?.Invoke(); }
    }
}
