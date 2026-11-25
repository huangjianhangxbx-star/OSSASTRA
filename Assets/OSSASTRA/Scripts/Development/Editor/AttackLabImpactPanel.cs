/*
 * 手感调试台的招式页。选中实际装备来源后在内存副本编辑，复位后仅为该本体的下一次招式提供候选。
 * 保存只替换来源的效果字段；候选、复位、选择样板均与共享来源写回分开。
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
        private sealed class Draft
        {
            public ScriptableObject copy;
            public string sourceStamp;
        }
        private readonly Dictionary<(ActorRoot,ScriptableObject),Draft> drafts = new Dictionary<(ActorRoot,ScriptableObject),Draft>();
        private readonly AttackLabMovementTrace trace = new AttackLabMovementTrace();
        private int selection, sample, generation, targetSlot;
        private ActorRoot actor;
        private ScriptableObject source, applied;
        private AttackLabSession session;
        private AttackLabSawTrial sawTrial;
        private string status;
        private Vector2 scroll;

        private ActionEffectList Resolve(ActorRoot receiver, ScriptableObject definition)
            => applied && session && generation == session.Generation && receiver == actor && session.Actor == actor && definition == source
                ? AttackLabImpactSamples.Read(applied) : null;

        internal void ClearOverride()
        {
            if (ActionEffectList.EditorOverride == Resolve) ActionEffectList.EditorOverride = null;
            if (applied) Object.DestroyImmediate(applied);
            applied = null;
        }

        public void Observe(AttackLabSession current)
        {
            if (sawTrial != null && !sawTrial.BelongsTo(current)) ReturnSaw();
            if (applied && (!current || generation != current.Generation || current.Actor != actor)) ClearOverride();
            trace.Observe(current, applied);
        }

        public void Dispose()
        {
            ReturnSaw();
            ClearOverride();
            trace.Dispose();
            foreach (var item in drafts.Values) if (item.copy) Object.DestroyImmediate(item.copy);
            drafts.Clear();
        }

        internal AbilityDefinitionSO EquipSaw(AttackLabSession current)
        {
            if (sawTrial != null) throw new InvalidOperationException("请先归还上次试装的圆锯");
            sawTrial = AttackLabSawTrial.Equip(current);
            ClearOverride();
            actor = current.Actor;
            source = null;
            selection = Mathf.Max(0, Definitions(actor).FindIndex(c => c.asset == sawTrial.Sample));
            sample = 4;
            return sawTrial.Sample;
        }

        internal void ReturnSaw()
        {
            if (sawTrial == null) return;
            if (source == sawTrial.Sample) ClearOverride();
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
            var selected = main && main.Sequence != null ? main.Choices.FirstOrDefault(p => p && p.definitionId == main.SelectedId) : null;
            if (selected)
            {
                var stages = selected.stages.Concat(selected.rapidPrefix).Concat(selected.rapidPlusPrefix).Append(selected.chisel);
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
            session = current;
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
            if (choices.Count == 0) { EditorGUILayout.HelpBox("当前角色尚未装配可编辑的动作。", MessageType.Info); return; }
            if (actor != current.Actor) selection = 0;
            selection = EditorGUILayout.Popup("招式与段位", Mathf.Clamp(selection,0,choices.Count-1), choices.Select(c => c.name).ToArray());
            var selected = choices[selection].asset;
            if (selected != source || actor != current.Actor)
            {
                ClearOverride();
                actor = current.Actor;
                source = selected;
                status = null;
            }
            if (!drafts.TryGetValue((actor,source), out var draft))
            {
                draft = new Draft();
                Load(draft);
                drafts.Add((actor, source),draft);
            }

            EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(source),EditorStyles.miniLabel,GUILayout.Height(18));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (source is AbilityDefinitionSO ability)
            {
                string channel = string.IsNullOrEmpty(ability.damageChannel) ? ability.effectRoot : ability.damageChannel;
                EditorGUILayout.LabelField("定义投递通道", string.IsNullOrEmpty(channel) ? "未指定" : channel);
                var nodes = ability.releases ?? Array.Empty<AbilityReleaseNode>();
                EditorGUILayout.LabelField("定义释放序号", nodes.Length == 0 ? "由执行器决定" :
                    string.Join("　",nodes.Select((node,index) => node == null ? index + "（缺失）" : $"{index} @ {node.at:0.###} s")),
                    EditorStyles.wordWrappedMiniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                sample = EditorGUILayout.Popup("试验样板", sample, AttackLabImpactSamples.Labels);
                if (GUILayout.Button("载入候选", GUILayout.Width(76)))
                {
                    Undo.RegisterCompleteObjectUndo(draft.copy, "载入招式效果样板");
                    AttackLabImpactSamples.Configure(draft.copy, sample);
                    EditorUtility.SetDirty(draft.copy);
                    status = "样板已载入待试副本。";
                }
            }
            using (var so = new SerializedObject(draft.copy))
            {
                var field = so.FindProperty("effects");
                EditorGUILayout.PropertyField(field,true);
                so.ApplyModifiedProperties();
            }
            var error = AttackLabImpactSamples.Problems(AttackLabImpactSamples.Read(draft.copy));
            if (error != null) EditorGUILayout.HelpBox(error,MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("A · 来源并复位") && reset("招式效果 A · " + source.name))
                { ClearOverride(); status = "已恢复来源 A，候选仍可编辑。"; }
                using (new EditorGUI.DisabledScope(error != null))
                    if (GUILayout.Button("B · 应用候选并复位"))
                    {
                        if (ActionEffectList.EditorOverride != null && ActionEffectList.EditorOverride != Resolve)
                            status = "另一试验正持有效果覆盖。";
                        else if (reset("招式效果 B · " + source.name))
                        {
                            ClearOverride();
                            applied = Object.Instantiate(draft.copy);
                            applied.hideFlags = HideFlags.HideAndDontSave;
                            generation = current.Generation;
                            ActionEffectList.EditorOverride = Resolve;
                            status = "已应用 B。再次编辑后可重新应用。";
                        }
                    }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重读来源") && EditorUtility.DisplayDialog("重读来源","替换当前招式的待试副本？","重读","取消"))
                { ClearOverride(); Load(draft); status = "待试副本已从所选来源重读。"; }
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("另存独立样板")) SaveCopy(draft);
                    if (GUILayout.Button("写回所选招式")) Save(draft);
                }
            }
            EditorGUILayout.LabelField("当前试验", current.Version);
            DrawTrace(current);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status,MessageType.Info);
            EditorGUILayout.EndScrollView();
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
                EditorGUILayout.LabelField($"输入 {step.InputHorizontalSpeed:F2} U/s　效果 {step.EffectHorizontalSpeed:F2} U/s");
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

        private void Load(Draft draft)
        {
            if (draft.copy) Object.DestroyImmediate(draft.copy);
            draft.copy = Object.Instantiate(source);
            draft.copy.hideFlags = HideFlags.HideAndDontSave;
            draft.sourceStamp = EditorJsonUtility.ToJson(source);
        }

        private void SaveCopy(Draft draft)
        {
            string path = EditorUtility.SaveFilePanelInProject("另存独立招式",source.name + "_Impact", "asset","保留原装配，新增可选用的独立招式。");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path)) { status = "请选择尚未使用的样板路径。"; return; }
            var copy = Object.Instantiate(draft.copy);
            copy.hideFlags = HideFlags.None;
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy,path);
            AssetDatabase.SaveAssetIfDirty(copy);
            status = "独立样板已保存：" + path;
        }

        private void Save(Draft draft)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (!AssetDatabase.IsOpenForEdit(source) || EditorUtility.IsDirty(source) || EditorJsonUtility.ToJson(source) != draft.sourceStamp)
            { status = "来源有待保存修改、已变化或不可写。可另存候选，或重读来源。"; return; }
            if (!EditorUtility.DisplayDialog("写回共享招式", path + "\n所有引用此 SO 的角色、武器或段位都会使用这些效果。", "写回效果字段","取消")) return;
            WriteEffects(draft.copy, source);
            draft.sourceStamp = EditorJsonUtility.ToJson(source);
            status = "已写回 " + path;
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
