/* AM02：迁自旧代码原实现；保留作者字段和分组，生产类型/GUID独立。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimPlanMapping_Base", menuName = "ScriptableObject/Animation/AnimPlanMapping")]
public class AnimationPlanMappingSO : ScriptableObject
{
    [System.NonSerialized] public int Revision;
    [BoxGroup("Skeleton", ShowLabel = false)]
    [LabelText("Skeleton Data")]
    public SkeletonDataAsset skeletonDataAsset;

    [System.Serializable]
    public class Entry
    {
        [HorizontalGroup("Entry", MaxWidth = 180, MinWidth = 120)]
        [LabelWidth(55)]
        [ValueDropdown("GetLogicStateLabels", IsUniqueList = true, ExcludeExistingValuesInList = true)]
        public EAnimationLogicState logicID;

        [HorizontalGroup("Entry")]
        [HideLabel]
        public CharacterAnimationPlan plan = new CharacterAnimationPlan();

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

    [TabGroup("Animation Plans", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansMelee = new List<Entry>();

    [TabGroup("Animation Plans", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansMovement = new List<Entry>();

    [TabGroup("Animation Plans", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansFirearm = new List<Entry>();

    [TabGroup("Animation Plans", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansJump = new List<Entry>();

    [TabGroup("Animation Plans", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansTransition = new List<Entry>();

    [TabGroup("Animation Plans", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansSkill = new List<Entry>();

    [TabGroup("Animation Plans", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansOther = new List<Entry>();

    [HideInInspector]
    public List<Entry> defaultPlans = new List<Entry>();

    [TabGroup("Animation Plans", "Migration")]
    [Button("Migrate old defaultPlans to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateDefaultPlans()
    {
        if (defaultPlans == null || defaultPlans.Count == 0)
        {
            Debug.Log("[AnimPlanMapping] defaultPlans is empty, nothing to migrate.");
            return;
        }

        int migrated = 0;
        foreach (var entry in defaultPlans)
        {
            if (entry == null) continue;
            var list = GetListForEntry(entry);
            list.Add(entry);
            migrated++;
        }

        defaultPlans.Clear();
        Debug.Log($"[AnimPlanMapping] Migration complete, {migrated} entries moved.");
    }

    private List<Entry> GetListForEntry(Entry entry)
    {
        int v = (int)entry.logicID;
        if ((v >= 11 && v <= 13) || v == 1001) return plansMelee;
        if (v >= 21 && v <= 30) return plansMovement;
        if (v >= 31 && v <= 36) return plansFirearm;
        if (v >= 37 && v <= 43) return plansJump;
        if ((v >= 501 && v <= 510) || v == 44 || v == 45) return plansTransition;
        if (v >= 514 && v <= 529) return plansSkill;
        return plansOther;
    }

    private IEnumerable<Entry> GetAllEntries()
    {
        if (plansMelee != null) foreach (var e in plansMelee) if (e != null) yield return e;
        if (plansMovement != null) foreach (var e in plansMovement) if (e != null) yield return e;
        if (plansFirearm != null) foreach (var e in plansFirearm) if (e != null) yield return e;
        if (plansJump != null) foreach (var e in plansJump) if (e != null) yield return e;
        if (plansTransition != null) foreach (var e in plansTransition) if (e != null) yield return e;
        if (plansSkill != null) foreach (var e in plansSkill) if (e != null) yield return e;
        if (plansOther != null) foreach (var e in plansOther) if (e != null) yield return e;
        if (defaultPlans != null) foreach (var e in defaultPlans) if (e != null) yield return e;
    }

    public CharacterAnimationPlan GetDefault(EAnimationLogicState id)
    {
        foreach (var entry in GetAllEntries())
        {
            if (!entry.logicID.Equals(id))
                continue;

            if (entry.plan == null)
                continue;

            if (!entry.plan.HasOperations) continue;
            var copy = entry.plan.CloneFor(null);
            copy.SetSkeletonDataAsset(skeletonDataAsset);
            return copy;
        }

        return null;
    }

    private void OnValidate()
    {
        Revision++;
        foreach (var entry in GetAllEntries())
        {
            if (entry == null)
                continue;

            if (entry.plan == null)
            {
                entry.plan = new CharacterAnimationPlan();
            }

            entry.plan.SetSkeletonDataAsset(skeletonDataAsset);
        }
    }
}

}
