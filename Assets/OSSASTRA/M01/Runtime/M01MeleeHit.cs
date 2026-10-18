using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.M01
{
    public sealed class M01MeleeHit : MonoBehaviour
    {
        [SerializeField, Min(0)] private float reach = .8f;
        [SerializeField, Min(.01f)] private float radius = .65f;
        [SerializeField] private float height = .8f;
        [SerializeField] private LayerMask targetLayers = ~0;
        public string LastResult { get; private set; } = "未释放";

        public void Resolve(AttackRelease release, Vector3 direction)
        {
            var center = transform.position + Vector3.up * height + direction * reach;
            var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
                .Select(c => c.GetComponentInParent<M01HealthTarget>()).Where(t => t != null).Distinct().ToArray();
            var outcomes = release.Resolve(targets.Select(t => t.Health));
            foreach (var outcome in outcomes)
                if (outcome.Reason == HitReason.Applied)
                    targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
            LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
                o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
        }
    }
}
