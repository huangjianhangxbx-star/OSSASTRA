// 绑定角色拥有的横版俯冲定义，按本体与来源身份给现有动作运行授权。
using System;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class SideViewDiveMount : MonoBehaviour
    {
        [SerializeField, Tooltip("本体俯冲能力。配置不随主手或副手换装变化。")]
        private AbilityDefinitionSO definition;
        [SerializeField, Tooltip("角色初始拥有该俯冲能力。停用或卸载组件即可解除。")]
        private bool ownedAtStart;
        private bool? developmentOverride;
        private Source source;

        public AbilityDefinitionSO Definition => definition;
        public bool OwnedAtStart => ownedAtStart;
        public bool IsAvailable => isActiveAndEnabled && (developmentOverride ?? ownedAtStart) &&
            definition != null && definition.executor is SideViewDiveAbilitySO;

#if UNITY_EDITOR
        public void SetDevelopmentOverride(bool active) => developmentOverride = active;
#endif

        internal IAbilitySource PrepareSource(CharacterRoot owner, Vector3 point)
        {
            if (source == null || source.Owner != owner || source.Definition != definition)
                source = new Source(this, owner, definition);
            source.AimPoint = point;
            return source;
        }

        internal bool Owns(IAbilitySource port) => source != null && ReferenceEquals(source, port) && source.IsAvailable;

        internal void TickSource(float delta, bool controlled)
        {
            if (source != null && isActiveAndEnabled && Definition == source.Definition)
                source.RuntimeState.Tick(delta, controlled);
        }

        private sealed class Source : IAbilitySource, IAbilityTargetSource
        {
            private readonly SideViewDiveMount mount;
            public CharacterRoot Owner { get; }
            public AbilityDefinitionSO Definition { get; }
            public Source(SideViewDiveMount mount, CharacterRoot owner, AbilityDefinitionSO definition)
            {
                this.mount = mount;
                Owner = owner;
                Definition = definition;
                InstanceId = owner.ActorId + ":innate:side-view-dive";
            }

            public string OwnerId => Owner.ActorId;
            public string InstanceId { get; }
            public bool IsAvailable => mount != null && mount.IsAvailable &&
                mount.Definition == Definition && ReferenceEquals(mount.source, this);
            public EquipmentAbilityState RuntimeState { get; } = new EquipmentAbilityState();
            public CombatVitals Target => null;
            public Vector3? AimPoint { get; set; }
            public bool TryReserve(Guid transactionId) => transactionId != Guid.Empty && IsAvailable;
            public bool CommitRelease(Guid executionId) => executionId != Guid.Empty && IsAvailable;
            public void CancelReservation() {}
        }
    }
}
