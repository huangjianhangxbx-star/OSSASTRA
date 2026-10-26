/* 脚本：SfxSourceHandle
 * 功能：持有音频来源的世界/代次租约，撤销时只终止该来源短音。
 * 边界：不持有AudioSource，不补造玩法事件；旧世界租约不能复活。
 */
using System;
using UnityEngine;
namespace Ossastra.Audio
{
    public sealed class SfxSourceHandle:IDisposable
    {
        private GameAudioRoot owner;
        public string SourceId{get;}
        public string WorldScopeId{get;}
        public int Generation{get;}
        public bool IsDisposed=>owner==null;
        internal SfxSourceHandle(GameAudioRoot owner,string id,string world,int generation)
        {this.owner=owner;SourceId=id;WorldScopeId=world;Generation=generation;}
        public SfxDecision Submit(string eventId,string factKey,Vector3 position)
        {
            if(owner==null)return new SfxDecision(false,"SOURCE_LEASE_DISPOSED");
            return owner.SubmitSfx(new SfxRequest(eventId,SourceId,WorldScopeId,Generation,factKey,position,Time.realtimeSinceStartupAsDouble));
        }
        public void Dispose(){if(owner==null)return;var root=owner;owner=null;root.ReleaseSfxSource(this);}
        /// <summary>只能停止本租约签发的声部，不能凭全局VoiceId停止别人。</summary>
        public bool StopVoice(long voiceId)=>owner!=null&&owner.StopSfxVoice(this,voiceId);
    }
}
