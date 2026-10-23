using System;

public sealed class DRPGBattlePreparedAction
{
    internal DRPGBattlePreparedAction(
        ActionIdentity identity,
        DRPGBattleCommand command,
        DRPGResolvedActionSpec spec,
        DRPGActionResourceTransaction resourceTransaction,
        DRPGBattleWeaponActionContext weaponActionContext = null,
        DRPGActionProgressTransaction progressTransaction = null,
        DRPGResolvedImpactSequence impactSequence = null,
        DRPGLegacyActionMutationPort legacyMutationPort = null,
        DRPGBasicAttackFrozenSnapshot? basicAttackSnapshot = null)
    {
        if (!identity.IsValid)
            throw new ArgumentException("Action identity is required.", nameof(identity));

        Identity = identity;
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        ResourceTransaction = resourceTransaction ??
            throw new ArgumentNullException(nameof(resourceTransaction));
        WeaponActionContext = weaponActionContext;
        ProgressTransaction = progressTransaction;
        ImpactSequence = impactSequence ??
            DRPGResolvedImpactSequence.CreateFor(command, spec);
        ImpactLedger = new DRPGBattleImpactCommitLedger(ImpactSequence.Count);
        LegacyMutationPort = legacyMutationPort;
        BasicAttackSnapshot = basicAttackSnapshot;
    }

    public ActionIdentity Identity { get; }
    public long ActionId => Identity.ActionId;
    public DRPGBattleCommand Command { get; }
    public DRPGResolvedActionSpec Spec { get; }
    public DRPGActionResourceTransaction ResourceTransaction { get; }
    public DRPGActionResourceReceipt ResourceReceipt =>
        ResourceTransaction.Receipt;
    public DRPGBattleWeaponActionContext WeaponActionContext { get; }
    public DRPGActionProgressTransaction ProgressTransaction { get; }
    public DRPGResolvedImpactSequence ImpactSequence { get; }
    public DRPGBattleImpactCommitLedger ImpactLedger { get; }
    internal DRPGLegacyActionMutationPort LegacyMutationPort { get; }
    internal DRPGBasicAttackFrozenSnapshot? BasicAttackSnapshot { get; }
    public int ActorRuntimeId => Command.ActorRuntimeId;
    public int TargetRuntimeId => Command.TargetRuntimeId;
}
