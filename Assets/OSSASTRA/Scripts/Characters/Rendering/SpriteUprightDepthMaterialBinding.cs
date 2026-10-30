/* 功能：包装一个正式SpriteRenderer的已知Sprites/Default材质，只拥有自身variant。
 * 边界：不改Sprite/color/flip/MPB/TRS；没有Update/事件/场景扫描，不反射私有装备字段。
 * 生命周期：source材质被外部接管后不抢回；Suspend只在仍是自身variant时归还prior并销毁自身资源。
 */
using System;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Ossastra.Characters.Rendering
{
    internal sealed class SpriteUprightDepthMaterialBinding : IDisposable
    {
        private SpriteRenderer source;
        private Transform actor;
        private Shader requestedShader, sourceShader;
        private Material prior, variant;
        private bool blocked;
        private float nextPropertySync;
        private static readonly int Foot=Shader.PropertyToID("_VisualDepthFoot"), Normal=Shader.PropertyToID("_VisualDepthNormal"), Enabled=Shader.PropertyToID("_VisualDepthEnabled");
        public int OwnedMaterialCount => variant == null ? 0 : 1;
        public bool HasSource => source != null;
        public string LastResult { get; private set; } = "无Sprite源";

        public void Bind(SpriteRenderer next, Transform owner, Shader shader)
        {
            if(source!=next || actor!=owner || requestedShader!=shader)
            { Suspend(); source=next; actor=owner; requestedShader=shader; blocked=false; }
            if(source==null) return;
            if(actor==null || !source.transform.IsChildOf(actor) || source.gameObject.scene!=actor.gameObject.scene)
            { Suspend(); LastResult="Sprite来源不属于本actor"; return; }
            if(blocked) return;
            if(variant!=null && source.sharedMaterial!=variant)
            { ReleaseVariant(false); blocked=true; LastResult="外部Sprite材质已接管；保留原写者"; return; }
            if(variant!=null && (prior==null || prior.shader!=sourceShader)) ReleaseVariant(true);
            if(variant==null)
            {
                var current=source.sharedMaterial;
                if(current==null || current.shader==null || current.shader.name!="Sprites/Default" || source.maskInteraction!=SpriteMaskInteraction.None)
                { LastResult="未知Sprite Shader/Mask；保留原深度"; return; }
                if(shader==null || !shader.isSupported)
                { LastResult="缺少已知Sprite深度Shader；保留原深度"; return; }
                prior=current; sourceShader=current.shader;
                variant=new Material(shader){name=current.name+" [Actor Sprite visual depth]",hideFlags=HideFlags.HideAndDontSave};
                SyncProperties(true);
                source.sharedMaterial=variant;
                LastResult="已绑定本actor Sprite实例材质";
            }
            else SyncProperties(false);
        }

        private void SyncProperties(bool force)
        {
            if(variant==null || prior==null) return;
            float now=Time.realtimeSinceStartup;
            if(!force && now<nextPropertySync) return;
            nextPropertySync=now+.25f;
            variant.CopyPropertiesFromMaterial(prior);
            variant.renderQueue=prior.renderQueue;
            variant.enableInstancing=prior.enableInstancing;
            variant.SetFloat(Enabled,0);
        }

        public void SetCameraMode(bool active, Vector3 foot, Vector3 normal)
        {
            if(variant==null) return;
            variant.SetVector(Foot,foot);variant.SetVector(Normal,normal);variant.SetFloat(Enabled,active?1:0);
        }

        private void ReleaseVariant(bool restore)
        {
            if(variant==null) return;
            if(restore && source!=null && source.sharedMaterial==variant) source.sharedMaterial=prior;
            variant.SetFloat(Enabled,0);
            if(Application.isPlaying) Object.Destroy(variant); else Object.DestroyImmediate(variant);
            variant=null; prior=null; sourceShader=null;
        }

        public void Suspend()
        { ReleaseVariant(true);source=null;actor=null;requestedShader=null;blocked=false;LastResult="Sprite自有材质已归还"; }
        public void Dispose() { Suspend(); }
    }
}
