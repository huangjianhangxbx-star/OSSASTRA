using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Screen-space pickup prompt. Follows a world anchor position.
/// Lives inside a HUD Canvas (PickupPromptLayer), never under the character Transform.
/// Owned and instantiated by CharacterPickupController.
/// </summary>
public class WorldPickupPrompt : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private UnityEngine.UI.Image background;
    [SerializeField] private TMPro.TextMeshProUGUI labelText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color fullColor = new Color(0.6f, 0.1f, 0.1f, 0.85f);

    private Camera _mainCamera;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (background == null)
            background = GetComponentInChildren<UnityEngine.UI.Image>();
        if (labelText == null)
            labelText = GetComponentInChildren<TMPro.TextMeshProUGUI>();

        if (canvasGroup != null)
        {
            // Prompt 永不阻挡 UI Raycast
            canvasGroup.blocksRaycasts = false;
        }
        Hide();
    }

    public void Initialize(Camera cam)
    {
        _mainCamera = cam;
    }

    public void Show(Vector3 worldAnchor, string displayName, bool backpackFull)
    {
        if (_mainCamera == null) return;

        // 目标在相机后方（背面）时隐藏
        Vector3 viewport = _mainCamera.WorldToViewportPoint(worldAnchor);
        if (viewport.z < 0f)
        {
            Hide();
            return;
        }

        var parentRT = transform.parent as RectTransform;
        if (parentRT == null)
        {
            // 父节点必须是 RectTransform（位于某个 Canvas 图层内）
            Hide();
            return;
        }

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldAnchor);

        // 依 Canvas 渲染模式选择正确的 UI 相机：
        //  Screen Space - Overlay：UI 相机为 null
        //  Screen Space - Camera：Canvas.worldCamera
        Canvas canvas = GetComponentInParent<Canvas>();
        DecideUiCamera(canvas, out Camera uiCamera);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                screenPos,
                uiCamera,
                out Vector2 localPos))
        {
            // 转换失败（通常目标在屏幕外），隐藏
            Hide();
            return;
        }

        ((RectTransform)transform).anchoredPosition = localPos;

        if (labelText != null)
        {
            if (backpackFull)
                labelText.text = $"[E] {displayName}\n<color=#ff6666>Backpack Full</color>";
            else
                labelText.text = $"[E] {displayName}";
        }

        if (background != null)
            background.color = backpackFull ? fullColor : normalColor;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void DecideUiCamera(Canvas canvas, out Camera uiCamera)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = null; // Overlay canvas 使用 null UI camera
        }
        else
        {
            uiCamera = canvas.worldCamera;
        }
    }

    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
