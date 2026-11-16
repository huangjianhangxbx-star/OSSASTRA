/*
 * 脚本：SideViewSliceTitleMenu
 * 功能：独立标题首页与八槽 uGUI；按钮只向应用会话流发送 New/Continue/Copy/Delete 请求。
 * 边界：不直接读写存档文件，不控制玩法角色；删除必须在 UI 中二次确认。
 */
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Ossastra.UI.GameMenu;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SideViewSliceTitleMenu : MonoBehaviour
    {
        public SideViewSliceSessionFlow sessionFlow;
        public GameMenuTheme theme;

        private enum Page { Home, Slots }
        private enum Mode { Select, CopyTarget, DeleteConfirm }
        private Page page = Page.Home;
        private Mode mode;
        private int selected, copySource, homeSelected;
        private SliceSlotPreview[] previews = Array.Empty<SliceSlotPreview>();
        private Text[] slotLabels = Array.Empty<Text>();
        private Image[] slotImages = Array.Empty<Image>();
        private Text statusLabel, pageTitle;
        private RectTransform homePage, slotsPage;
        private Button[] homeButtons = Array.Empty<Button>();
        private InputAction previous, next, submit, back, copy, delete;

        private void Awake()
        {
            previous = ButtonAction("PreviousSlot", "<Keyboard>/upArrow", "<Keyboard>/leftArrow",
                "<Gamepad>/dpad/up", "<Gamepad>/dpad/left");
            next = ButtonAction("NextSlot", "<Keyboard>/downArrow", "<Keyboard>/rightArrow",
                "<Gamepad>/dpad/down", "<Gamepad>/dpad/right");
            submit = ButtonAction("SelectSlot", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            back = ButtonAction("CancelSlotAction", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            copy = ButtonAction("CopySlot", "<Keyboard>/c", "<Gamepad>/buttonWest");
            delete = ButtonAction("DeleteSlot", "<Keyboard>/delete", "<Gamepad>/buttonNorth");
        }

        private static InputAction ButtonAction(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }

        private void OnEnable()
        {
            previous.Enable(); next.Enable(); submit.Enable(); back.Enable(); copy.Enable(); delete.Enable();
        }

        private void OnDisable()
        {
            previous.Disable(); next.Disable(); submit.Disable(); back.Disable(); copy.Disable(); delete.Disable();
        }

        private void OnDestroy()
        {
            previous.Dispose(); next.Dispose(); submit.Dispose(); back.Dispose(); copy.Dispose(); delete.Dispose();
        }

        private void Start()
        {
            if (sessionFlow == null) sessionFlow = SideViewSliceSessionFlow.Instance;
            if (theme == null || sessionFlow == null)
            { Debug.LogError("[SV-SLICE-01] 标题缺少 GameMenuTheme 或会话流", this); enabled = false; return; }
            Build();
            ShowHome();
        }

        private void Update()
        {
            if (sessionFlow == null || sessionFlow.Busy) return;
            if (page == Page.Home)
            {
                if (previous.WasPressedThisFrame() || next.WasPressedThisFrame())
                { homeSelected = 1 - homeSelected; RefreshHome(); }
                if (submit.WasPressedThisFrame())
                {
                    if (homeSelected == 0) ShowSlots();
                    else Application.Quit();
                }
                return;
            }
            if (previous.WasPressedThisFrame()) Select((selected + 7) % 8);
            else if (next.WasPressedThisFrame()) Select((selected + 1) % 8);
            if (submit.WasPressedThisFrame()) ActivateSelected();
            else if (copy.WasPressedThisFrame()) BeginCopy();
            else if (delete.WasPressedThisFrame()) RequestDelete();
            else if (back.WasPressedThisFrame())
            {
                if (mode == Mode.Select) ShowHome();
                else { mode = Mode.Select; Refresh(); }
            }
            if (statusLabel != null && mode == Mode.Select) statusLabel.text = sessionFlow.Status;
        }

        private void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, 7);
            Refresh();
        }

        private void ActivateSelected()
        {
            RefreshPreviews();
            if (mode == Mode.CopyTarget)
            {
                if (previews[selected].State == SliceSlotState.Empty
                    && sessionFlow.CopySlot(SliceSlotCatalog.SlotIds[copySource], SliceSlotCatalog.SlotIds[selected]))
                    mode = Mode.Select;
                Refresh();
                return;
            }
            if (mode == Mode.DeleteConfirm) { RequestDelete(); return; }
            var slot = previews[selected];
            if (slot.State == SliceSlotState.Empty) sessionFlow.StartNew(slot.ProfileId);
            else if (slot.State == SliceSlotState.Loadable) sessionFlow.Continue(slot.ProfileId);
            Refresh();
        }

        private void BeginCopy()
        {
            RefreshPreviews();
            if (mode == Mode.CopyTarget) { mode = Mode.Select; Refresh(); return; }
            if (previews[selected].State != SliceSlotState.Loadable) return;
            copySource = selected;
            mode = Mode.CopyTarget;
            Refresh();
        }

        private void RequestDelete()
        {
            RefreshPreviews();
            if (mode != Mode.DeleteConfirm)
            {
                if (previews[selected].State == SliceSlotState.Empty) return;
                mode = Mode.DeleteConfirm;
            }
            else
            {
                sessionFlow.DeleteSlot(SliceSlotCatalog.SlotIds[selected]);
                mode = Mode.Select;
            }
            Refresh();
        }

        private void RefreshPreviews() => previews = sessionFlow.InspectSlots();

        private void ShowHome()
        {
            page = Page.Home;
            mode = Mode.Select;
            if (homePage != null) homePage.gameObject.SetActive(true);
            if (slotsPage != null) slotsPage.gameObject.SetActive(false);
            if (pageTitle != null) pageTitle.text = "开始旅程";
            RefreshHome();
        }

        private void ShowSlots()
        {
            page = Page.Slots;
            mode = Mode.Select;
            if (homePage != null) homePage.gameObject.SetActive(false);
            if (slotsPage != null) slotsPage.gameObject.SetActive(true);
            if (pageTitle != null) pageTitle.text = "选择档案";
            Refresh();
        }

        private void RefreshHome()
        {
            for (int i = 0; i < homeButtons.Length; ++i)
            {
                var image = homeButtons[i].GetComponent<Image>();
                image.color = i == homeSelected
                    ? new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, .22f)
                    : new Color(theme.Background.r, theme.Background.g, theme.Background.b, .84f);
                homeButtons[i].GetComponentInChildren<Text>().color =
                    i == homeSelected ? theme.Accent : theme.Text;
            }
        }

        private void Refresh()
        {
            if (sessionFlow == null || slotLabels.Length == 0) return;
            RefreshPreviews();
            for (int i = 0; i < slotLabels.Length; ++i)
            {
                var row = previews[i];
                string detail = row.State == SliceSlotState.Empty ? "空槽"
                    : row.State == SliceSlotState.Invalid ? "损坏或不兼容"
                    : row.CheckpointMapId + "  /  " + row.CheckpointAnchorId
                        + (row.IsCompleted ? "  ·  已完成" : "");
                slotLabels[i].text = (i + 1).ToString("00") + "   " + detail;
                slotLabels[i].color = i == selected ? theme.Accent : theme.Text;
                slotImages[i].color = i == selected
                    ? new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, .22f)
                    : new Color(theme.Background.r, theme.Background.g, theme.Background.b, .84f);
            }
            if (statusLabel != null)
                statusLabel.text = mode == Mode.CopyTarget ? "复制：选择一个空槽，按 Enter / A 确认；Esc / B 取消"
                    : mode == Mode.DeleteConfirm ? "删除将移除所选档案。再次按 Delete / Y 确认；Esc / B 取消"
                    : sessionFlow.Status;
        }

        private void Build()
        {
            var canvasObject = new GameObject("SliceTitleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var root = canvasObject.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0, 1);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(1920, 1080);
            Image(root, "Background", new Rect(0, 0, 1920, 1080), theme.Background,
                theme.Get("background.base"));
            Image(root, "Lines", new Rect(0, 0, 1920, 1080), new Color(1, 1, 1, .52f),
                theme.Get("background.lines")).raycastTarget = false;
            Label(root, "GameTitle", new Rect(120, 74, 1100, 84), "星骸回廊", 64, theme.Text);
            pageTitle = Label(root, "PageTitle", new Rect(122, 165, 1050, 56), "开始旅程", 35, theme.Accent);
            homePage = Node(root, "HomePage", new Rect(0, 0, 1920, 1080));
            homeButtons = new[]
            {
                Button(homePage, "Start", new Rect(120, 386, 440, 88), "开始", ShowSlots),
                Button(homePage, "Exit", new Rect(120, 494, 440, 88), "退出", Application.Quit)
            };
            Label(homePage, "HomeHints", new Rect(120, 933, 1200, 48),
                "方向键 / 十字键 选择     Enter / A 确认", 20, theme.Muted);
            slotsPage = Node(root, "SlotsPage", new Rect(0, 0, 1920, 1080));
            slotLabels = new Text[8];
            slotImages = new Image[8];
            for (int i = 0; i < 8; ++i)
            {
                int index = i;
                float x = 120 + (i % 4) * 425;
                float y = 280 + (i / 4) * 205;
                var button = Button(slotsPage, "Slot" + i, new Rect(x, y, 400, 174), "", () => Select(index));
                button.onClick.AddListener(() => { if (mode == Mode.CopyTarget) ActivateSelected(); });
                slotImages[i] = button.GetComponent<Image>();
                slotLabels[i] = button.GetComponentInChildren<Text>();
                slotLabels[i].alignment = TextAnchor.MiddleLeft;
                slotLabels[i].rectTransform.offsetMin = new Vector2(24, 12);
                slotLabels[i].rectTransform.offsetMax = new Vector2(-18, -12);
            }
            Button(slotsPage, "New", new Rect(120, 738, 250, 70), "新游戏", () =>
            { RefreshPreviews(); if (previews[selected].State == SliceSlotState.Empty) sessionFlow.StartNew(previews[selected].ProfileId); Refresh(); });
            Button(slotsPage, "Continue", new Rect(393, 738, 250, 70), "继续", () =>
            { RefreshPreviews(); if (previews[selected].State == SliceSlotState.Loadable) sessionFlow.Continue(previews[selected].ProfileId); Refresh(); });
            Button(slotsPage, "Copy", new Rect(666, 738, 250, 70), "复制", BeginCopy);
            Button(slotsPage, "Delete", new Rect(939, 738, 250, 70), "删除", RequestDelete);
            Button(slotsPage, "Back", new Rect(1212, 738, 250, 70), "返回", ShowHome);
            statusLabel = Label(slotsPage, "Status", new Rect(120, 853, 1600, 72), "", 25, theme.Text);
            Label(slotsPage, "Hints", new Rect(120, 966, 1600, 45),
                "方向键 / 十字键 选择     Enter / A 进入     C / X 复制     Delete / Y 删除     Esc / B 返回", 20, theme.Muted);
            slotsPage.gameObject.SetActive(false);
            if (EventSystem.current == null)
            {
                var events = new GameObject("TitleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static RectTransform Node(Transform parent, string name, Rect rect)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = node.pivot = new Vector2(0, 1);
            node.anchoredPosition = new Vector2(rect.x, -rect.y);
            node.sizeDelta = rect.size;
            return node;
        }

        private static Image Image(Transform parent, string name, Rect rect, Color color, Sprite sprite = null)
        {
            var node = Node(parent, name, rect);
            var image = node.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        private Text Label(Transform parent, string name, Rect rect, string text, int size, Color color)
        {
            var node = Node(parent, name, rect);
            var label = node.gameObject.AddComponent<Text>();
            label.font = theme.Font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private Button Button(Transform parent, string name, Rect rect, string text, Action onClick)
        {
            var baseImage = Image(parent, name, rect, theme.Background, theme.Get("tabs.line"));
            var button = baseImage.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            var label = Label(baseImage.transform, "Label", new Rect(20, 8, rect.width - 40, rect.height - 16),
                text, 26, theme.Text);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
