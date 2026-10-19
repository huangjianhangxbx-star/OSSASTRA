/*
 * 脚本：CompanionFollowDirector
 * 功能：场景级最小跟随策略，读取唯一控制会话，为非实控角色提供移动帧。
 * 边界：不写Current、Transform、HP、Spine或动作时钟；不寻路、加速或自动攻击。
 * 协作：Start在所有Root初始化后显式绑定选择器；选择器Read时惰性计算，每角色策略独立。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public readonly struct CompanionFollowObservation
    {
        public CompanionFollowState State { get; }
        public string Reason { get; }
        public float Distance { get; }
        public string TargetId { get; }
        public CompanionFollowObservation(CompanionFollowState state, string reason, float distance, string targetId)
        { State=state; Reason=reason; Distance=distance; TargetId=targetId; }
    }

    public sealed class CompanionFollowDirector : MonoBehaviour, ICompanionIntentSource
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [Header("跟随候选参数 / 世界单位，不是正式玩法平衡")]
        [SerializeField, Min(.01f)] private float followStartDistance=2.4f, followStopDistance=1.6f;
        [SerializeField, Min(.01f)] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
        private readonly Dictionary<CharacterRoot, Entry> entries = new Dictionary<CharacterRoot, Entry>();
        private ICharacterControlSession session;
        private bool started, configured, bindingsOwned;
        public bool IsAvailable => configured && isActiveAndEnabled && coordinator != null;
        public float StartDistance => followStartDistance;
        public float StopDistance => followStopDistance;
        public string Status { get; private set; } = "未绑定";
        public CharacterControlCoordinator Coordinator => coordinator;

        private sealed class Entry
        {
            public CompanionFollowPolicy policy;
            public Vector3 lastPosition;
            public int revision=-1;
            public CompanionFollowState state;
            public string reason="等待采样";
        }

        private void Start() { started=true; BindMembers(); }
        private void OnEnable() { if (started) BindMembers(); }

        private void BindMembers()
        {
            configured=false;
            // 绑定失败/禁用的会话不能借Director占用原角色选择器，即使它仍保存成员引用。
            if (coordinator == null || !coordinator.isActiveAndEnabled) { Status="控制会话缺失或未启用"; return; }
            session=coordinator;
            try { new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta); }
            catch (ArgumentException) { Status="跟随阈值无效"; return; }
            // 先验证全部消费者；第二Director不得清理或替换原Director的有效帧。
            foreach (var character in session.Characters)
                if (character == null || !character.CanBindCompanionIntent(session,this))
                { Status="角色未就绪或伙伴来源已被占用"; return; }
            foreach (var character in session.Characters)
            {
                character.BindCompanionIntent(session,this);
                entries[character]=new Entry { policy=new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta) };
            }
            bindingsOwned=true; configured=true; Status="直接追随 / 无寻路";
        }

        /// <summary>只从合法会话的非Current成员产生移动；实际碰撞、Ready与表现仍经原角色管线。</summary>
        public CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta)
        {
            if (!IsAvailable || session == null || character == null || !entries.TryGetValue(character,out var entry)) return default;
            var target=session.Current;
            var allowed=session.IsIntentSamplingAllowed && target != null && target != character &&
                target.CanSimulateIntent && character.CanSimulateIntent && character.DataBus != null &&
                character.DataBus.AllowsMovement && character.DataBus.MoveReady;
            var position=character.transform.position;
            var travel=position-entry.lastPosition; travel.y=0;
            if (entry.revision != session.ControlRevision)
            {
                // 切人后的策略不能继承上一来源的运动命令或无位移计时。
                entry.policy.Reset(); travel=Vector3.zero; entry.revision=session.ControlRevision;
            }
            entry.lastPosition=position;
            var targetPosition=target == null ? position : target.transform.position;
            var offset=targetPosition-position;
            var decision=entry.policy.Sample(offset.x,offset.z,targetPosition.x,targetPosition.z,travel.magnitude,delta,allowed);
            entry.state=decision.State;
            entry.reason=!allowed ? "会话／焦点／暂停／Ready／移动许可不足" :
                decision.State==CompanionFollowState.Blocked ? "受阻等待领队移动；不承诺绕墙" :
                decision.State==CompanionFollowState.CloseEnough ? "停止区或启动阈值内" : "朝领队位置移动";
            // 只设置move；这里没有攻击、取消、暂停或调试命令，也不直接执行位移。
            return new CharacterInputFrame { move=new Vector2((float)decision.MoveX,(float)decision.MoveZ) };
        }

        /// <summary>开发只读观察，不推进策略；未采样的新代际不展示旧Following作为当前事实。</summary>
        public CompanionFollowObservation Observe(CharacterRoot character)
        {
            var target=coordinator == null ? null : coordinator.Current;
            var offset=target == null || character == null ? Vector3.zero : target.transform.position-character.transform.position;
            offset.y=0;
            var targetId=target == null ? "无" : target.ActorId;
            if (!IsAvailable || session == null || !session.IsIntentSamplingAllowed || character == null || !character.CanSimulateIntent)
                return new CompanionFollowObservation(CompanionFollowState.Inactive,"无合法会话／不可行动",offset.magnitude,targetId);
            if (target==character) return new CompanionFollowObservation(CompanionFollowState.Inactive,"玩家接管",0,targetId);
            if (!entries.TryGetValue(character,out var entry) || entry.revision!=session.ControlRevision)
                return new CompanionFollowObservation(CompanionFollowState.Inactive,"等待新代际采样",offset.magnitude,targetId);
            return new CompanionFollowObservation(entry.state,entry.reason,offset.magnitude,targetId);
        }

        private void OnDisable()
        {
            configured=false;
            if (!bindingsOwned) return;
            foreach (var pair in entries)
            {
                pair.Value.policy.Reset();
                if (pair.Key != null) pair.Key.ClearCompanionIntent(this);
            }
            Status="跟随关闭，玩家仍由原会话管理";
        }
        private void OnDestroy() { entries.Clear(); session=null; }
    }
}
