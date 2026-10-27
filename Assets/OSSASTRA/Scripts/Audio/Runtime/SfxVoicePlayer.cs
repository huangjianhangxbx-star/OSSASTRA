/*
 * 脚本：SfxVoicePlayer
 * 功能：在唯一GameAudioRoot下预建有限短音池，消费仍有效的仲裁许可并显式回收。
 * 边界：没有绕过仲裁的Clip播放接口，不决定命中、动作或BGM；不创建独立全局宿主。
 * 协作：SfxArbitrator拥有许可，Root传入总线路由并推进Tick；来源/世界停止只撤销对应短音。
 * 关键约束：每次播放不创建AudioSource；抢占立即归还旧声部，释放重复回调幂等。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Ossastra.Audio
{
    public sealed class SfxVoicePlayer : IDisposable
    {
        public sealed class VoiceState
        {
            public long VoiceId { get; internal set; }
            public AudioSource Source { get; internal set; }
            /// <summary>发出Play时的DSP读数；不是经过波形验证的精确首样本时间。</summary>
            public double StartedAt { get; internal set; }
            /// <summary>带设备缓冲余量的回收上限；正常声音优先以isPlaying自然结束归还。</summary>
            public double EndsAt { get; internal set; }
            public bool ObservedPlaying { get; internal set; }
            public AudioLowPassFilter LowPass {get;internal set;}
            public AudioReverbFilter Reverb {get;internal set;}
            internal double nextAcoustic;
            internal float gain=1;
            internal float acousticGain=1;
        }

        private readonly SfxArbitrator arbitrator;
        private readonly Func<SfxBus, AudioMixerGroup> route;
        private readonly GameObject pool;
        private readonly VoiceState[] voices;
        private bool disposed;
        public int PoolSize => voices.Length;
        public int ActiveCount { get; private set; }
        public int StartedCount { get; private set; }
        public int ReturnedCount { get; private set; }
        public int FailedStartCount { get; private set; }
        public bool AcousticsEnabled { get; set; }
        public Vector3 AcousticOrigin {get;set;}
        private readonly RaycastHit[] acousticHits=new RaycastHit[16];
        public string LastError { get; private set; } = string.Empty;
        public IReadOnlyList<VoiceState> Voices => voices;

        public SfxVoicePlayer(GameObject owner, SfxArbitrator arbitrator, Func<SfxBus, AudioMixerGroup> route, int capacity = 32)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            this.arbitrator = arbitrator ?? throw new ArgumentNullException(nameof(arbitrator));
            this.route = route ?? throw new ArgumentNullException(nameof(route));
            if (capacity < arbitrator.HardLimit) throw new ArgumentOutOfRangeException(nameof(capacity), "声源池不能小于仲裁硬上限");
            pool = new GameObject("SFX Voice Pool");
            pool.transform.SetParent(owner.transform, false);
            voices = new VoiceState[capacity];
            for (int index = 0; index < capacity; index++)
            {
                var child = new GameObject("SFX Voice " + index.ToString("00"));
                child.transform.SetParent(pool.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                var lowPass=child.AddComponent<AudioLowPassFilter>();lowPass.enabled=false;
                var reverb=child.AddComponent<AudioReverbFilter>();reverb.enabled=false;
                voices[index] = new VoiceState { Source = source,LowPass=lowPass,Reverb=reverb };
            }
        }

        /// <summary>只启动仍在Arbitrator中的许可；重复使用同一许可不产生第二个声音。</summary>
        public bool TryPlay(SfxDecision decision)
        {
            if (disposed) return Fail("PLAYER_DISPOSED");
            if (!decision.Accepted || !arbitrator.TryGetVoice(decision.VoiceId, out SfxVoiceRecord record))
                return Fail("VOICE_PERMIT_UNAVAILABLE");
            if (Find(decision.VoiceId) != null) return Fail("VOICE_PERMIT_ALREADY_STARTED");
            if (decision.StolenVoiceId != 0) Return(Find(decision.StolenVoiceId));
            // 另一个新许可可能已撤销旧源；先回收过期占位，再选空槽。
            ReclaimInvalidPermits();
            VoiceState slot = Find(0);
            if (slot == null) return FailPermit(record.VoiceId, "VOICE_POOL_UNAVAILABLE");
            var definition = record.Definition;
            string invalid = string.Empty;
            if (definition == null || !definition.TryValidate(out invalid))
                return FailPermit(record.VoiceId, definition == null ? "DEFINITION_DESTROYED" : invalid);
            if (record.ClipIndex < 0 || record.ClipIndex >= definition.clips.Length)
                return FailPermit(record.VoiceId, "CLIP_INDEX_INVALID");
            AudioClip clip = definition.clips[record.ClipIndex];
            if (clip.loadState != AudioDataLoadState.Loaded)
                return FailPermit(record.VoiceId, "MEDIA_NOT_READY: " + clip.loadState);
            AudioMixerGroup output = route(definition.bus);
            if (output == null) return FailPermit(record.VoiceId, "OUTPUT_ROUTE_MISSING: " + definition.bus);
            AudioSource source = slot.Source;
            if (source == null || !source.gameObject.activeInHierarchy)
                return FailPermit(record.VoiceId, "SOURCE_UNAVAILABLE");
            source.Stop();
            source.loop = definition.loop;
            source.clip = clip;
            source.volume = definition.volume;
            source.pitch = record.Pitch;
            source.priority = Mathf.Clamp(256 - record.Priority * 255 / 100, 0, 256);
            source.outputAudioMixerGroup = output;
            source.spatialBlend = definition.spatial ? 1 : 0;
            source.minDistance = definition.minDistance;
            source.maxDistance = definition.maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.transform.position = record.Request.WorldPosition;
            source.dopplerLevel=0;
            slot.gain=1;slot.acousticGain=1;slot.nextAcoustic=0;
            slot.LowPass.enabled=false;slot.Reverb.enabled=false;
            slot.VoiceId = record.VoiceId;
            slot.StartedAt = AudioSettings.dspTime;
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            double bufferSafety = (double)bufferLength * bufferCount / Math.Max(1, AudioSettings.outputSampleRate);
            // Play实际进入DSP可能晚一个缓冲块；正常以isPlaying归还，期限另留缓冲余量避免截断短音尾部。
            slot.EndsAt = definition.loop?double.PositiveInfinity:slot.StartedAt + (double)clip.samples / clip.frequency / record.Pitch + Math.Max(.05, bufferSafety + .02);
            slot.ObservedPlaying = false;
            ActiveCount++;
            source.Play();
            ObservePlaying(slot);
            LastError = string.Empty;
            return true;
        }

        public void Tick(double dspNow)
        {
            if (disposed || double.IsNaN(dspNow) || double.IsInfinity(dspNow)) return;
            foreach (VoiceState voice in voices)
            {
                if (voice.VoiceId == 0) continue;
                if (!arbitrator.TryGetVoice(voice.VoiceId, out _) || voice.Source == null || !voice.Source.gameObject.activeInHierarchy)
                { Return(voice); continue; }
                ObservePlaying(voice);
                ApplyAcoustics(voice,dspNow);
                // DSP期限负责静音/设备暂不输出时的上限；真实已播放源可在自然结束时提前归还。
                if (dspNow >= voice.EndsAt || (voice.ObservedPlaying && !voice.Source.isPlaying)) Return(voice);
            }
        }

        public void StopSource(string sourceId, int sourceGeneration)
        {
            if (disposed) return;
            arbitrator.StopSource(sourceId, sourceGeneration);
            ReclaimInvalidPermits();
        }

        public void StopVoice(long voiceId)
        {if(!disposed)Return(Find(voiceId));}

        /// <summary>持续源位置和淡变只作用于许可声部，音量仍经唯一Mixer。</summary>
        public bool SetVoice(long id,Vector3 position,float gain)
        {
            var voice=Find(id);if(voice==null||voice.Source==null||!arbitrator.TryGetVoice(id,out var record))return false;
            voice.Source.transform.position=position;voice.gain=Mathf.Clamp01(gain);voice.Source.volume=record.Definition.volume*voice.gain*voice.acousticGain;return true;
        }

        private void ApplyAcoustics(VoiceState voice,double now)
        {
            if(!arbitrator.TryGetVoice(voice.VoiceId,out var record))return;
            bool active=AcousticsEnabled&&record.Definition.spatial;
            if(!active){voice.LowPass.enabled=false;voice.Reverb.enabled=false;voice.acousticGain=1;voice.Source.volume=record.Definition.volume*voice.gain;return;}
            if(now<voice.nextAcoustic)return;voice.nextAcoustic=now+.1;
            var from=voice.Source.transform.position+Vector3.up*.6f;var delta=AcousticOrigin-from;
            bool blocked=false;float distance=delta.magnitude;
            if(distance>.05f)
            {
                int count=Physics.RaycastNonAlloc(from,delta/distance,acousticHits,distance,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    if(acousticHits[i].collider==null)continue;
                    var obstacle=acousticHits[i].collider.GetComponentInParent<AudioAcousticObstacle>();
                    if(obstacle!=null&&obstacle.isActiveAndEnabled){blocked=true;break;}
                }
            }
            // 只有启用的作者声学墙产生直达声遮挡；使用预分配低通，不新增混音所有者。
            voice.LowPass.enabled=true;voice.LowPass.cutoffFrequency=blocked?2800:22000;
            // 遮挡只负责直达声衰减。碰到一面墙不等于进入室内，不能凭此凭空叠加Room混响。
            voice.Reverb.enabled=false;
            voice.acousticGain=blocked?.68f:1;
            voice.Source.volume=record.Definition.volume*voice.gain*voice.acousticGain;
        }

        /// <summary>清空短音许可及声音池；新世界由Root之后显式SetWorldScope。</summary>
        public void StopWorld()
        {
            if (disposed) return;
            arbitrator.StopWorld();
            foreach (VoiceState voice in voices) Return(voice);
        }

        public void Dispose()
        {
            if (disposed) return;
            StopWorld();
            disposed = true;
            if (pool == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(pool);
            else UnityEngine.Object.DestroyImmediate(pool);
        }

        private void ObservePlaying(VoiceState voice)
        {
            if (voice.ObservedPlaying || voice.Source == null || !voice.Source.isPlaying) return;
            voice.ObservedPlaying = true;
            StartedCount++;
        }

        private VoiceState Find(long voiceId)
        {
            foreach (VoiceState voice in voices) if (voice.VoiceId == voiceId) return voice;
            return null;
        }

        private void ReclaimInvalidPermits()
        {
            foreach (VoiceState voice in voices)
                if (voice.VoiceId != 0 && !arbitrator.TryGetVoice(voice.VoiceId, out _)) Return(voice);
        }

        private void Return(VoiceState voice)
        {
            if (voice == null || voice.VoiceId == 0) return;
            arbitrator.Release(voice.VoiceId);
            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.loop=false;voice.LowPass.enabled=false;voice.Reverb.enabled=false;
                voice.Source.outputAudioMixerGroup = null;
            }
            voice.VoiceId = 0;
            voice.ObservedPlaying = false;
            ActiveCount--;
            ReturnedCount++;
        }

        private bool FailPermit(long voiceId, string reason)
        {
            arbitrator.Release(voiceId);
            FailedStartCount++;
            return Fail(reason);
        }

        private bool Fail(string reason) { LastError = reason; return false; }
    }
}
