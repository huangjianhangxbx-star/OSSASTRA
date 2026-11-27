/*
 * 功能：在原横版攻击调试场验证目标运动的到位、阻挡、拒绝和独立取消。
 * 边界：使用正式敌人运动器和实际碰撞，结束后恢复调试场原状。
 */
using System;
using Ossastra.Combat;
using Ossastra.Enemies;
using UnityEngine;
using LabEnemy = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.Development.Editor
{
    public static class AttackLabTargetMotionAcceptance
    {
        private const float Delta = 1f / 60f;

        public static string Verify(AttackLabSession lab)
        {
            Require(Application.isPlaying && lab && lab.gameObject.scene.name == "SC_SideViewAttackLab",
                "目标运动验收需要现有攻击调试场处于 Play。");
            var previousMode = lab.Condition;
            var previousStation = lab.Station;
            var previousDistance = lab.Distance;
            var previousHeight = lab.Height;
            var previousFacing = lab.FacingRight;
            var previousAttacks = lab.EnemyAttacks;
            var previousVersion = lab.Version;
            try
            {
                lab.FacingRight = true;
                lab.Distance = 2.8f;
                lab.Height = 0;
                lab.EnemyAttacks = false;
                var target = Reset(lab, AttackLabStation.Flat, AttackLabTargetMode.Light);
                Require(Begin(target, out var arrival, out var reason), reason);
                Step(target, 36);
                Require(arrival.Status == ActionImpactMotor.TargetMotionStatus.Arrived,
                    $"畅通地形应真正到位，当前 {arrival.Status} / {arrival.Reason}");
                Require(Mathf.Abs(target.transform.position.x - .7f) < .07f,
                    $"到位时实际位置错误 {target.transform.position.x:0.###}");

                target = Reset(lab, AttackLabStation.Wall, AttackLabTargetMode.Light);
                Require(Begin(target, out var blocked, out reason), reason);
                Step(target, 60);
                Require(blocked.Status == ActionImpactMotor.TargetMotionStatus.Failed &&
                    target.transform.position.x > 1.25f, $"实体墙需要拦截运动 {blocked.Status} / {target.transform.position.x:0.###}");

                target = Reset(lab, AttackLabStation.Flat, AttackLabTargetMode.Immune);
                Require(!Begin(target, out _, out reason) && !string.IsNullOrEmpty(reason),
                    "完全免推目标错误接受运动。");

                target = Reset(lab, AttackLabStation.Flat, AttackLabTargetMode.Light);
                Require(Begin(target, out var cancelled, out reason), reason);
                Require(!Begin(target, out _, out _), "另一个请求错误抢占进行中的目标运动。");
                cancelled.Cancel();
                Require(cancelled.Status == ActionImpactMotor.TargetMotionStatus.Cancelled, "未收到本次取消结果。");
                Require(Begin(target, out var current, out reason), reason);
                cancelled.Cancel();
                Require(current.Status == ActionImpactMotor.TargetMotionStatus.Pending,
                    "重复取消已结束的请求错误中断后继请求。");
                var motor = target.GetComponent<ActionImpactMotor>();
                Require(ActionImpactMotor.TryDisplace(target, Guid.NewGuid(), Vector2.right, .25f,
                    null, true, false, out reason), reason);
                current.Cancel();
                var before = target.transform.position.x;
                Step(target, 20);
                Require(target.transform.position.x > before + .5f && !motor.HasMotion,
                    "本次取消误删另一来源的普通推移，或推移没有按时结束。");
                return "通过：实际到位、实体墙阻挡、免推拒绝、请求独占、幂等取消及普通推移保留。";
            }
            finally
            {
                lab.Condition = previousMode;
                lab.Station = previousStation;
                lab.Distance = previousDistance;
                lab.Height = previousHeight;
                lab.FacingRight = previousFacing;
                lab.EnemyAttacks = previousAttacks;
                lab.ResetTrial(previousVersion);
            }
        }

        private static LabEnemy Reset(AttackLabSession lab, AttackLabStation station, AttackLabTargetMode mode)
        {
            lab.Station = station;
            lab.Condition = mode;
            Require(lab.ResetTrial("目标运动合同验收"), lab.Status);
            var target = lab.Target;
            Require(target && target.TryGetComponent<EnemyMovement>(out var move) && move.isActiveAndEnabled,
                "训练靶必须由正式敌人移动器驱动。");
            return target;
        }

        private static bool Begin(LabEnemy target, out ActionImpactMotor.TargetMotion motion, out string reason)
            => ActionImpactMotor.TryMoveTarget(target, Guid.NewGuid(), new Vector2(.7f, target.transform.position.y),
                8, .5f, .06f, .15f, out motion, out reason);

        private static void Step(LabEnemy target, int frames)
        {
            var move = target.GetComponent<EnemyMovement>();
            for (int i = 0; i < frames; i++) move.Tick(Vector3.zero, Delta);
        }

        private static void Require(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException(reason);
        }
    }
}
