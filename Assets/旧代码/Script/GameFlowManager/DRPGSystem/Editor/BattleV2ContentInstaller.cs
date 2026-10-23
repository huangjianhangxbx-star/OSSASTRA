#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class BattleV2ContentInstaller
{
    private const string PresentationPrefabPath =
        "Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab";
    private const string VisualRoot = "Assets/GameResources/DRPG/UI/BattleV2/";

    [MenuItem("Tools/ANKALUTE/DRPG/Battle V2/Install UI Foundation")]
    public static void InstallFromMenu()
    {
        Debug.Log(Install());
    }

    public static string Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PresentationPrefabPath);
        if (root == null)
            throw new InvalidOperationException("Missing prefab: " + PresentationPrefabPath);
        try
        {
            Transform existing = root.transform.Find("BattleV2Hud");
            if (existing != null)
            {
                TMP_FontAsset existingFont = ResolveFont(root);
                UpgradeVisuals(root, existing.gameObject, existingFont);
                ValidateAndWire(root, existing.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PresentationPrefabPath);
                AssetDatabase.SaveAssets();
                return "BattleV2 HUD upgraded with cards, iron arrow and stamina pips.";
            }

            TMP_FontAsset font = ResolveFont(root);
            GameObject hud = CreateRect("BattleV2Hud", root.transform,
                new Vector2(0f, 0f), new Vector2(1f, .34f), Vector2.zero, Vector2.zero);
            UnityEngine.UI.Image hudImage = hud.AddComponent<UnityEngine.UI.Image>();
            hudImage.color = Hex("0B0C0EF2");
            hudImage.raycastTarget = false;

            GameObject topRule = CreateRect("IronSpine", hud.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -5f), Vector2.zero);
            UnityEngine.UI.Image ruleImage = topRule.AddComponent<UnityEngine.UI.Image>();
            ruleImage.color = Hex("9CA3A6C0");
            ruleImage.raycastTarget = false;

            GameObject actorRail = CreateRect("ActorRail", hud.transform,
                new Vector2(.012f, .07f), new Vector2(.165f, .93f),
                Vector2.zero, Vector2.zero);
            AddPanel(actorRail, Hex("181B1ECC"));
            CreateText("ActorName", actorRail.transform, font, "当前行动者",
                30f, TextAlignmentOptions.TopLeft,
                new Vector2(.08f, .72f), new Vector2(.92f, .93f));
            CreateText("ActorStatus", actorRail.transform, font, "HP  — / —\n架势  —",
                24f, TextAlignmentOptions.MidlineLeft,
                new Vector2(.08f, .12f), new Vector2(.92f, .68f));

            GameObject skillArea = CreateRect("SkillArea", hud.transform,
                new Vector2(.175f, .07f), new Vector2(.555f, .93f),
                Vector2.zero, Vector2.zero);
            AddPanel(skillArea, Hex("111315E8"));
            CreateText("SkillHeader", skillArea.transform, font, "技能",
                24f, TextAlignmentOptions.Left,
                new Vector2(.025f, .82f), new Vector2(.25f, .98f));
            GameObject slotRow = CreateRect("SkillSlots", skillArea.transform,
                new Vector2(.025f, .08f), new Vector2(.975f, .80f),
                Vector2.zero, Vector2.zero);
            var layout = slotRow.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var slotViews = new List<BattleSkillMenuEntryView>();
            for (int i = 0; i < DRPGSkillLoadout.SlotCount; i++)
                slotViews.Add(CreateSkillSlot(slotRow.transform, font, i));

            GameObject recovery = CreateRect("PostureRecovery", hud.transform,
                new Vector2(.565f, .12f), new Vector2(.61f, .88f),
                Vector2.zero, Vector2.zero);
            UnityEngine.UI.Image recoveryImage = AddPanel(recovery, Hex("41474BE8"));
            UnityEngine.UI.Button recoveryButton = recovery.AddComponent<UnityEngine.UI.Button>();
            recoveryButton.targetGraphic = recoveryImage;
            recoveryButton.interactable = false;
            CreateText("Label", recovery.transform, font, "架\n势\n恢\n复",
                25f, TextAlignmentOptions.Center,
                new Vector2(.12f, .08f), new Vector2(.88f, .92f));

            GameObject cardPlaceholder = CreateRect("CardArea", hud.transform,
                new Vector2(.62f, .07f), new Vector2(.988f, .93f),
                Vector2.zero, Vector2.zero);
            AddPanel(cardPlaceholder, Hex("111315E8"));
            CreateText("CardPlaceholder", cardPlaceholder.transform, font,
                "卡牌演示区域 · A4接入",
                24f, TextAlignmentOptions.Center,
                new Vector2(.05f, .12f), new Vector2(.95f, .88f));

            BattleFixedSkillBar bar = hud.AddComponent<BattleFixedSkillBar>();
            var serializedBar = new SerializedObject(bar);
            SerializedProperty slots = serializedBar.FindProperty("slots");
            slots.arraySize = slotViews.Count;
            for (int i = 0; i < slotViews.Count; i++)
                slots.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
            serializedBar.FindProperty("postureRecoveryButton").objectReferenceValue =
                recoveryButton;
            serializedBar.ApplyModifiedPropertiesWithoutUndo();

            UpgradeVisuals(root, hud, font);
            ValidateAndWire(root, hud);
            PrefabUtility.SaveAsPrefabAsset(root, PresentationPrefabPath);
            AssetDatabase.SaveAssets();
            return "Installed BattleV2 HUD foundation with four fixed skill slots.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeVisuals(
        GameObject prefabRoot,
        GameObject hud,
        TMP_FontAsset font)
    {
        ImportVisuals();
        Sprite cardFrame = AssetDatabase.LoadAssetAtPath<Sprite>(VisualRoot + "card-frame.png");
        Sprite pipFilled = AssetDatabase.LoadAssetAtPath<Sprite>(VisualRoot + "pip-filled.png");
        Sprite pipEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(VisualRoot + "pip-empty.png");
        Sprite arrowSegment = AssetDatabase.LoadAssetAtPath<Sprite>(VisualRoot + "arrow-segment.png");
        Sprite arrowTip = AssetDatabase.LoadAssetAtPath<Sprite>(VisualRoot + "arrow-tip.png");

        Transform actorRail = hud.transform.Find("ActorRail");
        BattlePipRow pipRow = actorRail != null
            ? actorRail.GetComponentInChildren<BattlePipRow>(true)
            : null;
        if (pipRow == null && actorRail != null)
        {
            GameObject pipRoot = CreateRect("StaminaPips", actorRail,
                new Vector2(.07f, .03f), new Vector2(.93f, .19f),
                Vector2.zero, Vector2.zero);
            var layout = pipRoot.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            pipRow = pipRoot.AddComponent<BattlePipRow>();
            var images = new List<UnityEngine.UI.Image>();
            for (int i = 0; i < 10; i++)
            {
                GameObject pip = CreateRect("Pip_" + i, pipRoot.transform,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var image = pip.AddComponent<UnityEngine.UI.Image>();
                image.sprite = pipEmpty;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var element = pip.AddComponent<UnityEngine.UI.LayoutElement>();
                element.preferredHeight = 30f;
                element.preferredWidth = 30f;
                images.Add(image);
            }
            var serializedPips = new SerializedObject(pipRow);
            SetObjectArray(serializedPips.FindProperty("pips"), images);
            serializedPips.FindProperty("filledSprite").objectReferenceValue = pipFilled;
            serializedPips.FindProperty("emptySprite").objectReferenceValue = pipEmpty;
            serializedPips.ApplyModifiedPropertiesWithoutUndo();
            pipRow.Bind(4, 6, 0);
        }

        Transform cardArea = hud.transform.Find("CardArea");
        BattleCardHandView hand = cardArea != null
            ? cardArea.GetComponent<BattleCardHandView>()
            : null;
        if (hand == null && cardArea != null)
        {
            Transform placeholder = cardArea.Find("CardPlaceholder");
            if (placeholder != null)
                UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
            GameObject row = CreateRect("CardSlots", cardArea,
                new Vector2(.025f, .055f), new Vector2(.975f, .945f),
                Vector2.zero, Vector2.zero);
            var layout = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 7f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            hand = cardArea.gameObject.AddComponent<BattleCardHandView>();
            var cardViews = new List<BattleCardPreviewView>();
            for (int i = 0; i < 4; i++)
                cardViews.Add(CreateCardSlot(row.transform, font, cardFrame, i));
            var serializedHand = new SerializedObject(hand);
            SetObjectArray(serializedHand.FindProperty("slots"), cardViews);
            serializedHand.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (hand != null)
        {
            Transform row = cardArea.Find("CardSlots");
            var repairedViews = new List<BattleCardPreviewView>();
            if (row != null)
            {
                for (int i = 0; i < row.childCount; i++)
                {
                    GameObject card = row.GetChild(i).gameObject;
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(card);
                    BattleCardPreviewView view =
                        card.GetComponent<BattleCardPreviewView>();
                    if (view == null)
                        view = card.AddComponent<BattleCardPreviewView>();
                    WireCardView(view, card);
                    repairedViews.Add(view);
                }
            }
            var serializedHand = new SerializedObject(hand);
            SetObjectArray(serializedHand.FindProperty("slots"), repairedViews);
            serializedHand.ApplyModifiedPropertiesWithoutUndo();
        }

        BattleCardTargetArrow arrow =
            prefabRoot.GetComponentInChildren<BattleCardTargetArrow>(true);
        if (arrow == null)
        {
            Canvas canvas = prefabRoot.GetComponentInChildren<Canvas>(true);
            Transform parent = canvas != null ? canvas.transform : prefabRoot.transform;
            GameObject overlay = CreateRect("BattleV2CardArrow", parent,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.transform.SetAsLastSibling();
            arrow = overlay.AddComponent<BattleCardTargetArrow>();
            GameObject segments = CreateRect("Segments", overlay.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            GameObject segmentObject = CreateRect("Segment_0", segments.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            RectTransform segmentRect = segmentObject.GetComponent<RectTransform>();
            segmentRect.sizeDelta = new Vector2(18f, 52f);
            var segmentImage = segmentObject.AddComponent<UnityEngine.UI.Image>();
            segmentImage.sprite = arrowSegment;
            segmentImage.preserveAspect = true;
            segmentImage.raycastTarget = false;
            GameObject tipObject = CreateRect("Tip", overlay.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            RectTransform tipRect = tipObject.GetComponent<RectTransform>();
            tipRect.sizeDelta = new Vector2(54f, 112f);
            var tipImage = tipObject.AddComponent<UnityEngine.UI.Image>();
            tipImage.sprite = arrowTip;
            tipImage.preserveAspect = true;
            tipImage.raycastTarget = false;
            GameObject coreObject = CreateRect("AllegianceCore", tipObject.transform,
                new Vector2(.39f, .08f), new Vector2(.61f, .28f),
                Vector2.zero, Vector2.zero);
            var core = coreObject.AddComponent<UnityEngine.UI.Image>();
            core.color = Hex("7A2426FF");
            core.raycastTarget = false;
            var serializedArrow = new SerializedObject(arrow);
            serializedArrow.FindProperty("segmentRoot").objectReferenceValue =
                segments.GetComponent<RectTransform>();
            serializedArrow.FindProperty("coordinateRoot").objectReferenceValue =
                overlay.GetComponent<RectTransform>();
            serializedArrow.FindProperty("canvas").objectReferenceValue = canvas;
            serializedArrow.FindProperty("segmentTemplate").objectReferenceValue = segmentImage;
            serializedArrow.FindProperty("tip").objectReferenceValue = tipImage;
            serializedArrow.FindProperty("allegianceCore").objectReferenceValue = core;
            serializedArrow.ApplyModifiedPropertiesWithoutUndo();
            arrow.Hide();
        }
        Transform segmentVisual = arrow.transform.Find("Segments/Segment_0");
        if (segmentVisual != null)
        {
            RectTransform rect = segmentVisual.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(48f, 64f);
            UnityEngine.UI.Image image = segmentVisual.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                image.preserveAspect = false;
        }
        Transform tipVisual = arrow.transform.Find("Tip");
        if (tipVisual != null)
        {
            RectTransform rect = tipVisual.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(96f, 144f);
            UnityEngine.UI.Image image = tipVisual.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                image.preserveAspect = false;
        }

        DRPGBattleCardPreviewSO[] cards = EnsurePreviewCards();
        DRPGBattleUGUISurface surface =
            prefabRoot.GetComponentInChildren<DRPGBattleUGUISurface>(true);
        var serializedSurface = new SerializedObject(surface);
        serializedSurface.FindProperty("battleV2CardHand").objectReferenceValue = hand;
        serializedSurface.FindProperty("battleV2CardArrow").objectReferenceValue = arrow;
        serializedSurface.FindProperty("battleV2PipRow").objectReferenceValue = pipRow;
        serializedSurface.FindProperty("battleV2ActorName").objectReferenceValue =
            hud.transform.Find("ActorRail/ActorName")?.GetComponent<TextMeshProUGUI>();
        serializedSurface.FindProperty("battleV2ActorStatus").objectReferenceValue =
            hud.transform.Find("ActorRail/ActorStatus")?.GetComponent<TextMeshProUGUI>();
        SetObjectArray(serializedSurface.FindProperty("battleV2PreviewCards"), cards);
        serializedSurface.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.UI.Image hudBackground = hud.GetComponent<UnityEngine.UI.Image>();
        if (hudBackground != null)
            hudBackground.color = Hex("0B0C0EFF");

        BattleHudBinding binding = prefabRoot.GetComponent<BattleHudBinding>();
        if (binding == null)
            binding = prefabRoot.AddComponent<BattleHudBinding>();
        var serializedBinding = new SerializedObject(binding);
        serializedBinding.FindProperty("surface").objectReferenceValue = surface;
        serializedBinding.ApplyModifiedPropertiesWithoutUndo();
        DRPGBattlePresentationRoot presentationRoot =
            prefabRoot.GetComponent<DRPGBattlePresentationRoot>();
        if (presentationRoot != null)
        {
            var serializedRoot = new SerializedObject(presentationRoot);
            serializedRoot.FindProperty("battleV2HudBinding").objectReferenceValue = binding;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static BattleCardPreviewView CreateCardSlot(
        Transform parent,
        TMP_FontAsset font,
        Sprite frame,
        int index)
    {
        GameObject card = CreateRect("CardSlot_" + index, parent,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = card.AddComponent<UnityEngine.UI.Image>();
        image.sprite = frame;
        image.color = Color.white;
        image.raycastTarget = true;
        var element = card.AddComponent<UnityEngine.UI.LayoutElement>();
        element.minWidth = 128f;
        element.minHeight = 220f;
        var group = card.AddComponent<CanvasGroup>();
        TextMeshProUGUI title = CreateText("Title", card.transform, font,
            "演示卡牌", 20f, TextAlignmentOptions.Top,
            new Vector2(.14f, .66f), new Vector2(.86f, .88f));
        TextMeshProUGUI description = CreateText("Description", card.transform, font,
            "预览层，不产生效果", 16f, TextAlignmentOptions.Center,
            new Vector2(.14f, .27f), new Vector2(.86f, .63f));
        TextMeshProUGUI target = CreateText("TargetRule", card.transform, font,
            "目标", 15f, TextAlignmentOptions.Bottom,
            new Vector2(.12f, .08f), new Vector2(.88f, .25f));
        target.color = Hex("9CA3A6FF");
        var view = card.AddComponent<BattleCardPreviewView>();
        WireCardView(view, card);
        return view;
    }

    private static void WireCardView(BattleCardPreviewView view, GameObject card)
    {
        TextMeshProUGUI title = card.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI description = card.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI target = card.transform.Find("TargetRule")?.GetComponent<TextMeshProUGUI>();
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("motionRoot").objectReferenceValue =
            card.GetComponent<RectTransform>();
        serialized.FindProperty("title").objectReferenceValue = title;
        serialized.FindProperty("description").objectReferenceValue = description;
        serialized.FindProperty("targetRule").objectReferenceValue = target;
        serialized.FindProperty("canvasGroup").objectReferenceValue = group;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ImportVisuals()
    {
        string[] names = { "panel-metal.png", "card-frame.png", "arrow-tip.png",
            "arrow-segment.png", "pip-filled.png", "pip-empty.png", "target-corner.png" };
        AssetDatabase.Refresh();
        foreach (string name in names)
        {
            string path = VisualRoot + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing generated UI asset: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = name.StartsWith("card") || name.StartsWith("panel")
                ? 1024 : 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    private static DRPGBattleCardPreviewSO[] EnsurePreviewCards()
    {
        string cardRoot = VisualRoot + "Cards/";
        if (!AssetDatabase.IsValidFolder(cardRoot.TrimEnd('/')))
            AssetDatabase.CreateFolder(VisualRoot.TrimEnd('/'), "Cards");
        string[] names = { "猎人的小瓶", "腐蚀钉", "铁锈祷符", "余烬粉末" };
        string[] descriptions = { "为己方目标准备的恢复道具预览。", "投向敌方的消耗品预览。",
            "可指向任意存活单位的预览。", "投向敌方的易耗品预览。" };
        BattleCardTargetRule[] rules = { BattleCardTargetRule.Ally, BattleCardTargetRule.Enemy,
            BattleCardTargetRule.AnyLiving, BattleCardTargetRule.Enemy };
        var cards = new DRPGBattleCardPreviewSO[4];
        for (int i = 0; i < cards.Length; i++)
        {
            string path = cardRoot + "CardPreview_" + i + ".asset";
            cards[i] = AssetDatabase.LoadAssetAtPath<DRPGBattleCardPreviewSO>(path);
            if (cards[i] == null)
            {
                cards[i] = ScriptableObject.CreateInstance<DRPGBattleCardPreviewSO>();
                AssetDatabase.CreateAsset(cards[i], path);
            }
            var serialized = new SerializedObject(cards[i]);
            serialized.FindProperty("cardId").stringValue = "preview-card-" + i;
            serialized.FindProperty("displayName").stringValue = names[i];
            serialized.FindProperty("description").stringValue = descriptions[i];
            serialized.FindProperty("targetRule").enumValueIndex = (int)rules[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cards[i]);
        }
        return cards;
    }

    private static void SetObjectArray<T>(SerializedProperty property, IList<T> values)
        where T : UnityEngine.Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static BattleSkillMenuEntryView CreateSkillSlot(
        Transform parent,
        TMP_FontAsset font,
        int index)
    {
        GameObject slot = CreateRect("SkillSlot_" + index, parent,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UnityEngine.UI.Image image = AddPanel(slot, Hex("202428F0"));
        UnityEngine.UI.Button button = slot.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.interactable = false;
        var layoutElement = slot.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElement.minWidth = 150f;
        layoutElement.minHeight = 150f;

        TextMeshProUGUI label = CreateText("SkillName", slot.transform, font,
            "空槽位", 23f, TextAlignmentOptions.TopLeft,
            new Vector2(.08f, .64f), new Vector2(.92f, .94f));
        TextMeshProUGUI cost = CreateText("Cost", slot.transform, font,
            "—", 26f, TextAlignmentOptions.TopRight,
            new Vector2(.62f, .36f), new Vector2(.91f, .62f));
        cost.color = Hex("D6A93BFF");
        TextMeshProUGUI panel = CreateText("Panel", slot.transform, font,
            "—", 25f, TextAlignmentOptions.BottomLeft,
            new Vector2(.08f, .08f), new Vector2(.92f, .35f));
        TextMeshProUGUI reason = CreateText("UnavailableReason", slot.transform,
            font, "未配置技能", 17f, TextAlignmentOptions.BottomRight,
            new Vector2(.08f, .08f), new Vector2(.92f, .35f));
        reason.color = Hex("9CA3A6B0");

        BattleSkillMenuEntryView view = slot.AddComponent<BattleSkillMenuEntryView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("button").objectReferenceValue = button;
        serialized.FindProperty("label").objectReferenceValue = label;
        serialized.FindProperty("costText").objectReferenceValue = cost;
        serialized.FindProperty("panelText").objectReferenceValue = panel;
        serialized.FindProperty("unavailableReasonText").objectReferenceValue = reason;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static void ValidateAndWire(GameObject prefabRoot, GameObject hud)
    {
        BattleFixedSkillBar bar = hud.GetComponent<BattleFixedSkillBar>();
        if (bar == null || bar.VisibleSlotCount != DRPGSkillLoadout.SlotCount)
            throw new InvalidOperationException("BattleV2 HUD has an invalid fixed skill bar.");
        DRPGBattleUGUISurface surface =
            prefabRoot.GetComponentInChildren<DRPGBattleUGUISurface>(true);
        if (surface == null)
            throw new InvalidOperationException("Presentation prefab has no UGUI surface.");
        var serializedSurface = new SerializedObject(surface);
        serializedSurface.FindProperty("battleV2SkillBar").objectReferenceValue = bar;
        serializedSurface.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(bar);
    }

    private static TMP_FontAsset ResolveFont(GameObject root)
    {
        TextMeshProUGUI existing = root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existing == null || existing.font == null)
            throw new InvalidOperationException("No established TMP font found in battle prefab.");
        return existing.font;
    }

    private static GameObject CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return gameObject;
    }

    private static UnityEngine.UI.Image AddPanel(GameObject target, Color color)
    {
        UnityEngine.UI.Image image = target.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject target = CreateRect(name, parent, anchorMin, anchorMax,
            Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = Hex("E1E3E2FF");
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Color Hex(string value)
    {
        if (!ColorUtility.TryParseHtmlString("#" + value, out Color color))
            throw new ArgumentException("Invalid color " + value, nameof(value));
        return color;
    }
}
#endif
