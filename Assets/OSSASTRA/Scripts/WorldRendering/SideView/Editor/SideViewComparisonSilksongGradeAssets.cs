using UnityEngine;

namespace Ossastra.SideView.Rendering
{
    internal static class SideViewComparisonSilksongGradeAssets
    {
        internal const string ShaderName = "Hidden/Ossastra/SideView/ComparisonSilksongGrade";
        internal const int ExtractBloomPass = 0;
        internal const int BlurVerticalPass = 1;
        internal const int BlurHorizontalPass = 2;
        internal const int CompositeGradePass = 3;
        internal const int BloomDownsample = 4;
        internal const int BloomIterations = 2;
        internal const float DefaultNoiseStrength = 0.1f;

        private const int LutWidth = 256;
        private const int LutHeight = 4;
        private const int NoiseSize = 1024;

        // 原作 Tut_01 的 MOSS_CAVE 常态曲线。控制器持有并销毁返回纹理。
        internal static Texture2D CreateMossCaveRgbLut(bool makeNoLongerReadable = true)
        {
            var red = new AnimationCurve(
                new Keyframe(0f, 0f, 0.8559380174f, 0.8559380174f),
                new Keyframe(0.2368900478f, 0.2027631998f, 1.0653557777f, 1.0653557777f),
                new Keyframe(0.7136604786f, 0.8105375767f, 0.8802078366f, 0.8802078366f),
                new Keyframe(1f, 1f, 0.4636189640f, 0.4636189640f));
            var green = new AnimationCurve(
                new Keyframe(0f, 0f, 1.1007751226f, 1.1007751226f),
                new Keyframe(0.7442307472f, 0.8192307353f, 0.9037710428f, 0.9037710428f),
                new Keyframe(1f, 1f, 0.7067669630f, 0.7067669630f));
            var blue = new AnimationCurve(
                new Keyframe(0f, 0f, 1.2471153736f, 1.2471153736f),
                new Keyframe(0.0969447717f, 0.1209013164f, 1.0363639593f, 1.0363639593f),
                new Keyframe(0.7633482218f, 0.6710923314f, 1.1077252626f, 1.1077252626f),
                new Keyframe(1f, 1f, 1.3898380995f, 1.3898380995f));

            var pixels = new Color32[LutWidth * LutHeight];
            WriteCurveRow(pixels, 0, red);
            WriteCurveRow(pixels, 1, green);
            WriteCurveRow(pixels, 2, blue);

            var texture = new Texture2D(LutWidth, LutHeight, TextureFormat.RGBA32, false, true)
            {
                name = "SV Comparison Moss Cave RGB LUT",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: makeNoLongerReadable);
            return texture;
        }

        // 原作颗粒纹理为 1024²，使用 PerlinNoise 和 1920×1080 的基准密度。
        // B 只取红通道。创建时一次生成，普通逐帧 Blit 只绑定纹理和时间。
        internal static Texture2D CreateCameraNoiseTexture()
        {
            const float density = 1.8f;
            float xStep = 1920f / (density * NoiseSize);
            float yStep = 1080f / (density * NoiseSize);
            var pixels = new byte[NoiseSize * NoiseSize];
            for (int y = 0; y < NoiseSize; y++)
            {
                for (int x = 0; x < NoiseSize; x++)
                {
                    float noise = Mathf.PerlinNoise(x * xStep, y * yStep);
                    pixels[y * NoiseSize + x] = Quantize01(noise);
                }
            }

            var texture = new Texture2D(NoiseSize, NoiseSize, TextureFormat.R8, true, true)
            {
                name = "SV Comparison Camera Noise",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        private static void WriteCurveRow(Color32[] pixels, int row, AnimationCurve curve)
        {
            int start = row * LutWidth;
            for (int x = 0; x < LutWidth; x++)
            {
                byte value = Quantize01(curve.Evaluate(x / (LutWidth - 1f)));
                pixels[start + x] = new Color32(value, value, value, byte.MaxValue);
            }
        }

        private static byte Quantize01(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * byte.MaxValue);
        }
    }
}
