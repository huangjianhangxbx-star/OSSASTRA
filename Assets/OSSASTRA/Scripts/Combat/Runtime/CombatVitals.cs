/*
 * 脚本：CombatVitals
 * 功能：持有本体唯一Health/阵营，接收正式结算反馈并单次通知死亡、关闭自身表现和碰撞。
 * 边界：IsAlive只读Health；不另存isDead，不选Current、不计算攻击、不自动复活。
 * 协作：Root/敌人显式Initialize；Melee回调只反馈已Applied结果，Development显式恢复同一Health。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatVitals : MonoBehaviour, ICombatTarget
    {
        [SerializeField, Min(1)] private int maximumHp=100;
        [SerializeField] private CombatFaction faction=CombatFaction.Allied;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool[] rendererEnabled, colliderEnabled;
        private MaterialPropertyBlock[] originalBlocks;
        private MaterialPropertyBlock flashBlock;
        private bool deathNotified, flashing;
        private float flashUntil;
        public Health Health { get; private set; }
        public bool IsAlive => Health != null && Health.Current>0;
        public bool IsAvailable => isActiveAndEnabled && IsAlive;
        public Vector3 WorldPosition => transform.position;
        public Transform TargetTransform => transform;
        public CombatFaction Faction => faction;
        public int DeathCount { get; private set; }
        public string LastResult { get; private set; }="尚未受击";
        public event Action<HitOutcome> Died;

        public bool Initialize(string actorId)
        {
            if (Health != null) return Health.Id==actorId;
            if (string.IsNullOrWhiteSpace(actorId) || maximumHp<=0) return false;
            Health=new Health(actorId,maximumHp);
            // Unity原生对象必须在显式主线程初始化中创建，不能依赖MonoBehaviour字段构造时机。
            flashBlock=new MaterialPropertyBlock();
            renderers=GetComponentsInChildren<Renderer>(true);
            colliders=GetComponentsInChildren<Collider>(true);
            rendererEnabled=new bool[renderers.Length]; colliderEnabled=new bool[colliders.Length];
            originalBlocks=new MaterialPropertyBlock[renderers.Length];
            for (var i=0;i<renderers.Length;i++)
            { rendererEnabled[i]=renderers[i].enabled; originalBlocks[i]=new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(originalBlocks[i]); }
            for (var i=0;i<colliders.Length;i++) colliderEnabled[i]=colliders[i].enabled;
            return true;
        }

        public void OnHitResolved(HitOutcome outcome)
        {
            if (Health==null || outcome==null || outcome.TargetId!=Health.Id || outcome.Reason!=HitReason.Applied) return;
            LastResult=outcome.ActorId+" → "+outcome.TargetId+" "+outcome.HpBefore+"→"+outcome.HpAfter+" #"+outcome.ActionId.ToString("N");
            if (IsAlive) { ShowHit(); return; }
            if (deathNotified) return;
            deathNotified=true; DeathCount++; flashing=false;
            // 只退本体表现/碰撞，保留Root/身份/Health供会话与调试读取；死亡不靠禁用全组Root。
            foreach (var r in renderers) if (r!=null) r.enabled=false;
            foreach (var c in colliders) if (c!=null) c.enabled=false;
            Died?.Invoke(outcome);
        }

        public void ShowHit()
        {
            if (!IsAlive || renderers==null) return;
            flashUntil=Time.time+.15f; flashing=true;
            flashBlock.SetColor("_Color",Color.white); flashBlock.SetColor("_BaseColor",Color.white);
            foreach (var r in renderers) if (r!=null) r.SetPropertyBlock(flashBlock);
        }

        private void Update()
        {
            if (!flashing || Time.time<flashUntil) return;
            flashing=false; RestoreBlocks();
        }
        private void RestoreBlocks()
        { for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].SetPropertyBlock(originalBlocks[i]); }

        /// <summary>开发回放恢复同一身份/Health，仅还原本组件关掉的Renderer/Collider；无生产计时返场。</summary>
        public void RestoreForDevelopment()
        {
            if (Health==null) return;
            Health.RestoreForDevelopment(); deathNotified=false; flashing=false; LastResult="Development重置";
            for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].enabled=rendererEnabled[i];
            for (var i=0;i<colliders.Length;i++) if (colliders[i]!=null) colliders[i].enabled=colliderEnabled[i];
            RestoreBlocks();
        }
    }
}
