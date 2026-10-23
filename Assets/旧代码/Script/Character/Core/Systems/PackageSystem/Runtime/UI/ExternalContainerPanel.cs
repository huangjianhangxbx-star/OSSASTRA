using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the right-side external container panel.
/// Only visible when a scene container (chest, shop, etc.) is opened.
/// </summary>
public class ExternalContainerPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI containerNameText;
    [SerializeField] private TextMeshProUGUI capacityText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform gridContainer;

    private IItemContainer _container;

    public bool IsOpen => _container != null;

    private void Awake()
    {
        // Ensure panel background doesn't block raycasts when hidden
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseExternalContainer);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Opens the panel and binds it to an external container.
    /// </summary>
    public void OpenExternalContainer(IItemContainer container, string title)
    {
        if (container == null) return;

        _container = container;
        gameObject.SetActive(true);

        if (containerNameText != null)
            containerNameText.text = title;

        if (capacityText != null)
            capacityText.text = $"0 / {container.Capacity}";

        // TODO: Populate grid with container items (read-only for now)
    }

    /// <summary>
    /// Closes the panel and clears the container reference.
    /// </summary>
    public void CloseExternalContainer()
    {
        _container = null;
        gameObject.SetActive(false);
    }
}
