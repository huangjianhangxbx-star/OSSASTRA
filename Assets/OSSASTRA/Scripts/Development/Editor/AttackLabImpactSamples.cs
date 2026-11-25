/*
 * 招式效果试验配方。把 SAMPLE 参数填入指定动作或技能副本；独立样板资产复用原招式动画及执行器。
 * 两种 SO 都通过同一效果列表编写，正式装备引用由作者单独决定。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ossastra.CharacterAnimation;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Actor = Ossastra.Characters.CharacterRoot;
using SawLogic = Ossastra.CharacterAnimation.EAnimationLogicState;

namespace Ossastra.Development.Editor
{
    public static class AttackLabImpactSamples
    {
        public static readonly string[] Labels = { "轻型普攻", "连段末击", "踢击（占位动画）", "后撤斩", "圆锯持续", "挑飞", "自身后跳" };
        private const string Folder = "Assets/OSSASTRA/Configs/ActionImpactSamples";
        public const string SawPath = Folder + "/SawSustain.asset";
        private static readonly string[] Files = { "Light", "Finisher", "Kick", "Retreat", "SawSustain", "Launcher", "BackLeap" };
        private static readonly string[] Sources = {
            "Configs/Actions/Rozeul_Basic1", "Configs/Actions/Rozeul_Basic4", "Configs/Abilities/hunter-break",
            "Configs/Actions/Rozeul_Basic2", null,
            "Configs/Abilities/hunter-sweep", "Configs/Actions/Rozeul_Basic3" };

        public static ActionEffectList Read(ScriptableObject owner)
        {
            if (owner is ActionDefinitionSO action) return action.effects;
            if (owner is AbilityDefinitionSO skill) return skill.effects;
            throw new ArgumentException("招式效果仅支持普攻或能力定义");
        }

        internal static void Assign(ScriptableObject owner, ActionEffectList list)
        {
            if (owner is ActionDefinitionSO action) action.effects = list;
            else if (owner is AbilityDefinitionSO skill) skill.effects = list;
            else throw new ArgumentException("请选择招式定义");
        }

        private static ActionEffectList CloneEffects(ScriptableObject original)
        {
            // Instantiate 会复制 managed references 及 AnimationCurve；对象引用仍指向原有资源。
            var copy = Object.Instantiate(original);
            try { return Read(copy); }
            finally { Object.DestroyImmediate(copy); }
        }

        internal static void CopyEffects(ScriptableObject source, ScriptableObject destination)
            => Assign(destination, CloneEffects(source));

        private static SelfDisplacementEffect Travel(float distance, bool retreat = false)
            => new SelfDisplacementEffect { distance = distance, duration = .16f, direction = retreat
                ? ActionImpactDirection.Backward : ActionImpactDirection.Forward, trigger = ActionEffectTrigger.Accepted };
        private static InputMovementScaleEffect Input(float scale, bool fullBody = true)
            => new InputMovementScaleEffect { scale = scale, fullBody = fullBody, trigger = ActionEffectTrigger.Accepted };
        private static TargetKnockbackEffect Push(float distance)
            => new TargetKnockbackEffect { distance = distance, duration = .15f, trigger = ActionEffectTrigger.Contact,
                frequency = ActionEffectFrequency.TargetPerRelease };

        public static void Configure(ScriptableObject draft, int preset)
        {
            var effects = new ActionEffectList();
            var list = effects.entries = new List<ActionEffectEntry>();
            switch (preset)
            {
                case 0: list.Add(Travel(.1f)); list.Add(Input(.15f)); list.Add(Push(.1f)); break;
                case 1:
                    list.Add(Travel(.24f)); list.Add(Input(.05f)); list.Add(Push(.45f));
                    list.Add(new SelfRecoilEffect { distance = .12f, duration = .14f, trigger = ActionEffectTrigger.Contact, frequency = ActionEffectFrequency.Release });
                    break;
                case 2: list.Add(Input(0)); list.Add(Push(.8f)); break;
                case 3: list.Add(Travel(.6f, true)); list.Add(Input(0)); list.Add(Push(.15f)); break;
                case 4: list.Add(Input(.3f, false)); list.Add(Push(.02f)); break;
                case 5: list.Add(new LaunchEffect { target = true, velocity = new Vector2(1, 6), trigger = ActionEffectTrigger.Contact, frequency = ActionEffectFrequency.TargetPerRelease }); break;
                case 6: list.Add(new LaunchEffect { target = false, velocity = new Vector2(-2, 5), trigger = ActionEffectTrigger.Accepted }); break;
                default: throw new ArgumentOutOfRangeException(nameof(preset));
            }
            Assign(draft, effects);
        }

        internal static string Problems(ActionEffectList list)
        {
            if (list == null || list.entries == null) return null;
            var ids = new HashSet<string>();
            foreach (var entry in list.entries)
            {
                if (entry == null) return "存在缺失类型，请移除后重新添加";
                if (string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id)) return "效果身份缺失或重复";
                if (!entry.enabled) continue;
                string error = entry.ValidationError();
                if (!string.IsNullOrEmpty(error)) return error;
            }
            return null;
        }

        public static AbilityDefinitionSO EnsureSawSample()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OSSASTRA/Configs", "ActionImpactSamples");
            var saved = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(SawPath);
            if (!saved)
            {
                if (AssetDatabase.LoadMainAssetAtPath(SawPath)) throw new InvalidOperationException("圆锯样板路径已有其他类型资产");
                var saw = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
                var executor = ScriptableObject.CreateInstance<SustainedAttackAbilitySO>();
                var parts = new List<ScriptableObject> { executor };
                ActionDefinitionSO Stage(int number, SawLogic logic, float duration)
                {
                    var action = ScriptableObject.CreateInstance<ActionDefinitionSO>();
                    parts.Add(action);
                    action.name = new[] {"起手","维持","收招"}[number-1];
                    action.definitionId = "sample.freycia.saw." + number;
                    action.animationLogicId = logic;
                    action.releaseAt = 0;
                    action.moveReadyAt = action.attackReadyAt = action.finishAt = duration;
                    action.damage = 12; action.postureImpact = 8; action.staminaCost = 0;
                    return action;
                }
                bool created = false;
                try
                {
                    // 历史芙蕾骨架三段原速的 30 fps 时长。动作只引用角色通用逻辑，无骨架或媒体资源引用。
                    saw.action = Stage(1,SawLogic.Skill_14,4f/30f);
                    executor.loopAction = Stage(2,SawLogic.Skill_15,2f/30f);
                    executor.endAction = Stage(3,SawLogic.Skill_16,12f/30f);
                    executor.name = "圆锯持续执行器";
                    executor.walkDuringLoop = true;
                    executor.pulseInterval = .2f;
                    saw.name = "SawSustain";
                    saw.definitionId = "sample.freycia.saw.sustained";
                    saw.effectRoot = "freycia.saw.sustained";
                    saw.damageChannel = "melee";
                    saw.displayName = "圆锯（手感样板）";
                    saw.executor = executor;
                    saw.acceptCost = 10; saw.cooldown = .4f; saw.wearOnRelease = true;
                    saw.finishAt = saw.moveReadyAt = saw.attackReadyAt = executor.endAction.finishAt;
                    saw.delivery = new DeliveryParameters { mode=DeliveryMode.Sector, range=1.8f, arc=100f };
                    saw.releases = new[] {new AbilityReleaseNode { damage=12, postureImpact=8 }};
                    Configure(saw,4);
                    saw.Compile();
                    AssetDatabase.CreateAsset(saw,SawPath);
                    created = true;
                    foreach (var part in parts) AssetDatabase.AddObjectToAsset(part,saw);
                    EditorUtility.SetDirty(saw);
                    AssetDatabase.SaveAssetIfDirty(saw);
                    AssetDatabase.ImportAsset(SawPath,ImportAssetOptions.ForceUpdate);
                    saved = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(SawPath);
                    VerifySawSample(saved);
                }
                catch
                {
                    if (created) AssetDatabase.DeleteAsset(SawPath);
                    foreach (var part in parts) if (part && !AssetDatabase.Contains(part)) Object.DestroyImmediate(part);
                    if (saw && !AssetDatabase.Contains(saw)) Object.DestroyImmediate(saw);
                    throw;
                }
            }
            VerifySawSample(saved);
            return saved;
        }

        private static void VerifySawSample(AbilityDefinitionSO saw)
        {
            if (!saw || !(saw.executor is SustainedAttackAbilitySO executor) || !saw.action || !executor.loopAction || !executor.endAction)
                throw new InvalidOperationException("圆锯的执行器或三段动作未能读回");
            foreach (var part in new Object[] { executor,saw.action,executor.loopAction,executor.endAction })
                if (!AssetDatabase.IsSubAsset(part) || AssetDatabase.GetAssetPath(part)!=SawPath)
                    throw new InvalidOperationException("圆锯技术对象必须属于同一份样板资产");
            saw.Compile();
        }

        public static string EnsureAndVerifySamples()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OSSASTRA/Configs", "ActionImpactSamples");
            var report = new List<string>();
            for (int i = 0; i < Labels.Length; i++)
            {
                string path = Folder + "/" + Files[i] + ".asset";
                if (i == 4) { report.Add(Labels[i] + " · " + EnsureSawSample().effects.entries.Count + " 条（三段子资产）"); continue; }
                var original = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/OSSASTRA/" + Sources[i] + ".asset");
                if (!original) { report.Add("缺少来源：" + Sources[i]); continue; }
                if (!AssetDatabase.LoadMainAssetAtPath(path))
                {
                    var sample = Object.Instantiate(original);
                    sample.name = "SAMPLE " + Labels[i];
                    Configure(sample, i);
                    AssetDatabase.CreateAsset(sample, path);
                    AssetDatabase.SaveAssetIfDirty(sample);
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!saved || saved.GetType() != original.GetType() || (Read(saved)?.entries?.Count ?? 0) < 1)
                    throw new InvalidOperationException("样板读回失败：" + path);
                var error = Problems(Read(saved));
                if (error != null) throw new InvalidOperationException(path + "：" + error);
                report.Add(Labels[i] + " · " + Read(saved).entries.Count + " 条");
            }
            return string.Join("\n", report);
        }
    }

    // 练习场临时装配。武器/目录只复制到内存，归还时恢复该武器实例原有 Q 选择。
    public sealed class AttackLabSawTrial : IDisposable
    {
        private sealed class TrialInventory : ICharacterEquipmentInventory
        {
            public ICharacterEquipmentInventory original;
            public EquipmentDefinitionSO RightDefinition { get; set; }
            public EquipmentDefinitionSO LeftDefinition => original.LeftDefinition;
            public EquipmentDefinitionSO[] AvailableOffhands => original.AvailableOffhands;
            public EquipmentItemState Right => original.Right;
            public EquipmentItemState Left => original.Left;
            public bool TryEquip(EquipmentDefinitionSO definition) => original.TryEquip(definition);
        }

        private static readonly HashSet<Actor> ActiveOwners = new HashSet<Actor>();
        private readonly Actor actor;
        private readonly CharacterEquipment equipment;
        private readonly CharacterAbilities abilities;
        private readonly Dictionary<int,string> selections;
        private readonly bool hadQ;
        private readonly string originalQ;
        private readonly FieldInfo field;
        private readonly object previous;
        private object installed;
        private EquipmentDefinitionSO weapon;
        private AbilityLoadoutSO directory;
        private bool disposed;
        public AbilityDefinitionSO Sample { get; private set; }
        public bool BelongsTo(AttackLabSession session) => !disposed && actor && session && session.isActiveAndEnabled && session.Actor == actor;

        private AttackLabSawTrial(Actor owner)
        {
            actor = owner;
            equipment = actor.GetComponent<CharacterEquipment>();
            abilities = actor.GetComponent<CharacterAbilities>();
            if (!equipment || !abilities || !equipment.RightDefinition || equipment.State?.RightRuntime == null)
                throw new InvalidOperationException("芙蕾的主手尚未就绪");
            selections = equipment.State.RightRuntime.SkillSelections;
            hadQ = selections.TryGetValue(0,out originalQ);
            field = typeof(CharacterEquipment).GetField(equipment.InventoryBound ? "inventory" : "rightDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException("找不到练习场主手装配入口");
            previous = field.GetValue(equipment);
        }

        public static AttackLabSawTrial Equip(AttackLabSession session)
        {
            if (!Application.isPlaying || !session || !session.isActiveAndEnabled || !session.Actor || session.Actor.ActorId != "temporary-b")
                throw new InvalidOperationException("请先在独立测试场切到芙蕾");
            var owner = session.Actor;
            if (owner.ActionActive) throw new InvalidOperationException("动作结束或复位后可试装圆锯");
            var driver = owner.GetComponentInChildren<CharacterAnimationController>();
            var skeleton = driver ? driver.Skeleton?.Skeleton?.Data : null;
            for (int stage=1; stage<=3; stage++)
                if (skeleton?.FindAnimation("圆锯-技能1-" + stage) == null)
                    throw new InvalidOperationException("当前角色骨架缺少圆锯第 " + stage + " 段");
            var trial = new AttackLabSawTrial(owner);
            if (!ActiveOwners.Add(owner)) throw new InvalidOperationException("芙蕾已有圆锯试装，请先归还");
            try { trial.BindTrialWeapon(); return trial; }
            catch { trial.Dispose(); throw; }
        }

        private void BindTrialWeapon()
        {
            Sample = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(AttackLabImpactSamples.SawPath);
            if (!Sample) throw new InvalidOperationException("尚未生成圆锯样板，请先在编辑模式生成七份样板");
            var source = equipment.RightDefinition;
            var loadout = source.skillLoadout ? source.skillLoadout :
                AssetDatabase.LoadAssetAtPath<AbilityLoadoutSO>("Assets/OSSASTRA/Configs/Abilities/ranger_SkillLoadout.asset");
            if (!loadout) throw new InvalidOperationException("缺少圆锯试验用的主手目录");
            var existing = loadout.regularChoices.Concat(loadout.experimentalChoices).FirstOrDefault(d => d && d.definitionId == Sample.definitionId);
            if (existing && existing != Sample) throw new InvalidOperationException("现有技能与圆锯样板的定义 ID 冲突");
            weapon = Object.Instantiate(source);
            directory = Object.Instantiate(loadout);
            weapon.hideFlags = directory.hideFlags = HideFlags.HideAndDontSave;
            if (!existing) directory.experimentalChoices = directory.experimentalChoices.Append(Sample).ToArray();
            directory.Validate();
            weapon.skillLoadout = directory;
            installed = equipment.InventoryBound ? (object)new TrialInventory
                { original = (ICharacterEquipmentInventory)previous, RightDefinition = weapon } : weapon;
            field.SetValue(equipment,installed);
            selections[0] = Sample.definitionId;
            abilities.Initialize(actor);
            if (abilities.SkillDefinition(0) != Sample) throw new InvalidOperationException("圆锯未绑定到实际 Q 槽");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ActiveOwners.Remove(actor);
            try
            {
                if (installed == null) return;
                if (hadQ) selections[0]=originalQ; else selections.Remove(0);
                if (equipment && ReferenceEquals(field.GetValue(equipment),installed))
                    field.SetValue(equipment,previous);
                if (actor && abilities) abilities.Initialize(actor);
            }
            finally
            {
                if (directory) Object.DestroyImmediate(directory);
                if (weapon) Object.DestroyImmediate(weapon);
            }
        }
    }
}
