#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleCommandKindsProbe
{
    public static string Run()
    {
        DRPGSkillSO basic = CreateSkill("probe_basic", 20f);
        DRPGSkillSO equipped = CreateSkill("probe_equipped", 30f);
        DRPGSkillSO locked = CreateSkill("probe_locked", 99f);
        DRPGBattleItemDefinition medicine =
            ScriptableObject.CreateInstance<DRPGBattleItemDefinition>();
        DRPGCombatantProfileSO playerProfile = CreateProfile("command_player", 50f);
        // Keep the player slightly faster so they open the battle, while still
        // allowing the enemy's already-scheduled turn to occur before the
        // player's recovery turn. This makes the defend lifetime assertion
        // exercise an actual intervening enemy action.
        DRPGCombatantProfileSO enemyProfile = CreateProfile("command_enemy", 40f);

        try
        {
            SetField(medicine, "itemId", "probe_medicine");
            SetField(medicine, "healAmount", 40f);

            DRPGResolvedCombatantBuild playerBuild = CreateBuild(
                playerProfile,
                basic,
                equipped);
            DRPGResolvedCombatantBuild enemyBuild = CreateBuild(
                enemyProfile,
                basic);

            VerifyAttackKinds(playerProfile, enemyProfile, playerBuild, enemyBuild,
                basic, equipped, locked);
            VerifyDefend(playerProfile, enemyProfile, playerBuild, enemyBuild, basic);
            VerifyItem(playerProfile, enemyProfile, playerBuild, enemyBuild, medicine);
            VerifyEscape(playerProfile, enemyProfile, playerBuild, enemyBuild);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(medicine);
            UnityEngine.Object.DestroyImmediate(locked);
            UnityEngine.Object.DestroyImmediate(equipped);
            UnityEngine.Object.DestroyImmediate(basic);
        }

        return "PASS five DRPG command kinds";
    }

    private static void VerifyAttackKinds(
        DRPGCombatantProfileSO playerProfile,
        DRPGCombatantProfileSO enemyProfile,
        DRPGResolvedCombatantBuild playerBuild,
        DRPGResolvedCombatantBuild enemyBuild,
        DRPGSkillSO basic,
        DRPGSkillSO equipped,
        DRPGSkillSO locked)
    {
        DRPGBattleSession basicSession = CreateSession(
            playerProfile, enemyProfile, playerBuild, enemyBuild);
        basicSession.Start();
        float before = basicSession.Participants[1].CurrentHp;
        AssertTrue(
            basicSession.TrySubmitCommand(
                DRPGBattleCommand.BasicAttack(1, 2, basic),
                out string basicError),
            basicError);
        AssertTrue(basicSession.Participants[1].CurrentHp < before,
            "Basic attack must deal damage through the ordinary transaction");

        DRPGBattleSession skillSession = CreateSession(
            playerProfile, enemyProfile, playerBuild, enemyBuild);
        skillSession.Start();
        AssertFalse(
            skillSession.TrySubmitCommand(
                DRPGBattleCommand.UseSkill(1, 2, locked),
                out _),
            "A skill outside the four-slot Build must be rejected");
        AssertTrue(
            skillSession.TrySubmitCommand(
                DRPGBattleCommand.UseSkill(1, 2, equipped),
                out string skillError),
            skillError);
    }

    private static void VerifyDefend(
        DRPGCombatantProfileSO playerProfile,
        DRPGCombatantProfileSO enemyProfile,
        DRPGResolvedCombatantBuild playerBuild,
        DRPGResolvedCombatantBuild enemyBuild,
        DRPGSkillSO enemyAttack)
    {
        DRPGBattleSession session = CreateSession(
            playerProfile, enemyProfile, playerBuild, enemyBuild);
        session.Start();
        AssertTrue(session.TrySubmitCommand(
            DRPGBattleCommand.Defend(1), out string defendError), defendError);
        BattleParticipantRuntime player = session.Participants[0];
        if (session.CurrentActor.Side == DRPGBattleSide.Player)
        {
            AssertTrue(session.TrySubmitCommand(
                DRPGBattleCommand.Defend(session.CurrentActor.RuntimeId),
                out defendError), defendError);
        }
        AssertEqual(2, session.CurrentActor.RuntimeId,
            "The defend probe requires an intervening enemy turn");
        AssertTrue(player.IsDefending,
            "Defend must remain active during enemy actions");
        float before = player.CurrentHp;
        AssertTrue(session.TrySubmitCommand(
            DRPGBattleCommand.BasicAttack(2, 1, enemyAttack),
            out string attackError), attackError);
        AssertEqual(10f, before - player.CurrentHp,
            "Ordinary defend must halve the next incoming 20 damage");
        AssertFalse(player.IsDefending,
            "Ordinary defend must clear when the defender's next turn begins");
    }

    private static void VerifyItem(
        DRPGCombatantProfileSO playerProfile,
        DRPGCombatantProfileSO enemyProfile,
        DRPGResolvedCombatantBuild playerBuild,
        DRPGResolvedCombatantBuild enemyBuild,
        DRPGBattleItemDefinition medicine)
    {
        var source = new MemoryItemSource(medicine, 1);
        DRPGBattleSession session = CreateSession(
            playerProfile,
            enemyProfile,
            playerBuild,
            enemyBuild,
            source,
            initialPlayerHitPoints: 40f);
        BattleParticipantRuntime player = session.Participants[0];
        session.Start();
        AssertTrue(session.TrySubmitCommand(
            DRPGBattleCommand.Item(1, 1, medicine),
            out string itemError), itemError);
        AssertEqual(80f, player.CurrentHp,
            "The healing item must apply its configured amount once");
        AssertEqual(0, source.GetQuantity(medicine),
            "A successful item command must consume exactly one item");
    }

    private static void VerifyEscape(
        DRPGCombatantProfileSO playerProfile,
        DRPGCombatantProfileSO enemyProfile,
        DRPGResolvedCombatantBuild playerBuild,
        DRPGResolvedCombatantBuild enemyBuild)
    {
        DRPGBattleSession success = CreateSession(
            playerProfile,
            enemyProfile,
            playerBuild,
            enemyBuild,
            null,
            new DRPGBattleEscapeRules(true, 1f),
            new FixedRandom(0));
        success.Start();
        AssertTrue(success.TrySubmitCommand(
            DRPGBattleCommand.Escape(1), out string successError), successError);
        AssertEqual(DRPGBattleOutcome.Escaped, success.Outcome,
            "A successful escape must end battle through an explicit outcome");

        DRPGBattleSession failure = CreateSession(
            playerProfile,
            enemyProfile,
            playerBuild,
            enemyBuild,
            null,
            new DRPGBattleEscapeRules(true, 0f),
            new FixedRandom(0));
        failure.Start();
        AssertTrue(failure.TrySubmitCommand(
            DRPGBattleCommand.Escape(1), out string failureError), failureError);
        AssertEqual(DRPGBattleOutcome.None, failure.Outcome,
            "A failed escape must consume the turn without ending battle");
        if (failure.CurrentActor.Side == DRPGBattleSide.Player)
        {
            AssertTrue(failure.TrySubmitCommand(
                DRPGBattleCommand.Wait(failure.CurrentActor.RuntimeId),
                out failureError), failureError);
        }
        AssertEqual(2, failure.CurrentActor.RuntimeId,
            "A failed escape must advance to the next actor");
    }

    private static DRPGBattleSession CreateSession(
        DRPGCombatantProfileSO playerProfile,
        DRPGCombatantProfileSO enemyProfile,
        DRPGResolvedCombatantBuild playerBuild,
        DRPGResolvedCombatantBuild enemyBuild,
        IDRPGBattleItemSource itemSource = null,
        DRPGBattleEscapeRules? escapeRules = null,
        IDRPGBattleRandom random = null,
        float? initialPlayerHitPoints = null)
    {
        var player = new BattleParticipantRuntime(
            1,
            new BattleParticipantSpec(
                playerProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist,
                0,
                playerBuild));
        var enemy = new BattleParticipantRuntime(
            2,
            new BattleParticipantSpec(
                enemyProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy,
                0,
                enemyBuild));
        if (initialPlayerHitPoints.HasValue)
        {
            DRPGBattleStateMutationFixture.PrepareHitPoints(
                player,
                initialPlayerHitPoints.Value);
        }
        return DRPGBattleHeadlessSessionFactory.Create(
            new[] { player, enemy },
            randomSource: random,
            itemSource: itemSource,
            escapeRules: escapeRules);
    }

    private static DRPGResolvedCombatantBuild CreateBuild(
        DRPGCombatantProfileSO profile,
        DRPGSkillSO basic,
        DRPGSkillSO equipped = null)
    {
        var loadout = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        loadout[0] = equipped;
        return new DRPGResolvedCombatantBuild(
            profile,
            null,
            basic,
            loadout,
            equipped != null ? new[] { equipped } : Array.Empty<DRPGSkillSO>(),
            Array.Empty<DRPGPassiveModifierSO>());
    }

    private static DRPGSkillSO CreateSkill(string id, float power)
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(skill, "skillId", id);
        SetField(skill, "power", power);
        return skill;
    }

    private static DRPGCombatantProfileSO CreateProfile(string id, float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "combatantId", id);
        SetField(profile, "maxHp", 100f);
        SetField(profile, "agility", agility);
        return profile;
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

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }

    private sealed class MemoryItemSource : IDRPGBattleItemSource
    {
        private readonly DRPGBattleItemDefinition item;
        private int quantity;

        public MemoryItemSource(DRPGBattleItemDefinition item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }

        public IReadOnlyList<DRPGBattleItemDefinition> Items => new[] { item };

        public int GetQuantity(DRPGBattleItemDefinition definition) =>
            ReferenceEquals(item, definition) ? quantity : 0;

        public bool TryConsume(DRPGBattleItemDefinition definition, int amount)
        {
            if (!ReferenceEquals(item, definition) || amount <= 0 || quantity < amount)
                return false;
            quantity -= amount;
            return true;
        }
    }

    private sealed class FixedRandom : IDRPGBattleRandom
    {
        private readonly int value;
        public FixedRandom(int value) => this.value = value;
        public int Next(int exclusiveMax) => value;
    }
}
#endif
