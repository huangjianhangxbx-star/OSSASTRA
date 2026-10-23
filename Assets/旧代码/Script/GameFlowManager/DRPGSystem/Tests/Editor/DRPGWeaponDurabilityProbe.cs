#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGWeaponDurabilityProbe
{
    private sealed class MinimumRandom : IDRPGRandomSource
    {
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive;
        }

        public int RangeInclusive(int minimumInclusive, int maximumInclusive)
        {
            return minimumInclusive;
        }
    }

    public static string Run()
    {
        VerifyUseKindsAndCancellation();
        VerifyOrdinaryBreakAtBattleActionCompletion();
        VerifySignatureBreakFallback();
        return "PASS DRPG weapon durability";
    }

    private static void VerifyUseKindsAndCancellation()
    {
        DRPGSkillSO basic = CreateSkill("durability_basic", 1f);
        DRPGCombatantProfileSO profile = CreateProfile(
            "durability_use_actor",
            basic,
            Array.Empty<DRPGSkillSO>());
        DRPGWeaponDefinitionSO definition = CreateWeaponDefinition(
            "durability_use_weapon",
            DRPGWeaponOwnershipKind.General,
            null,
            20,
            null,
            null);

        try
        {
            DRPGWeaponInstance weapon = DRPGWeaponInstance.Create(
                "durability_use_instance",
                definition,
                new MinimumRandom());
            var inventory = new DRPGPartyInventoryState(20, 100f);
            var root = new DRPGEquipmentRoot(inventory);
            var equipment = new DRPGCharacterEquipmentState(
                profile.CombatantId,
                weapon);
            AssertTrue(root.TryRegisterCharacter(profile, equipment).Success,
                "The durability actor must register");

            var progression = new DRPGProgressionService();
            var service = new DRPGBattleWeaponDurabilityService(
                root,
                progression);
            BattleParticipantRuntime actor = CreateActor(
                1,
                profile,
                progression.ResolveBuild(
                    profile,
                    DRPGResolvedEquipmentSnapshot.FromWeapon(weapon, string.Empty)));

            int before = weapon.CurrentDurability;
            DRPGBattleWeaponActionContext threeShots =
                service.CaptureAction(actor, 101);
            for (int i = 0; i < 3; i++)
            {
                AssertTrue(service.TryRecordCommittedUse(
                    threeShots,
                    DRPGWeaponUseKind.FirearmShot).Success,
                    "Each committed firearm shot must enter the ledger");
            }
            DRPGBattleWeaponActionCompletion shotCompletion =
                service.CompleteAction(actor, threeShots);
            AssertTrue(shotCompletion.Success,
                "Three-shot action must complete");
            AssertEqual(3, shotCompletion.AppliedUseUnits,
                "Three committed shots must cost three durability units");
            AssertEqual(before - 3, weapon.CurrentDurability,
                "Three-shot durability must apply once at completion");

            DRPGBattleWeaponActionContext melee =
                service.CaptureAction(actor, 102);
            for (int i = 0; i < 2; i++)
            {
                service.TryRecordCommittedUse(
                    melee,
                    DRPGWeaponUseKind.MeleeCommittedSwing);
            }
            DRPGBattleWeaponActionCompletion meleeCompletion =
                service.CompleteAction(actor, melee);
            AssertEqual(2, meleeCompletion.AppliedUseUnits,
                "Two committed melee swings must cost two units");

            DRPGBattleWeaponActionContext freeStrike =
                service.CaptureAction(actor, 103);
            service.TryRecordCommittedUse(
                freeStrike,
                DRPGWeaponUseKind.FreeStrikeInput);
            service.TryRecordCommittedUse(
                freeStrike,
                DRPGWeaponUseKind.FreeStrikeInput);
            DRPGBattleWeaponActionCompletion freeCompletion =
                service.CompleteAction(actor, freeStrike);
            AssertEqual(2, freeCompletion.AppliedUseUnits,
                "Each free-strike input must cost once regardless of contact count");

            int beforeCancel = weapon.CurrentDurability;
            DRPGBattleWeaponActionContext cancelled =
                service.CaptureAction(actor, 104);
            service.TryRecordCommittedUse(
                cancelled,
                DRPGWeaponUseKind.FirearmShot);
            service.CancelAction(cancelled);
            AssertEqual(beforeCancel, weapon.CurrentDurability,
                "A cancelled action must not apply pending durability");
        }
        finally
        {
            DestroyAll(definition, profile, basic);
        }
    }

    private static void VerifyOrdinaryBreakAtBattleActionCompletion()
    {
        DRPGSkillSO professionBasic = CreateSkill("ordinary_profession_basic", 1f);
        DRPGSkillSO weaponBasic = CreateSkill("ordinary_weapon_basic", 200f);
        DRPGSkillSO intrinsic = CreateSkill("ordinary_intrinsic", 1f);
        var configured = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        for (int i = 0; i < configured.Length; i++)
            configured[i] = CreateSkill("ordinary_configured_" + i, 1f);
        SetField(configured[0], "requiresFunctionalWeapon", true);
        DRPGCombatantProfileSO profile = CreateProfile(
            "ordinary_break_actor",
            professionBasic,
            configured);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(
            "ordinary_break_enemy",
            professionBasic,
            Array.Empty<DRPGSkillSO>());
        DRPGWeaponDefinitionSO definition = CreateWeaponDefinition(
            "ordinary_break_weapon",
            DRPGWeaponOwnershipKind.General,
            null,
            1,
            weaponBasic,
            intrinsic);

        try
        {
            DRPGWeaponInstance weapon = DRPGWeaponInstance.Create(
                "ordinary_break_instance",
                definition,
                new MinimumRandom());
            var inventory = new DRPGPartyInventoryState(20, 100f);
            var root = new DRPGEquipmentRoot(inventory);
            var equipment = new DRPGCharacterEquipmentState(
                profile.CombatantId,
                weapon);
            AssertTrue(root.TryRegisterCharacter(profile, equipment).Success,
                "The ordinary weapon must enter equipped ownership");
            var progression = new DRPGProgressionService();
            var durability = new DRPGBattleWeaponDurabilityService(
                root,
                progression);
            BattleParticipantRuntime player = CreateActor(
                11,
                profile,
                progression.ResolveBuild(
                    profile,
                    DRPGResolvedEquipmentSnapshot.FromWeapon(weapon, string.Empty)));
            BattleParticipantRuntime enemy = CreateEnemy(12, enemyProfile);
            var session = DRPGBattleHeadlessSessionFactory.Create(
                new[] { player, enemy },
                weaponDurabilityService: durability);
            int breakEvents = 0;
            DRPGBattleWeaponBreakEvent breakEvent = null;
            session.WeaponBroken += value =>
            {
                breakEvents++;
                breakEvent = value;
            };

            session.Start();
            AssertTrue(player.Build.ContainsBattleSkill(intrinsic.SkillId),
                "Intrinsic skill must exist before the breaking action");
            DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                player.RuntimeId,
                enemy.RuntimeId,
                player.Build.BasicAttack);
            AssertTrue(session.TryPrepareCommand(command, out var action, out var error),
                error);
            AssertTrue(session.TryResolveImpact(action, out _, out error), error);
            AssertEqual(1, weapon.CurrentDurability,
                "Impact resolution must not spend durability before completion");
            AssertTrue(player.Build.ContainsBattleSkill(intrinsic.SkillId),
                "The breaking action must retain its intrinsic skill until completion");

            AssertTrue(session.TryCompleteAction(action, out error), error);
            AssertEqual(0, weapon.CurrentDurability,
                "The completed attack must cross durability to zero");
            AssertNull(equipment.EquippedWeapon,
                "A broken ordinary weapon must leave the equipment slot");
            AssertEqual(1, inventory.GetWeaponFragmentCount(definition.WeaponId),
                "A broken ordinary weapon must create one fragment entry");
            AssertFalse(player.Build.ContainsBattleSkill(intrinsic.SkillId),
                "Intrinsic skill must disappear after the breaking action completes");
            AssertSame(professionBasic, player.Build.BasicAttack,
                "Ordinary break must rebuild the actor with basic fallback");
            AssertEqual(DRPGSkillLoadout.SlotCount, player.Build.ConfiguredSkills.Count,
                "The four configured profession slots must remain stored");
            AssertSame(configured[0], player.Build.ConfiguredSkills[0],
                "A weapon-dependent configured card must stay in its saved slot");
            AssertFalse(player.Build.ContainsBattleSkill(configured[0].SkillId),
                "An unusable weapon-dependent card must be filtered from BattleSkills");
            AssertEqual(1, breakEvents,
                "A zero transition must publish one break event");
            AssertEqual(weapon.InstanceId, breakEvent.WeaponInstanceId,
                "Break event must identify the captured weapon instance");
            AssertEqual(
                DRPGWeaponBreakDisposition.DestroyIntoFragments,
                breakEvent.Disposition,
                "Ordinary break event must report fragment destruction");
        }
        finally
        {
            var objects = new List<UnityEngine.Object>
            {
                definition,
                enemyProfile,
                profile,
                intrinsic,
                weaponBasic,
                professionBasic
            };
            objects.AddRange(configured);
            DestroyAll(objects.ToArray());
        }
    }

    private static void VerifySignatureBreakFallback()
    {
        DRPGSkillSO professionBasic = CreateSkill("signature_profession_basic", 1f);
        DRPGSkillSO weaponBasic = CreateSkill("signature_weapon_basic", 1f);
        DRPGSkillSO intrinsic = CreateSkill("signature_intrinsic", 1f);
        DRPGCombatantProfileSO profile = CreateProfile(
            "signature_owner",
            professionBasic,
            Array.Empty<DRPGSkillSO>());
        DRPGWeaponDefinitionSO definition = CreateWeaponDefinition(
            "signature_break_weapon",
            DRPGWeaponOwnershipKind.Signature,
            profile.CombatantId,
            1,
            weaponBasic,
            intrinsic);

        try
        {
            DRPGWeaponInstance weapon = DRPGWeaponInstance.Create(
                "signature_break_instance",
                definition,
                new MinimumRandom());
            var inventory = new DRPGPartyInventoryState(20, 100f);
            var root = new DRPGEquipmentRoot(inventory);
            var equipment = new DRPGCharacterEquipmentState(
                profile.CombatantId,
                weapon);
            AssertTrue(root.TryRegisterCharacter(profile, equipment).Success,
                "The signature weapon must register");
            var progression = new DRPGProgressionService();
            var service = new DRPGBattleWeaponDurabilityService(root, progression);
            BattleParticipantRuntime actor = CreateActor(
                21,
                profile,
                progression.ResolveBuild(
                    profile,
                    DRPGResolvedEquipmentSnapshot.FromWeapon(weapon, string.Empty)));
            DRPGBattleWeaponActionContext context =
                service.CaptureAction(actor, 201);
            service.TryRecordCommittedUse(
                context,
                DRPGWeaponUseKind.MeleeCommittedSwing);
            DRPGBattleWeaponActionCompletion completion =
                service.CompleteAction(actor, context);

            AssertTrue(completion.Success && completion.BrokeThisCompletion,
                "Signature action must report its zero transition");
            AssertSame(weapon, equipment.EquippedWeapon,
                "A signature weapon must remain equipped at zero durability");
            AssertFalse(completion.ReplacementBuild.ContainsBattleSkill(
                    intrinsic.SkillId),
                "Broken signature intrinsic skill must be suppressed");
            AssertSame(professionBasic, completion.ReplacementBuild.BasicAttack,
                "Broken signature weapon must use basic fallback");
            AssertEqual(0, inventory.GetWeaponFragmentCount(definition.WeaponId),
                "A signature weapon must never create fragments");
        }
        finally
        {
            DestroyAll(definition, profile, intrinsic, weaponBasic, professionBasic);
        }
    }

    private static BattleParticipantRuntime CreateActor(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGResolvedCombatantBuild build)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0,
                build: build));
    }

    private static BattleParticipantRuntime CreateEnemy(
        int runtimeId,
        DRPGCombatantProfileSO profile)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(
                profile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0));
    }

    private static DRPGCombatantProfileSO CreateProfile(
        string id,
        DRPGSkillSO basic,
        IReadOnlyList<DRPGSkillSO> configured)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        var skills = new List<DRPGSkillSO> { basic };
        if (configured != null)
            skills.AddRange(configured);
        SetField(profile, "combatantId", id);
        SetField(profile, "maxHp", 100f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "baseTimelineDelay", 0f);
        SetField(profile, "baseRecoveryDelay", 100f);
        SetField(profile, "skills", skills);
        return profile;
    }

    private static DRPGSkillSO CreateSkill(string id, float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        return skill;
    }

    private static DRPGWeaponDefinitionSO CreateWeaponDefinition(
        string id,
        DRPGWeaponOwnershipKind ownership,
        string ownerId,
        int durability,
        DRPGSkillSO basicOverride,
        DRPGSkillSO intrinsic)
    {
        DRPGWeaponDefinitionSO definition =
            ScriptableObject.CreateInstance<DRPGWeaponDefinitionSO>();
        SetField(definition, "weaponId", id);
        SetField(definition, "ownershipKind", ownership);
        SetField(definition, "signatureCharacterId", ownerId);
        SetField(definition, "baseDurability", durability);
        SetField(definition, "rarity", DRPGEquipmentRarity.RedGold);
        SetField(definition, "basicAttackOverride", basicOverride);
        SetField(definition, "intrinsicSkill", intrinsic);
        SetField(definition, "attackPresentationKey", "weapon:" + id);
        return definition;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void DestroyAll(params UnityEngine.Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertNull(object value, string message)
    {
        if (value != null)
            throw new Exception(message);
    }

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }
}
#endif
