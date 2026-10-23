using System;

public sealed class DRPGBattleCommand
{
    public DRPGBattleCommand(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill,
        DRPGBattleCommandStage stage = DRPGBattleCommandStage.Immediate)
        : this(
            DRPGBattleCommandKind.Skill,
            actorRuntimeId,
            targetRuntimeId,
            skill,
            null,
            stage,
            true,
            null)
    {
    }

    private DRPGBattleCommand(
        DRPGBattleCommandKind kind,
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill,
        DRPGBattleItemDefinition item,
        DRPGBattleCommandStage stage,
        bool legacySkillCompatibility,
        DRPGFormationRow? destinationFormationRow,
        string weaponInstanceId = null)
    {
        if (actorRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        bool requiresTarget = kind == DRPGBattleCommandKind.BasicAttack ||
            kind == DRPGBattleCommandKind.Skill ||
            kind == DRPGBattleCommandKind.ResonanceSkill ||
            kind == DRPGBattleCommandKind.Item;
        if (requiresTarget && targetRuntimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        if (!requiresTarget && targetRuntimeId != 0)
            throw new ArgumentOutOfRangeException(nameof(targetRuntimeId));
        if ((kind == DRPGBattleCommandKind.BasicAttack ||
             kind == DRPGBattleCommandKind.Skill ||
             kind == DRPGBattleCommandKind.ResonanceSkill) && skill == null)
        {
            throw new ArgumentNullException(nameof(skill));
        }
        if (kind == DRPGBattleCommandKind.Item && item == null)
            throw new ArgumentNullException(nameof(item));
        if (kind == DRPGBattleCommandKind.SwitchWeapon &&
            string.IsNullOrWhiteSpace(weaponInstanceId))
        {
            throw new ArgumentException(
                "Weapon switching requires an instance ID.",
                nameof(weaponInstanceId));
        }

        Kind = kind;
        ActorRuntimeId = actorRuntimeId;
        TargetRuntimeId = targetRuntimeId;
        Skill = skill;
        ItemDefinition = item;
        Stage = stage;
        DestinationFormationRow = destinationFormationRow;
        WeaponInstanceId = weaponInstanceId ?? string.Empty;
        IsLegacySkillCompatibility = legacySkillCompatibility;
    }

    public DRPGBattleCommandKind Kind { get; }
    public int ActorRuntimeId { get; }
    public int TargetRuntimeId { get; }
    public DRPGSkillSO Skill { get; }
    public DRPGBattleItemDefinition ItemDefinition { get; }
    public DRPGBattleCommandStage Stage { get; }
    public DRPGFormationRow? DestinationFormationRow { get; }
    public string WeaponInstanceId { get; }
    public bool RequiresTarget => TargetRuntimeId > 0;
    internal bool IsLegacySkillCompatibility { get; }

    public static DRPGBattleCommand BasicAttack(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO basicAttack)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.BasicAttack,
            actorRuntimeId,
            targetRuntimeId,
            basicAttack,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand UseSkill(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill,
        DRPGBattleCommandStage stage = DRPGBattleCommandStage.Immediate)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Skill,
            actorRuntimeId,
            targetRuntimeId,
            skill,
            null,
            stage,
            false,
            null);
    }

    public static DRPGBattleCommand ResonanceSkill(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGSkillSO skill)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.ResonanceSkill,
            actorRuntimeId,
            targetRuntimeId,
            skill,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand Item(
        int actorRuntimeId,
        int targetRuntimeId,
        DRPGBattleItemDefinition item)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Item,
            actorRuntimeId,
            targetRuntimeId,
            null,
            item,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand Defend(int actorRuntimeId)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Defend,
            actorRuntimeId,
            0,
            null,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand Escape(int actorRuntimeId)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Escape,
            actorRuntimeId,
            0,
            null,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand Wait(int actorRuntimeId)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Wait,
            actorRuntimeId,
            0,
            null,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null);
    }

    public static DRPGBattleCommand Reposition(
        int actorRuntimeId,
        DRPGFormationRow destinationRow)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.Reposition,
            actorRuntimeId,
            0,
            null,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            destinationRow);
    }

    public static DRPGBattleCommand SwitchWeapon(
        int actorRuntimeId,
        string weaponInstanceId)
    {
        return new DRPGBattleCommand(
            DRPGBattleCommandKind.SwitchWeapon,
            actorRuntimeId,
            0,
            null,
            null,
            DRPGBattleCommandStage.Immediate,
            false,
            null,
            weaponInstanceId);
    }
}
