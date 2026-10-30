using System;

// 纯需求与状态推进：输入由外部已完成资格判定；不接管物理、角色或相机。
namespace Ossastra.SceneVisibility
{
    // 表示一个已判定资格的观测需求，本类型不再执行物理或相机查询。
    public readonly struct VisibilityDemand
    {
        public bool Eligible { get; }
        public float Coverage { get; }
        public float Entry { get; }
        public bool AllowWholeFade { get; }
        public bool SupportProtected { get; }

        public VisibilityDemand(bool eligible, float coverage, float entry, bool allowWholeFade, bool supportProtected)
        {
            VisibilityNumbers.RequireUnit(coverage, nameof(coverage));
            VisibilityNumbers.RequireUnit(entry, nameof(entry));
            Eligible = eligible; Coverage = coverage; Entry = entry;
            AllowWholeFade = allowWholeFade; SupportProtected = supportProtected;
        }
    }

    // 保存一个目标/遮挡组的状态，不持有Renderer、世界对象或另一份“当前角色”。
    // 构造时校验并复制配置；外部明确变更配置时应重新创建状态，而非隐式读取变化中的SO。
    public sealed class VisibilityState
    {
        readonly float enterSeconds, restoreSeconds, graceSeconds, maxRadius, wholeStart, wholeEnd;
        float graceRemaining, lastSeverity, lastWholeTarget;
        bool retainingDemand;

        public float LocalStrength { get; private set; }
        public float RadiusMultiplier { get; private set; } = 1f;
        public float WholeFade { get; private set; }
        public bool IsReleased => !retainingDemand && graceRemaining <= 0f &&
            LocalStrength == 0f && WholeFade == 0f && RadiusMultiplier == 1f;

        public VisibilityState(VisibilitySettingsSO settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.TryValidate(out string reason)) throw new ArgumentException(reason, nameof(settings));
            enterSeconds = settings.enterSeconds; restoreSeconds = settings.restoreSeconds;
            graceSeconds = settings.exitGraceSeconds; maxRadius = settings.maxRadiusMultiplier;
            wholeStart = settings.wholeFadeStart; wholeEnd = settings.wholeFadeEnd;
        }

        public void Tick(VisibilityDemand demand, float deltaSeconds)
        {
            // 先拒绝无效输入，再修改任何状态，包括支撑保护与滞回计时。
            VisibilityNumbers.RequireNonNegative(deltaSeconds, nameof(deltaSeconds));
            VisibilityNumbers.RequireUnit(demand.Coverage, nameof(demand.Coverage));
            VisibilityNumbers.RequireUnit(demand.Entry, nameof(demand.Entry));
            if (demand.SupportProtected)
            {
                // 支撑安全优先于平滑：本Tick立即撤销整组淡出，局部前景需求仍可继续。
                WholeFade = 0f;
                lastWholeTarget = 0f;
            }

            if (demand.Eligible)
            {
                // 程度只决定半径和整组需求；合法遮挡的清晰核心最终趋1，避免轻挡稳态发灰。
                lastSeverity = Math.Max(demand.Coverage, demand.Entry);
                lastWholeTarget = demand.AllowWholeFade && !demand.SupportProtected
                    ? SmoothUnit((lastSeverity - wholeStart) / (wholeEnd - wholeStart)) : 0f;
                if (!demand.AllowWholeFade) WholeFade = 0f;
                retainingDemand = true;
                graceRemaining = graceSeconds;
                Advance(deltaSeconds, 1f, RadiusTarget(), lastWholeTarget);
                return;
            }

            // 即使单次delta跨越滞回边界，也分段消费“保留需求”和“恢复”时间。
            // 检测暂缺时只保留上次程度；可继续趋近该目标，但不能按等待时间提高程度。
            float held = retainingDemand ? Math.Min(deltaSeconds, graceRemaining) : 0f;
            if (held > 0f)
            {
                Advance(held, 1f, RadiusTarget(), lastWholeTarget);
                graceRemaining = Math.Max(0f, graceRemaining - held);
            }
            if (graceRemaining <= 0f) retainingDemand = false;
            float restoring = deltaSeconds - held;
            if (restoring > 0f) Advance(restoring, 0f, 1f, 0f);
        }

        public void Reset()
        {
            LocalStrength = 0f; RadiusMultiplier = 1f; WholeFade = 0f;
            graceRemaining = lastSeverity = lastWholeTarget = 0f;
            retainingDemand = false;
        }

        float RadiusTarget() => (float)(1d + ((double)maxRadius - 1d) * lastSeverity);
        void Advance(float delta, float local, float radius, float whole)
        {
            LocalStrength = Move(LocalStrength, local, delta, 1f);
            RadiusMultiplier = Move(RadiusMultiplier, radius, delta, Math.Max(0f, maxRadius - 1f));
            WholeFade = Move(WholeFade, whole, delta, 1f);
        }
        float Move(float current, float target, float delta, float span)
        {
            // 参数表示完整跨度的进入/恢复耗时；需求改变不重新启动一条计时动画。
            if (current == target || delta <= 0f || span <= 0f) return current;
            double step = (double)delta / (target > current ? enterSeconds : restoreSeconds) * span;
            double difference = (double)target - current;
            return Math.Abs(difference) <= step ? target : (float)(current + Math.Sign(difference) * step);
        }
        static float SmoothUnit(float value)
        {
            float t = Math.Max(0f, Math.Min(1f, value));
            return t * t * (3f - 2f * t);
        }
    }
}
