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
        private bool previewEnabled;
        private Source source;

        public AbilityDefinitionSO Definition => definition;
        public bool PreviewEnabled => previewEnabled;

        /// <summary>仅供本轮动作验证，正式接入等待统一架势费用和 Spine 恢复事件。</summary>
        public void EnablePreview(bool active) => previewEnabled = active;

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
            public bool IsAvailable => mount != null && mount.isActiveAndEnabled && mount.PreviewEnabled &&
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
