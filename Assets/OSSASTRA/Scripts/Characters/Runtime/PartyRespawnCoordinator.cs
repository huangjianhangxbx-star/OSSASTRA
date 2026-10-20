/*
 * 脚本：PartyRespawnCoordinator
 * 功能：原会话成员的场景内返场；唯一模拟计时，先检查全批落点再恢复本体。
 * 边界：不保存第二份HP/Current，不重置敌人或物品；没有组件的旧场不会自动复生。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [DefaultExecutionOrder(-150)]
    public sealed class PartyRespawnCoordinator : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator session;
        [SerializeField] private CapsuleRespawnPlacement placement;
        [SerializeField] private Vector3[] checkpoints=Array.Empty<Vector3>();
        [SerializeField, Min(0)] private float singleDelay=10, wipeDelay=.5f;
        private CharacterRoot[] members;
        private RespawnSchedule schedule;
        private Action<HitOutcome>[] deathHandlers;
        private bool focused=true, applicationPaused;
        public string Status { get; private set; }="未初始化";
        public double SimulationTime => schedule==null?0:schedule.Time;
        public double DueAt(int index) => schedule==null?-1:schedule.DueAt(index);
        public double DiedAt(int index) => schedule==null?-1:schedule.DiedAt(index);
        private void Start()
        {
            if(session==null || placement==null || checkpoints.Length!=session.Characters.Count)
            {Status="返场配置无效"; enabled=false; return;}
            members=session.Characters.ToArray();
            if(members.Any(x=>x==null) || members.Distinct().Count()!=members.Length)
            {Status="成员重复或缺失";enabled=false;return;}
            foreach(var member in members)
                if(!member.BindRespawnOwner(this))
                {
                    foreach(var owned in members) owned.UnbindRespawnOwner(this);
                    Status="返场所有权冲突";enabled=false;return;
                }
            schedule=new RespawnSchedule(members.Length,singleDelay,wipeDelay);
            deathHandlers=new Action<HitOutcome>[members.Length];
            for(var i=0;i<members.Length;i++)
            {
                var index=i;
                deathHandlers[i]=outcome=>schedule.RecordDeath(index);
                members[i].Vitals.Died+=deathHandlers[i];
            }
            Status="等待死亡";
        }
        private void Update()
        {
            if(schedule==null || session==null || !session.isActiveAndEnabled || placement==null || !placement.isActiveAndEnabled) return;
            var available=members.Select(x=>x!=null && x.isActiveAndEnabled && session.Characters.Contains(x)).ToArray();
            var alive=members.Select(x=>x!=null && x.IsAlive).ToArray();
            var suspended=!focused || applicationPaused || members.Any(x=>x!=null && x.IsSimulationSuspended);
            schedule.Tick(alive,available,Time.deltaTime,suspended);
            var due=schedule.ReadyIndices;
            if(due.Length==0) {Status=suspended?"模拟暂停":"等待到期";return;}
            var positions=new List<Vector3>();
            foreach(var index in due)
            {
                var member=members[index];
                if(!member.CanRespawn(this) || !placement.TryFind(member.GetComponent<CharacterController>(),checkpoints[index],positions,out var point))
                {Status="等待合法落点";return;}
                positions.Add(point);
            }
            // 单线程事务：所有资格/落点均已验证，恢复不调用外部事件，不允许部分失败后继续下一批。
            for(var i=0;i<due.Length;i++)
            {
                if(!members[due[i]].Respawn(this,positions[i])) {Status="返场事务失效";enabled=false;return;}
                schedule.Complete(due[i]);
            }
            Physics.SyncTransforms();
            session.ReconcileAfterRespawn();
            Status="场景内返场完成";
        }
        private void OnApplicationFocus(bool value) {focused=value;}
        private void OnApplicationPause(bool value) {applicationPaused=value;}
        private void OnDestroy()
        {
            if(members==null) return;
            for(var i=0;i<members.Length;i++)
            {
                var member=members[i]; if(member==null)continue;
                if(deathHandlers!=null && member.Vitals!=null) member.Vitals.Died-=deathHandlers[i];
                member.UnbindRespawnOwner(this);
            }
        }
    }
}
