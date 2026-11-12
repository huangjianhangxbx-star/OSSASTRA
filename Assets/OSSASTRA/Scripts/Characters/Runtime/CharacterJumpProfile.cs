/*
 * 横版运动参数。来源为本机丝之歌普通跳跃及 50 Hz 物理设置，单位保持原复刻场的米与秒。
 * 只有装配此资产的角色走横版运动入口。
 */
using UnityEngine;

namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName = "OSSASTRA/Character/Side View Jump Profile")]
    public sealed class CharacterJumpProfile : ScriptableObject
    {
        [Min(.001f)] public float stepSeconds = .02f;
        [Min(.01f)] public float runSpeed = 8.25f;
        [Min(.01f)] public float jumpSpeed = 18.6f;
        [Min(.01f)] public float gravity = 60f;
        [Min(.01f)] public float fallSpeed = 30f;
        [Min(1)] public int sustainSteps = 9;
        [Min(0)] public int minimumSustainSteps = 2;
        [Min(0)] public int queueSteps = 2;
        [Min(0)] public int ledgeGraceSteps = 4;
        [Min(0)] public int headLockSteps = 3;
        [Range(0f, 1f)] public float releaseSpeedScale = .5f;
        [Range(0f, 1f)] public float hangInitialGravity = .1f;
        [Min(0f)] public float hangGravityRamp = 5f;
    }
}
