using System;
using System.Collections.Generic;

internal readonly struct DRPGBasicAttackEffectSnapshot
{
    internal DRPGBasicAttackEffectSnapshot(DRPGActionEffectDefinition effect, float skillPower)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));
        Kind = effect.Kind;
        Target = effect.Target;
        MagnitudeMode = effect.MagnitudeMode;
        Magnitude = effect.Magnitude;
        ResolvedMagnitude = effect.MagnitudeMode == DRPGEffectMagnitudeMode.SkillPower
            ? skillPower * effect.Magnitude : effect.Magnitude;
        DamageTags = effect.DamageTags;
        Status = new DRPGBasicAttackStatusDefinitionSnapshot(effect.Status);
        StatusStacks = effect.StatusStacks;
        StatusDurationOverride = effect.StatusDurationOverride;
        FormationRowDelta = effect.FormationRowDelta;
        TimelineDelta = effect.TimelineDelta;
        TimelinePriorityDelta = effect.TimelinePriorityDelta;
    }

    internal DRPGBasicAttackEffectSnapshot(DRPGResolvedActionEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));
        Kind = effect.Kind;
        Target = effect.Target;
        MagnitudeMode = effect.MagnitudeMode;
        Magnitude = effect.Magnitude;
        ResolvedMagnitude = effect.Magnitude;
        DamageTags = effect.DamageTags;
        Status = new DRPGBasicAttackStatusDefinitionSnapshot(effect.Status);
        StatusStacks = effect.StatusStacks;
        StatusDurationOverride = effect.StatusDurationOverride;
        FormationRowDelta = effect.FormationRowDelta;
        TimelineDelta = effect.TimelineDelta;
        TimelinePriorityDelta = effect.TimelinePriorityDelta;
    }

    internal DRPGActionEffectKind Kind { get; }
    internal DRPGActionEffectTarget Target { get; }
    internal DRPGEffectMagnitudeMode MagnitudeMode { get; }
    internal float Magnitude { get; }
    internal float ResolvedMagnitude { get; }
    internal DRPGDamageTag DamageTags { get; }
    internal DRPGBasicAttackStatusDefinitionSnapshot Status { get; }
    internal string StatusId => Status.StatusId;
    internal int StatusStacks { get; }
    internal int StatusDurationOverride { get; }
    internal int FormationRowDelta { get; }
    internal float TimelineDelta { get; }
    internal int TimelinePriorityDelta { get; }
}

internal readonly struct DRPGBasicAttackStatusDefinitionSnapshot
{
    internal DRPGBasicAttackStatusDefinitionSnapshot(
        DRPGStatusDefinitionSO definition)
    {
        IsSpecified = definition != null;
        StatusId = definition != null ? definition.StatusId : string.Empty;
        DisplayName = definition != null ? definition.DisplayName : string.Empty;
        Description = definition != null ? definition.Description : string.Empty;
        IconResourceKey = definition != null && definition.Icon != null
            ? definition.Icon.name
            : string.Empty;
        DurationTurns = definition != null ? definition.DurationTurns : 0;
        MaxStacks = definition != null ? definition.MaxStacks : 0;
        RefreshPolicy = definition != null ? definition.RefreshPolicy : default;
        TriggerTiming = definition != null ? definition.TriggerTiming : default;
        Priority = definition != null ? definition.Priority : 0;
        PersistsAfterSourceDefeat = definition != null && definition.PersistsAfterSourceDefeat;
        OutgoingDamageMultiplier = definition != null
            ? definition.OutgoingDamageMultiplier
            : 1f;
        IncomingDamageMultiplier = definition != null
            ? definition.IncomingDamageMultiplier
            : 1f;
        OutgoingHealingMultiplier = definition != null
            ? definition.OutgoingHealingMultiplier
            : 1f;
        ManaCostMultiplier = definition != null
            ? definition.ManaCostMultiplier
            : 1f;
        PostureCostMultiplier = definition != null
            ? definition.PostureCostMultiplier
            : 1f;
        TurnStartHpDelta = definition != null
            ? definition.TurnStartHpDelta
            : 0f;
        TurnStartPostureDelta = definition != null
            ? definition.TurnStartPostureDelta
            : 0f;
    }

    internal DRPGBasicAttackStatusDefinitionSnapshot(
        DRPGResolvedStatusSpec definition)
    {
        IsSpecified = definition != null;
        StatusId = definition != null ? definition.StatusId : string.Empty;
        DisplayName = definition != null ? definition.DisplayName : string.Empty;
        Description = string.Empty;
        IconResourceKey = definition != null && definition.Icon != null
            ? definition.Icon.name
            : string.Empty;
        DurationTurns = definition != null ? definition.DurationTurns : 0;
        MaxStacks = definition != null ? definition.MaxStacks : 0;
        RefreshPolicy = definition != null ? definition.RefreshPolicy : default;
        TriggerTiming = definition != null ? definition.TriggerTiming : default;
        Priority = definition != null ? definition.Priority : 0;
        PersistsAfterSourceDefeat = definition != null && definition.PersistsAfterSourceDefeat;
        OutgoingDamageMultiplier = definition != null
            ? definition.OutgoingDamageMultiplier
            : 1f;
        IncomingDamageMultiplier = definition != null
            ? definition.IncomingDamageMultiplier
            : 1f;
        OutgoingHealingMultiplier = definition != null
            ? definition.OutgoingHealingMultiplier
            : 1f;
        ManaCostMultiplier = definition != null
            ? definition.ManaCostMultiplier
            : 1f;
        PostureCostMultiplier = definition != null
            ? definition.PostureCostMultiplier
            : 1f;
        TurnStartHpDelta = definition != null
            ? definition.TurnStartHpDelta
            : 0f;
        TurnStartPostureDelta = definition != null
            ? definition.TurnStartPostureDelta
            : 0f;
    }

    internal bool IsSpecified { get; }
    internal string StatusId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal string IconResourceKey { get; }
    internal int DurationTurns { get; }
    internal int MaxStacks { get; }
    internal DRPGStatusRefreshPolicy RefreshPolicy { get; }
    internal DRPGStatusTriggerTiming TriggerTiming { get; }
    internal int Priority { get; }
    internal bool PersistsAfterSourceDefeat { get; }
    internal float OutgoingDamageMultiplier { get; }
    internal float IncomingDamageMultiplier { get; }
    internal float OutgoingHealingMultiplier { get; }
    internal float ManaCostMultiplier { get; }
    internal float PostureCostMultiplier { get; }
    internal float TurnStartHpDelta { get; }
    internal float TurnStartPostureDelta { get; }
}

internal readonly struct DRPGBasicAttackContributionSnapshot
{
    internal DRPGBasicAttackContributionSnapshot(
        DRPGActionSpecContributionTrace contribution)
    {
        if (contribution == null)
            throw new ArgumentNullException(nameof(contribution));
        SourceId = contribution.SourceId;
        SourceKind = contribution.SourceKind;
        SourceSequence = contribution.SourceSequence;
        WasApplied = contribution.WasApplied;
        SkipReason = contribution.SkipReason;
        BeforeSummary = contribution.BeforeSummary;
        AfterSummary = contribution.AfterSummary;
    }

    internal string SourceId { get; }
    internal DRPGActionModifierSourceKind SourceKind { get; }
    internal int SourceSequence { get; }
    internal bool WasApplied { get; }
    internal string SkipReason { get; }
    internal string BeforeSummary { get; }
    internal string AfterSummary { get; }
}

internal readonly struct DRPGBasicAttackVisualBaseline
{
    internal DRPGBasicAttackVisualBaseline(
        ActionTracePosition localScale,
        float red,
        float green,
        float blue,
        float alpha,
        string poseSemantic)
    {
        LocalScale = localScale;
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
        PoseSemantic = poseSemantic ?? string.Empty;
    }

    internal ActionTracePosition LocalScale { get; }
    internal float Red { get; }
    internal float Green { get; }
    internal float Blue { get; }
    internal float Alpha { get; }
    internal string PoseSemantic { get; }

    internal static DRPGBasicAttackVisualBaseline DefaultIdle =>
        new DRPGBasicAttackVisualBaseline(
            new ActionTracePosition(1f, 1f, 1f),
            1f,
            1f,
            1f,
            1f,
            "Idle");
}

internal readonly struct DRPGBasicAttackTimingSnapshot
{
    internal DRPGBasicAttackTimingSnapshot(
        float announcementSeconds,
        float conflictHoldSeconds,
        float windupMinimumSeconds,
        float recoilOutSeconds,
        float recoilReturnSeconds,
        float returnSeconds,
        float settlementItemSeconds,
        float emptySettlementSeconds)
    {
        AnnouncementSeconds = announcementSeconds;
        ConflictHoldSeconds = conflictHoldSeconds;
        WindupMinimumSeconds = windupMinimumSeconds;
        RecoilOutSeconds = recoilOutSeconds;
        RecoilReturnSeconds = recoilReturnSeconds;
        ReturnSeconds = returnSeconds;
        SettlementItemSeconds = settlementItemSeconds;
        EmptySettlementSeconds = emptySettlementSeconds;
    }

    internal float AnnouncementSeconds { get; }
    internal float ConflictHoldSeconds { get; }
    internal float WindupMinimumSeconds { get; }
    internal float RecoilOutSeconds { get; }
    internal float RecoilReturnSeconds { get; }
    internal float ReturnSeconds { get; }
    internal float SettlementItemSeconds { get; }
    internal float EmptySettlementSeconds { get; }

    // Presentation policy is configurable; validity only protects finite durations.
    internal bool IsValid => NonNegative(AnnouncementSeconds) && NonNegative(ConflictHoldSeconds) &&
        NonNegative(WindupMinimumSeconds) && NonNegative(RecoilOutSeconds) && NonNegative(RecoilReturnSeconds) &&
        NonNegative(ReturnSeconds) && ReturnSeconds > 0f && NonNegative(SettlementItemSeconds) && NonNegative(EmptySettlementSeconds);
    private static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    internal static DRPGBasicAttackTimingSnapshot CompactDefaults =>
        new DRPGBasicAttackTimingSnapshot(.04f, .14f, .10f, .09f, .16f, .22f, .45f, 0f);

    internal static DRPGBasicAttackTimingSnapshot ApprovedDefaults =>
        new DRPGBasicAttackTimingSnapshot(
            0.75f,
            0.12f,
            0.30f,
            0.09f,
            0.16f,
            0.38f,
            0.45f,
            0.30f);
}

internal readonly struct DRPGBasicAttackSemanticSnapshot
{
    internal DRPGBasicAttackSemanticSnapshot(
        string actionName,
        EAnimationLogicState animationIntent,
        string presentationKey,
        string impactAudioSemantic,
        string impactVfxSemantic,
        BasicAttackMarkerMode markerMode = BasicAttackMarkerMode.AuthoredFallback,
        int expectedMarkerOrdinal = 0)
    {
        ActionName = actionName ?? string.Empty;
        AnimationIntent = animationIntent;
        PresentationKey = presentationKey ?? string.Empty;
        ImpactAudioSemantic = impactAudioSemantic ?? string.Empty;
        ImpactVfxSemantic = impactVfxSemantic ?? string.Empty;
        MarkerMode = markerMode;
        ExpectedMarkerOrdinal = expectedMarkerOrdinal;
    }

    internal string ActionName { get; }
    internal EAnimationLogicState AnimationIntent { get; }
    internal string PresentationKey { get; }
    internal string ImpactAudioSemantic { get; }
    internal string ImpactVfxSemantic { get; }
    internal BasicAttackMarkerMode MarkerMode { get; }
    internal int ExpectedMarkerOrdinal { get; }
}

internal readonly struct DRPGBasicAttackPresentationFreezeRequest
{
    internal DRPGBasicAttackPresentationFreezeRequest(
        long battleGeneration,
        ParticipantId actor,
        ParticipantId target,
        string skillId,
        string presentationKey,
        string actionName = "")
    {
        BattleGeneration = battleGeneration;
        Actor = actor;
        Target = target;
        SkillId = skillId ?? string.Empty;
        PresentationKey = presentationKey ?? string.Empty;
        ActionName = actionName ?? string.Empty;
    }

    internal long BattleGeneration { get; }
    internal ParticipantId Actor { get; }
    internal ParticipantId Target { get; }
    internal string SkillId { get; }
    internal string PresentationKey { get; }
    internal string ActionName { get; }
}

internal readonly struct DRPGBasicAttackPresentationFreezeInput
{
    internal DRPGBasicAttackPresentationFreezeInput(
        ActionTracePosition actorHomePosition,
        ActionTracePosition actorConflictPosition,
        ActionTracePosition targetHomePosition,
        ActionTracePosition targetConflictPosition,
        int actorFacingSign,
        int targetFacingSign,
        DRPGBasicAttackVisualBaseline actorVisualBaseline,
        DRPGBasicAttackVisualBaseline targetVisualBaseline,
        DRPGBasicAttackTimingSnapshot timing,
        DRPGBasicAttackSemanticSnapshot semantics)
    {
        ActorHomePosition = actorHomePosition;
        ActorConflictPosition = actorConflictPosition;
        TargetHomePosition = targetHomePosition;
        TargetConflictPosition = targetConflictPosition;
        ActorFacingSign = actorFacingSign;
        TargetFacingSign = targetFacingSign;
        ActorVisualBaseline = actorVisualBaseline;
        TargetVisualBaseline = targetVisualBaseline;
        Timing = timing;
        Semantics = semantics;
    }

    internal ActionTracePosition ActorHomePosition { get; }
    internal ActionTracePosition ActorConflictPosition { get; }
    internal ActionTracePosition TargetHomePosition { get; }
    internal ActionTracePosition TargetConflictPosition { get; }
    internal int ActorFacingSign { get; }
    internal int TargetFacingSign { get; }
    internal DRPGBasicAttackVisualBaseline ActorVisualBaseline { get; }
    internal DRPGBasicAttackVisualBaseline TargetVisualBaseline { get; }
    internal DRPGBasicAttackTimingSnapshot Timing { get; }
    internal DRPGBasicAttackSemanticSnapshot Semantics { get; }
    internal bool IsValid =>
        ActorHomePosition.IsSpecified && ActorConflictPosition.IsSpecified &&
        TargetHomePosition.IsSpecified && TargetConflictPosition.IsSpecified &&
        ActorFacingSign != 0 && TargetFacingSign != 0 &&
        Timing.IsValid &&
        !string.IsNullOrEmpty(Semantics.ActionName) &&
        !string.IsNullOrEmpty(Semantics.PresentationKey) &&
        !string.IsNullOrEmpty(Semantics.ImpactAudioSemantic) &&
        !string.IsNullOrEmpty(Semantics.ImpactVfxSemantic);
}

internal readonly struct DRPGBasicAttackFrozenSnapshot
{
    internal DRPGBasicAttackFrozenSnapshot(
        ActorTurnToken turn,
        TargetSelectionToken selection,
        ParticipantId target,
        DRPGSkillSO skill)
        : this(
            turn,
            selection,
            target,
            skill,
            DRPGBattleSide.Player,
            DRPGBattleSide.Enemy,
            new ActionTracePosition(0f, 0f, 0f),
            new ActionTracePosition(-0.5f, 0f, 0f),
            new ActionTracePosition(0f, 0f, 0f),
            new ActionTracePosition(0.5f, 0f, 0f),
            1,
            -1,
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackTimingSnapshot.ApprovedDefaults,
            CreateSemantics(skill),
            new[] { turn.Actor, target },
            true,
            true,
            true,
            true)
    {
    }

    internal DRPGBasicAttackFrozenSnapshot(
        ActorTurnToken turn,
        TargetSelectionToken selection,
        ParticipantId target,
        DRPGSkillSO skill,
        DRPGBattleSide actorSide,
        ActionTracePosition actorHomePosition,
        ActionTracePosition actorConflictPosition,
        string actorDisplayName = null,
        BasicAttackMarkerMode markerMode = BasicAttackMarkerMode.AuthoredFallback)
        : this(
            turn,
            selection,
            target,
            skill,
            actorSide,
            Opposite(actorSide),
            actorHomePosition,
            actorConflictPosition,
            new ActionTracePosition(0f, 0f, 0f),
            new ActionTracePosition(
                actorSide == DRPGBattleSide.Player ? 0.5f : -0.5f,
                0f,
                0f),
            FacingFor(actorSide),
            FacingFor(Opposite(actorSide)),
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackVisualBaseline.DefaultIdle,
            DRPGBasicAttackTimingSnapshot.ApprovedDefaults,
            CreateSemantics(skill, markerMode),
            new[] { turn.Actor, target },
            true,
            true,
            true,
            true,
            actorDisplayName: actorDisplayName)
    {
    }

    internal DRPGBasicAttackFrozenSnapshot(
        ActorTurnToken turn,
        TargetSelectionToken selection,
        ParticipantId target,
        DRPGSkillSO skill,
        DRPGBattleSide actorSide,
        DRPGBattleSide targetSide,
        ActionTracePosition actorHomePosition,
        ActionTracePosition actorConflictPosition,
        ActionTracePosition targetHomePosition,
        ActionTracePosition targetConflictPosition,
        int actorFacingSign,
        int targetFacingSign,
        DRPGBasicAttackVisualBaseline actorVisualBaseline,
        DRPGBasicAttackVisualBaseline targetVisualBaseline,
        DRPGBasicAttackTimingSnapshot timing,
        DRPGBasicAttackSemanticSnapshot semantics,
        IReadOnlyList<ParticipantId> projectionCoverage,
        bool actorWasAlive,
        bool targetWasAlive,
        bool targetWasDeployed,
        bool targetWasLegal,
        DRPGResolvedActionSpec resolvedSpec = null,
        string actorDisplayName = null)
    {
        if (!turn.IsValid)
            throw new ArgumentException("Turn identity is required.", nameof(turn));
        if (!selection.IsValid)
            throw new ArgumentException("Selection identity is required.", nameof(selection));
        if (target.Value <= 0)
            throw new ArgumentException("Target identity is required.", nameof(target));
        Identity = default;
        Turn = turn;
        Selection = selection;
        Target = target;
        ActorDisplayName = actorDisplayName ?? turn.Actor.ToString();
        ActorSide = actorSide;
        TargetSide = targetSide;
        ActorHomePosition = actorHomePosition;
        ActorConflictPosition = actorConflictPosition;
        TargetHomePosition = targetHomePosition;
        TargetConflictPosition = targetConflictPosition;
        ActorFacingSign = actorFacingSign;
        TargetFacingSign = targetFacingSign;
        ActorVisualBaseline = actorVisualBaseline;
        TargetVisualBaseline = targetVisualBaseline;
        Timing = timing;
        Semantics = semantics;
        ProjectionCoverage = CopyParticipants(projectionCoverage);
        ActorWasAlive = actorWasAlive;
        TargetWasAlive = targetWasAlive;
        TargetWasDeployed = targetWasDeployed;
        TargetWasLegal = targetWasLegal;
        RuleSpecification = resolvedSpec;
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        SkillId = resolvedSpec != null ? resolvedSpec.SkillId : skill.SkillId;
        DisplayName = resolvedSpec != null
            ? resolvedSpec.DisplayName
            : skill.DisplayName;
        Description = skill.Description;
        PresentationKey = resolvedSpec != null
            ? resolvedSpec.PresentationKey
            : skill.PresentationKey;
        SkillPower = resolvedSpec != null
            ? resolvedSpec.ResolvedSkillPower
            : skill.Power;
        PrepareDelay = resolvedSpec != null
            ? resolvedSpec.PrepareDelay
            : skill.PrepareDelay;
        RecoveryDelay = resolvedSpec != null
            ? resolvedSpec.RecoveryDelay
            : skill.RecoveryDelay;
        AnimationIntent = resolvedSpec != null
            ? resolvedSpec.AnimationIntent
            : skill.AnimationIntent;
        SourceFormationRows = resolvedSpec != null
            ? resolvedSpec.SourceFormationRows
            : skill.SourceFormationRows;
        TargetFormationRows = resolvedSpec != null
            ? resolvedSpec.TargetFormationRows
            : skill.TargetFormationRows;
        Purpose = skill.Purpose;
        TargetRule = resolvedSpec != null
            ? resolvedSpec.TargetRule
            : skill.TargetRule;
        ExecutionMode = resolvedSpec != null
            ? resolvedSpec.ExecutionMode
            : skill.ExecutionMode;
        ImpactDamageMultipliers = Copy(skill.ImpactDamageMultipliers);
        EnemyWarningSteps = skill.EnemyWarningSteps;
        EnemyTargetDisclosureMode = skill.EnemyTargetDisclosureMode;
        PostureBreakInterruptionPolicy = resolvedSpec != null
            ? resolvedSpec.PostureBreakInterruptionPolicy
            : skill.PostureBreakInterruptionPolicy;
        IgnorePostureBreakProtection = skill.IgnorePostureBreakProtection;
        RequiresFunctionalWeapon = skill.RequiresFunctionalWeapon;
        ManaCost = resolvedSpec != null ? resolvedSpec.ManaCost : skill.ManaCost;
        StaminaCost = resolvedSpec != null
            ? resolvedSpec.StaminaCost
            : skill.StaminaCost;
        PostureCost = resolvedSpec != null
            ? resolvedSpec.PostureCost
            : skill.PostureCost;
        ActorDamageMultiplier = resolvedSpec != null
            ? resolvedSpec.ActorDamageMultiplier
            : 1f;
        ActorHealingMultiplier = resolvedSpec != null
            ? resolvedSpec.ActorHealingMultiplier
            : 1f;
        ActionProgressCost = resolvedSpec != null
            ? resolvedSpec.ActionProgressCost
            : skill.ActionProgressCost != null
                ? skill.ActionProgressCost.Freeze()
                : DRPGResolvedProgressCost.None;
        InputProgressCost = resolvedSpec != null
            ? resolvedSpec.InputProgressCost
            : skill.InputProgressCost != null
                ? skill.InputProgressCost.Freeze()
                : DRPGResolvedProgressCost.None;
        SelfPostureDepletionPolicy = resolvedSpec != null
            ? resolvedSpec.SelfPostureDepletionPolicy
            : skill.SelfPostureDepletionPolicy;
        Effects = resolvedSpec != null
            ? CopyEffects(resolvedSpec.Effects)
            : CopyEffects(skill.Effects, SkillPower);
        ContributionTrace = resolvedSpec != null
            ? CopyContributions(resolvedSpec.ContributionTrace)
            : Array.Empty<DRPGBasicAttackContributionSnapshot>();
    }

    private DRPGBasicAttackFrozenSnapshot(
        in DRPGBasicAttackFrozenSnapshot source,
        ActionIdentity identity)
    {
        Identity = identity;
        Turn = source.Turn;
        Selection = source.Selection;
        Target = source.Target;
        ActorDisplayName = source.ActorDisplayName;
        ActorSide = source.ActorSide;
        TargetSide = source.TargetSide;
        ActorHomePosition = source.ActorHomePosition;
        ActorConflictPosition = source.ActorConflictPosition;
        TargetHomePosition = source.TargetHomePosition;
        TargetConflictPosition = source.TargetConflictPosition;
        ActorFacingSign = source.ActorFacingSign;
        TargetFacingSign = source.TargetFacingSign;
        ActorVisualBaseline = source.ActorVisualBaseline;
        TargetVisualBaseline = source.TargetVisualBaseline;
        Timing = source.Timing;
        Semantics = source.Semantics;
        ProjectionCoverage = source.ProjectionCoverage;
        ActorWasAlive = source.ActorWasAlive;
        TargetWasAlive = source.TargetWasAlive;
        TargetWasDeployed = source.TargetWasDeployed;
        TargetWasLegal = source.TargetWasLegal;
        RuleSpecification = source.RuleSpecification;
        SkillId = source.SkillId;
        DisplayName = source.DisplayName;
        Description = source.Description;
        PresentationKey = source.PresentationKey;
        SkillPower = source.SkillPower;
        PrepareDelay = source.PrepareDelay;
        RecoveryDelay = source.RecoveryDelay;
        AnimationIntent = source.AnimationIntent;
        SourceFormationRows = source.SourceFormationRows;
        TargetFormationRows = source.TargetFormationRows;
        Purpose = source.Purpose;
        TargetRule = source.TargetRule;
        ExecutionMode = source.ExecutionMode;
        ImpactDamageMultipliers = source.ImpactDamageMultipliers;
        EnemyWarningSteps = source.EnemyWarningSteps;
        EnemyTargetDisclosureMode = source.EnemyTargetDisclosureMode;
        PostureBreakInterruptionPolicy = source.PostureBreakInterruptionPolicy;
        IgnorePostureBreakProtection = source.IgnorePostureBreakProtection;
        RequiresFunctionalWeapon = source.RequiresFunctionalWeapon;
        ManaCost = source.ManaCost;
        StaminaCost = source.StaminaCost;
        PostureCost = source.PostureCost;
        ActorDamageMultiplier = source.ActorDamageMultiplier;
        ActorHealingMultiplier = source.ActorHealingMultiplier;
        ActionProgressCost = source.ActionProgressCost;
        InputProgressCost = source.InputProgressCost;
        SelfPostureDepletionPolicy = source.SelfPostureDepletionPolicy;
        Effects = source.Effects;
        ContributionTrace = source.ContributionTrace;
    }

    internal DRPGBasicAttackFrozenSnapshot WithIdentity(ActionIdentity identity)
    {
        if (!identity.IsValid ||
            identity.BattleGeneration != Turn.BattleGeneration ||
            identity.TurnSerial != Turn.TurnSerial)
        {
            throw new ArgumentException(
                "The frozen snapshot identity must match its actor turn.",
                nameof(identity));
        }
        return new DRPGBasicAttackFrozenSnapshot(this, identity);
    }

    internal ActionIdentity Identity { get; }
    internal ActorTurnToken Turn { get; }
    internal TargetSelectionToken Selection { get; }
    internal ParticipantId Target { get; }
    internal string ActorDisplayName { get; }
    internal DRPGBattleSide ActorSide { get; }
    internal DRPGBattleSide TargetSide { get; }
    internal ActionTracePosition ActorHomePosition { get; }
    internal ActionTracePosition ActorConflictPosition { get; }
    internal ActionTracePosition TargetHomePosition { get; }
    internal ActionTracePosition TargetConflictPosition { get; }
    internal int ActorFacingSign { get; }
    internal int TargetFacingSign { get; }
    internal DRPGBasicAttackVisualBaseline ActorVisualBaseline { get; }
    internal DRPGBasicAttackVisualBaseline TargetVisualBaseline { get; }
    internal DRPGBasicAttackTimingSnapshot Timing { get; }
    internal DRPGBasicAttackSemanticSnapshot Semantics { get; }
    internal IReadOnlyList<ParticipantId> ProjectionCoverage { get; }
    internal bool ActorWasAlive { get; }
    internal bool TargetWasAlive { get; }
    internal bool TargetWasDeployed { get; }
    internal bool TargetWasLegal { get; }
    // Already resolved immutable values, never the mutable skill asset. Kept
    // with the lock snapshot so the root cannot re-resolve costs at impact.
    internal DRPGResolvedActionSpec RuleSpecification { get; }
    internal string SkillId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal string PresentationKey { get; }
    internal float SkillPower { get; }
    internal float PrepareDelay { get; }
    internal float RecoveryDelay { get; }
    internal EAnimationLogicState AnimationIntent { get; }
    internal DRPGFormationRowMask SourceFormationRows { get; }
    internal DRPGFormationRowMask TargetFormationRows { get; }
    internal DRPGSkillPurpose Purpose { get; }
    internal DRPGSkillTargetRule TargetRule { get; }
    internal DRPGSkillExecutionMode ExecutionMode { get; }
    internal IReadOnlyList<float> ImpactDamageMultipliers { get; }
    internal int EnemyWarningSteps { get; }
    internal DRPGEnemyTargetDisclosureMode EnemyTargetDisclosureMode { get; }
    internal DRPGPostureBreakInterruptionPolicy PostureBreakInterruptionPolicy { get; }
    internal bool IgnorePostureBreakProtection { get; }
    internal bool RequiresFunctionalWeapon { get; }
    internal float ManaCost { get; }
    internal int StaminaCost { get; }
    internal float PostureCost { get; }
    internal float ActorDamageMultiplier { get; }
    internal float ActorHealingMultiplier { get; }
    internal DRPGResolvedProgressCost ActionProgressCost { get; }
    internal DRPGResolvedProgressCost InputProgressCost { get; }
    internal DRPGSelfPostureDepletionPolicy SelfPostureDepletionPolicy { get; }
    internal IReadOnlyList<DRPGBasicAttackEffectSnapshot> Effects { get; }
    internal IReadOnlyList<DRPGBasicAttackContributionSnapshot>
        ContributionTrace { get; }

    private static IReadOnlyList<float> Copy(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<float>();
        var copy = new float[values.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = values[i];
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<DRPGBasicAttackEffectSnapshot> CopyEffects(
        IReadOnlyList<DRPGActionEffectDefinition> effects, float skillPower)
    {
        if (effects == null || effects.Count == 0)
            return Array.Empty<DRPGBasicAttackEffectSnapshot>();
        var copy = new DRPGBasicAttackEffectSnapshot[effects.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = new DRPGBasicAttackEffectSnapshot(effects[i], skillPower);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<DRPGBasicAttackEffectSnapshot> CopyEffects(
        IReadOnlyList<DRPGResolvedActionEffect> effects)
    {
        if (effects == null || effects.Count == 0)
            return Array.Empty<DRPGBasicAttackEffectSnapshot>();
        var copy = new DRPGBasicAttackEffectSnapshot[effects.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = new DRPGBasicAttackEffectSnapshot(effects[i]);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<DRPGBasicAttackContributionSnapshot>
        CopyContributions(IReadOnlyList<DRPGActionSpecContributionTrace> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<DRPGBasicAttackContributionSnapshot>();
        var copy = new DRPGBasicAttackContributionSnapshot[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = new DRPGBasicAttackContributionSnapshot(source[i]);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<ParticipantId> CopyParticipants(
        IReadOnlyList<ParticipantId> participants)
    {
        if (participants == null || participants.Count == 0)
            return Array.Empty<ParticipantId>();
        var copy = new ParticipantId[participants.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = participants[i];
        return Array.AsReadOnly(copy);
    }

    private static DRPGBasicAttackSemanticSnapshot CreateSemantics(
        DRPGSkillSO skill,
        BasicAttackMarkerMode markerMode = BasicAttackMarkerMode.AuthoredFallback)
    {
        if (skill == null)
            return default;
        string presentationKey = skill.PresentationKey;
        return new DRPGBasicAttackSemanticSnapshot(
            skill.DisplayName,
            skill.AnimationIntent,
            presentationKey,
            presentationKey + ".impact.audio",
            presentationKey + ".impact.vfx",
            markerMode);
    }

    private static DRPGBattleSide Opposite(DRPGBattleSide side)
    {
        return side == DRPGBattleSide.Player
            ? DRPGBattleSide.Enemy
            : DRPGBattleSide.Player;
    }

    private static int FacingFor(DRPGBattleSide side)
    {
        return side == DRPGBattleSide.Player ? 1 : -1;
    }
}

internal interface IDRPGStandardBasicAttackRunner
{
    BasicAttackRejection CheckAvailability(long battleGeneration);
    RootActionStartResult TryStart(
        in DRPGBasicAttackFrozenSnapshot snapshot,
        ActionIdentity identity,
        BattleRuleMutationLease lease,
        string actionLockedEventId);
}

internal sealed class DRPGStandardBasicAttackTransactionModule :
    IDRPGStandardBasicAttackRunner, IDisposable
{
    private readonly DRPGBattleTransactionPresentationPortSlot presentationSlot;
    private RootActionReceipt activeReceipt;
    private BattleRuleMutationLease activeLease;
    private IStandardBasicAttackProjectionLifetime projectionScope;
    private DRPGStandardBasicAttackRuleKernel rules;
    private DRPGPreparedBasicAttackRuleAction prepared;
    private DRPGBasicAttackImpactSequence impact;
    private DRPGBasicAttackReturnSequence returning;
    private DRPGBasicAttackSettlementFlow settling;
    private IStandardBasicAttackPresentationPort activePort;
    private IDRPGBattlePresentationPump activePump;
    private DRPGBasicAttackFrozenSnapshot frozen;
    private bool ticking, cleaning;
    private bool faultStarted;
    private bool retainFaultLease;
    private BattleRuleMutationLease faultLease;
    private IStandardBasicAttackProjectionLifetime faultScope;
    private PresentationCleanupReceipt faultCleanup;
    private double faultCleanupRequestedAt;
    private int faultCleanupAttempts;
    private string faultCleanupReason;
    internal bool FaultCleanupComplete { get; private set; }
    internal string FaultCleanupError { get; private set; } = string.Empty;
    private bool completionSubmitted;
    private RootActionCompletion sealedCompletion;
    private IBattleContinuationPort completionDestination;
    internal bool CompletionSubmitted => completionSubmitted;
    internal RootActionCompletion SealedCompletion => sealedCompletion;
    internal DRPGBattlePreparedAction CreateCompletionAction(DRPGBattleCommand command) => prepared?.CreateCompletionAction(command);
    internal DRPGBasicAttackPrelude Prelude { get; private set; }
    internal DRPGBattleActionResolutionLedger ResolutionLedger { get; private set; }
    internal bool ReadyForSettlement => returning != null && returning.ReadyForSettlement;
    internal DRPGBattleSettlementLedger SettlementLedger => settling?.Ledger;
    internal bool ReadyForFinalization => settling != null && settling.ReadyForFinalization;

    internal void BindRules(DRPGStandardBasicAttackRuleKernel kernel)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        if (rules != null || activeLease != null) throw new InvalidOperationException("Rule binding is immutable for this root module.");
        rules = kernel;
    }

    internal DRPGStandardBasicAttackTransactionModule(
        DRPGBattleTransactionPresentationPortSlot presentationSlot)
    {
        this.presentationSlot = presentationSlot ??
            throw new ArgumentNullException(nameof(presentationSlot));
    }

    public BasicAttackRejection CheckAvailability(long battleGeneration)
    {
        return presentationSlot.TryGet(battleGeneration, out _)
            ? BasicAttackRejection.None
            : BasicAttackRejection.PresentationUnavailable;
    }

    public RootActionStartResult TryStart(
        in DRPGBasicAttackFrozenSnapshot snapshot,
        ActionIdentity identity,
        BattleRuleMutationLease lease,
        string actionLockedEventId)
    {
        if (completionSubmitted && activeLease != null && !activeLease.IsActive) ReleaseTransferredResources();
        if (activeLease != null || ticking || cleaning || faultStarted)
            return RootActionStartResult.Rejected(BasicAttackRejection.Busy);
        if (!identity.IsValid || snapshot.Identity != identity || lease == null ||
            !lease.Arbiter.Owns(lease, BattleTransactionIdentity.ForRootAction(identity)))
            return RootActionStartResult.Rejected(BasicAttackRejection.StaleSelection);
        var receipt = new RootActionReceipt(
            identity,
            lease.Arbiter.Trace,
            actionLockedEventId);
        activeReceipt = receipt; activeLease = lease; frozen = snapshot;
        activePort = null; activePump = null;
        if (CheckAvailability(identity.BattleGeneration) !=
            BasicAttackRejection.None)
        {
            Fail("Presentation became unavailable after action lock.");
            return RootActionStartResult.Accepted(receipt);
        }

        presentationSlot.TryGet(identity.BattleGeneration, out var port);
        if (!(port is IDRPGBattlePresentationPump pump) || pump.Clock == null)
        {
            Fail("Presentation clock is unavailable after action lock.");
            return RootActionStartResult.Accepted(receipt);
        }
        activePort = port; activePump = pump;
        ResolutionLedger = null; impact = null; returning = null; settling = null;
        completionSubmitted = false; sealedCompletion = default; completionDestination = null;
        try
        {
            if (rules != null)
            {
                if (!(port is IStandardBasicAttackReturnPort))
                    throw new InvalidOperationException("Standard return port is unavailable.");
                if (!(port is IStandardBasicAttackSettlementPort))
                    throw new InvalidOperationException("Standard settlement port is unavailable.");
                if (!rules.TryPrepare(snapshot, snapshot.RuleSpecification, lease, out prepared, out var error))
                    throw new InvalidOperationException(error);
            }
            // Announcement is issued by the Prelude constructor. Pin the entire
            // battlefield first so ordinary event refreshes cannot race it.
            var opened = (port as IStandardBasicAttackProjectionScope)?.OpenProjection(snapshot);
            if (!OwnsRoot()) { opened?.Dispose(); return RootActionStartResult.Accepted(receipt); }
            projectionScope = opened;
            if (rules != null && (opened == null || !opened.IsActive))
                throw new InvalidOperationException("Standard root requires the full-battlefield projection lease before announcement.");
            Prelude = new DRPGBasicAttackPrelude(snapshot, port, pump.Clock, lease, actionLockedEventId);
        }
        catch (Exception exception) { Fail(exception.Message); }
        if (Prelude != null && !string.IsNullOrEmpty(Prelude.Fault)) Fail(Prelude.Fault);
        return RootActionStartResult.Accepted(receipt);
    }

    internal void Tick()
    {
        if (faultStarted) { FinishFaultCleanup(); return; }
        if (ticking || completionSubmitted || Prelude == null || activeLease == null) return;
        ticking = true;
        try
        {
            if (!OwnsRoot()) { Fail("Root/projection ownership lost."); return; }
            if (settling != null)
            {
                settling.Tick();
                if (!OwnsRoot()) { Fail("Root/projection ownership lost during settlement."); return; }
                if (settling.Fault.Length != 0) Fail(settling.Fault);
                return;
            }
            if (returning != null)
            {
                returning.Tick();
                if (!OwnsRoot()) { Fail("Root/projection ownership lost during return."); return; }
                if (returning.Fault.Length != 0) { Fail(returning.Fault); return; }
                if (returning.ReadyForSettlement)
                {
                    var next = new DRPGBasicAttackSettlementFlow(prepared, activePort, activePump.Clock, activeLease, OwnsRoot);
                    if (!OwnsRoot()) { next.Dispose(); Fail("Root lost ownership while starting settlement."); return; }
                    settling = next;
                    if (settling.Fault.Length != 0) Fail(settling.Fault);
                }
                // Final publication/continuation remains a separate Task11 gate.
                return;
            }
            if (impact != null)
            {
                impact.Tick();
                if (!OwnsRoot()) { Fail("Root/projection ownership lost during impact."); return; }
                if (impact.Fault.Length != 0) { Fail(impact.Fault); return; }
                if (impact.ReadyForReturn)
                    returning = new DRPGBasicAttackReturnSequence(frozen, prepared, activePort, activePump.Clock, activeLease);
                if (returning != null && returning.Fault.Length != 0) Fail(returning.Fault);
                return;
            }
            Prelude.Tick();
            if (!OwnsRoot()) { Fail("Root/projection ownership lost during prelude."); return; }
            if (!string.IsNullOrEmpty(Prelude.Fault)) { Fail(Prelude.Fault); return; }
            if (!Prelude.ReadyForImpact || prepared == null) return;
            var result = prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
            // The immutable event publisher can synchronously dispose this root.
            if (!OwnsRoot()) { Fail("Root disposed during committed notification."); return; }
            if (result.Receipt == null) { Fail(result.Error); return; }
            ResolutionLedger = result.Receipt.Ledger;
            impact = new DRPGBasicAttackImpactSequence(ResolutionLedger, activePort, activePump.Clock, activeLease);
            if (!OwnsRoot()) { Fail("Root disposed while issuing impact."); return; }
            if (impact.Fault.Length != 0) Fail(impact.Fault);
        }
        catch (Exception exception) { Fail(exception.Message); }
        finally { ticking = false; }
    }

    private bool OwnsRoot() => activeLease != null &&
        activeLease.Arbiter.Owns(activeLease, BattleTransactionIdentity.ForRootAction(frozen.Identity)) &&
        presentationSlot.TryGet(frozen.Identity.BattleGeneration, out var boundPort) && ReferenceEquals(boundPort, activePort) &&
        (projectionScope == null || projectionScope.IsActive);

    internal bool TryYieldCompletion(IBattleContinuationPort destination, out string error)
    {
        error = string.Empty;
        if (completionSubmitted)
        {
            if (ReferenceEquals(destination, completionDestination)) return true;
            error = "A sealed completion cannot be sent to a different inbox."; return false;
        }
        if (ticking || cleaning || destination == null || !ReadyForFinalization || prepared == null || !OwnsRoot())
        { error = "Root has not finished its final settlement barrier."; return false; }
        ticking = true;
        try
        {
            var terminal = new RootActionTerminal(Prelude.Degraded || impact.Degraded || settling.Degraded
                ? RootActionTerminalKind.DegradedCompleted : RootActionTerminalKind.Completed, "All Root presentation barriers completed.");
            if (!prepared.TryDescribeCompletion(terminal, out var value, out error)) { Fail(error); return false; }
            // The synchronous canonical close may reenter/cancel this module.
            // Retain the physical rule lease throughout and validate again.
            var closing = projectionScope; projectionScope = null;
            closing?.Dispose();
            if (closing != null && closing.IsActive) { error = "Canonical projection did not close."; Fail(error); return false; }
            if (!OwnsRoot() || prepared == null || !prepared.TryDescribeCompletion(terminal, out var after, out error) || !after.HasSameValues(value))
            { error = "Root changed during canonical projection close. " + error; Fail(error); return false; }
            var submitted = destination.Submit(value);
            if (!submitted.IsAccepted) { error = submitted.Error; Fail(error); return false; }
            if (!OwnsRoot()) { error = "Root lost ownership during completion submission."; Fail(error); return false; }
            if (!activeReceipt.TryComplete(frozen.Identity, terminal, value.Fingerprint.ToString("X16"), out error))
            { Fail(error); return false; }
            sealedCompletion = value; completionDestination = destination; completionSubmitted = true;
            return true;
        }
        catch (Exception exception) { error = exception.Message; Fail(error); return false; }
        finally { ticking = false; }
    }

    internal void CloseForContinuationFault(BattleRuleMutationLease originalLease, string reason)
    {
        if (!completionSubmitted || !ReferenceEquals(sealedCompletion.Lease, originalLease) ||
            originalLease == null || !originalLease.Arbiter.Owns(originalLease, originalLease.Owner))
            throw new InvalidOperationException("Only the sealed completion's original owner may close a failed continuation.");
        // Root completion is immutable. A later continuation fault must clean
        // its remaining leaves without rewriting that receipt or releasing the
        // rule/input owner before Session has sealed the technical terminal.
        retainFaultLease = true;
        Fail(reason);
    }

    private void Fail(string reason)
    {
        if (cleaning || faultStarted) return;
        cleaning = true;
        faultStarted = true;
        // Clear ownership before any Unity callback can re-enter cleanup/Tick.
        var closingLease = activeLease;
        activeLease = null;
        var closingPrelude = Prelude; Prelude = null;
        var closingImpact = impact; impact = null;
        var closingReturn = returning; returning = null;
        var closingSettlement = settling; settling = null;
        var closingPrepared = prepared; prepared = null;
        var closingScope = projectionScope; projectionScope = null;
        var closingReceipt = activeReceipt;
        faultLease = closingLease;
        faultScope = closingScope;
        if (closingLease != null)
            closingReceipt.TryComplete(closingLease.Owner.Action,
                new RootActionTerminal(RootActionTerminalKind.Faulted, reason),
                "standard-basic-attack-root-fault", out _);
        CloseOwned(closingPrelude, ref reason);
        CloseOwned(closingImpact, ref reason);
        CloseOwned(closingReturn, ref reason);
        CloseOwned(closingSettlement, ref reason);
        CloseOwned(closingPrepared, ref reason);
        try
        {
            // Leaf disposal requests cancellation; only the presentation's
            // acknowledgement authorizes releasing projection and rule leases.
            faultCleanupRequestedAt = activePump != null ? activePump.Clock.Now : 0;
            faultCleanupAttempts = 1;
            faultCleanupReason = reason;
            if (closingPrelude != null && activePort != null && closingLease != null)
                faultCleanup = activePort.EmergencyCleanup(new BattlePresentationCleanupCommand(closingLease.Owner, reason));
            else
            { faultCleanup = new PresentationCleanupReceipt(); faultCleanup.TryComplete(true); }
            if (faultCleanup == null) FaultCleanupError = "Missing fault cleanup acknowledgement.";
        }
        catch (Exception exception) { FaultCleanupError = exception.ToString(); }
        finally { cleaning = false; }
        FinishFaultCleanup();
    }
    private void FinishFaultCleanup()
    {
        if (cleaning || FaultCleanupComplete) return;
        if ((faultCleanup == null || !faultCleanup.Completion.IsCompleted || !faultCleanup.Completion.Result) &&
            activePump != null && activePort != null && faultLease != null && faultCleanupAttempts < 3 &&
            activePump.Clock.Now - faultCleanupRequestedAt >= 5d && faultLease.Arbiter.Owns(faultLease, faultLease.Owner))
        {
            // Retry only cleanup acknowledgement under the original owner;
            // never retry the action, its journal, or a continuation. Exhaustion
            // remains fail-closed rather than inventing successful cleanup.
            cleaning = true;
            faultCleanupRequestedAt = activePump.Clock.Now;
            faultCleanupAttempts++;
            try { faultCleanup = activePort.EmergencyCleanup(new BattlePresentationCleanupCommand(faultLease.Owner, faultCleanupReason)); }
            catch (Exception error) { FaultCleanupError = error.ToString(); }
            finally { cleaning = false; }
        }
        if (faultCleanup == null || !faultCleanup.Completion.IsCompleted) return;
        if (!faultCleanup.Completion.Result) { FaultCleanupError = "Presentation rejected fault cleanup."; return; }
        cleaning = true;
        try
        {
            // Dispose is idempotent, including reentrant canonical observers.
            // A throwing close must be retried/verified, never treated as proof.
            faultScope?.Dispose();
            if (faultScope != null && faultScope.IsActive)
            { FaultCleanupError = "Fault projection did not close."; return; }
            faultScope = null;
            if (!retainFaultLease) { faultLease?.Dispose(); faultLease = null; }
            FaultCleanupComplete = true;
        }
        catch (Exception exception) { FaultCleanupError = exception.ToString(); }
        finally { cleaning = false; }
    }
    private void ReleaseTransferredResources()
    {
        // This Root has already yielded; do not manufacture a fault terminal or
        // dispose the next transaction's lease when clearing reusable leaves.
        activeLease = null;
        Prelude?.Dispose(); impact?.Dispose(); returning?.Dispose(); settling?.Dispose(); prepared?.Dispose();
        Prelude = null; impact = null; returning = null; settling = null; prepared = null;
    }
    private static void CloseOwned(IDisposable resource, ref string reason)
    {
        try { resource?.Dispose(); }
        catch (Exception exception) { reason += " | Cleanup: " + exception.Message; }
    }
    public void Dispose()
    {
        if (completionSubmitted && (activeLease == null || !activeLease.IsActive)) { ReleaseTransferredResources(); return; }
        Fail("Standard Basic Attack module disposed before finalization.");
    }
}
