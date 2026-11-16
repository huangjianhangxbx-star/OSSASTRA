/*
 * 脚本：GameMenuTheme
 * 功能：为横版菜单提供可替换的画面部件、字体和配色。
 * 边界：只保存表现资源，由菜单壳和页面读取，不处理菜单输入或角色业务。
 */
using System;
using UnityEngine;

namespace Ossastra.UI.GameMenu
{
    [CreateAssetMenu(menuName = "OSSASTRA/UI/Game Menu Theme")]
    public sealed class GameMenuTheme : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] private Font font;
        [SerializeField] private UnityEngine.Video.VideoClip axePreview;
        public UnityEngine.Video.VideoClip AxePreview => axePreview;
        [SerializeField] private Entry[] sprites = Array.Empty<Entry>();
        [SerializeField] private Color text = new Color32(220, 229, 230, 255);
        [SerializeField] private Color muted = new Color32(121, 145, 151, 255);
        [SerializeField] private Color accent = new Color32(120, 221, 231, 255);
        [SerializeField] private Color background = new Color32(12, 18, 24, 255);
        private Font fallback;

        public Font Font
        {
            get
            {
                if (font != null) return font;
                if (fallback == null)
                    fallback = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei Light", "Microsoft YaHei", "Arial" }, 30);
                return fallback;
            }
        }

        public Color Text => text;
        public Color Muted => muted;
        public Color Accent => accent;
        public Color Background => background;

        public Sprite Get(string key)
        {
            foreach (var entry in sprites)
                if (entry.key == key) return entry.sprite;
            return null;
        }

        private void OnDisable()
        {
            if (fallback != null) Destroy(fallback);
        }
    }
}
