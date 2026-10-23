#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class DRPGGameLoopUIInstaller
{
    public const string PrefabPath =
        "Assets/Script/GameFlowManager/DRPGSystem/GameLoop/Prefabs/DRPGGameLoopCanvas.prefab";

    private static readonly Color Ink = new Color32(5, 7, 14, 255);
    private static readonly Color Panel = new Color32(10, 19, 31, 244);
    private static readonly Color PanelSoft = new Color32(17, 35, 48, 235);
    private static readonly Color Magenta = new Color32(242, 24, 112, 255);
    private static readonly Color Cyan = new Color32(32, 221, 238, 255);
    private static readonly Color White = new Color32(235, 241, 246, 255);
    private static readonly Color Muted = new Color32(136, 158, 173, 255);

    [MenuItem("Tools/ANKALUTE/DRPG/Install Game Loop UI")]
    public static void Install()
    {
        GameObject prefab = BuildPrefab();
        EnsureDebugEncounterRewards();
        InstallInOpenScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DRPGGameLoopUI] Installed title and settlement UI.");
    }

    public static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Script/GameFlowManager/DRPGSystem/GameLoop/Prefabs");
        TMP_FontAsset font = ResolveFont();

        GameObject root = new GameObject(
            "DRPGGameLoopCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster),
            typeof(DRPGGameLoopView));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The legacy exploration/menu canvases use high sorting orders.
            // Title and settlement are application-modal and must cover them.
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            UnityEngine.UI.CanvasScaler scaler =
                root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject title = BuildTitle(root.transform, font, out var startButton);
            GameObject strategicHub = BuildStrategicHub(
                root.transform,
                font,
                out GameObject hubHome,
                out GameObject dungeonSelection,
                out UnityEngine.UI.Button openDungeonSelection,
                out UnityEngine.UI.Button testExpedition,
                out UnityEngine.UI.Button hubBack,
                out UnityEngine.UI.Button returnToTitle);
            GameObject settlement = BuildSettlement(
                root.transform,
                font,
                out Transform experienceContent,
                out DRPGSettlementExperienceRowView experienceTemplate,
                out Transform lootContent,
                out DRPGLootSlotView lootTemplate,
                out Transform backpackContent,
                out DRPGLootSlotView backpackTemplate,
                out TMP_Text lootEmpty,
                out TMP_Text capacity,
                out TMP_Text feedback,
                out UnityEngine.UI.Button collectAll,
                out UnityEngine.UI.Button continueButton);
            title.SetActive(false);
            strategicHub.SetActive(false);
            hubHome.SetActive(false);
            dungeonSelection.SetActive(false);
            settlement.SetActive(false);

            SerializedObject view = new SerializedObject(
                root.GetComponent<DRPGGameLoopView>());
            SetObject(view, "titleRoot", title);
            SetObject(view, "strategicHubRoot", strategicHub);
            SetObject(view, "hubHomeRoot", hubHome);
            SetObject(view, "dungeonSelectionRoot", dungeonSelection);
            SetObject(view, "settlementRoot", settlement);
            SetObject(view, "startButton", startButton);
            SetObject(view, "openDungeonSelectionButton", openDungeonSelection);
            SetObject(view, "testExpeditionButton", testExpedition);
            SetObject(view, "hubBackButton", hubBack);
            SetObject(view, "returnToTitleButton", returnToTitle);
            SetObject(view, "experienceContent", experienceContent);
            SetObject(view, "experienceTemplate", experienceTemplate);
            SetObject(view, "lootContent", lootContent);
            SetObject(view, "lootTemplate", lootTemplate);
            SetObject(view, "backpackContent", backpackContent);
            SetObject(view, "backpackTemplate", backpackTemplate);
            SetObject(view, "lootEmptyText", lootEmpty);
            SetObject(view, "backpackCapacityText", capacity);
            SetObject(view, "feedbackText", feedback);
            SetObject(view, "collectAllButton", collectAll);
            SetObject(view, "continueButton", continueButton);
            view.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Upgrade Strategic Hub UI")]
    public static void UpgradeStrategicHubPrefab()
    {
        TMP_FontAsset font = ResolveFont();
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            DRPGGameLoopView viewComponent =
                contents.GetComponent<DRPGGameLoopView>();
            if (viewComponent == null)
                throw new MissingComponentException(
                    "DRPGGameLoopView is missing from the prefab root.");

            Transform existing = contents.transform.Find("StrategicHubRoot");
            GameObject strategicHub;
            GameObject hubHome;
            GameObject dungeonSelection;
            UnityEngine.UI.Button openDungeonSelection;
            UnityEngine.UI.Button testExpedition;
            UnityEngine.UI.Button hubBack;
            UnityEngine.UI.Button returnToTitle;
            if (existing == null)
            {
                strategicHub = BuildStrategicHub(
                    contents.transform,
                    font,
                    out hubHome,
                    out dungeonSelection,
                    out openDungeonSelection,
                    out testExpedition,
                    out hubBack,
                    out returnToTitle);
            }
            else
            {
                strategicHub = existing.gameObject;
                hubHome = existing.Find("HubHomeRoot")?.gameObject;
                dungeonSelection =
                    existing.Find("DungeonSelectionRoot")?.gameObject;
                openDungeonSelection = existing.Find(
                    "HubHomeRoot/OpenDungeonSelectionButton")
                    ?.GetComponent<UnityEngine.UI.Button>();
                returnToTitle = existing.Find(
                    "HubHomeRoot/ReturnToTitleButton")
                    ?.GetComponent<UnityEngine.UI.Button>();
                Transform testButtonTransform = existing.Find(
                    "DungeonSelectionRoot/TestExpeditionButton");
                if (testButtonTransform == null)
                {
                    testButtonTransform = existing.Find(
                        "DungeonSelectionRoot/ExpeditionDossier/" +
                        "TestExpeditionButton");
                    if (testButtonTransform != null &&
                        dungeonSelection != null)
                    {
                        RectTransform testRect =
                            testButtonTransform.GetComponent<RectTransform>();
                        testRect.SetParent(dungeonSelection.transform, false);
                        testRect.anchorMin = new Vector2(0.585f, 0.31f);
                        testRect.anchorMax = new Vector2(0.74f, 0.43f);
                        testRect.offsetMin = Vector2.zero;
                        testRect.offsetMax = Vector2.zero;
                    }
                }
                testExpedition = testButtonTransform
                    ?.GetComponent<UnityEngine.UI.Button>();
                hubBack = existing.Find("DungeonSelectionRoot/BackButton")
                    ?.GetComponent<UnityEngine.UI.Button>();
                if (hubHome == null || dungeonSelection == null ||
                    openDungeonSelection == null || testExpedition == null ||
                    hubBack == null || returnToTitle == null)
                {
                    throw new MissingReferenceException(
                        "Existing StrategicHubRoot is incomplete; refusing " +
                        "to replace a partially-authored subtree.");
                }
            }

            strategicHub.SetActive(false);
            hubHome.SetActive(false);
            dungeonSelection.SetActive(false);
            TMP_Text titleButtonLabel = contents.transform.Find(
                "TitleRoot/StartButton/Label")?.GetComponent<TMP_Text>();
            if (titleButtonLabel != null)
                titleButtonLabel.text = "进入据点   /   START";

            SerializedObject view = new SerializedObject(viewComponent);
            SetObject(view, "strategicHubRoot", strategicHub);
            SetObject(view, "hubHomeRoot", hubHome);
            SetObject(view, "dungeonSelectionRoot", dungeonSelection);
            SetObject(view, "openDungeonSelectionButton", openDungeonSelection);
            SetObject(view, "testExpeditionButton", testExpedition);
            SetObject(view, "hubBackButton", hubBack);
            SetObject(view, "returnToTitleButton", returnToTitle);
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.Refresh();
        Debug.Log("[DRPGGameLoopUI] Strategic Hub UI upgraded in place.");
    }

    private static GameObject BuildStrategicHub(
        Transform parent,
        TMP_FontAsset font,
        out GameObject hubHome,
        out GameObject dungeonSelection,
        out UnityEngine.UI.Button openDungeonSelection,
        out UnityEngine.UI.Button testExpedition,
        out UnityEngine.UI.Button hubBack,
        out UnityEngine.UI.Button returnToTitle)
    {
        GameObject root = CreateImage(
            "StrategicHubRoot", parent, Ink,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject cyanRail = CreateImage(
            "RouteTrace", root.transform, Cyan,
            new Vector2(0.055f, 0.12f), new Vector2(0.06f, 0.88f),
            Vector2.zero, Vector2.zero);
        cyanRail.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        GameObject magentaNode = CreateImage(
            "ActiveNode", root.transform, Magenta,
            new Vector2(0.044f, 0.74f), new Vector2(0.071f, 0.788f),
            Vector2.zero, Vector2.zero);
        magentaNode.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        CreateText("SystemLabel", root.transform,
            "PRE-EXPEDITION OPERATIONS  //  STRATEGIC HUB", font, 25f, Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0.09f, 0.89f), new Vector2(0.76f, 0.94f),
            Vector2.zero, Vector2.zero);
        CreateText("Clock", root.transform, "HUB // 00", font, 24f, Muted,
            TextAlignmentOptions.Right,
            new Vector2(0.76f, 0.89f), new Vector2(0.94f, 0.94f),
            Vector2.zero, Vector2.zero);

        hubHome = CreateImage(
            "HubHomeRoot", root.transform, new Color(0f, 0f, 0f, 0f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hubHome.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        CreateText("Title", hubHome.transform, "出击前厅", font, 92f, White,
            TextAlignmentOptions.Left,
            new Vector2(0.09f, 0.70f), new Vector2(0.55f, 0.84f),
            Vector2.zero, Vector2.zero);
        CreateText("Subtitle", hubHome.transform,
            "选择下一项行动。设施与城市网络将在后续阶段接入。", font, 27f, Muted,
            TextAlignmentOptions.Left,
            new Vector2(0.095f, 0.64f), new Vector2(0.68f, 0.70f),
            Vector2.zero, Vector2.zero);

        openDungeonSelection = CreateButton(
            "OpenDungeonSelectionButton", hubHome.transform,
            "选择副本   /   EXPEDITION", font,
            new Vector2(0.095f, 0.45f), new Vector2(0.42f, 0.56f),
            PanelSoft, Magenta, 33f);
        GameObject facility = CreateImage(
            "FacilityPlaceholder", hubHome.transform, Panel,
            new Vector2(0.095f, 0.33f), new Vector2(0.42f, 0.41f),
            Vector2.zero, Vector2.zero);
        CreateText("Label", facility.transform,
            "设施访问   /   LOCKED", font, 26f, Muted,
            TextAlignmentOptions.Left, Vector2.zero, Vector2.one,
            new Vector2(28f, 8f), new Vector2(-20f, -8f));
        GameObject city = CreateImage(
            "CityMapPlaceholder", hubHome.transform, Panel,
            new Vector2(0.095f, 0.23f), new Vector2(0.42f, 0.31f),
            Vector2.zero, Vector2.zero);
        CreateText("Label", city.transform,
            "城市地图   /   OFFLINE", font, 26f, Muted,
            TextAlignmentOptions.Left, Vector2.zero, Vector2.one,
            new Vector2(28f, 8f), new Vector2(-20f, -8f));
        returnToTitle = CreateButton(
            "ReturnToTitleButton", hubHome.transform,
            "返回标题   /   TITLE", font,
            new Vector2(0.095f, 0.10f), new Vector2(0.29f, 0.17f),
            Panel, Cyan, 24f);

        GameObject statusPanel = CreateImage(
            "OperationsStatus", hubHome.transform, PanelSoft,
            new Vector2(0.55f, 0.20f), new Vector2(0.91f, 0.77f),
            Vector2.zero, Vector2.zero);
        CreateText("Index", statusPanel.transform, "00", font, 210f,
            new Color(1f, 1f, 1f, 0.055f), TextAlignmentOptions.Center,
            new Vector2(0.48f, 0.42f), new Vector2(0.98f, 0.98f),
            Vector2.zero, Vector2.zero);
        CreateText("Header", statusPanel.transform,
            "CURRENT STATUS", font, 24f, Cyan, TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.82f), new Vector2(0.72f, 0.91f),
            Vector2.zero, Vector2.zero);
        CreateText("Value", statusPanel.transform,
            "待机\n\n队伍数据：已连接\n探索入口：可用\n城市网络：未接入", font,
            28f, White, TextAlignmentOptions.TopLeft,
            new Vector2(0.08f, 0.25f), new Vector2(0.86f, 0.76f),
            Vector2.zero, Vector2.zero);

        dungeonSelection = CreateImage(
            "DungeonSelectionRoot", root.transform,
            new Color(0f, 0f, 0f, 0f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dungeonSelection.GetComponent<UnityEngine.UI.Image>().raycastTarget =
            false;
        CreateText("Title", dungeonSelection.transform, "副本档案", font,
            86f, White, TextAlignmentOptions.Left,
            new Vector2(0.09f, 0.73f), new Vector2(0.55f, 0.85f),
            Vector2.zero, Vector2.zero);
        CreateText("Route", dungeonSelection.transform,
            "HUB // 00   >   DUNGEON // A", font, 25f, Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0.095f, 0.67f), new Vector2(0.62f, 0.72f),
            Vector2.zero, Vector2.zero);

        GameObject dossier = CreateImage(
            "ExpeditionDossier", dungeonSelection.transform, PanelSoft,
            new Vector2(0.095f, 0.24f), new Vector2(0.78f, 0.63f),
            Vector2.zero, Vector2.zero);
        UnityEngine.UI.Outline dossierOutline =
            dossier.AddComponent<UnityEngine.UI.Outline>();
        dossierOutline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
        dossierOutline.effectDistance = new Vector2(2f, -2f);
        CreateText("Code", dossier.transform, "DUNGEON-A", font, 26f,
            Magenta, TextAlignmentOptions.Left,
            new Vector2(0.06f, 0.76f), new Vector2(0.45f, 0.90f),
            Vector2.zero, Vector2.zero);
        CreateText("Name", dossier.transform, "测试地宫 · 当前场景", font,
            44f, White, TextAlignmentOptions.Left,
            new Vector2(0.06f, 0.52f), new Vector2(0.75f, 0.76f),
            Vector2.zero, Vector2.zero);
        CreateText("Details", dossier.transform,
            "首次接线目标\n不会切换场景；部署后恢复现有探索与战斗入口。", font,
            25f, Muted, TextAlignmentOptions.TopLeft,
            new Vector2(0.06f, 0.18f), new Vector2(0.70f, 0.49f),
            Vector2.zero, Vector2.zero);
        testExpedition = CreateButton(
            "TestExpeditionButton", dungeonSelection.transform,
            "部署   /   DEPLOY", font,
            new Vector2(0.585f, 0.31f), new Vector2(0.74f, 0.43f),
            Magenta, Cyan, 29f);
        hubBack = CreateButton(
            "BackButton", dungeonSelection.transform,
            "返回前厅   /   BACK", font,
            new Vector2(0.095f, 0.10f), new Vector2(0.31f, 0.17f),
            Panel, Cyan, 24f);

        root.SetActive(false);
        hubHome.SetActive(false);
        dungeonSelection.SetActive(false);
        return root;
    }

    private static GameObject BuildTitle(
        Transform parent,
        TMP_FontAsset font,
        out UnityEngine.UI.Button startButton)
    {
        GameObject root = CreateImage(
            "TitleRoot", parent, Ink, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        CreateImage("CyanRail", root.transform, Cyan,
            new Vector2(0f, 0.83f), new Vector2(1f, 0.836f),
            Vector2.zero, Vector2.zero);
        CreateImage("MagentaRail", root.transform, Magenta,
            new Vector2(0f, 0.18f), new Vector2(1f, 0.188f),
            Vector2.zero, Vector2.zero);
        RectTransform slashA = CreateImage("SlashA", root.transform, Magenta,
            new Vector2(0.62f, -0.15f), new Vector2(0.72f, 1.15f),
            Vector2.zero, Vector2.zero).GetComponent<RectTransform>();
        slashA.localEulerAngles = new Vector3(0f, 0f, -13f);
        RectTransform slashB = CreateImage("SlashB", root.transform,
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f),
            new Vector2(0.76f, -0.15f), new Vector2(0.78f, 1.15f),
            Vector2.zero, Vector2.zero).GetComponent<RectTransform>();
        slashB.localEulerAngles = new Vector3(0f, 0f, -13f);

        CreateText("Index", root.transform, "02", font, 330f,
            new Color(1f, 1f, 1f, 0.055f), TextAlignmentOptions.Center,
            new Vector2(0.68f, 0.15f), new Vector2(0.98f, 0.88f),
            Vector2.zero, Vector2.zero);
        CreateText("Kicker", root.transform,
            "PROJECT  //  BORDERLAND ARCHIVE", font, 30f, Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0.075f, 0.74f), new Vector2(0.56f, 0.79f),
            Vector2.zero, Vector2.zero);
        CreateText("Title", root.transform, "ANKALUTE", font, 162f, White,
            TextAlignmentOptions.Left,
            new Vector2(0.07f, 0.49f), new Vector2(0.64f, 0.73f),
            Vector2.zero, Vector2.zero);
        CreateText("Subtitle", root.transform, "异界地宫", font, 72f, Magenta,
            TextAlignmentOptions.Left,
            new Vector2(0.075f, 0.42f), new Vector2(0.5f, 0.51f),
            Vector2.zero, Vector2.zero);
        CreateText("Tagline", root.transform,
            "在交界的深处，记录每一次无法撤回的选择。", font, 30f, Muted,
            TextAlignmentOptions.Left,
            new Vector2(0.075f, 0.34f), new Vector2(0.58f, 0.40f),
            Vector2.zero, Vector2.zero);

        startButton = CreateButton(
            "StartButton", root.transform, "开始探索   /   START", font,
            new Vector2(0.075f, 0.21f), new Vector2(0.34f, 0.29f),
            PanelSoft, Magenta, 34f);
        CreateText("Footer", root.transform,
            "ANKALUTE DEVELOPMENT BUILD    2026", font, 20f, Muted,
            TextAlignmentOptions.Left,
            new Vector2(0.075f, 0.08f), new Vector2(0.55f, 0.12f),
            Vector2.zero, Vector2.zero);
        return root;
    }

    private static GameObject BuildSettlement(
        Transform parent,
        TMP_FontAsset font,
        out Transform experienceContent,
        out DRPGSettlementExperienceRowView experienceTemplate,
        out Transform lootContent,
        out DRPGLootSlotView lootTemplate,
        out Transform backpackContent,
        out DRPGLootSlotView backpackTemplate,
        out TMP_Text lootEmpty,
        out TMP_Text capacity,
        out TMP_Text feedback,
        out UnityEngine.UI.Button collectAll,
        out UnityEngine.UI.Button continueButton)
    {
        GameObject root = CreateImage("SettlementRoot", parent,
            new Color(0.01f, 0.015f, 0.025f, 0.965f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateImage("TopRail", root.transform, Magenta,
            new Vector2(0f, 0.925f), new Vector2(1f, 0.934f),
            Vector2.zero, Vector2.zero);
        CreateText("Header", root.transform,
            "BATTLE REPORT  //  战斗结算", font, 56f, White,
            TextAlignmentOptions.Left,
            new Vector2(0.045f, 0.87f), new Vector2(0.75f, 0.925f),
            Vector2.zero, Vector2.zero);
        CreateText("Serial", root.transform, "RESULT / 01", font, 26f, Cyan,
            TextAlignmentOptions.Right,
            new Vector2(0.72f, 0.87f), new Vector2(0.955f, 0.92f),
            Vector2.zero, Vector2.zero);

        GameObject expPanel = CreatePanel("ExperiencePanel", root.transform,
            new Vector2(0.045f, 0.18f), new Vector2(0.275f, 0.84f));
        CreateSectionTitle(expPanel.transform, "PARTY GROWTH", "角色经验", font);
        experienceContent = CreateScrollContent(
            expPanel.transform, "Experience", 18f, 0.08f, 0.17f, 0.92f);
        experienceTemplate = CreateExperienceTemplate(
            experienceContent, font);

        GameObject lootPanel = CreatePanel("LootPanel", root.transform,
            new Vector2(0.295f, 0.18f), new Vector2(0.64f, 0.84f));
        CreateSectionTitle(lootPanel.transform, "ENCOUNTER LOOT", "战利品", font);
        lootContent = CreateScrollContent(
            lootPanel.transform, "Loot", 12f, 0.08f, 0.20f, 0.92f);
        lootTemplate = CreateLootTemplate(lootContent, font, "LootTemplate");
        lootEmpty = CreateText("LootEmpty", lootPanel.transform,
            "NO MATERIAL DROP\n本次没有获得道具", font, 30f, Muted,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.65f),
            Vector2.zero, Vector2.zero);

        GameObject backpackPanel = CreatePanel("BackpackPanel", root.transform,
            new Vector2(0.66f, 0.18f), new Vector2(0.955f, 0.84f));
        DRPGInventoryDropZone dropZone =
            backpackPanel.AddComponent<DRPGInventoryDropZone>();
        SerializedObject drop = new SerializedObject(dropZone);
        SetObject(drop, "highlight",
            backpackPanel.GetComponent<UnityEngine.UI.Image>());
        drop.ApplyModifiedPropertiesWithoutUndo();
        CreateSectionTitle(backpackPanel.transform, "TEAM BACKPACK", "团队背包", font);
        capacity = CreateText("Capacity", backpackPanel.transform, string.Empty,
            font, 22f, Cyan, TextAlignmentOptions.Right,
            new Vector2(0.12f, 0.84f), new Vector2(0.92f, 0.91f),
            Vector2.zero, Vector2.zero);
        backpackContent = CreateScrollContent(
            backpackPanel.transform, "Backpack", 12f, 0.08f, 0.20f, 0.82f);
        backpackTemplate = CreateLootTemplate(
            backpackContent, font, "BackpackTemplate");
        CreateText("DropHint", backpackPanel.transform,
            "将战利品拖入此区域", font, 22f, Muted,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.16f),
            Vector2.zero, Vector2.zero);

        collectAll = CreateButton("CollectAll", root.transform,
            "全部存入背包", font,
            new Vector2(0.40f, 0.075f), new Vector2(0.61f, 0.135f),
            PanelSoft, Cyan, 28f);
        continueButton = CreateButton("Continue", root.transform,
            "返回探索", font,
            new Vector2(0.75f, 0.075f), new Vector2(0.955f, 0.135f),
            PanelSoft, Magenta, 28f);
        feedback = CreateText("Feedback", root.transform, string.Empty, font,
            24f, White, TextAlignmentOptions.Left,
            new Vector2(0.045f, 0.075f), new Vector2(0.38f, 0.135f),
            Vector2.zero, Vector2.zero);
        return root;
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject panel = CreateImage(name, parent, Panel,
            anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        UnityEngine.UI.Outline outline = panel.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        CreateImage("Accent", panel.transform, Magenta,
            new Vector2(0f, 0.965f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero);
        return panel;
    }

    private static void CreateSectionTitle(
        Transform parent,
        string english,
        string chinese,
        TMP_FontAsset font)
    {
        CreateText("SectionTitle", parent, english, font, 30f, White,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
            Vector2.zero, Vector2.zero);
        CreateText("SectionSubtitle", parent, chinese, font, 20f, Magenta,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.89f),
            Vector2.zero, Vector2.zero);
    }

    private static Transform CreateScrollContent(
        Transform parent,
        string name,
        float spacing,
        float left,
        float bottom,
        float top)
    {
        GameObject viewport = CreateImage(name + "Viewport", parent,
            new Color(0f, 0f, 0f, 0.16f),
            new Vector2(left, bottom), new Vector2(1f - left, top),
            Vector2.zero, Vector2.zero);
        viewport.AddComponent<UnityEngine.UI.RectMask2D>();
        UnityEngine.UI.ScrollRect scroll = viewport.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;

        GameObject content = new GameObject(
            name + "Content", typeof(RectTransform),
            typeof(UnityEngine.UI.VerticalLayoutGroup),
            typeof(UnityEngine.UI.ContentSizeFitter));
        RectTransform rt = content.GetComponent<RectTransform>();
        rt.SetParent(viewport.transform, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        UnityEngine.UI.VerticalLayoutGroup layout =
            content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        // LayoutElement on each runtime row owns its preferred height. The
        // layout group must consume that value; otherwise cloned rows retain
        // the template's stretch anchors and render on top of one another.
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
            UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = rt;
        return content.transform;
    }

    private static DRPGSettlementExperienceRowView CreateExperienceTemplate(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject row = CreateLayoutRow("ExperienceTemplate", parent, 112f);
        DRPGSettlementExperienceRowView view =
            row.AddComponent<DRPGSettlementExperienceRowView>();
        UnityEngine.UI.Image portrait = CreateImage("Portrait", row.transform,
            new Color(1f, 1f, 1f, 0.04f),
            new Vector2(0.02f, 0.08f), new Vector2(0.20f, 0.92f),
            Vector2.zero, Vector2.zero).GetComponent<UnityEngine.UI.Image>();
        TMP_Text name = CreateText("Name", row.transform, "CHARACTER", font,
            25f, White, TextAlignmentOptions.Left,
            new Vector2(0.24f, 0.52f), new Vector2(0.98f, 0.90f),
            Vector2.zero, Vector2.zero);
        TMP_Text exp = CreateText("Experience", row.transform, "+0 EXP", font,
            22f, Magenta, TextAlignmentOptions.Left,
            new Vector2(0.24f, 0.13f), new Vector2(0.61f, 0.49f),
            Vector2.zero, Vector2.zero);
        TMP_Text level = CreateText("Level", row.transform, "LV 1", font,
            21f, Cyan, TextAlignmentOptions.Right,
            new Vector2(0.58f, 0.13f), new Vector2(0.96f, 0.49f),
            Vector2.zero, Vector2.zero);
        SerializedObject so = new SerializedObject(view);
        SetObject(so, "portrait", portrait);
        SetObject(so, "characterName", name);
        SetObject(so, "experienceText", exp);
        SetObject(so, "levelText", level);
        so.ApplyModifiedPropertiesWithoutUndo();
        row.SetActive(false);
        return view;
    }

    private static DRPGLootSlotView CreateLootTemplate(
        Transform parent,
        TMP_FontAsset font,
        string name)
    {
        GameObject row = CreateLayoutRow(name, parent, 96f);
        CanvasGroup group = row.AddComponent<CanvasGroup>();
        DRPGLootSlotView view = row.AddComponent<DRPGLootSlotView>();
        UnityEngine.UI.Image icon = CreateImage("Icon", row.transform,
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f),
            new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.88f),
            Vector2.zero, Vector2.zero).GetComponent<UnityEngine.UI.Image>();
        TMP_Text itemName = CreateText("Name", row.transform, "ITEM", font,
            24f, White, TextAlignmentOptions.Left,
            new Vector2(0.19f, 0.48f), new Vector2(0.72f, 0.88f),
            Vector2.zero, Vector2.zero);
        TMP_Text qty = CreateText("Quantity", row.transform, "×1", font,
            23f, Magenta, TextAlignmentOptions.Right,
            new Vector2(0.73f, 0.48f), new Vector2(0.97f, 0.88f),
            Vector2.zero, Vector2.zero);
        TMP_Text weight = CreateText("Weight", row.transform, "0.0 kg", font,
            18f, Muted, TextAlignmentOptions.Left,
            new Vector2(0.19f, 0.12f), new Vector2(0.70f, 0.47f),
            Vector2.zero, Vector2.zero);
        SerializedObject so = new SerializedObject(view);
        SetObject(so, "icon", icon);
        SetObject(so, "displayName", itemName);
        SetObject(so, "quantityText", qty);
        SetObject(so, "weightText", weight);
        SetObject(so, "canvasGroup", group);
        so.ApplyModifiedPropertiesWithoutUndo();
        row.SetActive(false);
        return view;
    }

    private static GameObject CreateLayoutRow(
        string name,
        Transform parent,
        float height)
    {
        GameObject row = CreateImage(name, parent, PanelSoft,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UnityEngine.UI.LayoutElement element =
            row.AddComponent<UnityEngine.UI.LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        UnityEngine.UI.Outline outline = row.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.38f);
        outline.effectDistance = new Vector2(1f, -1f);
        return row;
    }

    private static UnityEngine.UI.Button CreateButton(
        string name,
        Transform parent,
        string label,
        TMP_FontAsset font,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color baseColor,
        Color accent,
        float fontSize)
    {
        GameObject buttonObject = CreateImage(name, parent, baseColor,
            anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        UnityEngine.UI.Button button =
            buttonObject.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, accent, 0.5f);
        colors.pressedColor = accent;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        UnityEngine.UI.Outline outline =
            buttonObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(2f, -2f);
        CreateText("Label", buttonObject.transform, label, font, fontSize,
            White, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(16f, 8f),
            new Vector2(-16f, -8f));
        return button;
    }

    private static GameObject CreateImage(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.GetComponent<UnityEngine.UI.Image>().color = color;
        return go;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static void InstallInOpenScene(GameObject prefab)
    {
        GameObject manager = GameObject.Find("GameFlowManager");
        if (manager == null)
            throw new MissingReferenceException("GameFlowManager is missing.");

        Transform existing = manager.transform.Find("DRPGGameLoopCanvas");
        if (existing == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab,
                manager.transform);
            instance.name = "DRPGGameLoopCanvas";
            Undo.RegisterCreatedObjectUndo(instance, "Install DRPG game loop UI");
        }
        EditorSceneManager.MarkSceneDirty(manager.scene);
        EditorSceneManager.SaveScene(manager.scene);
    }

    private static TMP_FontAsset ResolveFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.IndexOf("SourceHan", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("NotoSans", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static void EnsureDebugEncounterRewards()
    {
        const string lootFolder =
            "Assets/GameResources/DRPG/Config/Test/Loot";
        EnsureFolder(lootFolder);
        DRPGLootItemDefinitionSO residue = EnsureLootDefinition(
            lootFolder + "/Loot_AbyssResidue.asset",
            "abyss_residue",
            "深渊残质",
            "从异形躯壳中剥离的低温残质。",
            0.4f,
            20);
        DRPGLootItemDefinitionSO plate = EnsureLootDefinition(
            lootFolder + "/Loot_FracturedPlate.asset",
            "fractured_plate",
            "破损甲片",
            "仍残留术式回路反应的装甲碎片。",
            1.2f,
            10);

        DRPGEncounterDefinitionSO encounter =
            AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>(
                "Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
        if (encounter == null)
            return;
        SerializedObject so = new SerializedObject(encounter);
        so.FindProperty("experienceRewardPerCharacter").longValue = 180L;
        SerializedProperty drops = so.FindProperty("lootDrops");
        drops.arraySize = 2;
        ConfigureDrop(drops.GetArrayElementAtIndex(0), residue, 1f, 2, 4);
        ConfigureDrop(drops.GetArrayElementAtIndex(1), plate, 1f, 1, 2);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
    }

    private static DRPGLootItemDefinitionSO EnsureLootDefinition(
        string path,
        string itemId,
        string displayName,
        string description,
        float weight,
        int maxStack)
    {
        DRPGLootItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<DRPGLootItemDefinitionSO>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();
            AssetDatabase.CreateAsset(item, path);
        }
        SerializedObject so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = itemId;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("maxStack").intValue = maxStack;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void ConfigureDrop(
        SerializedProperty property,
        DRPGLootItemDefinitionSO item,
        float chance,
        int minimum,
        int maximum)
    {
        property.FindPropertyRelative("item").objectReferenceValue = item;
        property.FindPropertyRelative("dropChance").floatValue = chance;
        property.FindPropertyRelative("minimumQuantity").intValue = minimum;
        property.FindPropertyRelative("maximumQuantity").intValue = maximum;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void SetObject(
        SerializedObject target,
        string propertyName,
        Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new System.MissingFieldException(
                target.targetObject.GetType().Name,
                propertyName);
        property.objectReferenceValue = value;
    }
}
#endif
