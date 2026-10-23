using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 敌人是谁、使用哪个战斗背景、采用哪套表现配置
/// </summary>

[Serializable]
public sealed class DRPGEnemyDeploymentDefinition
{
    [SerializeField] private DRPGCombatantProfileSO profile;
    [SerializeField] private DRPGFormationRow preferredRow =
        DRPGFormationRow.Front;

    public DRPGEnemyDeploymentDefinition()
    {
    }

    public DRPGEnemyDeploymentDefinition(
        DRPGCombatantProfileSO profile,
        DRPGFormationRow preferredRow)
    {
        this.profile = profile;
        this.preferredRow = preferredRow;
    }

    public DRPGCombatantProfileSO Profile => profile;
    public DRPGFormationRow PreferredRow => preferredRow;
}

[Serializable]
public sealed class DRPGLootDropDefinition
{
    [SerializeField] private DRPGLootItemDefinitionSO item;
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 1f;
    [Min(1)]
    [SerializeField] private int minimumQuantity = 1;
    [Min(1)]
    [SerializeField] private int maximumQuantity = 1;

    public DRPGLootDropDefinition()
    {
    }

    public DRPGLootDropDefinition(
        DRPGLootItemDefinitionSO item,
        float dropChance,
        int minimumQuantity,
        int maximumQuantity)
    {
        this.item = item;
        this.dropChance = dropChance;
        this.minimumQuantity = minimumQuantity;
        this.maximumQuantity = maximumQuantity;
    }

    public DRPGLootItemDefinitionSO Item => item;
    public float DropChance => Mathf.Clamp01(dropChance);
    public int MinimumQuantity => Mathf.Max(1, minimumQuantity);
    public int MaximumQuantity => Mathf.Max(MinimumQuantity, maximumQuantity);
}

[CreateAssetMenu(
    fileName = "DRPGEncounter",
    menuName = "ANKALUTE/DRPG/Encounter")]
public sealed class DRPGEncounterDefinitionSO : ScriptableObject
{
    [SerializeField] private string encounterId = "encounter";
    [SerializeField] private List<DRPGCombatantProfileSO> enemies =
        new List<DRPGCombatantProfileSO>();
    [Header("Formation deployment")]
    [Tooltip("When populated, this replaces the legacy enemy list and allows repeated profiles with different row preferences.")]
    [SerializeField] private List<DRPGEnemyDeploymentDefinition> enemyDeployments =
        new List<DRPGEnemyDeploymentDefinition>();
    [SerializeField] private DRPGFormationRules formationRules =
        new DRPGFormationRules();
    [SerializeField] private DRPGEnemyWavePolicy enemyWavePolicy;
    [SerializeField] private Sprite battleBackground;
    [SerializeField] private DRPGBattlePresentationSO presentation;
    [Header("Victory rewards")]
    [Min(0)]
    [SerializeField] private long experienceRewardPerCharacter = 100L;
    [SerializeField] private List<DRPGLootDropDefinition> lootDrops =
        new List<DRPGLootDropDefinition>();
    [Header("Escape")]
    [SerializeField] private bool allowEscape = true;
    [Range(0f, 1f)]
    [SerializeField] private float baseEscapeChance = 0.5f;

    public string EncounterId => encounterId;
    public IReadOnlyList<DRPGCombatantProfileSO> Enemies => enemies;
    public DRPGFormationRules FormationRules =>
        formationRules ?? DRPGFormationRules.Default;
    public Sprite BattleBackground => battleBackground;
    public DRPGBattlePresentationSO Presentation => presentation;
    public long ExperienceRewardPerCharacter =>
        Math.Max(0L, experienceRewardPerCharacter);
    public IReadOnlyList<DRPGLootDropDefinition> LootDrops => lootDrops;
    public bool AllowEscape => allowEscape;
    public float BaseEscapeChance => Mathf.Clamp01(baseEscapeChance);
    public DRPGBattleEscapeRules EscapeRules =>
        new DRPGBattleEscapeRules(AllowEscape, BaseEscapeChance);

    internal DRPGEnemyWavePolicySnapshot FreezeWavePolicy(double defaultAnnouncement, double defaultStagger) =>
        enemyWavePolicy.Freeze(FormationRules.ActiveEnemyLimit, CreateResolvedEnemyDeployments().Count,
            defaultAnnouncement, defaultStagger);

    public IReadOnlyList<DRPGEnemyDeploymentDefinition>
        CreateResolvedEnemyDeployments()
    {
        if (enemyDeployments != null && enemyDeployments.Count > 0)
            return enemyDeployments.ToArray();

        var resolved = new DRPGEnemyDeploymentDefinition[enemies.Count];
        for (int i = 0; i < enemies.Count; i++)
        {
            DRPGCombatantProfileSO profile = enemies[i];
            resolved[i] = new DRPGEnemyDeploymentDefinition(
                profile,
                profile != null
                    ? profile.PreferredFormationRow
                    : DRPGFormationRow.Front);
        }
        return Array.AsReadOnly(resolved);
    }
}
