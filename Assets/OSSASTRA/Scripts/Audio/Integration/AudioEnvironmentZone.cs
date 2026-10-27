/* 脚本：AudioEnvironmentZone
 * 功能：以真实区域和当前实控位置管理一个持续环境声，进入/离开平滑淡变。
 * 边界：不拥有第二个管理者/AudioSource，不修改BGM、天气或玩法；缺声显式报告。
 * 协作：唯一音频根签发来源和声部许可；失败/被抢后节制重试，世界失效立即释放。
 */
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.Audio
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AudioEnvironmentZone : MonoBehaviour
    {
        [Header("区域声明；BoxCollider定义真实空间")]
        [SerializeField] private string zoneId = "environment";
        [SerializeField] private bool indoors;
        [SerializeField] private string eventId = "ambience.rain";
        [SerializeField, Min(0)] private float transitionSeconds = .5f;
        [SerializeField, Min(.1f)] private float retrySeconds = .5f;
        private BoxCollider volume;
        private CharacterControlCoordinator coordinator;
        private GameAudioRoot audioRoot;
        private SfxSourceHandle source;
        private string world;
        private long voiceId, sequence;
        private float gain, nextRetry, nextDiscovery;
        public string ZoneId => zoneId;
        public bool Indoors => indoors;
        public bool IsInside { get; private set; }
        public int AcceptedStarts { get; private set; }
        public int RetryCount { get; private set; }
        public int ActiveVoiceCount => IsVoiceLive() ? 1 : 0;
        public string Status { get; private set; } = "等待区域事实";

        public bool Contains(Vector3 point)
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            return volume != null && volume.enabled && volume.gameObject.activeInHierarchy && volume.bounds.Contains(point);
        }

        private void LateUpdate()
        {
            var root = GameAudioRoot.Instance;
            if (!Application.isPlaying || root == null || !root.SfxAvailable || !root.WorldAudioEnabled)
            { Release(); IsInside = false; Status = "当前内容档案未启用世界声音"; return; }
            // 关闭环境音必须真正归还持续声部，不能只静音却永久占用预算；脚步和UI不受此开关影响。
            if (root.UnifiedMix.GetBusVolume(SfxBus.Ambience) <= 0)
            { Release(); IsInside = false; Status = "环境声已关闭（声音设置）"; return; }
            if (audioRoot != root || world != root.WorldScopeId || source != null && source.IsDisposed)
            { Release(); audioRoot = root; world = root.WorldScopeId; nextRetry = 0; }
            if (coordinator == null || Time.unscaledTime >= nextDiscovery) DiscoverListener();
            var listener = coordinator == null ? null : coordinator.Current;
            IsInside = listener != null && listener.isActiveAndEnabled && listener.IsAlive && Contains(listener.transform.position);
            bool live = IsVoiceLive();
            if (voiceId != 0 && !live)
            {
                voiceId = 0; gain = 0; nextRetry = Time.unscaledTime + Mathf.Max(.1f, retrySeconds);
                Status = "持续声许可已结束；等待重试";
            }
            if (IsInside && !live && Time.unscaledTime >= nextRetry)
            {
                StartVoice(); live = IsVoiceLive();
            }
            if (!live) return;
            float target = IsInside ? 1 : 0;
            gain = transitionSeconds <= 0 ? target : Mathf.MoveTowards(gain, target, Time.unscaledDeltaTime / transitionSeconds);
            if (!audioRoot.SfxPlayer.SetVoice(voiceId, volume.bounds.center, gain))
            { source?.StopVoice(voiceId); voiceId = 0; nextRetry = Time.unscaledTime + Mathf.Max(.1f, retrySeconds); return; }
            if (!IsInside && gain <= 0)
            { source.StopVoice(voiceId); voiceId = 0; source.Dispose(); source = null; Status = "已离开区域并释放"; }
            else Status = IsInside ? "区域环境声活动" : "离开区域淡出";
        }

        private void StartVoice()
        {
            nextRetry = Time.unscaledTime + Mathf.Max(.1f, retrySeconds); RetryCount++;
            if (audioRoot.SfxCatalog == null || !audioRoot.SfxCatalog.TryResolve(eventId, out var definition, out var reason))
            { Status = "MISSING_SEMANTIC: " + eventId; return; }
            if (!definition.loop) { Status = "环境定义不是持续声: " + eventId; return; }
            if (source == null || source.IsDisposed) source = audioRoot.RegisterSfxSource("environment:" + zoneId + "@" + GetInstanceID());
            if (source == null) { Status = "区域来源不可用"; return; }
            var decision = source.Submit(eventId, "loop:" + (++sequence), volume.bounds.center);
            if (!decision.Accepted || !audioRoot.Sfx.TryGetVoice(decision.VoiceId, out _))
            { Status = decision.Reason; return; }
            voiceId = decision.VoiceId; gain = transitionSeconds <= 0 ? 1 : 0; AcceptedStarts++;
            audioRoot.SfxPlayer.SetVoice(voiceId, volume.bounds.center, gain);
        }

        private bool IsVoiceLive() => audioRoot != null && source != null && !source.IsDisposed && voiceId != 0 &&
            audioRoot.Sfx != null && audioRoot.Sfx.TryGetVoice(voiceId, out var voice) &&
            voice.Request.SourceId == source.SourceId && voice.Request.SourceGeneration == source.Generation;

        private void DiscoverListener()
        {
            coordinator = null; nextDiscovery = Time.unscaledTime + .5f;
            foreach (var candidate in FindObjectsOfType<CharacterControlCoordinator>())
                if (candidate.isActiveAndEnabled && candidate.gameObject.scene == gameObject.scene)
                { coordinator = candidate; break; }
        }

        private void Release()
        {
            source?.Dispose(); source = null; voiceId = 0; gain = 0; world = null; audioRoot = null;
        }
        private void OnDisable() { Release(); IsInside = false; }
        private void OnDestroy() => Release();
    }
}
