/*
 * 横版运动参数。普通跳基于本机丝之歌数据；墙滑及挂边速度为项目候选，蹬墙跳按能力单独控制。
 * 只有装配此资产的角色走横版运动入口。
 */
using System;
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
        public SideViewTraversalSettings traversal = new SideViewTraversalSettings();
    }

    [Serializable]
    public sealed class SideViewTraversalSettings
    {
        public bool enabled = true;
        [Tooltip("允许蹬墙起跳。关闭时仍可墙滑、挂边和翻越。")]
        public bool allowWallJump;
        public LayerMask surfaces = Physics.DefaultRaycastLayers;
        [Min(.01f)] public float wallReach = .12f;
        [Min(0f)] public float wallSlideStickSeconds = .15f;
        [Min(.01f)] public float wallSlideAcceleration = 24f;
        [Min(.01f)] public float wallSlideMaxSpeed = 4.8f;
        // 原作墙跳横向初速为 25；现有横版房间尺度使用较短的蹬离距离。
        [Min(.01f)] public float wallJumpKickSpeed = 13f;
        [Min(.01f)] public float wallJumpUpSpeed = 16f;
        [Min(0)] public int wallJumpShortLockSteps = 5;
        [Min(1)] public int wallJumpLongLockSteps = 15;
        [Min(1)] public int wallJumpSameWallSteps = 10;
        [Min(.02f)] public float gripBelowHand = .15f;
        [Min(.02f)] public float gripAboveHand = .38f;
        [Min(0f)] public float gripMaxRisingSpeed = 2f;
        [Min(.02f)] public float surfaceClearance = .065f;
        [Min(.1f)] public float grabSpeed = 12f;
        [Min(.1f)] public float climbUpSpeed = 5.8f;
        [Min(.1f)] public float climbAcrossSpeed = 4.8f;
    }
}
