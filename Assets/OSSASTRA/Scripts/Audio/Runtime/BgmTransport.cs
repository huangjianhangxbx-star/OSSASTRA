/*
 * 脚本：BgmTransport
 * 职责：独占两组固定音乐音源，以内容身份管理起播、DSP时基、显式淡变和诊断分层。
 * 边界：不选择玩法曲目，不写角色状态；加载只报告可用性，启动权限属于当前请求代次。
 * 协作：GameAudioRoot显式Request/Tick；本类独写AudioSource局部增益，统一Mixer独写用户Master/BGM因子。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Ossastra.Audio
{
    public sealed class BgmTransport : IDisposable
    {
        private sealed class Bank
        {
            public readonly AudioSource[] sources = new AudioSource[3];
            public readonly List<AudioClip> ownedSegments = new List<AudioClip>();
            public BgmTrackSO track;
            public string key = "";
            public string transportId = "";
            public double epoch;
            public float gain;
            public float target;
            public float intensity;
            public float pendingIntensity;
            public double intensityAt = double.PositiveInfinity;
            public int layers;
        }

        private readonly GameObject ownedRoot;
        private readonly Bank[] banks = { new Bank(), new Bank() };
        private readonly HashSet<int> loadRequests = new HashSet<int>();
        private readonly float fadeSeconds;
        private BgmTrackSO desiredTrack;
        private string desiredKey = "";
        private string pendingKey = "";
        private int current = -1;
        private float requestedIntensity;
        private double lastDspTime;
        private bool disposed;
        private bool stopped;
        private string status = "Ready";
        private string lastError = "";
        private string rejectedPreparationKey = "";
        private string rejectedPreparationError = "";
        private string rejectedPreparationStatus = "";
        private int generation;
        private int loadingGeneration;
        private int stopGeneration;
        private int startCount;
        private int sourceStartCount;
        private int loadRequestCount;
        private int scheduledTransition = -1;
        private double transitionAt;

        public BgmTransport(GameObject ownedParent, AudioMixerGroup output, float fadeSeconds)
        {
            if (ownedParent == null) throw new ArgumentNullException(nameof(ownedParent));
            this.fadeSeconds = Mathf.Max(0, Finite(fadeSeconds) ? fadeSeconds : 0);
            BackendId = Guid.NewGuid().ToString("N");
            ownedRoot = new GameObject("BGM Transport [Owned]");
            ownedRoot.transform.SetParent(ownedParent.transform, false);
            for (int bankIndex = 0; bankIndex < banks.Length; bankIndex++)
            {
                for (int layer = 0; layer < 3; layer++)
                {
                    var child = new GameObject("Bank " + bankIndex + " Layer " + layer);
                    child.transform.SetParent(ownedRoot.transform, false);
                    var source = child.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0;
                    source.dopplerLevel = 0;
                    source.pitch = 1;
                    source.volume = 0;
                    source.ignoreListenerPause = true;
                    source.outputAudioMixerGroup = output;
                    banks[bankIndex].sources[layer] = source;
                }
            }
        }

        public string BackendId { get; }
        public string TransportId => current < 0 ? "" : banks[current].transportId;
        public string CurrentKey => current < 0 ? "" : banks[current].key;
        public BgmTrackSO CurrentTrack => current < 0 ? null : banks[current].track;
        public double Epoch => current < 0 ? 0 : banks[current].epoch;
        public double Elapsed => current < 0 ? 0 : Math.Max(0, lastDspTime - Epoch);
        public string PendingKey => pendingKey;
        public string ScheduledKey => scheduledTransition < 0 ? "" : banks[scheduledTransition].key;
        public double ScheduledEpoch => scheduledTransition < 0 ? 0 : transitionAt;
        public string FadingKey
        {
            get
            {
                for (int i = 0; i < banks.Length; i++)
                    if (i != current && banks[i].track != null) return banks[i].key;
                return "";
            }
        }
        public string Status => status;
        public string LastError => lastError;
        public int StartCount => startCount;
        public int SourceStartCount => sourceStartCount;
        public int SourceCount => disposed ? 0 : 6;
        public int Generation => generation;
        public int LoadingGeneration => loadingGeneration;
        public int StopGeneration => stopGeneration;
        public int LoadRequestCount => loadRequestCount;
        public bool IsStopped => stopped;
        public bool IsDisposed => disposed;
        public float[] BankGains => new[] { banks[0].gain, banks[1].gain };
        public float[] LayerGains
        {
            get
            {
                var result = new float[6];
                for (int b = 0; b < 2; b++) for (int l = 0; l < 3; l++)
                    result[b * 3 + l] = banks[b].sources[l] == null ? 0 : banks[b].sources[l].volume;
                return result;
            }
        }

        public void Request(BgmTrackSO track, double dspNow)
        {
            if (disposed || stopped) return;
            if (!Finite(dspNow)) { lastError = "INVALID_DSP_TIME"; return; }
            lastDspTime = dspNow;
            if (track == null) { InvalidateTarget(); return; }
            if (!track.TryGetIdentity(out var identity, out var error))
            {
                InvalidateTarget(); lastError = error; status = "INVALID_CONTENT_IDENTITY"; return;
            }
            string key = track.PlaybackKey;
            // 规则和SO实例变化不影响同一播放身份；等待同一资源也不重置加载代次。
            if (desiredKey != key)
            {
                generation++;
                desiredTrack = track;
                desiredKey = key;
                pendingKey = key;
                loadingGeneration = generation;
                loadRequests.Clear();
                rejectedPreparationKey = "";
            }
            else desiredTrack = track;
            ResolvePending(dspNow);
        }

        public void Tick(double dspNow, float delta, float finalGain)
        {
            if (disposed || stopped) return;
            if (!Finite(dspNow)) return;
            lastDspTime = dspNow;
            if (desiredTrack != null) ResolvePending(dspNow);
            if (scheduledTransition >= 0 && dspNow >= transitionAt)
            {
                for (int i = 0; i < banks.Length; i++) banks[i].target = i == scheduledTransition ? 1 : 0;
                scheduledTransition = -1;
            }
            float step = fadeSeconds <= 0 ? 1 : Mathf.Max(0, Finite(delta) ? delta : 0) / fadeSeconds;
            float gain = Mathf.Max(0, Finite(finalGain) ? finalGain : 0);
            for (int b = 0; b < banks.Length; b++)
            {
                var bank = banks[b];
                if (bank.track == null) continue;
                if (dspNow >= bank.intensityAt)
                {
                    bank.intensity = bank.pendingIntensity;
                    bank.intensityAt = double.PositiveInfinity;
                }
                bank.gain = Mathf.MoveTowards(bank.gain, bank.target, step);
                for (int layer = 0; layer < 3; layer++)
                {
                    float level = bank.layers == 1 ? (layer == 0 ? .42f : 0) :
                        layer == 0 ? .12f : layer == 1 && bank.intensity > .2f ? .085f :
                        layer == 2 && bank.intensity > .7f ? .065f : 0;
                    bank.sources[layer].volume = Mathf.Clamp01(gain * bank.gain * level);
                }
                if (b != current && bank.target == 0 && bank.gain <= 0) ClearBank(bank);
            }
        }

        public void SetIntensity(float value, double dspNow)
        {
            if (disposed || stopped || !Finite(value) || !Finite(dspNow)) return;
            requestedIntensity = Mathf.Clamp01(value);
            foreach (var bank in banks)
            {
                if (bank.track == null || bank.layers != 3) continue;
                if (bank.pendingIntensity == requestedIntensity && !double.IsPositiveInfinity(bank.intensityAt)) continue;
                if (bank.intensity == requestedIntensity && double.IsPositiveInfinity(bank.intensityAt)) continue;
                bank.pendingIntensity = requestedIntensity;
                // 只有作者确认节拍的诊断分层才预约下一小节；整曲不凭空推断BPM。
                double bar = 60d / bank.track.knownBpm * Math.Max(1, bank.track.beatsPerBar);
                bank.intensityAt = dspNow < bank.epoch ? bank.epoch :
                    bank.epoch + (Math.Floor((dspNow - bank.epoch) / bar) + 1) * bar;
            }
        }

        public void InvalidateTarget(double? dspNow = null)
        {
            if (disposed) return;
            // Host解绑可能发生在本帧Tick之前；撤销预约必须使用调用现场的真实DSP时钟。
            if (dspNow.HasValue && Finite(dspNow.Value)) lastDspTime = Math.Max(lastDspTime, dspNow.Value);
            if (scheduledTransition >= 0 && banks[scheduledTransition].epoch <= lastDspTime)
                for (int i = 0; i < banks.Length; i++) banks[i].target = i == scheduledTransition ? 1 : 0;
            generation++;
            desiredTrack = null;
            desiredKey = pendingKey = "";
            rejectedPreparationKey = "";
            loadingGeneration = 0;
            loadRequests.Clear();
            // 世界目标失效只撤销尚未起播的预约，已经开始的音乐允许自然延续。
            for (int i = 0; i < banks.Length; i++)
                if (banks[i].track != null && banks[i].epoch > lastDspTime) ClearBank(banks[i]);
            if (current >= 0 && banks[current].track == null)
            {
                current = banks[0].track != null ? 0 : banks[1].track != null ? 1 : -1;
                if (current >= 0) banks[current].target = 1;
            }
            scheduledTransition = -1;
            if (!stopped) status = current < 0 ? "TargetInvalidated" : "TargetInvalidated_Continuing";
        }

        public void Stop()
        {
            if (disposed) return;
            InvalidateTarget();
            stopped = true;
            stopGeneration = generation;
            foreach (var bank in banks) ClearBank(bank);
            current = -1;
            scheduledTransition = -1;
            status = "Stopped";
        }

        public void Resume()
        {
            if (disposed) return;
            stopped = false;
            status = current < 0 ? "Ready" : "Playing";
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            status = "Disposed";
            DestroyOwned(ownedRoot);
        }

        private void ResolvePending(double dspNow)
        {
            if (disposed || stopped || desiredTrack == null) return;
            int token = generation;
            if (!TryAvailableTrack(desiredTrack, out var available, out var clips, out bool fallback))
            {
                pendingKey = desiredKey;
                loadingGeneration = token;
                return;
            }
            // 可用资源没有独立启动权限。Stop/失效或更新目标后，旧代次不能覆盖当前目标。
            if (token != generation || stopped || disposed || desiredTrack == null) return;
            string key = available.PlaybackKey;
            if (rejectedPreparationKey == key)
            { lastError = rejectedPreparationError; status = rejectedPreparationStatus; return; }
            int existing = FindBank(key);
            if (existing >= 0)
            {
                SelectBank(existing, dspNow, false);
                pendingKey = fallback ? desiredKey : "";
                loadingGeneration = fallback ? token : 0;
                status = fallback ? "ASSET_BLOCKED_FALLBACK" : FadingKey.Length > 0 ? "Crossfading" : "Playing";
                return;
            }
            // 当前可能是尚未起播的预约。B→C只替换该预约，不能误清另一组仍在播放的A。
            int next = ReplacementBank(dspNow);
            double epoch = ScheduledStart(available, dspNow, 1 - next);
            if (!PrepareSegments(available, clips, out var prepared, out var owned))
            {
                rejectedPreparationKey = key;
                rejectedPreparationError = lastError;
                rejectedPreparationStatus = status;
                return;
            }
            ClearBank(banks[next]);
            var bank = banks[next];
            bank.track = available;
            bank.key = key;
            bank.transportId = Guid.NewGuid().ToString("N");
            bank.epoch = epoch;
            bank.layers = prepared.Length;
            bank.intensity = requestedIntensity;
            bank.pendingIntensity = requestedIntensity;
            bank.ownedSegments.AddRange(owned);
            for (int layer = 0; layer < prepared.Length; layer++)
            {
                var source = bank.sources[layer];
                source.clip = prepared[layer];
                source.loop = available.loop;
                source.volume = 0;
                source.PlayScheduled(bank.epoch);
                sourceStartCount++;
            }
            startCount++;
            SelectBank(next, dspNow, true);
            pendingKey = fallback ? desiredKey : "";
            loadingGeneration = fallback ? token : 0;
            status = fallback ? "ASSET_BLOCKED_FALLBACK" : "Crossfading";
        }

        private bool TryAvailableTrack(BgmTrackSO requested, out BgmTrackSO available, out AudioClip[] clips, out bool fallback)
        {
            available = requested; clips = null; fallback = false;
            var visited = new HashSet<int>();
            while (available != null && visited.Add(available.GetInstanceID()))
            {
                if (!available.TryGetIdentity(out var identity, out var error))
                { lastError = error; status = "INVALID_CONTENT_IDENTITY"; return false; }
                bool hasLayers = available.synchronizedLayers != null && available.synchronizedLayers.Length > 0;
                clips = hasLayers ? available.synchronizedLayers : available.clip == null ? null : new[] { available.clip };
                if (hasLayers && (!available.diagnosticOnly || available.knownBpm <= 0 || !Finite(available.knownBpm) || clips.Length != 3))
                { lastError = "三层同步只允许已知BPM的原创诊断内容"; status = "INVALID_DIAGNOSTIC_LAYERS"; return false; }
                bool missing = clips == null || Array.Exists(clips, c => c == null);
                bool failed = false;
                bool ready = !missing;
                if (!missing)
                {
                    foreach (var clip in clips)
                    {
                        if (clip.loadState == AudioDataLoadState.Unloaded && loadRequests.Add(clip.GetInstanceID()))
                        { clip.LoadAudioData(); loadRequestCount++; }
                        if (clip.loadState == AudioDataLoadState.Failed) failed = true;
                        if (clip.loadState != AudioDataLoadState.Loaded) ready = false;
                    }
                }
                if (ready)
                {
                    if (hasLayers && !SameLayerClock(clips))
                    { lastError = "诊断分层采样率/采样长度不一致"; status = "INVALID_LAYER_CLOCK"; return false; }
                    lastError = fallback ? "ASSET_BLOCKED：本机媒体缺失，使用明确诊断回退" : "";
                    return true;
                }
                status = missing || failed ? "ASSET_BLOCKED" : "Loading";
                lastError = missing ? "ASSET_BLOCKED：本机媒体未绑定" : failed ? "ASSET_LOAD_FAILED" : "";
                if (available.missingFallback == null) return false;
                available = available.missingFallback;
                fallback = true;
            }
            if (available != null) { status = "INVALID_FALLBACK_CYCLE"; lastError = "回退链循环"; }
            return false;
        }

        private bool PrepareSegments(BgmTrackSO track, AudioClip[] input, out AudioClip[] clips, out List<AudioClip> owned)
        {
            clips = input;
            owned = new List<AudioClip>();
            if (track.loopStartSeconds == 0 && track.loopEndSeconds == 0) return true;
            clips = new AudioClip[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var source = input[i];
                int first = Mathf.RoundToInt(track.loopStartSeconds * source.frequency);
                int end = track.loopEndSeconds == 0 ? source.samples : Mathf.RoundToInt(track.loopEndSeconds * source.frequency);
                long values = (long)(end - first) * source.channels;
                if (first < 0 || end > source.samples || end <= first || values > int.MaxValue)
                { lastError = "循环段超出实际音频采样范围"; status = "INVALID_LOOP_RANGE"; ReleaseSegments(owned); return false; }
                var data = new float[(int)values];
                if (!source.GetData(data, first))
                { lastError = "自定义循环段要求可读取的已解压AudioClip"; status = "LOOP_PCM_UNAVAILABLE"; ReleaseSegments(owned); return false; }
                var segment = AudioClip.Create(source.name + " [Owned BGM Section]", end - first, source.channels, source.frequency, false);
                if (!segment.SetData(data, 0))
                { DestroyOwned(segment); ReleaseSegments(owned); lastError = "循环段PCM写入失败"; status = "LOOP_PCM_UNAVAILABLE"; return false; }
                clips[i] = segment;
                owned.Add(segment);
            }
            return true;
        }

        private static bool SameLayerClock(AudioClip[] clips)
        {
            for (int i = 1; i < clips.Length; i++)
                if (clips[i].samples != clips[0].samples || clips[i].frequency != clips[0].frequency) return false;
            return true;
        }
        private int FindBank(string key)
        {
            for (int i = 0; i < banks.Length; i++) if (banks[i].track != null && banks[i].key == key) return i;
            return -1;
        }
        private int ReplacementBank(double dspNow)
        {
            if (current < 0) return 0;
            if (banks[current].epoch > dspNow) return current;
            int other = 1 - current;
            if (banks[other].track == null) return other;
            // 两组都已起播时保留较响的一组；不能清掉.98的旧歌，只留下刚淡入.02的新歌。
            float currentOutput = 0, otherOutput = 0;
            for (int i = 0; i < 3; i++)
            {
                currentOutput += banks[current].sources[i].volume;
                otherOutput += banks[other].sources[i].volume;
            }
            if (currentOutput == 0 && otherOutput == 0)
                return banks[current].gain < banks[other].gain ? current : other;
            return currentOutput < otherOutput ? current : other;
        }
        private double ScheduledStart(BgmTrackSO track, double dspNow, int anchor)
        {
            double earliest = dspNow + .08d;
            if (track.knownBpm <= 0 || !Finite(track.knownBpm)) return earliest;
            if (banks[anchor].track == null || banks[anchor].epoch > dspNow || banks[anchor].track.knownBpm <= 0 || !Finite(banks[anchor].track.knownBpm)) return earliest;
            var previous = banks[anchor];
            double bar = 60d / previous.track.knownBpm * Math.Max(1, previous.track.beatsPerBar);
            return previous.epoch + Math.Max(0, Math.Ceiling((earliest - previous.epoch) / bar)) * bar;
        }
        private void SelectBank(int index, double dspNow, bool newlyScheduled)
        {
            if (!newlyScheduled && current == index) return;
            // 返回仍在播放的旧歌时撤销尚未到DSP起点的新歌；不能让过时预约稍后插入。
            for (int i = 0; i < banks.Length; i++)
                if (i != index && banks[i].track != null && banks[i].epoch > dspNow) ClearBank(banks[i]);
            current = index;
            if (newlyScheduled && banks[index].epoch > dspNow)
            {
                scheduledTransition = index;
                transitionAt = banks[index].epoch;
                banks[index].target = 0;
                for (int i = 0; i < banks.Length; i++)
                    if (i != index && banks[i].track != null) banks[i].target = 1;
            }
            else
            {
                scheduledTransition = -1;
                for (int i = 0; i < banks.Length; i++) banks[i].target = i == index ? 1 : 0;
            }
        }
        private static void ClearBank(Bank bank)
        {
            foreach (var source in bank.sources)
            {
                if (source == null) continue;
                source.Stop(); source.clip = null; source.volume = 0;
            }
            ReleaseSegments(bank.ownedSegments);
            bank.track = null; bank.key = bank.transportId = ""; bank.epoch = 0; bank.gain = bank.target = 0;
            bank.layers = 0; bank.intensityAt = double.PositiveInfinity;
        }
        private static void ReleaseSegments(List<AudioClip> clips)
        { foreach (var clip in clips) DestroyOwned(clip); clips.Clear(); }
        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
