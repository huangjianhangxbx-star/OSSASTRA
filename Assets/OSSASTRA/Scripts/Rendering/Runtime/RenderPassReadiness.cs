/* 功能：只检查即将实际绘制的材质Pass，防止编辑器异步青色占位进入中间数据。
 * 边界：不改全局异步设置、不全量Warmup；Player使用其实际已打包着色器。
 */
using UnityEngine;
namespace Ossastra.Rendering
{
    public static class RenderPassReadiness
    {
        public static bool Ensure(Material material, int pass)
        {
            if (material == null || material.shader == null || !material.shader.isSupported || pass < 0 || pass >= material.passCount) return false;
#if UNITY_EDITOR
            if (!UnityEditor.ShaderUtil.IsPassCompiled(material, pass))
                UnityEditor.ShaderUtil.CompilePass(material, pass, true);
            return UnityEditor.ShaderUtil.IsPassCompiled(material, pass);
#else
            return true;
#endif
        }
    }
}
