using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main pickup controller. Placed on the character alongside CharacterPackageSystem.
/// Receives E-press events from InputHandler, manages candidate detection, and shows prompts.
/// The prompt lives inside a HUD Canvas (PickupPromptLayer) — never under this character's Transform.
/// </summary>
public class CharacterPickupController : MonoBehaviour, ICharacterSubsystem
{
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private WorldPickupPrompt promptPrefab;
    [Tooltip("若指定，Prompt 会实例化到该 HUD 提示图层下方。为空则自动查找/创建 PickupPromptLayer。")]
    [SerializeField] private RectTransform promptParent;

    private CharacterRoot _characterRoot;
    private CharacterPackageSystem _packageSystem;
    private PickupCandidateDetector _detector;
    private WorldPickupPrompt _promptInstance;
    private Camera _mainCamera;
    private bool _isInitialized;
    private bool _inputBlocked;

    public bool IsInitialized => _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized) return;
        _characterRoot = root;
        _packageSystem = root.characterPackageSystem;

        if (_packageSystem == null)
        {
            Debug.LogError("[CharacterPickupController] CharacterPackageSystem not found", this);
            return;
        }

        // Create dedicated trigger child for pickup detection.
        // Never touch the character's own collider (it's used for ground collision).
        var triggerGO = new GameObject("PickupTrigger");
        triggerGO.transform.SetParent(transform, false);
        triggerGO.transform.localPosition = Vector3.zero;
        var triggerCircle = triggerGO.AddComponent<CircleCollider2D>();
        triggerCircle.isTrigger = true;
        triggerCircle.radius = detectionRadius;

        _detector = triggerGO.AddComponent<PickupCandidateDetector>();

        _mainCamera = Camera.main;

        CreatePromptInstance();

        _isInitialized = true;
    }

    /// <summary>
    /// Creates the pickup prompt inside the HUD PickupPromptLayer.
    /// If a HUD layer already exists it is reused, otherwise a scene-level HUD Canvas is created.
    /// </summary>
    private void CreatePromptInstance()
    {
        Transform promptHost = ResolvePromptLayer();

        if (promptPrefab != null)
        {
            _promptInstance = Instantiate(promptPrefab, promptHost, false);
        }
        else
        {
            // Build a simple prompt UI at runtime inside the HUD layer.
            var promptGO = new GameObject("WorldPickupPrompt");
            promptGO.transform.SetParent(promptHost, false);
            var promptRT = promptGO.AddComponent<RectTransform>();
            promptRT.sizeDelta = new Vector2(140, 50);

            var cg = promptGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var bg = promptGO.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            bg.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(promptGO.transform, false);
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "[E]";
            text.fontSize = 14;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _promptInstance = promptGO.AddComponent<WorldPickupPrompt>();
        }

        if (_promptInstance != null)
            _promptInstance.Initialize(_mainCamera);
    }

    /// <summary>
    /// Locates an existing PickupPromptLayer, or creates a scene-level HUD Canvas containing one.
    /// Never falls back to this character's Transform.
    /// </summary>
    private Transform ResolvePromptLayer()
    {
        // Explicit inspector assignment takes priority.
        if (promptParent != null)
            return promptParent;

        // Reuse an existing HUD prompt layer.
        var layer = Object.FindObjectOfType<PickupPromptLayer>();
        if (layer != null)
            return layer.transform;

        // Host canvas — scene-level Screen Space Overlay, detached from any character.
        var canvasGO = new GameObject("HUDCanvas_Pickup");
        canvasGO.transform.position = Vector3.zero;
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // The dedicated prompt layer rectangle (full-screen stretch) inside the HUD Canvas.
        var layerGO = new GameObject("PickupPromptLayer");
        layerGO.transform.SetParent(canvasGO.transform, false);
        var layerRT = layerGO.AddComponent<RectTransform>();
        layerRT.anchorMin = Vector2.zero;
        layerRT.anchorMax = Vector2.one;
        layerRT.offsetMin = Vector2.zero;
        layerRT.offsetMax = Vector2.zero;
        layerGO.AddComponent<PickupPromptLayer>();

        return layerGO.transform;
    }

    /// <summary>
    /// Called by InputHandler when E is pressed. Input system routes this here.
    /// </summary>
    public void OnPickupPressed()
    {
        if (!_isInitialized || _inputBlocked) return;
        if (_detector == null || !_detector.HasCandidate) return;

        var candidate = _detector.CurrentBest;
        if (candidate == null || candidate.Instance == null) return;

        bool success = _packageSystem.TryPickupItem(candidate.Instance);

        if (success)
        {
            _detector.RemoveCandidate(candidate);
            candidate.OnPickedUp();
        }
        else
        {
            // Backpack full — keep world object, prompt will show "Backpack Full"
        }
    }

    /// <summary>
    /// Block/unblock pickup input when UI is open.
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        _inputBlocked = blocked;
        if (blocked && _promptInstance != null)
            _promptInstance.Hide();
    }

    private void Update()
    {
        if (!_isInitialized || _detector == null) return;
        if (_inputBlocked) return;

        if (_promptInstance == null) return;

        if (_detector.HasCandidate)
        {
            var candidate = _detector.CurrentBest;
            bool backpackFull = _packageSystem.Backpack != null && _packageSystem.Backpack.IsFull();
            _promptInstance.Show(
                candidate.PromptAnchor.position + Vector3.up * 0.5f,
                candidate.Instance?.DisplayName ?? "???",
                backpackFull);
        }
        else
        {
            _promptInstance.Hide();
        }
    }
}
