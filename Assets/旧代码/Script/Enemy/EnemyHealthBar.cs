using UnityEngine;

/// <summary>
/// 敌人头顶血条。使用 SpriteRenderer 实现，
/// 从 EnemyDataBus 读取 currentHp/maxHp 并更新填充比例。
/// </summary>
public class EnemyHealthBar : MonoBehaviour, IEnemySubsystem
{
    [Header("外观")]
    [SerializeField] private Vector3 barOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float barWidth = 1.2f;
    [SerializeField] private float barHeight = 0.15f;
    [SerializeField] private Color bgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color fillColor = new Color(0f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color fillColorMid = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] private Color fillColorLow = new Color(0.9f, 0.1f, 0.1f, 1f);
    [SerializeField] private string sortingLayerName = "Character1";
    [SerializeField] private int sortingOrder = 100;

    private EnemyRoot _root;
    private GameObject _barRoot;
    private SpriteRenderer _bgRenderer;
    private SpriteRenderer _fillRenderer;
    private Sprite _whiteSprite;
    private float _lastHp = float.NaN;

    public void Initialize(EnemyRoot root)
    {
        _root = root;
    }

    private void Awake()
    {
        if (_root == null)
            _root = GetComponentInParent<EnemyRoot>();
    }

    private void Start()
    {
        CreateBar();
    }

    private void LateUpdate()
    {
        if (_root?.enemyDataBus == null)
            return;

        float hp = _root.enemyDataBus.currentHp;
        if (Mathf.Approximately(hp, _lastHp))
            return;

        _lastHp = hp;
        RefreshBar();
    }

    private void CreateBar()
    {
        if (_barRoot != null)
            return;

        _whiteSprite = CreateWhiteSprite();

        _barRoot = new GameObject("HealthBar");
        _barRoot.transform.SetParent(transform, false);
        _barRoot.transform.localPosition = barOffset;
        _barRoot.transform.localScale = Vector3.one;

        // Background
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(_barRoot.transform, false);
        _bgRenderer = bgGO.AddComponent<SpriteRenderer>();
        _bgRenderer.sprite = _whiteSprite;
        _bgRenderer.color = bgColor;
        _bgRenderer.sortingLayerName = sortingLayerName;
        _bgRenderer.sortingOrder = sortingOrder;
        bgGO.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // Fill (anchored to left edge)
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        _fillRenderer = fillGO.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite = _whiteSprite;
        _fillRenderer.color = fillColor;
        _fillRenderer.sortingLayerName = sortingLayerName;
        _fillRenderer.sortingOrder = sortingOrder + 1;
        // Fill covers full BG, then we scale X and reposition to left-align
        fillGO.transform.localScale = new Vector3(1f, 1f, 1f);
        fillGO.transform.localPosition = new Vector3(-0.5f, 0f, 0f); // left-align pivot
    }

    private void RefreshBar()
    {
        if (_barRoot == null)
            CreateBar();
        if (_barRoot == null || _root?.enemyDataBus == null)
            return;

        EnemyDataBus db = _root.enemyDataBus;
        float ratio = db.maxHp > 0f ? Mathf.Clamp01(db.currentHp / db.maxHp) : 0f;

        // Scale fill horizontally, keep left-aligned
        _fillRenderer.transform.localScale = new Vector3(ratio, 1f, 1f);
        // With center pivot, adjust position so left edge stays at BG's left edge (-0.5)
        _fillRenderer.transform.localPosition = new Vector3(-0.5f + ratio * 0.5f, 0f, 0f);

        Color c;
        if (ratio > 0.5f)
            c = Color.Lerp(fillColorMid, fillColor, (ratio - 0.5f) * 2f);
        else
            c = Color.Lerp(fillColorLow, fillColorMid, ratio * 2f);
        _fillRenderer.color = c;

        bool show = db.isAlive && ratio > 0f;
        _barRoot.SetActive(show);
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[4 * 4];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
