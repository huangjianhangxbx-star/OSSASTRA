/* AM02：原算法适配当前只读角色事实与来源；不引用旧角色/Bus/武器运行态。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using System.Collections.Generic;
using UnityEngine;

public class AnimationArbitrator
{
    public static CharacterAnimationPlan Resolve(
        EAnimationLogicState id,
        AnimationPlanMappingSO baseMap,
        List<AnimationPlanOverrideRuleSetSO> itemOverrides,
        AnimationPlanOverrideRuleSetSO statusOverrides,
        CharacterRoot root, AnimationComposeContext context = null)
    {
        CharacterAnimationPlan resolvedPlan = null;

        // 1. 状态覆盖优先
        if (statusOverrides != null)
        {
            resolvedPlan = statusOverrides.GetOverride(id, context);
        }

        // 2. 装备/职业覆盖
        if (resolvedPlan == null && itemOverrides != null)
        {
            for (int i = itemOverrides.Count - 1; i >= 0; i--)
            {
                AnimationPlanOverrideRuleSetSO overrideSet = itemOverrides[i];

                if (overrideSet == null)
                    continue;

                resolvedPlan = overrideSet.GetOverride(id, context);

                if (resolvedPlan != null)
                    break;
            }
        }

        // 3. 基础映射兜底
        if (resolvedPlan == null && baseMap != null)
        {
            resolvedPlan = baseMap.GetDefault(id);

            if (resolvedPlan == null)
            {
                Debug.LogWarning(
                    $"[AnimationArbitrator] baseMap 未命中: id={id}, int={(int)id}, baseMap={baseMap.name}, character={root?.name}"
                );
            }
        }

        // 4. Clone + 补收件人
        if (resolvedPlan != null)
        {
            return resolvedPlan.CloneFor(root);
        }

        //Debug.LogWarning($"[AnimationArbitrator] 未找到动画映射: {id} | 角色: {root.name}");
        return null;
    }
}

}
