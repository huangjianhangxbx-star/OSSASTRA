using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DRPGGameLoopView : MonoBehaviour, IDRPGGameLoopView
{
    [Header("Roots")]
    [SerializeField] private GameObject titleRoot;
    [SerializeField] private GameObject strategicHubRoot;
    [SerializeField] private GameObject hubHomeRoot;
    [SerializeField] private GameObject dungeonSelectionRoot;
    [SerializeField] private GameObject settlementRoot;
    [Header("Title")]
    [SerializeField] private UnityEngine.UI.Button startButton;
    [Header("Strategic Hub")]
    [SerializeField] private UnityEngine.UI.Button openDungeonSelectionButton;
    [SerializeField] private UnityEngine.UI.Button testExpeditionButton;
    [SerializeField] private UnityEngine.UI.Button hubBackButton;
    [SerializeField] private UnityEngine.UI.Button returnToTitleButton;
    [Header("Settlement")]
    [SerializeField] private Transform experienceContent;
    [SerializeField] private DRPGSettlementExperienceRowView experienceTemplate;
    [SerializeField] private Transform lootContent;
    [SerializeField] private DRPGLootSlotView lootTemplate;
    [SerializeField] private Transform backpackContent;
    [SerializeField] private DRPGLootSlotView backpackTemplate;
    [SerializeField] private TMP_Text lootEmptyText;
    [SerializeField] private TMP_Text backpackCapacityText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private UnityEngine.UI.Button collectAllButton;
    [SerializeField] private UnityEngine.UI.Button continueButton;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private DRPGGameLoopRuntimeRoot owner;

    public void Configure(DRPGGameLoopRuntimeRoot gameLoop)
    {
        owner = gameLoop;
        startButton?.onClick.RemoveListener(OnStartClicked);
        openDungeonSelectionButton?.onClick.RemoveListener(
            OnOpenDungeonSelectionClicked);
        testExpeditionButton?.onClick.RemoveListener(
            OnTestExpeditionClicked);
        hubBackButton?.onClick.RemoveListener(OnHubBackClicked);
        returnToTitleButton?.onClick.RemoveListener(OnReturnToTitleClicked);
        collectAllButton?.onClick.RemoveListener(OnCollectAllClicked);
        continueButton?.onClick.RemoveListener(OnContinueClicked);
        startButton?.onClick.AddListener(OnStartClicked);
        openDungeonSelectionButton?.onClick.AddListener(
            OnOpenDungeonSelectionClicked);
        testExpeditionButton?.onClick.AddListener(OnTestExpeditionClicked);
        hubBackButton?.onClick.AddListener(OnHubBackClicked);
        returnToTitleButton?.onClick.AddListener(OnReturnToTitleClicked);
        collectAllButton?.onClick.AddListener(OnCollectAllClicked);
        continueButton?.onClick.AddListener(OnContinueClicked);
    }

    public void ShowTitle()
    {
        gameObject.SetActive(true);
        if (titleRoot != null)
            titleRoot.SetActive(true);
        if (strategicHubRoot != null)
            strategicHubRoot.SetActive(false);
        if (settlementRoot != null)
            settlementRoot.SetActive(false);
    }

    public void ShowStrategicHub(StrategicHubSnapshot snapshot)
    {
        gameObject.SetActive(true);
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (settlementRoot != null)
            settlementRoot.SetActive(false);
        if (strategicHubRoot != null)
            strategicHubRoot.SetActive(true);

        bool showDungeonSelection = snapshot != null &&
            snapshot.Route.Kind == StrategicHubRouteKind.DungeonSelection;
        if (hubHomeRoot != null)
            hubHomeRoot.SetActive(!showDungeonSelection);
        if (dungeonSelectionRoot != null)
            dungeonSelectionRoot.SetActive(showDungeonSelection);
    }

    public void HideAll()
    {
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (strategicHubRoot != null)
            strategicHubRoot.SetActive(false);
        if (hubHomeRoot != null)
            hubHomeRoot.SetActive(false);
        if (dungeonSelectionRoot != null)
            dungeonSelectionRoot.SetActive(false);
        if (settlementRoot != null)
            settlementRoot.SetActive(false);
    }

    public void ShowSettlement(
        DRPGVictorySettlement settlement,
        DRPGPartyInventoryState inventory)
    {
        gameObject.SetActive(true);
        if (titleRoot != null)
            titleRoot.SetActive(false);
        if (strategicHubRoot != null)
            strategicHubRoot.SetActive(false);
        if (settlementRoot != null)
            settlementRoot.SetActive(true);
        RefreshSettlement(settlement, inventory, string.Empty);
    }

    public void RefreshSettlement(
        DRPGVictorySettlement settlement,
        DRPGPartyInventoryState inventory,
        string feedback)
    {
        ClearSpawned();
        if (settlement != null)
        {
            for (int i = 0; i < settlement.ExperienceAwards.Count; i++)
            {
                DRPGSettlementExperienceRowView row = Instantiate(
                    experienceTemplate,
                    experienceContent);
                row.gameObject.SetActive(true);
                row.Bind(settlement.ExperienceAwards[i]);
                spawned.Add(row.gameObject);
            }

            IReadOnlyList<DRPGLootStackState> loot = settlement.Loot.Stacks;
            for (int i = 0; i < loot.Count; i++)
            {
                DRPGLootSlotView slot = Instantiate(lootTemplate, lootContent);
                slot.gameObject.SetActive(true);
                slot.Bind(owner, loot[i], true);
                spawned.Add(slot.gameObject);
            }
            if (lootEmptyText != null)
                lootEmptyText.gameObject.SetActive(loot.Count == 0);
        }

        IReadOnlyList<DRPGLootStackState> backpack = inventory != null
            ? inventory.BackpackLootStacks
            : null;
        if (backpack != null)
        {
            for (int i = 0; i < backpack.Count; i++)
            {
                DRPGLootSlotView slot = Instantiate(
                    backpackTemplate,
                    backpackContent);
                slot.gameObject.SetActive(true);
                slot.Bind(owner, backpack[i], false);
                spawned.Add(slot.gameObject);
            }
        }

        if (backpackCapacityText != null)
        {
            backpackCapacityText.text = inventory != null
                ? "SLOT " + inventory.UsedSlots + "/" +
                    inventory.SlotCapacity + "    LOAD " +
                    inventory.CurrentWeight.ToString("0.0") + "/" +
                    inventory.WeightCapacity.ToString("0.0")
                : "BACKPACK UNAVAILABLE";
        }
        feedbackText?.SetText(feedback ?? string.Empty);
        if (collectAllButton != null)
        {
            collectAllButton.interactable = settlement != null &&
                !settlement.Loot.IsEmpty && inventory != null;
        }
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    private void OnStartClicked()
    {
        owner?.StartGame();
    }

    private void OnOpenDungeonSelectionClicked()
    {
        owner?.OpenDungeonSelection();
    }

    private void OnTestExpeditionClicked()
    {
        owner?.LaunchExpedition(
            new ExpeditionStartRequest(new ExpeditionId("dungeon-a")));
    }

    private void OnHubBackClicked()
    {
        owner?.BackStrategicHub();
    }

    private void OnReturnToTitleClicked()
    {
        owner?.ReturnToTitle();
    }

    private void OnCollectAllClicked()
    {
        owner?.CollectAllLoot();
    }

    private void OnContinueClicked()
    {
        owner?.CompleteSettlement();
    }

    private void OnDestroy()
    {
        startButton?.onClick.RemoveListener(OnStartClicked);
        openDungeonSelectionButton?.onClick.RemoveListener(
            OnOpenDungeonSelectionClicked);
        testExpeditionButton?.onClick.RemoveListener(
            OnTestExpeditionClicked);
        hubBackButton?.onClick.RemoveListener(OnHubBackClicked);
        returnToTitleButton?.onClick.RemoveListener(OnReturnToTitleClicked);
        collectAllButton?.onClick.RemoveListener(OnCollectAllClicked);
        continueButton?.onClick.RemoveListener(OnContinueClicked);
    }
}
