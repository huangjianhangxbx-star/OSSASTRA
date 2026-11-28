/*
 * 手感台锁定实际装配的招式，编辑走正式 SO 的 Inspector。
 * 复位与游戏输入继续使用既有手感场，已受理动作保留自己的快照。
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
    internal sealed class AttackLabBehaviorPanel
    {
        private Object source;
        private ActorRoot actor;
        private ActionDefinitionSO third, fourth;
        private bool thirdShared;
        private Vector2 scroll;

        internal void Observe(AttackLabSession current)
        {
            if (!current || !actor || current.Actor != actor || actor.ActionActive) return;
            var combat = actor.StateArbitrator?.Combat;
            if (combat == null || combat.ComboSource != source) return;
            var authored = source is CharacterDefinitionSO character
                ? character.comboSettings : ((MainActionProfileSO)source).comboSettings;
            bool required = authored.IsMigrated && authored.RequiresBehaviorSuccess(3);
            if (combat.EffectiveComboSettings.GetValueOrDefault().RequiresBehaviorSuccess(3) != required)
                combat.RebindLaboratorySequence();
        }

        internal bool BindSession(AttackLabSession current)
        {
            var owner = current ? current.Actor : null;
            var actualSource = owner && owner.Definition?.definitionId == "temporary-b.vc02"
                ? owner.StateArbitrator?.Combat?.ComboSource : null;
            var moves = actualSource ? ResolveStages(actualSource, owner) : null;
            if (moves == null || moves.Length < 4 ||
                moves[2] == moves[3] || !AssetDatabase.Contains(actualSource) ||
                !AssetDatabase.Contains(moves[2]) || !AssetDatabase.Contains(moves[3]))
            {
                source = null;
                actor = null;
                third = fourth = null;
                return false;
            }

            actor = owner;
            source = actualSource;
            third = moves[2];
            fourth = moves[3];
            thirdShared = moves.Count(move => move == third) > 1;
            return true;
        }

        internal ActionDefinitionSO FourthSource => fourth;

        private static ActionDefinitionSO[] ResolveStages(Object origin, ActorRoot owner)
        {
            if (origin is CharacterDefinitionSO character)
            {
                var actions = owner.GetComponent<CharacterEquipment>()?.RightDefinition?.actions;
                return actions != null && actions.Length > 0 ? actions : character.basicAttacks;
            }

            var main = owner.GetComponent<CharacterMainAction>();
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

        private BasicComboSettings SourceSettings => source is CharacterDefinitionSO character
            ? character.ComboSettings : ((MainActionProfileSO)source).ComboSettings;

        internal void Draw(AttackLabSession current, Func<string, bool> reset)
        {
            if (!BindSession(current))
            {
                EditorGUILayout.HelpBox("请选择芙蕾，并装配含第三段和第四段的普攻。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("芙蕾链匕首连段", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("试打读取下方列出的 SO。打开后直接调整，再到 Game 里出招。", EditorStyles.wordWrappedMiniLabel);
            bool requireSuccess = SourceSettings.RequiresBehaviorSuccess(3);
            DrawSource("第三段 · 投掷与回拉", third, third.executor ? third.executor.name : "尚未配置专用行为");
            DrawSource("第四段 · 飞踢", FourthSource, "目标推移 " + DescribePush(FourthSource));
            DrawSource("第三段 → 第四段", source, requireSuccess ? "回拉成功后才能接飞踢" : "沿用普攻接段规则");

            if (!third.executor)
            {
                if (thirdShared)
                    EditorGUILayout.HelpBox("第三段和其他段共用同一个招式。配置链匕首前请为第三段指定独立招式。", MessageType.Info);
                else if (GUILayout.Button("为第三段添加链匕首") &&
                    EditorUtility.DisplayDialog("添加链匕首",
                        $"在「{third.name}」加入投掷与回拉，并在「{source.name}」开启回拉成功后接飞踢。引用这些资产的装配会使用新配置。",
                        "添加", "取消"))
                {
                    try
                    {
                        AddChainDagger();
                        EditorUtility.OpenPropertyEditor(third);
                    }
                    catch (InvalidOperationException error)
                    {
                        EditorUtility.DisplayDialog("无法添加链匕首", error.Message, "关闭");
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("复位试打", "角色和目标回到起点，重新读取连段与装配。")))
                reset("正式 SO · 芙蕾连段");
            DrawDiagnostics(current);
            EditorGUILayout.EndScrollView();
        }

        private void AddChainDagger()
        {
            if (thirdShared || third.executor || !AssetDatabase.IsMainAsset(third) ||
                !AssetDatabase.IsOpenForEdit(third) || !AssetDatabase.IsOpenForEdit(source))
                throw new InvalidOperationException("第三段或连段来源当前不可写，请在 Inspector 检查这两份资产。");

            var settings = SourceSettings;
            if (!settings.RequiresBehaviorSuccess(3))
                settings.behaviorSuccessConnections = (settings.behaviorSuccessConnections ?? Array.Empty<int>())
                    .Append(3).ToArray();

            const string undoLabel = "为第三段添加链匕首";
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoLabel);
            try
            {
                var executor = AttackLabBehaviorAuthoring.CopyToOwner(third, null);
                Undo.RegisterCompleteObjectUndo(new[] { (Object)third, source }, undoLabel);
                third.executor = executor;
                if (source is CharacterDefinitionSO character) character.comboSettings = settings;
                else ((MainActionProfileSO)source).comboSettings = settings;
                EditorUtility.SetDirty(third);
                EditorUtility.SetDirty(source);
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void DrawSource(string label, Object value, string detail)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, detail);
                if (GUILayout.Button(new GUIContent("打开 SO", AssetDatabase.GetAssetPath(value)), GUILayout.Width(76)))
                    EditorUtility.OpenPropertyEditor(value);
            }
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
