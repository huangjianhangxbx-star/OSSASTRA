/*
 * 脚本：CompanionFollowDirector
 * 功能：场景级目标分配与绑定，读取唯一会话为成员分配Current目标。
 * 边界：不保存角色策略/距离/受阻/位移历史，不执行运动或动作。
 * 协作：Start在Root初始化后预检全组；将各角色自己的Follow接至其Selector，归还仅清自己绑定。
 */
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed class CompanionFollowDirector : MonoBehaviour, ICharacterFollowTargetSource
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        // 仅保留旧场序列化迁移证据，不再参与运行时计算；权威参数在每角色CharacterFollow。
        [SerializeField, HideInInspector] private float followStartDistance=2.4f, followStopDistance=1.6f;
        [SerializeField, HideInInspector] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
        private readonly List<CharacterFollow> bindings = new List<CharacterFollow>();
        private bool started, configured;
        public bool IsAvailable => configured && isActiveAndEnabled && coordinator != null && coordinator.isActiveAndEnabled;
        public string Status { get; private set; } = "未绑定";
        public CharacterControlCoordinator Coordinator => coordinator;
        private void Start() { started=true; BindMembers(); }
        private void OnEnable() { if (started) BindMembers(); }

        private void BindMembers()
        {
            configured=false;
            if (coordinator == null || !coordinator.isActiveAndEnabled) { Status="控制会话缺失或未启用"; return; }
            var ids=new HashSet<string>();
            // 先检查全部成员；列表只记录待归还的绑定，不成为第二成员/Current权威。
            foreach (var character in coordinator.Characters)
                if (character == null || !ids.Add(character.ActorId) || character.Follow == null ||
                    !character.Follow.CanAssign(coordinator,this) || !character.CanBindCompanionIntent(coordinator,character.Follow))
                { Status="角色能力未就绪或分配已被占用"; return; }
            foreach (var character in coordinator.Characters)
            {
                var follow=character.Follow;
                if (!follow.Assign(coordinator,this)) { ReleaseBindings(); Status="分配失败，已归还自身绑定"; return; }
                bindings.Add(follow);
                if (!character.BindCompanionIntent(coordinator,follow))
                { ReleaseBindings(); Status="接线失败，已归还自身绑定"; return; }
            }
            configured=true; Status="队伍目标分配 / 角色自身计算跟随";
        }

        public bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target)
        {
            target=null;
            if (!IsAvailable || !coordinator.IsIntentSamplingAllowed || follower == null ||
                coordinator.Current == null || follower==coordinator.Current) return false;
            foreach (var character in coordinator.Characters)
                if (character==follower) { target=coordinator.Current; return true; }
            return false;
        }

        // 历史开发调用的只读转发；本体观察才是唯一状态来源，Director没有镜像缓存。
        public CompanionFollowObservation Observe(CharacterRoot character)
            => character != null && character.Follow != null ? character.Follow.Observe() :
                new CompanionFollowObservation(CharacterCore.CompanionFollowState.Inactive,"缺少角色跟随能力",0,"无");

        private void ReleaseBindings()
        {
            configured=false;
            foreach (var follow in bindings) if (follow != null) follow.Unassign(this);
            bindings.Clear();
        }
        private void OnDisable() { ReleaseBindings(); Status="目标分配关闭，玩家仍由原会话管理"; }
        private void OnDestroy() { ReleaseBindings(); }
    }
}