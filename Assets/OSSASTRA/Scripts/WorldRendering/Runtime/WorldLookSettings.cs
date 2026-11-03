/* 功能：把既有空气、调色与展示 Profile 转成单相机不可变环境显示快照。
 * 边界：只读源档；不调用旧预览驱动、不改灯光/材质/管线，不把分角色材质色板冒充全屏调色。
 * 协作：控制器给出已获许可观察点；Feature 只消费有限数值与独立效果开关。
 * 生命周期：每次发布创建独立数值快照，后续作者改源档不会篡改已发布帧；无 Unity/GPU 资源所有权。
 */
using UnityEngine;
using XingHai.T7B;
using XingHai.T7C1;
using XingHai.T7C2;

namespace Ossastra.WorldRendering
{
    public sealed class WorldLookSettings
    {
        public bool Enabled { get; private set; }
        public bool AirEnabled { get; private set; }
        public bool FocusEnabled { get; private set; }
        public bool BloomEnabled { get; private set; }
        public bool GradeEnabled { get; private set; }
        public bool VignetteEnabled { get; private set; }
        public float FocusDepth { get; private set; }
        public float SharpBand { get; private set; }
        public float Transition { get; private set; }
        public float BlurRadius { get; private set; }
        public float BloomIntensity { get; private set; }
        public float BloomThreshold { get; private set; }
        public float BloomScatter { get; private set; }
        public float Vignette { get; private set; }
        public float Exposure { get; private set; }
        public float Contrast { get; private set; }
        public float Saturation { get; private set; }
        public float Temperature { get; private set; }
        public Color GradeFilter { get; private set; }
        public Color AirNear { get; private set; }
        public Color AirMid { get; private set; }
        public Color AirFar { get; private set; }
        public float AirNearDistance { get; private set; }
        public float AirMidDistance { get; private set; }
        public float AirFarDistance { get; private set; }
        public float Extinction { get; private set; }
        public float SaturationLoss { get; private set; }
        public float ContrastLoss { get; private set; }
        public float NearInfluence { get; private set; }

        WorldLookSettings() { }

        /// <summary>
        /// 展示档只提供焦平面带宽、Bloom 与暗角；区域档只贡献环境空气色/原调色偏移。
        /// Ground/Stone/Vegetation 等逐材质颜色由真实材质角色负责，本快照不会取其中一个颜色染全屏。
        /// </summary>
        public static WorldLookSettings Build(Camera camera, Vector3 observerPosition,
            HD2DPostProfile presentation, OutdoorAtmosphereProfile atmosphere,
            RegionalLookProfile palette, WorldLookEffectOptions options)
        {
            bool valid = camera != null && Finite(observerPosition) && Finite(camera.transform.position) && Finite(camera.transform.forward);
            bool master = valid && (options == null || options.enabled);
            bool hasPresentation = presentation != null && presentation.presentationEnabled;
            bool hasAtmosphere = atmosphere != null;
            bool hasPalette = palette != null;
            var result = new WorldLookSettings();
            result.AirEnabled = master && hasAtmosphere && atmosphere.airEnabled && (options == null || options.airEnabled);
            result.FocusEnabled = master && hasPresentation && (options == null || options.focusEnabled);
            result.BloomEnabled = master && hasPresentation && (options == null || options.bloomEnabled);
            result.GradeEnabled = master && (hasAtmosphere || hasPalette) && (options == null || options.gradeEnabled);
            result.VignetteEnabled = master && hasPresentation && (options == null || options.vignetteEnabled);
            result.Enabled = result.AirEnabled || result.FocusEnabled || result.BloomEnabled || result.GradeEnabled || result.VignetteEnabled;

            // 焦点使用同一已获许可观察点的前向眼深；不引入旧预览平滑器的滞后或另一角色权威。
            float near = camera != null ? Safe(camera.nearClipPlane, .05f, .001f, 10000f) : .05f;
            float depth = valid ? Vector3.Dot(observerPosition - camera.transform.position, camera.transform.forward) : near + .01f;
            result.FocusDepth = Safe(depth, near + .01f, near + .01f, 100000f);
            result.SharpBand = Safe(hasPresentation ? presentation.sharpBand : 8f, 8f, 0f, 1000f);
            result.Transition = Safe(hasPresentation ? presentation.transition : 18f, 18f, .001f, 1000f);
            result.BlurRadius = Safe(hasPresentation ? presentation.blurRadius : 0f, 0f, 0f, 3f);
            result.BloomIntensity = Safe(hasPresentation ? presentation.bloomIntensity : 0f, 0f, 0f, 5f);
            result.BloomThreshold = Safe(hasPresentation ? presentation.bloomThreshold : 1.3f, 1.3f, 0f, 10f);
            result.BloomScatter = Safe(hasPresentation ? presentation.bloomScatter : .5f, .5f, 0f, 1f);
            result.Vignette = Safe(hasPresentation ? presentation.vignette : 0f, 0f, 0f, 1f);

            result.Exposure = Safe((hasAtmosphere ? atmosphere.exposure : 0f) + (hasPalette ? palette.exposureOffset : 0f), 0f, -10f, 10f);
            result.Contrast = Safe((hasAtmosphere ? atmosphere.contrast : 0f) + (hasPalette ? palette.contrastOffset : 0f), 0f, -100f, 100f);
            result.Saturation = Safe((hasAtmosphere ? atmosphere.saturation : 0f) + (hasPalette ? palette.saturationOffset : 0f), 0f, -100f, 100f);
            result.Temperature = Safe((hasAtmosphere ? atmosphere.temperature : 0f) + (hasPalette ? palette.temperatureOffset : 0f), 0f, -100f, 100f);
            result.GradeFilter = SafeColor(hasPalette ? palette.gradeFilter : Color.white, Color.white);
            result.AirNear = SafeColor(hasPalette ? palette.airNear : hasAtmosphere ? atmosphere.nearColor : Color.white, Color.white);
            result.AirMid = SafeColor(hasPalette ? palette.airMid : hasAtmosphere ? atmosphere.midColor : Color.white, Color.white);
            result.AirFar = SafeColor(hasPalette ? palette.airFar : hasAtmosphere ? atmosphere.farColor : Color.white, Color.white);
            result.AirNearDistance = Safe(hasAtmosphere ? atmosphere.nearDistance : 25f, 25f, 0f, 10000f);
            result.AirMidDistance = Mathf.Max(result.AirNearDistance + .001f, Safe(hasAtmosphere ? atmosphere.midDistance : 80f, 80f, 0f, 20000f));
            result.AirFarDistance = Mathf.Max(result.AirMidDistance + .001f, Safe(hasAtmosphere ? atmosphere.farDistance : 180f, 180f, 0f, 30000f));
            result.Extinction = Safe(hasAtmosphere ? atmosphere.extinction : 0f, 0f, 0f, 1f);
            result.SaturationLoss = Safe(hasAtmosphere ? atmosphere.saturationLoss : 0f, 0f, 0f, 1f);
            result.ContrastLoss = Safe(hasAtmosphere ? atmosphere.contrastLoss : 0f, 0f, 0f, 1f);
            result.NearInfluence = Safe(hasAtmosphere ? atmosphere.nearColorInfluence : 0f, 0f, 0f, 1f);
            return result;
        }

        static float Safe(float value, float fallback, float minimum, float maximum)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum); }

        static Color SafeColor(Color value, Color fallback)
        {
            return new Color(Safe(value.r, fallback.r, 0f, 8f), Safe(value.g, fallback.g, 0f, 8f),
                Safe(value.b, fallback.b, 0f, 8f), Safe(value.a, fallback.a, 0f, 1f));
        }

        static bool Finite(Vector3 value)
        { return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z); }
    }
}
