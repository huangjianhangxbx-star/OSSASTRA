namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using UnityEngine;

public class AnimationIntent
{
    /// <summary>
    /// 这个请求属于哪个角色。
    /// Composer 会过滤非本角色请求。
    /// </summary>
    public CharacterRoot root;

    /// <summary>
    /// 请求来源。
    /// 例如 "Movement" / "AutoAttack" / "Aim" / "Hurt"。
    /// 同一个 source 后提交的 intent 会覆盖旧 intent。
    /// </summary>
    public AnimationIntentSourceStateMachine intentSourceStateMatine;

    /// <summary>
    /// 逻辑动画 ID。
    /// 例如 Jog_Full / Attack_Normal_1。
    /// </summary>
    public EAnimSemantic semantic;

    /// <summary>
    /// 动画逻辑层。
    /// 第一阶段主要使用 BaseFullBody 和 FullBodyAction。
    /// </summary>
    public AnimationLayer layer;

    /// <summary>
    /// 优先级。
    /// 数字越大越优先。
    /// </summary>
    public int priority;

    /// <summary>
    /// 是否独占全身。
    /// true 时会压掉移动层、上下半身层。
    /// 第一阶段攻击动作建议 true。
    /// </summary>
    public bool exclusiveFullBody;

    /// <summary>
    /// 是否是一次性动作。
    /// 例如普通攻击、受击、翻滚。
    /// </summary>
    public bool oneShot;

    /// <summary>
    /// oneShot 存活时间。
    /// 小于等于 0 时表示不自动过期。
    /// </summary>
    public float duration;

    /// <summary>
    /// Composer 内部用。
    /// 表示这个 intent 什么时候过期。
    /// </summary>
    [System.NonSerialized]
    public float expireTime;

    /// <summary>
    /// oneShot 是否强制重播。
    /// 例如连续两次 Attack_Normal_1，如果不清 Track，Spine 可能不会重新播放。
    /// </summary>
    //public bool forceReplay;
    // 新增：forceReplay 是否已经被这次 Intent 消费过
    public bool forceReplayConsumed;
    /// <summary>
    /// 同一个动画名的第几次播放。
    /// 只有 replayId 改变时，HandleSystem 才真正重播。
    /// </summary>
    public int replayId;

    public System.Guid actionId, phaseId;
    public EAnimationLogicState logicId = EAnimationLogicState.None;
    public int lifeGeneration, presentationGeneration, configurationVersion;
    public string actorId, target = "body";
    public CharacterAnimationPlan frozenPlan;
    public bool IsValid(CharacterRoot owner, string targetId)
        => owner != null && root == owner && owner.IsAlive && owner.isActiveAndEnabled
        && actorId == owner.ActorId && lifeGeneration == owner.LifeGeneration && target == targetId;
    public AnimationIntent Clone()
    {
        return new AnimationIntent
        {
            root = root, actorId = actorId, target = target,
            actionId = actionId, phaseId = phaseId, lifeGeneration = lifeGeneration, logicId = logicId,
            presentationGeneration = presentationGeneration, configurationVersion = configurationVersion,
            frozenPlan = frozenPlan?.CloneFor(root),
            intentSourceStateMatine = intentSourceStateMatine,
            semantic = semantic,

            layer = layer,
            priority = priority,
            exclusiveFullBody = exclusiveFullBody,

            oneShot = oneShot,
            duration = duration,
            expireTime = expireTime,

            //forceReplay = forceReplay,
            replayId = replayId,

            forceReplayConsumed = forceReplayConsumed
        };
    }



}

public static class AnimationIntentSource
{
    public const string Movement = "Movement";
    public const string AutoAttack = "AutoAttack";
    public const string Aim = "Aim";
    public const string Hurt = "Hurt";
    public const string Skill = "Skill";
}
}
