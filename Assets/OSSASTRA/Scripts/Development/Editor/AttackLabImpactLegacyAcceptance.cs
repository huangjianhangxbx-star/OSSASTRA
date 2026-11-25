// 在横版手感场经原设备输入、身体动作、Spine 轨道和命中回执核对三段持续技与下冲。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterAnimation;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.CombatCore;
using Ossastra.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Actor = Ossastra.Characters.CharacterRoot;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    public static class AttackLabImpactLegacyAcceptance
    {
        private const string SawClip = "圆锯-技能1-";
        private static void Check(List<string> lines, bool ok, string result) => lines.Add((ok ? "PASS " : "FAIL ") + result);
        private static void Keys(Keyboard keyboard, params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        public static IEnumerator Cases(AttackLabSession lab, List<string> lines, Keyboard keyboard)
        {
            var previous = ActionEffectList.EditorOverride;
            int original = lab.Party.Characters.ToList().IndexOf(lab.Actor);
            int freycia = lab.Party.Characters.ToList().FindIndex(p => p && p.ActorId == "temporary-b");
            lab.Condition = AttackLabTargetMode.Whiff;
            lab.Station = AttackLabStation.Flat;
            lab.FacingRight = true;
            lab.EnemyAttacks = false;
            lab.Height = 0;
            lab.Distance = 1.12f;
            if (freycia < 0 || !lab.SwitchActor(freycia))
            { Check(lines, false, "专项回归未找到可控制的芙蕾"); yield break; }
            try
            {
                foreach (var scenario in new[] { SawCase(lab, lines, keyboard), DiveCase(lab, lines, keyboard), PostureSwitch(lab, lines, original), BrokenPosture(lab, lines) })
                {
                    try
                    {
                        while (true)
                        {
                            bool more;
                            try { more = scenario.MoveNext(); }
                            catch (Exception ex) { Check(lines, false, "专项回归 " + ex); break; }
                            if (!more) break;
                            yield return scenario.Current;
                        }
                    }
                    finally { (scenario as IDisposable)?.Dispose(); }
                }
            }
            finally
            {
                Keys(keyboard);
                ActionEffectList.EditorOverride = previous;
                if (original >= 0 && lab.Actor != lab.Party.Characters[original]) lab.SwitchActor(original);
            }
        }

        private static IEnumerator SawCase(AttackLabSession lab, List<string> lines, Keyboard keyboard)
        {
            var actor = lab.Actor;
            var ability = actor.GetComponent<CharacterAbilities>();
            var equipment = actor.GetComponent<CharacterEquipment>();
            var driver = actor.GetComponentInChildren<CharacterAnimationController>();
            var restoreResources = equipment.State.CaptureAttackLabBaseline();
            var selections = equipment.State.RightRuntime.SkillSelections;
            bool hadQ = selections.TryGetValue(0, out string oldQ);
            var previous = ActionEffectList.EditorOverride;
            var originalWeapon = equipment.RightDefinition;
            var originalSkill = ability.SkillDefinition(0);
            var panel = new AttackLabImpactPanel();
            AbilityDefinitionSO sample = null;
            try
            {
                lab.Condition = AttackLabTargetMode.Light;
                lab.ResetTrial("圆锯 Q");
                yield return new WaitForSeconds(.2f);
                var saw = panel.EquipSaw(lab);
                Check(lines, ability.SkillDefinition(0)==saw && AssetDatabase.GetAssetPath(saw)==AttackLabImpactSamples.SawPath,
                    "手感台试装共享圆锯子资产到芙蕾实际 Q 槽");
                lab.ResetTrial("圆锯复位");
                Check(lines, ability.SkillDefinition(0)==saw, "训练复位保留圆锯 Q 试装");
                sample = Object.Instantiate(saw);
                var tail = new InputMovementScaleEffect { trigger=ActionEffectTrigger.PhaseChanged, phaseClip=SawClip+"3", fullBody=false, scale=.9f };
                sample.effects.entries.Add(tail);
                ActionEffectList.EditorOverride = (owner, source) => owner == actor && source == saw ? sample.effects : null;
                yield return new WaitForSeconds(.15f);
                var posture = actor.GetComponent<CombatPosture>();
                double priorPosture = posture.Current;
                int hp = lab.Target.Vitals.Health.Current;
                float from = lab.Target.transform.position.x;
                Keys(keyboard, Key.Q);
                Guid id = Guid.Empty;
                bool start = false, loop = false, end = false;
                bool walking = false, walked = false, sawDuringWalk = false;
                var walkTracks = new HashSet<string>();
                float walkFrom = 0, walkedDistance = 0;
                float loopTime = 0;
                double spent = 0;
                float until = Time.realtimeSinceStartup + 1.35f;
                while (Time.realtimeSinceStartup < until)
                {
                    if (id == Guid.Empty && actor.ActionActive) id = actor.DataBus.ActionId;
                    var tracks = driver.Skeleton.AnimationState.Tracks;
                    var track = tracks.Count > 0 ? tracks.Items[0] : null;
                    start |= track?.Animation?.Name == SawClip + "1";
                    if (track?.Animation?.Name == SawClip + "2" && track.Loop)
                    { loop = true; loopTime = Mathf.Max(loopTime, track.TrackTime); }
                    if (loop && !walking && !walked && Time.realtimeSinceStartup > until - .88f)
                    {
                        walking = true;
                        walkFrom = actor.transform.position.x;
                        Keys(keyboard, Key.Q, Key.D);
                    }
                    if (walking)
                    {
                        walkedDistance = Mathf.Max(walkedDistance, actor.transform.position.x - walkFrom);
                        for (int i=0; i<tracks.Count; ++i)
                        {
                            var playing = tracks.Items[i];
                            if (playing?.Animation == null) continue;
                            walkTracks.Add(i + "/" + playing.Animation.Name + (playing.Loop ? " loop" : "") +
                                (actor.DataBus.PresentsLocomotion ? " 移动" : ""));
                            sawDuringWalk |= actor.DataBus.PresentsLocomotion &&
                                playing.Animation.Name.Contains(SawClip + "2") && playing.Loop;
                        }
                        if (Time.realtimeSinceStartup > until - .66f)
                        { Keys(keyboard, Key.Q); walking = false; walked = true; }
                    }
                    spent = Math.Max(spent, priorPosture - posture.Current);
                    yield return null;
                }
                bool earlyTail = lab.Trace.Events.Any(e => e.trial==lab.Generation && e.label=="激活" && e.detail.Contains(tail.id));
                Check(lines, walked && sawDuringWalk && walkedDistance > .025f, $"圆锯持 Q 走 A，角色实际前移 {walkedDistance:F3}，移动中循环轨 {sawDuringWalk}，{string.Join("、", walkTracks)}");
                Keys(keyboard);
                until = Time.realtimeSinceStartup + 1.35f;
                while (Time.realtimeSinceStartup < until)
                {
                    var tracks = driver.Skeleton.AnimationState.Tracks;
                    var track = tracks.Count > 0 ? tracks.Items[0] : null;
                    end |= track?.Animation?.Name == SawClip + "3" && !track.Loop;
                    yield return null;
                }
                string key = id.ToString("N");
                var rows = lab.Trace.Events.Where(e => e.trial == lab.Generation).ToArray();
                start |= rows.Any(e => e.clip == SawClip+"1" && e.animationTrack == 0);
                end |= rows.Any(e => e.clip == SawClip+"3" && e.animationTrack == 0);
                int phases = rows.Count(e => e.label=="PhaseChanged" && e.action==key);
                int pulses = rows.Where(e => e.label=="Applied" && e.parent==key).Select(e=>e.emission).Distinct().Count();
                int pushes = rows.Count(e => e.label=="激活" && e.parent==key && e.detail.Contains(nameof(TargetKnockbackEffect)));
                int tails = rows.Count(e=>e.label=="激活" && e.parent==key && e.detail.Contains(tail.id));
                Check(lines, !earlyTail && tails==1, $"指定收招片段：持有期间提前触发 {earlyTail}，松键后触发 {tails} 次");
                bool finished = rows.Any(e=>e.label=="Finished" && e.action==key) && !actor.ActionActive;
                float travel = lab.Target.transform.position.x-from;
                Check(lines, id!=Guid.Empty && start && loop && loopTime>.14f && end && phases==2 && finished,
                    $"芙蕾真实 Q 三段（共享样板，现用骨架），起手 {start}，循环轨时 {loopTime:F3}，松键收招 {end}，同动作换相 {phases}，终结 {finished}");
                float expectedTravel = sample.effects.entries.OfType<TargetKnockbackEffect>().Single().distance * pulses;
                Check(lines,pulses>=2 && pushes==pulses && Mathf.Abs(travel-expectedTravel)<.012f && hp>lab.Target.Vitals.Health.Current,
                    $"圆锯跨脉冲词条，真实接触 {pulses}，推动激活 {pushes}，靶根 Δx={travel:F3} / 预期 {expectedTravel:F3}");
                Check(lines, spent>0 && posture.ActionLedger?.RecoveryBlockers==0,
                    $"圆锯架势，支出 {spent:F1}，收招后恢复阻塞 {posture.ActionLedger?.RecoveryBlockers}");
            }
            finally
            {
                Keys(keyboard);
                ActionEffectList.EditorOverride = previous;
                lab.ResetTrial("圆锯验收结束");
                restoreResources();
                panel.ReturnSaw();
                bool hasQ = selections.TryGetValue(0, out string restoredQ);
                Check(lines, equipment.RightDefinition==originalWeapon && ability.SkillDefinition(0)==originalSkill &&
                    hasQ==hadQ && restoredQ==oldQ,"归还后原主手引用、Q 定义与原槽位选择均恢复");
                panel.Dispose();
                if (sample) Object.Destroy(sample);
            }
        }

        private static IEnumerator DiveCase(AttackLabSession lab, List<string> lines, Keyboard keyboard)
        {
            ActionEffectList.EditorOverride = null;
            var actor = lab.Actor;
            var mount = actor.GetComponent<SideViewDiveMount>();
            if (!mount || !mount.IsAvailable) { Check(lines,false,"芙蕾没有正式下冲来源"); yield break; }
            lab.Distance=1.1f;
            lab.Condition=AttackLabTargetMode.Fixed;
            lab.ResetTrial("芙蕾跳跃下冲");
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                yield return new WaitForSeconds(.24f);
                float floor = actor.transform.position.y;
                Keys(keyboard,Key.Space);
                float until=Time.realtimeSinceStartup+1f;
                while (actor.transform.position.y < floor+1.48f && Time.realtimeSinceStartup < until) yield return null;
                float jump=actor.transform.position.y-floor;
                Vector3 aim=(lab.Target.transform.position+Vector3.up*.38f-actor.transform.position).normalized;
                Vector2 cursor=Camera.main.WorldToScreenPoint(actor.transform.position+aim*2.7f);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=cursor});
                Keys(keyboard,Key.S);
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=cursor}.WithButton(MouseButton.Left));
                yield return null; yield return null; yield return null;
                Keys(keyboard);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=cursor});
                yield return new WaitForSeconds(1.1f);
                var rows=lab.Trace.Events.Where(e=>e.trial==lab.Generation).ToArray();
                var accepted=rows.FirstOrDefault(e=>e.label=="accepted" && e.detail.StartsWith(mount.Definition.definitionId+" · "));
                bool landed=accepted!=null && rows.Any(e=>e.action==accepted.action && e.label=="dive-land");
                bool bounced=accepted!=null && rows.Any(e=>e.action==accepted.action && e.label=="dive-bounce");
                Check(lines,jump>1f && accepted!=null && bounced &&
                    rows.Count(e=>e.action==accepted.action && e.label=="dive-bounce")==1 && !actor.ActionActive,
                    $"原下冲，起跳 {jump:F2}，受理 {accepted!=null}，碰靶腾起 {bounced}，触地收招 {landed}，空闲 {!actor.ActionActive}");
            }
            finally { Keys(keyboard); InputSystem.RemoveDevice(mouse); }
        }

        private static IEnumerator BrokenPosture(AttackLabSession lab,List<string> lines)
        {
            lab.Condition = AttackLabTargetMode.Normal;
            lab.Distance = 1.25f;
            lab.EnemyAttacks = false;
            if (!lab.ResetTrial("零架势出招与敌击破防")) throw new InvalidOperationException(lab.Status);
            yield return new WaitForSeconds(.2f);
            var actor = lab.Actor;
            var enemy = lab.Target;
            var posture = actor.GetComponent<CombatPosture>();
            var movement = actor.GetComponent<CharacterMovement>();
            var reaction = actor.StateArbitrator.Reaction;
            var definition = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            definition.effects.entries.Add(new TargetKnockbackEffect { distance=1.8f, duration=.15f });
            definition.effects.entries.Add(new LaunchEffect { target=true, velocity=new Vector2(0,35) });
            var binding = enemy.gameObject.AddComponent<EnemyActionImpactBinding>();
            binding.effectSource = definition;
            var seed = Guid.NewGuid();
            int hp = actor.Vitals.Health.Current, enemyHits = 0;
            bool brokenOnReceipt = false, independentMotion = false;
            void Observe(CombatContactBatch batch)
            {
                if (batch.Source != enemy.Vitals) return;
                foreach (var receipt in batch.Receipts)
                {
                    if (receipt.Target != actor.Vitals || receipt.Outcome.Reason != HitReason.Applied) continue;
                    enemyHits++;
                    brokenOnReceipt = receipt.Outcome.HpAfter < receipt.Outcome.HpBefore &&
                        reaction.Phase==PostureReactionPhase.Launched && reaction.ActionId==receipt.Outcome.ActionId;
                    var impact = actor.GetComponent<ActionImpactMotor>();
                    independentMotion = impact && impact.HasMotion;
                    enemy.AttacksEnabled = false;
                    posture.EndActionExposure(seed);
                }
            }
            CombatContactGateway.Resolved += Observe;
            try
            {
                posture.BeginActionExposure(seed,PostureActionGroup.Locomotion,posture.Maximum,true);
                bool zero = posture.Current<.001 && !posture.IsStaggered;
                bool accepted = lab.RequestAttack() && actor.ActionActive && posture.Current<.001 && !posture.IsStaggered;
                var phases = new List<PostureReactionPhase> { reaction.Phase };
                float origin=actor.transform.position.y, peak=0;
                bool airborne=false, groundedWhenDown=false;
                enemy.AttacksEnabled=true;
                float until=Time.realtimeSinceStartup+7f;
                while (Time.realtimeSinceStartup<until)
                {
                    var phase = reaction.Phase;
                    if (phase!=phases[phases.Count-1]) phases.Add(phase);
                    airborne |= phase==PostureReactionPhase.Launched && !movement.Grounded;
                    groundedWhenDown |= phase==PostureReactionPhase.Downed && movement.Grounded;
                    peak = Mathf.Max(peak,actor.transform.position.y-origin);
                    if (phases.Contains(PostureReactionPhase.GettingUp) && phase==PostureReactionPhase.Inactive) break;
                    yield return null;
                }
                var expected = new[]{PostureReactionPhase.Inactive,PostureReactionPhase.Launched,PostureReactionPhase.Downed,
                    PostureReactionPhase.GettingUp,PostureReactionPhase.Inactive};
                bool recovered = phases.SequenceEqual(expected) && movement.Grounded &&
                    Mathf.Abs(actor.transform.position.y-origin)<.11f && !posture.IsStaggered;
                Check(lines,zero && accepted && hp>actor.Vitals.Health.Current && enemyHits==1 && brokenOnReceipt &&
                    !independentMotion && airborne && peak>.1f && groundedWhenDown && recovered,
                    $"零架势出招 {zero}/{accepted}；实际敌击 {enemyHits}，生命 {hp}→{actor.Vitals.Health.Current}，同一回执破防 {brokenOnReceipt}，普通运动通道占用 {independentMotion}，根峰高 {peak:F3}，倒地接地 {groundedWhenDown}，恢复 {recovered}，{string.Join("/",phases)}");
            }
            finally
            {
                CombatContactGateway.Resolved -= Observe;
                posture.EndActionExposure(seed);
                enemy.AttacksEnabled = false;
                Object.Destroy(binding);
                Object.Destroy(definition);
            }
        }

        private static IEnumerator PostureSwitch(AttackLabSession lab,List<string> lines,int original)
        {
            var before=lab.Actor;
            lab.Condition=AttackLabTargetMode.Whiff;
            lab.ResetTrial("切换及架势");
            yield return new WaitForSeconds(.2f);
            var posture=before.GetComponent<CombatPosture>();
            double initial=posture.Current;
            bool attack=lab.RequestAttack();
            double spend=initial-posture.Current;
            yield return new WaitForSeconds(.95f);
            bool released=!before.ActionActive && posture.ActionLedger?.RecoveryBlockers==0;
            int next=lab.Party.Characters.ToList().FindIndex(a=>a && a!=before);
            if (original>=0 && lab.Party.Characters[original]!=before) next=original;
            bool transferred = next>=0 && lab.SwitchActor(next);
            // 协调者有意屏蔽切控当帧的输入，下一次正常更新才可确认新控制权。
            yield return null;
            yield return new WaitForSeconds(.15f);
            bool switched = transferred && lab.Actor == lab.Party.Characters[next] && lab.Actor != before && lab.Actor.HasPlayerInput && !before.HasPlayerInput;
            bool newAttack=switched && lab.RequestAttack();
            yield return new WaitForSeconds(.95f);
            Check(lines,attack && spend>0 && released && newAttack && !lab.Actor.ActionActive,
                $"架势及交接，旧角色动作支出 {spend:F1}，恢复阻塞清除 {released}，已选 {transferred}/{lab.Actor.ActorId}，独占输入 {switched}，新角色出招 {newAttack}");
        }
    }
}
