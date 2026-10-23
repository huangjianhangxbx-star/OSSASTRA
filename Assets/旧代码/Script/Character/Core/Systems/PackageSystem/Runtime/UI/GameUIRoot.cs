using UnityEngine;

/// <summary>
/// The single scene-level owner for gameplay overlays. Package and game-menu
/// canvases live here rather than under a character instance.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class GameUIRoot : MonoBehaviour
{
    public static GameUIRoot Instance { get; private set; }

    [SerializeField] private PackageUIController packageUI;
    [SerializeField] private GameMenuController gameMenu;
    [SerializeField] private DRPGPartyBackpackController drpgBackpack;

    private readonly GameUIModalCoordinator modalCoordinator =
        new GameUIModalCoordinator();

    public PackageUIController PackageUI => packageUI;
    public GameMenuController GameMenu => gameMenu;
    public DRPGPartyBackpackController DRPGBackpack => drpgBackpack;
    public GameUIModalCoordinator ModalCoordinator => modalCoordinator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                $"[GameUIRoot] Duplicate scene instance destroyed: {name}",
                this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GameMenuInputRouter router = GetComponent<GameMenuInputRouter>();
        if (router == null)
            router = gameObject.AddComponent<GameMenuInputRouter>();
        router.Initialize(this);

        DRPGPartyBackpackInputRouter backpackRouter =
            GetComponent<DRPGPartyBackpackInputRouter>();
        if (backpackRouter == null)
            backpackRouter = gameObject.AddComponent<DRPGPartyBackpackInputRouter>();
        backpackRouter.Initialize(this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevDebugHubInputRouter debugRouter =
            GetComponent<DevDebugHubInputRouter>();
        if (debugRouter == null)
            debugRouter = gameObject.AddComponent<DevDebugHubInputRouter>();
        debugRouter.Initialize(this);
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public PackageUIController GetOrCreatePackageUI(GameObject prefab)
    {
        if (packageUI != null)
            return packageUI;
        packageUI = InstantiateController<PackageUIController>(
            prefab,
            "PackageUI");
        return packageUI;
    }

    public GameMenuController GetOrCreateGameMenu(GameObject prefab)
    {
        if (gameMenu != null)
            return gameMenu;
        gameMenu = InstantiateController<GameMenuController>(prefab, "GameMenu");
        return gameMenu;
    }

    public DRPGPartyBackpackController GetOrCreateDRPGBackpack(GameObject prefab)
    {
        if (drpgBackpack != null) return drpgBackpack;
        if (prefab != null)
        {
            drpgBackpack = InstantiateController<DRPGPartyBackpackController>(
                prefab, "DRPGPartyBackpack");
            return drpgBackpack;
        }
        var fallback = new GameObject("DRPGPartyBackpack",
            typeof(RectTransform), typeof(DRPGPartyBackpackController));
        fallback.transform.SetParent(transform, false);
        drpgBackpack = fallback.GetComponent<DRPGPartyBackpackController>();
        return drpgBackpack;
    }

    private T InstantiateController<T>(GameObject prefab, string label)
        where T : Component
    {
        if (prefab == null)
        {
            Debug.LogError($"[GameUIRoot] Cannot create {label}: prefab is null.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, transform, false);
        T controller = instance.GetComponent<T>();
        if (controller != null)
            return controller;

        Debug.LogError(
            $"[GameUIRoot] {label} prefab root requires {typeof(T).Name}.",
            instance);
        Destroy(instance);
        return null;
    }
}
