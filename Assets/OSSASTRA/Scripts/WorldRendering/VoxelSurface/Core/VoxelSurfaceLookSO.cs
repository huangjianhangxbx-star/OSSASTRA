/* 功能：体素完整外观候选的数据资产；用明确的C作者材质映射配色，并引用已有受光Profile。
 * 边界：不引用场景Renderer、不实例化材质、不调灯/相机；原对象及原完整材质数组仍由比较组件保存。
 * 作者约定：source为binding.shaped中的明确作者源；replacement为独立体素材质，未映射槽维持原C。
 */
using System;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [CreateAssetMenu(menuName = "OSSASTRA/Rendering/Voxel Surface Look", fileName = "VoxelSurfaceLook")]
    public sealed class VoxelSurfaceLookSO : ScriptableObject
    {
        [Serializable]
        public sealed class MaterialOverride
        {
            public Material source;
            public Material replacement;
            public MaterialOverride Clone() => new MaterialOverride { source = source, replacement = replacement };
        }

        public string label = "体素候选";
        [Tooltip("只用于开发四格/诊断；正式面板隐藏，不能设为正常默认。")]
        public bool developmentOnly;
        public VoxelSurfaceProfileSO responseProfile;
        public MaterialOverride[] materials = Array.Empty<MaterialOverride>();

        public bool Validate(out string reason)
        {
            if (responseProfile == null || responseProfile.settings == null) { reason = "候选缺少受光Profile"; return false; }
            if (materials == null || materials.Length == 0) { reason = "候选没有明确材质映射"; return false; }
            var sources = new System.Collections.Generic.HashSet<Material>();
            foreach (var item in materials)
            {
                if (item == null || item.source == null || item.replacement == null) { reason = "候选存在缺失材质映射"; return false; }
                if (!sources.Add(item.source)) { reason = "候选同一源材质重复登记"; return false; }
                if (item.source.shader == null || item.replacement.shader == null ||
                    item.source.shader.name != "Ossastra/WorldRendering/VoxelLit" || item.replacement.shader.name != "Ossastra/WorldRendering/VoxelLit")
                { reason = "候选仅接受明确VoxelLit作者材质，不接管2D或其他家族"; return false; }
            }
            reason = ""; return true;
        }
    }

    [Serializable]
    public sealed class VoxelSurfaceLookSnapshot
    {
        public string label;
        public bool developmentOnly;
        public VoxelSurfaceSettings settings;
        public VoxelSurfaceLookSO.MaterialOverride[] materials;
    }
}
