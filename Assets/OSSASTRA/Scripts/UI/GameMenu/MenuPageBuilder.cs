// 提供菜单页复用的 uGUI 元素构造方法。
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ossastra.UI.GameMenu
{
    internal sealed class MenuPageBuilder
    {
        internal static readonly Color TextColor = new Color32(223, 231, 228, 255);
        internal static readonly Color Muted = new Color32(129, 174, 180, 255);
        internal static readonly Color Cyan = new Color32(111, 207, 228, 255);
        internal static readonly Color Silver = new Color32(157, 170, 173, 192);
        internal static readonly Color Line = new Color32(104, 162, 174, 120);
        internal static readonly Color Row = new Color32(8, 11, 13, 170);
        internal static readonly Color Band = new Color32(50, 77, 81, 178);
        internal static readonly Color SlotFace = new Color32(20, 23, 25, 246);

        internal readonly RectTransform Body;
        internal readonly GameMenuPageNavigation Nav;
        internal readonly GameMenuTheme Theme;
        internal MenuPageBuilder(RectTransform body, GameMenuTheme theme)
        {
            Body = body;
            Theme = theme;
            Nav = body.gameObject.AddComponent<GameMenuPageNavigation>();
        }

        internal static RectTransform Box(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static UnityEngine.UI.Image Image(RectTransform rect, Color color, bool raycast = false)
        {
            var img = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        internal static UnityEngine.UI.Image Fill(RectTransform parent, string name, float x, float y, float w, float h, Color color)
            => Image(Box(parent, name, x, y, w, h), color);

        internal UnityEngine.UI.Image Art(RectTransform parent, string name, string key, float x, float y, float w, float h,
            Color? tint = null, bool aspect = true)
        {
            var rect = Box(parent, name, x, y, w, h);
            var image = Image(rect, tint ?? Color.white);
            image.sprite = Theme.Get(key);
            image.enabled = image.sprite != null;
            image.preserveAspect = aspect;
            return image;
        }

        internal UnityEngine.UI.Text Label(RectTransform parent, string name, string value, float x, float y, float w, float h,
            int size, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rect = Box(parent, name, x, y, w, h);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Theme.Font;
            text.fontSize = size;
            text.color = color ?? TextColor;
            text.alignment = anchor;
            text.text = value;
            text.lineSpacing = 1.15f;
            text.raycastTarget = false;
            return text;
        }

        internal void BandLabel(RectTransform parent, string title, float x, float y, float w, bool framed = false)
        {
            var band = Box(parent, "Section-" + title, x, y, w, 43);
            var asset = Art(band, "Header skin", "section.band", 0, 0, w, 43, aspect: false);
            if (!asset.enabled)
            {
                Fill(band, "Header fill", 0, 0, w, 43, Band);
                Fill(band, "Accent", 0, 0, 2, 43, Line);
                if (framed) Fill(band, "Bottom rule", 0, 41, w, 1.5f, Line);
                Diamond(band, "Header marker", 13, 21, 4, Silver);
            }
            Label(band, "Header caption", title, 27, 0, w - 36, 43, 26, Muted);
        }

        internal void Rule(RectTransform parent, float x, float y, float w, Color? color = null, bool endCaps = true)
        {
            Fill(parent, "Rule", x, y, w, 1.5f, color ?? Silver);
            if (!endCaps) return;
            Diamond(parent, "Rule start", x, y + 1, 3.9f, color ?? Silver);
            Diamond(parent, "Rule end", x + w, y + 1, 3.9f, color ?? Silver);
        }

        internal static void Diamond(RectTransform parent, string name, float x, float y, float radius, Color color)
        {
            Shape(parent, name, x - radius, y - radius, radius * 2f, radius * 2f, 4, 0, color, 0f);
        }

        internal static void Shape(RectTransform parent, string name, float x, float y, float w, float h,
            int sides, float stroke, Color color, float angle = 0f)
        {
            var shape = Box(parent, name, x, y, w, h).gameObject.AddComponent<MenuPagePolygon>();
            shape.Sides = sides;
            shape.Stroke = stroke;
            shape.Angle = angle;
            shape.color = color;
            shape.raycastTarget = false;
        }

        internal void Keycap(RectTransform parent, string label, float x, float y, float width = 36, int fontSize = 21)
        {
            var cap = Box(parent, "Keycap-" + label, x, y, width, 31);
            var skin = Art(cap, "Keycap backing", "keycap", 0, 0, width, 31, aspect: false);
            if (!skin.enabled) Fill(cap, "Keycap fill", 0, 0, width, 31, new Color32(223, 230, 230, 255));
            Label(cap, "Key label", label, 0, 0, width, 31, fontSize, new Color32(18, 31, 36, 255), TextAnchor.MiddleCenter);
        }

        private GameObject Focus(RectTransform parent, float width, float height, string key)
        {
            var level = Box(parent, "Focus brackets", 0, 0, width, height);
            if (width <= height * 1.35f)
            {
                if (Art(level, "Focus skin", key, 0, 0, width, height, aspect: false).enabled)
                    return level.gameObject;
            }
            // 宽列表使用独立端部，避免把方形焦点图拉伸后压住整行文字。
            var core = new Color32(236, 249, 252, 243);
            var glow = new Color32(180, 230, 239, 73);
            Fill(level, "Selected wash", 4, 3, width - 8, height - 6, new Color32(168, 224, 235, 35));
            Fill(level, "Left glow", -4, -3, 8, height + 6, glow);
            Fill(level, "Right glow", width - 4, -3, 8, height + 6, glow);
            Fill(level, "Left stem", -1, 0, 2.7f, height, core);
            Fill(level, "Left upper tip", -1, 0, 12, 2.7f, core);
            Fill(level, "Left lower tip", -1, height - 2.7f, 12, 2.7f, core);
            Fill(level, "Right stem", width - 1.7f, 0, 2.7f, height, core);
            Fill(level, "Right upper tip", width - 11, 0, 12, 2.7f, core);
            Fill(level, "Right lower tip", width - 11, height - 2.7f, 12, 2.7f, core);
            Diamond(level, "Focus center left", 0, height / 2, 4, core);
            Diamond(level, "Focus center right", width, height / 2, 4, core);
            return level.gameObject;
        }

        internal Action Button(RectTransform rect, Action select, Action confirm = null,
            UnityEngine.UI.ScrollRect scroll = null, string focusKey = "focus.bracket")
        {
            if (!rect.TryGetComponent<UnityEngine.UI.Image>(out var backing))
                backing = Image(rect, new Color32(255, 255, 255, 0), true);
            backing.raycastTarget = true;
            var focus = Focus(rect, rect.rect.width, rect.rect.height, focusKey);
            var focusThis = Nav.Add(rect, focus, select, confirm, scroll);
            rect.gameObject.AddComponent<MenuPagePointer>().Clicked = focusThis;
            return focusThis;
        }

        internal void Click(RectTransform rect, Action action)
        {
            if (!rect.TryGetComponent<UnityEngine.UI.Image>(out var backing))
                backing = Image(rect, new Color32(255, 255, 255, 0), true);
            backing.raycastTarget = true;
            rect.gameObject.AddComponent<MenuPagePointer>().Clicked = action;
        }

        internal RectTransform Slot(RectTransform parent, string id, DemoMenuItem item, string type,
            float x, float y, float size, Action selected = null, string badge = null,
            UnityEngine.UI.ScrollRect scroll = null, bool bare = false)
        {
            var slot = Box(parent, id, x, y, size, size);
            if (!bare)
            {
                var backing = Art(slot, "Slot frame", "slot." + type, 0, 0, size, size);
                if (!backing.enabled)
                {
                    var facets = type == "equipment" ? 32 : 4;
                    var margin = type == "equipment" ? 7f : 3f;
                    Shape(slot, "Outer frame", 2, 2, size - 4, size - 4, 4, 1.8f, Silver);
                    var face = type == "relic" ? 4 : facets;
                    var angle = type == "relic" ? 45f : 0f;
                    Shape(slot, "Slot face", margin, margin, size - margin * 2, size - margin * 2, face, 0, SlotFace, angle);
                    Shape(slot, "Slot edging", margin, margin, size - margin * 2, size - margin * 2, face, 1.6f, Silver, angle);
                }
            }
            if (item != null)
            {
                var icon = Art(slot, "Item " + item.Id, "icon." + item.Id, size * .22f, size * .22f, size * .56f, size * .56f);
                if (!icon.enabled)
                    Label(slot, "Symbol", item.Symbol, size * .15f, size * .14f, size * .7f, size * .72f,
                        Mathf.Max(19, (int)(size * .34f)), item.Color, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(badge))
            {
                Diamond(slot, "Slot marker", size - 6, 11, 12, new Color32(35, 46, 48, 255));
                Shape(slot, "Marker frame", size - 18, -1, 24, 24, 4, 1.2f, Silver);
                Label(slot, "Slot marker text", badge, size - 20, -2, 28, 25, 18, TextColor, TextAnchor.MiddleCenter);
            }
            if (selected != null) Button(slot, selected, scroll: scroll,
                focusKey: type == "skill" ? "focus.frame" : "focus.bracket");
            return slot;
        }

        internal ScrollArea Scroll(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var root = Box(parent, name, x, y, w, h);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 44;
            Image(root, new Color32(255, 255, 255, 0), true);

            var viewport = Box(root, "Viewport", 0, 0, w - 19, h);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content = Box(viewport, "Content", 0, 0, w - 19, 0);
            var stack = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            stack.spacing = 8;
            stack.padding = new RectOffset(8, 8, 4, 4);
            var fit = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var track = Box(root, "Scrollbar", w - 11, 0, 8, h);
            Image(track, new Color32(88, 111, 117, 92), true);
            var thumbArea = Box(track, "Thumb span", 0, 0, 8, h);
            Stretch(thumbArea);
            var thumb = Box(thumbArea, "Thumb", 0, 0, 8, 20);
            var handle = Image(thumb, Silver, true);
            var bar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.targetGraphic = handle;
            bar.handleRect = thumb;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return new ScrollArea(scroll, content, w - 35);
        }
    }

    internal readonly struct ScrollArea
    {
        internal readonly UnityEngine.UI.ScrollRect Scroll;
        internal readonly RectTransform Content;
        internal readonly float Width;
        internal ScrollArea(UnityEngine.UI.ScrollRect scroll, RectTransform content, float width)
        {
            Scroll = scroll;
            Content = content;
            Width = width;
        }

        internal RectTransform Row(string name, float height)
        {
            var row = MenuPageBuilder.Box(Content, name, 0, 0, Width, height);
            var size = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.preferredHeight = height;
            return row;
        }
    }

    internal sealed class MenuPagePointer : MonoBehaviour, IPointerClickHandler
    {
        internal Action Clicked { get; set; }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Clicked?.Invoke();
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MenuPagePolygon : UnityEngine.UI.Graphic
    {
        internal int Sides = 4;
        internal float Stroke;
        internal float Angle;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = rect.size * .5f;
            var step = 360f / Sides;
            for (int index = 0; index < Sides; index++)
            {
                var a = Unit(Angle + index * step);
                var b = Unit(Angle + (index + 1) * step);
                var from = rect.center + Vector2.Scale(radius, a);
                var to = rect.center + Vector2.Scale(radius, b);
                var offset = mesh.currentVertCount;
                mesh.AddVert(from, color, Vector2.zero);
                mesh.AddVert(to, color, Vector2.zero);
                if (Stroke <= 0)
                {
                    mesh.AddVert(rect.center, color, Vector2.zero);
                    mesh.AddTriangle(offset, offset + 2, offset + 1);
                    continue;
                }
                var inner = radius - Vector2.one * Stroke;
                mesh.AddVert(rect.center + Vector2.Scale(inner, b), color, Vector2.zero);
                mesh.AddVert(rect.center + Vector2.Scale(inner, a), color, Vector2.zero);
                mesh.AddTriangle(offset, offset + 2, offset + 1);
                mesh.AddTriangle(offset, offset + 3, offset + 2);
            }
        }

        private static Vector2 Unit(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
