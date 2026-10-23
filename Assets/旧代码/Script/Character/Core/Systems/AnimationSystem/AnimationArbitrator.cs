using System.Collections.Generic;
using UnityEngine;

public class AnimationArbitrator
{
    public static CharacterAnimationPlan Resolve(
        EAnimationLogicState id,
        AnimationPlanMappingSO baseMap,
        List<AnimationPlanOverrideRuleSetSO> itemOverrides,
        AnimationPlanOverrideRuleSetSO statusOverrides,
        CharacterRoot root)
    {
        if (root == null || root.characterDataBus == null)
        {
            Debug.LogWarning("[AnimationArbitrator] root 或 characterDataBus 为空");
            return null;
        }

        CharacterAnimationPlan resolvedPlan = null;

        // 1. 状态覆盖优先
        if (statusOverrides != null)
        {
            resolvedPlan = statusOverrides.GetOverride(id, root.characterDataBus);
        }

        // 2. 装备/职业覆盖
        if (resolvedPlan == null && itemOverrides != null)
        {
            for (int i = itemOverrides.Count - 1; i >= 0; i--)
            {
                AnimationPlanOverrideRuleSetSO overrideSet = itemOverrides[i];

                if (overrideSet == null)
                    continue;

                resolvedPlan = overrideSet.GetOverride(id, root.characterDataBus);

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
                    $"[AnimationArbitrator] baseMap 未命中: id={id}, int={(int)id}, baseMap={baseMap.name}, character={root.name}"
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
