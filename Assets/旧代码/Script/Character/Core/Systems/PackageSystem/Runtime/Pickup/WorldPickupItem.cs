using UnityEngine;

/// <summary>
/// World-space pickup entity. Holds an ItemDefinitionSO and creates an ItemInstance on start.
/// If injected with an existing ItemInstance (e.g. player dropped weapon), preserves it.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class WorldPickupItem : MonoBehaviour
{
    [Header("Item Definition")]
    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField] private int quantity = 1;

    [Header("Prompt Anchor")]
    [SerializeField] private Transform promptAnchor;

    /// <summary>
    /// If set, this pickup was dropped by the player and should preserve the original instance.
    /// </summary>
    private ItemInstance _injectedInstance;

    public ItemInstance Instance { get; private set; }
    public ItemDefinitionSO Definition => itemDefinition;
    public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

    private void Awake()
    {
        // Ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Start()
    {
        if (_injectedInstance != null)
        {
            Instance = _injectedInstance;
        }
        else if (itemDefinition != null)
        {
            Instance = new ItemInstance(itemDefinition, quantity);
        }

        ApplyVisual();
    }

    /// <summary>
    /// Inject an existing ItemInstance (e.g. from player dropping a weapon).
    /// Must be called before Start().
    /// </summary>
    public void InjectInstance(ItemInstance instance)
    {
        _injectedInstance = instance;
        if (instance?.definition != null)
        {
            itemDefinition = instance.definition;
            quantity = instance.quantity;
        }
        Instance = instance;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = itemDefinition != null ? itemDefinition.InventoryIcon : null;
    }

    /// <summary>
    /// Called by CharacterPickupController when the item is successfully picked up.
    /// </summary>
    public void OnPickedUp()
    {
        Destroy(gameObject);
    }
}
