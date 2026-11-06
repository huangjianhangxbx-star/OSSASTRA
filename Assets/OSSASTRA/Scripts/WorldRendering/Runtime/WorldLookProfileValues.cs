/* 功能：定义原生世界Look的纯配置值与作者档适配端口；运行层不依赖研究Profile类型。
 * 边界：值由适配器捕获后立即复制为不可变Settings，不持GPU/玩家/开发面板。
 */
using UnityEngine;
namespace Ossastra.WorldRendering {
 public interface IWorldLookProfileAdapter {WorldLookProfileValues Capture(ScriptableObject presentation,ScriptableObject atmosphere,ScriptableObject palette);}
 public sealed class WorldLookProfileValues {public WorldLookPresentationValues Presentation;public WorldLookAtmosphereValues Atmosphere;public WorldLookPaletteValues Palette;}
 public sealed class WorldLookPresentationValues { public float bloomIntensity; public float bloomScatter; public float bloomThreshold; public float blurRadius; public bool presentationEnabled; public float sharpBand; public float transition; public float vignette; }
 public sealed class WorldLookAtmosphereValues { public bool airEnabled; public float contrast; public float contrastLoss; public float exposure; public float extinction; public Color farColor; public float farDistance; public Color midColor; public float midDistance; public Color nearColor; public float nearColorInfluence; public float nearDistance; public float saturation; public float saturationLoss; public float temperature; }
 public sealed class WorldLookPaletteValues { public Color airFar; public Color airMid; public Color airNear; public float contrastOffset; public float exposureOffset; public Color gradeFilter; public float saturationOffset; public float temperatureOffset; }
}
