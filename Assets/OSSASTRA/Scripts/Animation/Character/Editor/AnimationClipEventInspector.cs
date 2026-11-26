/*
 * 脚本：AnimationClipEventInspector
 * 功能：展示真实片段时间轴并编辑归属该片段的响应。
 * 协作：Mapping Inspector 与横版攻击调试台共享本界面，Undo 和写盘只作用于真实来源资产。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Spine;
using UnityEditor;
using UnityEngine;

namespace Ossastra.CharacterAnimation.Editor
{
    internal sealed class AnimationClipEventInspector
    {
        private static readonly string[] Meanings =
        {
            "出手释放", "完成并开放后续动作", "开放冲刺取消", "脚步", "仅表现", "不处理"
        };

        private readonly HashSet<AnimationCalibrationBinding> expanded = new HashSet<AnimationCalibrationBinding>();
        private string search = "";
        private bool onlyUnresolved;
        private bool showDefinitions;
        private ActionDefinitionSO pendingAction;
        private ActionDefinitionSO selectedAction;

        public void Draw(ScriptableObject owner, CharacterAnimationClip clip, ActionDefinitionSO action = null,
            Action<AnimationCalibrationBinding> previewSound = null, Action<AnimationCalibrationBinding> previewEffect = null)
        {
            if (clip == null)
                return;

            var data = clip.skeletonDataAsset?.GetSkeletonData(true);
            var animation = data?.FindAnimation(clip.animaName);
            if (animation == null)
            {
                EditorGUILayout.HelpBox("找不到当前骨架与片段，请检查动画字段。已有响应保留在此条目。", MessageType.Warning);
                return;
            }

            var config = clip.eventResponses ?? new AnimationClipEventConfiguration();
            var actionOverrides = config.actionOverrides ?? Array.Empty<AnimationClipActionEvents>();
            var scope = DrawScope(owner, clip, config, actionOverrides, action);
            var responses = (scope == null ? config.responses : scope.responses) ?? Array.Empty<AnimationCalibrationBinding>();
            var fps = scope == null ? config.framesPerSecond : scope.framesPerSecond;
            var drivesTiming = scope == null ? config.drivesActionTiming : scope.drivesActionTiming;
            fps = DrawTiming(owner, clip, config, scope, fps, drivesTiming);

            var events = AnimationCalibrationSO.Events(animation).ToArray();
            var valid = new Dictionary<AnimationCalibrationBinding, Spine.Event[]>();
            var failures = new Dictionary<AnimationCalibrationBinding, string>();
            foreach (var binding in responses)
            {
                if (binding == null)
                    continue;
                if (AnimationClipEventConfiguration.TryResolveSource(binding, animation, out var source, out var error))
                    valid[binding] = source;
                else
                    failures[binding] = error;
            }

            if (drivesTiming && (valid.Keys.Count(b => b.semantic == AnimationEventMeaning.Finish) != 1 ||
                !valid.Keys.Any(b => b.semantic == AnimationEventMeaning.Release)))
                EditorGUILayout.HelpBox("事件驱动需要有效的出手事件与唯一的结束事件。", MessageType.Warning);

            EditorGUILayout.LabelField($"轨道 {clip.track}  ·  {animation.Duration:0.###} 秒  ·  {events.Length} 个事件", EditorStyles.miniLabel);
            search = EditorGUILayout.TextField("筛选事件", search);
            onlyUnresolved = EditorGUILayout.ToggleLeft("仅显示异常和未处理事件", onlyUnresolved);
            foreach (var source in events)
            {
                if (!MatchesSearch(source.Data.Name))
                    continue;

                var rows = valid.Where(pair => pair.Value.Contains(source)).Select(pair => pair.Key).ToArray();
                var warning = rows.Any(binding => !HasValidParameters(binding, source, animation, data, fps));
                if (onlyUnresolved && rows.Length > 0 && !warning)
                    continue;

                // 集合响应在所有命中行都显示摘要，编辑面板只在集合第一处出现。
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    int ordinal = events.TakeWhile(e => e != source).Count(e => e.Data.Name == source.Data.Name);
                    var summary = rows.Length == 0 ? "未处理" : string.Join("、", rows.Select(Describe));
                    EditorGUILayout.LabelField($"{source.Data.Name}  #{ordinal + 1}  ·  {source.Time * fps:0.##} 帧  ·  {source.Time:0.###} s",
                        EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel);
                        if (GUILayout.Button("添加响应", GUILayout.Width(78)))
                            ShowAddMenu(owner, clip, config, scope, responses, rows, animation, source, events);
                    }

                    if (warning)
                        EditorGUILayout.HelpBox("部分音画挂点或偏移超出当前片段可用范围。", MessageType.Warning);
                    foreach (var binding in rows)
                    {
                        if (valid[binding][0] == source)
                            DrawResponse(owner, clip, config, scope, responses, binding, data, previewSound, previewEffect);
                    }
                }
            }

            foreach (var pair in failures)
            {
                if (!MatchesSearch(pair.Key.eventName))
                    continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox($"{pair.Key.eventName} · {Describe(pair.Key)}\n{pair.Value}", MessageType.Warning);
                    if (GUILayout.Button("重新选择真实事件"))
                        ShowRebindMenu(owner, clip, config, scope, responses, pair.Key, animation, events);
                    DrawResponse(owner, clip, config, scope, responses, pair.Key, data, previewSound, previewEffect);
                }
            }

            showDefinitions = EditorGUILayout.Foldout(showDefinitions, "骨架声明的事件（参考）", true);
            if (showDefinitions)
                EditorGUILayout.LabelField(string.Join("、", data.Events.Select(item => item.Name)), EditorStyles.wordWrappedLabel);

            if (owner == null)
                return;

            using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(owner)))
            {
                if (GUILayout.Button("保存此动画映射"))
                    AssetDatabase.SaveAssetIfDirty(owner);
            }
        }

        private AnimationClipActionEvents DrawScope(ScriptableObject owner, CharacterAnimationClip clip,
            AnimationClipEventConfiguration config, AnimationClipActionEvents[] overrides, ActionDefinitionSO context)
        {
            var scopes = overrides.Where(entry => entry != null && entry.action != null).ToArray();
            if (context != null)
                selectedAction = context;

            if (scopes.Length > 0 && context == null)
            {
                var labels = new[] { "片段默认" }.Concat(scopes.Select(entry => entry.action.name)).ToArray();
                int index = Array.FindIndex(scopes, entry => entry.action == selectedAction) + 1;
                index = EditorGUILayout.Popup("适用动作", index, labels);
                selectedAction = index == 0 ? null : scopes[index - 1].action;
            }

            var scope = scopes.FirstOrDefault(entry => entry.action == selectedAction);
            if (scope != null)
            {
                EditorGUILayout.LabelField("动作专用响应", scope.action.name);
                if (GUILayout.Button("移除专用响应，恢复片段默认") &&
                    EditorUtility.DisplayDialog("恢复片段默认", $"移除「{scope.action.name}」的专用响应？", "移除", "保留"))
                {
                    Edit(owner, clip, config, () => config.actionOverrides = overrides.Where(entry => entry != scope).ToArray());
                    scope = null;
                }
            }

            if (scope == null)
            {
                pendingAction = context != null ? context : (ActionDefinitionSO)EditorGUILayout.ObjectField(
                    "新增动作专用（可选）", pendingAction, typeof(ActionDefinitionSO), false);
                using (new EditorGUI.DisabledScope(pendingAction == null || scopes.Any(entry => entry.action == pendingAction)))
                {
                    if (GUILayout.Button("为该动作复制片段默认响应"))
                    {
                        var added = new AnimationClipActionEvents
                        {
                            action = pendingAction,
                            drivesActionTiming = config.drivesActionTiming,
                            framesPerSecond = config.framesPerSecond,
                            responses = (config.responses ?? Array.Empty<AnimationCalibrationBinding>())
                                .Where(binding => binding != null).Select(binding => binding.Copy()).ToArray()
                        };
                        Edit(owner, clip, config, () => config.actionOverrides = overrides.Append(added).ToArray());
                        selectedAction = pendingAction;
                        pendingAction = null;
                        scope = added;
                    }
                }
            }
            return scope;
        }

        private static int DrawTiming(ScriptableObject owner, CharacterAnimationClip clip,
            AnimationClipEventConfiguration config, AnimationClipActionEvents scope, int fps, bool drivesTiming)
        {
            EditorGUI.BeginChangeCheck();
            bool timing = EditorGUILayout.Toggle(new GUIContent("事件驱动动作时序", "动作主轨的出手与结束由此片段的响应决定。"),
                drivesTiming);
            int newFps = Mathf.Clamp(EditorGUILayout.DelayedIntField("制作帧率", fps), 1, 240);
            if (EditorGUI.EndChangeCheck())
            {
                Edit(owner, clip, config, () =>
                {
                    if (scope == null)
                    {
                        config.drivesActionTiming = timing;
                        config.framesPerSecond = newFps;
                    }
                    else
                    {
                        scope.drivesActionTiming = timing;
                        scope.framesPerSecond = newFps;
                    }
                });
            }
            return Mathf.Max(1, newFps);
        }

        private void ShowAddMenu(ScriptableObject owner, CharacterAnimationClip clip, AnimationClipEventConfiguration config,
            AnimationClipActionEvents scope, AnimationCalibrationBinding[] responses, AnimationCalibrationBinding[] current,
            Spine.Animation animation, Spine.Event source, Spine.Event[] events)
        {
            var menu = new GenericMenu();
            AddChoices("仅本次", false);
            if (events.Count(e => e.Data.Name == source.Data.Name) > 1)
                AddChoices("全部同名", true);
            menu.ShowAsContext();

            void AddChoices(string label, bool allNamed)
            {
                for (int i = 0; i < Meanings.Length; i++)
                {
                    var meaning = (AnimationEventMeaning)i;
                    var content = new GUIContent(label + "/" + Meanings[i]);
                    if (current.Any(binding => binding.semantic == meaning))
                    {
                        menu.AddDisabledItem(content);
                        continue;
                    }
                    menu.AddItem(content, false, () =>
                    {
                        var binding = new AnimationCalibrationBinding { semantic = meaning };
                        if (allNamed)
                            AnimationClipEventConfiguration.ConfigureAllNamed(binding, animation, source.Data.Name);
                        else
                            AnimationClipEventConfiguration.AssignSource(binding, animation, source);
                        ReplaceResponses(owner, clip, config, scope, responses.Append(binding).ToArray());
                        expanded.Add(binding);
                    });
                }
            }
        }

        private static void ShowRebindMenu(ScriptableObject owner, CharacterAnimationClip clip, AnimationClipEventConfiguration config,
            AnimationClipActionEvents scope, AnimationCalibrationBinding[] responses, AnimationCalibrationBinding binding,
            Spine.Animation animation, Spine.Event[] events)
        {
            var menu = new GenericMenu();
            var seen = new HashSet<string>();
            foreach (var source in events)
            {
                var item = source;
                var ordinal = events.TakeWhile(e => e != item).Count(e => e.Data.Name == item.Data.Name) + 1;
                // '/' 在 Spine 名称中合法，菜单用全角替代显示以保持单级选项。
                var title = item.Data.Name.Replace('/', '／');
                menu.AddItem(new GUIContent($"仅本次/{title} #{ordinal}  {item.Time:0.###} s"), false,
                    () => Rebind(next => AnimationClipEventConfiguration.AssignSource(next, animation, item)));
                if (seen.Add(item.Data.Name) && events.Count(e => e.Data.Name == item.Data.Name) > 1)
                    menu.AddItem(new GUIContent("全部同名/" + title), false,
                        () => Rebind(next => AnimationClipEventConfiguration.ConfigureAllNamed(next, animation, item.Data.Name)));
            }
            if (events.Length == 0)
                menu.AddDisabledItem(new GUIContent("该片段没有事件"));
            menu.ShowAsContext();

            void Rebind(Action<AnimationCalibrationBinding> assign)
            {
                var next = binding.Copy();
                assign(next);
                // 只重写源身份，保留独立制作的音画、挂点及帧偏移。
                ReplaceResponses(owner, clip, config, scope, responses.Select(row => row == binding ? next : row).ToArray());
            }
        }

        private void DrawResponse(ScriptableObject owner, CharacterAnimationClip clip, AnimationClipEventConfiguration config,
            AnimationClipActionEvents scope, AnimationCalibrationBinding[] responses, AnimationCalibrationBinding binding,
            SkeletonData data, Action<AnimationCalibrationBinding> previewSound, Action<AnimationCalibrationBinding> previewEffect)
        {
            bool wasExpanded = expanded.Contains(binding);
            bool open = EditorGUILayout.Foldout(wasExpanded, Describe(binding), true);
            if (!open)
            {
                expanded.Remove(binding);
                return;
            }
            expanded.Add(binding);

            var next = binding.Copy();
            EditorGUI.BeginChangeCheck();
            next.semantic = (AnimationEventMeaning)EditorGUILayout.Popup("用途", (int)next.semantic, Meanings);
            if (next.semantic != AnimationEventMeaning.Ignore)
            {
                next.offsetFrames = EditorGUILayout.DelayedIntField("标记偏移（帧）", next.offsetFrames);
                next.restorePosture = EditorGUILayout.Toggle("恢复本动作架势资格", next.restorePosture);
                next.sound = (AudioClip)EditorGUILayout.ObjectField("音效", next.sound, typeof(AudioClip), false);
                if (next.sound != null)
                {
                    next.soundOffsetFrames = EditorGUILayout.DelayedIntField("音效补偿（帧）", next.soundOffsetFrames);
                    next.volume = EditorGUILayout.Slider("音量", next.volume, 0, 1);
                }
                next.effect = (GameObject)EditorGUILayout.ObjectField("特效预制体", next.effect, typeof(GameObject), false);
                if (next.effect != null || next.sound != null)
                {
                    next.anchor = DrawAnchor(next.anchor, data);
                    next.localOffset = EditorGUILayout.Vector3Field("挂点局部偏移", next.localOffset);
                }
                if (next.effect != null)
                {
                    next.effectOffsetFrames = EditorGUILayout.DelayedIntField("特效补偿（帧）", next.effectOffsetFrames);
                    next.rotation = EditorGUILayout.DelayedFloatField("局部旋转（度）", next.rotation);
                    next.scale = Mathf.Max(.001f, EditorGUILayout.DelayedFloatField("特效缩放", next.scale));
                    next.lifetime = Mathf.Max(.001f, EditorGUILayout.DelayedFloatField("特效寿命（秒）", next.lifetime));
                    next.follow = EditorGUILayout.Toggle("跟随挂点", next.follow);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                expanded.Remove(binding);
                expanded.Add(next);
                ReplaceResponses(owner, clip, config, scope, responses.Select(row => row == binding ? next : row).ToArray());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (next.semantic != AnimationEventMeaning.Ignore && next.sound != null &&
                    previewSound != null && GUILayout.Button("试听"))
                    previewSound(next);
                if (next.semantic != AnimationEventMeaning.Ignore && next.effect != null &&
                    previewEffect != null && Application.isPlaying && GUILayout.Button("试放特效"))
                    previewEffect(next);
                if (GUILayout.Button("移除响应"))
                {
                    expanded.Remove(binding);
                    ReplaceResponses(owner, clip, config, scope, responses.Where(row => row != binding).ToArray());
                }
            }
        }

        private static string DrawAnchor(string chosen, SkeletonData data)
        {
            var options = new[] { "" }.Concat(data.Bones.Select(bone => bone.Name))
                .Concat(data.Slots.Select(slot => "slot:" + slot.Name)).ToList();
            if (!string.IsNullOrEmpty(chosen) && !options.Contains(chosen))
                options.Add(chosen);
            var display = options.Select(name => name.Length == 0 ? "角色根" :
                name.StartsWith("slot:", StringComparison.Ordinal) ? "插槽 / " + name.Substring(5) : "骨骼 / " + name).ToArray();
            return options[EditorGUILayout.Popup("骨骼 / 插槽", Mathf.Max(0, options.IndexOf(chosen)), display)];
        }

        private static bool HasValidParameters(AnimationCalibrationBinding binding, Spine.Event source, Spine.Animation animation,
            SkeletonData data, int fps)
        {
            if (binding.semantic == AnimationEventMeaning.Ignore)
                return true;
            float time = source.Time + (float)binding.offsetFrames / fps;
            bool InClip(float seconds) => seconds >= -0.000001f && seconds <= animation.Duration + 0.000001f;
            bool anchorValid = string.IsNullOrEmpty(binding.anchor) || (binding.IsSlotAnchor
                ? data.FindSlot(binding.AnchorName) != null : data.FindBone(binding.AnchorName) != null);
            return InClip(time) && (binding.effect == null && binding.sound == null || anchorValid)
                && (binding.sound == null || InClip(time + (float)binding.soundOffsetFrames / fps))
                && (binding.effect == null || InClip(time + (float)binding.effectOffsetFrames / fps));
        }

        private static string Describe(AnimationCalibrationBinding binding)
        {
            int index = (int)binding.semantic;
            var label = index >= 0 && index < Meanings.Length ? Meanings[index] : "未知响应";
            return binding.occurrence < 0 ? label + "（全部同名）" : label;
        }

        private bool MatchesSearch(string eventName) => string.IsNullOrWhiteSpace(search)
            || (eventName ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private static void ReplaceResponses(ScriptableObject owner, CharacterAnimationClip clip,
            AnimationClipEventConfiguration config, AnimationClipActionEvents scope, AnimationCalibrationBinding[] rows)
        {
            Edit(owner, clip, config, () =>
            {
                if (scope == null) config.responses = rows;
                else scope.responses = rows;
            });
        }

        internal static void Edit(ScriptableObject owner, CharacterAnimationClip clip, AnimationClipEventConfiguration config, Action apply)
        {
            if (owner != null)
                Undo.RecordObject(owner, "编辑动画片段事件响应");
            clip.eventResponses = config;
            apply();
            if (owner is AnimationPlanMappingSO mapping) mapping.Revision++;
            if (owner is AnimationPlanOverrideRuleSetSO overrides) overrides.Revision++;
            if (owner != null)
                EditorUtility.SetDirty(owner);
        }
    }
}
