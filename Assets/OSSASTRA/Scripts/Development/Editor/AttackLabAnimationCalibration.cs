/*
 * 脚本：AttackLabAnimationCalibration
 * 功能：在已有横版调试台定位角色的真实 Mapping 片段，展示实录及试听、试放。
 * 协作：用同一片段响应界面修改正式来源，试放只生成临时表现对象。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterAnimation;
using Ossastra.CharacterAnimation.Editor;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using AuthoredClip = Ossastra.CharacterAnimation.CharacterAnimationClip;
using AnimationConfig = Ossastra.CharacterAnimation.CharacterAnimationConfigManager;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabAnimationCalibration : IDisposable
    {
        private readonly struct ActionChoice
        {
            public readonly ActionDefinitionSO Definition;
            public readonly CharacterAnimationPreparation Preparation;
            public readonly string Label;

            public ActionChoice(ActionDefinitionSO definition, CharacterAnimationPreparation preparation, string label)
            {
                Definition = definition;
                Preparation = preparation;
                Label = label;
            }
        }

        private ActorRoot actor;
        private int actionIndex;
        private int clipIndex;
        private Vector2 scroll;
        private AudioSource preview;
        private AuthoredClip drawnClip;
        private AuthoredClip candidateClip;
        private CharacterAnimationController candidateDriver;
        private ScriptableObject candidateSource;
        private string candidateBaseline;
        private bool candidateActive;
        private AnimationClipEventInspector events = new AnimationClipEventInspector();

        public void Draw(AttackLabSession session)
        {
            if (!Application.isPlaying && candidateClip != null)
                ClearCandidate();

            var actors = Object.FindObjectsOfType<ActorRoot>()
                .Where(a => a.Definition != null && a.GetComponentInChildren<AnimationConfig>(true) != null)
                .OrderBy(a => a.name).ToArray();
            if (actors.Length == 0)
            {
                EditorGUILayout.HelpBox("打开含正式角色的场景，或从实战验证进入现有训练场。", MessageType.Info);
                return;
            }

            var index = Array.IndexOf(actors, actor != null ? actor : session?.Actor);
            var selected = EditorGUILayout.Popup("角色", Mathf.Max(0, index), actors.Select(a => a.name).ToArray());
            if (actor != actors[selected])
            {
                ClearCandidate();
                actor = actors[selected];
                actionIndex = clipIndex = 0;
            }

            var driver = actor.GetComponentInChildren<CharacterAnimationController>(true);
            var config = actor.GetComponentInChildren<AnimationConfig>(true);
            var equipment = actor.GetComponent<CharacterEquipment>();
            var actionOptions = Actions(actor, equipment).ToArray();
            var selectedAction = EditorGUILayout.Popup("定位动作", Mathf.Clamp(actionIndex, 0, actionOptions.Length),
                new[] { "浏览所用映射" }.Concat(actionOptions.Select(option => option.Label)).ToArray());
            if (selectedAction != actionIndex)
            {
                actionIndex = selectedAction;
                clipIndex = 0;
            }

            var action = selectedAction == 0 ? default : actionOptions[selectedAction - 1];
            var clips = FindClips(config, driver, equipment, action, out var isResolved);
            if (clips.Length == 0)
            {
                EditorGUILayout.HelpBox("当前角色或动作没有可定位的作者片段。请核对动画逻辑映射。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("受控武器", equipment?.RightDefinition?.name ?? "未装配主手");
            EditorGUILayout.LabelField(isResolved ? "当前路由解析" : "角色引用的作者映射", EditorStyles.miniBoldLabel);
            if (!isResolved && action.Definition != null)
                EditorGUILayout.HelpBox("显示该逻辑的作者配置候选。播放时可核对当时的覆盖条件和实际来源。", MessageType.Info);

            clipIndex = EditorGUILayout.Popup("片段来源", Mathf.Clamp(clipIndex, 0, clips.Length - 1),
                clips.Select(choice => choice.Label).ToArray());
            var chosen = clips[clipIndex];
            if (GUILayout.Button("在 Inspector 定位来源"))
            {
                Selection.activeObject = chosen.Source;
                EditorGUIUtility.PingObject(chosen.Source);
            }

            if (drawnClip != chosen.Clip)
            {
                drawnClip = chosen.Clip;
                ClearCandidate();
                events = new AnimationClipEventInspector();
            }

            DrawCandidateControls(chosen, driver, isResolved);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;
                DrawPlayback(session, chosen, action.Definition, candidateActive ? candidateClip?.eventResponses : null);
                if (candidateClip != null)
                    events.Draw(null, candidateClip, action.Definition, PreviewSound, PreviewEffect);
                else
                    events.Draw(chosen.Source, chosen.Clip, action.Definition, PreviewSound, PreviewEffect);
            }

            void PreviewEffect(AnimationCalibrationBinding binding)
            {
                if (driver == null || driver.Skeleton?.Skeleton == null || binding.effect == null || !Application.isPlaying)
                    return;

                var instance = Object.Instantiate(binding.effect, driver.AnchorPosition(binding), driver.AnchorRotation(binding));
                instance.transform.localScale *= binding.scale;
                Object.Destroy(instance, binding.lifetime);
            }
        }

        private void DrawCandidateControls(AnimationAuthoredClips.Entry chosen, CharacterAnimationController driver, bool isResolved)
        {
            if (candidateClip != null && (!isResolved || candidateDriver != driver || candidateSource != chosen.Source))
                ClearCandidate();

            if (candidateClip == null)
            {
                using (new EditorGUI.DisabledScope(!isResolved || driver == null || string.IsNullOrEmpty(chosen.Clip.eventClipId)))
                {
                    if (GUILayout.Button("为已解析动作建立临时候选"))
                    {
                        candidateDriver = driver;
                        candidateSource = chosen.Source;
                        candidateClip = chosen.Clip.Clone();
                        candidateClip.eventResponses = CopyConfiguration(chosen.Clip.eventResponses);
                        candidateBaseline = JsonUtility.ToJson(chosen.Clip.eventResponses);
                    }
                }
                return;
            }

            EditorGUILayout.HelpBox("正在编辑此角色的临时候选。切换基线 / 候选后，下次动作采用所选配置。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool preview = GUILayout.Toolbar(candidateActive ? 1 : 0, new[] { "A · 源配置", "B · 临时候选" }) == 1;
                if (preview != candidateActive)
                {
                    candidateActive = preview;
                    if (preview)
                        driver.SetEventResponsePreview(candidateSource, candidateClip.eventClipId, candidateClip.eventResponses);
                    else
                        driver.ClearEventResponsePreview();
                }

                if (GUILayout.Button("放弃候选"))
                {
                    ClearCandidate();
                    return;
                }
            }

            bool changedOutside = candidateBaseline != JsonUtility.ToJson(chosen.Clip.eventResponses);
            if (changedOutside)
                EditorGUILayout.HelpBox("源配置已在别处修改，请放弃后从最新配置建立候选。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(changedOutside))
            {
                if (GUILayout.Button("将候选保存到当前映射片段"))
                {
                    var accepted = CopyConfiguration(candidateClip.eventResponses);
                    AnimationClipEventInspector.Edit(chosen.Source, chosen.Clip, accepted, () => { });
                    AssetDatabase.SaveAssetIfDirty(chosen.Source);
                    ClearCandidate();
                }
            }
        }

        private static AnimationClipEventConfiguration CopyConfiguration(AnimationClipEventConfiguration source)
        {
            if (source == null) return new AnimationClipEventConfiguration();
            return new AnimationClipEventConfiguration
            {
                framesPerSecond = source.framesPerSecond,
                drivesActionTiming = source.drivesActionTiming,
                responses = CopyResponses(source.responses),
                actionOverrides = (source.actionOverrides ?? Array.Empty<AnimationClipActionEvents>())
                    .Where(entry => entry != null).Select(entry => new AnimationClipActionEvents
                    {
                        action = entry.action,
                        framesPerSecond = entry.framesPerSecond,
                        drivesActionTiming = entry.drivesActionTiming,
                        responses = CopyResponses(entry.responses)
                    }).ToArray()
            };

            AnimationCalibrationBinding[] CopyResponses(AnimationCalibrationBinding[] responses)
                => (responses ?? Array.Empty<AnimationCalibrationBinding>())
                    .Where(response => response != null).Select(response => response.Copy()).ToArray();
        }

        private void ClearCandidate()
        {
            if (candidateDriver != null)
                candidateDriver.ClearEventResponsePreview();
            candidateClip = null;
            candidateDriver = null;
            candidateSource = null;
            candidateBaseline = null;
            candidateActive = false;
        }

        private static IEnumerable<ActionChoice> Actions(ActorRoot root, CharacterEquipment equipment)
        {
            var attacks = root.Definition.basicAttacks ?? Array.Empty<ActionDefinitionSO>();
            for (int index = 0; index < attacks.Length; index++)
                if (attacks[index] != null)
                    yield return new ActionChoice(attacks[index], new CharacterAnimationPreparation(true, index + 1, true),
                        $"角色基础 {index + 1} / {attacks[index].name}");

            foreach (var profile in equipment?.RightDefinition?.mainActionProfiles ?? Array.Empty<MainActionProfileSO>())
            {
                if (profile?.stages == null)
                    continue;
                for (int index = 0; index < profile.stages.Length; index++)
                {
                    var stage = profile.stages[index];
                    if (stage?.action != null)
                        yield return new ActionChoice(stage.action, new CharacterAnimationPreparation(true, index + 1, true),
                            $"{profile.name} · 第 {index + 1} 段 / {stage.action.name}");
                }
            }

            // 编辑态也能读取当前装备的能力目录，运行中可对照实际路由结果。
            var loadout = equipment?.RightDefinition?.skillLoadout;
            if (loadout == null)
                yield break;
            var abilities = (loadout.regularChoices ?? Array.Empty<AbilityDefinitionSO>())
                .Concat(loadout.experimentalChoices ?? Array.Empty<AbilityDefinitionSO>())
                .Append(loadout.nativeAbility);
            foreach (var ability in abilities.Where(item => item != null).Distinct())
            {
                if (ability.action != null)
                    yield return AbilityAction(ability, ability.action, "起手");
                if (ability.executor is SustainedAttackAbilitySO sustained)
                {
                    if (sustained.loopAction != null)
                        yield return AbilityAction(ability, sustained.loopAction, "维持");
                    if (sustained.endAction != null)
                        yield return AbilityAction(ability, sustained.endAction, "收招");
                }
            }
        }

        private static ActionChoice AbilityAction(AbilityDefinitionSO ability, ActionDefinitionSO action, string phase)
            => new ActionChoice(action, new CharacterAnimationPreparation(true), $"{ability.name} · {phase} / {action.name}");

        private static AnimationAuthoredClips.Entry[] FindClips(AnimationConfig config, CharacterAnimationController driver,
            CharacterEquipment equipment, ActionChoice action, out bool isResolved)
        {
            var available = AnimationAuthoredClips.ForCharacter(config, equipment).ToArray();
            if (Application.isPlaying && action.Definition != null && driver != null && driver.Skeleton?.Skeleton != null)
            {
                var plan = driver.ResolveAction(action.Definition, action.Preparation);
                if (plan != null)
                {
                    // 用克隆保留的身份定位确切的持久条目，同名片段或其他轨道不会被误选。
                    var resolved = plan.EnumerateClips().Where(clip => clip.runtimeMapping != null &&
                        !string.IsNullOrEmpty(clip.eventClipId)).ToArray();
                    var matches = available.Where(choice => resolved.Any(clip => clip.runtimeMapping == choice.Source &&
                        clip.runtimeMappingLogic == choice.Logic && clip.eventClipId == choice.Clip.eventClipId)).ToArray();
                    if (matches.Length > 0)
                    {
                        isResolved = true;
                        return matches;
                    }
                }
            }

            isResolved = false;
            return action.Definition == null ? available : available.Where(choice => choice.Logic == action.Definition.animationLogicId).ToArray();
        }

        private void DrawPlayback(AttackLabSession session, AnimationAuthoredClips.Entry choice, ActionDefinitionSO action,
            AnimationClipEventConfiguration candidate)
        {
            var animation = choice.Clip.skeletonDataAsset?.GetSkeletonData(true)?.FindAnimation(choice.Clip.animaName);
            if (animation == null)
                return;

            var sourceTimes = AnimationCalibrationSO.Events(animation).Select(item => item.Time);
            var calibratedTimes = new List<float>();
            var configuration = candidate ?? choice.Clip.eventResponses;
            try
            {
                if (configuration != null && configuration.TrySelect(action, out var fps, out _, out var responses))
                {
                    foreach (var response in responses)
                        if (response != null && response.semantic != AnimationEventMeaning.Ignore &&
                            AnimationClipEventConfiguration.TryResolveSource(response, animation, out var items, out _))
                            calibratedTimes.AddRange(items.Select(item => item.Time + (float)response.offsetFrames / Mathf.Max(1, fps)));
                }
            }
            catch (ArgumentException error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Warning);
            }

            var records = session != null && session.Actor == actor && session.Trace != null
                ? session.Trace.Events.Where(item => item.trial == session.Generation &&
                    item.animationTrack == choice.Clip.track && item.clip == choice.Clip.animaName && item.markerSequence != 0)
                    .ToArray()
                : Array.Empty<AttackLabEvent>();
            DrawLane("源事件", sourceTimes, new Color(.54f, .78f, .9f));
            DrawLane("响应偏移", calibratedTimes, new Color(.84f, .72f, .39f));
            DrawLane("本次实录", records.Select(item => Mathf.Max(0, item.collectedTrackTime - item.markerCycle * animation.Duration)),
                new Color(.54f, .84f, .6f));

            var last = records.LastOrDefault();
            if (last != null)
                EditorGUILayout.LabelField($"最近实录  {last.label} · 有效 {last.effectiveMarkerTime:0.###} 秒 · 采集轨时 {last.collectedTrackTime:0.###} 秒",
                    EditorStyles.wordWrappedMiniLabel);

            void DrawLane(string title, IEnumerable<float> times, Color markerColor)
            {
                Rect row = EditorGUILayout.GetControlRect(false, 17);
                EditorGUI.LabelField(new Rect(row.x, row.y, 64, row.height), title, EditorStyles.miniLabel);
                row.xMin += 66;
                EditorGUI.DrawRect(row, new Color(.13f, .13f, .13f));
                foreach (float time in times)
                {
                    float normalized = Mathf.Clamp01(time / Mathf.Max(.001f, animation.Duration));
                    EditorGUI.DrawRect(new Rect(row.x + Mathf.Max(0, row.width - 3) * normalized, row.y + 2, 3, row.height - 4),
                        markerColor);
                }
            }
        }

        private void PreviewSound(AnimationCalibrationBinding binding)
        {
            if (binding.sound == null)
                return;
            if (preview == null)
            {
                var instance = EditorUtility.CreateGameObjectWithHideFlags("动画事件试听", HideFlags.HideAndDontSave, typeof(AudioSource));
                preview = instance.GetComponent<AudioSource>();
            }
            preview.Stop();
            preview.clip = binding.sound;
            preview.volume = binding.volume;
            preview.Play();
        }

        public void Dispose()
        {
            ClearCandidate();
            if (preview != null)
                Object.DestroyImmediate(preview.gameObject);
        }
    }
}
