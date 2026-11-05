/* 功能：保存可复制的角色光感作者参数，独立控制环境染色、内侧轮廓、高亮保护与受光外缘柔光。
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
        [Tooltip("让角色环境基础色度跟随有效环境；直射与局部灯保留原色。关闭不影响轮廓/柔光/保护。")]
        public bool environmentTint = true;
        [Tooltip("增强模式把实际可读性补足与底光一起染色；关闭精确使用原版底光染色。")]
        public bool enhancedEnvironmentTint = true;
        [Range(0, 2), Tooltip("0精确回A；0到1过渡为B，1到2放大环境色响应（最终权重限1）；不改环境强度或D/L。")]
        public float environmentBlend = 2;
        [Tooltip("按真实角色完整覆盖，在朝向有效灯的一侧产生细内带；不向外扩张。")]
        public bool rim = true;
        [Tooltip("压缩角色本体与轮廓合计光照增益的过亮段，保留原图纹理与透明度。")]
        public bool highlightProtection = true;

        [Tooltip("只在当前合法可见的受光自然外缘增加薄淡柔光；不抬高身体亮度或扩大角色显示权限。")]
        public bool outerGlow = true;
        [Tooltip("仅查看新增外缘能量的诊断模式；不作为正常显示预设，也不关闭本体高亮保护。")]
        public bool outerDiagnostic;

        [Range(0, 1), Tooltip("环境色响应强度。0严格回对应原路径；不等同物理天空漫反射积分。")]
        public float tintStrength = 1f;
        [Range(0, 2), Tooltip("方向性轮廓贡献倍率；没有有效灯时保持0贡献。")]
        public float rimStrength = 2;
        [Range(.25f, 4), Tooltip("最终画面的内侧轮廓宽度，单位为像素；不是源图集像素。")]
        public float innerWidth = 4f;
        [Range(.05f, 2), Tooltip("内侧轮廓过渡柔度；不扩张角色显示许可。")]
        public float softness = 2;
        [Range(0, 2), Tooltip("轮廓在总光照增益中的独立亮度预算；强主体光照时进一步收敛。")]
        public float maxRimGain = 2;
        [Range(.05f, 16), Tooltip("线性光照增益的软肩拐点；低于此值完全不压缩。")]
        public float knee = 16;
        [Range(.1f, 32), Tooltip("线性光照增益的渐近亮度上限；不逐通道硬截颜色。")]
        public float ceiling = 32;

        [Range(1, 4), Tooltip("外扩宽度，单位为1080p等效最终画面像素；后端按实际输出高度和Render Scale换算，独立于内侧亮边。")]
        public float outerWidth = 4;
        [Range(0, 2), Tooltip("外缘独立受光贡献倍率；0严格关闭新增能量，亮度保底不作为发光来源。")]
        public float outerStrength = 2;
        [Range(0, 1), Tooltip("新增外缘能量的独立有界预算；不更改身体原高亮保护或内侧轮廓预算。")]
        public float maxOuterGain = 1;
        [Range(.25f, 4), Tooltip("仅改变外缘向外衰减的柔度；不放宽窗孔、墙体、黑幕与角色身份边界。")]
        public float outerSoftness = 4;

        public CharacterLightingSettings Clone()
        {
            return new CharacterLightingSettings
            {
                environmentTint = environmentTint,
                enhancedEnvironmentTint = enhancedEnvironmentTint,
                environmentBlend = environmentBlend,
                rim = rim,
                highlightProtection = highlightProtection,
                outerGlow = outerGlow,
                outerDiagnostic = outerDiagnostic,
                tintStrength = tintStrength,
                rimStrength = rimStrength,
                innerWidth = innerWidth,
                softness = softness,
                maxRimGain = maxRimGain,
                knee = knee,
                ceiling = ceiling,
                outerWidth = outerWidth,
                outerStrength = outerStrength,
                maxOuterGain = maxOuterGain,
                outerSoftness = outerSoftness
            };
        }

        public void Sanitize()
        {
            tintStrength = FiniteRange(tintStrength, 0, 1, 1f);
            environmentBlend = FiniteRange(environmentBlend, 0, 2, 2);
            rimStrength = FiniteRange(rimStrength, 0, 2, 2);
            innerWidth = FiniteRange(innerWidth, .25f, 4, 4f);
            softness = FiniteRange(softness, .05f, 2, 2);
            maxRimGain = FiniteRange(maxRimGain, 0, 2, 2);
            knee = FiniteRange(knee, .05f, 16, 16);
            ceiling = FiniteRange(ceiling, knee + .05f, 32, 32);
            outerWidth = FiniteRange(outerWidth, 1, 4, 4);
            outerStrength = FiniteRange(outerStrength, 0, 2, 2);
            maxOuterGain = FiniteRange(maxOuterGain, 0, 1, 1);
            outerSoftness = FiniteRange(outerSoftness, .25f, 4, 4);
        }

        static float FiniteRange(float value, float minimum, float maximum, float fallback)
        {
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);
        }
    }
}
