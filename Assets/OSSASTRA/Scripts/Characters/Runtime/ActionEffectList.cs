/* 功能：普攻与技能共用的可选招式效果配置。曲线、ID和参数在接受时复制。
 * 边界：SO 只保存作者配置；角色、去重与计时属于 ActionImpactRun。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Characters
{
    public enum ActionEffectTrigger { Accepted, Released, MoveReady, AttackReady, PhaseChanged, Contact, SpineEvent }
    public enum ActionImpactDirection { Forward, Backward, Input, Custom }
    public enum ActionInputDirection { Both, Forward, Backward }
    public enum ActionEffectGround { Any, Grounded, Airborne }

    [Serializable]
    public sealed class ActionEffectList
    {
        public bool bypass;
        [SerializeReference] public List<ActionEffectEntry> entries = new List<ActionEffectEntry>();
#if UNITY_EDITOR
        public static Func<CharacterRoot, ScriptableObject, ActionEffectList> EditorOverride;
#endif
        internal static ActionEffectEntry[] Freeze(CharacterRoot actor, ScriptableObject source, ActionEffectList authored)
        {
#if UNITY_EDITOR
            if (actor) authored = EditorOverride?.Invoke(actor, source) ?? authored;
#endif
            if (authored == null || authored.bypass || authored.entries == null || authored.entries.Count == 0)
                return Array.Empty<ActionEffectEntry>();
            var frozen = new List<ActionEffectEntry>(authored.entries.Count);
            var ids = new HashSet<string>();
            foreach (var entry in authored.entries)
            {
                if (entry != null && !entry.enabled) continue;
                string error = entry == null ? "缺失效果类型" : entry.ValidationError();
                if (error == null && !ids.Add(entry.id)) error = "重复条目 ID";
                if (error != null)
                {
                    Debug.LogWarning($"招式效果 {source.name} / {entry?.id} 已跳过：{error}", source);
                    continue;
                }
                frozen.Add(entry.Freeze());
            }
            return frozen.ToArray();
        }
    }

    [Serializable]
    public abstract class ActionEffectEntry
    {
        public string id = Guid.NewGuid().ToString("N");
        public bool enabled = true;
        public ActionEffectTrigger trigger;
        public ActionEffectFrequency frequency;
        [Tooltip("指定已绑定动画事件的原名")] public string spineEvent;
        [Tooltip("仅换相时比对进入阶段的正式主片段名；留空允许任意换相。")] public string phaseClip;
        [Tooltip("-1 允许所有释放序号")] public int emissionIndex = -1;
        [Tooltip("留空允许任意投递通道")] public string channel;
        [Tooltip("真实格挡也可触发。动作结束后，离手命中只能影响有效目标，自身反冲/输入窗口已结清。")] public bool onGuard;
        [Min(.01f)] public float interval = .2f;
        [Min(0)] public int maxTriggers;
        [Tooltip("按发动者所在的地面/空中状态判断")] public ActionEffectGround ground;

        protected static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        internal virtual ActionEffectEntry Freeze() => (ActionEffectEntry)MemberwiseClone();
        public virtual string ValidationError()
        {
            if (string.IsNullOrWhiteSpace(id)) return "缺少条目 ID";
            if (!Enum.IsDefined(typeof(ActionEffectTrigger), trigger) || !Enum.IsDefined(typeof(ActionEffectFrequency), frequency) ||
                !Enum.IsDefined(typeof(ActionEffectGround), ground)) return "触发或条件枚举无效";
            if (emissionIndex < -1 || maxTriggers < 0 || frequency == ActionEffectFrequency.Interval && (!Finite(interval) || interval <= 0))
                return "释放序号、次数或间隔无效";
            if (trigger == ActionEffectTrigger.SpineEvent && string.IsNullOrWhiteSpace(spineEvent)) return "缺少 Spine 事件名";
            if (trigger != ActionEffectTrigger.Contact && trigger != ActionEffectTrigger.Released &&
                (emissionIndex >= 0 || !string.IsNullOrEmpty(channel))) return "序号/通道只适用于投递或接触";
            return null;
        }
    }

    [Serializable]
    public abstract class FixedDisplacementEffect : ActionEffectEntry
    {
        [Min(0)] public float distance = .12f;
        [Min(.001f)] public float duration = .15f;
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        internal override ActionEffectEntry Freeze()
        {
            var copy = (FixedDisplacementEffect)base.Freeze();
            copy.curve = new AnimationCurve(curve.keys);
            return copy;
        }
        public override string ValidationError()
        {
            var error = base.ValidationError();
            if (error != null) return error;
            if (!Finite(distance) || distance < 0 || !Finite(duration) || duration <= 0) return "距离、持续时间无效";
            if (curve == null || curve.length < 2 || Mathf.Abs(curve.Evaluate(0)) > .001f ||
                Mathf.Abs(curve.Evaluate(1) - 1) > .001f) return "位移曲线须从 0 到 1";
            float previous = 0;
            for (int i = 0; i <= 64; i++)
            {
                float value = curve.Evaluate(i / 64f);
                if (!Finite(value) || value + .00001f < previous) return "位移曲线须单调递增";
                previous = value;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class SelfDisplacementEffect : FixedDisplacementEffect
    {
        public ActionImpactDirection direction;
        public Vector2 customDirection = Vector2.right;
        [Tooltip("覆盖输入分量；原阶段已配置前冲时，保留旧前冲并拒绝重复自身位移。")] public bool replaceInput;
        public override string ValidationError() => base.ValidationError() ??
            (!Enum.IsDefined(typeof(ActionImpactDirection), direction) || direction == ActionImpactDirection.Custom &&
             (!Finite(customDirection.x) || !Finite(customDirection.y) || customDirection.sqrMagnitude < .00001f) ? "主动位移方向无效" : null);
    }

    [Serializable]
    public sealed class InputMovementScaleEffect : ActionEffectEntry
    {
        [Range(0,1)] public float scale = .15f;
        public ActionInputDirection allowed;
        public bool fullBody = true;
        [Tooltip("勾选时在正式 MoveReady 提前交还此输入窗口，终结与取消始终会清理。")] public bool endAtMoveReady;
        public override string ValidationError() => base.ValidationError() ??
            (!Finite(scale) || scale < 0 || scale > 1 || !Enum.IsDefined(typeof(ActionInputDirection), allowed) ? "输入倍率或方向无效" : null);
    }

    [Serializable]
    public sealed class TargetKnockbackEffect : FixedDisplacementEffect
    {
        [Tooltip("开启时从命中双方位置求远离侧；关闭时使用 Accepted 冻结的身体攻击方向。")] public bool awayFromAttacker = true;
        public TargetKnockbackEffect() { trigger = ActionEffectTrigger.Contact; frequency = ActionEffectFrequency.TargetPerRelease; }
        public override string ValidationError() => base.ValidationError() ?? (trigger != ActionEffectTrigger.Contact ? "目标推移需要真实接触" : null);
    }

    [Serializable]
    public sealed class SelfRecoilEffect : FixedDisplacementEffect
    {
        public SelfRecoilEffect() { trigger = ActionEffectTrigger.Contact; frequency = ActionEffectFrequency.Release; }
        public override string ValidationError() => base.ValidationError() ?? (trigger != ActionEffectTrigger.Contact ? "反冲需要真实接触" : null);
    }

    [Serializable]
    public sealed class LaunchEffect : ActionEffectEntry
    {
        public Vector2 velocity = new Vector2(1.5f, 4);
        public bool target;
        public override string ValidationError() => base.ValidationError() ??
            (target && trigger != ActionEffectTrigger.Contact ? "目标腾空需要真实接触" :
             !Finite(velocity.x) || !Finite(velocity.y) || velocity.y <= 0 ? "腾空初速度无效" : null);
    }
}
