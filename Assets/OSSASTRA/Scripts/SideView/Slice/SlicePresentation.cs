/*
 * Presentation shared by the title, rest menu and contextual prompt.
 * All textures are optional local research assets. Gameplay stays independent of this skin.
 */
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ossastra.SideView.Slice
{
    internal static class SlicePresentation
    {
        private const string ArtPrefix = "SlicePresentation/";
        internal static readonly Vector2 ReferenceSize = new Vector2(1920, 1080);
        internal static readonly Color TextColor = new Color32(229, 227, 229, 255);
        internal static readonly Color AccentColor = new Color32(116, 187, 196, 255);
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        internal static Texture2D Art(string name)
        {
            if (!Application.isPlaying) return Resources.Load<Texture2D>(ArtPrefix + name);
            if (!Textures.TryGetValue(name, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>(ArtPrefix + name);
                if (texture != null) Textures[name] = texture;
            }
            return texture;
        }

        internal static RectTransform Node(Transform parent, string name, Rect rect)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = node.pivot = new Vector2(0, 1);
            node.anchoredPosition = new Vector2(rect.x, -rect.y);
            node.sizeDelta = rect.size;
            return node;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static RectTransform Canvas(Transform parent, string name, int order, bool raycasts)
        {
            var root = Node(parent, name, default);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scale = root.gameObject.AddComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = ReferenceSize;
            scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (raycasts) root.gameObject.AddComponent<GraphicRaycaster>();
            return root;
        }

        internal static RawImage Image(Transform parent, string name, Rect rect, Color color, string asset = null)
        {
            var image = Node(parent, name, rect).gameObject.AddComponent<RawImage>();
            if (!string.IsNullOrEmpty(asset)) image.texture = Art(asset);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static RawImage ArtImage(Transform parent, string name, Rect rect, Color color, string asset)
        {
            var image = Image(parent, name, rect, color, asset);
            image.enabled = image.texture != null;
            return image;
        }

        internal static Text Label(Transform parent, string name, Rect rect, string value, int size,
            Font font, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = Node(parent, name, rect).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        internal static void Rule(Transform parent, string name, Rect rect, Color color)
        {
            Image(parent, name, rect, color);
            foreach (float x in new[] { rect.x, rect.xMax })
            {
                var point = Image(parent, "端点", new Rect(x - 4, rect.y - 3, 8, 8), color);
                point.rectTransform.pivot = new Vector2(.5f, .5f);
                point.rectTransform.anchoredPosition += new Vector2(4, -4);
                point.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        internal static void Shadow(Text text)
        {
            var edge = text.gameObject.AddComponent<Shadow>();
            edge.effectColor = new Color(0, 0, 0, .82f);
            edge.effectDistance = new Vector2(1, -2);
        }
    }
}
