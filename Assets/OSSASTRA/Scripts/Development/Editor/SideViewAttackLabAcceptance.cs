using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.Audio;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.CharacterAnimation;
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
        internal static bool IsRunning => SessionState.GetBool(Pending, false);

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
            if (mode == PlayModeStateChange.EnteredPlayMode) deadline = EditorApplication.timeSinceStartup + 65;
            if (mode == PlayModeStateChange.ExitingPlayMode) Complete("验收进行时退出了 Play");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 65;
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
            var offsets = CheckReleaseOffsets();
            while (offsets.MoveNext()) yield return offsets.Current;
            var stops = CheckLocalHitStop();
            while (stops.MoveNext()) yield return stops.Current;
            var feedback = CheckContactFeedback();
            while (feedback.MoveNext()) yield return feedback.Current;
            int freycia = members.FindIndex(c => c && c.ActorId == "temporary-b");
            lab.Condition = AttackLabTargetMode.Whiff;
            if (freycia >= 0 && lab.SwitchActor(freycia))
            {
                var oldInput = input;
                input = lab.Actor.GetComponent<CharacterInputHandler>();
                try
                {
                    Reset("芙蕾第一击");
                    yield return new WaitForSeconds(.25f);
                    bool freyciaAccepted = Send(lab.RequestAttack);
                    yield return new WaitForSeconds(1);
                    var rows = Rows();
                    Check(freyciaAccepted && rows.Any(r => r.clip == "ATK1" && r.semantic == "Release") &&
                          rows.Count(r => r.label == "Released") == 1 &&
                          rows.Any(r => r.clip == "ATK1" && r.semantic == "Finish"),
                          $"芙蕾 ATK1 受理 {freyciaAccepted}，{lab.Status}，正式释放 {rows.Count(r => r.label == "Released")} 次");
                    var feet = CheckFootsteps();
                    while (feet.MoveNext()) yield return feet.Current;
                }
                finally
                {
                    input.SetInventoryCapture(captureOwner, false, true);
                    input = oldInput;
                    lab.SwitchActor(indexA);
                }
            }
            else Check(false, "无法切换至芙蕾验收第一击");
            input.SetInventoryCapture(captureOwner, false, true);
            lab.Party.SetInventoryCapture(captureOwner, false, true);
            var movement = AttackLabMovementAcceptance.Cases(lab, Check);
            while (movement.MoveNext()) yield return movement.Current;
            CheckDurableRoleSettings();
        }

        private static void CheckDurableRoleSettings()
        {
            var realRole = lab.Actor.Definition;
            var shared = lab.Actor.GetComponent<CharacterMovement>().AuthoredJumpProfile;
            var original = UnityEngine.Object.Instantiate(realRole);
            var root = new GameObject("隔离角色配置读回");
            root.SetActive(false);
            string saveFolder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets",
                "AttackLabAcceptance_" + Guid.NewGuid().ToString("N")));
            if (string.IsNullOrEmpty(saveFolder))
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(original);
                Check(false, "无法建立隔离的配置验收目录");
                return;
            }
            try
            {
                var movement = UnityEngine.Object.Instantiate(shared);
                movement.runSpeed = shared.runSpeed + .5f;
                var recipe = ScriptableObject.CreateInstance<SideViewFeedbackProfileSO>();
                recipe.movement = new[] { new SideViewMotionFeedbackBinding { kind = SideViewMotionKind.Land,
                    material = "stone", settings = new SideViewFeedbackSettings { soundDelayMs = 37 } } };
                var attack = ScriptableObject.CreateInstance<ActionDefinitionSO>();
                attack.hitStop = new AttackHitStop { configured = true, attackerSeconds = .045f, receiverSeconds = .07f };
                AssetDatabase.CreateAsset(movement, saveFolder + "/Movement.asset");
                AssetDatabase.CreateAsset(recipe, saveFolder + "/Feedback.asset");
                AssetDatabase.CreateAsset(attack, saveFolder + "/Action.asset");
                AssetDatabase.CreateAsset(original, saveFolder + "/Role.asset");
                original.sideViewMovementOverride = movement;
                original.sideViewFeedback = recipe;
                original.basicAttacks = new[] { attack };
                EditorUtility.SetDirty(original);
                AssetDatabase.SaveAssetIfDirty(original);
                AssetDatabase.ImportAsset(saveFolder + "/Role.asset", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(saveFolder + "/Role.asset");
                var reader = root.AddComponent<Ossastra.Characters.CharacterRoot>();
                var binding = new SerializedObject(reader);
                binding.FindProperty("characterDefinition").objectReferenceValue = loaded;
                binding.ApplyModifiedPropertiesWithoutUndo();
                var motor = root.GetComponent<CharacterMovement>();
                motor.ConfigureSideView(shared);
                Check(loaded != null && loaded != realRole && motor.AuthoredJumpProfile == loaded.sideViewMovementOverride &&
                    Mathf.Approximately(motor.AuthoredJumpProfile.runSpeed, shared.runSpeed + .5f) &&
                    loaded.sideViewFeedback.ResolveMovement(SideViewMotionKind.Land, "stone")?.settings.soundDelayMs == 37f &&
                    Mathf.Approximately(loaded.basicAttacks[0].hitStop.receiverSeconds, .07f),
                    "正式角色引用重导入：正常运动读取角色专用 Profile，材质反馈与逐招卡肉写盘");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(saveFolder);
            }
        }

        private static IEnumerator CheckContactFeedback()
        {
            if (!lab.HasOutcomeFixtures) { Check(false, "缺少守卫、致死或双目标装配"); yield break; }
            var actor = lab.Actor;
            var vitals = actor.GetComponent<CombatVitals>();
            var priorStop = vitals.LaboratoryHitStop;
            var profile = ScriptableObject.CreateInstance<SideViewFeedbackProfileSO>();
            profile.name = "独立实战反馈对照";
            var catalog = Ossastra.Audio.GameAudioRoot.Instance?.SfxCatalog;
            var sound = catalog?.events?.FirstOrDefault(item => item && !item.loop && item.clips != null &&
                item.clips.Length > 0 && item.eventId.StartsWith("player.", StringComparison.Ordinal) &&
                (item.eventId.Contains("hit") || item.eventId.Contains("impact")));
            var settings = new SideViewFeedbackSettings { overrideSound = sound != null, sound = sound,
                soundDelayMs = 70, soundStartMs = 10, overrideShake = true, shake = 0 };
            profile.combat = new[]
            {
                new SideViewCombatFeedbackBinding { selector = "*", result = SideViewContactResult.Hit, settings = settings },
                new SideViewCombatFeedbackBinding { selector = "*", result = SideViewContactResult.Hit, material = "metal", settings = settings },
                new SideViewCombatFeedbackBinding { selector = "*", result = SideViewContactResult.Guard, material = "metal", settings = settings }
            };
            try
            {
                Check(SideViewFeedbackRuntime.SetPreview(actor, profile, captureOwner), "取得角色独立反馈对照租约");
                foreach (var mode in new[] { AttackLabTargetMode.Whiff, AttackLabTargetMode.Guarded, AttackLabTargetMode.Fixed,
                             AttackLabTargetMode.Lethal, AttackLabTargetMode.Pair })
                {
                    lab.Condition = mode;
                    lab.Distance = mode == AttackLabTargetMode.Pair ? .8f : 1.12f;
                    lab.Station = AttackLabStation.Flat;
                    Reset("真实回执 " + mode);
                    vitals.LaboratoryHitStop = new AttackHitStop { configured = true, attackerSeconds = .045f,
                        receiverSeconds = mode == AttackLabTargetMode.Pair ? .12f : .045f, onGuard = true };
                    yield return new WaitForSeconds(.25f);
                    int hp = lab.Target.Vitals.Health.Current;
                    int secondHp = mode == AttackLabTargetMode.Pair ? lab.PairedTarget.Vitals.Health.Current : 0;
                    bool accepted = Send(lab.RequestAttack);
                    bool attackerFroze = false, victimFroze = false, secondFroze = false;
                    bool stretched = false;
                    float end = Time.realtimeSinceStartup + .9f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        yield return null;
                        if (mode == AttackLabTargetMode.Fixed && !stretched && hp > lab.Target.Vitals.Health.Current)
                        {
                            stretched = true;
                            // 模拟长渲染更新，验证首次消费不因请求帧和显示帧之间隔了 45 ms 而丢失。
                            System.Threading.Thread.Sleep(78);
                        }
                        attackerFroze |= vitals.InHitStop;
                        victimFroze |= lab.Target.Vitals.InHitStop;
                        if (mode == AttackLabTargetMode.Pair) secondFroze |= lab.PairedTarget.Vitals.InHitStop;
                    }
                    var rows = Rows();
                    var outcomes = rows.Where(row => row.label == "Applied" || row.label == "Guarded").ToArray();
                    var recipes = rows.Where(row => row.label == "实战配方").ToArray();
                    bool correctContact = mode == AttackLabTargetMode.Whiff
                        ? outcomes.Length == 0 && recipes.Length == 0 && !attackerFroze
                        : mode == AttackLabTargetMode.Guarded
                            ? outcomes.Any(row => row.label == "Guarded") && lab.Target.Vitals.Health.Current == hp
                            : lab.Target.Vitals.Health.Current < hp;
                    bool modeCheck = mode == AttackLabTargetMode.Lethal ? lab.Target.Vitals.Health.Current == 0 :
                        mode != AttackLabTargetMode.Pair || (lab.PairedTarget.Vitals.Health.Current < secondHp && secondFroze);
                    bool localClock = mode == AttackLabTargetMode.Whiff ||
                        (attackerFroze && (mode == AttackLabTargetMode.Lethal || victimFroze) && !vitals.InHitStop);
                    bool recipeExpected = mode == AttackLabTargetMode.Whiff ||
                        recipes.Any(entry => entry.detail.Contains(profile.name));
                    Check(accepted && correctContact && modeCheck && localClock && recipeExpected,
                        $"{mode}：HP {hp}→{lab.Target.Vitals.Health.Current}，回执 {outcomes.Length}，配方 {recipes.Length}，攻/受/第二目标暂停 {attackerFroze}/{victimFroze}/{secondFroze}，受控长帧 {stretched}");
                    if (mode == AttackLabTargetMode.Fixed || mode == AttackLabTargetMode.Pair)
                        lines.Add("   攻击者：" + vitals.LastHitStop + " · 受击者：" + lab.Target.Vitals.LastHitStop +
                            (mode == AttackLabTargetMode.Pair ? " · 第二目标：" + lab.PairedTarget.Vitals.LastHitStop : ""));
                    if (mode == AttackLabTargetMode.Lethal && sound != null)
                        Check(rows.Any(entry => entry.label == "声音提交" && entry.track == "反馈" && entry.detail.Contains(sound.eventId)),
                            "致死命中的已确认延迟音效，经统一声音根提交");
                }
            }
            finally
            {
                vitals.LaboratoryHitStop = priorStop;
                SideViewFeedbackRuntime.ClearPreview(actor, captureOwner);
                UnityEngine.Object.Destroy(profile);
                lab.Distance = 1.2f;
            }
        }

        private static IEnumerator CheckFootsteps()
        {
            var audio = UnityEngine.Object.FindObjectOfType<Ossastra.Audio.SideViewLocomotionAudioAdapter>();
            if (!audio) { Check(false, "训练场缺少正式横版运动音频适配器"); yield break; }
            turnPad = InputSystem.AddDevice<Gamepad>();
            input.SetInventoryCapture(captureOwner, false, true);
            lab.Party.SetInventoryCapture(captureOwner, false, true);
            try
            {
                foreach (bool walk in new[] { true, false })
                {
                    Reset(walk ? "慢走脚步" : "普通移动脚步");
                    InputSystem.QueueStateEvent(turnPad, new GamepadState());
                    yield return new WaitForSeconds(.2f);
                    int before = audio.SubmittedFootsteps;
                    InputSystem.QueueStateEvent(turnPad, new GamepadState { leftStick = Vector2.right * (walk ? .4f : .9f) });
                    yield return new WaitForSeconds(1.8f);
                    InputSystem.QueueStateEvent(turnPad, new GamepadState());
                    yield return new WaitForSeconds(.2f);
                    int played = audio.SubmittedFootsteps - before;
                    string clip = walk ? "Walk" : "Move";
                    int marks = Rows().Count(r => r.clip == clip && r.semantic == "Footstep");
                    yield return new WaitForSeconds(.3f);
                    Check(marks > 0 && played == marks && audio.SubmittedFootsteps == before + played,
                          $"{clip} 时间轴脚步 {marks} 次，运动音频提交 {played} 次，停止后新增 {audio.SubmittedFootsteps - before - played} 次");
                }
            }
            finally
            {
                InputSystem.QueueStateEvent(turnPad, new GamepadState());
                input.SetInventoryCapture(captureOwner, true, true);
                lab.Party.SetInventoryCapture(captureOwner, true, true);
            }
        }

        private static IEnumerator CheckLocalHitStop()
        {
            var vitals = lab.Actor.Vitals;
            bool enabled = vitals.enableAttackHitStop, attacker = vitals.stopAttacker;
            float duration = vitals.hitStopSeconds;
            try
            {
                foreach (int mode in new[] { 0, 1, 2 })
                {
                    lab.Condition = mode == 2 ? AttackLabTargetMode.Whiff : AttackLabTargetMode.Fixed;
                    Reset("命中停顿 " + mode);
                    vitals.enableAttackHitStop = mode != 0;
                    vitals.stopAttacker = true;
                    vitals.hitStopSeconds = .12f;
                    yield return new WaitForSeconds(.15f);
                    bool accepted = Send(lab.RequestAttack), paused = false;
                    double until = Time.realtimeSinceStartupAsDouble + .85;
                    while (Time.realtimeSinceStartupAsDouble < until)
                    {
                        paused |= vitals.InHitStop;
                        yield return null;
                    }
                    var rows = Rows();
                    Check(accepted && paused == (mode == 1) && !vitals.InHitStop &&
                          rows.Count(r => r.label == "Released") == 1 && rows.Any(r => r.label == "Applied") == (mode != 2),
                          $"卡肉 {mode}，受理 {accepted}，观察到攻击者局部停顿 {paused}，已恢复 {!vitals.InHitStop}");
                }
            }
            finally { vitals.enableAttackHitStop = enabled; vitals.hitStopSeconds = duration; vitals.stopAttacker = attacker; }
        }

        // 使用运行副本比较同一条正式释放标记。直接观察调试台采集的时刻和正式释放数。
        private static IEnumerator CheckReleaseOffsets()
        {
            var source = lab.Actor.Definition.eventCalibration;
            var baseline = Rows().FirstOrDefault(e => e.semantic == "Release" && e.markerSequence > 0);
            var driver = lab.Actor.GetComponentsInChildren<Ossastra.CharacterAnimation.CharacterAnimationController>(true)
                .FirstOrDefault(c => c.isActiveAndEnabled && c.Skeleton != null);
            var handle = driver ? driver.GetComponent<Ossastra.CharacterAnimation.AnimationHandleSystem>() : null;
            if (!source || baseline == null || !handle) { Check(false, "缺少正式释放基线或活动身体轨"); yield break; }
            foreach (int offset in new[] { -1, 0, 1 })
            {
                var candidate = UnityEngine.Object.Instantiate(source);
                var marker = candidate.bindings.First(b => b.animation == baseline.clip && b.semantic == AnimationEventMeaning.Release);
                marker.offsetFrames = offset;
                try
                {
                    Reset("释放偏移 " + offset);
                    handle.CandidateCalibration = candidate;
                    yield return new WaitForSeconds(.2f);
                    bool ok = Send(lab.RequestAttack);
                    yield return new WaitForSeconds(.85f);
                    var rows = Rows();
                    var hit = rows.FirstOrDefault(e => e.clip == baseline.clip && e.semantic == "Release");
                    int releases = rows.Count(e => e.label == "Released");
                    Check(ok && hit != null && releases == 1 &&
                          Mathf.Abs(hit.effectiveMarkerTime - hit.localTime - (float)offset / candidate.framesPerSecond) < .0001f,
                          $"偏移 {offset} 帧，片段 {baseline.clip}，有效时间 {hit?.effectiveMarkerTime:F4}s，正式释放 {releases} 次");
                }
                finally
                {
                    handle.CandidateCalibration = null;
                    UnityEngine.Object.Destroy(candidate);
                }
            }
            // 专门拉开窗口与释放，检验拒绝不会先取消，以及成功取消后不会迟发。
            var cancelTest = UnityEngine.Object.Instantiate(source);
            var release = cancelTest.bindings.First(b => b.animation == baseline.clip && b.semantic == AnimationEventMeaning.Release);
            var window = cancelTest.bindings.First(b => b.behavior == release.behavior && b.animation == baseline.clip &&
                b.semantic == AnimationEventMeaning.DodgeCancel);
            window.offsetFrames = 1 - Mathf.RoundToInt(window.sourceTime * cancelTest.framesPerSecond);
            release.offsetFrames = 6 - Mathf.RoundToInt(release.sourceTime * cancelTest.framesPerSecond);
            try
            {
                Reset("冲刺取消候选");
                handle.CandidateCalibration = cancelTest;
                yield return new WaitForSeconds(.2f);
                var dodge = lab.Actor.GetComponent<CharacterDodge>();
                bool startedAttack = Send(lab.RequestAttack);
                var attackId = lab.Actor.DataBus.ActionId;
                bool early = Send(() => dodge.Request(Vector2.right, true));
                bool preserved = lab.Actor.DataBus.ActionId == attackId && lab.Actor.DataBus.ActionActive;
                double limit = Time.realtimeSinceStartupAsDouble + 1;
                while (!Rows().Any(e => e.semantic == "DodgeCancel") && Time.realtimeSinceStartupAsDouble < limit) yield return null;
                bool refused = !Send(() => dodge.Request(Vector2.right, false)) && lab.Actor.DataBus.ActionId == attackId;
                bool cancelled = Send(() => dodge.Request(Vector2.right, true));
                string dodgeReason = dodge.LastResult + " active=" + lab.Actor.DataBus.ActionActive + " move=" + lab.Actor.DataBus.AllowsMovement;
                yield return new WaitForSeconds(.55f);
                Check(startedAttack && !early && preserved && refused && cancelled && Rows().All(r => r.label != "Released"),
                      $"冲刺窗口：提前受理 {early}，原动作保留 {preserved}，无权限请求被拒 {refused}，窗口内受理 {cancelled} ({dodgeReason})，释放 {Rows().Count(r => r.label == "Released")} 次");
            }
            finally { handle.CandidateCalibration = null; UnityEngine.Object.Destroy(cancelTest); }
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
