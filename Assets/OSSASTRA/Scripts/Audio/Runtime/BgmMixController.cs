/* 脚本：BgmMixController
 * 功能：维护用户音乐/主音量和音乐专用快照，同时提供完整增益与局部音乐增益。
 * 边界：不选曲、不启动音源；快照逐项相乘，撤销不覆盖用户设置。
 * 协作：Root推进显式平滑；共享Mixer的Master/BGM各乘用户因子一次，Transport只用LocalCurrentGain。
 * 关键约束：原CurrentGain/TargetGain保持兼容，不把对话duck扩散到SFX。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.Audio
{
    public sealed class BgmMixController
    {
        private sealed class Lease : IDisposable
        {
            private BgmMixController owner;
            private readonly string token;
            public Lease(BgmMixController owner,string token){this.owner=owner;this.token=token;}
            public void Dispose(){if(owner==null)return;owner.ReleaseSnapshot(token);owner=null;}
        }
        private readonly Dictionary<string,float> snapshots=new Dictionary<string,float>();
        private readonly float baseline,dialogueGain,smoothing;
        private float user=.24f,master=.75f;
        public BgmMixController(float baseline,float dialogueGain,float smoothing)
        {this.baseline=Clamp(baseline,2);this.dialogueGain=Clamp(dialogueGain,2);this.smoothing=float.IsNaN(smoothing)||float.IsInfinity(smoothing)?.035f:Mathf.Max(.001f,smoothing);CurrentGain=TargetGain;LocalCurrentGain=LocalTargetGain;}
        public float UserBgmVolume {get=>user;set=>user=Clamp(value,1);}
        public float MasterVolume {get=>master;set=>master=Clamp(value,1);}
        public bool Muted {get;set;}
        public bool Focused {get;set;}=true;
        public int SnapshotCount=>snapshots.Count;
        public float CurrentGain {get;private set;}
        public float LocalCurrentGain {get;private set;}
        /// <summary>仅音乐自身的基线、失焦与快照；用户Master、BGM和mute由共享Mixer拥有。</summary>
        public float LocalTargetGain
        {
            get{float result=baseline*(Focused?1:.25f);foreach(var gain in snapshots.Values)result*=gain;return result;}
        }
        public float TargetGain
        {
            get=>Muted?0:LocalTargetGain*user*master;
        }
        public void AcquireSnapshot(string token,float gain)
        {if(string.IsNullOrEmpty(token))throw new ArgumentException("Snapshot token required",nameof(token));snapshots[token]=Clamp(gain,2);}
        public void ReleaseSnapshot(string token){if(token!=null)snapshots.Remove(token);}
        public IDisposable AcquireDialogueFocus()
        {string token="dialogue-focus/"+Guid.NewGuid().ToString("N");AcquireSnapshot(token,dialogueGain);return new Lease(this,token);}
        public void ClearSnapshots()=>snapshots.Clear();
        public void Tick(float unscaledDelta)
        {if(float.IsNaN(unscaledDelta)||float.IsInfinity(unscaledDelta)||unscaledDelta<=0)return;float blend=1-Mathf.Exp(-unscaledDelta/smoothing);CurrentGain=Mathf.Lerp(CurrentGain,TargetGain,blend);LocalCurrentGain=Mathf.Lerp(LocalCurrentGain,LocalTargetGain,blend);}
        private static float Clamp(float value,float max)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,0,max);
    }
}
