/* 功能：体素表面的独立受光作者参数；中性值保留原PBR响应。
 * 边界：不持有灯、天气、相机、材质实例或业务状态；ApplyTo只写调用方显式提供的材质。
 */
using System;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [Serializable]
    public sealed class VoxelSurfaceSettings
    {
        [Range(0, 1), Tooltip("主方向光的连续分面响应。0保留原始NdotL；不改变法线、镜面、阴影或局部灯距离。")]
        public float mainShape;
        [Range(0, 2), Tooltip("间接漫反射贡献倍率。1保留原PBR；不等同全局环境光强度，也不增加角色保底。")]
        public float indirectDiffuse = 1;
        [Range(0, 2), Tooltip("环境反射/环境镜面贡献倍率。与间接漫反射独立，0只关闭这部分反射。")]
        public float environmentSpecular = 1;
        [Range(0, 2), Tooltip("直接灯镜面贡献倍率。1保留原PBR；不改变直接漫反射或灯本身。")]
        public float directSpecular = 1;
        [Range(0, 1), Tooltip("亮部柔性压缩强度。0精确关闭；按同一RGB倍率保色，不逐通道硬截断。")]
        public float highlightStrength;
        [Range(.01f, 16), Tooltip("线性受光贡献的软肩拐点；低于该值不压缩。")]
        public float highlightKnee = 4;
        [Range(.02f, 32), Tooltip("软肩渐近上限，自动保持大于拐点；不改变全局曝光或后处理。")]
        public float highlightCeiling = 8;
        [Range(0, 7), Tooltip("0合成；1固有色；2世界法线；3主灯漫反射；4间接漫反射；5直接镜面；6环境反射；7真实主灯阴影。")]
        public int diagnostic;
        [Tooltip("显式把Mesh顶点色乘入固有色。默认关闭；仅在确认模型顶点色用途后开启。")]
        public bool vertexColors;

        public VoxelSurfaceSettings Clone()
        {
            return (VoxelSurfaceSettings)MemberwiseClone();
        }

        public void Sanitize()
        {
            mainShape = FiniteRange(mainShape, 0, 1, 0);
            indirectDiffuse = FiniteRange(indirectDiffuse, 0, 2, 1);
            environmentSpecular = FiniteRange(environmentSpecular, 0, 2, 1);
            directSpecular = FiniteRange(directSpecular, 0, 2, 1);
            highlightStrength = FiniteRange(highlightStrength, 0, 1, 0);
            highlightKnee = FiniteRange(highlightKnee, .01f, 16, 4);
            highlightCeiling = FiniteRange(highlightCeiling, highlightKnee + .01f, 32, 8);
            diagnostic = Mathf.Clamp(diagnostic, 0, 7);
        }

        public void ApplyTo(Material material)
        {
            if (material == null) return;
            var value = Clone();
            value.Sanitize();
            Set(material, "_VoxelMainShape", value.mainShape);
            Set(material, "_VoxelIndirectDiffuse", value.indirectDiffuse);
            Set(material, "_VoxelEnvironmentSpecular", value.environmentSpecular);
            Set(material, "_VoxelDirectSpecular", value.directSpecular);
            Set(material, "_VoxelHighlightStrength", value.highlightStrength);
            Set(material, "_VoxelHighlightKnee", value.highlightKnee);
            Set(material, "_VoxelHighlightCeiling", value.highlightCeiling);
            Set(material, "_VoxelDiagnostic", value.diagnostic);
            Set(material, "_VoxelVertexColors", value.vertexColors ? 1 : 0);
        }

        static void Set(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        static float FiniteRange(float value, float minimum, float maximum, float fallback)
        {
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);
        }
    }
}
