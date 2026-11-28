/*
 * 从测试场的当前装配定位正式招式。参数由该招式的 Inspector 编辑，测试台保留复位和诊断。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using Ossastra.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabImpactPanel : IDisposable
    {
        private readonly AttackLabMovementTrace trace = new AttackLabMovementTrace();
        private int targetSlot;
        private ActorRoot actor;
        private ScriptableObject inspectedMove;
        private string inspectedMoveLabel;
        private AttackLabSawTrial sawTrial;
        private string status;
        private Vector2 scroll;
        private bool showTrace;

        public void Observe(AttackLabSession current)
        {
            if (sawTrial != null && !sawTrial.BelongsTo(current)) ReturnSaw();
            trace.Observe(current, false);
        }

        public void Dispose()
        {
            ReturnSaw();
            trace.Dispose();
        }

        internal AbilityDefinitionSO EquipSaw(AttackLabSession current)
        {
            if (sawTrial != null) throw new InvalidOperationException("请先归还上次试装的圆锯");
            sawTrial = AttackLabSawTrial.Equip(current);
            actor = current.Actor;
            inspectedMove = sawTrial.Sample;
            inspectedMoveLabel = null;
            return sawTrial.Sample;
        }

        internal void ReturnSaw()
        {
            if (sawTrial == null) return;
            if (inspectedMove == sawTrial.Sample)
            {
                inspectedMove = null;
                inspectedMoveLabel = null;
            }
            sawTrial.Dispose();
            sawTrial = null;
        }

        private static List<(string name, ScriptableObject asset)> Definitions(ActorRoot owner)
        {
            var sources = new List<(string,ScriptableObject)>();
            void Add(string label, ScriptableObject asset)
            {
                if (asset && AssetDatabase.Contains(asset)) sources.Add((label + " · " + asset.name, asset));
            }
            var equipment = owner.GetComponent<CharacterEquipment>();
            var main = owner.GetComponent<CharacterMainAction>();
            var selected = main && main.Sequence != null ? main.ComboSource : null;
            if (selected)
            {
                var stages = (selected.stages ?? Array.Empty<MainActionStageDefinition>())
                    .Concat(selected.rapidPrefix ?? Array.Empty<MainActionStageDefinition>())
                    .Concat(selected.rapidPlusPrefix ?? Array.Empty<MainActionStageDefinition>())
                    .Append(selected.chisel);
                for (int i = 0; i < main.Sequence.Count; i++)
                {
                    string id = main.Sequence.Stage(i).Id;
                    var stage = stages.FirstOrDefault(v => v != null && v.stageId == id);
                    Add("主手 " + (i + 1) + " · " + id + (stage != null && stage.dashDistance > 0 ? "（已有突进）" : ""), stage?.action);
                }
            }
            else
            {
                var basic = equipment?.RightDefinition?.actions;
                if (basic == null || basic.Length == 0) basic = owner.Definition?.basicAttacks;
                if (basic != null) for (int i = 0; i < basic.Length; i++) Add("普攻 " + (i + 1), basic[i]);
            }
            var skills = owner.GetComponent<CharacterAbilities>();
            if (skills && equipment?.State != null)
                for (int i = 0; i < 3; i++) Add("主手技能 " + "QER"[i], skills.SkillDefinition(i));
            foreach (var h in new[] { equipment?.LeftDefinition, equipment?.RightDefinition })
            {
                if (!h || h.abilityBindings == null) continue;
                foreach (var b in h.abilityBindings)
                {
                    if (b == null) continue;
                    string label = (h == equipment.LeftDefinition ? "副手 " : "主手 ") + b.operation;
                    if (!sources.Any(s => s.Item2 == b.ability)) Add(label, b.ability);
                    if (!sources.Any(s => s.Item2 == b.longPressAbility)) Add(label + " 长按", b.longPressAbility);
                }
            }
            return sources;
        }

        public void Draw(AttackLabSession current, Func<string,bool> reset)
        {
            if (actor != current.Actor)
            {
                actor = current.Actor;
                inspectedMove = null;
                inspectedMoveLabel = null;
                status = null;
            }
            if (sawTrial != null && !sawTrial.BelongsTo(current)) ReturnSaw();
            if (current.Actor.ActorId == "temporary-b")
            {
                if (sawTrial == null)
                {
                    if (GUILayout.Button("临时试装圆锯到芙蕾 Q"))
                    {
                        try { EquipSaw(current); }
                        catch (Exception bindFailure) { status = bindFailure.Message; }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("芙蕾 Q · 按住维持圆锯，松开收招");
                    if (GUILayout.Button("归还圆锯并恢复原 Q") && reset("归还圆锯")) ReturnSaw();
                }
            }
            var choices = Definitions(current.Actor);
            if (choices.Count == 0)
            {
                EditorGUILayout.HelpBox("当前装备没有可编辑的招式。", MessageType.Info);
                return;
            }

            // 同一招式可被几个槽位引用。优先保留所选槽位，重排后仍匹配原资源。
            int selected = choices.FindIndex(choice => choice.asset == inspectedMove && choice.name == inspectedMoveLabel);
            if (selected < 0) selected = choices.FindIndex(choice => choice.asset == inspectedMove);
            var names = new[] { "请选择招式" }.Concat(choices.Select(choice => choice.name)).ToArray();
            int next = EditorGUILayout.Popup("查看的招式", selected + 1, names) - 1;
            (inspectedMoveLabel, inspectedMove) = next >= 0 ? choices[next] : (null, null);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!inspectedMove))
                    if (GUILayout.Button("打开正式招式")) EditorUtility.OpenPropertyEditor(inspectedMove);
                if (GUILayout.Button("复位试打")) reset("正式招式 · " + (inspectedMove ? inspectedMove.name : "当前装备"));
            }
            EditorGUILayout.LabelField("在 Inspector 调整招式效果，再到 Game 用原按键试打。调参会影响使用此招式的所有角色和装备。",
                EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);

            showTrace = EditorGUILayout.Foldout(showTrace, "试打记录", true);
            if (showTrace)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawTrace(current);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTrace(AttackLabSession current)
        {
            var steps = trace.Live?.steps;
            if (steps != null && steps.Count != 0)
            {
                var step = steps[steps.Count - 1];
                Vector3 actual = step.WorldPosition - step.PreviousWorldPosition;
                float request = step.RequestedHorizontalSpeed * step.SimulatedSeconds;
                EditorGUILayout.LabelField("攻击者 · 最近运动步", step.Driver + "　XY " + ((Vector2)actual).ToString("F3"));
                EditorGUILayout.LabelField($"X 请求 {request:F3} U　差值 {actual.x-request:F3} U　侧阻 {step.HorizontalBlocked}　碰顶 {step.HitHead}");
                EditorGUILayout.LabelField($"方向输入 {step.InputHorizontalSpeed:F2} U/s　主动位移 {step.EffectHorizontalSpeed:F2} U/s");
            }
            if (current.Actor && current.Actor.TryGetComponent<ActionImpactMotor>(out var actorMotor))
            {
                var request = actorMotor.LastRequestedStep;
                var actual = actorMotor.LastActualStep;
                EditorGUILayout.LabelField($"攻击者最近效果步 XY（U）　请求 {request:F3}　实际 {actual:F3}　差值 {actual-request:F3}",
                    EditorStyles.wordWrappedMiniLabel);
            }
            if (current.Condition == AttackLabTargetMode.Pair)
                targetSlot = GUILayout.Toolbar(targetSlot,new[] {"首靶","次靶"});
            var target = current.Condition == AttackLabTargetMode.Pair && targetSlot == 1 ? current.PairedTarget : current.Target;
            if (target && target.gameObject.activeInHierarchy)
            {
                EditorGUILayout.LabelField("目标 " + target.name + " · 实际 XY", ((Vector2)target.transform.position).ToString("F3"));
                if (target.TryGetComponent<ActionImpactMotor>(out var motor))
                {
                    var request = motor.LastRequestedStep;
                    var actual = motor.LastActualStep;
                    EditorGUILayout.LabelField($"目标最近运动步（U）　请求 {request:F3}　实际 {actual:F3}　差值 {actual-request:F3}",
                        EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("目标推移抗性", motor.PushResistance.ToString("P0"));
                }
            }
            var events = current.Trace?.Events;
            if (events == null) return;
            var rows = events.Where(e => e.trial == current.Generation && e.track != "输入").Reverse().Take(10).Reverse();
            foreach (var e in rows)
                EditorGUILayout.LabelField($"{e.realMs:0} ms　{e.label}　{e.detail}",EditorStyles.wordWrappedMiniLabel);
        }

        internal static void WriteEffects(ScriptableObject draft, ScriptableObject selected)
        {
            Undo.RegisterCompleteObjectUndo(selected,"写回招式效果");
            AttackLabImpactSamples.CopyEffects(draft,selected);
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssetIfDirty(selected);
        }
    }
}
