/* AM02：迁自旧代码原实现；保留作者字段和分组，生产类型/GUID独立。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "OverridePlan_Weapon", menuName = "ScriptableObject/Animation/AnimOverrideRuleSet")]
public class AnimationPlanOverrideRuleSetSO : ScriptableObject
{
    [System.NonSerialized] public int Revision;
    [BoxGroup("Skeleton", ShowLabel = false)]
    [LabelText("Skeleton Data")]
    public SkeletonDataAsset skeletonDataAsset;

    public enum ConditionType
    {
        Always,
        Bool,
        FloatGreater,
        FloatLess,
        EnumMatch,
        StringMatch
    }

    [System.Serializable]
    public class Rule
    {
        [HorizontalGroup("Entry", MaxWidth = 180, MinWidth = 120)]
        [LabelWidth(55)]
        [ValueDropdown("GetLogicStateLabels", IsUniqueList = true, ExcludeExistingValuesInList = true)]
        public EAnimationLogicState targetID;

        [HorizontalGroup("Entry")]
        [HideLabel]
        public CharacterAnimationPlan replacementPlan = new CharacterAnimationPlan();

        [FoldoutGroup("Entry/Condition", false)]
        [HorizontalGroup("Entry/Condition/Row", Width = 130)]
        [LabelWidth(60)]
        public ConditionType condition = ConditionType.Always;

        [HorizontalGroup("Entry/Condition/Row", Width = 150)]
        [LabelWidth(50)]
        [Tooltip("Always 条件下可留空。其余填写 CharacterDataBus 字段名")]
        public string dataBusFieldName;

        [HorizontalGroup("Entry/Condition/Row", Width = 80)]
        [LabelWidth(40)]
        [ShowIf("@condition == ConditionType.FloatGreater || condition == ConditionType.FloatLess")]
        public float threshold;

        [HorizontalGroup("Entry/Condition/Row", Width = 120)]
        [LabelWidth(35)]
        [ShowIf("@condition == ConditionType.EnumMatch || condition == ConditionType.StringMatch")]
        public string compareString;

        private static IEnumerable<ValueDropdownItem<EAnimationLogicState>> GetLogicStateLabels()
        {
            foreach (EAnimationLogicState value in System.Enum.GetValues(typeof(EAnimationLogicState)))
                if ((int)value >= 1001 && value != EAnimationLogicState.None)
                    yield return new ValueDropdownItem<EAnimationLogicState> { Text = "当前动作/" + value, Value = value };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Melee ——", Value = (EAnimationLogicState)(-1) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 1", Value = EAnimationLogicState.Attack_Normal_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 2", Value = EAnimationLogicState.Attack_Normal_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 3", Value = EAnimationLogicState.Attack_Normal_3 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Movement ——", Value = (EAnimationLogicState)(-2) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk", Value = EAnimationLogicState.Walk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk Back", Value = EAnimationLogicState.WalkBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog", Value = EAnimationLogicState.Jog };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Back", Value = EAnimationLogicState.JogBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Fast", Value = EAnimationLogicState.JogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Dash", Value = EAnimationLogicState.Dash };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle", Value = EAnimationLogicState.Idle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Crouch", Value = EAnimationLogicState.Crouch };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Firearm ——", Value = (EAnimationLogicState)(-3) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim", Value = EAnimationLogicState.Aim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Fire", Value = EAnimationLogicState.Fire };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Reload", Value = EAnimationLogicState.Reload };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Weapon LowReady", Value = EAnimationLogicState.Weapon_LowReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAim ShootReady", Value = EAnimationLogicState.NotAimShootReady };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Avoid", Value = EAnimationLogicState.Avoid_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Jump ——", Value = (EAnimationLogicState)(-4) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump PreStart", Value = EAnimationLogicState.Jump_PreStart_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Start", Value = EAnimationLogicState.Jump_Start_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Rising", Value = EAnimationLogicState.Jump_Rising_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Peak", Value = EAnimationLogicState.Jump_Peak_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump 2nd Air", Value = EAnimationLogicState.JumpSecond__Air_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Fall", Value = EAnimationLogicState.Jump_Fall_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Land", Value = EAnimationLogicState.Jump_Land_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Transition ——", Value = (EAnimationLogicState)(-5) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog End", Value = EAnimationLogicState.Jog_End_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Turn Around", Value = EAnimationLogicState.TurnAround };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim>Gundown", Value = EAnimationLogicState.AimToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>Aim", Value = EAnimationLogicState.GundownToAim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>NotAimShoot", Value = EAnimationLogicState.GundownToNotAimShoot };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAimShoot>Gundown", Value = EAnimationLogicState.NotAimShootToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogFast", Value = EAnimationLogicState.IdleToJogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogSlow", Value = EAnimationLogicState.IdleToJogSlow };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>Walk", Value = EAnimationLogicState.IdleToWalk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogFast>Idle", Value = EAnimationLogicState.JogFastToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogSlow>Idle", Value = EAnimationLogicState.JogSlowToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk>Idle", Value = EAnimationLogicState.WalkToIdle };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Combat/Skill ——", Value = (EAnimationLogicState)(-6) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Combat Idle", Value = EAnimationLogicState.CombatIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Notice", Value = EAnimationLogicState.SkillNotice };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Ready", Value = EAnimationLogicState.SkillReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 1", Value = EAnimationLogicState.Skill_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 2", Value = EAnimationLogicState.Skill_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 3", Value = EAnimationLogicState.Skill_3 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 4", Value = EAnimationLogicState.Skill_4 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 5", Value = EAnimationLogicState.Skill_5 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 6", Value = EAnimationLogicState.Skill_6 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 7", Value = EAnimationLogicState.Skill_7 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 8", Value = EAnimationLogicState.Skill_8 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 9", Value = EAnimationLogicState.Skill_9 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 10", Value = EAnimationLogicState.Skill_10 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 11", Value = EAnimationLogicState.Skill_11 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 12", Value = EAnimationLogicState.Skill_12 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 13", Value = EAnimationLogicState.Skill_13 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 14", Value = EAnimationLogicState.Skill_14 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 15", Value = EAnimationLogicState.Skill_15 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 16", Value = EAnimationLogicState.Skill_16 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "None", Value = EAnimationLogicState.None };
        }
    }

    [TabGroup("Override Rules", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesMelee = new List<Rule>();

    [TabGroup("Override Rules", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesMovement = new List<Rule>();

    [TabGroup("Override Rules", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesFirearm = new List<Rule>();

    [TabGroup("Override Rules", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesJump = new List<Rule>();

    [TabGroup("Override Rules", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesTransition = new List<Rule>();

    [TabGroup("Override Rules", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesSkill = new List<Rule>();

    [TabGroup("Override Rules", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesOther = new List<Rule>();

    [HideInInspector]
    public List<Rule> rules = new List<Rule>();

    [TabGroup("Override Rules", "Migration")]
    [Button("Migrate old rules to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateRules()
    {
        if (rules == null || rules.Count == 0)
        {
            Debug.Log("[OverrideRuleSet] rules is empty, nothing to migrate.");
            return;
        }

        int migrated = 0;
        foreach (var rule in rules)
        {
            if (rule == null) continue;
            var list = GetListForRule(rule);
            list.Add(rule);
            migrated++;
        }

        rules.Clear();
        Debug.Log($"[OverrideRuleSet] Migration complete, {migrated} entries moved.");
    }

    private List<Rule> GetListForRule(Rule rule)
    {
        int v = (int)rule.targetID;
        if ((v >= 11 && v <= 13) || v == 1001) return rulesMelee;
        if (v >= 21 && v <= 30) return rulesMovement;
        if (v >= 31 && v <= 36) return rulesFirearm;
        if (v >= 37 && v <= 43) return rulesJump;
        if ((v >= 501 && v <= 510) || v == 44 || v == 45) return rulesTransition;
        if (v >= 514 && v <= 529) return rulesSkill;
        return rulesOther;
    }

    private IEnumerable<Rule> GetAllRules()
    {
        if (rulesMelee != null) foreach (var r in rulesMelee) if (r != null) yield return r;
        if (rulesMovement != null) foreach (var r in rulesMovement) if (r != null) yield return r;
        if (rulesFirearm != null) foreach (var r in rulesFirearm) if (r != null) yield return r;
        if (rulesJump != null) foreach (var r in rulesJump) if (r != null) yield return r;
        if (rulesTransition != null) foreach (var r in rulesTransition) if (r != null) yield return r;
        if (rulesSkill != null) foreach (var r in rulesSkill) if (r != null) yield return r;
        if (rulesOther != null) foreach (var r in rulesOther) if (r != null) yield return r;
        if (rules != null) foreach (var r in rules) if (r != null) yield return r;
    }

    public CharacterAnimationPlan GetOverride(EAnimationLogicState id, AnimationComposeContext context)
    {
        foreach (var rule in GetAllRules())
        {
            if (!rule.targetID.Equals(id))
                continue;

            if (rule.replacementPlan == null)
                continue;

            if (!CheckCondition(rule, context))
                continue;

            if (!HasUsablePlan(rule.replacementPlan))
            {
                Debug.LogWarning(
                    $"[AnimationPlanOverrideRuleSetSO] Override rule ReplacementPlan is empty, skipping. TargetID: {id}",
                    this
                );
                continue;
            }

            var copy = rule.replacementPlan.CloneFor(context?.root);
            copy.SetSkeletonDataAsset(skeletonDataAsset);
            return copy;
        }

        return null;
    }

    private bool HasUsablePlan(CharacterAnimationPlan plan) => plan != null && plan.HasOperations;

    private bool CheckCondition(Rule rule, AnimationComposeContext context)
    {
        if (rule.condition == ConditionType.Always) return true;
        if (context == null || !context.TryRead(rule.dataBusFieldName, out var value)) return false;
        switch (rule.condition)
        {
            case ConditionType.Bool: return value is bool b && b;
            case ConditionType.FloatGreater: return TryConvertToFloat(value, out var greater) && greater > rule.threshold;
            case ConditionType.FloatLess: return TryConvertToFloat(value, out var less) && less < rule.threshold;
            case ConditionType.EnumMatch:
            case ConditionType.StringMatch: return value != null && value.ToString() == rule.compareString;
            default: return false;
        }
    }

    private bool TryConvertToFloat(object value, out float result)
    {
        if (value is double doubleValue && !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue) && doubleValue <= float.MaxValue && doubleValue >= -float.MaxValue)
        { result = (float)doubleValue; return true; }
        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0f;
        return false;
    }

    private void OnValidate()
    {
        Revision++;
        foreach (var rule in GetAllRules())
        {
            if (rule == null)
                continue;

            if (rule.replacementPlan == null)
            {
                rule.replacementPlan = new CharacterAnimationPlan();
            }

            rule.replacementPlan.SetSkeletonDataAsset(skeletonDataAsset);
        }
    }
}

}
