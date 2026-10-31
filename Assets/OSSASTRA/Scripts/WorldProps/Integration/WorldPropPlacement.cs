/* 脚本：WorldPropPlacement
 * 功能：以作者显式同层支撑Collider验证有限掉落候选及安全锚点。
 * 边界：不向整个世界无限向下寻找、不会落到桥下；失败保留Pending，不吞奖励。
 */
using UnityEngine;

namespace Ossastra.WorldProps
{
    public static class WorldPropPlacement
    {
        public static bool TryPlace(WorldPropRoot prop, out Vector3 position, out string error)
        {
            position = default;
            error = null;
            // 高台的顶盖属于可重建缓存；保留稳定Binding，不持有已销毁的旧Collider。
            var support = prop == null ? null : prop.supportSurface != null
                ? (prop.supportSurface.isActiveAndEnabled ? prop.supportSurface.capCollider : null) : prop.support;
            if (prop == null || support == null || !support.enabled || !support.gameObject.activeInHierarchy || prop.safeDropAnchor == null || string.IsNullOrWhiteSpace(prop.surfaceId))
            {
                error = "缺少同层支撑/安全锚点";
                return false;
            }

            var anchor = prop.safeDropAnchor.position;
            var offsets = new[]
            {
                Vector3.zero,
                Vector3.right * .25f,
                Vector3.left * .25f,
                Vector3.forward * .25f,
                Vector3.back * .25f
            };
            foreach (var offset in offsets)
            {
                var expected = anchor + offset;
                var ray = new Ray(expected + Vector3.up * .4f, Vector3.down);
                if (!support.Raycast(ray, out var hit, .8f) || hit.normal.y < .7f || Mathf.Abs(hit.point.y - anchor.y) > .2f)
                    continue;
                var foot = hit.point + Vector3.up * .03f;
                var obstacles = Physics.OverlapCapsule(foot + Vector3.up * .08f, foot + Vector3.up * .25f, .08f, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (var obstacle in obstacles)
                {
                    if (obstacle == support || System.Array.IndexOf(prop.ownedColliders, obstacle) >= 0 || obstacle.GetComponentInParent<Ossastra.Combat.CombatVitals>() != null)
                        continue;
                    blocked = true;
                    break;
                }

                if (blocked)
                    continue;
                position = foot;
                return true;
            }

            error = "同层有限候选全部受阻；保留既定掉落意图";
            return false;
        }
    }
}
