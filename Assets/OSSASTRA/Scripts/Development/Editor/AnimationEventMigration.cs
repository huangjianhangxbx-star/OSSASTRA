/*
 * 功能：预览、校验并定向迁移指定角色的逐动画校准数据。
 * 协作：实际动画事件提供最新时间。只有通过校验的整组响应才写入选定 Mapping。
 * 边界：旧校准表用于回查。不会写入原骨架、场景或动作资产。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterAnimation;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using RuntimeAnimationPlanMappingSO = Ossastra.CharacterAnimation.AnimationPlanMappingSO;
using RuntimeOverrideRules = Ossastra.CharacterAnimation.AnimationPlanOverrideRuleSetSO;
using RuntimeAnimationLogic = Ossastra.CharacterAnimation.EAnimationLogicState;
using RuntimeCharacterAnimationClip = Ossastra.CharacterAnimation.CharacterAnimationClip;
using RuntimeCharacterAnimationPlan = Ossastra.CharacterAnimation.CharacterAnimationPlan;

namespace Ossastra.Development.Editor
{
    public static class AnimationEventMigration
    {
        public static string Run(ScriptableObject mapping, AnimationCalibrationSO legacy,
            IReadOnlyList<ActionDefinitionSO> actions, string defaultRecovery, bool apply)
        {
            var skeleton = (mapping as RuntimeAnimationPlanMappingSO)?.skeletonDataAsset ??
                (mapping as RuntimeOverrideRules)?.skeletonDataAsset;
            if (!mapping || !legacy || !legacy.MatchesSkeleton(skeleton))
                throw new ArgumentException("Mapping 与旧校准的骨架必须一致。");
            if (legacy.framesPerSecond <= 0)
                throw new ArgumentException(legacy.name + " 的制作帧率无效。");

            var data = skeleton.GetSkeletonData(false);
            if (data == null) throw new ArgumentException(mapping.name + " 无法读取骨架。");
            var entries = Entries(mapping).ToArray();
            var report = new List<string> { mapping.name + " ← " + legacy.name };
            var writes = new List<Action>();
            foreach (var action in actions.Where(a => a).Distinct())
            {
                var rows = legacy.bindings.Where(b => b != null && b.behavior == action.definitionId).ToArray();
                if (rows.Length == 0) continue;
                var plans = entries.Where(e => e.logic == action.animationLogicId && e.plan.HasOperations).ToArray();
                if (plans.Length != 1)
                {
                    report.Add(action.name + "：需人工确认唯一逻辑条目 " + action.animationLogicId);
                    continue;
                }
                var carrier = plans[0].plan.clips.FirstOrDefault(c => c != null && c.track == 0);
                var carriers = Clips(plans[0].plan).ToArray();
                var byClip = rows.GroupBy(b => (b.animation, b.track)).ToArray();
                var actionWrites = new List<Action>();
                bool complete = carrier != null;
                foreach (var group in byClip)
                {
                    var matching = carriers.Where(c => c.animaName == group.Key.animation && c.track == group.Key.track).ToArray();
                    if (matching.Length == 0)
                    {
                        report.Add(action.name + "：找不到 " + group.Key.animation + " [轨 " + group.Key.track + "]");
                        complete = false;
                        continue;
                    }
                    foreach (var clip in matching)
                    {
                        var cfg = clip.eventResponses;
                        if (cfg.actionOverrides.Any(o => o != null && o.action == action))
                        {
                            report.Add(action.name + " → " + clip.animaName + "：已有专用响应");
                            continue;
                        }
                        var recovery = string.IsNullOrWhiteSpace(action.postureRecoveryEvent) ? defaultRecovery : action.postureRecoveryEvent;
                        bool drivesTiming = clip == carrier && legacy.Drives(action.definitionId);
                        if (!TryBuild(legacy, data.FindAnimation(clip.animaName), group, recovery, drivesTiming, out var responses, out var issue))
                        {
                            report.Add(action.name + " / " + clip.animaName + "：" + issue);
                            complete = false;
                            continue;
                        }
                        report.Add(action.name + " → " + plans[0].logic + " / " + clip.animaName + " [轨 " + clip.track + "]，" +
                            string.Join("、", responses.Select(b => b.eventName + "@" + b.sourceTime.ToString("F3"))));
                        actionWrites.Add(() => cfg.actionOverrides = cfg.actionOverrides.Append(new AnimationClipActionEvents
                        {
                            action = action, drivesActionTiming = drivesTiming, framesPerSecond = legacy.framesPerSecond, responses = responses
                        }).ToArray());
                    }
                }
                if (complete && byClip.Any(g => g.Key.animation == carrier.animaName && g.Key.track == carrier.track))
                    writes.AddRange(actionWrites);
                else report.Add(action.name + "：保留旧来源，整组未写入");
            }

            foreach (var group in legacy.bindings.Where(b => b?.semantic == AnimationEventMeaning.Footstep)
                         .GroupBy(b => (b.animation, b.track)))
            {
                var matches = entries.SelectMany(e => Clips(e.plan)).Distinct()
                    .Where(c => c.animaName == group.Key.animation && c.track == group.Key.track).ToArray();
                if (matches.Length == 0)
                {
                    report.Add("脚步 " + group.Key.animation + "：找不到映射");
                    continue;
                }
                if (!TryBuild(legacy, data.FindAnimation(group.Key.animation), group, null, false,
                        out var responses, out var issue))
                {
                    report.Add("脚步 " + group.Key.animation + "：" + issue);
                    continue;
                }
                foreach (var clip in matches)
                {
                    var cfg = clip.eventResponses;
                    if (cfg.responses.Length > 0) { report.Add("脚步 " + clip.animaName + "：已有响应"); continue; }
                    report.Add("脚步 " + clip.animaName + "：" + responses.Length + " 个事件");
                    writes.Add(() =>
                    {
                        cfg.framesPerSecond = legacy.framesPerSecond;
                        cfg.responses = responses.Select(r => r.Copy()).ToArray();
                    });
                }
            }

            if (apply && writes.Count > 0)
            {
                Undo.RecordObject(mapping, "迁移动画事件响应");
                foreach (var write in writes) write();
                if (mapping is RuntimeAnimationPlanMappingSO baseMap) baseMap.Revision++;
                if (mapping is RuntimeOverrideRules rules) rules.Revision++;
                EditorUtility.SetDirty(mapping);
                AssetDatabase.SaveAssetIfDirty(mapping);
            }
            report.Add((apply ? "已写入 " : "预览待写入 ") + writes.Count + " 个片段配置");
            return string.Join("\n", report);
        }

        private static bool TryBuild(AnimationCalibrationSO legacy, Spine.Animation clip,
            IEnumerable<AnimationCalibrationBinding> rows, string recovery, bool drivesTiming,
            out AnimationCalibrationBinding[] responses, out string issue)
        {
            var list = new List<AnimationCalibrationBinding>();
            responses = null;
            issue = null;
            if (clip == null) { issue = "源动画缺失"; return false; }
            var events = AnimationCalibrationSO.Events(clip).ToArray();
            foreach (var old in rows)
            {
                var named = events.Where(e => e.Data.Name == old.eventName).ToArray();
                var equivalent = named.Where(e => e.Int == old.sourceInt && e.Float == old.sourceFloat &&
                    (e.String ?? "") == (old.sourceString ?? "")).ToArray();
                // 保留可靠的原实例。只在名称、次数和参数足以唯一识别时跟随新源时间。
                var source = legacy.SourceMatches(old, clip) ? named[old.occurrence]
                    : named.Length == old.sourceCount && equivalent.Length == 1 ? equivalent[0] : null;
                if (source == null) { issue = old.eventName + " 的源实例已缺失或存在歧义"; return false; }
                var copy = old.Copy();
                AnimationClipEventConfiguration.AssignSource(copy, clip, source);
                copy.behavior = null;
                copy.restorePosture = copy.eventName == recovery;
                if (!legacy.Valid(copy, clip))
                { issue = old.eventName + " 的偏移或挂点失效"; return false; }
                list.Add(copy);
            }
            if (drivesTiming)
            {
                var finish = list.Where(b => b.semantic == AnimationEventMeaning.Finish).ToArray();
                if (finish.Length != 1 || !list.Any(b => b.semantic == AnimationEventMeaning.Release) ||
                    list.Any(b => b.sourceTime + (double)b.offsetFrames / legacy.framesPerSecond >
                                  finish[0].sourceTime + (double)finish[0].offsetFrames / legacy.framesPerSecond))
                { issue = "释放或结束标记无效"; return false; }
            }
            responses = list.ToArray();
            return true;
        }

        private static IEnumerable<(RuntimeAnimationLogic logic, RuntimeCharacterAnimationPlan plan)> Entries(ScriptableObject mapping)
        {
            if (mapping is RuntimeAnimationPlanMappingSO map)
            {
                return new[] { map.plansMelee, map.plansMovement, map.plansFirearm, map.plansJump, map.plansTransition,
                        map.plansSkill, map.plansOther, map.defaultPlans }
                    .Where(group => group != null).SelectMany(group => group).Where(e => e?.plan != null)
                    .Select(e => (e.logicID, e.plan));
            }
            if (mapping is RuntimeOverrideRules rules)
            {
                return new[] { rules.rulesMelee, rules.rulesMovement, rules.rulesFirearm, rules.rulesJump, rules.rulesTransition,
                        rules.rulesSkill, rules.rulesOther, rules.rules }
                    .Where(group => group != null).SelectMany(group => group).Where(e => e?.replacementPlan != null)
                    .Select(e => (e.targetID, e.replacementPlan));
            }
            return Array.Empty<(RuntimeAnimationLogic, RuntimeCharacterAnimationPlan)>();
        }

        private static IEnumerable<RuntimeCharacterAnimationClip> Clips(RuntimeCharacterAnimationPlan plan) =>
            plan.clips.Concat(plan.movingLoopClips).Concat(plan.queuedClips.Select(q => q?.clip)).Where(c => c != null);
    }
}
