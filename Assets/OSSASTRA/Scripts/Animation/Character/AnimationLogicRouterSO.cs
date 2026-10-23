/* AM02：迁自旧代码原实现；保留作者字段和分组，生产类型/GUID独立。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationLogicRouter", menuName = "ScriptableObject/Animation/AnimationLogicRouter")]
public class AnimationLogicRouterSO : ScriptableObject
{
    [System.NonSerialized] public int Revision;
    public enum ConditionType
    {
        Always,

        HasWeapon,
        NoWeapon,

        HasIntent,
        NotHasIntent,

        DataBusBoolTrue,
        DataBusBoolFalse,

        DataBusEnumMatch,
        DataBusStringMatch,
    }

    [System.Serializable]
    public class Condition
    {
        [HorizontalGroup("Cond", Width = 130)]
        [LabelWidth(45)]
        public ConditionType type = ConditionType.Always;

        [HorizontalGroup("Cond", Width = 160)]
        [LabelWidth(55)]
        [Tooltip("HasIntent / NotHasIntent use this. e.g. AutoAttack / Aim / Skill")]
        [ShowIf("@type == ConditionType.HasIntent || type == ConditionType.NotHasIntent")]
        public AnimationIntentSourceStateMachine intentSource = AnimationIntentSourceStateMachine.None;

        [HorizontalGroup("Cond", Width = 160)]
        [LabelWidth(50)]
        [Tooltip("DataBus field name")]
        [ShowIf("@type == ConditionType.DataBusBoolTrue || type == ConditionType.DataBusBoolFalse || type == ConditionType.DataBusEnumMatch || type == ConditionType.DataBusStringMatch")]
        public string dataBusFieldName;

        [HorizontalGroup("Cond", Width = 120)]
        [LabelWidth(35)]
        [Tooltip("Enum/String compare value")]
        [ShowIf("@type == ConditionType.DataBusEnumMatch || type == ConditionType.DataBusStringMatch")]
        public string compareString;
    }

    [System.Serializable]
    public class RouteRule
    {
        [HorizontalGroup("Rule", Width = 70)]
        [LabelWidth(40)]
        [Tooltip("Match the Intent source? Uncheck = any source")]
        public bool matchSource = false;

        [HorizontalGroup("Rule", Width = 170)]
        [LabelWidth(50)]
        [Tooltip("Which state machine submitted the Intent, e.g. Movement / AutoAttack")]
        [ShowIf("matchSource")]
        public AnimationIntentSourceStateMachine source = AnimationIntentSourceStateMachine.None;

        [HorizontalGroup("Rule", Width = 170)]
        [LabelWidth(90)]
        [Tooltip("Input semantic, e.g. Jog")]
        public EAnimSemantic inputSemantic;

        [HorizontalGroup("Rule", Width = 190)]
        [LabelWidth(65)]
        [ValueDropdown("GetOutputIDLabels", IsUniqueList = false)]
        [Tooltip("Output logic state, e.g. Jog_LowerOnly_Attack")]
        public EAnimationLogicState outputID;

        [HorizontalGroup("Rule", Width = 80)]
        [LabelWidth(45)]
        [Tooltip("Higher = higher priority")]
        public int priority = 0;

        [FoldoutGroup("Rule/Conditions", false)]
        [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 3)]
        [Tooltip("All conditions must be met")]
        public List<Condition> conditions = new List<Condition>();

        private static IEnumerable<ValueDropdownItem<EAnimationLogicState>> GetOutputIDLabels()
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

    [TabGroup("Route Rules", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesMelee = new List<RouteRule>();

    [TabGroup("Route Rules", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesMovement = new List<RouteRule>();

    [TabGroup("Route Rules", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesFirearm = new List<RouteRule>();

    [TabGroup("Route Rules", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesJump = new List<RouteRule>();

    [TabGroup("Route Rules", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesTransition = new List<RouteRule>();

    [TabGroup("Route Rules", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesSkill = new List<RouteRule>();

    [TabGroup("Route Rules", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesOther = new List<RouteRule>();

    [HideInInspector]
    public List<RouteRule> rules = new List<RouteRule>();

    [TabGroup("Route Rules", "Migration")]
    [Button("Migrate old rules to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateRules()
    {
        if (rules == null || rules.Count == 0)
        {
            Debug.Log("[LogicRouter] rules is empty, nothing to migrate.");
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
        Debug.Log($"[LogicRouter] Migration complete, {migrated} entries moved.");
    }

    private List<RouteRule> GetListForRule(RouteRule rule)
    {
        int v = (int)rule.outputID;
        if ((v >= 11 && v <= 13) || v == 1001) return rulesMelee;
        if (v >= 21 && v <= 30) return rulesMovement;
        if (v >= 31 && v <= 36) return rulesFirearm;
        if (v >= 37 && v <= 43) return rulesJump;
        if ((v >= 501 && v <= 510) || v == 44 || v == 45) return rulesTransition;
        if (v >= 514 && v <= 529) return rulesSkill;
        return rulesOther;
    }

    private IEnumerable<RouteRule> GetAllRules()
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

    public EAnimationLogicState Route(AnimationIntent intent, AnimationComposeContext context)
    {
        if (intent == null)
            return EAnimationLogicState.None;

        RouteRule bestRule = null;

        foreach (var rule in GetAllRules())
        {
            if (rule == null)
                continue;

            if (rule.matchSource && rule.source != intent.intentSourceStateMatine)
                continue;

            if (!rule.inputSemantic.Equals(intent.semantic))
                continue;

            if (!CheckConditions(rule, context))
                continue;

            if (bestRule == null || rule.priority > bestRule.priority)
            {
                bestRule = rule;
            }
        }

        if (bestRule != null)
            return bestRule.outputID;

        return EAnimationLogicState.None;
    }

    private bool CheckConditions(RouteRule rule, AnimationComposeContext context)
    {
        if (rule.conditions == null || rule.conditions.Count <= 0)
            return true;

        for (int i = 0; i < rule.conditions.Count; i++)
        {
            Condition condition = rule.conditions[i];

            if (!CheckCondition(condition, context))
                return false;
        }

        return true;
    }

    private bool CheckCondition(Condition condition, AnimationComposeContext context)
    {
        if (condition == null)
            return true;

        switch (condition.type)
        {
            case ConditionType.Always:
                return true;

            case ConditionType.HasWeapon:
                return context != null && context.HasWeapon;

            case ConditionType.NoWeapon:
                return context != null && !context.HasWeapon;

            case ConditionType.HasIntent:
                return context != null && context.HasIntent(condition.intentSource);

            case ConditionType.NotHasIntent:
                return context != null && !context.HasIntent(condition.intentSource);

            case ConditionType.DataBusBoolTrue:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object trueValue)
                       && trueValue is bool b1
                       && b1;

            case ConditionType.DataBusBoolFalse:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object falseValue)
                       && falseValue is bool b2
                       && !b2;

            case ConditionType.DataBusEnumMatch:
            case ConditionType.DataBusStringMatch:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object value)
                       && value != null
                       && value.ToString() == condition.compareString;
        }

        return false;
    }

    private bool ReadDataBusValue(AnimationComposeContext context, string fieldName, out object value)
    {
        value = null;
        return context != null && context.TryRead(fieldName, out value);
    }
    private void OnValidate() { Revision++; }
}

}
