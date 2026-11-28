/*
 * 功能：在原角色手感调试台编辑芙蕾链匕首的第三段、3→4 连接和第四段击退候选。
 * 边界：A 读取当前作者来源，B 只对本次试打角色和当前连段来源生效。已受理动作继续持有冻结值。
 * 协作：调试台负责复位及真实输入，序列宿主接受候选，场景仍推进正式战斗链。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabBehaviorPanel : IDisposable
    {
        private AttackLabSession session;
        private Object source;
        private ActorRoot actor;
        private ActionDefinitionSO third, fourth;
        private AttackLabBehaviorDraft draft;
        private bool preview, appliedGate, thirdShared, fourthShared;
        private string equipmentInstance;
        private string status;
        private string thirdStamp, fourthStamp, comboStamp;
        private Vector2 scroll;

        internal bool Previewing => preview;
        internal AttackLabBehaviorDraft Draft => draft;

        internal void Observe(AttackLabSession current)
        {
            if (session && (!current || current != session || !current.Actor || current.Actor != actor ||
                current.Actor.StateArbitrator?.Combat?.ComboSource != source ||
                EquipmentInstance(current.Actor) != equipmentInstance))
                ReleaseDraft();
        }

        internal bool BindSession(AttackLabSession current)
        {
            Observe(current);
            var owner = current ? current.Actor : null;
            if (!owner || owner.Definition?.definitionId != "temporary-b.vc02") return false;

            var combat = owner.StateArbitrator?.Combat;
            var actualSource = combat?.ComboSource;
            if (!actualSource) return false;
            var main = owner.GetComponent<CharacterMainAction>();
            var moves = ResolveStages(actualSource, main);
            if (moves == null || moves.Length != 4 || moves.Any(move => !move) || moves[2] == moves[3]) return false;
            if (source == actualSource && third == moves[2] && fourth == moves[3] && draft != null)
                return true;

            ReleaseDraft();
            session = current;
            actor = owner;
            source = actualSource;
            third = moves[2];
            fourth = moves[3];
            equipmentInstance = EquipmentInstance(owner);
            thirdShared = moves.Count(move => move == third) > 1;
            fourthShared = moves.Count(move => move == fourth) > 1;
            ReadSource();
            try
            {
                if (AttackLabBehaviorPreset.Restore(actor.ActorId, source, third, fourth, draft))
                    status = "已载入本机 B 候选。";
            }
            catch (Exception error)
            {
                status = "读取候选失败，当前显示初始候选 · " + error.Message;
                ReadSource();
            }
            return true;
        }

        private static ActionDefinitionSO[] ResolveStages(Object origin, CharacterMainAction main)
        {
            if (origin is CharacterDefinitionSO character) return character.basicAttacks;
            if (!(origin is MainActionProfileSO profile) || !main || main.Sequence == null) return null;
            var stages = (profile.stages ?? Array.Empty<MainActionStageDefinition>())
                .Concat(profile.rapidPrefix ?? Array.Empty<MainActionStageDefinition>())
                .Concat(profile.rapidPlusPrefix ?? Array.Empty<MainActionStageDefinition>())
                .Append(profile.chisel);
            return Enumerable.Range(0, main.Sequence.Count).Select(index =>
            {
                string id = main.Sequence.Stage(index).Id;
                return stages.FirstOrDefault(stage => stage != null && stage.stageId == id)?.action;
            }).ToArray();
        }

        private static string EquipmentInstance(ActorRoot owner)
            => owner.GetComponent<CharacterEquipment>()?.State?.RightInstanceId;

        private BasicComboSettings SourceSettings => source is CharacterDefinitionSO character
            ? character.ComboSettings : ((MainActionProfileSO)source).ComboSettings;

        private static string Stamp(Object value) => Hash128.Compute(EditorJsonUtility.ToJson(value)).ToString();

        private static string MoveStamp(ActionDefinitionSO move) => Stamp(move) +
            (move.executor ? Stamp(move.executor) : "");

        private void ReadSource()
        {
            draft?.Dispose();
            var settings = SourceSettings;
            draft = new AttackLabBehaviorDraft(third, fourth, settings.behaviorSuccessConnections, true);
            thirdStamp = MoveStamp(third);
            fourthStamp = MoveStamp(fourth);
            comboStamp = Stamp(source);
        }

        private ActionDefinitionSO ResolveAction(ActorRoot owner, Object origin, int stage, ActionDefinitionSO original)
        {
            if (!preview || !Matches(owner, origin)) return original;
            if (stage == 3 && original == third) return draft.Third;
            if (stage == 4 && original == fourth) return draft.Fourth;
            return original;
        }

        private BasicComboSettings ResolveCombo(ActorRoot owner, Object origin, BasicComboSettings settings)
        {
            if (preview && Matches(owner, origin))
                settings.behaviorSuccessConnections = AttackLabBehaviorDraft.ResolveConnections(
                    settings.behaviorSuccessConnections, appliedGate);
            return settings;
        }

        private bool Matches(ActorRoot owner, Object origin)
            => session && draft != null && owner == actor && session.Actor == actor && source == origin &&
                EquipmentInstance(owner) == equipmentInstance;

        internal bool Apply(bool useCandidate, Func<string, bool> reset)
        {
            if (draft == null || useCandidate && Problem() != null) return false;
            if (useCandidate &&
                (CharacterCombatStateMachine.EditorBehaviorActionOverride != null &&
                    CharacterCombatStateMachine.EditorBehaviorActionOverride != ResolveAction ||
                 CharacterCombatStateMachine.EditorBehaviorComboOverride != null &&
                    CharacterCombatStateMachine.EditorBehaviorComboOverride != ResolveCombo))
            {
                status = "另一个试验持有连段候选。";
                return false;
            }
            if (!reset(useCandidate ? "B · 芙蕾链匕首连段" : "A · 当前来源四段"))
            {
                status = "试打复位失败 · " + session.Status;
                return false;
            }
            ClearOverride(false);
            if (useCandidate)
            {
                preview = true;
                appliedGate = draft.RequireSuccess;
                CharacterCombatStateMachine.EditorBehaviorActionOverride = ResolveAction;
                CharacterCombatStateMachine.EditorBehaviorComboOverride = ResolveCombo;
            }
            actor.StateArbitrator.Combat.RebindLaboratorySequence();
            status = useCandidate ? "已应用 B。到 Game 视图使用原普攻按键。" : "已恢复 A。B 参数可继续编辑。";
            return true;
        }

        private void ClearOverride(bool rebind)
        {
            bool wasActive = preview;
            preview = false;
            if (CharacterCombatStateMachine.EditorBehaviorActionOverride == ResolveAction)
                CharacterCombatStateMachine.EditorBehaviorActionOverride = null;
            if (CharacterCombatStateMachine.EditorBehaviorComboOverride == ResolveCombo)
                CharacterCombatStateMachine.EditorBehaviorComboOverride = null;
            if (!rebind || !wasActive || !actor || !Application.isPlaying) return;

            // 运行中的动作保留接受时配置。身体空闲后回读原作者连接。
            var previous = actor;
            void Rebind()
            {
                if (Application.isPlaying && previous && previous.ActionActive) return;
                EditorApplication.update -= Rebind;
                if (Application.isPlaying && previous) previous.StateArbitrator?.Combat?.RebindLaboratorySequence();
            }
            Rebind();
            if (previous && previous.ActionActive) EditorApplication.update += Rebind;
        }

        private void ReleaseDraft()
        {
            ClearOverride(true);
            draft?.Dispose();
            draft = null;
            source = null;
            actor = null;
            session = null;
        }

        public void Dispose() => ReleaseDraft();

        internal void Draw(AttackLabSession current, Func<string, bool> reset)
        {
            if (!BindSession(current))
            {
                EditorGUILayout.HelpBox("选中芙蕾和一个完整四段连段来源后，可以载入链匕首候选。", MessageType.Info);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("芙蕾链匕首连段", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("试打版本", preview ? "B · 本机连段候选" : "A · 当前正式来源");
            DrawLocate("连段原资产", source);
            DrawLocate("第三段原资产", third);
            DrawLocate("第四段原资产", fourth);
            EditorGUILayout.LabelField(preview
                ? "第三段运行锁链匕首。第四段推移在本页「第四段 B 候选效果」调整。"
                : "当前试打读取原资产。下方参数供 B 候选使用。", EditorStyles.wordWrappedMiniLabel);
            if (preview)
                EditorGUILayout.LabelField("第四段 B 推移", DescribePush(draft.Fourth));
            EditorGUILayout.LabelField("第四段原 SO 推移", DescribePush(fourth));
            current.Distance = EditorGUILayout.Slider("靶距（U）", current.Distance, .25f, 12);
            using (new EditorGUILayout.HorizontalScope())
            {
                current.FacingRight = EditorGUILayout.ToggleLeft("向右出招", current.FacingRight);
                current.EnemyAttacks = EditorGUILayout.ToggleLeft("允许靶反击", current.EnemyAttacks);
            }
            string error = Problem();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("A · 来源并复位")) Apply(false, reset);
                using (new EditorGUI.DisabledScope(error != null))
                    if (GUILayout.Button("B · 链匕首并复位")) Apply(true, reset);
            }
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            DrawDiagnostics(current);
            EditorGUILayout.Space();

            bool changed = AttackLabBehaviorAuthoring.DrawParameters(draft.Third.executor);
            using (var action = new SerializedObject(draft.Third))
            {
                EditorGUILayout.LabelField("第三段招式效果", EditorStyles.boldLabel);
                ActionEffectListDrawer.DrawForMove(action, () => changed = true);
            }

            EditorGUILayout.LabelField("第三段 → 第四段", EditorStyles.boldLabel);
            bool gate = EditorGUILayout.ToggleLeft("需要第三段回拉成功", draft.RequireSuccess);
            changed |= gate != draft.RequireSuccess;
            draft.RequireSuccess = gate;

            using (var action = new SerializedObject(draft.Fourth))
            {
                EditorGUILayout.LabelField("第四段 B 候选效果", EditorStyles.boldLabel);
                ActionEffectListDrawer.DrawForMove(action, () => changed = true);
            }
            if (changed) Persist();

            if (preview && appliedGate != draft.RequireSuccess)
                EditorGUILayout.LabelField("重新应用 B 后，连接规则生效。", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存本机 B 候选")) Persist();
                if (GUILayout.Button("重读来源") && Apply(false, reset)) ReadSource();
            }
            using (new EditorGUI.DisabledScope(error != null))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(thirdShared))
                    if (GUILayout.Button("写回第三段")) Write(third, thirdStamp, "专用行为与第三段效果", SaveThird);
                if (GUILayout.Button("写回 3→4 连接")) Write(source, comboStamp, "第三段 → 第四段的接段要求", SaveCombo);
                using (new EditorGUI.DisabledScope(fourthShared))
                    if (GUILayout.Button("写回第四段效果")) Write(fourth, fourthStamp, "第四段效果列表", SaveFourth);
            }
            if (thirdShared || fourthShared)
                EditorGUILayout.HelpBox("此动作在当前连段还有其他引用。可用 B 试打，请先为该段准备独立动作再写回。", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawDiagnostics(AttackLabSession current)
        {
            var combat = actor.StateArbitrator.Combat;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("第三段回拉", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(combat.BehaviorStatus ?? "尚无行为", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(combat.BehaviorWait ?? "尚无待接段", EditorStyles.wordWrappedMiniLabel);
            DrawFourthOutcome(current);
            var target = current.Target;
            var motor = target ? target.GetComponent<Ossastra.Combat.ActionImpactMotor>() : null;
            if (motor)
                EditorGUILayout.LabelField($"{target.name}最近运动步 · 请求 {motor.LastRequestedStep:F3} U · 实际 {motor.LastActualStep:F3} U",
                    EditorStyles.wordWrappedMiniLabel);
        }

        private static string DescribePush(ActionDefinitionSO move)
        {
            var settings = move.effects?.entries?.OfType<TargetKnockbackEffect>()
                .Where(effect => effect.enabled)
                .Select(effect => $"{effect.distance:0.##} U / {effect.duration:0.##} 秒").ToArray();
            return settings == null || settings.Length == 0 ? "未启用" : string.Join("、", settings);
        }

        private static void DrawFourthOutcome(AttackLabSession current)
        {
            var trace = current.Trace;
            if (!trace || trace.Frozen) return;
            var rows = trace.Events;
            var kick = rows.LastOrDefault(row => row.trial == current.Generation && row.stage == 4 &&
                row.track == "状态" && row.label == "Accepted");
            if (kick == null) return;

            var contacts = rows.Where(row => row.parent == kick.action && row.track == "判定");
            var outcome = contacts.LastOrDefault(row => !string.IsNullOrEmpty(row.outcome));
            string hit = outcome?.outcome ?? (contacts.Any() ? "未碰到目标" : "等待命中结算");
            var push = rows.LastOrDefault(row => row.parent == kick.action && row.track == "招式效果" &&
                row.detail.Contains(" / " + nameof(TargetKnockbackEffect) + " / "));
            string result = push?.label ?? "未触发";
            if (push?.label == "拒绝")
            {
                int separator = push.detail.LastIndexOf(" / ", StringComparison.Ordinal);
                result += " · " + push.detail.Substring(separator + 3);
            }
            EditorGUILayout.LabelField("第四段命中 · " + hit + "    目标推移 · " + result,
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawLocate(string label, Object value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, value.name);
                if (GUILayout.Button("定位原资产", GUILayout.Width(82)))
                {
                    Selection.activeObject = value;
                    EditorGUIUtility.PingObject(value);
                }
            }
        }

        private string Problem()
        {
            try
            {
                draft.Third.executor.CompileExecutor();
                return AttackLabImpactSamples.Problems(draft.Third.effects) ??
                    AttackLabImpactSamples.Problems(draft.Fourth.effects);
            }
            catch (ArgumentException error) { return error.Message; }
        }

        private void Persist()
        {
            try
            {
                AttackLabBehaviorPreset.Store(actor.ActorId, source, third, fourth, draft);
                status = "已保存本机 B 候选。";
            }
            catch (Exception error) { status = "保存候选失败 · " + error.Message; }
        }

        private void Write(Object value, string expected, string scope, Action save)
        {
            string actual = value is ActionDefinitionSO move ? MoveStamp(move) : Stamp(value);
            if (!AssetDatabase.IsMainAsset(value) || !AssetDatabase.IsOpenForEdit(value) || EditorUtility.IsDirty(value) || actual != expected)
            {
                status = "来源有待保存修改、已变化或不可写，可先保存本机候选。";
                return;
            }
            if (!EditorUtility.DisplayDialog("写回选定来源",
                AssetDatabase.GetAssetPath(value) + "\n写回范围 · " + scope + "\n引用该来源的所有装配将读到新值。", "写回所示字段", "取消"))
                return;
            try
            {
                Undo.RecordObject(value, scope);
                save();
                EditorUtility.SetDirty(value);
                AssetDatabase.SaveAssetIfDirty(value);
                status = "已写回 · " + value.name + " · " + scope;
            }
            catch (Exception error) { status = "写回失败 · " + error.Message; }
        }

        private void SaveThird()
        {
            third.executor = AttackLabBehaviorAuthoring.CopyToOwner(third, draft.Third.executor);
            AttackLabImpactSamples.CopyEffects(draft.Third, third);
            thirdStamp = MoveStamp(third);
        }

        private void SaveFourth()
        {
            if (fourth == third) throw new InvalidOperationException("两段使用同一招式。");
            AttackLabImpactSamples.CopyEffects(draft.Fourth, fourth);
            fourthStamp = MoveStamp(fourth);
        }

        private void SaveCombo()
        {
            var settings = SourceSettings;
            settings.behaviorSuccessConnections = draft.ResolveConnections(settings.behaviorSuccessConnections);
            if (source is CharacterDefinitionSO character) character.comboSettings = settings;
            if (source is MainActionProfileSO profile) profile.comboSettings = settings;
            comboStamp = Stamp(source);
        }
    }

    internal sealed class AttackLabBehaviorDraft : IDisposable
    {
        internal ActionDefinitionSO Third { get; }
        internal ActionDefinitionSO Fourth { get; }
        internal bool RequireSuccess { get; set; }

        internal AttackLabBehaviorDraft(ActionDefinitionSO third, ActionDefinitionSO fourth, int[] connections, bool sample)
        {
            Third = Object.Instantiate(third);
            Fourth = Object.Instantiate(fourth);
            Third.name = third.name;
            Fourth.name = fourth.name;
            Third.hideFlags = Fourth.hideFlags = HideFlags.HideAndDontSave;
            Third.executor = third.executor is ChainDaggerExecutorSO chain
                ? Object.Instantiate(chain) : ScriptableObject.CreateInstance<ChainDaggerExecutorSO>();
            Third.executor.hideFlags = HideFlags.HideAndDontSave;
            RequireSuccess = sample || (connections != null && connections.Contains(3));

            Third.effects ??= new ActionEffectList();
            Third.effects.entries ??= new List<ActionEffectEntry>();
            // 初始候选把前置目标位移留给牵引。普通自移动和其他词条沿用来源。
            foreach (var entry in Third.effects.entries)
                if (entry is TargetKnockbackEffect || entry is LaunchEffect { target: true })
                    entry.enabled = false;

            Fourth.effects ??= new ActionEffectList();
            Fourth.effects.entries ??= new List<ActionEffectEntry>();
            if (!Fourth.effects.entries.OfType<TargetKnockbackEffect>().Any(entry => entry.enabled))
                Fourth.effects.entries.Add(new TargetKnockbackEffect { distance = 1.15f, duration = .15f });
        }

        internal int[] ResolveConnections(int[] current) => ResolveConnections(current, RequireSuccess);

        internal static int[] ResolveConnections(int[] current, bool requireSuccess)
        {
            var other = (current ?? Array.Empty<int>()).Where(stage => stage != 3);
            return (requireSuccess ? other.Append(3) : other).OrderBy(stage => stage).Distinct().ToArray();
        }

        public void Dispose()
        {
            if (Third && Third.executor) Object.DestroyImmediate(Third.executor);
            if (Third) Object.DestroyImmediate(Third);
            if (Fourth) Object.DestroyImmediate(Fourth);
        }
    }
}
