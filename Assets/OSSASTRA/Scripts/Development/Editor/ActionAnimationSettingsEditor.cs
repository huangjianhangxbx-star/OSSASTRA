/*
 * 功能：在动作定义中定位对应的实际 Mapping 片段，并按该片段的状态展示时序。
 * 边界：Mapping 负责已迁移事件。仅兼容时序在这里保留秒数和架势恢复选择。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Ossastra.CharacterAnimation.Editor;
using UnityEditor;
using UnityEngine;
using MappingConfig = Ossastra.CharacterAnimation.CharacterAnimationConfigManager;
using LegacyCalibration = Ossastra.CharacterAnimation.AnimationCalibrationSO;

namespace Ossastra.Development.Editor
{
    [CustomEditor(typeof(ActionDefinitionSO))]
    internal sealed class ActionAnimationSettingsEditor : UnityEditor.Editor
    {
        private int selection;

        public override void OnInspectorGUI()
        {
            var action = (ActionDefinitionSO)target;
            serializedObject.Update();
            var contexts = FindContexts(action).ToArray();
            selection = Mathf.Clamp(selection, 0, Math.Max(0, contexts.Length - 1));
            if (contexts.Length > 1)
                selection = EditorGUILayout.Popup("查看动画配置", selection,
                    contexts.Select(c => c.Source.name + " · " + c.Clip.animaName).ToArray());

            bool events = false;
            if (contexts.Length > 0)
            {
                var context = contexts[selection];
                try
                {
                    events = context.Clip.eventResponses.TrySelect(action, out _, out bool drives, out _) && drives;
                    EditorGUILayout.LabelField("动作时序", events ? "Mapping 事件驱动" : "兼容时序");
                }
                catch (ArgumentException error)
                {
                    EditorGUILayout.HelpBox(error.Message, MessageType.Error);
                }
                if (GUILayout.Button("打开动画事件配置 · " + context.Clip.animaName))
                {
                    Selection.activeObject = context.Source;
                    EditorGUIUtility.PingObject(context.Source);
                }
                if (!events) DrawLegacyRecovery(context.Clip);
            }
            else EditorGUILayout.HelpBox("在使用这项动作的场景中可查看对应的动画配置。", MessageType.Info);

            DrawPropertiesExcluding(serializedObject, events
                ? new[] { "postureRecoveryEvent", "eventTimeline", "releaseAt", "moveReadyAt", "attackReadyAt", "finishAt", "timingEvidence" }
                : new[] { "postureRecoveryEvent" });
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLegacyRecovery(Ossastra.CharacterAnimation.CharacterAnimationClip clip)
        {
            var marker = serializedObject.FindProperty("postureRecoveryEvent");
            var animation = clip.skeletonDataAsset?.GetSkeletonData(true)?.FindAnimation(clip.animaName);
            var options = new List<string> { "" };
            options.AddRange(LegacyCalibration.Events(animation).Select(e => e.Data.Name).Distinct());
            if (!options.Contains(marker.stringValue)) options.Add(marker.stringValue);
            var labels = options.Select((v, i) => i == 0 ? "随动作终结" : v).ToArray();
            int current = Mathf.Max(0, options.IndexOf(marker.stringValue));
            int next = EditorGUILayout.Popup("架势恢复（兼容来源）", current, labels);
            if (next != current) marker.stringValue = options[next];
        }

        private static IEnumerable<AnimationAuthoredClips.Entry> FindContexts(ActionDefinitionSO action)
        {
            var configs = UnityEngine.Object.FindObjectsOfType<MappingConfig>(true);
            return configs.SelectMany(config => AnimationAuthoredClips.ForCharacter(config,
                    config.GetComponentInParent<CharacterEquipment>()))
                .Where(entry => entry.Logic == action.animationLogicId && entry.Clip.track == 0)
                .Distinct();
        }
    }
}
