// 构建七个原生 uGUI 菜单页及其局部交互。
using System;
using UnityEngine;
using static Ossastra.UI.GameMenu.GameMenuDemoData;
using static Ossastra.UI.GameMenu.MenuPageBuilder;

namespace Ossastra.UI.GameMenu
{
    public static class GameMenuPages
    {
        public static void Build(int page, RectTransform parent, GameMenuTheme theme)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var view = new MenuPageBuilder(parent, theme);
            switch (page)
            {
                case 0: Record(view); break;
                case 1: SkillPage(view); break;
                case 2: EquipmentPage(view); break;
                case 3: RelicPage(view); break;
                case 4: CollectionPage(view); break;
                case 5: InformationPage(view); break;
                case 6: new GameMenuMap(view).Build(); break;
                default: throw new ArgumentOutOfRangeException(nameof(page));
            }
            view.Nav.RefreshFocus();
        }

        private static void Record(MenuPageBuilder v)
        {
            var body = v.Body;
            v.Label(body, "Chapter label", "章节", 47, 171, 91, 51, 24);
            Shape(body, "Chapter lozenge", 111, 148, 98, 98, 4, 2f, Silver);
            Diamond(body, "Chapter inner", 160, 197, 43, new Color32(168, 196, 196, 27));
            v.Label(body, "Chapter number", "2", 111, 153, 98, 89, 44, TextColor, TextAnchor.MiddleCenter);
            v.Label(body, "Play time", "◷  01:55:08", 236, 172, 228, 51, 30);
            v.BandLabel(body, "状态", 47, 257, 480);
            v.Label(body, "Experience label", "Lv 9   经验值", 47, 325, 164, 38, 27, Muted);
            v.Label(body, "Experience sample", "210 / 398", 207, 325, 198, 38, 27);
            Stats(v, body, 46, 396, 402, 57);
            for (int index = 0; index < CurrencyNames.Length; index++)
            {
                var y = 760 + 76 * index;
                v.Rule(body, 47, y - 2, 411, new Color32(151, 162, 165, 93), false);
                Diamond(body, "Currency-" + CurrencyNames[index], 61, y + 41, 9, index == 1 ? new Color32(186, 158, 99, 255) : Cyan);
                v.Label(body, "Currency label", CurrencyNames[index], 94, y + 17, 190, 46, 26);
                v.Label(body, "Currency value", CurrencyValues[index], 282, y + 17, 166, 46, 28, TextColor, TextAnchor.MiddleRight);
            }

            var hero = v.Art(body, "Record portrait composition", "page.record.hero", 454, 132, 952, 952);
            if (!hero.enabled)
            {
                v.Art(body, "Record back portrait", "page.record.hero.back", 545, 139, 470, 875);
                v.Art(body, "Record front portrait", "page.record.hero.front", 825, 400, 382, 614);
            }

            const float panelX = 1320;
            v.BandLabel(body, "技能", panelX, 165, 509);
            var equipped = new[] { Skills[1], Skills[2], Skills[4], null };
            for (int index = 0; index < equipped.Length; index++)
            {
                var x = panelX + 39 + 123 * index;
                v.Slot(body, "Current skill " + (index + 1), equipped[index], "skill", x, 248, 97);

            }

            v.BandLabel(body, "装备", panelX, 375, 509);
            for (int index = 0; index < 4; index++)
            {
                var x = panelX + 40 + 116 * index;
                v.Slot(body, "Current equipment " + (index + 1), null, "equipment", x, 456, 89);
                v.Label(body, "Equipment role", new[] { "◇", "L", "R", "○" }[index], x + 75, 446, 31, 26, 20,
                    Silver, TextAnchor.MiddleCenter);
            }

            v.BandLabel(body, "遗物", panelX, 584, 509);
            v.Label(body, "Relic capacity", "当前装备值     3 / 4", 1555, 643, 271, 31, 23, Muted, TextAnchor.MiddleRight);
            v.Rule(body, 1574, 677, 253, Line, false);
            for (int index = 0; index < 10; index++)
            {
                var item = index < 2 ? Relics[index] : null;
                v.Slot(body, "Current relic " + (index + 1), item, "relic",
                    panelX + 26 + index % 5 * 102, 699 + index / 5 * 122, 93, badge: item?.Badge);
            }
        }

        private static void SkillPage(MenuPageBuilder v)
        {
            var body = v.Body;
            Locked(v);
            v.Art(body, "Skill portrait", "page.skills.hero", 905, 236, 559, 775, new Color32(255, 255, 255, 185));
            var previewFrame = Box(body, "Skill preview window", 1253, 132, 603, 306);
            Image(previewFrame, new Color32(5, 8, 12, 205));
            var image = v.Art(previewFrame, "Preview artwork", "page.skills.preview", 2, 2, 599, 302, aspect: false);
            GameObject videoObject = null;
            if (v.Theme.AxePreview != null)
            {
                var videoRect = Box(previewFrame, "Axe gameplay video", 33, 2, 537, 302);
                videoObject = videoRect.gameObject;
                videoObject.SetActive(false);
                videoObject.AddComponent<GameMenuSkillPreview>().Configure(v.Theme.AxePreview);
            }
            var placeholder = v.Label(previewFrame, "Preview placeholder", "预览画面待接入", 3, 3, 597, 300, 22,
                Muted, TextAnchor.MiddleCenter);
            Fill(previewFrame, "Frame top", 0, 0, 603, 2, Silver);
            Fill(previewFrame, "Frame bottom", 0, 304, 603, 2, Silver);
            Fill(previewFrame, "Frame left", 0, 0, 2, 306, Silver);
            Fill(previewFrame, "Frame right", 601, 0, 2, 306, Silver);

            var detail = new Detail(v, body, 1253, 458, 603, false);
            var notice = v.Label(body, "Selected proficiency", "✦ ✦ ◇ ◇ ◇", 1652, 461, 200, 38, 25,
                Cyan, TextAnchor.MiddleRight);
            var progress = new RectTransform[4];
            var stats = new[] { "威力", "击倒能力", "速度", "范围" };
            for (int index = 0; index < stats.Length; index++)
            {
                var x = 1255 + index * 151;
                Fill(body, "Skill stat track", x, 558, 139, 8, new Color32(107, 109, 119, 140));
                progress[index] = Fill(body, "Skill stat fill", x, 558, 100, 8, new Color32(113, 156, 221, 255)).rectTransform;
                v.Label(body, "Skill stat", stats[index], x, 565, 141, 35, 20, Silver);
            }

            void Display(int index)
            {
                var item = Skills[index];
                detail.Show(item);
                image.sprite = v.Theme.Get("preview." + item.Id) ?? v.Theme.Get("page.skills.preview");
                image.enabled = image.sprite != null;
                if (videoObject != null) videoObject.SetActive(item.Id == "skill.axe");
                placeholder.gameObject.SetActive(!image.enabled);
                placeholder.text = index == 5 ? "暂无预览" : "预览画面待接入";
                notice.text = index == 5 ? "◇ ◇ ◇ ◇ ◇" : "✦ ✦ ◇ ◇ ◇";
                for (int column = 0; column < progress.Length; column++)
                    progress[column].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        index == 5 ? 0 : 40 + ((index * 29 + column * 19) % 72));
            }

            var quick = new[] { 1, 2, 4, 5 };
            for (int index = 0; index < quick.Length; index++)
            {
                int selected = quick[index];
                float pos = 290 + index * 139;
                v.Slot(body, "Equipped skill " + (index + 1), selected == 5 ? null : Skills[selected],
                    "skill", pos, 200, 106, () => Display(selected));

            }
            v.Rule(body, 168, 343, 920, Line, false);
            var branches = new[] { new[] { 0, 1 }, new[] { 2 }, new[] { 3, 4 } };
            var coordinates = new[] { new Vector2(124, 414), new Vector2(124, 559), new Vector2(574, 414) };
            for (int index = 0; index < branches.Length; index++)
            {
                var at = coordinates[index];
                var branch = Box(body, "Skill branch " + (index + 1), at.x, at.y, 405, 120);
                var portrait = v.Art(branch, "Branch portrait", "page.skills.branch." + (index + 1),
                    0, 0, 101, 105, new Color32(205, 231, 236, 224));
                if (!portrait.enabled)
                {
                    Shape(branch, "Companion outline", 23, 9, 61, 66, 28, 1.4f, Line);
                    Shape(branch, "Companion collar", 7, 49, 92, 63, 5, 1.8f, Line, 90);
                }
                v.Rule(branch, 0, 107, 89, Silver);
                v.Rule(branch, 132, 57, branches[index].Length * 106, Silver);
                for (int n = 0; n < branches[index].Length; n++)
                {
                    var itemIndex = branches[index][n];
                    v.Slot(branch, "Branch ability " + itemIndex, Skills[itemIndex], "skill",
                        106 + n * 142, 13, 83, () => Display(itemIndex));
                }
            }
            Display(1);
        }

        private static void EquipmentPage(MenuPageBuilder v)
        {
            var body = v.Body;
            Locked(v);
            var detail = new Detail(v, body, 1248, 173, 591, true);
            var views = new RectTransform[EquipmentTabs.Length];
            var focus = new Action[EquipmentTabs.Length];
            int current = 0;
            Action<int> indicators = null;

            void Show(int index)
            {
                current = (index + views.Length) % views.Length;
                for (int i = 0; i < views.Length; i++) views[i].gameObject.SetActive(i == current);
                indicators?.Invoke(current);
                detail.Show(null);
                focus[current]?.Invoke();
            }

            for (int index = 0; index < 4; index++)
            {
                int availableTab = Mathf.Clamp(index - 1, 0, EquipmentTabs.Length - 1);
                var mount = v.Slot(body, "Equipment mount " + (index + 1), null, "equipment", 86 + index * 159,
                    251, 107, () => detail.Show(null));
                v.Label(body, "Equipment mark", new[] { "◈", "L", "R", "○" }[index],
                    164 + index * 159, 223, 36, 27, 20, Muted, TextAnchor.MiddleCenter);
                // Confirmation on the mount opens the matching read-only equipment category.

                var click = mount.GetComponent<MenuPagePointer>();
                var select = click.Clicked;
                click.Clicked = () => { select(); Show(availableTab); };
            }

            var attributes = Box(body, "Equipment attributes", 726, 136, 353, 341);
            Image(attributes, new Color32(14, 25, 28, 138));
            v.Rule(attributes, 5, 0, 343, Line, false);
            Stats(v, attributes, 25, 22, 277, 48);
            v.Rule(attributes, 5, 338, 343, Line, false);

            for (int i = 0; i < views.Length; i++)
            {
                var category = i;
                var group = Box(body, "Equipment category " + EquipmentTabs[i], 68, 613, 1009, 322);
                views[i] = group;
                for (int index = 0; index < Equipment[i].Length; index++)
                {
                    var item = Equipment[category][index];
                    var row = Box(group, "Equipment item " + item.Id, 10, 18 + index * 83, 752, 67);
                    Image(row, Row);
                    v.Slot(row, "Equipment example", item, "equipment", 6, 4, 59);
                    v.Label(row, "Equipment name", item.Title, 88, 0, 630, 67, 27);
                    var select = v.Button(row, () => detail.Show(item));
                    if (index == 0) focus[i] = select;
                }
            }

            indicators = Tabs(v, body, EquipmentTabs, 62, 508, 984, Show);
            v.Nav.ChangeContext = delta => { Show(current + delta); return true; };
            Show(0);
        }

        private static void RelicPage(MenuPageBuilder v)
        {
            var body = v.Body;
            Locked(v);
            var detail = new Detail(v, body, 1254, 160, 603, true);
            v.Label(body, "Relic capacity", "当前装备值    3 / 4", 639, 124, 283, 44, 27, Muted, TextAnchor.MiddleRight);
            v.Rule(body, 650, 167, 268, Line, false);
            var equipped = Box(body, "Equipped relics", 226, 190, 680, 284);
            for (int index = 0; index < 10; index++)
            {
                var item = index < 2 ? Relics[index] : Relics[3];
                v.Slot(equipped, "Equipped relic " + (index + 1), index < 2 ? item : null, "relic",
                    10 + index % 5 * 141, 10 + index / 5 * 133, 106, () => detail.Show(item), item.Badge);
            }

            v.Rule(body, 106, 503, 975, Line, false);
            var held = Box(body, "Owned relics", 66, 566, 981, 285);
            for (int index = 0; index < 3; index++)
            {
                var item = Relics[index];
                v.Slot(held, "Owned relic " + (index + 1), item, "relic", 11 + index * 137, 13, 106,
                    () => detail.Show(item), item.Badge);
            }
            detail.Show(Relics[0]);
        }

        private static void CollectionPage(MenuPageBuilder v)
        {
            var body = v.Body;
            var detail = new Detail(v, body, 1254, 154, 603, true);
            var list = v.Scroll(body, "Collection scrolling list", 50, 170, 1012, 791);
            for (int index = 0; index < CollectionGroups.Length; index++)
            {
                var row = list.Row("Collection section " + (index + 1), 174);
                v.BandLabel(row, CollectionGroups[index], 0, 0, list.Width - 7, true);
                for (int entry = 0; entry < Collection[index].Length; entry++)
                {
                    var item = Collection[index][entry];
                    var x = 18 + entry * 137;
                    v.Slot(row, "Collection item " + item.Id, item, "relic", x, 53, 99, () => detail.Show(item),
                        scroll: list.Scroll, bare: true);
                    if (!string.IsNullOrEmpty(item.Badge))
                        v.Label(row, "Inventory sample quantity", item.Badge, x + 63, 116, 45, 38, 26, TextColor,
                            TextAnchor.MiddleRight);
                }
            }
            detail.Show(Collection[0][0]);
        }

        private static void InformationPage(MenuPageBuilder v)
        {
            var body = v.Body;
            var detail = new Detail(v, body, 1254, 140, 603, false, true);
            var roots = new RectTransform[Information.Length];
            var focus = new Action[Information.Length];
            var pageText = v.Label(body, "Information page index", "1 / 2", 456, 962, 201, 43, 26,
                TextColor, TextAnchor.MiddleCenter);
            v.Rule(body, 454, 961, 207, Line);
            v.Rule(body, 454, 1006, 207, Line);
            int current = 0;
            Action<int> indicators = null;

            void Show(int index)
            {
                current = (index + roots.Length) % roots.Length;
                for (int i = 0; i < roots.Length; i++) roots[i].gameObject.SetActive(i == current);
                indicators?.Invoke(current);
                focus[current]?.Invoke();
            }

            for (int i = 0; i < Information.Length; i++)
            {
                var context = i;
                roots[i] = Box(body, "Information category " + InformationTabs[i], 67, 222, 1009, 716);
                var list = v.Scroll(roots[i], "Document titles", 22, 8, 983, 702);
                for (int index = 0; index < Information[i].Length; index++)
                {
                    var entry = index;
                    var title = Information[context][entry];
                    var row = list.Row("Document entry " + (entry + 1), 75);
                    Image(row, Row);
                    v.Label(row, "Document title", title, 37, 0, list.Width - 59, 75, 28);
                    var item = new DemoMenuItem("info." + context + "." + entry, title, InformationTabs[context],
                        "", InformationBody(context, entry));
                    var set = v.Button(row, () =>
                    {
                        detail.Show(item);
                        var pages = Mathf.CeilToInt(Information[context].Length / 8f);
                        pageText.text = (entry / 8 + 1) + " / " + pages;
                    }, scroll: list.Scroll);
                    if (index == 0) focus[i] = set;
                }
            }
            indicators = Tabs(v, body, InformationTabs, 72, 130, 955, Show);
            v.Nav.ChangeContext = delta => { Show(current + delta); return true; };
            Show(0);
        }

        private static Action<int> Tabs(MenuPageBuilder v, RectTransform parent, string[] texts,
            float x, float y, float width, Action<int> select)
        {
            var tabs = Box(parent, "Subpage navigation", x, y, width, 58);
            v.Rule(tabs, 0, 55, width);
            v.Keycap(tabs, "1", width * .12f, 1);
            v.Keycap(tabs, "3", width * .83f, 1);
            var markers = new RectTransform[texts.Length];
            var labels = new UnityEngine.UI.Text[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                var index = i;
                var center = width * (.30f + (i + .5f) * .43f / texts.Length);
                var hit = Box(tabs, "Subpage-" + texts[i], center - 70, 0, 140, 43);
                labels[i] = v.Label(hit, "Label", texts[i], 0, 0, 140, 43, 28, Silver, TextAnchor.MiddleCenter);
                v.Click(hit, () => select(index));
                Diamond(tabs, "Tab outline", center, 56, 8, Silver);
                Diamond(tabs, "Tab inactive center", center, 56, 6, SlotFace);
                var glow = Box(tabs, "Active marker " + texts[i], center - 56, 46, 112, 21);
                markers[i] = glow;
                Fill(glow, "Active underline", 3, 9, 106, 2, Line);
                Diamond(glow, "Active diamond", 56, 10, 8, Cyan);
            }
            return index =>
            {
                for (int i = 0; i < markers.Length; i++)
                {
                    markers[i].gameObject.SetActive(i == index);
                    labels[i].color = i == index ? TextColor : Silver;
                }
            };
        }

        private static void Stats(MenuPageBuilder v, RectTransform root, float x, float y, float width, float spacing)
        {
            for (int index = 0; index < AttributeNames.Length; index++)
            {
                var row = Box(root, "Attribute " + AttributeNames[index], x, y + index * spacing, width, spacing);
                Shape(row, "Attribute medallion", 0, 4, 35, 35, 32, 0, Band);
                char[] marks = { '◇', '⚔', '▥', '✧', '◈', '✦' };
                v.Label(row, "Attribute symbol", marks[index].ToString(), 0, 1, 35, 39, 25, Silver, TextAnchor.MiddleCenter);
                v.Label(row, "Attribute name", AttributeNames[index], 43, 0, width - 103, 36, 25, Muted);
                v.Label(row, "Attribute value", AttributeValues[index], width - 84, 0, 84, 36, 27,
                    TextColor, TextAnchor.MiddleRight);
                Fill(row, "Attribute track", 42, 39, width - 98, 4.5f, new Color32(142, 145, 145, 154));
                if (AttributeFill[index] > 0f)
                    Fill(row, "Attribute fill", 42, 39, (width - 98) * AttributeFill[index], 4.5f, TextColor);
            }
        }

        private static void Locked(MenuPageBuilder v)
        {
            var color = new Color32(255, 117, 117, 255);
            var icon = Box(v.Body, "Disabled change glyph", 51, 136, 27, 27);
            Shape(icon, "Border", 0, 0, 27, 27, 4, 1.7f, color, 45);
            var slash = Fill(icon, "Slash", 5, 6, 24, 2, color).rectTransform;
            slash.localEulerAngles = new Vector3(0, 0, -45);
            v.Label(v.Body, "Read-only label", "无法更换", 89, 128, 179, 43, 25, color);
        }

        private sealed class Detail
        {
            private readonly MenuPageBuilder view;
            private readonly UnityEngine.UI.Text kind, title, summary, note, badge, symbol;
            private readonly UnityEngine.UI.Image illustration;

            internal Detail(MenuPageBuilder v, RectTransform parent, float x, float y, float width,
                bool art = true, bool document = false)
            {
                view = v;
                var detail = Box(parent, "Item details", x, y, width, 844);
                float header = art ? 230 : 0;
                if (art)
                {
                    illustration = v.Art(detail, "Detail icon", "empty", (width - 223) / 2f, 2, 223, 212);
                    symbol = v.Label(detail, "Detail symbol", "", (width - 223) / 2f, 0, 223, 212, 64,
                        Silver, TextAnchor.MiddleCenter);
                }
                float category = art || document ? header + 1 : 56;
                Fill(detail, "Category band", 0, category, 118, 31, Band);
                kind = v.Label(detail, "Item category", "", 11, category - 1, 150, 33, 23, Muted);
                title = v.Label(detail, "Item title", "", 0, header + (art || document ? 40 : -6),
                    width - 151, 52, 36);
                v.Rule(detail, 0, header + (art || document ? 98 : 43), width);
                badge = v.Label(detail, "Item usage or quantity", "", width - 189, header - 1, 188, 32, 22,
                    TextColor, TextAnchor.MiddleRight);
                float summaryStart = document ? 117 : art ? header + 119 : 156;
                float summaryHeight = document ? 571 : 97;
                summary = v.Label(detail, "Item description", "", 2, summaryStart, width - 12, summaryHeight,
                    document ? 27 : 27, TextColor, TextAnchor.UpperLeft);
                note = v.Label(detail, "Item flavor text", "", 2, summaryStart + 111, width - 12, 238, 26,
                    Cyan, TextAnchor.UpperLeft);
                note.gameObject.SetActive(!document);
            }

            internal void Show(DemoMenuItem item)
            {
                kind.text = item?.Kind ?? "装备";
                title.text = item?.Title ?? "空装备槽";
                summary.text = item?.Summary ?? "请选择左侧条目查看详情。";
                note.text = item?.Notes ?? "";
                badge.text = string.IsNullOrEmpty(item?.Badge) ? "" : (item.Kind == "遗物" ? "占用装备值    " : "持有数量    ") + item.Badge;
                if (illustration == null) return;
                illustration.sprite = item == null ? null : view.Theme.Get("icon." + item.Id);
                illustration.enabled = illustration.sprite != null;
                symbol.text = illustration.enabled ? "" : item?.Symbol ?? "";
                symbol.color = item?.Color ?? Silver;
            }
        }
    }
}
