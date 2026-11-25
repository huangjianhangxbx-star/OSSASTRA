// 在现有独立手感场通过正式动作和命中网关量取双方根位移。临时候选仅属于本次 Play。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ossastra.CharacterAnimation;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    [InitializeOnLoad]
    internal static class AttackLabImpactAcceptance
    {
        private const string Pending = "OSSASTRA.ACT-IMPACT-01.Pending";
        private static bool started;
        static AttackLabImpactAcceptance() => EditorApplication.update += Tick;

        [MenuItem("OSSASTRA/战斗/验证招式效果")]
        internal static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SideViewAttackLabAcceptance.IsRunning) return;
            SessionState.SetBool(Pending, true);
            if (!SideViewAttackLabWindow.Launch()) SessionState.EraseBool(Pending);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || started || !Application.isPlaying ||
                SceneManager.GetActiveScene().path != AttackLabSceneBuilder.ScenePath) return;
            var lab = Object.FindObjectOfType<AttackLabSession>();
            if (!lab || !lab.Actor || lab.Generation == 0) return;
            started = true;
            lab.StartCoroutine(Verify(lab));
        }

        // 展开迭代器以报告异常，即使某个试验失败也能恢复编辑场。
        private static IEnumerator Verify(AttackLabSession lab)
        {
            var lines = new List<string>();
            var old = ActionEffectList.EditorOverride;
            var draft = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var steps = Cases(lab, draft, keyboard, pad, lines);
            try
            {
                while (true)
                {
                    bool more;
                    try { more = steps.MoveNext(); }
                    catch (Exception e) { lines.Add("FAIL " + e); break; }
                    if (!more) break;
                    yield return steps.Current;
                }
            }
            finally
            {
                (steps as IDisposable)?.Dispose();
                ActionEffectList.EditorOverride = old;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                Object.Destroy(draft);
                var folder = Path.Combine(Application.dataPath, "../Reports/SideViewAttackLab");
                Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, "招式效果验收.txt"), lines);
                SessionState.EraseBool(Pending);
                started = false;
                EditorApplication.isPlaying = false;
            }
        }

        private static IEnumerator Cases(AttackLabSession lab, ActionDefinitionSO draft, Keyboard keyboard, Gamepad pad, List<string> lines)
        {
            int actorIndex = -1;
            for (int i = 0; i < lab.Party.Characters.Count; ++i)
                if (lab.Party.Characters[i].ActorId == "temporary-a") actorIndex = i;
            if (actorIndex < 0 || !lab.SwitchActor(actorIndex)) throw new InvalidOperationException("缺少主角或控制权");
            bool skill = false;
            ActionEffectList.EditorOverride = (actor, source) =>
                actor == lab.Actor && (skill ? source is AbilityDefinitionSO : source is ActionDefinitionSO) ? draft.effects : null;
            lab.Station = AttackLabStation.Flat;
            lab.EnemyAttacks = false;
            lab.Distance = 1.25f;
            lab.Trace.Detailed = true;
            float baseMove = 0;
            for (int test = 0; test < 12; test++)
            {
                skill = test == 9;
                lab.Condition = test <= 2 || test >= 7 && test != 9 ? AttackLabTargetMode.Whiff :
                    test == 5 ? AttackLabTargetMode.Heavy : test == 6 ? AttackLabTargetMode.Immune : AttackLabTargetMode.Light;
                lab.FacingRight = test != 2;
                int preset = test == 0 || test == 1 || test == 2 ? 3 : test == 3 ? 1 :
                    test == 7 ? 6 : test == 8 || test >= 10 ? 0 : 2;
                AttackLabImpactSamples.Configure(draft, preset);
                draft.effects.bypass = test == 0;
                if (test == 8 || test >= 10) draft.effects.entries.RemoveAll(e => !(e is InputMovementScaleEffect));
                if (test == 11) ((InputMovementScaleEffect)draft.effects.entries[0]).allowed = ActionInputDirection.Forward;
                if (!lab.ResetTrial("ACT-IMPACT-" + test)) throw new InvalidOperationException(lab.Status);
                yield return new WaitForSeconds(.15f);
                var p = lab.Actor.transform;
                var t = lab.Target.transform;
                Vector3 from = p.position, other = t.position;
                var hp = lab.Target.Vitals.Health.Current;
                if (skill) lab.SelectSkillSlot(0);
                bool accepted = skill ? lab.RequestSkill() : lab.RequestAttack();
                bool held = test == 10 || test == 11;
                if (held) InputSystem.QueueStateEvent(keyboard, new KeyboardState(test == 10 ? Key.D : Key.A));
                float height = 0, motionDuringInput = 0;
                float until = Time.time + .95f;
                while (Time.time < until)
                {
                    height = Mathf.Max(height, p.position.y - from.y);
                    if (held && lab.Actor.ActionActive) motionDuringInput = Mathf.Max(motionDuringInput, Mathf.Abs(p.position.x - from.x));
                    if (held && Time.time > until - .82f)
                    { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); held = false; }
                    yield return null;
                }
                float x = p.position.x - from.x, target = t.position.x - other.x;
                float damage = hp - lab.Target.Vitals.Health.Current;
                if (test == 0) baseMove = x;
                bool correct = test switch
                {
                    0 => damage == 0, 1 => Mathf.Abs(x - baseMove + .6f) <= .035f,
                    2 => Mathf.Abs(x + baseMove - .6f) <= .035f,
                    3 => damage > 0 && target > .25f && Mathf.Abs(x - .12f) < .045f,
                    4 => damage > 0 && Mathf.Abs(target - .8f) <= .05f,
                    5 => damage > 0 && Mathf.Abs(target - .32f) <= .04f,
                    6 => damage > 0 && Mathf.Abs(target) <= .025f,
                    7 => height > .08f && Mathf.Abs(p.position.y - from.y) < .08f,
                    8 => Mathf.Abs(x - baseMove) < .05f,
                    9 => damage > 0 && target > .1f,
                    10 => motionDuringInput > .012f && motionDuringInput < .26f,
                    _ => motionDuringInput < .05f
                };
                lines.Add($"{(accepted && correct ? "PASS" : "FAIL")} 测例 {test} / {lab.Condition} / {AttackLabImpactSamples.Labels[preset]}；受理={accepted}，自身 Δx={x:F3}，目标 Δx={target:F3}，峰高={height:F3}，输入段位移={motionDuringInput:F3}，HP 差={damage}，{lab.Status}");
            }
            // 同一次释放命中双靶，两份正常受击与一次发动者反冲应同时成立。
            skill = false;
            foreach (var mode in new[] { AttackLabTargetMode.Pair, AttackLabTargetMode.Guarded, AttackLabTargetMode.Lethal })
            {
                lab.Condition = mode;
                AttackLabImpactSamples.Configure(draft, 1);
                draft.effects.entries.RemoveAll(e => !(e is SelfRecoilEffect));
                if (!lab.ResetTrial(mode.ToString())) throw new InvalidOperationException(lab.Status);
                yield return new WaitForSeconds(.12f);
                float origin = lab.Actor.transform.position.x;
                var first = lab.Target.Vitals.Health.Current;
                var second = lab.PairedTarget.Vitals.Health.Current;
                GameObject secondShape = null;
                bool accepted;
                try
                {
                    if (mode == AttackLabTargetMode.Pair)
                    {
                        secondShape = new GameObject("同一目标的第二个受击形状");
                        secondShape.transform.SetParent(lab.Target.transform, false);
                        secondShape.layer = lab.Target.gameObject.layer;
                        var shape = secondShape.AddComponent<SphereCollider>();
                        shape.isTrigger = true;
                        shape.center = lab.Target.GetComponent<CharacterController>().center;
                        shape.radius = .16f;
                        Physics.SyncTransforms();
                    }
                    accepted = lab.RequestAttack();
                    yield return new WaitForSeconds(.85f);
                }
                finally { if (secondShape) Object.Destroy(secondShape); }
                float delta = lab.Actor.transform.position.x - origin;
                int hp = first - lab.Target.Vitals.Health.Current;
                int peer = second - lab.PairedTarget.Vitals.Health.Current;
                bool ok = mode == AttackLabTargetMode.Guarded ? Mathf.Abs(delta) < .02f && hp == 0 :
                    Mathf.Abs(delta + .12f) < .035f && hp > 0 && (mode != AttackLabTargetMode.Pair || hp == 12 && peer == hp);
                lines.Add($"{(accepted && ok ? "PASS" : "FAIL")} {mode} 反冲 Δx={delta:F3}, HP 差 {hp}/{peer}");
            }
            lab.Condition = AttackLabTargetMode.Whiff;
            lab.ResetTrial("手柄微移");
            AttackLabImpactSamples.Configure(draft, 0);
            draft.effects.entries.RemoveAll(e => !(e is InputMovementScaleEffect));
            yield return new WaitForSeconds(.15f);
            var actor = lab.Actor;
            float start = actor.transform.position.x;
            bool attack = lab.RequestAttack();
            var movement = actor.GetComponent<CharacterMovement>();
            var animation = actor.GetComponentInChildren<CharacterAnimationController>();
            var spine = actor.GetComponentInChildren<CharacterSpineView>();
            int movingSteps = 0, footstepSteps = 0;
            string movingClip = null;
            void OnMovingStep(SideMovementStep step)
            {
                if (!actor.ActionActive || Mathf.Abs(step.InputHorizontalSpeed) < .01f) return;
                ++movingSteps;
                if (step.AllowsFootstep) ++footstepSteps;
                if (!actor.DataBus.PresentsLocomotion)
                    movingClip = animation ? animation.CurrentClip : spine ? spine.CurrentClip : null;
            }
            movement.SideStepCompleted += OnMovingStep;
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                yield return new WaitForSeconds(.13f);
            }
            finally
            {
                movement.SideStepCompleted -= OnMovingStep;
                InputSystem.QueueStateEvent(pad, new GamepadState());
            }
            yield return new WaitForSeconds(.1f);
            var displacement = actor.transform.position.x - start;
            bool heldPose = !string.IsNullOrEmpty(movingClip) && movingClip.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0 &&
                movingClip.IndexOf("move", StringComparison.OrdinalIgnoreCase) < 0 && movingClip.IndexOf("walk", StringComparison.OrdinalIgnoreCase) < 0;
            lines.Add($"{(attack && displacement > .01f && displacement < .27f && movingSteps > 0 && footstepSteps == 0 && heldPose ? "PASS" : "FAIL")} 手柄全身攻击微移 {displacement:F3}，移动步 {movingSteps}，允许脚步 {footstepSteps}，播放片段 {movingClip}");

            lab.Condition = AttackLabTargetMode.Light;
            lab.ResetTrial("挑飞落地");
            AttackLabImpactSamples.Configure(draft, 5);
            yield return new WaitForSeconds(.15f);
            float floor = lab.Target.transform.position.y;
            float peak = 0;
            attack = lab.RequestAttack();
            float end = Time.time + 1.2f;
            while (Time.time < end)
            { peak = Mathf.Max(peak, lab.Target.transform.position.y - floor); yield return null; }
            float landed = lab.Target.transform.position.y - floor;
            lines.Add($"{(attack && peak > .15f && Mathf.Abs(landed) < .08f ? "PASS" : "FAIL")} 目标挑飞峰高 {peak:F3}，落地高度差 {landed:F3}");

            lab.Condition = AttackLabTargetMode.Normal;
            lab.EnemyAttacks = true;
            lab.ResetTrial("敌方出招");
            AttackLabImpactSamples.Configure(draft, 2);
            var binding = lab.Target.gameObject.AddComponent<EnemyActionImpactBinding>();
            binding.effectSource = draft;
            var p0 = actor.transform.position;
            var hp0 = actor.Vitals.Health.Current;
            yield return new WaitForSeconds(2.5f);
            displacement = actor.transform.position.x - p0.x;
            lines.Add($"{(hp0 > actor.Vitals.Health.Current && displacement < -.05f ? "PASS" : "FAIL")} 敌方正式出招 {lab.Target.LastFact}，玩家 HP 差 {hp0-actor.Vitals.Health.Current}，Δx={displacement:F3}");
            Object.Destroy(binding);
            lab.EnemyAttacks = false;
            yield return null;
            lab.Distance = .95f;
            lab.ResetTrial("敌方起手受轻推");
            AttackLabImpactSamples.Configure(draft, 0);
            draft.effects.entries.RemoveAll(entry => !(entry is TargetKnockbackEffect));
            var opponent = lab.Target;
            float x0 = opponent.transform.position.x;
            int opponentHp = opponent.Vitals.Health.Current;
            Guid enemyAction = Guid.Empty;
            bool hitDuringEnemyAttack = false, enemyReleased = false;
            void ObserveEnemyAction(ActionFact fact)
            {
                if (fact.Kind == ActionFactKind.Accepted && enemyAction == Guid.Empty)
                {
                    enemyAction = fact.ActionId;
                    lab.RequestAttack();
                }
                if (fact.Kind == ActionFactKind.Released && fact.ActionId == enemyAction)
                    enemyReleased = true;
            }
            opponent.ActionObserved += ObserveEnemyAction;
            try
            {
                lab.EnemyAttacks = true;
                opponent.AttacksEnabled = true;
                // 给训练靶原有的决策间隔和起手都留时间，实际释放后可立即结束观察。
                float limit = Time.time + 4f;
                while (Time.time < limit && !(hitDuringEnemyAttack && enemyReleased))
                {
                    hitDuringEnemyAttack |= opponent.ActionActive && opponent.ActionId == enemyAction &&
                        opponent.Vitals.Health.Current < opponentHp;
                    yield return null;
                }
            }
            finally
            {
                opponent.ActionObserved -= ObserveEnemyAction;
                opponent.AttacksEnabled = false;
                lab.EnemyAttacks = false;
            }
            var pushDistance = opponent.transform.position.x - x0;
            lines.Add($"{(hitDuringEnemyAttack && enemyReleased && pushDistance > .03f ? "PASS" : "FAIL")} 出招中的敌人受推 {pushDistance:F3}，原攻击期间承伤 {hitDuringEnemyAttack}，同一动作释放 {enemyReleased}");
            ActionEffectList.EditorOverride = null;
            var spatial = AttackLabImpactSpatialAcceptance.Run(lab, lines);
            try { while (spatial.MoveNext()) yield return spatial.Current; }
            finally { (spatial as IDisposable)?.Dispose(); }
            var legacy = AttackLabImpactLegacyAcceptance.Cases(lab, lines, keyboard);
            try { while (legacy.MoveNext()) yield return legacy.Current; }
            finally { (legacy as IDisposable)?.Dispose(); }
        }
    }
}
