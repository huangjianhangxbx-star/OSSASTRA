using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.UserInterface;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ossastra.Development.Editor
{
    /// <summary>在独立训练场通过正式输入、装备、结算与采集验证边界。由菜单启动并自动回到 Edit。</summary>
    [InitializeOnLoad]
    internal static class SideViewAttackLabAcceptance
    {
        private const string Pending = "Ossastra.AttackLab.Acceptance";
        private const string Report = "Reports/SideViewAttackLab/边界验收.txt";
        private static readonly List<string> lines = new List<string>();
        private static readonly object captureOwner = new object();
        private static AttackLabSession lab;
        private static CharacterInputHandler input;
        private static CharacterMainAction main;
        private static Gamepad turnPad;
        private static SideViewCameraFollow cameraFollow;
        private static float initialShake;
        private static int failures, originalSkill, originalActor;
        private static string originalProfile, originalVersion;
        private static AttackLabTargetMode originalCondition;
        private static AttackLabStation originalStation;
        private static float originalDistance, originalHeight;
        private static bool originalRight, originalEnemyAttacks, originalFrozen, originalDetailed, started;
        private static IEnumerator routine;
        private static double deadline;

        static SideViewAttackLabAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnMode;
        }

        [MenuItem("OSSASTRA/战斗/验证横版攻击调试台", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(Pending, false);

        [MenuItem("OSSASTRA/战斗/验证横版攻击调试台")]
        private static void Run()
        {
            if (!CanRun()) return;
            lines.Clear();
            failures = 0;
            SessionState.SetBool(Pending, true);
            if (!SideViewAttackLabWindow.Launch()) Complete("启动独立训练场失败");
        }

        private static void OnMode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode) deadline = EditorApplication.timeSinceStartup + 35;
            if (mode == PlayModeStateChange.ExitingPlayMode) Complete("验收进行时退出了 Play");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 35;
            if (EditorApplication.timeSinceStartup > deadline) { Complete("超时。"+Context()); return; }
            if (started) return;
            lab = UnityEngine.Object.FindObjectOfType<AttackLabSession>();
            if (!lab || !lab.Actor || lab.Generation == 0) return;
            started = true;
            originalVersion = lab.Version;
            originalActor = lab.Party.Characters.ToList().IndexOf(lab.Actor);
            originalSkill = lab.SkillSlot;
            originalCondition = lab.Condition;
            originalStation = lab.Station;
            originalDistance = lab.Distance;
            originalHeight = lab.Height;
            originalEnemyAttacks = lab.EnemyAttacks;
            originalRight = lab.FacingRight;
            originalFrozen = lab.Trace.Frozen;
            originalDetailed = lab.Trace.Detailed;
            cameraFollow = UnityEngine.Object.FindObjectOfType<SideViewCameraFollow>();
            if (cameraFollow) initialShake = cameraFollow.ShakeSettings.intensity;
            routine = Cases();
            lab.StartCoroutine(Guard());
        }

        private static IEnumerator Guard()
        {
            while (SessionState.GetBool(Pending, false))
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception error) { Complete(error.ToString()); yield break; }
                if (!more) { Complete(null); yield break; }
                yield return routine.Current;
            }
        }

        private static void Check(bool passed, string description)
        {
            if (!passed) failures++;
            lines.Add((passed ? "PASS " : "FAIL ") + description);
        }

        private static void Reset(string version)
        {
            lab.Trace.Frozen = false;
            if (!lab.ResetTrial(version)) throw new InvalidOperationException(lab.Status);
        }

        private static string Context()
        {
            if (!lab || !lab.Actor || !input) return "尚未取得可用角色";
            var bus = lab.Actor.DataBus;
            var frame = bus.Input;
            return $"代次 {lab.Generation}，实控 {lab.Actor.ActorId}，设定向右 {lab.FacingRight}，实际瞄准 {bus.AttackDirection}，显示 {bus.VisualFacingX}，输入源 {frame.aimSource}，输入方向 {frame.aimDirection}，指针 {frame.hasPointer}/{frame.pointer}，记忆 {input.InputSources.LastAimDirection}，焦点 {Application.isFocused}，请求 {lab.Status}";
        }

        private static AttackLabEvent[] Rows() => lab.Trace.Events.Where(e => e.trial == lab.Generation).ToArray();

        private static bool Send(Func<bool> command)
        {
            input.SetInventoryCapture(captureOwner, false, true);
            lab.Party.SetInventoryCapture(captureOwner, false, true);
            try { return command(); }
            finally
            {
                input.SetInventoryCapture(captureOwner, true, true);
                lab.Party.SetInventoryCapture(captureOwner, true, true);
            }
        }
        private static IEnumerator Cases()
        {
            var members = lab.Party.Characters.ToList();
            int indexA = members.FindIndex(c => c && c.ActorId == "temporary-a");
            if (indexA < 0 || indexA != originalActor && !lab.SwitchActor(indexA))
                throw new InvalidOperationException("找不到可用角色 A。"+lab.Status);
            input = lab.Actor.GetComponent<CharacterInputHandler>();
            input.SetInventoryCapture(captureOwner, true, true);
            lab.Party.SetInventoryCapture(captureOwner, true, true);
            main = lab.Actor.GetComponent<CharacterMainAction>();
            originalProfile = main.SelectedId;
            lab.EnemyAttacks = false;
            lab.Station = AttackLabStation.Flat;
            lab.Distance = 1.2f;
            lab.Height = 0;
            lab.FacingRight = true;
            lab.Condition = AttackLabTargetMode.Whiff;
            lab.Trace.Detailed = true;
            Reset("主手与技能槽回归");
            yield return null;
            var choices = main.Choices.Where(p => p).ToArray();
            if (choices.Length < 2) throw new InvalidOperationException("角色 A 没有登记第二主手动作组");
            var profile = choices[1];
            if (!main.Configure(profile.definitionId)) throw new InvalidOperationException("切换第二主手动作组失败："+main.LastResult);
            lab.Actor.StateArbitrator.Combat.RebindLaboratorySequence();
            var skills = lab.Actor.GetComponent<CharacterAbilities>();
            var skill = skills.SkillDefinition(1);
            if (!skill || !lab.SelectSkillSlot(1)) throw new InvalidOperationException("角色 A 没有有效 E 技能");
            Reset("第二主手 E 槽");
            var first = Send(lab.RequestAttack);
            var firstId = lab.Actor.DataBus.ActionId.ToString();
            var firstStage = main.AcceptedStageId;
            float until = Time.realtimeSinceStartup + 2.5f;
            while (!lab.Actor.DataBus.AttackReady && Time.realtimeSinceStartup < until) yield return null;
            var second = Send(lab.RequestAttack);
            var secondStage = main.AcceptedStageId;
            yield return new WaitForSeconds(.8f);
            var accepted = Rows().Where(e => e.label == "Accepted").ToArray();
            var sequence = main.Sequence;
            Check(first && second && sequence != null && sequence.Id == profile.definitionId && sequence.Count == 2 &&
                  firstStage == sequence.Stage(0).Id && secondStage == sequence.Stage(1).Id &&
                  accepted.Select(e => e.action).Distinct().Count() == 2,
                  $"第二主手 {profile.definitionId}，已受理 {accepted.Length} 次，正式段 {firstStage} → {secondStage}，首动作 {firstId}");
            Reset("E 槽技能");
            bool skillOk = Send(lab.RequestSkill);
            yield return new WaitForSeconds(1.5f);
            var skillRows = Rows().Where(e => !string.IsNullOrEmpty(e.detail) && e.detail.StartsWith(skill.definitionId+" · ",StringComparison.Ordinal)).ToArray();
            Check(skillOk && lab.SkillSlot == 1 && skillRows.Any(e => !string.IsNullOrEmpty(e.action)),
                $"E 槽 {skill.definitionId}，正式受理 {skillOk}，关联能力事实 {skillRows.Length}，{lab.Status}");
            var turn = TurnAfterRelease();
            while (turn.MoveNext()) yield return turn.Current;
            lab.Condition = AttackLabTargetMode.Whiff;
            Reset("恢复主手");
            var engagement = UnityEngine.Object.FindObjectOfType<CombatEngagementSession>();
            float disengageAt = Time.realtimeSinceStartup + 5f;
            while (engagement && engagement.Engaged && Time.realtimeSinceStartup < disengageAt) yield return null;
            if (!main.Configure(originalProfile)) throw new InvalidOperationException(main.LastResult);
            lab.Actor.StateArbitrator.Combat.RebindLaboratorySequence();
            lab.Condition = AttackLabTargetMode.Fixed;
            var posture = lab.Actor.GetComponent<CombatPosture>();
            for (int mode = 0; mode < 3; mode++)
            {
                Reset("录制三态");
                if (cameraFollow) cameraFollow.ShakeSettings.intensity = mode == 1 ? 0 : initialShake;
                yield return new WaitForSeconds(.3f);
                if (mode == 1) lab.Trace.Frozen = true;
                if (mode == 2) lab.Trace.EndTrial();
                var previousRows = Rows();
                Check(!lab.Actor.DataBus.ActionActive && previousRows.All(e => e.label != "Accepted"),
                    $"模式 {mode} 请求前动作空闲。"+Context());
                int hp = lab.Target.Vitals.Health.Current;
                double before = posture.Current;
                bool ok = Send(lab.RequestAttack);
                double spent = before - posture.Current;
                float shakePeak = 0;
                float untilTime = Time.time + .85f;
                while (Time.time < untilTime)
                {
                    yield return null;
                    if (cameraFollow) shakePeak = Mathf.Max(shakePeak, cameraFollow.AppliedShakeOffset.magnitude);
                }
                Check(cameraFollow && (mode == 1 ? shakePeak < .00001f : shakePeak > .00001f),
                    $"模式 {mode} 相机强度 {cameraFollow?.ShakeSettings.intensity}，逐帧实际位移峰值 {shakePeak:F6}");
                var rows = Rows();
                var damage = hp - lab.Target.Vitals.Health.Current;
                if (mode == 0) { baselineDamage = damage; baselineSpend = spent; }
                Check(ok && damage > 0 && damage == baselineDamage && spent > 0 && Math.Abs(spent-baselineSpend) < .0001,
                    $"模式 {mode} 正式受理 {ok}，伤害 {damage}，即时架势消耗 {spent}。"+Context());
                if (mode != 0) Check(rows.Length == previousRows.Length, $"模式 {mode} 新记录 {rows.Length-previousRows.Length}");
                else
                {
                    var attack = rows.FirstOrDefault(r=>r.label=="Accepted");
                    var sounds = rows.Where(r=>r.label=="声音提交").ToArray();
                    Check(attack != null && sounds.Any(r=>r.parent==attack.action && !string.IsNullOrEmpty(r.action) && r.emission>=0),
                        $"音频提交 {sounds.Length} 条，核对正式 parent/action/emission");
                }
            }
            Reset("释放前复位");
            yield return new WaitForSeconds(.25f);
            bool preReset = Send(lab.RequestAttack);
            var pre = Rows();
            var onset = pre.LastOrDefault(e => e.label=="Accepted");
            Check(preReset && onset != null && pre.All(e=>e.label!="Released"), "复位前同帧正式受理且尚未释放");
            Reset("新代次");
            var restored = lab.Target.Vitals.Health.Current;
            yield return new WaitForSeconds(1.1f);
            Check(onset!=null && restored==lab.Target.Vitals.Health.Current &&
                  Rows().All(e=>e.action!=onset.action && e.parent!=onset.action && e.label!="Applied"),
                  "重置后旧动作及扣血数量为零，靶 HP "+lab.Target.Vitals.Health.Current);
            lab.Condition = AttackLabTargetMode.Whiff;
            foreach (bool right in new[] {false,true})
            {
                lab.FacingRight = right;
                Reset(right ? "向右空挥" : "向左空挥");
                yield return new WaitForSeconds(.25f);
                bool ok = Send(lab.RequestAttack);
                yield return new WaitForSeconds(.85f);
                var rows = Rows();
                var q = rows.FirstOrDefault(r=>!string.IsNullOrEmpty(r.queryType));
                Check(ok && q!=null && q.direction.x*(right?1:-1)>.99f && rows.All(e=>e.label!="Applied"),
                      $"空挥实际查询方向 {q?.direction}，球心 {q?.start}。"+Context());
            }
        }

        // 虚拟设备走正式 Action + 移动朝向采样。等到正式 Released 后才送反向摇杆。
        private static IEnumerator TurnAfterRelease()
        {
            lab.Condition = AttackLabTargetMode.Fixed;
            lab.FacingRight = true;
            Reset("释放后转身");
            turnPad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(turnPad, new GamepadState());
            yield return new WaitForSeconds(.15f);
            var view = UnityEngine.Object.FindObjectOfType<CombatFeedbackView>();
            int hpBefore = lab.Target.Vitals.Health.Current;
            bool ok = Send(lab.RequestAttack);
            string id = lab.Actor.DataBus.ActionId.ToString("N");
            bool noEarlyQuery = Rows().All(e => e.parent != id || string.IsNullOrEmpty(e.queryType));
            float limit = Time.realtimeSinceStartup + 2.5f;
            AttackLabEvent release = null;
            while (release == null && Time.realtimeSinceStartup < limit)
            {
                yield return null;
                release = Rows().FirstOrDefault(e => e.action == id && e.label == "Released");
            }
            var first = Rows().FirstOrDefault(e => e.parent == id && !string.IsNullOrEmpty(e.queryType));
            var renderId = view ? view.LastReleaseActionId : Guid.Empty;
            var renderDirection = view ? view.LastReleaseGeometry.ReleaseDirection : Vector3.zero;
            var contact = view ? view.LastContactDirection : Vector3.zero;
            var contactPoint = view ? view.LastContactVisualPosition : Vector3.zero;
            int hpAfter = lab.Target.Vitals.Health.Current;
            int turnFrame = -1;
            input.SetInventoryCapture(captureOwner, false, true);
            yield return null; // 等输入采样器在中立状态重新武装。
            InputSystem.QueueStateEvent(turnPad, new GamepadState { leftStick = Vector2.left });
            float maxTurnWait = Time.realtimeSinceStartup + .7f;
            while (Time.realtimeSinceStartup < maxTurnWait)
            {
                yield return null;
                if (lab.Actor.DataBus.AttackDirection.x < -.99f && lab.Actor.DataBus.VisualFacingX < 0 &&
                    lab.Actor.DataBus.Input.moveSource == PlayerInputSource.Gamepad)
                {
                    turnFrame = Time.frameCount;
                    break;
                }
            }
            InputSystem.QueueStateEvent(turnPad, new GamepadState());
            yield return null;
            input.SetInventoryCapture(captureOwner, true, true);
            yield return new WaitForSeconds(.2f);
            var rows = Rows();
            var queries = rows.Where(e => e.parent == id && !string.IsNullOrEmpty(e.queryType)).ToArray();
            var applied = rows.Where(e => e.parent == id && e.label == "Applied").ToArray();
            bool feedbackMatch = view && first != null && renderId.ToString("N") == first.action &&
                                 view.LastReleaseActionId == renderId &&
                                 view.LastReleaseGeometry.ReleaseDirection == renderDirection && renderDirection.x > .99f &&
                                 contactPoint == view.LastContactVisualPosition && contact == view.LastContactDirection;
            Check(ok && noEarlyQuery && release != null && first != null && turnFrame > release.frame &&
                  queries.All(e => e.action == first.action && e.direction.x > .99f) && feedbackMatch &&
                  applied.Length == 1 && hpAfter < hpBefore && hpAfter == lab.Target.Vitals.Health.Current,
                  $"释放后转身：父动作 {id}，子发射 {first?.action}，释放帧 {release?.frame}，手柄左转帧 {turnFrame}，正式查询 {queries.Length} 次（转身后 {queries.Count(e => turnFrame >= 0 && e.frame > turnFrame)} 次），首／末方向 {first?.direction}/{queries.LastOrDefault()?.direction}，挥击反馈方向 {renderDirection}，命中视觉方向 {contact}，反馈快照匹配 {feedbackMatch}，HP {hpBefore} → {hpAfter} → {lab.Target.Vitals.Health.Current}。"+Context());
            InputSystem.RemoveDevice(turnPad);
            turnPad = null;
        }

        private static int baselineDamage;
        private static double baselineSpend;

        private static void Complete(string error)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            if (error != null) Check(false, error);
            try
            {
                if (turnPad != null && turnPad.added) InputSystem.RemoveDevice(turnPad);
                turnPad = null;
                if (started && lab && Application.isPlaying)
                {
                    if (cameraFollow) cameraFollow.ShakeSettings.intensity = initialShake;
                    if (input) input.SetInventoryCapture(captureOwner, false, true);
                    lab.Party.SetInventoryCapture(captureOwner, false, true);
                    if (main && originalProfile!=null && main.SelectedId!=originalProfile)
                    {
                        Reset("还原主手选择");
                        if (!main.Configure(originalProfile)) Check(false,"还原主手："+main.LastResult);
                        lab.Actor.StateArbitrator.Combat.RebindLaboratorySequence();
                    }
                    if (originalActor>=0 && lab.Party.Characters[originalActor]!=lab.Actor) lab.SwitchActor(originalActor);
                    lab.Condition=originalCondition; lab.Station=originalStation; lab.Distance=originalDistance;
                    lab.Height=originalHeight; lab.FacingRight=originalRight; lab.EnemyAttacks=originalEnemyAttacks;
                    if (originalSkill>=0) lab.SelectSkillSlot(originalSkill);
                    Reset(originalVersion);
                    lab.Trace.Detailed=originalDetailed;
                    lab.Trace.Frozen=originalFrozen;
                }
            }
            catch (Exception e) { Check(false,"还原试验："+e); }
            string report = "[AttackLab验收] "+(failures==0?"PASS":"FAIL")+" · "+failures+" 项失败\n"+string.Join("\n",lines);
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), Report);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report);
            }
            catch (IOException e) { report += "\n无法写入报告："+e.Message; }
            if(failures==0) Debug.Log(report); else Debug.LogError(report);
            started=false; deadline=0; routine=null; lab=null; main=null; input=null;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying=false;
        }
    }
}
