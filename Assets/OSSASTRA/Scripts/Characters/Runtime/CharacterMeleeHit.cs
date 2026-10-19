/*
 * 脚本：CharacterMeleeHit
 * 功能：将一次释放的方向快照转换为物理范围检测和目标批次。
 * 边界：不创建动作时钟或另一份HP；不直接依赖开发靶类型。
 * 协作：Root经ICharacterHitResolver调用；ICharacterHitTarget连接Health及命中表现。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed class CharacterMeleeHit : MonoBehaviour, ICharacterHitResolver
    {
        [SerializeField, Min(0)] private float reach = .8f;
        [SerializeField, Min(.01f)] private float radius = .65f;
        [SerializeField] private float height = .8f;
        [SerializeField] private LayerMask targetLayers = ~0;
        public string LastResult { get; private set; } = "未释放";

public void Resolve(AttackRelease release, Vector3 releaseDirection)
        {
            var direction = releaseDirection;
            var center = transform.position + Vector3.up * height + direction * reach;
            var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
                .Select(c => c.GetComponentsInParent<MonoBehaviour>().OfType<ICharacterHitTarget>().FirstOrDefault()).Where(t => t != null).Distinct().ToArray();
            var outcomes = release.Resolve(targets.Select(t => t.Health));
            foreach (var outcome in outcomes)
                if (outcome.Reason == HitReason.Applied)
                    targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
            LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
                o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
        }
    }
}
