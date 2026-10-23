using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls top-level page navigation for the Package UI.
/// Currently only Equipment page is active; others are placeholders.
/// </summary>
public class PackagePageController : MonoBehaviour
{
    public enum PackagePageType
    {
        Equipment,
        CharacterStatus,
        Skills,
        Quests
    }

    [Header("Tab Buttons")]
    [SerializeField] private Button equipmentTab;
    [SerializeField] private Button characterStatusTab;
    [SerializeField] private Button skillsTab;
    [SerializeField] private Button questsTab;

    [Header("Page Containers")]
    [SerializeField] private GameObject equipmentPage;
    [SerializeField] private GameObject characterStatusPage;
    [SerializeField] private GameObject skillsPage;
    [SerializeField] private GameObject questsPage;

    [Header("Colors")]
    [SerializeField] private Color activeTabColor = new Color(0.2f, 0.35f, 0.6f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.12f, 0.12f, 0.15f, 0.8f);
    [SerializeField] private Color disabledTabColor = new Color(0.08f, 0.08f, 0.1f, 0.6f);

    private PackagePageType _currentPage = PackagePageType.Equipment;

    private void Awake()
    {
        // Wire button clicks
        if (equipmentTab != null)
            equipmentTab.onClick.AddListener(() => SwitchToPage(PackagePageType.Equipment));

        // Disable unimplemented pages
        DisableTab(characterStatusTab);
        DisableTab(skillsTab);
        DisableTab(questsTab);

        // Show only equipment page
        ShowPage(PackagePageType.Equipment);
    }

    private void DisableTab(Button tab)
    {
        if (tab == null) return;
        tab.interactable = false;
        var img = tab.GetComponent<Image>();
        if (img != null) img.color = disabledTabColor;
    }

    private void SwitchToPage(PackagePageType page)
    {
        if (page != PackagePageType.Equipment) return; // Only equipment implemented
        ShowPage(page);
    }

    private void ShowPage(PackagePageType page)
    {
        _currentPage = page;

        SetPageActive(equipmentPage, page == PackagePageType.Equipment);
        SetPageActive(characterStatusPage, page == PackagePageType.CharacterStatus);
        SetPageActive(skillsPage, page == PackagePageType.Skills);
        SetPageActive(questsPage, page == PackagePageType.Quests);

        UpdateTabColors();
    }

    private void SetPageActive(GameObject pageObj, bool active)
    {
        if (pageObj == null) return;
        pageObj.SetActive(active);
    }

    private void UpdateTabColors()
    {
        UpdateTabColor(equipmentTab, _currentPage == PackagePageType.Equipment);
    }

    private void UpdateTabColor(Button tab, bool isActive)
    {
        if (tab == null || !tab.interactable) return;
        var img = tab.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeTabColor : inactiveTabColor;
    }
}
