/*
 * 脚本：GameAudioMixService
 * 功能：唯一写入统一Mixer的Master、BGM、SFX及各短音分组用户增益。
 * 边界：不选曲、不播放声音、不创建快照；旧BgmMixController仍唯一持有Master/BGM/mute设置。
 * 协作：GameAudioRoot在显式Tick应用参数，监控只改用户设置；对话duck始终留在BGM局部增益。
 * 关键约束：每个用户因子只出现于一个Mixer组；0映射-80dB的Mixer静音下限，绝不Log10(0)。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Ossastra.Audio
{
    public sealed class GameAudioMixService
    {
        private static readonly string[] busParameters =
            { "PlayerGain", "CompanionGain", "EnemyGain", "UIGain", "InteractionGain", "VoiceGain", "AmbienceGain" };
        private readonly BgmMixController bgm;
        private readonly float[] busVolumes = { 1, 1, 1, 1, 1, 1, 0 };
        private readonly Dictionary<string, float> applied = new Dictionary<string, float>(StringComparer.Ordinal);
        private float sfxVolume = 1;

        public AudioMixer Mixer { get; }
        public bool IsConfigured { get; private set; }
        public bool RoutingReady => IsConfigured;
        public string LastError { get; private set; } = string.Empty;
        public float SfxVolume { get => sfxVolume; set => sfxVolume = Clamp(value); }
        public float MasterVolume { get => bgm.MasterVolume; set => bgm.MasterVolume = value; }
        public float BgmVolume { get => bgm.UserBgmVolume; set => bgm.UserBgmVolume = value; }
        public bool Muted { get => bgm.Muted; set => bgm.Muted = value; }

        public GameAudioMixService(BgmMixController bgm, AudioMixer mixer)
        {
            this.bgm = bgm ?? throw new ArgumentNullException(nameof(bgm));
            Mixer = mixer;
            if (Mixer == null) { LastError = "MIXER_MISSING"; return; }
            var missing = new List<string>();
            Check("MasterGain", missing);
            Check("BgmGain", missing);
            Check("SfxGain", missing);
            foreach (string parameter in busParameters) Check(parameter, missing);
            IsConfigured = missing.Count == 0;
            if (!IsConfigured) LastError = "MIXER_PARAMETER_MISSING: " + string.Join(", ", missing);
        }

        public float GetBusVolume(SfxBus bus) => busVolumes[BusIndex(bus)];
        public void SetBusVolume(SfxBus bus, float value) => busVolumes[BusIndex(bus)] = Clamp(value);

        /// <summary>目标线性增益用于诊断；Mixer的-80dB静音下限可能仍留下极小物理残余。</summary>
        public float EffectiveSfxGain(SfxBus bus) => bgm.Muted ? 0 : bgm.MasterVolume * sfxVolume * GetBusVolume(bus);

        /// <summary>仅在Root初始化时显式读新增键；不覆盖既有Master/BGM/muted键。</summary>
        public void LoadUserSettings()
        {
            SfxVolume = PlayerPrefs.GetFloat("ossastra.audio.sfx", 1);
            for (int index = 0; index < busVolumes.Length; index++)
                // 测试场未布置实际环境发声物；持续环境层默认关闭，明确开启后的选择继续持久化。
                busVolumes[index] = Clamp(PlayerPrefs.GetFloat(BusPreference(index), index == (int)SfxBus.Ambience ? 0 : 1));
        }

        public void SaveUserSettings()
        {
            PlayerPrefs.SetFloat("ossastra.audio.sfx", sfxVolume);
            for (int index = 0; index < busVolumes.Length; index++)
                PlayerPrefs.SetFloat(BusPreference(index), busVolumes[index]);
        }

        /// <summary>统一Mixer参数的唯一写入口；变化时才写，失败不伪报配置成功。</summary>
        public bool Tick()
        {
            if (!IsConfigured || Mixer == null) return false;
            var failed = new List<string>();
            Apply("MasterGain", bgm.Muted ? 0 : bgm.MasterVolume, failed);
            Apply("BgmGain", bgm.UserBgmVolume, failed);
            Apply("SfxGain", sfxVolume, failed);
            for (int index = 0; index < busVolumes.Length; index++) Apply(busParameters[index], busVolumes[index], failed);
            LastError = failed.Count == 0 ? string.Empty : "MIXER_WRITE_FAILED: " + string.Join(", ", failed);
            return failed.Count == 0;
        }

        public static float LinearToDecibels(float linear)
        {
            float safe = Clamp(linear);
            return safe <= .0001f ? -80 : Mathf.Clamp(20 * Mathf.Log10(safe), -80, 0);
        }

        private void Check(string parameter, List<string> missing)
        {
            if (!Mixer.GetFloat(parameter, out _)) missing.Add(parameter);
        }

        private void Apply(string parameter, float linear, List<string> failed)
        {
            float decibels = LinearToDecibels(linear);
            if (applied.TryGetValue(parameter, out float previous) && Mathf.Abs(previous - decibels) < .00001f) return;
            if (Mixer.SetFloat(parameter, decibels)) applied[parameter] = decibels;
            else failed.Add(parameter);
        }

        private static int BusIndex(SfxBus bus)
        {
            int index = (int)bus;
            if (index < 0 || index >= busParameters.Length) throw new ArgumentOutOfRangeException(nameof(bus));
            return index;
        }

        private static string BusPreference(int index) => "ossastra.audio.sfx." + ((SfxBus)index).ToString().ToLowerInvariant();
        private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
    }
}
