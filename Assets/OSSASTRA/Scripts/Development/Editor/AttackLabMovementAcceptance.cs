// 运动回归通过正式虚拟手柄与实际碰撞测量。候选在各自复位点应用，不修改正式资产。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Audio;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal static class AttackLabMovementAcceptance
    {
        internal static IEnumerator Cases(AttackLabSession lab, Action<bool, string> check)
        {
            var motor = lab.Actor.GetComponent<CharacterMovement>();
            var source = motor.AuthoredJumpProfile;
            var peer = lab.Party.Characters.FirstOrDefault(actor => actor != null && actor != lab.Actor);
            var peerMotor = peer != null ? peer.GetComponent<CharacterMovement>() : null;
            if (source == null || peerMotor == null)
            {
                check(false, "运动验收缺少正式来源或同伴。");
                yield break;
            }

            var peerSource = peerMotor.JumpProfile;
            var owner = new object();
            var candidate = Object.Instantiate(source);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = lab.Actor.GetComponent<CharacterInputHandler>();
            var eventSystem = EventSystem.current;
            var originalSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            // 运动夹具应在 Game 输入上下文。清掉刚结束的战斗 UI 导航焦点，保留真实输入路由的资格。
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            var samples = new List<SideMovementStep>();
            void Observe(SideMovementStep step) => samples.Add(step);
            string Context() => $"物理步 {samples.Count}，输入峰值 {samples.Select(step => Mathf.Abs(step.HorizontalInput)).DefaultIfEmpty().Max():0.00}，实控 {lab.Actor.HasPlayerInput}，窗口捕获 {input.InventoryCaptured}，模态 {lab.Party.IsWorldInputCaptured}，焦点 {Application.isFocused}，键位 {keyboard.dKey.isPressed}/{keyboard.spaceKey.isPressed}";
            motor.SideStepCompleted += Observe;
            try
            {
                lab.Station = AttackLabStation.Flat;
                lab.Condition = AttackLabTargetMode.Whiff;
                lab.Height = 0;
                lab.FacingRight = true;
                if (!lab.ResetTrial("运动正式基线")) { check(false, lab.Status); yield break; }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(.2f);
                samples.Clear();
                var run = new KeyboardState(Key.D);
                InputSystem.QueueStateEvent(keyboard, run);
                yield return new WaitForSeconds(.3f);
                string baselineContext = Context();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float baseline = MeasuredGroundSpeed(samples);

                candidate.runSpeed = source.runSpeed * 1.4f;
                check(motor.TryUseDevelopmentProfile(owner, source, candidate), "运动候选占用当前角色");
                if (!lab.ResetTrial("运动跑速候选")) { check(false, lab.Status); yield break; }
                yield return new WaitForSeconds(.2f);
                samples.Clear();
                InputSystem.QueueStateEvent(keyboard, run);
                yield return new WaitForSeconds(.3f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float newSpeed = MeasuredGroundSpeed(samples);
                bool noCross = peerMotor.JumpProfile == peerSource && motor.AuthoredJumpProfile == source;
                check(baseline > .1f && Mathf.Abs(newSpeed - baseline * 1.4f) < .2f && noCross,
                    $"实际地面速度 {baseline:0.00} → {newSpeed:0.00} U/s，同伴及正式来源引用保持 {noCross}。基线 {baselineContext}。候选 {Context()}");
                check(!motor.ReleaseDevelopmentProfile(new object()) && motor.JumpProfile == candidate &&
                      motor.ReleaseDevelopmentProfile(owner) && motor.JumpProfile == source,
                    "运行候选仅由原实验撤销，恢复当前正式引用");
                yield return new WaitForSeconds(.15f);

                var peaks = new float[2];
                for (int i = 0; i < peaks.Length; i++)
                {
                    if (!lab.ResetTrial(i == 0 ? "短按跳" : "长按跳")) { check(false, lab.Status); yield break; }
                    yield return new WaitForSeconds(.23f);
                    samples.Clear();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return new WaitForSeconds(i == 0 ? .055f : .46f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSeconds(1.2f);
                    var jump = MeasureJump(samples);
                    peaks[i] = jump?.Height ?? 0f;
                    check(jump != null && jump.CompleteFreeJump && jump.landingSpeed > 0f &&
                          samples.Any(step => step.Launched && step.LaunchSupportCollider != null),
                        $"真实{i switch { 0 => "短按", _ => "长按" }}跳，最高 {peaks[i]:0.00} U，腾空 {jump?.elapsed:0.00} s，落地 {jump?.landingSpeed:0.00} U/s。{Context()}");
                    var facts = lab.Trace.Events.Where(entry => entry.trial == lab.Generation && entry.track == "运动反馈").ToArray();
                    check(facts.Any(entry => entry.label == "Jump" && entry.detail.Contains("事实") && entry.detail.Contains("stone")) &&
                          facts.Any(entry => entry.label == "Land" && entry.detail.Contains("事实") && entry.detail.Contains("stone")),
                        $"真实起跳和落地各有石地支撑事实，运动反馈记录 {facts.Length} 条");
                }
                check(peaks[1] > peaks[0] + .25f, $"长按实际最高点高于短按 · {peaks[1]:0.00} / {peaks[0]:0.00} U");

                lab.Station = AttackLabStation.LowCeiling;
                if (!lab.ResetTrial("实碰顶")) { check(false, lab.Status); yield break; }
                yield return new WaitForSeconds(.23f);
                samples.Clear();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return new WaitForSeconds(.35f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(.6f);
                var ceilingJump = MeasureJump(samples);
                check(samples.Any(step => step.HitHead) && ceilingJump?.interruption == "碰顶" && !ceilingJump.CompleteFreeJump,
                    $"低顶真实截断，碰顶步 {samples.Count(step => step.HitHead)}，轨迹 {ceilingJump?.interruption ?? "无"}");

                lab.Station = AttackLabStation.Drop;
                if (!lab.ResetTrial("实下落")) { check(false, lab.Status); yield break; }
                yield return new WaitForSeconds(.24f);
                samples.Clear();
                InputSystem.QueueStateEvent(keyboard, run);
                yield return new WaitForSeconds(.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(.85f);
                int coyote = samples.Count(step => step.GroundedBefore && !step.GroundedAfter && !step.Launched);
                int landed = samples.Count(step => step.LandingDownwardSpeed > 0f && step.SupportCollider != null);
                check(coyote > 0 && landed > 0 && samples.All(step => !step.Launched),
                    $"定点下落记录离台 {coyote} 次及真实落地 {landed} 次 · {Context()}");

                var ledge = CheckLedge(lab, motor, keyboard, check);
                while (ledge.MoveNext()) yield return ledge.Current;
                var deferred = CheckDeferredContact(lab, keyboard, check);
                while (deferred.MoveNext()) yield return deferred.Current;
            }
            finally
            {
                motor.SideStepCompleted -= Observe;
                motor.ReleaseDevelopmentProfile(owner);
                Object.Destroy(candidate);
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (eventSystem != null && eventSystem.currentSelectedGameObject == null && originalSelection != null)
                    eventSystem.SetSelectedGameObject(originalSelection);
            }
        }

        private static IEnumerator CheckDeferredContact(AttackLabSession lab, Keyboard keyboard, Action<bool, string> check)
        {
            var adapter = Object.FindObjectOfType<SideViewLocomotionAudioAdapter>();
            var cue = adapter != null ? new SerializedObject(adapter).FindProperty("jumpCue")
                ?.objectReferenceValue as SfxEventDefinitionSO : null;
            if (cue == null) { check(false, "运动延迟验收缺少正式起跳声"); yield break; }
            var role = lab.Actor;
            var owner = new object();
            var profile = ScriptableObject.CreateInstance<SideViewFeedbackProfileSO>();
            profile.name = "起跳材质延迟验收";
            profile.movement = new[] { new SideViewMotionFeedbackBinding { kind = SideViewMotionKind.Jump, material = "stone",
                settings = new SideViewFeedbackSettings { overrideSound = true, sound = cue, soundDelayMs = 180 } } };
            try
            {
                if (!SideViewFeedbackRuntime.SetPreview(role, profile, owner))
                {
                    check(false, "运动回执验收无法取得角色试验租约");
                    yield break;
                }
                lab.Station = AttackLabStation.Flat;
                foreach (bool cancel in new[] { false, true })
                {
                    if (!lab.ResetTrial(cancel ? "复位清理旧运动音" : "匹配石地起跳声"))
                    {
                        check(false, lab.Status);
                        yield break;
                    }
                    yield return new WaitForSeconds(.24f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    float deadline = Time.realtimeSinceStartup + .5f;
                    AttackLabEvent actual = null;
                    while (actual == null && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        actual = lab.Trace.Events.LastOrDefault(entry => entry.trial == lab.Generation &&
                            entry.track == "运动反馈" && entry.label == "Jump" && entry.detail.Contains(profile.name) &&
                            entry.detail.Contains("事实"));
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    int submittedBeforeReset = adapter.SubmittedJumps;
                    if (cancel) lab.ResetTrial("起跳声尚未到期时复位");
                    yield return new WaitForSeconds(.42f);
                    var played = lab.Trace.Events.LastOrDefault(entry => entry.trial == lab.Generation &&
                        entry.track == "运动反馈" && entry.label == "Jump" && entry.detail.Contains("声音提交") &&
                        entry.detail.Contains(profile.name));
                    double delay = played == null || actual == null ? -1 : played.realMs - actual.realMs;
                    bool submitted = cancel ? adapter.SubmittedJumps == submittedBeforeReset :
                        adapter.SubmittedJumps == submittedBeforeReset + 1 && delay >= 170;
                    check(actual != null && submitted, cancel ? "复位取消尚未到期的运动音，不在新试验续播" :
                        $"石地真实起跳匹配私人试验配方，声音在事实后 {delay:0} ms 提交一次");
                }
            }
            finally
            {
                SideViewFeedbackRuntime.ClearPreview(role, owner);
                Object.Destroy(profile);
            }
        }

        private static IEnumerator CheckLedge(AttackLabSession lab, CharacterMovement motor, Keyboard keyboard,
            Action<bool, string> check)
        {
            var occurrences = new List<SideTraversalEvent>();
            void Contact(SideTraversalEvent sample) => occurrences.Add(sample);
            motor.SideTraversalOccurred += Contact;
            try
            {
                foreach (bool drop in new[] { false, true })
                {
                    lab.Station = AttackLabStation.HighLedge;
                    lab.Condition = AttackLabTargetMode.Whiff;
                    if (!lab.ResetTrial(drop ? "墙边主动放手" : "墙边抓取并翻越"))
                    {
                        check(false, lab.Status);
                        yield break;
                    }
                    occurrences.Clear();
                    yield return new WaitForSeconds(.22f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    yield return new WaitForSeconds(.24f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
                    yield return new WaitForSeconds(.069f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    float deadline = Time.realtimeSinceStartup + 1.1f;
                    while (occurrences.All(e => e.Kind != SideTraversalEventKind.Grab) && Time.realtimeSinceStartup < deadline)
                        yield return null;

                    if (drop) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                    var transition = drop ? SideTraversalEventKind.Drop : SideTraversalEventKind.ClimbComplete;
                    deadline = Time.realtimeSinceStartup + .9f;
                    while (occurrences.All(e => e.Kind != transition) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSeconds(.06f);
                    bool reached = occurrences.Any(e => e.Kind == SideTraversalEventKind.Grab && e.ContactCollider != null);
                    bool exit = occurrences.Any(e => e.Kind == transition);
                    var mode = lab.Actor.StateArbitrator.Movement;
                    check(reached && exit && (drop || motor.Grounded), $"高台{(drop ? "放手" : "翻越")}：{string.Join("、", occurrences.Select(e => e.Kind))} · {mode.TraversalPhase} / {mode.TraversalLastRejection}");
                }
            }
            finally { motor.SideTraversalOccurred -= Contact; }
        }

        private static float MeasuredGroundSpeed(IEnumerable<SideMovementStep> steps) =>
            steps.Where(step => step.GroundedAfter && step.ActualHorizontalSpeed > .05f)
                .Select(step => step.ActualHorizontalSpeed).DefaultIfEmpty(0f).Max();

        private static AttackLabMovementTrace.Jump MeasureJump(IEnumerable<SideMovementStep> steps)
        {
            AttackLabMovementTrace.Jump jump = null;
            foreach (var step in steps)
            {
                if (jump == null && step.Launched)
                    jump = new AttackLabMovementTrace.Jump { origin = step.PreviousWorldPosition, apex = step.PreviousWorldPosition };
                jump?.Observe(step);
            }
            return jump;
        }
    }
}
