/*
 * 脚本：GameMenuRoot
 * 功能：管理七页表现、导航和可见状态，提供声音设置入口。
 * 边界：输入捕获属于 MenuInputAdapter。页面读取样例数据，不改写角色或世界。
 */
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace Ossastra.UI.GameMenu
{
    [DisallowMultipleComponent]
    public sealed class GameMenuRoot : MonoBehaviour
    {
        private static readonly string[] Titles = { "记录", "技能", "装备", "遗物", "收集", "信息", "地图" };
        private static readonly string[] Directions =
        {
            "", "方向键  选择", "1 / 3  分类    方向键  选择", "方向键  选择",
            "滚轮  浏览    方向键  选择", "1 / 3  分类    方向键  选择",
            "Tab  归位    1 / 3  区域    Shift  休息点    滚轮  缩放    Enter  标记"
        };
        private static readonly string[] PadDirections =
        {
            "", "十字键  选择", "LT / RT  分类    十字键  选择", "十字键  选择",
            "十字键  浏览", "LT / RT  分类    十字键  选择",
            "Start  归位    LT / RT  区域    Y  休息点    A  标记"
        };

        [SerializeField] private GameMenuTheme theme;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform composition;
        [SerializeField] private RectTransform[] pageRoots = Array.Empty<RectTransform>();
        [SerializeField] private bool rememberLastPage = true;
        [SerializeField, Min(0f)] private float openingSeconds = .12f;

        private readonly GameMenuPageNavigation[] pages = new GameMenuPageNavigation[7];
        private readonly Text[] tabLabels = new Text[7];
        private readonly Image[] nodes = new Image[7];
        private readonly Image[] tabLights = new Image[7];
        private Text previousHint, nextHint, footerHint, closeHint;
        private GameObject previousFocus;
        private bool built, externalOverlay = true, gamepad;
        private static GameMenuRoot displayedMenu;

        // 调试面板的 OnGUI 在 Canvas 之后绘制，菜单期间只让出可视和指针范围。
        internal static bool IsDisplaying => displayedMenu != null && displayedMenu.IsOpen;

        public event Action AudioRequested;
        public event Action Closed;
        public bool IsOpen { get; private set; }
        public bool OwnsGameplayInput => IsOpen;
        public int CurrentPage { get; private set; }

        private void Awake()
        {
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        public bool Open()
        {
            if (!isActiveAndEnabled || IsOpen || !EnsureBuilt()) return false;
            if (!rememberLastPage) CurrentPage = 0;
            previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            IsOpen = true;
            displayedMenu = this;
            externalOverlay = true;
            overlay.alpha = openingSeconds > 0f ? 0f : 1f;
            SetOverlayVisible(true);
            ShowPage();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (displayedMenu == this) displayedMenu = null;
            foreach (var page in pages)
                if (page != null) page.ResetSession();
            if (overlay != null) overlay.gameObject.SetActive(false);
            if (EventSystem.current != null && previousFocus != null && previousFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousFocus);
            previousFocus = null;
            Closed?.Invoke();
        }

        public bool Cancel()
        {
            if (!IsOpen) return false;
            if (!externalOverlay) return false;
            if (pages[CurrentPage] != null && pages[CurrentPage].Cancel()) return true;
            Close();
            return true;
        }

        public void SetOverlayVisible(bool visible)
        {
            externalOverlay = visible;
            if (overlay == null) return;
            overlay.gameObject.SetActive(IsOpen && visible);
            if (IsOpen && visible && built) pages[CurrentPage]?.RefreshFocus();
        }

        public void MovePage(int step)
        {
            if (!IsOpen || !externalOverlay) return;
            SelectPage((CurrentPage + step % Titles.Length + Titles.Length) % Titles.Length);
        }

        public void Navigate(Vector2Int direction)
        {
            if (IsOpen && externalOverlay) pages[CurrentPage]?.Step(direction);
        }

        public void Submit()
        {
            if (IsOpen && externalOverlay) pages[CurrentPage]?.Submit();
        }

        public void ChangeContext(int step)
        {
            if (!IsOpen || !externalOverlay) return;
            if (step < 0) pages[CurrentPage]?.PreviousContext();
            else if (step > 0) pages[CurrentPage]?.NextContext();
        }

        public void Auxiliary(string command)
        {
            if (IsOpen && externalOverlay) pages[CurrentPage]?.Auxiliary(command);
        }

        public void SetDevice(bool useGamepad)
        {
            if (gamepad == useGamepad) return;
            gamepad = useGamepad;
            if (built) UpdateHints();
        }

        private void SelectPage(int index)
        {
            if (!IsOpen || !externalOverlay || index == CurrentPage) return;
            CurrentPage = index;
            ShowPage();
        }

        private void ShowPage()
        {
            for (int i = 0; i < Titles.Length; i++)
            {
                bool current = i == CurrentPage;
                pageRoots[i].gameObject.SetActive(current);
                tabLabels[i].color = current ? theme.Text : theme.Muted;
                nodes[i].sprite = theme.Get(current ? "tabs.diamond" : "tabs.inactive") ?? theme.Get("tabs.diamond");
                nodes[i].color = current ? theme.Accent : theme.Muted;
                tabLights[i].gameObject.SetActive(current);
            }
            UpdateHints();
            pages[CurrentPage]?.RefreshFocus();
        }

        private void UpdateHints()
        {
            SetKeySprite(previousHint, gamepad ? "key.lb" : "key.q", gamepad ? "LB" : "Q");
            SetKeySprite(nextHint, gamepad ? "key.rb" : "key.e", gamepad ? "RB" : "E");
            closeHint.text = gamepad ? "B   返回" : "Esc   返回";
            footerHint.text = gamepad ? PadDirections[CurrentPage] : Directions[CurrentPage];
        }

        private void SetKeySprite(Text label, string key, string fallback)
        {
            var image = label.transform.parent.GetComponent<Image>();
            image.sprite = theme.Get(key);
            image.preserveAspect = true;
            label.text = image.sprite == null ? fallback : "";
        }

        private void Update()
        {
            if (!IsOpen || !externalOverlay || overlay == null) return;
            overlay.alpha = openingSeconds <= 0f ? 1f :
                Mathf.MoveTowards(overlay.alpha, 1f, Time.unscaledDeltaTime / openingSeconds);
            Image light = tabLights[CurrentPage];
            if (light != null)
            {
                var c = light.color;
                c.a = .75f + .15f * Mathf.Sin(Time.unscaledTime * 2.3f);
                light.color = c;
            }
        }

        private void OnDisable() => Close();

        private bool EnsureBuilt()
        {
            if (built) return true;
            if (theme == null || overlay == null || composition == null || pageRoots.Length != Titles.Length)
                return false;
            foreach (var root in pageRoots)
                if (root == null) return false;
            BuildShell();
            for (int i = 0; i < Titles.Length; i++)
            {
                GameMenuPages.Build(i, pageRoots[i], theme);
                pages[i] = pageRoots[i].GetComponent<GameMenuPageNavigation>();
            }
            built = true;
            return true;
        }

        private void BuildShell()
        {
            var bg = NewRect("Backdrop layers", composition, 0, 0, 1920, 1080);
            bg.SetAsFirstSibling();
            Sprite art = theme.Get("background.base");
            Sprite lines = theme.Get("background.lines");
            Sprite vignette = theme.Get("background.vignette");
            Draw(bg, "Matte", 0, 0, 1920, 1080, null, theme.Background);
            if (art != null) Draw(bg, "Base", 160, -260, 1600, 1600, art, new Color(.55f,.57f,.62f,1f));
            if (lines != null) Draw(bg, "Insignia and lines", 160, -260, 1600, 1600, lines, new Color(.19f,.42f,.49f,.11f));
            Sprite fog = theme.Get("background.fog");
            if (fog != null) Draw(bg, "Mist", 0, 422, 1920, 658, fog, new Color(.29f,.47f,.52f,.28f));
            if (vignette != null) Draw(bg, "Vignette", 0, 0, 1920, 1080, vignette, new Color(0f,0f,0f,.36f));

            var top = NewRect("Top tabs", composition, 0, 0, 1920, 118);
            Sprite line = theme.Get("tabs.line");
            if (line == null)
            {
                Draw(top, "Upper rule", 54, 89, 1812, 1, null, theme.Muted);
                Draw(top, "Lower rule", 54, 105, 1812, 1, null, theme.Muted);
            }
            else Draw(top, "Top rail", 43, 56, 1834, 55, line, new Color(1,1,1,.8f)).type = Image.Type.Sliced;

            for (int i = 0; i < Titles.Length; i++)
            {
                int index = i;
                float x = 296 + i * 221;
                var tab = NewRect(Titles[i], top, x - 80, 22, 160, 91);
                var hit = tab.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => SelectPage(index));

                Sprite glow = theme.Get("tabs.glow");
                tabLights[i] = Draw(tab, "Selected haze", 20, -2, 120, 86, glow, glow != null ? new Color(.42f,.88f,1f,1f) : new Color(.12f,.53f,.64f,.21f));
                tabLabels[i] = Label(tab, "Title", 0, 9, 160, 55, Titles[i], 30, theme.Text);
                Sprite dot = theme.Get("tabs.diamond");
                nodes[i] = Draw(tab, "Diamond", 71, 69, 18, 18, dot, theme.Accent);
                if (dot == null) nodes[i].rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            }

            previousHint = Key(top, "Previous tab", 124, 41, -1);
            nextHint = Key(top, "Next tab", 1749, 41, 1);
            var footer = NewRect("Footer", composition, 0, 1018, 1920, 62);
            ClickableLabel(footer, "Sound settings", 44, 8, 146, 38, "声音设置", () => AudioRequested?.Invoke(), theme.Muted);
            closeHint = ClickableLabel(footer, "Close", 1715, 8, 153, 38, "", () => Cancel(), theme.Text);
            footerHint = Label(footer, "Context shortcuts", 210, 6, 1494, 40, "", 23, theme.Text);
            footerHint.alignment = TextAnchor.MiddleRight;
        }

        private Text Key(RectTransform parent, string name, float x, float y, int step)
        {
            var result = ClickableLabel(parent, name, x, y, 46, 36, "", () => MovePage(step), new Color32(29,52,61,255));
            result.transform.parent.GetComponent<Image>().color = Color.white;
            Sprite key = theme.Get("keycap");
            if (key != null) result.transform.parent.GetComponent<Image>().sprite = key;
            return result;
        }

        private Text ClickableLabel(RectTransform parent, string name, float x, float y, float width,
            float height, string label, Action action, Color color)
        {
            var root = NewRect(name, parent, x, y, width, height);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => action());
            return Label(root, "Label", 0, 0, width, height, label, 24, color);
        }

        private Text Label(RectTransform parent, string name, float x, float y, float width, float height,
            string label, int size, Color color)
        {
            var t = NewRect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            t.font = theme.Font;
            t.supportRichText = false;
            t.text = label;
            t.color = color;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        private Image Asset(RectTransform parent, string name, float x, float y, float width, float height, string key, Color color)
        {
            Sprite sprite = theme.Get(key);
            return sprite != null ? Draw(parent, name, x, y, width, height, sprite, color) : null;
        }

        private static Image Draw(RectTransform parent, string name, float x, float y, float width, float height,
            Sprite sprite, Color color)
        {
            var image = NewRect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewRect(string name, RectTransform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
