/*
 * 脚本：AnimationConfigEditors
 * 功能：保留已有 Odin 动画映射、路由和覆盖分组，并在实际片段下打开事件响应编辑区。
 * 边界：绘制作者配置，保存和撤销交给片段事件编辑器。
 */
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Ossastra.CharacterAnimation.Editor
{
    [CustomEditor(typeof(AnimationPlanMappingSO))]
    public sealed class AnimationPlanMappingEditor : OdinEditor { }

    [CustomEditor(typeof(AnimationPlanOverrideRuleSetSO))]
    public sealed class AnimationPlanOverrideEditor : OdinEditor { }

    [CustomEditor(typeof(AnimationLogicRouterSO))]
    public sealed class AnimationLogicRouterEditor : OdinEditor { }

    public sealed class CharacterAnimationClipEventDrawer : OdinValueDrawer<CharacterAnimationClip>
    {
        private readonly AnimationClipEventInspector events = new AnimationClipEventInspector();
        private bool showEvents;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            CallNextDrawer(label);

            // Odin 的多选同时改多个对象时，没有唯一事件源。
            if (Property.Tree.WeakTargets.Count != 1 ||
                !(Property.Tree.WeakTargets[0] is ScriptableObject owner) ||
                !(owner is AnimationPlanMappingSO || owner is AnimationPlanOverrideRuleSetSO))
                return;

            var clip = ValueEntry.SmartValue;
            if (clip == null)
                return;

            showEvents = EditorGUILayout.Foldout(showEvents, "事件响应", true);
            if (showEvents)
                events.Draw(owner, clip);
        }
    }

    [CustomEditor(typeof(AnimationCalibrationSO))]
    public sealed class LegacyAnimationCalibrationEditor : UnityEditor.Editor
    {
        private bool showArchive;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("旧事件校准兼容资产。已迁移的响应请在角色实际使用的动画映射片段下编辑。",
                MessageType.Info);
            showArchive = EditorGUILayout.Foldout(showArchive, "查看旧校准资料", true);
            if (showArchive)
                using (new EditorGUI.DisabledScope(true))
                    DrawDefaultInspector();
        }
    }

    /// <summary>从真实作者资产枚举普通、移动循环和排队片段，保留来源及逻辑项。</summary>
    internal static class AnimationAuthoredClips
    {
        public readonly struct Entry
        {
            public readonly ScriptableObject Source;
            public readonly EAnimationLogicState Logic;
            public readonly CharacterAnimationClip Clip;

            public Entry(ScriptableObject source, EAnimationLogicState logic, CharacterAnimationClip clip)
            {
                Source = source;
                Logic = logic;
                Clip = clip;
            }

            public string Label => $"{Source.name} / {Logic} / {Clip.animaName} · 轨 {Clip.track}";
        }

        public static IEnumerable<Entry> ForCharacter(CharacterAnimationConfigManager config, CharacterEquipment equipment)
        {
            if (config == null)
                return Enumerable.Empty<Entry>();

            var sources = new List<ScriptableObject>
            {
                config.baseMap, config.sideViewTraversalMap, config.statusOverrides,
                equipment?.RightDefinition?.bodyAnimationOverride, equipment?.LeftDefinition?.bodyAnimationOverride
            };
            if (config.itemOverrides != null)
                sources.AddRange(config.itemOverrides);
            if (config.modelOverrides != null)
                sources.AddRange(config.modelOverrides);
            return sources.Where(source => source != null).Distinct().SelectMany(FromMap);
        }

        public static IEnumerable<Entry> FromMap(ScriptableObject source)
        {
            if (source is AnimationPlanMappingSO mapping)
            {
                var groups = new[] { mapping.plansMelee, mapping.plansMovement, mapping.plansFirearm, mapping.plansJump,
                    mapping.plansTransition, mapping.plansSkill, mapping.plansOther, mapping.defaultPlans };
                foreach (var entry in groups.Where(group => group != null).SelectMany(group => group))
                    if (entry?.plan != null)
                        foreach (var clip in entry.plan.EnumerateClips())
                            yield return new Entry(source, entry.logicID, clip);
            }
            else if (source is AnimationPlanOverrideRuleSetSO overrides)
            {
                var groups = new[] { overrides.rulesMelee, overrides.rulesMovement, overrides.rulesFirearm, overrides.rulesJump,
                    overrides.rulesTransition, overrides.rulesSkill, overrides.rulesOther, overrides.rules };
                foreach (var rule in groups.Where(group => group != null).SelectMany(group => group))
                    if (rule?.replacementPlan != null)
                        foreach (var clip in rule.replacementPlan.EnumerateClips())
                            yield return new Entry(source, rule.targetID, clip);
            }
        }
    }
}
