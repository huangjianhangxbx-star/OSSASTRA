/*
 * 横版能力运动接口。有效能力提供下一步 XY 速度并接收真实运动结果。
 * 碰撞、固定步、深度与速度仍由原角色运动模块管理；接口不授权能力直接移动角色。
 */
using UnityEngine;

namespace Ossastra.Characters
{
    internal interface ISideViewAbilityMotion
    {
        bool TryPlanStep(float delta, out SideViewAbilityMotionCommand command);
        void ObserveStep(in SideViewAbilityMotionStep result);
    }

    internal readonly struct SideViewAbilityMotionCommand
    {
        public Vector2 Velocity { get; }
        public bool IsBounce { get; }

        private SideViewAbilityMotionCommand(Vector2 velocity, bool isBounce)
        {
            Velocity = velocity;
            IsBounce = isBounce;
        }

        public static SideViewAbilityMotionCommand Drive(Vector2 velocity)
            => new SideViewAbilityMotionCommand(velocity, false);

        public static SideViewAbilityMotionCommand Bounce(Vector2 velocity)
            => new SideViewAbilityMotionCommand(velocity, true);
    }

    internal readonly struct SideViewAbilityMotionStep
    {
        public Vector3 Before { get; }
        public Vector3 After { get; }
        public Vector3 AttackEnd { get; }
        public Vector3 TerrainNormal { get; }
        public bool HitGround { get; }
        public bool HitWall { get; }
        public bool HitHead { get; }
        public bool Grounded { get; }
        public bool TerrainBlocked => HitGround || HitWall || HitHead;

        internal SideViewAbilityMotionStep(Vector3 before, Vector3 after, Vector3 attackEnd, Vector3 terrainNormal,
            bool hitGround, bool hitWall, bool hitHead, bool grounded)
        {
            Before = before;
            After = after;
            AttackEnd = attackEnd;
            TerrainNormal = terrainNormal;
            HitGround = hitGround;
            HitWall = hitWall;
            HitHead = hitHead;
            Grounded = grounded;
        }
    }
}
