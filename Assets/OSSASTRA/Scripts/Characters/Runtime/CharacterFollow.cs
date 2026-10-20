/*
 * 脚本：CharacterFollow
 * 功能：角色自身的可选跟随能力，拥有独立阈值、策略、位移历史与只读观察。
 * 边界：仅按外部分配目标产出move；不选择Current、不读设备、不执行运动/动作/HP/Spine。
 * 协作：Root显式初始化，Director分配目标源；Selector在Root原更新中惰性读取。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterFollow : MonoBehaviour, ICompanionIntentSource
    {
        [Header("跟随候选参数 / 世界单位，不是正式玩法平衡")]
        [SerializeField, Min(.01f)] private float followStartDistance=2.4f, followStopDistance=1.6f;
        [SerializeField, Min(.01f)] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
        private CharacterRoot owner, sampledTarget;
        private ICharacterControlSession session;
        private ICharacterFollowTargetSource targetSource;
        private CompanionFollowPolicy policy;
        private Vector3 lastPosition;
        private int revision=-1;
        private CompanionFollowState state;
        private string reason="未分配目标";
        public float StartDistance => followStartDistance;
        public float StopDistance => followStopDistance;
        public float BlockedCheckWindow => blockedCheckWindow;
        public float RetryTargetDelta => retryTargetDelta;
        public CharacterRoot Owner => owner;

        /// <summary>许可检查也撤回已失效历史/帧；不推进策略时钟。观察UI使用Observe，无此副作用。</summary>
        public bool IsAvailable
        {
            get
            {
                if (!TryContext(out var target)) { ResetIntent(); return false; }
                if (sampledTarget != target || revision != session.ControlRevision) ResetIntent();
                return true;
            }
        }

        public bool Initialize(CharacterRoot character)
        {
            if (character == null || character.gameObject != gameObject || (owner != null && owner != character)) return false;
            owner=character;
            // 参数只在显式初始化时构造策略；无效组件不会让本来可单人操作的Root启动失败。
            try { policy=new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta); }
            catch (ArgumentException) { policy=null; reason="跟随参数无效"; return false; }
            ResetIntent(); return true;
        }

        public bool CanAssign(ICharacterControlSession authority, ICharacterFollowTargetSource source)
            => owner != null && policy != null && Alive(authority) && Alive(source) &&
                (!Alive(targetSource) || ReferenceEquals(targetSource,source));

        /// <summary>只能同一分配者重绑，或接替已销毁/明确归还的分配；不抢活owner。</summary>
        public bool Assign(ICharacterControlSession authority, ICharacterFollowTargetSource source)
        {
            if (!CanAssign(authority,source)) return false;
            session=authority; targetSource=source; ResetIntent(); return true;
        }

        public void Unassign(ICharacterFollowTargetSource source)
        {
            if (!ReferenceEquals(source,targetSource)) return;
            ResetIntent(); session=null; targetSource=null; reason="未分配目标";
        }

        public CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta)
        {
            // 外来角色不能借本角色的组件读取移动，更不能清掉合法owner正在使用的策略。
            if (character != owner) return default;
            if (!IsAvailable || !TryContext(out var target)) return default;
            var position=owner.transform.position;
            var travel=position-lastPosition; travel.y=0;
            if (sampledTarget != target || revision != session.ControlRevision) travel=Vector3.zero;
            sampledTarget=target; revision=session.ControlRevision; lastPosition=position;
            var targetPosition=target.transform.position;
            var offset=targetPosition-position;
            var decision=policy.Sample(offset.x,offset.z,targetPosition.x,targetPosition.z,travel.magnitude,delta,true);
            state=decision.State;
            reason=state==CompanionFollowState.Blocked ? "受阻等待目标移动；无绕墙" :
                state==CompanionFollowState.CloseEnough ? "停止区或启动阈值内" : "朝分配目标移动";
            return new CharacterInputFrame { move=new Vector2((float)decision.MoveX,(float)decision.MoveZ) };
        }

        private bool TryContext(out CharacterRoot target)
        {
            target=null;
            if (!isActiveAndEnabled || owner == null || !owner.CanSimulateIntent || policy == null ||
                !Alive(session) || !session.IsIntentSamplingAllowed || !Alive(targetSource) ||
                (targetSource is Behaviour provider && !provider.isActiveAndEnabled) ||
                !targetSource.TryGetFollowTarget(owner,out target) || target == null || target == owner ||
                !target.CanSimulateIntent || owner.DataBus == null || !owner.DataBus.AllowsMovement || !owner.DataBus.MoveReady)
                return false;
            return true;
        }

        /// <summary>仅重置本角色能力；Root在暂停/失焦/禁用时调用，旧动作生命周期不变。</summary>
        public void ResetIntent()
        {
            if (policy != null) policy.Reset();
            sampledTarget=null; revision=-1; state=CompanionFollowState.Inactive;
            reason=policy == null ? "跟随参数无效或未初始化" : "等待合法目标重新采样";
            if (owner != null) { lastPosition=owner.transform.position; owner.ClearCompanionIntent(this); }
        }

        /// <summary>纯观察，不Sample、不清历史；无效条件或新目标尚未采样时不展示旧Following。</summary>
        public CompanionFollowObservation Observe()
        {
            var valid=TryContext(out var target);
            var distance=target == null || owner == null ? 0 : Vector2.Distance(
                new Vector2(target.transform.position.x,target.transform.position.z),new Vector2(owner.transform.position.x,owner.transform.position.z));
            var current=valid && sampledTarget==target && revision==session.ControlRevision;
            return new CompanionFollowObservation(current ? state : CompanionFollowState.Inactive,
                current ? reason : "未分配／不可行动／等待新目标采样",distance,target == null ? "无" : target.ActorId);
        }

        private void OnDisable() { ResetIntent(); }
        private void OnDestroy() { ResetIntent(); targetSource=null; session=null; owner=null; }
        private static bool Alive(object value) => value != null && (!(value is UnityEngine.Object unityObject) || unityObject != null);
    }

    public readonly struct CompanionFollowObservation
    {
        public CompanionFollowState State { get; }
        public string Reason { get; }
        public float Distance { get; }
        public string TargetId { get; }
        public CompanionFollowObservation(CompanionFollowState state, string reason, float distance, string targetId)
        { State=state; Reason=reason; Distance=distance; TargetId=targetId; }
    }
}
