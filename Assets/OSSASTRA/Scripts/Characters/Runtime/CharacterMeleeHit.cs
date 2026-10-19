using Ossastra.Development;
using System;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed class CharacterMeleeHit : MonoBehaviour
    {
        [SerializeField, Min(0)] private float reach = .8f;
        [SerializeField, Min(.01f)] private float radius = .65f;
        [SerializeField] private float height = .8f;
        [SerializeField] private LayerMask targetLayers = ~0;
        private CharacterDataBus dataBus;
        public string LastResult { get; private set; } = "未释放";

        public void Initialize(CharacterDataBus dataBus)
        {
            if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
            this.dataBus = dataBus;
        }

        public void Resolve(AttackRelease release)
        {
            if (dataBus == null) throw new InvalidOperationException("Melee requires character DataBus initialization");
            var direction = dataBus.Direction;
            var center = transform.position + Vector3.up * height + direction * reach;
            var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
                .Select(c => c.GetComponentInParent<CombatTestTarget>()).Where(t => t != null).Distinct().ToArray();
            var outcomes = release.Resolve(targets.Select(t => t.Health));
            foreach (var outcome in outcomes)
                if (outcome.Reason == HitReason.Applied)
                    targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
            LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
                o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
        }
    }
}
