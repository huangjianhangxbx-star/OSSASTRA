#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DevDebugHubInstaller
{
    public const string PrefabPath =
        "Assets/Resources/Development/PF_DevDebugHub.prefab";
    private const string FontPath =
        "Assets/SiYuanHeiTi-ExtraLight/SourceHanSansSC-ExtraLight-22 SDF.asset";

    private static readonly Color BackdropColor =
        new Color(0.006f, 0.015f, 0.025f, 0.88f);
    private static readonly Color FrameColor =
        new Color(0.018f, 0.045f, 0.064f, 0.985f);
    private static readonly Color PanelColor =
        new Color(0.025f, 0.070f, 0.095f, 0.96f);
    private static readonly Color PanelAltColor =
        new Color(0.018f, 0.052f, 0.076f, 0.98f);
    private static readonly Color Cyan =
        new Color(0.08f, 0.82f, 0.92f, 1f);
    private static readonly Color CyanMuted =
        new Color(0.16f, 0.48f, 0.56f, 1f);
    private static readonly Color TextMain =
        new Color(0.88f, 0.96f, 0.98f, 1f);
    private static readonly Color TextMuted =
        new Color(0.52f, 0.69f, 0.74f, 1f);
    private static readonly Color Amber =
        new Color(1f, 0.62f, 0.16f, 1f);

    [MenuItem("Tools/ANKALUTE/Development/Install Debug Hub")]
    public static void Install()
    {
        EnsureAssetFolder("Assets/Resources");
        EnsureAssetFolder("Assets/Resources/Development");
        TMP_FontAsset font =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ??
            TMP_Settings.defaultFontAsset;

        GameObject root = new GameObject(
            "PF_DevDebugHub",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(DevDebugHubController));
        try
        {
            RectTransform rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9500;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            GameObject backdrop = CreatePanel(
                "Backdrop",
                root.transform,
                BackdropColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            RectTransform safeArea = CreateRect(
                "SafeAreaRoot",
                root.transform,
                new Vector2(0.025f, 0.045f),
                new Vector2(0.975f, 0.955f),
                Vector2.zero,
                Vector2.zero);
            GameObject frameObject = CreatePanel(
                "Frame",
                safeArea,
                FrameColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RectTransform frame = (RectTransform)frameObject.transform;
            Outline frameOutline = frameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);
            frameOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform header = CreateRect(
                "Header",
                frame,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(24f, -104f),
                new Vector2(-24f, -18f));
            AddBottomLine(header, Cyan, 3f);
            CreateText(
                "Kicker",
                header,
                "ANKALUTE // DEVELOPMENT",
                font,
                23f,
                TextMuted,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.50f),
                new Vector2(0.62f, 1f),
                new Vector2(6f, -4f),
                new Vector2(-8f, 0f));
            CreateText(
                "Title",
                header,
                "DEBUG COMMAND CENTER  /  开发调试中心",
                font,
                38f,
                TextMain,
                TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f),
                new Vector2(0.75f, 0.63f),
                new Vector2(6f, 0f),
                new Vector2(-8f, 0f));
            CreateText(
                "Shortcut",
                header,
                "F10 TOGGLE  ·  ESC / RMB CLOSE",
                font,
                21f,
                Cyan,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.61f, 0f),
                new Vector2(0.88f, 1f),
                Vector2.zero,
                new Vector2(-10f, 0f));
            Button closeButton = CreateButton(
                "CloseButton",
                header,
                "CLOSE  ×",
                font,
                new Color(0.23f, 0.07f, 0.08f, 1f),
                new Vector2(0.89f, 0.16f),
                new Vector2(1f, 0.84f),
                Vector2.zero,
                Vector2.zero);

            RectTransform body = CreateRect(
                "Body",
                frame,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(24f, 90f),
                new Vector2(-24f, -122f));

            GameObject categoryPanel = CreatePanel(
                "CategoryRail",
                body,
                PanelAltColor,
                new Vector2(0f, 0f),
                new Vector2(0.17f, 1f),
                Vector2.zero,
                new Vector2(-10f, 0f));
            RectTransform categoryRoot =
                (RectTransform)categoryPanel.transform;
            VerticalLayoutGroup categoryLayout =
                categoryPanel.AddComponent<VerticalLayoutGroup>();
            categoryLayout.padding = new RectOffset(18, 18, 80, 18);
            categoryLayout.spacing = 10f;
            categoryLayout.childControlHeight = true;
            categoryLayout.childControlWidth = true;
            categoryLayout.childForceExpandHeight = false;
            categoryLayout.childForceExpandWidth = true;
            CreateText(
                "RailLabel",
                categoryRoot,
                "MODULES",
                font,
                20f,
                TextMuted,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(18f, -66f),
                new Vector2(-18f, -18f),
                true);
            Button categoryTemplate = CreateButton(
                "CategoryButtonTemplate",
                categoryRoot,
                "CATEGORY",
                font,
                new Color(0.035f, 0.09f, 0.13f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                true);
            LayoutElement categoryElement =
                categoryTemplate.gameObject.AddComponent<LayoutElement>();
            categoryElement.preferredHeight = 62f;
            categoryElement.minHeight = 62f;
            categoryTemplate.gameObject.SetActive(false);

            GameObject actionPanel = CreatePanel(
                "ActionPanel",
                body,
                PanelColor,
                new Vector2(0.17f, 0f),
                new Vector2(0.55f, 1f),
                new Vector2(4f, 0f),
                new Vector2(-8f, 0f));
            CreateText(
                "PanelLabel",
                actionPanel.transform,
                "REGISTERED TEST ACTIONS",
                font,
                20f,
                TextMuted,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -64f),
                new Vector2(-22f, -18f));
            RectTransform actionScroll = CreateRect(
                "ActionScroll",
                actionPanel.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 18f),
                new Vector2(-18f, -76f));
            Image scrollImage = actionScroll.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0.12f);
            RectTransform viewport = CreateRect(
                "Viewport",
                actionScroll,
                Vector2.zero,
                Vector2.one,
                new Vector2(4f, 4f),
                new Vector2(-4f, -4f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform actionRoot = CreateRect(
                "Content",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            actionRoot.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup actionLayout =
                actionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            actionLayout.padding = new RectOffset(8, 8, 8, 8);
            actionLayout.spacing = 12f;
            actionLayout.childControlHeight = true;
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childForceExpandWidth = true;
            ContentSizeFitter fitter =
                actionRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scrollRect = actionScroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = actionRoot;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 38f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            DevDebugActionButtonView actionTemplate =
                CreateActionTemplate(actionRoot, font);
            actionTemplate.gameObject.SetActive(false);

            GameObject detailPanel = CreatePanel(
                "DetailPanel",
                body,
                PanelAltColor,
                new Vector2(0.55f, 0f),
                new Vector2(1f, 1f),
                new Vector2(6f, 0f),
                Vector2.zero);
            CreateText(
                "PanelLabel",
                detailPanel.transform,
                "ACTION INSPECTOR",
                font,
                20f,
                TextMuted,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(28f, -64f),
                new Vector2(-28f, -18f));
            TMP_Text detailTitle = CreateText(
                "Title",
                detailPanel.transform,
                "SELECT A DEBUG ACTION",
                font,
                34f,
                TextMain,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.78f),
                new Vector2(1f, 0.92f),
                new Vector2(28f, 0f),
                new Vector2(-28f, 0f));
            TMP_Text detailSummary = CreateText(
                "Summary",
                detailPanel.transform,
                "悬停测试项以查看用途与前置条件。",
                font,
                22f,
                Cyan,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.64f),
                new Vector2(1f, 0.78f),
                new Vector2(28f, 0f),
                new Vector2(-28f, 0f));
            TMP_Text detailBody = CreateText(
                "Details",
                detailPanel.transform,
                "调试项由注册表动态提供，可随系统开发自由增删。",
                font,
                22f,
                TextMain,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.20f),
                new Vector2(1f, 0.63f),
                new Vector2(28f, 12f),
                new Vector2(-28f, -6f));
            detailBody.enableWordWrapping = true;
            TMP_Text detailState = CreateText(
                "State",
                detailPanel.transform,
                "READY",
                font,
                22f,
                Cyan,
                TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.18f),
                new Vector2(28f, 22f),
                new Vector2(-28f, 0f));
            AddLeftLine((RectTransform)detailPanel.transform, Cyan, 4f);

            RectTransform footer = CreateRect(
                "Footer",
                frame,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(24f, 18f),
                new Vector2(-24f, 78f));
            AddTopLine(footer, CyanMuted, 2f);
            CreateText(
                "Label",
                footer,
                "LAST RESULT",
                font,
                18f,
                TextMuted,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f),
                new Vector2(0.14f, 1f),
                new Vector2(6f, 0f),
                Vector2.zero);
            TMP_Text resultText = CreateText(
                "Result",
                footer,
                "[READY] Debug registry online.",
                font,
                21f,
                Cyan,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.14f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(-6f, 0f));

            DevDebugHubController controller =
                root.GetComponent<DevDebugHubController>();
            SerializedObject serialized = new SerializedObject(controller);
            Assign(serialized, "canvasGroup", canvasGroup);
            Assign(serialized, "frame", frame);
            Assign(serialized, "closeButton", closeButton);
            Assign(serialized, "categoryRoot", categoryRoot);
            Assign(serialized, "categoryButtonTemplate", categoryTemplate);
            Assign(serialized, "actionRoot", actionRoot);
            Assign(serialized, "actionButtonTemplate", actionTemplate);
            Assign(serialized, "detailTitleText", detailTitle);
            Assign(serialized, "detailSummaryText", detailSummary);
            Assign(serialized, "detailBodyText", detailBody);
            Assign(serialized, "detailStateText", detailState);
            Assign(serialized, "resultText", resultText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath,
                out bool success);
            if (!success || saved == null)
                throw new UnityException("Failed to save " + PrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                PrefabPath,
                ImportAssetOptions.ForceSynchronousImport);
            Selection.activeObject = saved;
            Debug.Log("[DevDebugHub] Installed " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static DevDebugActionButtonView CreateActionTemplate(
        RectTransform parent,
        TMP_FontAsset font)
    {
        GameObject root = CreatePanel(
            "ActionButtonTemplate",
            parent,
            new Color(0.035f, 0.11f, 0.145f, 1f),
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.55f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.32f, 0.76f, 0.82f, 1f);
        colors.disabledColor = new Color(0.42f, 0.46f, 0.48f, 0.7f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        LayoutElement element = root.AddComponent<LayoutElement>();
        element.preferredHeight = 108f;
        element.minHeight = 108f;

        GameObject railObject = CreatePanel(
            "StateRail",
            root.transform,
            Cyan,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(7f, 0f));
        Image rail = railObject.GetComponent<Image>();
        TMP_Text title = CreateText(
            "Title",
            root.transform,
            "DEBUG ACTION",
            font,
            24f,
            TextMain,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0.48f),
            new Vector2(0.74f, 1f),
            new Vector2(24f, 0f),
            new Vector2(-8f, -5f));
        TMP_Text summary = CreateText(
            "Summary",
            root.transform,
            "Short diagnostic description",
            font,
            18f,
            TextMuted,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f),
            new Vector2(0.83f, 0.50f),
            new Vector2(24f, 5f),
            new Vector2(-8f, 0f));
        TMP_Text state = CreateText(
            "State",
            root.transform,
            "READY",
            font,
            17f,
            Cyan,
            TextAlignmentOptions.MidlineRight,
            new Vector2(0.76f, 0f),
            new Vector2(1f, 1f),
            Vector2.zero,
            new Vector2(-18f, 0f));

        DevDebugActionButtonView view =
            root.AddComponent<DevDebugActionButtonView>();
        SerializedObject serialized = new SerializedObject(view);
        Assign(serialized, "button", button);
        Assign(serialized, "titleText", title);
        Assign(serialized, "summaryText", summary);
        Assign(serialized, "stateText", state);
        Assign(serialized, "stateRail", rail);
        Assign(serialized, "canvasGroup", group);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        TMP_FontAsset font,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        bool ignoreLayout = false)
    {
        GameObject root = CreatePanel(
            name,
            parent,
            color,
            anchorMin,
            anchorMax,
            offsetMin,
            offsetMax);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.45f, 0.75f, 0.80f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        TMP_Text text = CreateText(
            "Label",
            root.transform,
            label,
            font,
            20f,
            TextMain,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            new Vector2(8f, 3f),
            new Vector2(-8f, -3f));
        text.fontStyle = FontStyles.Bold;
        if (ignoreLayout)
        {
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.ignoreLayout = false;
        }
        return button;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        bool ignoreLayout = false)
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            anchorMin,
            anchorMax,
            offsetMin,
            offsetMax);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (ignoreLayout)
        {
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }
        return text;
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            anchorMin,
            anchorMax,
            offsetMin,
            offsetMax);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect.gameObject;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void AddBottomLine(RectTransform parent, Color color, float height)
    {
        CreatePanel(
            "BottomLine",
            parent,
            color,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            Vector2.zero,
            new Vector2(0f, height));
    }

    private static void AddTopLine(RectTransform parent, Color color, float height)
    {
        CreatePanel(
            "TopLine",
            parent,
            color,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -height),
            Vector2.zero);
    }

    private static void AddLeftLine(RectTransform parent, Color color, float width)
    {
        CreatePanel(
            "LeftLine",
            parent,
            color,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(width, 0f));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Assign(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingReferenceException("Missing field: " + propertyName);
        property.objectReferenceValue = value;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string folder = path.Substring(slash + 1);
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
#endif
