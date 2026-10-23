using System;
using System.Collections.Generic;

public sealed class DRPGProgressionService
{
    private readonly Dictionary<string, DRPGCharacterProgressionState> states =
        new Dictionary<string, DRPGCharacterProgressionState>(StringComparer.Ordinal);
    private readonly DRPGResonanceService resonanceService;

    public DRPGProgressionService(DRPGResonanceService resonanceService = null)
    {
        this.resonanceService = resonanceService;
    }

    public DRPGResonanceService ResonanceService => resonanceService;

    public DRPGCharacterProgressionView GetView(DRPGCombatantProfileSO profile)
    {
        DRPGCharacterProgressionState character = RequireState(profile);
        if (!character.TryGetActive(out DRPGProfessionProgressState professionState))
            throw new InvalidOperationException("Active profession state is missing.");
        return CreateView(profile, character, professionState);
    }

    public DRPGProgressionOperationResult TryActivateNode(
        DRPGCombatantProfileSO profile,
        string nodeId)
    {
        if (!TryGetActive(profile, out DRPGCharacterProgressionState character,
                out DRPGProfessionProgressState state,
                out DRPGProgressionOperationResult failure))
        {
            return failure;
        }

        DRPGSkillTreeSO tree = state.Profession.SkillTree;
        if (tree == null)
        {
            return Fail(DRPGProgressionError.MissingTree,
                "The active profession has no skill tree.");
        }
        tree.ValidateOrThrow();
        DRPGSkillTreeNodeDefinition node = tree.FindNode(nodeId);
        if (node == null)
            return Fail(DRPGProgressionError.NodeNotFound, "Node not found: " + nodeId);
        if (state.IsUnlocked(node.NodeId))
        {
            return Fail(
                DRPGProgressionError.NodeAlreadyUnlocked,
                "Node is already unlocked: " + node.NodeId);
        }

        IReadOnlyList<string> prerequisites = node.PrerequisiteNodeIds;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!state.IsUnlocked(prerequisites[i]))
            {
                return Fail(
                    DRPGProgressionError.PrerequisiteLocked,
                    "Prerequisite is locked: " + prerequisites[i]);
            }
        }

        if (character.AvailableSkillPoints < node.PointCost)
        {
            return Fail(
                DRPGProgressionError.InsufficientPoints,
                "Not enough skill points for node: " + node.NodeId);
        }
        if (!node.HasValidReward)
        {
            return Fail(
                DRPGProgressionError.InvalidReward,
                "Node has no valid reward: " + node.NodeId);
        }

        state.Purchase(node.NodeId, node.PointCost);
        return DRPGProgressionOperationResult.Ok();
    }

    public DRPGProgressionOperationResult TrySetActiveProfession(
        DRPGCombatantProfileSO profile,
        DRPGProfessionSO profession)
    {
        if (profession == null)
        {
            return Fail(
                DRPGProgressionError.ProfessionUnavailable,
                "Profession is not available to this character.");
        }
        return TrySetActiveProfession(profile, profession.ProfessionId);
    }

    public DRPGProgressionOperationResult TrySetActiveProfession(
        DRPGCombatantProfileSO profile,
        string professionId)
    {
        if (profile == null)
            return Fail(DRPGProgressionError.MissingProfile, "Profile is missing.");
        DRPGCharacterProgressionDefinitionSO definition =
            profile.ProgressionDefinition;
        if (definition == null)
            return Fail(DRPGProgressionError.MissingDefinition, "Progression definition is missing.");
        DRPGProfessionSO profession = definition.FindProfession(professionId);
        if (profession == null)
        {
            return Fail(
                DRPGProgressionError.ProfessionUnavailable,
                "Profession is not available to this character.");
        }

        DRPGCharacterProgressionState character = RequireState(profile);
        if (!character.IsProfessionUnlocked(profession.ProfessionId))
        {
            return Fail(
                DRPGProgressionError.ProfessionLocked,
                "Profession is locked: " + profession.ProfessionId);
        }
        return character.SetActive(profession.ProfessionId)
            ? DRPGProgressionOperationResult.Ok()
            : Fail(DRPGProgressionError.MissingProfession, "Profession state is missing.");
    }

    public bool IsProfessionUnlocked(
        DRPGCombatantProfileSO profile,
        string professionId)
    {
        if (profile == null || profile.ProgressionDefinition == null ||
            profile.ProgressionDefinition.FindProfession(professionId) == null)
        {
            return false;
        }
        return RequireState(profile).IsProfessionUnlocked(professionId);
    }

    public DRPGProgressionOperationResult TryUnlockProfession(
        DRPGCombatantProfileSO profile,
        string professionId)
    {
        if (profile == null)
            return Fail(DRPGProgressionError.MissingProfile, "Profile is missing.");
        DRPGCharacterProgressionDefinitionSO definition =
            profile.ProgressionDefinition;
        if (definition == null)
        {
            return Fail(
                DRPGProgressionError.MissingDefinition,
                "Progression definition is missing.");
        }
        DRPGProfessionSO profession = definition.FindProfession(professionId);
        if (profession == null)
        {
            return Fail(
                DRPGProgressionError.ProfessionUnavailable,
                "Profession is not available to this character.");
        }

        DRPGCharacterProgressionState character = RequireState(profile);
        if (character.IsProfessionUnlocked(professionId))
        {
            return Fail(
                DRPGProgressionError.ProfessionAlreadyUnlocked,
                "Profession is already unlocked: " + professionId);
        }
        return character.UnlockProfession(professionId)
            ? DRPGProgressionOperationResult.Ok()
            : Fail(
                DRPGProgressionError.MissingProfession,
                "Profession state is missing: " + professionId);
    }

    public DRPGProgressionOperationResult TryEquipSkill(
        DRPGCombatantProfileSO profile,
        DRPGSkillSO skill,
        int slotIndex)
    {
        if (!TryGetActive(profile, out _, out DRPGProfessionProgressState state,
                out DRPGProgressionOperationResult failure))
        {
            return failure;
        }

        HashSet<string> allowed = CollectUnlockedSkillIds(state);
        return state.Loadout.TryEquip(skill, slotIndex, allowed);
    }

    public DRPGProgressionOperationResult TrySwapSkillSlots(
        DRPGCombatantProfileSO profile,
        int firstSlot,
        int secondSlot)
    {
        if (!TryGetActive(profile, out _, out DRPGProfessionProgressState state,
                out DRPGProgressionOperationResult failure))
        {
            return failure;
        }

        return state.Loadout.TrySwap(firstSlot, secondSlot);
    }

    public DRPGProgressionOperationResult ResetProfession(
        DRPGCombatantProfileSO profile)
    {
        if (!TryGetActive(profile, out _, out DRPGProfessionProgressState state,
                out DRPGProgressionOperationResult failure))
        {
            return failure;
        }

        state.ResetPurchased();
        state.Loadout.RemoveUnavailable(CollectUnlockedSkillIds(state));
        return DRPGProgressionOperationResult.Ok();
    }

    public DRPGResolvedCombatantBuild ResolveBuild(DRPGCombatantProfileSO profile)
    {
        return ResolveBuild(profile, null);
    }

    public DRPGResolvedCombatantBuild ResolveBuild(
        DRPGCombatantProfileSO profile,
        DRPGResolvedEquipmentSnapshot equipment)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (profile.ProgressionDefinition == null)
            return DRPGResolvedCombatantBuild.CreateLegacy(profile, equipment);

        DRPGCharacterProgressionView view = GetView(profile);
        return new DRPGResolvedCombatantBuild(
            profile,
            view.ActiveProfession,
            view.ActiveProfession != null ? view.ActiveProfession.BasicAttack : null,
            view.EquippedSkills,
            view.UnlockedSkills,
            view.PassiveModifiers,
            view.DerivedStats,
            equipment ?? DRPGResolvedEquipmentSnapshot.Empty(
                view.ActiveProfession != null
                    ? view.ActiveProfession.ProfessionId
                    : string.Empty),
            Array.Empty<DRPGSkillSO>(),
            resonanceService != null
                ? resonanceService.ResolveIdentityContribution(profile)
                : DRPGResolvedIdentityContribution.Empty);
    }

    public DRPGLevelGainResult GainExperience(
        DRPGCombatantProfileSO profile,
        long amount)
    {
        return RequireState(profile).GainExperience(amount);
    }

    public DRPGProgressionOperationResult TryUnlockBreakthrough(
        DRPGCombatantProfileSO profile)
    {
        DRPGCharacterProgressionState state = RequireState(profile);
        if (!state.AllowsLevelBreakthrough)
        {
            return Fail(
                DRPGProgressionError.BreakthroughUnavailable,
                "This character cannot unlock level breakthrough.");
        }
        if (state.BreakthroughUnlocked)
        {
            return Fail(
                DRPGProgressionError.BreakthroughAlreadyUnlocked,
                "Level breakthrough is already unlocked.");
        }
        return state.UnlockBreakthrough()
            ? DRPGProgressionOperationResult.Ok()
            : Fail(
                DRPGProgressionError.BreakthroughUnavailable,
                "Level breakthrough could not be unlocked.");
    }

    public DRPGAttributeAllocationDraft BeginAttributeDraft(
        DRPGCombatantProfileSO profile)
    {
        return RequireState(profile).BeginAttributeDraft();
    }

    public DRPGProgressionOperationResult TryCommitAttributeDraft(
        DRPGCombatantProfileSO profile,
        DRPGAttributeAllocationDraft draft)
    {
        DRPGCharacterProgressionState state = RequireState(profile);
        return state.TryCommit(draft)
            ? DRPGProgressionOperationResult.Ok()
            : Fail(
                DRPGProgressionError.StaleDraft,
                "Attribute allocation changed before it could be committed.");
    }

    public int ResetAllocatedAttributes(DRPGCombatantProfileSO profile)
    {
        return RequireState(profile).ResetAllocatedAttributes();
    }

    public DRPGProgressionSaveData CaptureSaveData(
        IEnumerable<DRPGCombatantProfileSO> profiles)
    {
        var save = new DRPGProgressionSaveData();
        if (resonanceService != null)
            save.resonance = resonanceService.CaptureSaveData();
        if (profiles == null)
            return save;

        foreach (DRPGCombatantProfileSO profile in profiles)
        {
            if (profile == null || profile.ProgressionDefinition == null)
                continue;
            DRPGCharacterProgressionState state = RequireState(profile);
            var character = new DRPGCharacterProgressionSaveData
            {
                characterId = state.CharacterId,
                level = state.Level,
                experience = state.CurrentExperience,
                breakthroughUnlocked = state.BreakthroughUnlocked,
                availableAttributePoints = state.AvailableAttributePoints,
                totalSkillPoints = state.TotalSkillPoints,
                attributes = DRPGCoreAttributeSaveData.From(state.Attributes),
                activeProfessionId = state.ActiveProfessionId
            };
            foreach (DRPGProfessionProgressState professionState in state.Professions)
            {
                var profession = new DRPGProfessionProgressionSaveData
                {
                    professionId = professionState.Profession.ProfessionId,
                    isUnlocked = professionState.IsProfessionUnlocked
                };
                profession.purchasedNodeIds.AddRange(professionState.PurchasedNodeIds);
                IReadOnlyList<DRPGSkillSO> slots = professionState.Loadout.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    profession.equippedSkillIds.Add(
                        slots[i] != null ? slots[i].SkillId : string.Empty);
                }
                character.professions.Add(profession);
            }
            save.characters.Add(character);
        }
        return save;
    }

    public DRPGProgressionOperationResult RestoreSaveData(
        IEnumerable<DRPGCombatantProfileSO> profiles,
        DRPGProgressionSaveData save)
    {
        return RestoreSaveData(profiles, save, null);
    }

    public DRPGProgressionOperationResult RestoreSaveData(
        IEnumerable<DRPGCombatantProfileSO> profiles,
        DRPGProgressionSaveData save,
        DRPGPartyCompositionState party)
    {
        if (profiles == null || save == null || save.characters == null)
        {
            return Fail(
                DRPGProgressionError.InvalidSaveData,
                "Progression save data is missing.");
        }

        var profilesById = new Dictionary<string, DRPGCombatantProfileSO>(
            StringComparer.Ordinal);
        foreach (DRPGCombatantProfileSO profile in profiles)
        {
            if (profile != null && profile.ProgressionDefinition != null)
            {
                string id = string.IsNullOrWhiteSpace(profile.CombatantId)
                    ? profile.ProgressionDefinition.ProgressionId
                    : profile.CombatantId;
                profilesById[id] = profile;
            }
        }

        DRPGProgressionOperationResult validation = ValidateSaveData(
            profilesById,
            save);
        if (!validation.Success)
            return validation;

        for (int i = 0; i < save.characters.Count; i++)
        {
            DRPGCharacterProgressionSaveData characterSave = save.characters[i];
            DRPGCombatantProfileSO profile = profilesById[characterSave.characterId];

            states.Remove(characterSave.characterId);
            DRPGCharacterProgressionState state = RequireState(profile);
            if (save.version == 2)
            {
                foreach (DRPGProfessionProgressState legacyProfession in
                    state.Professions)
                {
                    state.UnlockProfession(
                        legacyProfession.Profession.ProfessionId);
                }
            }
            state.RestoreGrowth(
                characterSave.level,
                characterSave.experience,
                characterSave.breakthroughUnlocked,
                characterSave.attributes != null
                    ? characterSave.attributes.ToValue()
                    : profile.ProgressionDefinition.InitialAttributes,
                characterSave.availableAttributePoints,
                characterSave.totalSkillPoints);

            if (characterSave.professions != null)
            {
                for (int professionIndex = 0;
                    professionIndex < characterSave.professions.Count;
                    professionIndex++)
                {
                    DRPGProfessionProgressionSaveData professionSave =
                        characterSave.professions[professionIndex];
                    if (professionSave == null ||
                        !state.TryGet(
                            professionSave.professionId,
                            out DRPGProfessionProgressState professionState))
                    {
                        continue;
                    }

                    if (save.version >= 3 && professionSave.isUnlocked)
                    {
                        state.UnlockProfession(
                            professionState.Profession.ProfessionId);
                    }

                    professionState.RestorePurchased(
                        professionSave.purchasedNodeIds,
                        professionState.Profession.SkillTree);
                    for (int slot = 0; slot < DRPGSkillLoadout.SlotCount; slot++)
                        professionState.Loadout.TryEquip(null, slot, null);
                    RestoreLoadout(professionState, professionSave.equippedSkillIds);
                }
            }

            if (state.SpentSkillPoints > state.TotalSkillPoints)
            {
                states.Remove(characterSave.characterId);
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Saved skill-tree spending exceeds the character-owned point pool: " +
                    characterSave.characterId);
            }
            if (!string.IsNullOrWhiteSpace(characterSave.activeProfessionId) &&
                !state.SetActive(characterSave.activeProfessionId))
            {
                states.Remove(characterSave.characterId);
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Saved active profession is locked: " +
                    characterSave.activeProfessionId);
            }
        }
        if (resonanceService != null && save.version >= 4)
        {
            if (save.resonance == null)
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Progression v4 is missing resonance state.");
            }
            DRPGResonanceOperationResult resonanceResult = party != null
                ? resonanceService.RestoreSaveData(
                    save.resonance,
                    party,
                    profilesById.Values)
                : resonanceService.RestoreSaveData(
                    save.resonance,
                    profilesById.Values);
            if (!resonanceResult.Success)
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    resonanceResult.Message);
            }
        }
        return DRPGProgressionOperationResult.Ok();
    }

    private static DRPGProgressionOperationResult ValidateSaveData(
        IReadOnlyDictionary<string, DRPGCombatantProfileSO> profilesById,
        DRPGProgressionSaveData save)
    {
        if (save.version != 2 && save.version != 3 && save.version != 4)
        {
            return Fail(
                DRPGProgressionError.InvalidSaveData,
                "Unsupported progression save version: " + save.version);
        }
        var seenCharacters = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < save.characters.Count; i++)
        {
            DRPGCharacterProgressionSaveData character = save.characters[i];
            if (character == null || string.IsNullOrWhiteSpace(character.characterId) ||
                !seenCharacters.Add(character.characterId) ||
                !profilesById.TryGetValue(
                    character.characterId,
                    out DRPGCombatantProfileSO profile))
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Unknown or duplicate progression character ID at record " + i + ".");
            }

            DRPGCharacterProgressionDefinitionSO definition =
                profile.ProgressionDefinition;
            if (character.level < 1 ||
                character.level > DRPGGrowthRules.BreakthroughLevelCap ||
                character.experience < 0L ||
                character.availableAttributePoints < 0 ||
                character.totalSkillPoints < 0 ||
                character.attributes == null ||
                character.breakthroughUnlocked && !definition.AllowsLevelBreakthrough ||
                character.level > DRPGGrowthRules.NormalLevelCap &&
                !character.breakthroughUnlocked)
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Invalid level, point, attribute, or breakthrough data: " +
                    character.characterId);
            }

            DRPGCoreAttributes raw = new DRPGCoreAttributes(
                character.attributes.vitality,
                character.attributes.strength,
                character.attributes.endurance,
                character.attributes.technique,
                character.attributes.circuit,
                character.attributes.reaction);
            DRPGCoreAttributes initial = definition.InitialAttributes;
            for (int attributeIndex = 0; attributeIndex < 6; attributeIndex++)
            {
                DRPGCoreAttributeId attribute =
                    (DRPGCoreAttributeId)attributeIndex;
                int value = raw.Get(attribute);
                if (value < initial.Get(attribute) ||
                    value > DRPGGrowthRules.AttributeCap)
                {
                    return Fail(
                        DRPGProgressionError.InvalidSaveData,
                        "Saved attributes violate initial values or caps: " +
                        character.characterId);
                }
            }

            var professionsById = new Dictionary<string, DRPGProfessionSO>(
                StringComparer.Ordinal);
            IReadOnlyList<DRPGProfessionSO> available =
                definition.AvailableProfessions;
            for (int professionIndex = 0;
                professionIndex < available.Count;
                professionIndex++)
            {
                DRPGProfessionSO profession = available[professionIndex];
                if (profession != null &&
                    !string.IsNullOrWhiteSpace(profession.ProfessionId))
                {
                    professionsById[profession.ProfessionId] = profession;
                }
            }

            if (!string.IsNullOrWhiteSpace(character.activeProfessionId) &&
                !professionsById.ContainsKey(character.activeProfessionId))
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Unknown active profession ID: " + character.activeProfessionId);
            }

            int spentSkillPoints = 0;
            var seenProfessions = new HashSet<string>(StringComparer.Ordinal);
            if (character.professions == null)
                character.professions = new List<DRPGProfessionProgressionSaveData>();
            for (int professionIndex = 0;
                professionIndex < character.professions.Count;
                professionIndex++)
            {
                DRPGProfessionProgressionSaveData professionSave =
                    character.professions[professionIndex];
                if (professionSave == null ||
                    !seenProfessions.Add(professionSave.professionId) ||
                    !professionsById.TryGetValue(
                        professionSave.professionId,
                        out DRPGProfessionSO profession))
                {
                    return Fail(
                        DRPGProgressionError.InvalidSaveData,
                        "Unknown or duplicate profession record for " +
                        character.characterId + ".");
                }

                DRPGSkillTreeSO tree = profession.SkillTree;
                var unlockedSkillIds = new HashSet<string>(StringComparer.Ordinal);
                var unlockedNodeIds = new HashSet<string>(StringComparer.Ordinal);
                IReadOnlyList<string> granted = profession.GrantedNodeIds;
                for (int grantedIndex = 0; grantedIndex < granted.Count; grantedIndex++)
                    unlockedNodeIds.Add(granted[grantedIndex]);
                IReadOnlyList<string> purchased =
                    professionSave.purchasedNodeIds != null
                        ? (IReadOnlyList<string>)professionSave.purchasedNodeIds
                        : Array.Empty<string>();
                for (int nodeIndex = 0; nodeIndex < purchased.Count; nodeIndex++)
                {
                    DRPGSkillTreeNodeDefinition node =
                        tree != null ? tree.FindNode(purchased[nodeIndex]) : null;
                    if (node == null || !unlockedNodeIds.Add(node.NodeId))
                    {
                        return Fail(
                            DRPGProgressionError.InvalidSaveData,
                            "Unknown or duplicate purchased node ID: " +
                            purchased[nodeIndex]);
                    }
                    spentSkillPoints += node.PointCost;
                }

                if (tree != null)
                {
                    IReadOnlyList<DRPGSkillTreeNodeDefinition> nodes = tree.Nodes;
                    for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                    {
                        DRPGSkillTreeNodeDefinition node = nodes[nodeIndex];
                        if (unlockedNodeIds.Contains(node.NodeId) &&
                            node.RewardKind == DRPGSkillTreeRewardKind.Skill &&
                            node.Skill != null)
                        {
                            unlockedSkillIds.Add(node.Skill.SkillId);
                        }
                    }
                }

                IReadOnlyList<string> equipped =
                    professionSave.equippedSkillIds != null
                        ? (IReadOnlyList<string>)professionSave.equippedSkillIds
                        : Array.Empty<string>();
                if (equipped.Count > DRPGSkillLoadout.SlotCount)
                {
                    return Fail(
                        DRPGProgressionError.InvalidSaveData,
                        "Saved loadout has too many slots: " + professionSave.professionId);
                }
                for (int slot = 0; slot < equipped.Count; slot++)
                {
                    if (!string.IsNullOrWhiteSpace(equipped[slot]) &&
                        !unlockedSkillIds.Contains(equipped[slot]))
                    {
                        return Fail(
                            DRPGProgressionError.InvalidSaveData,
                            "Unknown or locked equipped skill ID: " + equipped[slot]);
                    }
                }
            }

            if (spentSkillPoints > character.totalSkillPoints)
            {
                return Fail(
                    DRPGProgressionError.InvalidSaveData,
                    "Saved skill spending exceeds character-owned points: " +
                    character.characterId);
            }
        }
        return DRPGProgressionOperationResult.Ok();
    }

    private DRPGCharacterProgressionState RequireState(
        DRPGCombatantProfileSO profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        DRPGCharacterProgressionDefinitionSO definition =
            profile.ProgressionDefinition;
        if (definition == null)
        {
            throw new InvalidOperationException(
                "Profile has no progression definition: " + profile.CombatantId);
        }

        string key = string.IsNullOrWhiteSpace(profile.CombatantId)
            ? definition.ProgressionId
            : profile.CombatantId;
        if (states.TryGetValue(key, out DRPGCharacterProgressionState existing))
            return existing;

        var created = new DRPGCharacterProgressionState(key, definition);
        IReadOnlyList<DRPGProfessionSO> professions =
            definition.AvailableProfessions;
        for (int i = 0; i < professions.Count; i++)
        {
            DRPGProfessionSO profession = professions[i];
            if (profession == null || string.IsNullOrWhiteSpace(profession.ProfessionId))
                continue;
            profession.SkillTree?.ValidateOrThrow();
            var state = new DRPGProfessionProgressState(
                profession,
                definition.IsStartingProfessionUnlocked(profession));
            IReadOnlyList<string> granted = profession.GrantedNodeIds;
            for (int nodeIndex = 0; nodeIndex < granted.Count; nodeIndex++)
            {
                if (profession.SkillTree != null &&
                    profession.SkillTree.FindNode(granted[nodeIndex]) == null)
                {
                    throw new InvalidOperationException(
                        "Granted node is missing from tree: " + granted[nodeIndex]);
                }
                state.GrantNode(granted[nodeIndex]);
            }
            SeedLoadout(state, profession.InitialLoadout);
            created.Add(state);
        }

        DRPGProfessionSO defaultProfession = definition.DefaultProfession;
        if (defaultProfession == null ||
            !created.SetActive(defaultProfession.ProfessionId))
        {
            throw new InvalidOperationException(
                "Progression definition has no valid default profession: " +
                definition.ProgressionId);
        }

        states.Add(key, created);
        return created;
    }

    private void SeedLoadout(
        DRPGProfessionProgressState state,
        IReadOnlyList<DRPGSkillSO> initialLoadout)
    {
        HashSet<string> allowed = CollectUnlockedSkillIds(state);
        int count = Math.Min(DRPGSkillLoadout.SlotCount, initialLoadout.Count);
        for (int i = 0; i < count; i++)
        {
            DRPGSkillSO skill = initialLoadout[i];
            if (skill == null)
                continue;
            DRPGProgressionOperationResult result =
                state.Loadout.TryEquip(skill, i, allowed);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);
        }
    }

    private void RestoreLoadout(
        DRPGProfessionProgressState state,
        IReadOnlyList<string> skillIds)
    {
        if (skillIds == null)
            return;
        HashSet<string> allowed = CollectUnlockedSkillIds(state);
        DRPGSkillTreeSO tree = state.Profession.SkillTree;
        int count = Math.Min(DRPGSkillLoadout.SlotCount, skillIds.Count);
        for (int slot = 0; slot < count; slot++)
        {
            string skillId = skillIds[slot];
            if (string.IsNullOrWhiteSpace(skillId) || tree == null)
                continue;
            DRPGSkillSO skill = FindSkill(tree, skillId);
            if (skill != null)
                state.Loadout.TryEquip(skill, slot, allowed);
        }
    }

    private static DRPGSkillSO FindSkill(DRPGSkillTreeSO tree, string skillId)
    {
        IReadOnlyList<DRPGSkillTreeNodeDefinition> nodes = tree.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Skill != null && nodes[i].Skill.SkillId == skillId)
                return nodes[i].Skill;
        }
        return null;
    }

    private DRPGCharacterProgressionView CreateView(
        DRPGCombatantProfileSO profile,
        DRPGCharacterProgressionState character,
        DRPGProfessionProgressState state)
    {
        var unlockedIds = new List<string>();
        var unlockedSkills = new List<DRPGSkillSO>();
        var passives = new List<DRPGPassiveModifierSO>();
        DRPGSkillTreeSO tree = state.Profession.SkillTree;
        if (tree != null)
        {
            IReadOnlyList<DRPGSkillTreeNodeDefinition> nodes = tree.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                DRPGSkillTreeNodeDefinition node = nodes[i];
                if (!state.IsUnlocked(node.NodeId))
                    continue;
                unlockedIds.Add(node.NodeId);
                if (node.RewardKind == DRPGSkillTreeRewardKind.Skill &&
                    node.Skill != null)
                {
                    unlockedSkills.Add(node.Skill);
                }
                else if (node.RewardKind == DRPGSkillTreeRewardKind.Passive &&
                         node.PassiveModifier != null)
                {
                    passives.Add(node.PassiveModifier);
                }
            }
        }

        return new DRPGCharacterProgressionView(
            profile,
            state.Profession,
            character.Level,
            character.CurrentExperience,
            DRPGGrowthRules.ExperienceToNextLevel(character.Level),
            character.AllowsLevelBreakthrough,
            character.BreakthroughUnlocked,
            character.AvailableAttributePoints,
            character.AvailableSkillPoints,
            character.Attributes,
            DRPGDerivedStatResolver.Resolve(
                profile,
                profile.ProgressionDefinition.InitialAttributes,
                character.Attributes,
                character.Level),
            unlockedIds.AsReadOnly(),
            unlockedSkills.AsReadOnly(),
            passives.AsReadOnly(),
            Array.AsReadOnly(state.Loadout.Snapshot()));
    }

    private HashSet<string> CollectUnlockedSkillIds(
        DRPGProfessionProgressState state)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        DRPGSkillTreeSO tree = state.Profession.SkillTree;
        if (tree == null)
            return result;
        IReadOnlyList<DRPGSkillTreeNodeDefinition> nodes = tree.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = nodes[i];
            if (state.IsUnlocked(node.NodeId) &&
                node.RewardKind == DRPGSkillTreeRewardKind.Skill &&
                node.Skill != null)
            {
                result.Add(node.Skill.SkillId);
            }
        }
        return result;
    }

    private bool TryGetActive(
        DRPGCombatantProfileSO profile,
        out DRPGCharacterProgressionState character,
        out DRPGProfessionProgressState state,
        out DRPGProgressionOperationResult failure)
    {
        character = null;
        state = null;
        failure = null;
        if (profile == null)
        {
            failure = Fail(DRPGProgressionError.MissingProfile, "Profile is missing.");
            return false;
        }
        if (profile.ProgressionDefinition == null)
        {
            failure = Fail(
                DRPGProgressionError.MissingDefinition,
                "Progression definition is missing.");
            return false;
        }
        character = RequireState(profile);
        if (!character.TryGetActive(out state))
        {
            failure = Fail(
                DRPGProgressionError.MissingProfession,
                "Active profession state is missing.");
            return false;
        }
        return true;
    }

    private static DRPGProgressionOperationResult Fail(
        DRPGProgressionError error,
        string message)
    {
        return DRPGProgressionOperationResult.Fail(error, message);
    }
}
