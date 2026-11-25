// 在正式横版攻击调试场 Play 中执行 Run。同步驱动现有本体的正式 TickSideView，
// 让 PhysX / CharacterController.Move 处理墙、平台与落地。结束时按原设置重置当前试验。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.CharacterCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Development.Editor
{
    public static class AttackLabImpactSpatialAcceptance
    {
        private const float Delta = 1f / 60;
        private static void Require(bool ok, string detail)
        {
            if (!ok) throw new InvalidOperationException(detail);
        }

        public static IEnumerator Run(AttackLabSession lab, List<string> lines)
        {
            if (!Application.isPlaying || !lab || lab.gameObject.scene.name != "SC_SideViewAttackLab" || !lab.Actor)
            {
                lines.Add("FAIL 空间验收需要正式 SC_SideViewAttackLab Play 中的玩家。");
                yield break;
            }
            var log = new StringBuilder("正式玩家 " + lab.Actor.ActorId + "。");
            var cases = Cases(lab, log);
            try
            {
                while (true)
                {
                    bool more;
                    try { more = cases.MoveNext(); }
                    catch (Exception ex)
                    {
                        lines.Add("FAIL " + log + " " + (ex.InnerException ?? ex));
                        break;
                    }
                    if (!more) { lines.Add("PASS " + log); break; }
                    yield return cases.Current;
                }
            }
            finally { (cases as IDisposable)?.Dispose(); }
        }

        private static IEnumerator Cases(AttackLabSession lab, StringBuilder log)
        {
            var actor = lab.Actor;
            var move = actor.GetComponent<CharacterMovement>();
            var body = actor.GetComponent<CharacterController>();
            var vitals = actor.Vitals;
            var status = actor.GetComponent<CombatStatusEffects>();
            Require(move && move.IsSideView && body && vitals && status, "正式角色需要横版运动器、胶囊、受击与控制组件");
            var method = typeof(CharacterMovement).GetMethod("TickSideView", BindingFlags.Instance | BindingFlags.NonPublic);
            var args = method.GetParameters();
            var frame = new object[]{ false, Delta, Enum.Parse(args[2].ParameterType,"Gravity"), 1f, false,
                Activator.CreateInstance(args[5].ParameterType), null, null, null };
            var previousCondition = lab.Condition;
            var previousStation = lab.Station;
            var previousDistance = lab.Distance;
            var previousHeight = lab.Height;
            var previousFacing = lab.FacingRight;
            var previousAttacks = lab.EnemyAttacks;
            var previousVersion = lab.Version;
            var curve = AnimationCurve.Linear(0, 0, 1, 1);
            var id = Guid.NewGuid();
            var stun = Guid.NewGuid();
            log.Append(" 胶囊 ID " + body.GetInstanceID() + "。");
            var impactSteps = 0;
            var airborne = false;
            var blocked = false;
            var plane = .27f;
            var maxDepthError = 0f;
            var movedWithSupport = false;
            SideViewMovingSupport temporarySupport = null;
            var clock = (LocalHitStopClock)typeof(CombatVitals).GetField("hitStop", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vitals);

            void Observe(SideMovementStep step)
            {
                if (step.Driver == SideMovementDriver.ActionImpact) ++impactSteps;
                blocked |= step.HorizontalBlocked;
                movedWithSupport |= step.MovingSupport;
                airborne |= !step.GroundedAfter;
                maxDepthError = Mathf.Max(maxDepthError, Mathf.Abs(step.WorldPosition.z - plane));
            }
            void Simulate(int n)
            {
                for (int i = 0; i < n; ++i) method.Invoke(move, frame);
            }
            void Reset(AttackLabStation station)
            {
                lab.Station = station;
                Require(lab.ResetTrial("ACT-IMPACT-01 玩家空间"), lab.Status);
                var start = actor.transform.position;
                start.z = plane;
                move.Teleport(start);
                Physics.SyncTransforms();
                Simulate(20);
                Require(move.Grounded, "测试起点未落地 " + station + "，坐标 " + actor.transform.position);
                impactSteps = 0;
                blocked = false;
                airborne = false;
            }
            void Request(float dx)
            {
                Require(ActionImpactMotor.TryDisplace(actor, id, Vector2.right * dx, .2f, curve, false, false, out var why),
                        "位移未进入生产接收器 " + why);
            }
            void SameBody()
            {
                Physics.SyncTransforms();
                var bounds = body.bounds;
                var world = actor.gameObject.scene.GetPhysicsScene();
                var overlaps = new Collider[64];
                int count = world.OverlapSphere(bounds.center, body.radius * .5f, overlaps, Physics.AllLayers, QueryTriggerInteraction.Collide);
                var hitCollider = overlaps.Take(count).FirstOrDefault(c => c == body) ??
                    overlaps.Take(count).FirstOrDefault(c => ReferenceEquals(CombatSpatialTarget.Find(c), vitals));
                if (!hitCollider)
                {
                    var nearby = new Collider[64];
                    int n = world.OverlapSphere(bounds.center, 3, nearby, Physics.AllLayers, QueryTriggerInteraction.Collide);
                    string own = string.Join(", ", actor.GetComponentsInChildren<Collider>(true).Select(c =>
                        $"{c.name}/{c.GetType().Name}@{c.bounds.center} enabled={c.enabled} trigger={c.isTrigger} layer={c.gameObject.layer}"));
                    string broad = string.Join(", ", nearby.Take(n).Select(c =>
                        $"{c.name}/{c.GetType().Name}@{c.bounds.center} id={CombatSpatialTarget.Find(c)?.Health?.Id}"));
                    throw new InvalidOperationException($"角色局部球查询 count={count}, center={bounds.center}, scene={actor.gameObject.scene.name}, " +
                        $"defaultWorld={world == Physics.defaultPhysicsScene}, detectCollisions={body.detectCollisions}, overlapRecovery={body.enableOverlapRecovery}。自身 [{own}]，三米查询 [{broad}]");
                }
                var hitTarget = CombatSpatialTarget.Find(hitCollider);
                Require(ReferenceEquals(hitTarget, vitals),
                    $"正式接触映射错误，碰撞体 {hitCollider.GetInstanceID()} 映射到 {hitTarget?.Health?.Id}，期望 {vitals.Health?.Id}");
                log.Append(hitCollider == body ? " 本体胶囊命中。" : " 本体受击形状命中 " + hitCollider.name + "。");
                var anchor = CombatSpatialTarget.Position(hitTarget);
                Require(Vector3.Distance(anchor, actor.transform.position) < .002f,
                    $"受击锚点 {anchor} 与角色根 {actor.transform.position} 不一致");
                var expectedCenter = actor.transform.TransformPoint(body.center);
                Require(Vector3.Distance(bounds.center, expectedCenter) < .012f,
                    $"胶囊世界中心 {bounds.center} 与配置中心 {expectedCenter} 不一致");
            }
            move.SideStepCompleted += Observe;
            try
            {
                lab.Condition = AttackLabTargetMode.Whiff;
                lab.FacingRight = true;
                lab.Height = 0;
                lab.Distance = 4;
                lab.EnemyAttacks = false;

                Reset(AttackLabStation.Flat);
                var start = actor.transform.position;
                Request(1.5f);
                Simulate(30);
                var flat = actor.transform.position.x - start.x;
                Require(Mathf.Abs(flat - 1.5f) < .04f && impactSteps > 0, "平地位移 " + flat + "，正式运动步 " + impactSteps);
                yield return new WaitForFixedUpdate();
                yield return null;
                SameBody();
                log.Append(" 平地 " + flat.ToString("F3") + " m，正式近战查询映射至移动后本体。");

                Reset(AttackLabStation.Wall);
                start = actor.transform.position;
                Request(1.5f);
                Simulate(30);
                var actual = actor.transform.position.x - start.x;
                var wall = (Transform)typeof(AttackLabSession).GetField("wall", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lab);
                var wallFace = wall.GetComponent<Collider>().bounds.min.x;
                Require(blocked && actual >= -.02f && actual < flat - .2f && body.bounds.max.x <= wallFace + body.skinWidth + .035f,
                        "墙：x=" + actual + "，胶囊最右=" + body.bounds.max.x + "，墙面=" + wallFace + "，阻挡=" + blocked);
                yield return new WaitForFixedUpdate();
                yield return null;
                SameBody();
                log.Append(" 同一请求顶墙只走 " + actual.ToString("F3") + " m，墙面 " + wallFace.ToString("F3") + "。");

                Reset(AttackLabStation.Drop);
                start = actor.transform.position;
                Request(2.25f);
                Simulate(110);
                var end = actor.transform.position;
                Require(airborne && move.Grounded && end.x > start.x + 1.5f && end.y < start.y - 1.2f,
                        "台沿/落地：" + start + " -> " + end + "，离地=" + airborne + "，着地=" + move.Grounded);
                yield return new WaitForFixedUpdate();
                yield return null;
                SameBody();
                Require(maxDepthError < .003f, "横版深度误差 " + maxDepthError);
                log.Append(" 真实高台沿下落 " + (start.y-end.y).ToString("F3") + " m 后着地；三组 Z 最大误差 " + maxDepthError.ToString("F5") + " m。");

                var platform = (Transform)typeof(AttackLabSession).GetField("playerPlatform", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lab);
                if (!platform.GetComponent<SideViewMovingSupport>()) temporarySupport = platform.gameObject.AddComponent<SideViewMovingSupport>();
                Reset(AttackLabStation.Drop);
                var supportedAt = actor.transform.position.x;
                movedWithSupport = false;
                Request(.3f);
                platform.position += Vector3.right * .2f;
                Physics.SyncTransforms();
                Simulate(1);
                float firstStep = actor.transform.position.x - supportedAt;
                Require(movedWithSupport && firstStep > .18f && firstStep < .26f,
                    "移动平台当步未叠加：步长 " + firstStep + "，支撑识别 " + movedWithSupport);
                Simulate(25);
                float carried = actor.transform.position.x - supportedAt;
                Require(Mathf.Abs(carried - .5f) < .04f, "动态平台 0.2 + 主动 0.3 实际 " + carried);
                log.Append(" 动态托台 0.20 + 自身 0.30，实际 " + carried.ToString("F3") + " m。");

                Reset(AttackLabStation.Flat);
                start = actor.transform.position;
                Request(.6f);
                Simulate(2);
                float beforeStop = actor.transform.position.x;
                int testFrame = Time.frameCount;
                double testTime = Time.unscaledTimeAsDouble;
                Require(clock.Request(id, .1f, testFrame, testTime) &&
                        clock.Advance(++testFrame, testTime + .01) == HitStopEdge.Started && actor.IsLocalHitStop,
                        "本体卡肉时钟未进入");
                int previousStepCount = impactSteps;
                for (int i=0; i<4; i++) actor.StateArbitrator.ManagedUpdate(Delta);
                Require(impactSteps == previousStepCount && Mathf.Abs(actor.transform.position.x-beforeStop) < .001f &&
                        actor.GetComponent<ActionImpactMotor>().HasMotion, "局部卡肉时仲裁器推进或清空了位移");
                Require(clock.Advance(++testFrame, testTime + .13) == HitStopEdge.Ended && !actor.IsLocalHitStop,
                        "未离开本体局部卡肉");
                Simulate(1);
                float resumedStep = actor.transform.position.x - beforeStop;
                Simulate(16);
                Require(Mathf.Abs(resumedStep - .05f) < .015f &&
                        Mathf.Abs(actor.transform.position.x - start.x - .6f) < .025f, "卡肉恢复补跳或漏位移 " + resumedStep);
                log.Append(" 真仲裁器卡肉四次更新步数未增，解冻第一步 " + resumedStep.ToString("F3") + " m。");

                Reset(AttackLabStation.Flat);
                start = actor.transform.position;
                Request(2);
                Simulate(2);
                var stoppedAt = actor.transform.position.x;
                var motor = actor.GetComponent<ActionImpactMotor>();
                Require(motor && motor.HasMotion && stoppedAt > start.x, "受控前未开始");
                status.ApplyStun(stun, 1);
                Require(status.BlocksIntent, "受控未进入");
                Simulate(5);
                Require(!motor.HasMotion && Mathf.Abs(actor.transform.position.x - stoppedAt) < .015f, "眩晕未清位移");
                status.Remove(stun);
                Simulate(20);
                Require(!status.BlocksIntent && Mathf.Abs(actor.transform.position.x - stoppedAt) < .015f, "解控后旧位移继续");
                Request(.5f);
                Simulate(25);
                Require(Mathf.Abs(actor.transform.position.x - stoppedAt - .5f) < .025f, "解控后新请求未恢复");
                log.Append(" 眩晕中断余量，解控后原地保持，新请求独立前移 0.50 m。");
            }
            finally
            {
                move.SideStepCompleted -= Observe;
                status.Remove(stun);
                clock.Reset();
                if (temporarySupport) UnityEngine.Object.DestroyImmediate(temporarySupport);
                lab.Condition = previousCondition; lab.Station = previousStation;
                lab.Distance = previousDistance; lab.Height = previousHeight;
                lab.FacingRight = previousFacing; lab.EnemyAttacks = previousAttacks;
                lab.ResetTrial(previousVersion);
            }
        }
    }
}
