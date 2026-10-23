using TMPro;
using UnityEngine;

public readonly struct BattleIntentSnapshot
{
    public BattleIntentSnapshot(
        string skillId,
        string displayName,
        Sprite icon,
        string attributeKey,
        int? panel,
        int warning,
        int? disclosedTargetId)
    {
        SkillId = skillId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Icon = icon;
        AttributeKey = attributeKey ?? string.Empty;
        Panel = panel;
        Warning = Mathf.Max(0, warning);
        DisclosedTargetId = disclosedTargetId;
    }

    public string SkillId { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    public string AttributeKey { get; }
    public int? Panel { get; }
    public int Warning { get; }
    public int? DisclosedTargetId { get; }
}

public sealed class BattleIntentView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text skillName;
    [SerializeField] private TMP_Text panel;
    [SerializeField] private TMP_Text warning;

    public string RenderedPanel => panel != null ? panel.text : "";

    public static BattleIntentView Create(RectTransform owner)
    {
        GameObject root = NewRect("BattleV2Intent", owner,
            new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(230f, 42f), new Vector2(0f, 12f));
        var background = root.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color32(11, 12, 14, 232);
        background.raycastTarget = false;
        TMP_Text name = NewText("Skill", root.transform, 13f,
            TextAlignmentOptions.MidlineLeft, new Vector2(.05f, .2f),
            new Vector2(.52f, .8f));
        TMP_Text value = NewText("Panel", root.transform, 16f,
            TextAlignmentOptions.Center, new Vector2(.53f, .15f),
            new Vector2(.76f, .85f));
        value.color = new Color32(214, 169, 59, 255);
        TMP_Text countdown = NewText("Warning", root.transform, 18f,
            TextAlignmentOptions.Center, new Vector2(.78f, .12f),
            new Vector2(.97f, .88f));
        countdown.color = new Color32(122, 36, 38, 255);
        var result = root.AddComponent<BattleIntentView>();
        result.skillName = name;
        result.panel = value;
        result.warning = countdown;
        return result;
    }

    public void Bind(BattleIntentSnapshot value)
    {
        gameObject.SetActive(true);
        if (skillName != null)
            skillName.text = string.IsNullOrWhiteSpace(value.DisplayName)
                ? "行动" : value.DisplayName;
        if (panel != null)
            panel.text = value.Panel.HasValue ? value.Panel.Value.ToString() : "—";
        if (warning != null)
            warning.text = value.Warning.ToString();
        if (icon != null)
        {
            icon.sprite = value.Icon;
            icon.enabled = value.Icon != null;
        }
    }

    public void Clear() => gameObject.SetActive(false);

    private static GameObject NewRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
    {
        var target = new GameObject(name, typeof(RectTransform));
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return target;
    }

    private static TMP_Text NewText(string name, Transform parent, float size,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        GameObject target = NewRect(name, parent, min, max, Vector2.zero, Vector2.zero);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = target.AddComponent<TextMeshProUGUI>();
        text.font = ResolveCjkFont(text.font);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_FontAsset ResolveCjkFont(TMP_FontAsset fallback)
    {
        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            TMP_FontAsset candidate = loaded[i];
            if (candidate != null && candidate.name.IndexOf(
                    "NotoSansSC", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }
        return fallback;
    }
}
