/*
 * 脚本：CombatHitFlash
 * 功能：给支持闪白的战斗材质提交每个渲染器的瞬时强度，清除时只还原这项参数。
 * 协作：CombatVitals 决定脉冲和结束时机；精灵、Spine 与光照继续各自持有纹理、颜色和透明度。
 */
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Combat
{
    internal sealed class CombatHitFlash
    {
        private static readonly int FlashId = Shader.PropertyToID("_OssastraHitFlash");
        private const float ActiveStrength = .82f;

        private readonly Renderer[] renderers;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly List<Material> materials = new List<Material>(4);

        public CombatHitFlash(Renderer[] renderers) => this.renderers = renderers;

        public void Apply() => Write(ActiveStrength);
        public void Clear() => Write(0);

        private void Write(float strength)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetSharedMaterials(materials);
                bool supported = false;
                for (int i = 0; i < materials.Count; i++)
                    supported |= SupportsFlash(materials[i]);

                if (strength == 0 || supported) WriteBlock(renderer, strength, -1);
                for (int i = 0; i < materials.Count; i++)
                    if (strength == 0 || SupportsFlash(materials[i])) WriteBlock(renderer, strength, i);
            }
        }

        private void WriteBlock(Renderer renderer, float strength, int index)
        {
            if (index < 0) renderer.GetPropertyBlock(block);
            else renderer.GetPropertyBlock(block, index);

            // 空材质槽继承 Renderer 块。空槽写自己的块会遮蔽 Renderer 上的纹理、光照等属性。
            if ((index >= 0 && block.isEmpty) || block.GetFloat(FlashId) == strength) return;
            block.SetFloat(FlashId, strength);
            if (index < 0) renderer.SetPropertyBlock(block);
            else renderer.SetPropertyBlock(block, index);
        }

        private static bool SupportsFlash(Material material) => material != null && material.HasProperty(FlashId);
    }
}
