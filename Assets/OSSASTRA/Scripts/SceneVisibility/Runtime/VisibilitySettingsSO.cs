using UnityEngine;

// 仅保存并校验可见性配置；不执行检测、投影、角色选择或渲染状态写入。
namespace Ossastra.SceneVisibility
{
    [CreateAssetMenu(menuName = "OSSASTRA/场景可见性/配置", fileName = "VisibilitySettings")]
    public sealed class VisibilitySettingsSO : ScriptableObject
    {
        [Header("检测预算（显示投影与平滑仍逐帧更新）")]
        public float detectionHz = 20f;
        public int maxTargets = 2;
        public int maxGroups = 4;

        [Header("过渡时间（秒）")]
        public float enterSeconds = .16f;
        public float restoreSeconds = .28f;
        public float exitGraceSeconds = .1f;

        [Header("身体投影与局部圆窗")]
        [Tooltip("相对身体屏幕高度的留白比例；0.1表示10%。由Root投影计算最小核心，不是世界米数。")]
        public float minBodyPadding = .1f;
        [Tooltip("局部半径相对身体最小清晰核心的最大倍率；程度稳定后不会按时间继续增长。")]
        public float maxRadiusMultiplier = 2.5f;
        [Tooltip("羽化宽度相对身体屏幕高度的比例；0.3表示30%。Root负责实际像素半径。")]
        public float feather = .3f;

        [Header("仅显式允许的有限模型组可完全淡出")]
        public float wholeFadeStart = .75f;
        public float wholeFadeEnd = .95f;

        public bool TryValidate(out string reason)
        {
            if (!VisibilityNumbers.IsFinite(detectionHz) || detectionHz <= 0f)
                return Invalid("detectionHz必须是有限正数。", out reason);
            if (maxTargets < 1 || maxTargets > 2)
                return Invalid("当前实现支持1或2个保护目标。", out reason);
            if (maxGroups < 1 || maxGroups > 4)
                return Invalid("当前渲染后端支持1至4个活跃组。", out reason);
            if (!VisibilityNumbers.IsFinite(enterSeconds) || enterSeconds <= 0f ||
                !VisibilityNumbers.IsFinite(restoreSeconds) || restoreSeconds <= 0f)
                return Invalid("进入与恢复时间必须是有限正数。", out reason);
            if (!VisibilityNumbers.IsFinite(exitGraceSeconds) || exitGraceSeconds < 0f)
                return Invalid("退出滞回时间必须是有限非负数。", out reason);
            if (!VisibilityNumbers.IsFinite(minBodyPadding) || minBodyPadding < 0f)
                return Invalid("身体留白比例必须是有限非负数。", out reason);
            if (!VisibilityNumbers.IsFinite(maxRadiusMultiplier) || maxRadiusMultiplier < 1f)
                return Invalid("最大半径倍率必须是有限值且不小于1。", out reason);
            if (!VisibilityNumbers.IsFinite(feather) || feather <= 0f)
                return Invalid("羽化比例必须是有限正数；平滑主效不接受零羽化。", out reason);
            if (!VisibilityNumbers.IsUnit(wholeFadeStart) || !VisibilityNumbers.IsUnit(wholeFadeEnd) ||
                wholeFadeEnd <= wholeFadeStart)
                return Invalid("整组淡出门槛需满足0≤start<end≤1。", out reason);
            reason = null;
            return true;
        }

        static bool Invalid(string message, out string reason) { reason = message; return false; }
    }
}
