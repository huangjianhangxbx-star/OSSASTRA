using System;

// 纯需求与状态推进：输入由外部已完成资格判定；不接管物理、角色或相机。
namespace Ossastra.SceneVisibility
{
    public readonly struct VisibilityDemand
    {
        public bool Eligible { get; }
        public float Coverage { get; }
        public float Entry { get; }
        public bool AllowWholeFade { get; }
        public bool SupportProtected { get; }
        public VisibilityDemand(bool eligible, float coverage, float entry, bool allowWholeFade, bool supportProtected)
        {
            VisibilityNumbers.RequireUnit(coverage, nameof(coverage)); VisibilityNumbers.RequireUnit(entry, nameof(entry));
            Eligible = eligible; Coverage = coverage; Entry = entry; AllowWholeFade = allowWholeFade; SupportProtected = supportProtected;
        }
    }

    // 保存一个目标/遮挡组的状态；配置复制为值，Reconfigure不重置当前值与身份/滞回历史。
    public sealed class VisibilityState
    {
        float enterSeconds, restoreSeconds, radiusEnter, radiusRestore, wholeEnter, wholeRestore;
        float graceSeconds, maxRadius, radiusSpan, wholeStart, wholeEnd, wholeDelay;
        float graceRemaining, lastSeverity, lastWholeTarget, strongDemandSeconds;
        bool retainingDemand;
        public float LocalStrength { get; private set; }
        public float RadiusMultiplier { get; private set; } = 1f;
        public float WholeFade { get; private set; }
        public float WholeDelayProgress => wholeDelay <= 0 ? 1f : Math.Min(1f, strongDemandSeconds / wholeDelay);
        public bool IsReleased => !retainingDemand && graceRemaining <= 0f && LocalStrength == 0f && WholeFade == 0f && RadiusMultiplier == 1f;

        public VisibilityState(VisibilitySettingsSO settings) { Reconfigure(settings); }
        public void Reconfigure(VisibilitySettingsSO settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.TryValidate(out string reason)) throw new ArgumentException(reason, nameof(settings));
            enterSeconds = settings.enterSeconds; restoreSeconds = settings.restoreSeconds;
            radiusEnter = settings.radiusEnterSeconds > 0 ? settings.radiusEnterSeconds : enterSeconds;
            radiusRestore = settings.radiusRestoreSeconds > 0 ? settings.radiusRestoreSeconds : restoreSeconds;
            wholeEnter = settings.wholeEnterSeconds > 0 ? settings.wholeEnterSeconds : enterSeconds;
            wholeRestore = settings.wholeRestoreSeconds > 0 ? settings.wholeRestoreSeconds : restoreSeconds;
            graceSeconds = settings.exitGraceSeconds; maxRadius = settings.maxRadiusMultiplier;
            // 新cap可低于当前半径。保留完整回收跨度，尤其cap=1时不能令速率变成0。
            radiusSpan = Math.Max(maxRadius - 1f, RadiusMultiplier - 1f);
            wholeStart = settings.wholeFadeStart; wholeEnd = settings.wholeFadeEnd; wholeDelay = settings.wholeFadeDelaySeconds;
        }
        public void Tick(VisibilityDemand demand, float deltaSeconds)
        {
            VisibilityNumbers.RequireNonNegative(deltaSeconds, nameof(deltaSeconds));
            VisibilityNumbers.RequireUnit(demand.Coverage, nameof(demand.Coverage)); VisibilityNumbers.RequireUnit(demand.Entry, nameof(demand.Entry));
            if (demand.SupportProtected) { WholeFade = 0f; lastWholeTarget = 0f; strongDemandSeconds = 0f; }
            if (demand.Eligible)
            {
                lastSeverity = Math.Max(demand.Coverage, demand.Entry);
                lastWholeTarget = demand.AllowWholeFade && !demand.SupportProtected
                    ? SmoothUnit((lastSeverity - wholeStart) / (wholeEnd - wholeStart)) : 0f;
                if (!demand.AllowWholeFade) WholeFade = 0f;
                float wholeDelta = deltaSeconds;
                if (lastWholeTarget > 0f)
                {
                    float waiting = Math.Max(0f, wholeDelay - strongDemandSeconds);
                    wholeDelta = Math.Max(0f, deltaSeconds - waiting);
                    strongDemandSeconds = Math.Min(wholeDelay, strongDemandSeconds + deltaSeconds);
                }
                else strongDemandSeconds = 0f;
                retainingDemand = true; graceRemaining = graceSeconds;
                Advance(deltaSeconds, 1f, RadiusTarget(), lastWholeTarget, wholeDelta); return;
            }
            // 检测暂缺不累计“持续强遮挡”。旧delay=0仍保持旧滞回期间继续进入的行为。
            strongDemandSeconds = 0f;
            float held = retainingDemand ? Math.Min(deltaSeconds, graceRemaining) : 0f;
            if (held > 0f)
            {
                Advance(held, 1f, RadiusTarget(), lastWholeTarget, wholeDelay <= 0 ? held : 0f);
                graceRemaining = Math.Max(0f, graceRemaining - held);
            }
            if (graceRemaining <= 0f) retainingDemand = false;
            float restoring = deltaSeconds - held;
            if (restoring > 0f) Advance(restoring, 0f, 1f, 0f, restoring);
        }
        public void Reset()
        {
            LocalStrength = 0f; RadiusMultiplier = 1f; WholeFade = 0f;
            graceRemaining = lastSeverity = lastWholeTarget = strongDemandSeconds = 0f; retainingDemand = false;
        }
        float RadiusTarget() => (float)(1d + ((double)maxRadius - 1d) * lastSeverity);
        void Advance(float delta, float local, float radius, float whole, float wholeEntryDelta)
        {
            LocalStrength = Move(LocalStrength, local, delta, 1f, enterSeconds, restoreSeconds);
            RadiusMultiplier = Move(RadiusMultiplier, radius, delta, radiusSpan, radiusEnter, radiusRestore);
            WholeFade = Move(WholeFade, whole, whole > WholeFade ? wholeEntryDelta : delta, 1f, wholeEnter, wholeRestore);
        }
        static float Move(float current, float target, float delta, float span, float enter, float restore)
        {
            if (current == target || delta <= 0f || span <= 0f) return current;
            double step = (double)delta / (target > current ? enter : restore) * span, difference = (double)target - current;
            return Math.Abs(difference) <= step ? target : (float)(current + Math.Sign(difference) * step);
        }
        static float SmoothUnit(float value) { float t = Math.Max(0f, Math.Min(1f, value)); return t * t * (3f - 2f * t); }
    }
}
