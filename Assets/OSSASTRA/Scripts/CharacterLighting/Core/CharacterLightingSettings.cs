/* 功能：保存可复制的角色光感作者参数，独立控制环境染色、内侧轮廓与高亮保护。
 * 边界：仅描述视觉参数，不保存灯列表、RT、当前房间或业务状态；运行时使用Clone副本，不能回写共享Profile。
 * 校验：非有限值回中性可用缺省；软肩上限始终大于拐点，防止除零或强光放大。
 */
using System;
using UnityEngine;

namespace Ossastra.CharacterLighting
{
    [Serializable]
    public sealed class CharacterLightingSettings
    {
        [Tooltip("只让环境底光的色度跟随手动天空覆盖；直射与局部灯保留原色。")]
        public bool environmentTint = true;
        [Tooltip("按真实角色完整覆盖，在朝向有效灯的一侧产生细内带；不向外扩张。")]
        public bool rim = true;
        [Tooltip("压缩角色本体与轮廓合计光照增益的过亮段，保留原图纹理与透明度。")]
        public bool highlightProtection = true;

        [Range(0, 1), Tooltip("环境色响应强度。0严格回对应原路径；不等同物理天空漫反射积分。")]
        public float tintStrength = 1f;
        [Range(0, 2), Tooltip("方向性轮廓贡献倍率；没有有效灯时保持0贡献。")]
        public float rimStrength = .28f;
        [Range(.25f, 4), Tooltip("最终画面的内侧轮廓宽度，单位为像素；不是源图集像素。")]
        public float innerWidth = 4f;
        [Range(.05f, 2), Tooltip("内侧轮廓过渡柔度；不扩张角色显示许可。")]
        public float softness = 1.23f;
        [Range(0, 2), Tooltip("轮廓在总光照增益中的独立亮度预算；强主体光照时进一步收敛。")]
        public float maxRimGain = .35f;
        [Range(.05f, 16), Tooltip("线性光照增益的软肩拐点；低于此值完全不压缩。")]
        public float knee = 1.2f;
        [Range(.1f, 32), Tooltip("线性光照增益的渐近亮度上限；不逐通道硬截颜色。")]
        public float ceiling = 2;

        public CharacterLightingSettings Clone()
        {
            return new CharacterLightingSettings
            {
                environmentTint = environmentTint,
                rim = rim,
                highlightProtection = highlightProtection,
                tintStrength = tintStrength,
                rimStrength = rimStrength,
                innerWidth = innerWidth,
                softness = softness,
                maxRimGain = maxRimGain,
                knee = knee,
                ceiling = ceiling
            };
        }

        public void Sanitize()
        {
            tintStrength = FiniteRange(tintStrength, 0, 1, 1f);
            rimStrength = FiniteRange(rimStrength, 0, 2, .28f);
            innerWidth = FiniteRange(innerWidth, .25f, 4, 4f);
            softness = FiniteRange(softness, .05f, 2, 1.23f);
            maxRimGain = FiniteRange(maxRimGain, 0, 2, .35f);
            knee = FiniteRange(knee, .05f, 16, 1.2f);
            ceiling = FiniteRange(ceiling, knee + .05f, 32, Mathf.Max(knee + .05f, 2));
        }

        static float FiniteRange(float value, float minimum, float maximum, float fallback)
        {
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);
        }
    }
}
