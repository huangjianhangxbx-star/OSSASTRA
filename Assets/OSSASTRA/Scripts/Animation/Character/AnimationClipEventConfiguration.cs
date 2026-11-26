/*
 * 脚本：AnimationClipEventConfiguration
 * 功能：保存单个映射片段的事件响应，识别实际 Spine 事件并冻结当前播放所需数据。
 * 边界：保留动作派发权与播放时钟，识别失败时保留作者原配置。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Spine;
using UnityEngine;

namespace Ossastra.CharacterAnimation
{
    [Serializable]
    public sealed class AnimationClipActionEvents
    {
        public ActionDefinitionSO action;
        [Min(1)] public int framesPerSecond = 30;
        public bool drivesActionTiming;
        public AnimationCalibrationBinding[] responses = Array.Empty<AnimationCalibrationBinding>();
    }

    [Serializable]
    public sealed class AnimationClipEventConfiguration
    {
        [Min(1)] public int framesPerSecond = 30;
        public bool drivesActionTiming;
        public AnimationCalibrationBinding[] responses = Array.Empty<AnimationCalibrationBinding>();
        public AnimationClipActionEvents[] actionOverrides = Array.Empty<AnimationClipActionEvents>();

        public bool TrySelect(ActionDefinitionSO action, out int fps, out bool drivesTiming,
            out AnimationCalibrationBinding[] selected)
        {
            fps = framesPerSecond;
            drivesTiming = drivesActionTiming;
            selected = responses ?? Array.Empty<AnimationCalibrationBinding>();
            AnimationClipActionEvents actionResponse = null;
            if (action != null && actionOverrides != null)
            {
                foreach (var candidate in actionOverrides)
                {
                    if (candidate == null || candidate.action != action) continue;
                    if (actionResponse != null)
                        throw new ArgumentException("同一动作存在多组专用事件响应：" + action.name);
                    actionResponse = candidate;
                }
            }

            if (actionResponse == null) return drivesTiming || selected.Length != 0;
            // 专用配置整体接管该片段；空的专用配置可以明确关闭该动作的默认响应。
            fps = actionResponse.framesPerSecond;
            drivesTiming = actionResponse.drivesActionTiming;
            selected = actionResponse.responses ?? Array.Empty<AnimationCalibrationBinding>();
            return true;
        }

        public static void AssignSource(AnimationCalibrationBinding binding, Spine.Animation clip, Spine.Event source)
        {
            if (binding == null || clip == null || source == null) throw new ArgumentNullException();
            var named = Named(clip, source.Data.Name);
            int occurrence = Array.IndexOf(named, source);
            if (occurrence < 0) throw new ArgumentException("事件不属于当前片段");
            // 片段提供作用域，不保留旧校准中的动作字符串或相邻事件拼接。
            binding.behavior = null;
            binding.previousEvent = null;
            binding.nextEvent = null;
            binding.animation = clip.Name;
            binding.eventName = source.Data.Name;
            binding.sourceCount = named.Length;
            binding.occurrence = occurrence;
            CaptureValues(binding, source);
        }

        public static void ConfigureAllNamed(AnimationCalibrationBinding binding, Spine.Animation clip, string name)
        {
            var named = Named(clip, name);
            if (named.Length == 0) throw new ArgumentException("当前片段没有指定事件：" + name);
            AssignSource(binding, clip, named[0]);
            binding.occurrence = -1;
        }

        public static bool TryResolveSource(AnimationCalibrationBinding binding, Spine.Animation clip,
            out Spine.Event[] sources, out string error)
        {
            sources = Array.Empty<Spine.Event>();
            if (binding == null || clip == null || clip.Name != binding.animation)
            {
                error = "原绑定属于其他片段或片段缺失";
                return false;
            }

            var named = Named(clip, binding.eventName);
            if (named.Length == 0)
            {
                error = "事件已移除或改名";
                return false;
            }
            if (binding.occurrence == -1)
            {
                sources = named;
                error = null;
                return true;
            }

            if (binding.occurrence < 0 || binding.occurrence >= named.Length ||
                binding.sourceCount != named.Length)
            {
                error = "同名事件数量变化，请重新选择发生次序";
                return false;
            }
            var source = named[binding.occurrence];
            if (!SameValues(binding, source) ||
                (source.Time != binding.sourceTime && named.Count(e => SameValues(binding, e)) > 1))
            {
                error = "源参数或同名事件身份变化，请重新选择";
                return false;
            }
            // 事件仍在相同同名序位且可唯一识别时，实际源时间始终来自当前片段。
            sources = new[] { source };
            error = null;
            return true;
        }

        internal static int Occurrence(Spine.Animation animation, Spine.Event source)
            => Array.IndexOf(Named(animation, source.Data.Name), source);

        internal static AnimationCalibrationBinding[] Freeze(Spine.Animation animation, SkeletonData skeleton,
            int track, int fps, IEnumerable<AnimationCalibrationBinding> source, bool controlsTiming,
            Action<string> diagnostic)
        {
            if (fps <= 0) throw new ArgumentException("事件制作帧率必须大于零");
            var frozen = new List<AnimationCalibrationBinding>();
            var markers = new HashSet<(string name, int occurrence, AnimationEventMeaning semantic)>();
            foreach (var binding in source)
            {
                if (binding == null) continue;
                bool critical = controlsTiming && (binding.semantic == AnimationEventMeaning.Release ||
                    binding.semantic == AnimationEventMeaning.Finish);
                if (!TryResolveSource(binding, animation, out var events, out var error))
                {
                    Report(critical, binding.eventName + "：" + error, diagnostic);
                    continue;
                }

                foreach (var item in events)
                {
                    var response = binding.Copy();
                    CaptureValues(response, item);
                    response.track = track;
                    if (binding.occurrence == -1) response.occurrence = Occurrence(animation, item);
                    if (!markers.Add((response.eventName, response.occurrence, response.semantic)))
                    {
                        Report(critical, binding.eventName + " 存在重复响应：" + response.semantic, diagnostic);
                        continue;
                    }
                    var markerTime = Effective(response, fps);
                    if (!Inside(markerTime, animation))
                    {
                        Report(critical, binding.eventName + " 响应偏移超出片段", diagnostic);
                        continue;
                    }
                    if (!string.IsNullOrEmpty(response.anchor) && (response.sound || response.effect) &&
                        (skeleton == null || (response.IsSlotAnchor ? skeleton.FindSlot(response.AnchorName) == null :
                            skeleton.FindBone(response.anchor) == null)))
                    {
                        diagnostic?.Invoke(binding.eventName + " 缺少音画挂点：" + response.anchor);
                        response.sound = null;
                        response.effect = null;
                    }
                    if (response.sound && !Inside(markerTime + (float)response.soundOffsetFrames / fps, animation))
                    {
                        diagnostic?.Invoke(binding.eventName + " 音效偏移超出片段");
                        response.sound = null;
                    }
                    if (response.effect && !Inside(markerTime + (float)response.effectOffsetFrames / fps, animation))
                    {
                        diagnostic?.Invoke(binding.eventName + " 特效偏移超出片段");
                        response.effect = null;
                    }
                    frozen.Add(response);
                }
            }

            if (controlsTiming)
            {
                var finish = frozen.Where(binding => binding.semantic == AnimationEventMeaning.Finish).ToArray();
                if (finish.Length != 1 || !frozen.Any(binding => binding.semantic == AnimationEventMeaning.Release))
                    throw new ArgumentException("事件时序需要至少一个释放响应和唯一结束响应");
                if (frozen.Any(binding => (binding.semantic == AnimationEventMeaning.Release ||
                    binding.semantic == AnimationEventMeaning.DodgeCancel) && Effective(binding, fps) > Effective(finish[0], fps)))
                    throw new ArgumentException("释放或冲刺取消事件位于结束之后");
            }
            return frozen.ToArray();
        }

        private static Spine.Event[] Named(Spine.Animation clip, string name)
            => AnimationCalibrationSO.Events(clip).Where(item => item != null && item.Data.Name == name).ToArray();

        private static void CaptureValues(AnimationCalibrationBinding binding, Spine.Event source)
        {
            binding.sourceTime = source.Time;
            binding.sourceInt = source.Int;
            binding.sourceFloat = source.Float;
            binding.sourceString = source.String;
        }

        private static bool SameValues(AnimationCalibrationBinding binding, Spine.Event source)
            => binding.sourceInt == source.Int && binding.sourceFloat.Equals(source.Float) &&
               (binding.sourceString ?? "") == (source.String ?? "");

        private static float Effective(AnimationCalibrationBinding binding, int fps)
            => binding.sourceTime + (float)binding.offsetFrames / fps;

        private static bool Inside(float time, Spine.Animation animation)
            => time >= -0.000001f && time <= animation.Duration + 0.000001f;

        private static void Report(bool critical, string error, Action<string> diagnostic)
        {
            if (critical) throw new ArgumentException(error);
            diagnostic?.Invoke(error);
        }
    }
}
