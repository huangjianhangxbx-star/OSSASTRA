/* 脚本：BgmContextHost
 * 功能：将本场标题/区域/已交战事实组成音乐快照，绑定唯一音频根。
 * 边界：不判断战斗、不读输入；目前没有正式对话提供方，仅暴露作用域接口。
 * 协作：Integration适配器分别写本领域事实；场景失效只撤自己的Host与对话令牌。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.Audio
{
    [DefaultExecutionOrder(-800)]
    public sealed class BgmContextHost:MonoBehaviour
    {
        [SerializeField] private GameAudioRoot audioRoot;
        private readonly HashSet<object> farZones=new HashSet<object>();
        private readonly List<IDisposable> dialogueLeases=new List<IDisposable>();
        private sealed class HostLease:IDisposable
        {
            private BgmContextHost owner;
            private IDisposable inner;
            public HostLease(BgmContextHost owner,IDisposable inner){this.owner=owner;this.inner=inner;}
            public void Dispose(){if(inner==null)return;inner.Dispose();inner=null;owner.dialogueLeases.Remove(this);owner=null;}
        }
        private bool title,engaged;
        public GameAudioRoot AudioRoot=>audioRoot;
        public BgmContext Snapshot=>new BgmContext{title=title,engaged=engaged,far=farZones.Count>0};
        private void OnEnable()
        {
            if(audioRoot==null||!audioRoot.IsOwner)audioRoot=GameAudioRoot.Instance;
            if(audioRoot==null)return;
            audioRoot.BindHost(this);Publish();
        }
        public void SetTitle(bool value){title=value;Publish();}
        public void SetEngaged(bool value){engaged=value;Publish();}
        public void SetFarZone(object owner,bool active)
        {if(owner==null)return;if(active)farZones.Add(owner);else farZones.Remove(owner);Publish();}
        public IDisposable AcquireDialogueFocus()
        {
            if(!isActiveAndEnabled||audioRoot==null||!audioRoot.IsBoundHost(this))throw new InvalidOperationException("Host is not bound to the active audio world");
            var lease=new HostLease(this,audioRoot.AcquireDialogueFocus());dialogueLeases.Add(lease);return lease;
        }
        internal void ReleaseWorldScope()
        {foreach(var lease in dialogueLeases.ToArray())lease.Dispose();farZones.Clear();}
        private void Publish(){if(isActiveAndEnabled&&audioRoot!=null)audioRoot.Publish(this,Snapshot);}
        private void OnDisable()
        {ReleaseWorldScope();if(audioRoot!=null)audioRoot.UnbindHost(this);}
    }
}
