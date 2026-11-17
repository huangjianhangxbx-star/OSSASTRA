/*
 * The title owns uGUI focus and delegates all slot operations to SideViewSliceSessionFlow.
 */
using System;
using Ossastra.UI.GameMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SliceAudioRoot = Ossastra.Audio.GameAudioRoot;
using static Ossastra.SideView.Slice.SlicePresentation;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SideViewSliceTitleMenu : MonoBehaviour
    {
        public SideViewSliceSessionFlow sessionFlow;
        public GameMenuTheme theme;

        private enum Screen { Home, Slots, Settings }
        private enum SlotPage { Grid, Actions, CopyTarget, DeleteConfirm }

        private Screen screen;
        private SlotPage slotPage;
        private int selected, copySource, choice, homeSelected, settingsSelected;
        private readonly InputAction[] directions = new InputAction[4];
        private InputAction submit, back, copy, delete;
        private bool gamepad, settingsChanged;
        private RectTransform home, slots, settings, dialog;
        private Graphic dialogCurtain;
        private Button[] homeButtons, contextButtons, confirmButtons, settingsButtons;
        private SlotCard[] cards;
        private SlotCard contextCard;
        private Text status, sectionTitle, contextDescription, confirmQuestion;
        private RawImage enterKey, escapeKey;
        private Text enterText, escapeText;
        private Text[] settingLabels;
        private SliceSlotPreview[] previews = Array.Empty<SliceSlotPreview>();

        private sealed class SlotCard
        {
            public RawImage Preview, Crest;
            public Text Number, Empty, Location, Detail;
            public GameObject Focus;
        }

        private void Awake()
        {
            directions[0] = Input("Up", "upArrow", "dpad/up", "leftStick/up");
            directions[1] = Input("Down", "downArrow", "dpad/down", "leftStick/down");
            directions[2] = Input("Left", "leftArrow", "dpad/left", "leftStick/left");
            directions[3] = Input("Right", "rightArrow", "dpad/right", "leftStick/right");
            submit = Input("Submit", "enter", "buttonSouth");
            submit.AddBinding("<Keyboard>/numpadEnter");
            back = Input("Back", "escape", "buttonEast");
            copy = Input("Copy", "c", "buttonWest");
            delete = Input("Delete", "delete", "buttonNorth");
        }

        private InputAction Input(string name, string keyboard, string pad, string alternate = null)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding("<Keyboard>/" + keyboard);
            action.AddBinding("<Gamepad>/" + pad);
            if (alternate != null) action.AddBinding("<Gamepad>/" + alternate);
            action.performed += context => gamepad = context.control.device is Gamepad;
            return action;
        }

        private void OnEnable()
        {
            foreach (var direction in directions) direction.Enable();
            submit.Enable();
            back.Enable();
            copy.Enable();
            delete.Enable();
        }

        private void OnDisable()
        {
            foreach (var direction in directions) direction.Disable();
            submit.Disable();
            back.Disable();
            copy.Disable();
            delete.Disable();
        }

        private void OnDestroy()
        {
            foreach (var direction in directions) direction.Dispose();
            submit.Dispose();
            back.Dispose();
            copy.Dispose();
            delete.Dispose();
        }

        private void Start()
        {
            // Single 回标题会重建视图，使用本轮一直存活的存档会话。
            var activeFlow = SideViewSliceSessionFlow.Instance;
            if (activeFlow != null) sessionFlow = activeFlow;
            if (theme == null || sessionFlow == null)
            {
                Debug.LogError("[SV-SLICE] 标题缺少主题或会话流。", this);
                enabled = false;
                return;
            }
            Build();
            Show(Screen.Home);
        }

        private void Update()
        {
            if (home == null || sessionFlow == null) return;
            RefreshFooter();
            if (sessionFlow.Busy)
            {
                if (screen == Screen.Slots) status.text = sessionFlow.Status;
                return;
            }

            int vertical = directions[0].WasPressedThisFrame() ? -1 : directions[1].WasPressedThisFrame() ? 1 : 0;
            int horizontal = directions[2].WasPressedThisFrame() ? -1 : directions[3].WasPressedThisFrame() ? 1 : 0;
            if (screen == Screen.Home)
            {
                if (vertical != 0) SelectHome(Wrap(homeSelected + vertical, homeButtons.Length));
                if (submit.WasPressedThisFrame()) ActivateHome(homeSelected);
                return;
            }

            if (screen == Screen.Settings)
            {
                if (vertical != 0) SelectSetting(Wrap(settingsSelected + vertical, settingsButtons.Length));
                if (back.WasPressedThisFrame()) CloseSettings();
                else if (horizontal != 0) AdjustSettings(settingsSelected, horizontal);
                else if (submit.WasPressedThisFrame()) AdjustSettings(settingsSelected, 1);
                return;
            }

            if (back.WasPressedThisFrame()) { BackFromSlots(); return; }
            if (slotPage == SlotPage.Grid || slotPage == SlotPage.CopyTarget)
            {
                if (vertical != 0 || horizontal != 0)
                    SelectSlot(Wrap(selected / 4 + vertical, 2) * 4 + Wrap(selected % 4 + horizontal, 4));
                if (submit.WasPressedThisFrame()) ActivateSlot();
                else if (slotPage == SlotPage.Grid && copy.WasPressedThisFrame()) BeginCopy();
                else if (slotPage == SlotPage.Grid && delete.WasPressedThisFrame()) BeginDelete();
                return;
            }

            int count = slotPage == SlotPage.Actions ? contextButtons.Length : confirmButtons.Length;
            if (vertical != 0 || horizontal != 0) SelectChoice(Wrap(choice + vertical + horizontal, count));
            if (submit.WasPressedThisFrame()) ActivateChoice(choice);
        }

        private static int Wrap(int index, int length) => (index + length) % length;

        private void Show(Screen target)
        {
            screen = target;
            home.gameObject.SetActive(target == Screen.Home);
            slots.gameObject.SetActive(target == Screen.Slots);
            settings.gameObject.SetActive(target == Screen.Settings);
            if (target == Screen.Home) SelectHome(homeSelected);
            if (target == Screen.Slots)
            {
                slotPage = SlotPage.Grid;
                RefreshSlots();
            }
            if (target == Screen.Settings)
            {
                settingsChanged = false;
                RefreshSettings();
            }
            RefreshFooter();
        }

        private void SelectHome(int index)
        {
            if (homeSelected != index) homeSelected = index;
            SetFocus(homeButtons, index);
        }

        private void ActivateHome(int index)
        {
            if (sessionFlow.Busy) return;
            if (index == 0) Show(Screen.Slots);
            else if (index == 1) Show(Screen.Settings);
            else Application.Quit();
        }

        private void SelectSlot(int index)
        {
            if (sessionFlow.Busy || (slotPage != SlotPage.Grid && slotPage != SlotPage.CopyTarget)) return;
            selected = index;
            RefreshSlotPresentation();
        }

        private void ActivateSlot()
        {
            if (sessionFlow.Busy || previews.Length != cards.Length) return;
            var slot = previews[selected];
            if (slotPage == SlotPage.CopyTarget)
            {
                if (slot.State != SliceSlotState.Empty) { status.text = "请选择空槽"; return; }
                bool copied = sessionFlow.CopySlot(SliceSlotCatalog.SlotIds[copySource], slot.ProfileId);
                if (copied) slotPage = SlotPage.Grid;
                RefreshSlots();
                if (!copied) status.text = sessionFlow.Status;
                return;
            }

            switch (slot.State)
            {
                case SliceSlotState.Empty:
                    sessionFlow.StartNew(slot.ProfileId);
                    status.text = sessionFlow.Status;
                    break;
                case SliceSlotState.Loadable:
                case SliceSlotState.Invalid:
                    OpenSlotActions();
                    break;
            }
        }

        private void BackFromSlots()
        {
            switch (slotPage)
            {
                case SlotPage.CopyTarget:
                    selected = copySource;
                    OpenSlotActions(1);
                    break;
                case SlotPage.DeleteConfirm:
                    OpenSlotActions(2);
                    break;
                case SlotPage.Actions:
                    slotPage = SlotPage.Grid;
                    RefreshSlots();
                    break;
                default:
                    Show(Screen.Home);
                    break;
            }
        }

        private void OpenSlotActions(int preferred = 0)
        {
            slotPage = SlotPage.Actions;
            choice = previews[selected].State == SliceSlotState.Loadable ? preferred : 2;
            RefreshSlotPresentation();
        }

        private void BeginCopy()
        {
            if (previews.Length != cards.Length || previews[selected].State != SliceSlotState.Loadable) return;
            copySource = selected;
            int empty = Array.FindIndex(previews, row => row.State == SliceSlotState.Empty);
            if (empty < 0)
            {
                contextDescription.text = "没有可供复制的空槽";
                status.text = contextDescription.text;
                return;
            }
            selected = empty;
            slotPage = SlotPage.CopyTarget;
            RefreshSlotPresentation();
        }

        private void BeginDelete()
        {
            if (previews.Length != cards.Length || previews[selected].State == SliceSlotState.Empty) return;
            slotPage = SlotPage.DeleteConfirm;
            choice = 1; // A separate action is required to select the destructive choice.
            RefreshSlotPresentation();
        }

        private void SelectChoice(int index)
        {
            if (slotPage == SlotPage.Actions && !contextButtons[index].interactable)
                index = 2;
            choice = index;
            RefreshSlotPresentation();
        }

        private void ActivateChoice(int index)
        {
            if (sessionFlow.Busy || previews.Length != cards.Length) return;
            var slot = previews[selected];
            if (slotPage == SlotPage.DeleteConfirm)
            {
                if (index != 0) { OpenSlotActions(2); return; }
                bool removed = sessionFlow.DeleteSlot(slot.ProfileId);
                if (removed) slotPage = SlotPage.Grid;
                RefreshSlots();
                if (!removed) contextDescription.text = sessionFlow.Status;
                return;
            }
            if (index == 0 && slot.State == SliceSlotState.Loadable)
            {
                sessionFlow.Continue(slot.ProfileId);
                contextDescription.text = sessionFlow.Status;
            }
            else if (index == 1) BeginCopy();
            else if (index == 2) BeginDelete();
        }

        private void RefreshSlots()
        {
            previews = sessionFlow.InspectSlots();
            RefreshSlotPresentation();
        }

        private void RefreshSlotPresentation()
        {
            bool modal = slotPage == SlotPage.Actions || slotPage == SlotPage.DeleteConfirm;
            dialog.gameObject.SetActive(modal);
            dialogCurtain.gameObject.SetActive(modal);
            sectionTitle.text = slotPage == SlotPage.CopyTarget ? "选择复制目标" : "存档槽位";
            string sessionStatus = sessionFlow.Status;
            status.text = slotPage == SlotPage.CopyTarget ? "选择空槽"
                : sessionStatus.Contains("失败") || sessionStatus == "槽位已复制" || sessionStatus == "槽位已删除"
                    ? sessionStatus : "";
            for (int i = 0; i < cards.Length; i++)
                PaintCard(cards[i], i < previews.Length ? previews[i] : null, i, i == selected && !modal);
            if (!modal || previews.Length != cards.Length) return;

            var row = previews[selected];
            PaintCard(contextCard, row, selected, false);
            bool confirm = slotPage == SlotPage.DeleteConfirm;
            foreach (var button in contextButtons) button.gameObject.SetActive(!confirm);
            foreach (var button in confirmButtons) button.gameObject.SetActive(confirm);
            confirmQuestion.gameObject.SetActive(confirm);
            if (confirm) confirmQuestion.text = "删除存档 " + (selected + 1) + "？";
            contextButtons[0].interactable = row.State == SliceSlotState.Loadable;
            contextButtons[1].interactable = row.State == SliceSlotState.Loadable;
            SetFocus(confirm ? confirmButtons : contextButtons, choice);
            contextDescription.text = confirm ? "删除后无法恢复" : choice == 2 ? "删除所选存档" :
                choice == 1 ? "复制到空槽" : "读取所选存档";
        }

        private static void PaintCard(SlotCard card, SliceSlotPreview row, int index, bool focused)
        {
            bool valid = row?.State == SliceSlotState.Loadable;
            bool invalid = row?.State == SliceSlotState.Invalid;
            bool rest = valid && row.CheckpointMapId == SliceSceneCatalog.TowerRestMapId &&
                row.CheckpointAnchorId == SliceSceneCatalog.RestCheckpointAnchorId;
            card.Preview.enabled = rest && card.Preview.texture != null;
            card.Crest.enabled = !card.Preview.enabled && card.Crest.texture != null;
            card.Number.text = (index + 1).ToString();
            card.Empty.text = row == null ? "正在读取…" : invalid ? "档案损坏或不兼容" : valid ? "" : "新存档";
            card.Empty.color = invalid ? new Color32(233, 156, 148, 255) : TextColor;
            card.Location.text = valid ? LocationName(row.CheckpointMapId) : "";
            card.Detail.text = valid ? row.IsCompleted ? "已完成" : rest ? "保存椅" : "检查点" : "";
            card.Focus.SetActive(focused);
        }

        private static string LocationName(string mapId) => mapId switch
        {
            SliceSceneCatalog.Tut01MapId => "初始洞穴",
            SliceSceneCatalog.Tut02MapId => "苔骨洞窟",
            SliceSceneCatalog.TowerRestMapId => "破落的房间",
            SliceSceneCatalog.TowerLiftMapId => "锁之塔 · 升降井",
            SliceSceneCatalog.TowerCombatMapId => "锁之塔 · 廊道",
            SliceSceneCatalog.TowerBossMapId => "苔骨母巢",
            _ => mapId
        };

        private void SelectSetting(int index)
        {
            settingsSelected = index;
            SetFocus(settingsButtons, index);
        }

        private void AdjustSettings(int index, int step)
        {
            if (index == 3) { CloseSettings(); return; }
            var mix = SliceAudioRoot.Instance?.UnifiedMix;
            if (mix == null) return;
            float delta = step * .05f;
            if (index == 0) mix.MasterVolume += delta;
            if (index == 1) mix.BgmVolume += delta;
            if (index == 2) mix.SfxVolume += delta;
            settingsChanged = true;
            RefreshSettings();
        }

        private void CloseSettings()
        {
            if (settingsChanged) SliceAudioRoot.Instance?.SaveUserSettings();
            Show(Screen.Home);
        }

        private void RefreshSettings()
        {
            var mix = SliceAudioRoot.Instance?.UnifiedMix;
            float[] values = { mix?.MasterVolume ?? 0, mix?.BgmVolume ?? 0, mix?.SfxVolume ?? 0 };
            for (int i = 0; i < values.Length; ++i)
            {
                settingLabels[i].text = mix != null ? Mathf.RoundToInt(values[i] * 100) + "%" : "不可用";
                settingsButtons[i].interactable = mix != null;
            }
            SelectSetting(settingsSelected);
        }

        private void RefreshFooter()
        {
            enterKey.texture = Art(gamepad ? "A" : "Enter");
            escapeKey.texture = Art(gamepad ? "B" : "Escape");
            enterKey.enabled = enterKey.texture != null;
            escapeKey.enabled = escapeKey.texture != null && screen != Screen.Home;
            float confirmX = screen == Screen.Home ? 138f : 0f;
            enterKey.rectTransform.anchoredPosition = new Vector2(9 + confirmX, -5);
            enterText.rectTransform.anchoredPosition = new Vector2(63 + confirmX, 0);
            enterText.text = enterKey.enabled ? "确定" : gamepad ? "A  确定" : "Enter  确定";
            escapeText.text = screen == Screen.Home ? "" : escapeKey.enabled ? "取消" : gamepad ? "B  取消" : "Esc  取消";
        }

        private Font Font => theme.Font;

        private void Build()
        {
            var canvas = Canvas(transform, "SliceTitleCanvas", 200, true);
            var backing = Image(canvas, "暗底", default, new Color32(8, 8, 10, 255));
            Stretch(backing.rectTransform);
            var fog = ArtImage(canvas, "原作标题雾层", default, new Color(.64f, .64f, .68f, 1f), "TitleFog");
            Stretch(fog.rectTransform);
            var fit = fog.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;

            var main = Node(canvas, "标题构图", new Rect(0, 0, 1920, 1080));
            main.anchorMin = main.anchorMax = main.pivot = new Vector2(.5f, .5f);
            main.anchoredPosition = Vector2.zero;
            ArtImage(main, "雾光", new Rect(246, 128, 1428, 555), new Color(1, 1, 1, .075f), "TitleSmoke");
            ArtImage(main, "中央徽记", new Rect(644, 86, 632, 635), new Color(1, 1, 1, .13f), "Crest");
            var title = Label(main, "游戏名", new Rect(185, 318, 1550, 163), "星 骸 回 廊", 102, Font, TextColor);
            Shadow(title);

            home = Node(main, "首页", new Rect(0, 0, 1920, 1080));
            string[] commands = { "开始", "设置", "退出" };
            homeButtons = new Button[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                int index = i;
                homeButtons[i] = MenuButton(home, commands[i], new Rect(727, 691 + i * 76, 466, 67),
                    () => ActivateHome(index), () => SelectHome(index));
            }

            BuildSlots(main);
            BuildSettings(main);
            var footer = Node(canvas, "操作提示", new Rect(0, 0, 292, 44));
            footer.anchorMin = footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(1, 0);
            footer.anchoredPosition = new Vector2(-24, 21);
            enterKey = Image(footer, "确认键", new Rect(9, 5, 54, 35), Color.white);
            enterText = Label(footer, "确定", new Rect(63, 0, 77, 44), "", 23, Font, TextColor);
            escapeKey = Image(footer, "取消键", new Rect(150, 5, 54, 35), Color.white);
            escapeText = Label(footer, "取消", new Rect(204, 0, 77, 44), "", 23, Font, TextColor);
            if (EventSystem.current == null)
            {
                var events = new GameObject("TitleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void BuildSlots(RectTransform main)
        {
            slots = Node(main, "选择存档", new Rect(0, 0, 1920, 1080));
            var veil = Image(slots, "标题暗幕", default, new Color(0, 0, 0, .68f));
            Stretch(veil.rectTransform);
            sectionTitle = Label(slots, "页名", new Rect(500, 30, 920, 64), "存档槽位", 36, Font, TextColor);
            Shadow(sectionTitle);
            Rule(slots, "分隔线", new Rect(26, 103, 1868, 2), new Color(1, 1, 1, .33f));
            cards = new SlotCard[8];
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i] = BuildCard(slots, "存档 " + (i + 1),
                    new Rect(82 + 444 * (i % 4), 251 + 329 * (i / 4), 386, 264),
                    () => { SelectSlot(index); ActivateSlot(); }, () => SelectSlot(index));
            }
            status = Label(slots, "状态", new Rect(570, 893, 780, 51), "", 22, Font, AccentColor);
            dialogCurtain = Image(slots, "遮罩", default, new Color(0, 0, 0, .75f));
            dialogCurtain.raycastTarget = true;
            Stretch(dialogCurtain.rectTransform);
            dialog = Node(slots, "存档操作", new Rect(0, 232, 1920, 520));
            Image(dialog, "操作底面", new Rect(0, 0, 1920, 520), new Color32(31, 30, 35, 255));
            ArtImage(dialog, "上缘渐隐", new Rect(0, -24, 1920, 50), new Color(1, 1, 1, .52f), "DialogFade");
            ArtImage(dialog, "下缘渐隐", new Rect(0, 494, 1920, 50), new Color(1, 1, 1, .52f), "DialogFade");
            contextCard = BuildCard(dialog, "所选存档", new Rect(461, 59, 386, 264), null, null);
            contextButtons = new Button[3];
            string[] actions = { "开始", "复制", "删除" };
            for (int i = 0; i < actions.Length; i++)
            {
                int index = i;
                contextButtons[i] = MenuButton(dialog, actions[i], new Rect(961, 68 + 80 * i, 461, 68),
                    () => ActivateChoice(index), () => SelectChoice(index));
            }
            confirmQuestion = Label(dialog, "确认问题", new Rect(961, 68, 461, 62), "", 34, Font, TextColor);
            confirmButtons = new[]
            {
                MenuButton(dialog, "是", new Rect(961, 148, 461, 68), () => ActivateChoice(0), () => SelectChoice(0)),
                MenuButton(dialog, "否", new Rect(961, 228, 461, 68), () => ActivateChoice(1), () => SelectChoice(1))
            };
            Rule(dialog, "说明分隔线", new Rect(162, 385, 1596, 2), new Color(1, 1, 1, .33f));
            contextDescription = Label(dialog, "操作说明", new Rect(430, 392, 1060, 55), "", 28, Font, AccentColor);
        }

        private SlotCard BuildCard(Transform parent, string name, Rect rect, Action activate, Action focus)
        {
            var card = Image(parent, name, rect, Color.black);
            if (activate != null) AddButton(card, activate, focus);
            var root = card.rectTransform;
            var result = new SlotCard
            {
                Preview = ArtImage(root, "检查点预览", new Rect(7, 8, 372, 171), Color.white, "BenchPreview"),
                Crest = ArtImage(root, "空槽徽记", new Rect(79, 16, 228, 229), new Color(1, 1, 1, .038f), "Crest"),
                Number = Label(root, "编号", new Rect(18, 10, 105, 49), "", 36, Font, TextColor, TextAnchor.MiddleLeft),
                Empty = Label(root, "档案状态", new Rect(22, 104, 342, 56), "", 22, Font, TextColor),
                Detail = Label(root, "进度", new Rect(19, 180, 344, 33), "", 20, Font, TextColor, TextAnchor.MiddleLeft),
                Location = Label(root, "地点", new Rect(19, 210, 347, 42), "", 27, Font, AccentColor, TextAnchor.MiddleLeft)
            };
            Shadow(result.Number);
            var frame = Node(root, "槽卡边框", new Rect(0, 0, 386, 264));
            SlotFrame(frame);
            var glow = Node(root, "焦点", new Rect(0, 0, 386, 264));
            var left = ArtImage(glow, "左焦点", new Rect(-25, -18, 27, 300), Color.white, "SlotFocus");
            left.uvRect = new Rect(1, 0, -1, 1);
            ArtImage(glow, "右焦点", new Rect(384, -18, 27, 300), Color.white, "SlotFocus");
            result.Focus = glow.gameObject;
            return result;
        }

        // The square frame's authored corners retain their pixel shape on the rectangular slot card.
        private static void SlotFrame(RectTransform parent)
        {
            var texture = Art("SlotFrame");
            var color = new Color(1, 1, 1, .24f);
            if (texture == null)
            {
                Rule(parent, "槽卡顶边", new Rect(5, 4, 376, 2), color);
                Rule(parent, "槽卡底边", new Rect(5, 257, 376, 2), color);
                return;
            }
            float[] x = { 0, 23, 363, 386 };
            float[] y = { 0, 23, 241, 264 };
            float[] u = { 0f, .25f, .75f, 1f };
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                if (row == 1 && col == 1) continue;
                var image = Image(parent, "边框", new Rect(x[col], y[row], x[col + 1] - x[col], y[row + 1] - y[row]), color);
                image.texture = texture;
                image.uvRect = Rect.MinMaxRect(u[col], 1 - u[row + 1], u[col + 1], 1 - u[row]);
            }
        }

        private void BuildSettings(RectTransform main)
        {
            settings = Node(main, "设置", new Rect(0, 0, 1920, 1080));
            Stretch(Image(settings, "设置暗幕", default, new Color(0, 0, 0, .84f)).rectTransform);
            Label(settings, "设置标题", new Rect(406, 64, 1108, 65), "设置", 36, Font, TextColor);
            Rule(settings, "设置分隔线", new Rect(340, 132, 1240, 2), new Color(1, 1, 1, .38f));
            string[] labels = { "总音量", "背景音乐", "音效", "返回" };
            settingLabels = new Text[3];
            settingsButtons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                settingsButtons[i] = MenuButton(settings, labels[i], new Rect(709, 311 + i * 84, 502, 67),
                    () => AdjustSettings(index, 1), () => SelectSetting(index));
                if (i < settingLabels.Length)
                    settingLabels[i] = Label(settings, "音量", new Rect(1218, 311 + i * 84, 155, 67),
                        "", 25, Font, AccentColor);
            }
        }

        private Button MenuButton(Transform parent, string label, Rect rect, Action activate, Action focus)
        {
            var target = Image(parent, label, rect, new Color(0, 0, 0, .01f));
            var button = AddButton(target, activate, focus);
            var highlight = Node(target.transform, "焦点", new Rect(0, 0, rect.width, rect.height));
            var light = ArtImage(highlight, "焦点光带", new Rect(0, 0, rect.width, rect.height), Color.white, "ButtonLight");
            if (light.texture == null)
            {
                light.enabled = true;
                light.color = new Color(1, 1, 1, .13f);
            }
            ArtImage(highlight, "左饰条", new Rect(-4, -3, 23, rect.height + 6), Color.white, "ButtonEdge")
                .uvRect = new Rect(1, 0, -1, 1);
            ArtImage(highlight, "右饰条", new Rect(rect.width - 19, -3, 23, rect.height + 6), Color.white, "ButtonEdge");
            var caption = Label(target.transform, "文字", new Rect(28, 0, rect.width - 56, rect.height),
                label, 34, Font, TextColor);
            Shadow(caption);
            return button;
        }

        private static Button AddButton(RawImage target, Action activate, Action focus)
        {
            target.raycastTarget = true;
            var button = target.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target;
            button.onClick.AddListener(() => activate());
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => focus());
            target.gameObject.AddComponent<EventTrigger>().triggers.Add(enter);
            return button;
        }

        private static void SetFocus(Button[] buttons, int index)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.transform.Find("焦点").gameObject.SetActive(i == index && button.interactable);
                var label = button.transform.Find("文字").GetComponent<Text>();
                label.color = button.interactable ? TextColor : new Color(1, 1, 1, .32f);
            }
        }
    }
}
