using UnityEngine;

// 仅保存并校验可见性配置；不执行检测、投影、角色选择或渲染状态写入。
namespace Ossastra.SceneVisibility
{
    public enum VisibilityEdgeStyle { Soft = 0, SoftDitherEdge = 1 }

    [CreateAssetMenu(menuName = "OSSASTRA/场景可见性/配置", fileName = "VisibilitySettings")]
    public sealed class VisibilitySettingsSO : ScriptableObject
    {
        [Header("检测预算（显示投影与平滑仍逐帧更新）")]
        public float detectionHz = 20f;
        public int maxTargets = 2;
        public int maxGroups = 4;

        [Header("核心过渡时间（秒；也是旧配置的兼容速率）")]
        public float enterSeconds = .16f;
        public float restoreSeconds = .28f;
        public float exitGraceSeconds = .1f;

        [Header("扩圈与整组独立时间（0沿用核心速率，保持旧SO行为）")]
        [Min(0)] public float radiusEnterSeconds;
        [Min(0)] public float radiusRestoreSeconds;
        [Min(0)] public float wholeEnterSeconds;
        [Min(0)] public float wholeRestoreSeconds;
        [Tooltip("强遮挡持续达到该秒数后才开始整组淡出；0保持旧配置。支撑保护仍立即撤销整组淡出。")]
        [Min(0)] public float wholeFadeDelaySeconds;

        [Header("身体投影与局部圆窗")]
        [Tooltip("相对身体屏幕高度的留白比例；0.1表示10%。由Root投影计算最小核心，不是世界米数。")]
        public float minBodyPadding = .1f;
        [Tooltip("局部半径相对身体最小清晰核心的最大倍率；程度稳定后不会按时间继续增长。")]
        public float maxRadiusMultiplier = 2.5f;
        [Tooltip("羽化宽度相对身体屏幕高度的比例；0.3表示30%。")]
        public float feather = .3f;
        [Tooltip("相对当前内半径的最小羽化宽度；0保持旧宽度。")]
        [Min(0)] public float featherRadiusRatio;
        [Tooltip("1080高视口的最小羽化像素；按实际视口高度缩放，0保持旧宽度。")]
        [Min(0)] public float featherMinPixels;

        [Header("仅显式允许的有限模型组可完全淡出")]
        public float wholeFadeStart = .75f;
        public float wholeFadeEnd = .95f;

        [Header("柔边点阵（共享opacity后处理，不增加捕获纹理）")]
        public VisibilityEdgeStyle edgeStyle = VisibilityEdgeStyle.Soft;
        [Range(0, 1)] public float ditherStrength = .18f;
        [Tooltip("1080高视口的点阵单元像素；按视口缩放后取整，至少1物理像素。0兼容为1。")]
        [Range(1, 2)] public float ditherCell1080Pixels = 1f;

        public float FeatherWidthPixels(float bodyPixels, float innerPixels, int viewportHeight)
        {
            VisibilityNumbers.RequireNonNegative(bodyPixels, nameof(bodyPixels));
            VisibilityNumbers.RequireNonNegative(innerPixels, nameof(innerPixels));
            if (viewportHeight <= 0) throw new System.ArgumentOutOfRangeException(nameof(viewportHeight));
            return Mathf.Max(Mathf.Max(bodyPixels * feather, innerPixels * featherRadiusRatio), featherMinPixels * (viewportHeight / 1080f));
        }

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
            if (!Nonnegative(exitGraceSeconds) || !Nonnegative(radiusEnterSeconds) || !Nonnegative(radiusRestoreSeconds) ||
                !Nonnegative(wholeEnterSeconds) || !Nonnegative(wholeRestoreSeconds) || !Nonnegative(wholeFadeDelaySeconds))
                return Invalid("独立时间与退出滞回必须是有限非负数；独立时间0沿用旧速率。", out reason);
            if (!Nonnegative(minBodyPadding)) return Invalid("身体留白比例必须是有限非负数。", out reason);
            if (!VisibilityNumbers.IsFinite(maxRadiusMultiplier) || maxRadiusMultiplier < 1f)
                return Invalid("最大半径倍率必须是有限值且不小于1。", out reason);
            if (!VisibilityNumbers.IsFinite(feather) || feather <= 0f || !Nonnegative(featherRadiusRatio) || !Nonnegative(featherMinPixels))
                return Invalid("羽化比例须为有限正数；最小像素与半径比例须为有限非负数。", out reason);
            if (!VisibilityNumbers.IsUnit(wholeFadeStart) || !VisibilityNumbers.IsUnit(wholeFadeEnd) || wholeFadeEnd <= wholeFadeStart)
                return Invalid("整组淡出门槛需满足0≤start<end≤1。", out reason);
            if ((edgeStyle != VisibilityEdgeStyle.Soft && edgeStyle != VisibilityEdgeStyle.SoftDitherEdge) ||
                !VisibilityNumbers.IsUnit(ditherStrength) || !Nonnegative(ditherCell1080Pixels) || ditherCell1080Pixels > 2f)
                return Invalid("需使用已定义柔边样式；点阵强度0至1，1080单元0至2像素（0兼容为1）。", out reason);
            reason = null; return true;
        }
        static bool Nonnegative(float value) => VisibilityNumbers.IsFinite(value) && value >= 0;
        static bool Invalid(string message, out string reason) { reason = message; return false; }
    }
}
