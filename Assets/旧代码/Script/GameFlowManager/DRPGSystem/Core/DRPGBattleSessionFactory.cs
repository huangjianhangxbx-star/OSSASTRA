using System;
using System.Collections.Generic;

public enum DRPGBattleSessionCreationError
{
    None = 0,
    MissingPartySetup = 1,
    MissingEncounter = 2,
    MissingProtagonist = 3,
    TooManyPartyMembers = 4,
    InvalidPartyMember = 5,
    MissingEnemies = 6,
    InvalidEnemy = 7,
    TooManyBattlefieldMembers = 8,
    MissingBattlefieldMembers = 9
}

public sealed class DRPGBattleSessionCreationResult
{
    private static readonly IReadOnlyList<BattleParticipantRuntime> EmptyParticipants =
        Array.Empty<BattleParticipantRuntime>();

    private DRPGBattleSessionCreationResult(
        IReadOnlyList<BattleParticipantRuntime> participants,
        IReadOnlyList<BattleParticipantRuntime> reserveEnemies,
        DRPGFormationRules formationRules,
        DRPGBattleSessionCreationError error,
        string errorMessage)
    {
        Participants = participants ?? EmptyParticipants;
        ReserveEnemies = reserveEnemies ?? EmptyParticipants;
        FormationRules = formationRules ?? DRPGFormationRules.Default;
        Error = error;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public bool IsSuccess => Error == DRPGBattleSessionCreationError.None;
    public IReadOnlyList<BattleParticipantRuntime> Participants { get; }
    public IReadOnlyList<BattleParticipantRuntime> ReserveEnemies { get; }
    public DRPGFormationRules FormationRules { get; }
    public DRPGBattleSessionCreationError Error { get; }
    public string ErrorMessage { get; }

    public static DRPGBattleSessionCreationResult Success(
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        return new DRPGBattleSessionCreationResult(
            participants,
            null,
            DRPGFormationRules.Default,
            DRPGBattleSessionCreationError.None,
            string.Empty);
    }

    public static DRPGBattleSessionCreationResult Success(
        IReadOnlyList<BattleParticipantRuntime> participants,
        IReadOnlyList<BattleParticipantRuntime> reserveEnemies,
        DRPGFormationRules formationRules)
    {
        return new DRPGBattleSessionCreationResult(
            participants,
            reserveEnemies,
            formationRules,
            DRPGBattleSessionCreationError.None,
            string.Empty);
    }

    public static DRPGBattleSessionCreationResult Failure(
        DRPGBattleSessionCreationError error,
        string errorMessage)
    {
        return new DRPGBattleSessionCreationResult(
            null,
            null,
            DRPGFormationRules.Default,
            error,
            errorMessage);
    }
}

public static class DRPGBattleSessionFactory
{
    internal static DRPGBattleSession CreateConfiguredSession(DRPGBattleSessionCreationResult roster,
        DRPGBattleStartRequest request, DRPGBattleTransactionPolicy policy,
        IDRPGBattleItemSource itemSource = null, IDRPGBattleEquipmentService equipmentService = null,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService = null, IDRPGBattleResonanceAccess resonanceAccess = null)
    {
        if (request == null || request.Encounter == null) throw new ArgumentException("Configured battle requires an encounter request.");
        ValidateAcceptanceRequest(request, policy);
        if (policy.StandardBasicAttackEnabled)
            return CreateWaveSession(roster, request.BattleSeed, request.Encounter.EncounterId,
                request.Encounter.FreezeWavePolicy(.75, .3), itemSource, request.Encounter.EscapeRules,
                equipmentService, weaponDurabilityService, resonanceAccess, policy, request.AcceptanceOptions);
        return CreateSession(roster, itemSource, request.Encounter.EscapeRules, equipmentService,
            weaponDurabilityService, resonanceAccess: resonanceAccess);
    }
    internal static void ValidateAcceptanceRequest(DRPGBattleStartRequest request, DRPGBattleTransactionPolicy policy)
    {
        if (!request.AcceptanceOptions.Enabled) return;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        throw new ArgumentException("BasicAttackOnly is restricted to development builds.");
#else
        if (!policy.StandardBasicAttackEnabled || request.AcceptanceOptions.BattleSeed != request.BattleSeed)
            throw new ArgumentException("BasicAttackOnly requires Standard policy and a matching fixed seed.");
#endif
    }
    internal static DRPGBattleSession CreateWaveSession(DRPGBattleSessionCreationResult roster, int seed,
        string encounterId, DRPGEnemyWavePolicySnapshot policy, IDRPGBattleItemSource itemSource = null,
        DRPGBattleEscapeRules? escapeRules = null, IDRPGBattleEquipmentService equipmentService = null,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService = null, IDRPGBattleResonanceAccess resonanceAccess = null,
        DRPGBattleTransactionPolicy? transactionPolicy = null,
        BasicAttackOnlyAcceptanceOptions acceptanceOptions = default)
    {
        if (roster == null || !roster.IsSuccess) throw new ArgumentException("A successful roster is required.");
        var party = new List<BattleParticipantRuntime>(); var enemies = new List<BattleParticipantRuntime>();
        foreach (var actor in roster.Participants) (actor.Side == DRPGBattleSide.Enemy ? enemies : party).Add(actor);
        enemies.AddRange(roster.ReserveEnemies);
        var specs = new List<BattleParticipantSpec>(); foreach (var actor in enemies) specs.Add(actor.Spec);
        var plan = DRPGEnemyWavePlan.Freeze(seed, encounterId, specs, policy.MinWaveSize, policy.MaxWaveSize, roster.FormationRules.PerRowLimit * 3);
        var all = new List<BattleParticipantRuntime>(party); all.AddRange(enemies);
        var progress = CreateConfiguredProgressPipeline(all);
        var composition = DRPGBattleSessionComposition.Build(party, enemies, null, DRPGProcessBattleGenerationSource.Instance,
            transactionPolicy ?? new DRPGBattleTransactionPolicy(true), progress, weaponDurabilityService, resonanceAccess, wavePlan: plan, wavePolicy: policy);
        return new DRPGBattleSession(composition, null, null, itemSource, escapeRules, roster.FormationRules,
            equipmentService, weaponDurabilityService, null, null, progress, resonanceAccess, acceptanceOptions);
    }
    public const int MaximumBattlefieldMembers = 5;
    public const int MaximumPartyMembers = MaximumBattlefieldMembers;

    // Explicit integration opt-in. Public CreateSession/F8 remain Legacy until
    // waves, fault cleanup and authored incapacitated bindings are accepted.
    internal static DRPGBattleSession CreateStandardSession(DRPGBattleSessionCreationResult roster)
    {
        if (roster == null || !roster.IsSuccess) throw new ArgumentException("A successful roster is required.", nameof(roster));
        var progress = CreateConfiguredProgressPipeline(roster.Participants);
        var composition = DRPGBattleSessionComposition.Build(roster.Participants, roster.ReserveEnemies, null,
            DRPGProcessBattleGenerationSource.Instance, DRPGBattleTransactionPolicy.StandardBasicAttackTest, progress);
        return new DRPGBattleSession(composition, null, null, null, null, roster.FormationRules,
            null, null, null, null, progress, null);
    }

    public static DRPGBattleSession CreateSession(
        DRPGBattleSessionCreationResult roster,
        IDRPGBattleItemSource itemSource = null,
        DRPGBattleEscapeRules? escapeRules = null,
        IDRPGBattleEquipmentService equipmentService = null,
        IDRPGBattleWeaponDurabilityService weaponDurabilityService = null,
        DRPGBattleProgressPipeline progressPipeline = null,
        IDRPGBattleResonanceAccess resonanceAccess = null)
    {
        if (roster == null || !roster.IsSuccess)
        {
            throw new ArgumentException(
                "A successful roster result is required.",
                nameof(roster));
        }
        DRPGBattleProgressPipeline resolvedProgress = progressPipeline ??
            CreateConfiguredProgressPipeline(roster.Participants);
        return new DRPGBattleSession(
            roster.Participants,
            itemSource: itemSource,
            escapeRules: escapeRules,
            reserveEnemies: roster.ReserveEnemies,
            formationRules: roster.FormationRules,
            equipmentService: equipmentService,
            weaponDurabilityService: weaponDurabilityService,
            progressPipeline: resolvedProgress,
            resonanceAccess: resonanceAccess);
    }

    public static DRPGBattleProgressPipeline CreateConfiguredProgressPipeline(
        IReadOnlyList<BattleParticipantRuntime> participants)
    {
        var resources = new Dictionary<string, DRPGProgressResourceSO>(
            StringComparer.Ordinal);
        var bindings = new List<DRPGConfiguredProgressRuleBinding>();
        var bindingKeys = new HashSet<string>(StringComparer.Ordinal);

        int count = participants != null ? participants.Count : 0;
        for (int i = 0; i < count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (participant == null || participant.Build == null)
                continue;
            CollectSkillProgress(
                participant.Build.BasicAttack,
                resources,
                bindings,
                bindingKeys);
            IReadOnlyList<DRPGSkillSO> skills = participant.Build.BattleSkills;
            for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
            {
                CollectSkillProgress(
                    skills[skillIndex],
                    resources,
                    bindings,
                    bindingKeys);
            }
        }

        IDRPGBattleProgressSource[] sources = bindings.Count > 0
            ? new IDRPGBattleProgressSource[]
            {
                new DRPGConfiguredProgressSource(
                    "progress.configured",
                    bindings)
            }
            : Array.Empty<IDRPGBattleProgressSource>();
        var pipeline = new DRPGBattleProgressPipeline(sources);
        foreach (DRPGProgressResourceSO resource in resources.Values)
        {
            resource.ValidateOrThrow();
            for (int i = 0; i < count; i++)
            {
                BattleParticipantRuntime participant = participants[i];
                if (participant == null || participant.Profile == null)
                    continue;
                float capacityModifier = ResolveCapacityModifier(
                    participant.Build != null
                        ? participant.Build.Equipment
                        : null,
                    resource.ResourceId);
                if (!pipeline.TryRegister(
                        participant.Profile.CombatantId,
                        resource.CreateDefinition(capacityModifier),
                        out _,
                        out string error))
                {
                    throw new InvalidOperationException(error);
                }
            }
        }
        return pipeline;
    }

    public static DRPGBattleSessionCreationResult CreateRoster(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter)
    {
        return CreateRoster(party, encounter, null);
    }

    public static DRPGBattleSessionCreationResult CreateRoster(
        DRPGPartySetupSO party,
        DRPGEncounterDefinitionSO encounter,
        DRPGProgressionService progressionService)
    {
        if (party == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingPartySetup,
                "A DRPG party setup is required.");
        }

        if (encounter == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingEncounter,
                "A DRPG encounter definition is required.");
        }

        if (party.Protagonist == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingProtagonist,
                "The required protagonist profile is missing.");
        }

        DRPGPartyRosterSnapshot snapshot;
        try
        {
            snapshot = DRPGPartyCompositionState.FromSetup(party).CreateSnapshot();
        }
        catch (ArgumentException exception)
        {
            if (party.PartyMembers != null &&
                party.PartyMembers.Count + 1 > MaximumBattlefieldMembers)
            {
                return Failure(
                    DRPGBattleSessionCreationError.TooManyBattlefieldMembers,
                    $"At most {MaximumBattlefieldMembers} battlefield members are allowed.");
            }
            return Failure(
                DRPGBattleSessionCreationError.InvalidPartyMember,
                exception.Message);
        }

        return CreateRoster(snapshot, encounter, progressionService);
    }

    public static DRPGBattleSessionCreationResult CreateRoster(
        DRPGPartyRosterSnapshot party,
        DRPGEncounterDefinitionSO encounter,
        DRPGProgressionService progressionService = null,
        DRPGNodePartyBattleState nodePartyState = null)
    {
        if (party == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingPartySetup,
                "A frozen DRPG party roster is required.");
        }

        if (encounter == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingEncounter,
                "A DRPG encounter definition is required.");
        }

        if (party.Protagonist == null)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingProtagonist,
                "The required protagonist profile is missing.");
        }

        if (party.Count > MaximumBattlefieldMembers)
        {
            return Failure(
                DRPGBattleSessionCreationError.TooManyBattlefieldMembers,
                $"At most {MaximumBattlefieldMembers} battlefield members are allowed.");
        }

        if (party.Count == 0)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingBattlefieldMembers,
                "At least one battlefield member is required.");
        }

        IReadOnlyList<DRPGCombatantProfileSO> orderedProfiles =
            party.OrderedProfiles;
        for (int i = 0; i < orderedProfiles.Count; i++)
        {
            if (orderedProfiles[i] == null)
            {
                return Failure(
                    DRPGBattleSessionCreationError.InvalidPartyMember,
                    $"Party member at slot {i} is missing.");
            }
        }

        IReadOnlyList<DRPGEnemyDeploymentDefinition> enemies =
            encounter.CreateResolvedEnemyDeployments();
        if (enemies.Count == 0)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingEnemies,
                "The encounter must contain at least one enemy.");
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].Profile == null)
            {
                return Failure(
                    DRPGBattleSessionCreationError.InvalidEnemy,
                    $"Enemy at slot {i} is missing.");
            }
        }

        int capacity = orderedProfiles.Count + enemies.Count;
        var participants = new List<BattleParticipantRuntime>(capacity);
        int nextRuntimeId = 1;

        bool protagonistAdded = false;
        for (int i = 0; i < orderedProfiles.Count; i++)
        {
            DRPGCombatantProfileSO profile = orderedProfiles[i];
            bool isProtagonist = ReferenceEquals(profile, party.Protagonist) ||
                (!string.IsNullOrWhiteSpace(profile.CombatantId) &&
                 string.Equals(profile.CombatantId,
                     party.Protagonist.CombatantId,
                     StringComparison.Ordinal));
            if (isProtagonist)
            {
                if (protagonistAdded)
                {
                    return Failure(
                        DRPGBattleSessionCreationError.InvalidPartyMember,
                        "The protagonist appears more than once in the physical formation.");
                }
                protagonistAdded = true;
            }
            float? initialHp = nodePartyState != null &&
                nodePartyState.TryGetHitPoints(profile.CombatantId, out float savedHp)
                    ? savedHp
                    : (float?)null;
            participants.Add(new BattleParticipantRuntime(
                nextRuntimeId++,
                new BattleParticipantSpec(
                    profile,
                    DRPGBattleSide.Player,
                    isProtagonist
                        ? DRPGBattleRosterRole.Protagonist
                        : DRPGBattleRosterRole.PartyMember,
                    i,
                    ResolveBuild(profile, progressionService),
                    profile.PreferredFormationRow,
                    profile.PreferredFormationRow,
                    !initialHp.HasValue || initialHp.Value > 0f,
                    i,
                    initialHp)));
        }
        if (!protagonistAdded)
        {
            return Failure(
                DRPGBattleSessionCreationError.MissingProtagonist,
                "The protagonist is not present in the physical formation.");
        }

        DRPGFormationRules formationRules = encounter.FormationRules;
        var reserveEnemies = new List<BattleParticipantRuntime>();
        var rowCounts = new int[3];
        int activeEnemyCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            DRPGEnemyDeploymentDefinition deployment = enemies[i];
            bool canDeploy = activeEnemyCount < formationRules.InitialEnemyLimit;
            DRPGFormationRow initialRow = deployment.PreferredRow;
            if (canDeploy)
            {
                canDeploy = TryFindAvailableRow(
                    deployment.PreferredRow,
                    rowCounts,
                    formationRules.PerRowLimit,
                    out initialRow);
            }

            var runtime = new BattleParticipantRuntime(
                nextRuntimeId++,
                new BattleParticipantSpec(
                    deployment.Profile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    i,
                    ResolveBuild(deployment.Profile, progressionService),
                    initialRow,
                    deployment.PreferredRow,
                    canDeploy));
            if (canDeploy)
            {
                participants.Add(runtime);
                rowCounts[(int)initialRow]++;
                activeEnemyCount++;
            }
            else
            {
                reserveEnemies.Add(runtime);
            }
        }

        DRPGFormationNormalizer.CompactOccupiedRows(
            participants,
            DRPGBattleSide.Enemy);
        return DRPGBattleSessionCreationResult.Success(
            participants.AsReadOnly(),
            reserveEnemies.AsReadOnly(),
            formationRules);
    }

    private static bool TryFindAvailableRow(
        DRPGFormationRow preferred,
        IReadOnlyList<int> rowCounts,
        int perRowLimit,
        out DRPGFormationRow resolved)
    {
        for (int distance = 0; distance <= 2; distance++)
        {
            int lower = (int)preferred - distance;
            if (lower >= 0 && rowCounts[lower] < perRowLimit)
            {
                resolved = (DRPGFormationRow)lower;
                return true;
            }
            int upper = (int)preferred + distance;
            if (distance > 0 && upper <= 2 && rowCounts[upper] < perRowLimit)
            {
                resolved = (DRPGFormationRow)upper;
                return true;
            }
        }

        resolved = preferred;
        return false;
    }

    private static void CollectSkillProgress(
        DRPGSkillSO skill,
        IDictionary<string, DRPGProgressResourceSO> resources,
        ICollection<DRPGConfiguredProgressRuleBinding> bindings,
        ISet<string> bindingKeys)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
            return;
        CollectResource(skill.ActionProgressCost != null
            ? skill.ActionProgressCost.Resource
            : null, resources);
        CollectResource(skill.InputProgressCost != null
            ? skill.InputProgressCost.Resource
            : null, resources);
        IReadOnlyList<DRPGProgressGainRuleDefinition> rules =
            skill.ProgressGainRules;
        for (int i = 0; i < rules.Count; i++)
        {
            DRPGProgressGainRuleDefinition rule = rules[i];
            if (rule == null)
                continue;
            rule.ValidateOrThrow();
            CollectResource(rule.Resource, resources);
            string key = skill.SkillId + "\n" + rule.RuleId;
            if (bindingKeys.Add(key))
            {
                bindings.Add(new DRPGConfiguredProgressRuleBinding(
                    skill.SkillId,
                    rule));
            }
        }
    }

    private static void CollectResource(
        DRPGProgressResourceSO resource,
        IDictionary<string, DRPGProgressResourceSO> resources)
    {
        if (resource == null)
            return;
        resource.ValidateOrThrow();
        if (resources.TryGetValue(
                resource.ResourceId,
                out DRPGProgressResourceSO existing) &&
            !ReferenceEquals(existing, resource))
        {
            throw new InvalidOperationException(
                "Duplicate progress resource ID uses different assets: " +
                resource.ResourceId);
        }
        resources[resource.ResourceId] = resource;
    }

    private static float ResolveCapacityModifier(
        DRPGResolvedEquipmentSnapshot equipment,
        string resourceId)
    {
        float total = 0f;
        IReadOnlyList<DRPGResolvedProgressCapacityModifier> modifiers =
            equipment != null
                ? equipment.ProgressCapacityModifiers
                : Array.Empty<DRPGResolvedProgressCapacityModifier>();
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (string.Equals(
                    modifiers[i].ResourceId,
                    resourceId,
                    StringComparison.Ordinal))
            {
                total += modifiers[i].CapacityModifier;
            }
        }
        return total;
    }

    private static DRPGResolvedCombatantBuild ResolveBuild(
        DRPGCombatantProfileSO profile,
        DRPGProgressionService progressionService)
    {
        return progressionService != null
            ? progressionService.ResolveBuild(profile)
            : DRPGResolvedCombatantBuild.CreateLegacy(profile);
    }

    private static DRPGBattleSessionCreationResult Failure(
        DRPGBattleSessionCreationError error,
        string message)
    {
        return DRPGBattleSessionCreationResult.Failure(error, message);
    }
}
