// 职责：定义物质实验室的美术预设。运行时以只读方式采样，不保存或复制物理参数。
using System;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    public enum MatterPaintStyle { Ruins, Moss }

    [Flags]
    public enum MatterPaintLayers
    {
        Background = 1, Body = 2, Trim = 4, Decoration = 8, Foreground = 16,
        All = Background | Body | Trim | Decoration | Foreground
    }

    [CreateAssetMenu(menuName = "OSSASTRA/实验室/手绘表现配置", fileName = "MatterPaintProfile")]
    public sealed class MatterPaintProfile : ScriptableObject
    {
        public MatterPaintStyle Style;
        public string Title;
        [Header("布景")]
        public Texture2D Background, Structure, Foreground;
        public Color BackgroundTint = Color.white;
        [Range(0, 1)] public float BackgroundParallax = .28f;
        [Range(0, 1)] public float ForegroundParallax = .13f;
        [Header("世界纹理与配色")]
        public Texture2D Stone, Water, WaterEdge, Oil, Plant, Wood, Flame, Smoke, Sand;
        public Color WaterColor, WaterDeep, OilColor, StoneColor, EdgeColor, PlantColor, WoodColor, SmokeColor, FlameColor;
        [Range(.1f, 1)] public float WaterAlpha = .81f;
        [Range(.04f, .9f)] public float SurfaceWidth = .27f;
        [Range(0, .6f)] public float WaveAmount = .18f;
        [Range(.3f, 7f)] public float TextureScale = 1.25f;
        [Range(0, 2)] public float FireGain = 1f;
        [Range(0, 2)] public float SmokeGain = 1f;

        public static MatterPaintProfile CreateSample(MatterPaintStyle style)
        {
            var result = CreateInstance<MatterPaintProfile>();
            result.Style = style;
            bool moss = style == MatterPaintStyle.Moss;
            result.Title = moss ? "苔光幽洞" : "雾蓝水渠";
            result.Background = Resources.Load<Texture2D>("OSPhysics/Art/" + (moss ? "MossBackdrop" : "RuinsBackdrop"));
            result.BackgroundTint = moss ? RGB(209, 229, 181) : RGB(197, 214, 235);
            result.StoneColor = moss ? RGB(47, 59, 43) : RGB(38, 47, 64);
            result.EdgeColor = moss ? RGB(136, 154, 90) : RGB(107, 136, 162);
            result.WaterColor = moss ? RGB(95, 221, 164) : RGB(98, 209, 233);
            result.WaterDeep = moss ? RGB(22, 86, 64) : RGB(21, 72, 111);
            result.OilColor = moss ? RGB(199, 133, 63) : RGB(217, 142, 64);
            result.PlantColor = moss ? RGB(126, 161, 69) : RGB(81, 144, 112);
            result.WoodColor = moss ? RGB(133, 89, 58) : RGB(136, 93, 65);
            result.SmokeColor = moss ? RGB(159, 171, 140) : RGB(149, 147, 162);
            result.FlameColor = moss ? RGB(255, 148, 58) : RGB(255, 141, 61);
            return result;
        }

        private static Color RGB(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
