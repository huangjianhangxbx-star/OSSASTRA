using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 是本场副本，当前 HP、大破、死亡等变化都写在这里。
/// </summary>
public sealed class BattleParticipantRuntime
{
    private DRPGResolvedCombatantBuild build;
    private readonly DRPGDamageResponseDefinition[] damageResponses;
    private DRPGBattleStateMutationCapability stateMutationCapability;
    private int stateMutationScopeDepth;

    public BattleParticipantRuntime(int runtimeId, BattleParticipantSpec spec)
    {
        if (runtimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeId));

        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        build = spec.Build;
        RuntimeId = runtimeId;
        CurrentHp = spec.InitialHitPoints.HasValue
            ? Mathf.Clamp(spec.InitialHitPoints.Value, 0f, Build.EffectiveMaxHp)
            : Build.EffectiveMaxHp;
        CurrentMp = Build.EffectiveMaxMp;
        BattleStamina = new BattleStaminaValue(
            Build.EffectiveStaminaCapacity,
            Build.EffectiveStaminaCapacity,
            0d);
        CurrentPosture = Build.EffectiveMaxPosture;
        PostureBreak = new DRPGPostureBreakRuntime(
            Profile.CreatePostureBreakRules());
        Statuses = new DRPGStatusRuntimeCollection(new ParticipantId(runtimeId));
        FormationRow = spec.InitialFormationRow;
        PreferredFormationRow = spec.PreferredFormationRow;
        IsDeployed = spec.InitiallyDeployed;

        IReadOnlyList<DRPGDamageResponseDefinition> configuredResponses =
            Profile.DamageResponses;
        damageResponses = new DRPGDamageResponseDefinition[
            configuredResponses.Count];
        for (int i = 0; i < configuredResponses.Count; i++)
            damageResponses[i] = configuredResponses[i];
    }

    public int RuntimeId { get; }
    public BattleParticipantSpec Spec { get; }
    public DRPGCombatantProfileSO Profile => Spec.Profile;
    public DRPGResolvedCombatantBuild Build => build;
    public float EffectiveMaxHp => Build.EffectiveMaxHp;
    public float EffectiveMaxMp => Build.EffectiveMaxMp;
    public float EffectiveAgility => Build.EffectiveAgility;
    public float EffectiveBaseRecoveryDelay => Build.EffectiveBaseRecoveryDelay;
    public float EffectiveMaxPosture => Build.EffectiveMaxPosture;
    public float EffectivePostureRecoveryPerTurn =>
        Build.EffectivePostureRecoveryPerTurn;
    public DRPGBattleSide Side => Spec.Side;
    public DRPGBattleRosterRole RosterRole => Spec.RosterRole;
    public int FormationSlot => Spec.FormationSlot;
    public int PartyOrderIndex => Spec.PartyOrderIndex;
    public DRPGFormationRow FormationRow { get; private set; }
    public DRPGFormationRow PreferredFormationRow { get; }
    public bool IsDeployed { get; private set; }
    public bool IsBattlefieldMember =>
        Side == DRPGBattleSide.Player &&
        (RosterRole == DRPGBattleRosterRole.PartyMember ||
         RosterRole == DRPGBattleRosterRole.Protagonist);
    public bool AppearsInPartyHud => IsDeployed && IsBattlefieldMember;
    public bool IsOrdinaryEnemyTarget => IsAlive && IsDeployed && IsBattlefieldMember;
    public float CurrentHp { get; private set; }
    public float CurrentMp { get; private set; }
    public BattleStaminaValue BattleStamina { get; private set; }
    public float CurrentPosture { get; private set; }
    public float CurrentShield { get; private set; }
    public DRPGStatusRuntimeCollection Statuses { get; }
    public DRPGPostureBreakRuntime PostureBreak { get; }
    public bool IsAlive => CurrentHp > 0f;
    public DRPGPostureBreakState PostureBreakState => PostureBreak.State;
    public bool IsPostureBroken => CurrentPosture <= 0f;
    public int RemainingPostureBreakStabilityOwnTurns =>
        PostureBreak.RemainingStabilityOwnTurns;
    public float IncomingPostureDamageMultiplier =>
        PostureBreak.IncomingPostureDamageMultiplier;
    public float IncomingAilmentBuildUpMultiplier =>
        PostureBreak.IncomingAilmentBuildUpMultiplier;
    public bool CanUseDefensiveResponse => true;
    public bool IsDefending { get; private set; }
    public DRPGLowHpPresentationState LowHpState { get; private set; } =
        DRPGLowHpPresentationState.Normal;

    internal void ApplyCommittedRuleValues(
        DRPGBattleStateMutationCapability capability,
        float hitPoints,
        float mana,
        float posture,
        float shield,
        DRPGFormationRow formationRow,
        bool isDeployed,
        DRPGPostureRuleSnapshot? postureState = null,
        bool isDefending = false,
        BattleStaminaValue? stamina = null)
    {
        RequireStateCapability(capability);
        CurrentHp = Mathf.Clamp(hitPoints, 0f, EffectiveMaxHp);
        CurrentMp = Mathf.Clamp(mana, 0f, EffectiveMaxMp);
        if (stamina.HasValue)
            BattleStamina = stamina.Value;
        CurrentPosture = Mathf.Max(0f, posture);
        // The workspace has already resolved every intermediate zero/restore.
        // Applying only the final posture would lose a break followed by healing.
        if (postureState.HasValue) PostureBreak.ApplyCommittedRuleState(capability, postureState.Value);
        IsDefending = isDefending;
        CurrentShield = Mathf.Max(0f, shield);
        FormationRow = formationRow;
        IsDeployed = isDeployed;
        if (LowHpState == DRPGLowHpPresentationState.Normal &&
            CurrentHp / EffectiveMaxHp <= Profile.DamageBrokenHpRatio)
        {
            LowHpState = DRPGLowHpPresentationState.DamageBroken;
        }
    }

    internal void AttachStateMutationCapability(
        DRPGBattleStateMutationCapability capability)
    {
        ValidateStateMutationCapabilityAttach(capability);
        stateMutationCapability = capability;
        Statuses.AttachStateMutationCapability(capability);
        PostureBreak.AttachStateMutationCapability(capability);
    }

    internal void ValidateStateMutationCapabilityAttach(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));
        if (stateMutationCapability != null &&
            !ReferenceEquals(stateMutationCapability, capability))
        {
            throw new InvalidOperationException(
                "The participant already belongs to another battle state.");
        }
        Statuses.ValidateStateMutationCapabilityAttach(capability);
        PostureBreak.ValidateStateMutationCapabilityAttach(capability);
    }

    internal void EnterStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        stateMutationScopeDepth++;
        Statuses.EnterStateMutationScope(capability);
        PostureBreak.EnterStateMutationScope(capability);
    }

    internal void ExitStateMutationScope(
        DRPGBattleStateMutationCapability capability)
    {
        RequireStateCapability(capability);
        if (stateMutationScopeDepth <= 0)
            throw new InvalidOperationException("Participant mutation scope underflow.");
        PostureBreak.ExitStateMutationScope(capability);
        Statuses.ExitStateMutationScope(capability);
        stateMutationScopeDepth--;
    }

    public void ReplaceBuild(DRPGResolvedCombatantBuild replacement)
    {
        RequireDirectMutationAccess();
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        if (!ReferenceEquals(replacement.Profile, Profile) &&
            !string.Equals(
                replacement.Profile.CombatantId,
                Profile.CombatantId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A replacement Build must belong to the same combatant.",
                nameof(replacement));
        }

        build = replacement;
        CurrentHp = Mathf.Clamp(CurrentHp, 0f, EffectiveMaxHp);
        CurrentMp = Mathf.Clamp(CurrentMp, 0f, EffectiveMaxMp);
        double staminaCurrent = Math.Min(
            BattleStamina.Current,
            replacement.EffectiveStaminaCapacity);
        double staminaPending = Math.Min(
            BattleStamina.Pending,
            replacement.EffectiveStaminaCapacity - staminaCurrent);
        BattleStamina = new BattleStaminaValue(
            replacement.EffectiveStaminaCapacity,
            staminaCurrent,
            staminaPending);
        CurrentPosture = Mathf.Max(0f, CurrentPosture);
    }

    internal void ApplyCommittedWeaponBuild(DRPGBattleStateMutationCapability capability,
        DRPGResolvedCombatantBuild replacement, float hp, float mana, float posture)
    {
        RequireStateCapability(capability);
        build = replacement;
        CurrentHp = hp;
        CurrentMp = mana;
        CurrentPosture = posture;
    }

    public bool TrySetFormationRow(DRPGFormationRow row)
    {
        RequireDirectMutationAccess();
        if (FormationRow == row)
            return false;
        FormationRow = row;
        return true;
    }

    public bool TrySetDeployed(bool value)
    {
        RequireDirectMutationAccess();
        if (IsDeployed == value)
            return false;
        IsDeployed = value;
        return true;
    }

    public float ApplyDamage(float amount)
    {
        return ApplyDamage(amount, out _);
    }

    public float ApplyDamage(float amount, out float shieldAbsorbed)
    {
        RequireDirectMutationAccess();
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));

        shieldAbsorbed = Mathf.Min(CurrentShield, amount);
        CurrentShield -= shieldAbsorbed;
        amount -= shieldAbsorbed;
        float previousHp = CurrentHp;
        CurrentHp = Mathf.Clamp(CurrentHp - amount, 0f, EffectiveMaxHp);
        if (LowHpState == DRPGLowHpPresentationState.Normal &&
            CurrentHp / EffectiveMaxHp <= Profile.DamageBrokenHpRatio)
        {
            LowHpState = DRPGLowHpPresentationState.DamageBroken;
        }
        return previousHp - CurrentHp;
    }

    public float AddShield(float amount)
    {
        RequireDirectMutationAccess();
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        CurrentShield += amount;
        return amount;
    }

    public float RestoreHp(float amount)
    {
        RequireDirectMutationAccess();
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));

        float previousHp = CurrentHp;
        CurrentHp = Mathf.Clamp(CurrentHp + amount, 0f, EffectiveMaxHp);
        return CurrentHp - previousHp;
    }

    public float RestoreMp(float amount)
    {
        RequireDirectMutationAccess();
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));

        float previous = CurrentMp;
        CurrentMp = Mathf.Clamp(CurrentMp + amount, 0f, EffectiveMaxMp);
        return CurrentMp - previous;
    }

    public bool CanAffordActionResources(float manaCost, float postureCost)
    {
        return CanAffordActionResources(manaCost, postureCost, 0);
    }

    public bool CanAffordActionResources(
        float manaCost,
        float postureCost,
        int staminaCost)
    {
        return manaCost >= 0f && postureCost >= 0f && staminaCost >= 0 &&
            CurrentMp + 0.0001f >= manaCost &&
            CurrentPosture + 0.0001f >= postureCost &&
            BattleStamina.Available >= staminaCost;
    }

    public bool CanAffordStamina(int staminaCost)
    {
        return staminaCost >= 0 && BattleStamina.Available >= staminaCost;
    }

    public bool TrySpendActionResources(
        float manaCost,
        float postureCost,
        out float spentMana,
        out float spentPosture)
    {
        return TrySpendActionResources(manaCost, postureCost, 0,
            out spentMana, out spentPosture, out _);
    }

    public bool TrySpendActionResources(
        float manaCost,
        float postureCost,
        int staminaCost,
        out float spentMana,
        out float spentPosture,
        out int spentStamina)
    {
        RequireDirectMutationAccess();
        spentMana = 0f;
        spentPosture = 0f;
        spentStamina = 0;
        if (!CanAffordActionResources(manaCost, postureCost, staminaCost))
            return false;

        spentMana = Mathf.Min(CurrentMp, manaCost);
        CurrentMp -= spentMana;
        spentPosture = SpendPosture(postureCost);
        if (!BattleStamina.TrySpend(staminaCost, out BattleStaminaValue next))
            throw new InvalidOperationException(
                "Stamina affordability changed during an atomic spend.");
        BattleStamina = next;
        spentStamina = staminaCost;
        return true;
    }

    public float ReducePosture(float amount)
    {
        RequireDirectMutationAccess();
        return ApplyPostureReduction(amount, true);
    }

    public float SpendPosture(float amount)
    {
        RequireDirectMutationAccess();
        return ApplyPostureReduction(amount, false);
    }

    public bool TrySpendPostureDeferredBreak(
        float amount,
        out float spent,
        out bool depleted)
    {
        RequireDirectMutationAccess();
        spent = 0f;
        depleted = false;
        if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsPostureBroken || CurrentPosture + 0.0001f < amount)
            return false;

        float previous = CurrentPosture;
        CurrentPosture = Mathf.Clamp(
            CurrentPosture - amount,
            0f,
            EffectiveMaxPosture);
        spent = previous - CurrentPosture;
        depleted = previous > 0f && CurrentPosture <= 0f;
        return true;
    }

    public DRPGPostureBreakTransition EnterDeferredPostureBreak()
    {
        RequireDirectMutationAccess();
        if (CurrentPosture > 0f)
        {
            return new DRPGPostureBreakTransition(
                PostureBreak.State,
                PostureBreak.State,
                false,
                false,
                0f);
        }
        DRPGPostureBreakTransition transition = PostureBreak.Enter();
        if (transition.EnteredBreak)
            IsDefending = false;
        return transition;
    }

    private float ApplyPostureReduction(
        float amount,
        bool applyIncomingDamageMultiplier)
    {
        if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));

        float resolvedAmount = amount;
        float previous = CurrentPosture;
        CurrentPosture = Mathf.Max(0f, CurrentPosture - resolvedAmount);
        return previous - CurrentPosture;
    }

    public float RestorePosture(float amount)
    {
        RequireDirectMutationAccess();
        if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));

        float previous = CurrentPosture;
        float restored = CurrentPosture + amount;
        if (float.IsNaN(restored) || float.IsInfinity(restored))
            throw new OverflowException("Posture restoration overflowed the battle store.");
        CurrentPosture = restored;
        return CurrentPosture - previous;
    }

    public DRPGPostureBreakTransition ResolvePostureBreakOwnTurnStart()
    {
        RequireDirectMutationAccess();
        DRPGPostureBreakTransition transition =
            PostureBreak.ResolveOwnTurnStart();
        if (transition.RestorePostureRatio > 0f)
        {
            CurrentPosture = Mathf.Clamp(
                EffectiveMaxPosture * transition.RestorePostureRatio,
                0f,
                EffectiveMaxPosture);
        }
        return transition;
    }

    public DRPGPostureBreakTransition CompletePostureBreakOwnAction()
    {
        RequireDirectMutationAccess();
        return PostureBreak.CompleteOwnAction();
    }

    public void BeginDefend()
    {
        RequireDirectMutationAccess();
        if (CanUseDefensiveResponse)
            IsDefending = true;
    }

    public void ClearDefendAtTurnStart()
    {
        RequireDirectMutationAccess();
        IsDefending = false;
    }

    public float ModifyIncomingDamage(float amount)
    {
        return amount * IncomingDefenseMultiplier;
    }

    public float IncomingDefenseMultiplier => 1f;

    public float ResolveDamageResponseMultiplier(DRPGDamageTag tags)
    {
        if (tags == DRPGDamageTag.None)
            return 1f;

        float result = 1f;
        for (int i = 0; i < damageResponses.Length; i++)
        {
            DRPGDamageResponseDefinition response = damageResponses[i];
            if (response != null && response.Matches(tags))
                result *= response.DamageMultiplier;
        }
        return Mathf.Max(0f, result);
    }

    public int ResolveBattleResistance(DRPGDamageTag attribute)
    {
        return BattleResistanceMath.Resolve(
            attribute,
            Profile.BattleResistances);
    }

    public DRPGStatusTurnResult TickStatusesAtTurnStart()
    {
        RequireDirectMutationAccess();
        DRPGStatusTurnResult result = Statuses.TickTurnStart();
        if (result.HpDelta > 0f)
            RestoreHp(result.HpDelta);
        else if (result.HpDelta < 0f)
            ApplyDamage(-result.HpDelta);

        if (result.PostureDelta > 0f)
            RestorePosture(result.PostureDelta);
        else if (result.PostureDelta < 0f)
            ReducePosture(-result.PostureDelta);
        return result;
    }

    public float RecoverPostureAtTurnStart()
    {
        RequireDirectMutationAccess();
        return 0f;
    }

    private void RequireDirectMutationAccess()
    {
        if (stateMutationCapability != null && stateMutationScopeDepth <= 0)
        {
            throw new InvalidOperationException(
                "An attached battle participant can mutate only through " +
                "its owning battle state.");
        }
    }

    private void RequireStateCapability(
        DRPGBattleStateMutationCapability capability)
    {
        if (capability == null || stateMutationCapability == null ||
            !ReferenceEquals(capability, stateMutationCapability))
        {
            throw new InvalidOperationException(
                "The participant mutation capability is missing or stale.");
        }
    }
}
