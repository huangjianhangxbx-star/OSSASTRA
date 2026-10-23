using UnityEngine;

/// <summary>
/// Presents the encounter background as a real scene renderer. It follows the
/// battle camera at a fixed depth and fits the current perspective frustum.
/// </summary>
public sealed class BattleWorldBackgroundPresenter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField, Min(1f)] private float cameraDistance = 28f;
    [SerializeField, Min(1f)] private float overscan = 1.06f;

    private Camera battleCamera;

    public void Configure(
        SpriteRenderer renderer,
        float distance = 28f,
        float fitOverscan = 1.06f)
    {
        backgroundRenderer = renderer;
        cameraDistance = Mathf.Max(1f, distance);
        overscan = Mathf.Max(1f, fitOverscan);
    }

    public void Bind(Sprite sprite, Camera camera)
    {
        battleCamera = camera != null ? camera : battleCamera;
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (backgroundRenderer == null)
            return;
        backgroundRenderer.sprite = sprite;
        backgroundRenderer.enabled = sprite != null;
        RefreshTransform();
    }

    private void LateUpdate() => RefreshTransform();

    private void RefreshTransform()
    {
        if (battleCamera == null || backgroundRenderer == null ||
            backgroundRenderer.sprite == null)
        {
            return;
        }

        Transform cameraTransform = battleCamera.transform;
        transform.position = cameraTransform.position +
            cameraTransform.forward * cameraDistance;
        transform.rotation = cameraTransform.rotation;

        float height = battleCamera.orthographic
            ? battleCamera.orthographicSize * 2f
            : 2f * cameraDistance * Mathf.Tan(
                battleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * Mathf.Max(0.01f, battleCamera.aspect);
        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;
        float scale = Mathf.Max(width / spriteSize.x, height / spriteSize.y) *
            overscan;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
