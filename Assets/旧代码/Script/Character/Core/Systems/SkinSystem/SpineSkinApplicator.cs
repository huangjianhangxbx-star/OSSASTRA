using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpineSkinApplicator : MonoBehaviour, ISkinApplicator
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    private Skin combinedSkin;

    public void ApplySkins(IEnumerable<SkinItemSO> items)
    {
        var skeleton = skeletonAnimation.Skeleton;
        var skeletonData = skeleton.Data;

        // 1. 创建/清空组合皮肤
        combinedSkin = combinedSkin ?? new Skin("combined-skin");
        combinedSkin.Clear();

        // 2. 遍历数据进行合并
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.spineSkinName)) continue;

            Skin foundSkin = skeletonData.FindSkin(item.spineSkinName);
            if (foundSkin != null)
            {
                combinedSkin.AddSkin(foundSkin);
            }
            else
            {
                Debug.LogWarning($"Spine皮肤不存在: {item.spineSkinName}");
            }
        }
        // 3. 应用并刷新
        skeleton.SetSkin(combinedSkin);
 
        skeleton.SetupPoseSlots();
        skeletonAnimation.Update(0);
        //这是 Spine 的强制刷新 API。Update(0) 表示“跳过时间步进，只做一次骨骼计算并更新 Mesh”。
        //因为在切换皮肤后，骨骼姿势没变，但贴图变了，必须手动触发一次更新才能看到效果。
    }
}
