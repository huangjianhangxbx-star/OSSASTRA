// 在手感调试台编辑正式接触配方，比较试验开始时的版本并显示真实反馈回执。
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Audio;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEditor;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabFeedbackPanel : IDisposable
    {
        private static readonly string[] CombatResults = { "实际命中", "格挡", "闪避", "环境接触" };
        private static readonly string[] MotionFacts = { "起跳", "落地", "抓边", "墙跳", "翻越完成", "主动放手" };

        private sealed class ActionChoice
        {
            public string Label, Selector;
            public int Stage;
            public ActionDefinitionSO Definition;
        }

        private ActorRoot actor;
        private SideViewFeedbackProfileSO source, draft, trialBaseline;
        private string sourceVersion, draftVersion, message;
        private Vector2 scroll;
        private int roleIndex, actionIndex, combatResult, motionFact;
        private string material = "", channel = "", delivery = "";
        private bool showConditions, comparing;
        private readonly HashSet<UnityEngine.Object> written = new HashSet<UnityEngine.Object>();
        private readonly Dictionary<ActionDefinitionSO, Ossastra.CharacterCore.AttackHitStop> baselineStops =
            new Dictionary<ActionDefinitionSO, Ossastra.CharacterCore.AttackHitStop>();
        private CombatVitals previewVitals;
        private Ossastra.CharacterCore.AttackHitStop? previousPreviewStop;
        private Ossastra.CharacterCore.AttackHitStop? currentPreviewStop;

        public AttackLabFeedbackPanel() => Undo.undoRedoPerformed += SaveUndo;

        public void Observe(AttackLabSession session)
        {
            if (comparing && (session == null || session.Actor != actor)) ReleaseComparison();
        }

        public void Draw(AttackLabSession session, bool movement, Action requestAttack)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { DrawContents(session, movement, requestAttack); }
            finally { EditorGUILayout.EndScrollView(); }

            var gui = Event.current;
            if (draft != null && gui != null && gui.type != EventType.Layout && gui.type != EventType.Repaint &&
                EditorJsonUtility.ToJson(draft) != draftVersion)
                Save();
        }

        private void DrawContents(AttackLabSession session, bool movement, Action requestAttack)
        {
            var roles = session?.Party?.Characters?.Where(member => member != null && member.Definition != null).ToArray() ??
                Object.FindObjectsOfType<ActorRoot>().Where(member => member.Definition != null &&
                    member.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true) != null).OrderBy(member => member.name).ToArray();
            if (roles.Length == 0)
            {
                EditorGUILayout.HelpBox("打开带正式角色的横版场景，或进入独立测试场。", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("进入独立测试场并试玩")) SideViewAttackLabWindow.Launch();
                return;
            }
            int position = actor == null ? Array.IndexOf(roles, session?.Actor) : Array.IndexOf(roles, actor);
            if (position < 0) position = Mathf.Clamp(roleIndex, 0, roles.Length - 1);
            roleIndex = EditorGUILayout.Popup("编辑角色", position, roles.Select(DisplayName).ToArray());
            if (roles[roleIndex] != actor)
            {
                ReleaseComparison();
                DestroySnapshot();
                actor = roles[roleIndex];
                LoadSource();
                actionIndex = 0;
            }
            if (!DraftChanged() && SourceChanged()) LoadSource();
            var references = source == null ? Array.Empty<ActorRoot>() : roles.Where(role =>
                role.Definition != actor.Definition && role.Definition.sideViewFeedback == source).ToArray();
            EditorGUILayout.LabelField("作用范围", references.Length == 0 ? DisplayName(actor) + " 的角色定义" :
                "场内共用 · " + string.Join("、", new[] { DisplayName(actor) }.Concat(references.Select(DisplayName))),
                EditorStyles.wordWrappedMiniLabel);
            if (references.Length > 0 && GUILayout.Button("为当前角色另存独立配方") && BindNewProfile())
                LoadSource();
            EditorGUILayout.LabelField("配置来源", source == null ? "共用声音与表现" : AssetDatabase.GetAssetPath(source),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("保存与生效", "自动保存 · " + (movement ? "下一次接触生效" : "下一次新动作生效"));
            if (message != null)
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (GUILayout.Button("重新读取正式配置")) LoadSource();
            }

            var actions = movement ? Array.Empty<ActionChoice>() : Choices(actor, session).ToArray();
            ActionChoice action = null;
            if (actions.Length > 0)
            {
                actionIndex = Mathf.Clamp(actionIndex, 0, actions.Length - 1);
                actionIndex = EditorGUILayout.Popup("招式与阶段", actionIndex, actions.Select(choice => choice.Label).ToArray());
                action = actions[actionIndex];
            }

            if (movement)
                motionFact = EditorGUILayout.Popup("运动事实", motionFact, MotionFacts);
            else
                combatResult = EditorGUILayout.Popup("接触结果", combatResult, CombatResults);
            material = EditorGUILayout.DelayedTextField("材质条件（留空通用）", material).Trim().ToLowerInvariant();
            if (!movement)
            {
                showConditions = EditorGUILayout.Foldout(showConditions, "投递条件", true);
                if (showConditions)
                {
                    channel = EditorGUILayout.DelayedTextField("通道（留空通用）", channel).Trim();
                    delivery = EditorGUILayout.DelayedTextField("投递形式（留空通用）", delivery).Trim();
                }
            }

            if (movement || action != null)
                DrawRecipe(movement, action);
            if (!movement && action != null)
                DrawHitStop(action);
            DrawTrial(session, movement, requestAttack);
        }

        private static string DisplayName(ActorRoot root) => string.IsNullOrEmpty(root.Definition.displayName)
            ? root.name : root.Definition.displayName;

        private static IEnumerable<ActionChoice> Choices(ActorRoot actor, AttackLabSession session)
        {
            var basic = actor.Definition.basicAttacks ?? Array.Empty<ActionDefinitionSO>();
            for (int stage = 0; stage < basic.Length; stage++)
                if (basic[stage] != null)
                    yield return Choice(basic[stage], "body.basic", stage + 1, "默认普攻");
            var main = actor.GetComponent<CharacterMainAction>();
            foreach (var group in main?.Choices ?? Array.Empty<MainActionProfileSO>())
            {
                if (group == null || group.stages == null) continue;
                for (int stage = 0; stage < group.stages.Length; stage++)
                {
                    var definition = group.stages[stage]?.action;
                    if (definition != null)
                        yield return Choice(definition, "main:" + group.definitionId, stage + 1, group.displayName);
                }
            }
            var abilities = actor.GetComponent<CharacterAbilities>();
            if (abilities == null) yield break;
            var skills = session?.Actor == actor
                ? Enumerable.Range(0, 3).Select(abilities.SkillDefinition).Where(skill => skill != null)
                : (abilities.SkillChoices ?? Array.Empty<AbilityDefinitionSO>()).Where(skill => skill != null);
            foreach (var skill in skills.Distinct())
                if (skill.action != null)
                    yield return Choice(skill.action, skill.definitionId, 0, skill.displayName);
        }

        private static ActionChoice Choice(ActionDefinitionSO definition, string selector, int stage, string label) =>
            new ActionChoice { Definition = definition, Selector = selector, Stage = stage,
                Label = stage == 0 ? label : label + " · 第 " + stage + " 段" };

        private void DrawRecipe(bool movement, ActionChoice action)
        {
            var kind = (SideViewMotionKind)motionFact;
            var result = (SideViewContactResult)combatResult;
            var moveRows = draft.movement ?? Array.Empty<SideViewMotionFeedbackBinding>();
            var combatRows = draft.combat ?? Array.Empty<SideViewCombatFeedbackBinding>();
            int index = movement
                ? Array.FindIndex(moveRows, row => row != null && row.kind == kind && Same(row.material, material))
                : Array.FindIndex(combatRows, row => row != null && row.selector == action.Selector && row.stage == action.Stage &&
                    row.result == result && Same(row.material, material) && Same(row.channel, channel) && Same(row.deliveryMode, delivery));

            var resolved = movement ? draft.ResolveMovement(kind, material)?.settings :
                draft.ResolveCombat(action.Selector, action.Stage, result, material, channel, delivery)?.settings;
            EditorGUILayout.LabelField("按这些条件解析", index >= 0 ? "此条件的角色专用配方" : resolved != null
                ? "同一角色的匹配配方" : "共用配置");
            if (!movement && resolved?.overrideSound != true)
                DrawCommonSound(action, result);
            if (index < 0)
            {
                if (GUILayout.Button("创建此条件的角色配方"))
                {
                    if (movement)
                        draft.movement = moveRows.Append(new SideViewMotionFeedbackBinding { kind = kind, material = material }).ToArray();
                    else
                        draft.combat = combatRows.Append(new SideViewCombatFeedbackBinding { selector = action.Selector,
                            stage = action.Stage, result = result, material = material, channel = channel, deliveryMode = delivery }).ToArray();
                }
                return;
            }

            var serialized = new SerializedObject(draft);
            var array = serialized.FindProperty(movement ? "movement" : "combat");
            DrawSettings(array.GetArrayElementAtIndex(index).FindPropertyRelative("settings"), movement, kind);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (GUILayout.Button("移除该覆盖并恢复匹配的共用配置"))
            {
                if (movement) draft.movement = moveRows.Where((_, row) => row != index).ToArray();
                else draft.combat = combatRows.Where((_, row) => row != index).ToArray();
            }
        }

        private void DrawCommonSound(ActionChoice action, SideViewContactResult result)
        {
            var root = Object.FindObjectOfType<Ossastra.Audio.GameAudioRoot>();
            var profile = root?.CombatSoundProfile;
            string cue = "";
            bool found = result == SideViewContactResult.Hit && profile != null &&
                profile.TryResolve(action.Selector, "hit", action.Stage, channel, out cue, delivery,
                    actor.HasPlayerInput ? "player" : "companion", material);
            EditorGUILayout.LabelField("共用命中声", found ? profile.name + " · " + cue : "由实战回执显示最终选择",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawSettings(SerializedProperty settings, bool movement, SideViewMotionKind kind)
        {
            SerializedProperty Field(string key) => settings.FindPropertyRelative(key);
            void Property(string key, string label) => EditorGUILayout.PropertyField(Field(key), new GUIContent(label));
            void Number(string key, string label) => Field(key).floatValue = EditorGUILayout.DelayedFloatField(label, Field(key).floatValue);

            EditorGUILayout.LabelField("声音", EditorStyles.boldLabel);
            Property("overrideSound", "覆盖共用声音");
            if (Field("overrideSound").boolValue)
            {
                Property("sound", "声音定义（空为静音）");
                Number("volume", "音量倍率");
                Number("pitchMin", "最低音高");
                Number("pitchMax", "最高音高");
                Number("soundDelayMs", "事实成立后延迟（ms）");
                Number("soundStartMs", "素材起播点（ms）");
            }
            EditorGUILayout.LabelField("接触特效", EditorStyles.boldLabel);
            Property("overrideEffect", "覆盖共用特效");
            if (Field("overrideEffect").boolValue)
            {
                Property("effect", "特效预制体（空为隐藏）");
                Number("effectDelayMs", "事实成立后延迟（ms）");
                Number("effectScale", "缩放");
                Number("effectLifetime", "寿命（秒）");
                Property("localOffset", "接触点偏移");
                Property("direction", "方向依据");
                Property("followTarget", movement ? "跟随角色" : "跟随受击者");
            }
            if (!movement)
            {
                EditorGUILayout.LabelField("震屏", EditorStyles.boldLabel);
                Property("overrideShake", "覆盖共用震屏强度");
                if (Field("overrideShake").boolValue) Number("shake", "强度（0–1）");
            }
            else if (kind == SideViewMotionKind.Land)
            {
                Number("strongLandingSpeed", "重落地速度阈值");
                Number("strongLandingMultiplier", "重落地音画倍率");
            }
        }

        private void DrawHitStop(ActionChoice action)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("卡肉 · " + action.Definition.name, EditorStyles.boldLabel);
            var initial = action.Definition.hitStop;
            if (!baselineStops.ContainsKey(action.Definition)) baselineStops.Add(action.Definition, initial);
            var change = initial;
            change.configured = EditorGUILayout.Toggle("逐招式配置", initial.configured);
            if (change.configured)
            {
                change.attackerSeconds = .001f * EditorGUILayout.Slider("攻击者（ms）", initial.attackerSeconds * 1000, 0, 120);
                change.receiverSeconds = .001f * EditorGUILayout.Slider("受击者（ms）", initial.receiverSeconds * 1000, 0, 120);
                change.onGuard = EditorGUILayout.Toggle("格挡时也暂停", initial.onGuard);
            }
            if (!change.Equals(initial))
            {
                Undo.RecordObject(action.Definition, "调整逐招式卡肉");
                action.Definition.hitStop = change;
                EditorUtility.SetDirty(action.Definition);
                AssetDatabase.SaveAssetIfDirty(action.Definition);
                written.Add(action.Definition);
            }
            if (actor.TryGetComponent<CombatVitals>(out var vitals))
                EditorGUILayout.LabelField("攻击者实际暂停", vitals.LastHitStop, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawTrial(AttackLabSession session, bool movement, Action requestAttack)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("实战对照", EditorStyles.boldLabel);
            if (session == null)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("进入独立测试场并试玩")) SideViewAttackLabWindow.Launch();
                return;
            }
            if (session.Actor != actor)
            {
                EditorGUILayout.LabelField("实控角色", session.Actor == null ? "未连接" : DisplayName(session.Actor));
                int slot = Array.IndexOf(session.Party.Characters.ToArray(), actor);
                if (slot >= 0 && GUILayout.Button("切到当前编辑角色")) session.SwitchActor(slot);
                return;
            }
            if (trialBaseline == null)
            {
                trialBaseline = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<SideViewFeedbackProfileSO>();
                foreach (var choice in Choices(actor, session))
                    if (!baselineStops.ContainsKey(choice.Definition)) baselineStops.Add(choice.Definition, choice.Definition.hitStop);
            }
            EditorGUILayout.LabelField("正在试玩", comparing ? "反馈对照快照" : "当前正式配置");
            if (!movement)
                session.Condition = (AttackLabTargetMode)EditorGUILayout.Popup("目标", (int)session.Condition,
                    new[] { "空挥", "命中", "正常受击", "格挡", "致死", "双目标" });

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("对照快照并复位") && session.ResetTrial("反馈对照快照"))
                {
                    comparing = SideViewFeedbackRuntime.SetPreview(actor, trialBaseline, this);
                    var choice = Choices(actor, session).ElementAtOrDefault(actionIndex);
                    if (comparing && !movement && choice != null && baselineStops.TryGetValue(choice.Definition, out var stop) &&
                        actor.TryGetComponent<CombatVitals>(out var receiver))
                    {
                        if (previewVitals != receiver) previousPreviewStop = receiver.LaboratoryHitStop;
                        previewVitals = receiver;
                        currentPreviewStop = stop;
                        receiver.LaboratoryHitStop = stop;
                    }
                }
                if (GUILayout.Button("当前配置并复位"))
                {
                    ReleaseComparison();
                    session.ResetTrial("当前正式反馈");
                }
            }
            if (!movement)
            {
                EditorGUILayout.LabelField("按当前装备出招；用正式输入接后续段。", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("请求一次普攻")) requestAttack();
            }
            if (!movement && session.Target?.Vitals != null)
                EditorGUILayout.LabelField("目标实际暂停", session.Target.Vitals.LastHitStop, EditorStyles.wordWrappedMiniLabel);
            if (session.Trace == null) return;
            var track = movement ? "运动反馈" : "反馈";
            foreach (var entry in session.Trace.Events.Where(item => item.trial == session.Generation && item.track == track)
                         .Reverse().Take(8))
            {
                EditorGUILayout.LabelField($"{entry.realMs:0} ms · {entry.label}", entry.detail,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static bool Same(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

        private bool DraftChanged() => draft != null && EditorJsonUtility.ToJson(draft) != draftVersion;
        private bool SourceChanged() => actor.Definition.sideViewFeedback != source ||
            (source != null && EditorJsonUtility.ToJson(source) != sourceVersion);

        private void LoadSource()
        {
            if (draft != null) Object.DestroyImmediate(draft);
            source = actor.Definition.sideViewFeedback;
            draft = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<SideViewFeedbackProfileSO>();
            sourceVersion = source != null ? EditorJsonUtility.ToJson(source) : null;
            draftVersion = EditorJsonUtility.ToJson(draft);
            message = null;
        }

        private void Save()
        {
            if (SourceChanged())
            {
                message = "正式配方已在别处修改。当前更改尚未保存，可重新读取后再编辑。";
                return;
            }
            var settings = (draft.combat ?? Array.Empty<SideViewCombatFeedbackBinding>()).Select(row => row?.settings)
                .Concat((draft.movement ?? Array.Empty<SideViewMotionFeedbackBinding>()).Select(row => row?.settings));
            if (settings.Any(value => value == null || !Valid(value)))
            {
                message = "尚未保存。请核对延迟、起播点、音高、寿命和缩放的有效范围。";
                return;
            }

            if (source == null)
            {
                if (!BindNewProfile()) return;
            }
            else
            {
                Undo.RecordObject(source, "调整角色反馈配方");
                draft.name = source.name;
                EditorUtility.CopySerialized(draft, source);
            }
            EditorUtility.SetDirty(source);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssetIfDirty(source);
            written.Add(source);
            sourceVersion = EditorJsonUtility.ToJson(source);
            draftVersion = EditorJsonUtility.ToJson(draft);
            message = null;
        }

        private bool BindNewProfile()
        {
            string definitionPath = AssetDatabase.GetAssetPath(actor.Definition);
            if (!definitionPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                message = "当前角色没有可写的正式定义资产。";
                return false;
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.ChangeExtension(definitionPath, null) + "_ContactFeedback.asset");
            source = Object.Instantiate(draft);
            AssetDatabase.CreateAsset(source, path);
            Undo.RegisterCreatedObjectUndo(source, "创建角色反馈配方");
            Undo.RecordObject(actor.Definition, "绑定角色反馈配方");
            actor.Definition.sideViewFeedback = source;
            EditorUtility.SetDirty(actor.Definition);
            AssetDatabase.SaveAssetIfDirty(actor.Definition);
            AssetDatabase.SaveAssetIfDirty(source);
            written.Add(actor.Definition);
            written.Add(source);
            return true;
        }

        private static bool Valid(SideViewFeedbackSettings settings)
        {
            bool Between(float n, float min, float max) => n >= min && n <= max;
            bool SoundSourceValid() => !settings.overrideSound || settings.sound == null ||
                !settings.sound.loop && settings.sound.TryValidate(out _) &&
                settings.sound.clips.All(clip => settings.soundStartMs < clip.samples * 1000.0 / clip.frequency);
            return SoundSourceValid() && Between(settings.volume, 0, 2) && Between(settings.pitchMin, .1f, 3) &&
                Between(settings.pitchMax, settings.pitchMin, 3) &&
                Between(settings.soundDelayMs, 0, 2000) && Between(settings.soundStartMs, 0, 60000) &&
                Between(settings.effectDelayMs, 0, 2000) && Between(settings.effectScale, .001f, 20) &&
                Between(settings.effectLifetime, .001f, 10) && Between(settings.shake, 0, 1) &&
                Between(settings.strongLandingSpeed, 0, 100) && Between(settings.strongLandingMultiplier, 0, 4);
        }

        private void SaveUndo()
        {
            foreach (var asset in written)
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            if (actor != null && draft != null && SourceChanged()) LoadSource();
        }

        private void ReleaseComparison()
        {
            if (actor != null) SideViewFeedbackRuntime.ClearPreview(actor, this);
            if (previewVitals != null && Nullable.Equals(previewVitals.LaboratoryHitStop, currentPreviewStop))
                previewVitals.LaboratoryHitStop = previousPreviewStop;
            comparing = false;
            previewVitals = null;
            previousPreviewStop = null;
            currentPreviewStop = null;
        }

        private void DestroySnapshot()
        {
            if (trialBaseline != null) Object.DestroyImmediate(trialBaseline);
            trialBaseline = null;
            baselineStops.Clear();
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= SaveUndo;
            ReleaseComparison();
            DestroySnapshot();
            if (draft != null) Object.DestroyImmediate(draft);
            draft = null;
        }
    }
}
