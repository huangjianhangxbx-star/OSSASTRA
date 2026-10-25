/* 脚本：BgmMixController
 * 功能：维护用户音乐/主音量和作用域快照，输出唯一目标增益。
 * 边界：不选曲、不启动音源；快照逐项相乘，撤销不覆盖用户设置。
 * 协作：Root推进显式平滑，Transport唯一写AudioSource.volume；Mixer保持单位增益。
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
        {this.baseline=Clamp(baseline,2);this.dialogueGain=Clamp(dialogueGain,2);this.smoothing=Mathf.Max(.001f,smoothing);CurrentGain=TargetGain;}
        public float UserBgmVolume {get=>user;set=>user=Clamp(value,1);}
        public float MasterVolume {get=>master;set=>master=Clamp(value,1);}
        public bool Muted {get;set;}
        public bool Focused {get;set;}=true;
        public int SnapshotCount=>snapshots.Count;
        public float CurrentGain {get;private set;}
        public float TargetGain
        {
            get{float result=Muted?0:baseline*user*master*(Focused?1:.25f);foreach(var gain in snapshots.Values)result*=gain;return result;}
        }
        public void AcquireSnapshot(string token,float gain)
        {if(string.IsNullOrEmpty(token))throw new ArgumentException("Snapshot token required",nameof(token));snapshots[token]=Clamp(gain,2);}
        public void ReleaseSnapshot(string token){if(token!=null)snapshots.Remove(token);}
        public IDisposable AcquireDialogueFocus()
        {string token="dialogue-focus/"+Guid.NewGuid().ToString("N");AcquireSnapshot(token,dialogueGain);return new Lease(this,token);}
        public void ClearSnapshots()=>snapshots.Clear();
        public void Tick(float unscaledDelta)
        {if(float.IsNaN(unscaledDelta)||float.IsInfinity(unscaledDelta)||unscaledDelta<=0)return;CurrentGain=Mathf.Lerp(CurrentGain,TargetGain,1-Mathf.Exp(-unscaledDelta/smoothing));}
        private static float Clamp(float value,float max)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,0,max);
    }
}
