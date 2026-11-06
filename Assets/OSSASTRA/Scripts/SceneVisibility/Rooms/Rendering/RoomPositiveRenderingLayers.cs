/* 功能：给已登记表面规划静态桶、只读动态Lit与独立无光照绘制筛选位，并明确支持的材质家族。
 * 边界：只写帧DTO，不修改Renderer、原材质、物理Layer或原光照位；混合家族与超预算保守拒绝。
 * 生命周期：组织者在相机剔除前附加这些位，结束相机时还原原值。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    public static class RoomPositiveRenderingLayers
    {
        // 调度位不等同着色实现；显式Voxel源与Lit共用筛选位，由源感知resolver选各自适配器。
        public const string VoxelLitShaderName = "Ossastra/WorldRendering/VoxelLit";
        public const uint FullBit = 1u << 16, PartialLitBit = 1u << 17,
            PartialUnlitBit = 1u << 18, PartialThinTopBit = 1u << 19,
            PartialDecorationBit = 1u << 20, PartialDepthPlaneBit = 1u << 21,
            PartialSpineBit = 1u << 22, PartialSpriteBit = 1u << 23, RoofBit = 1u << 24, PartialWorldTileBit = 1u << 25,
            PartialLitSpineBit = 1u << 26, PartialSimpleLitBit = 1u << 27, PartialMapDepthBit = 1u << 28,
            PartialSkeletonBit = 1u << 29, PartialDefaultSpriteBit = 1u << 30, IndependentBit = 1u << 31;
        public const uint ReservedMask = FullBit | PartialLitBit | PartialUnlitBit | PartialThinTopBit |
            PartialDecorationBit | PartialDepthPlaneBit | PartialSpineBit | PartialSpriteBit | RoofBit | PartialWorldTileBit | PartialLitSpineBit |
            PartialSimpleLitBit | PartialMapDepthBit | PartialSkeletonBit | PartialDefaultSpriteBit | IndependentBit;
        // 这些家族始终只读原材质，因而不再用其旧家族位过滤静态桶。六位只供有界动态Lit组。
        static readonly uint[] independentLitBits = { PartialDepthPlaneBit, PartialSpineBit, PartialSpriteBit,
            PartialLitSpineBit, PartialSkeletonBit, PartialDefaultSpriteBit };

        public static bool IsIndependentLitFamily(uint family) => family == PartialLitBit || family == PartialSimpleLitBit || family == PartialLitSpineBit;

        public static bool RequiresIndependentDraw(RoomPositiveSurface surface)
        {
            var renderer = surface.Renderer;
            if (renderer == null) return false;
            if (surface.IndependentDraw || renderer is SpriteRenderer ||
                renderer.GetComponentInParent<Ossastra.Characters.CharacterRoot>() != null ||
                renderer.GetComponentInParent<Ossastra.Enemies.EnemyRoot>() != null) return true;
            foreach (var material in renderer.sharedMaterials)
            {
                uint family = Classify(material);
                if (family == PartialDepthPlaneBit || family == PartialSpineBit || family == PartialSpriteBit ||
                    family == PartialLitSpineBit || family == PartialSkeletonBit || family == PartialDefaultSpriteBit) return true;
            }
            return false;
        }

        public static void AssignDrawMasks(RoomPositiveSurface[] surfaces)
        {
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            var litGroups = new Dictionary<Material, uint>();
            for (int i = 0; i < surfaces.Length; ++i)
            {
                var surface = surfaces[i]; var renderer = surface.Renderer;
                surface.DrawMask = 0; surface.DrawMaterial = null;
                if (renderer == null) { surfaces[i] = surface; continue; }
                uint family = Classify(renderer);
                if (family == 0) { surfaces[i] = surface; continue; }
                if (surface.Kind == RoomPositiveSurfaceKind.Full) surface.DrawMask = FullBit;
                else if (!RequiresIndependentDraw(surface)) surface.DrawMask = surface.Kind == RoomPositiveSurfaceKind.Roof ? RoofBit : family;
                else if (!IsIndependentLitFamily(family)) surface.DrawMask = IndependentBit;
                else
                {
                    var materials = renderer.sharedMaterials; var source = materials[0];
                    for (int slot = 1; slot < materials.Length; ++slot)
                        if (!ReferenceEquals(source, materials[slot])) throw new InvalidOperationException("动态Lit逐对象路线只支持同一源材质的槽；拒绝不同材质槽混画：" + renderer.name);
                    if (!litGroups.TryGetValue(source, out uint mask))
                    {
                        if (litGroups.Count == independentLitBits.Length) throw new InvalidOperationException("动态Lit超过6个独立源材质组；拒绝截断或改写角色材质。");
                        mask = independentLitBits[litGroups.Count]; litGroups.Add(source, mask);
                    }
                    surface.DrawMask = mask; surface.DrawMaterial = source;
                }
                surfaces[i] = surface;
            }
        }

        public static uint GetMask(RoomPositiveSurface surface)
        {
            if (surface.DrawMask != 0) return surface.DrawMask;
            if (Classify(surface.Renderer) == 0) return 0;
            if (surface.Kind == RoomPositiveSurfaceKind.Full) return FullBit;
            return RequiresIndependentDraw(surface) ? IndependentBit : surface.Kind == RoomPositiveSurfaceKind.Roof ? RoofBit : Classify(surface.Renderer);
        }

        public static uint GetMask(Renderer renderer, RoomPositiveSurfaceKind kind)
        {
            return GetMask(new RoomPositiveSurface { Renderer = renderer, Kind = kind });
        }

        public static uint Classify(Renderer renderer)
        {
            if (renderer == null) return 0;
            if (renderer is SpriteRenderer sprite &&
                (sprite.drawMode != SpriteDrawMode.Simple || sprite.maskInteraction != SpriteMaskInteraction.None)) return 0;
            uint family = 0;
            foreach (var material in renderer.sharedMaterials)
            {
                uint next = Classify(material);
                if (next == 0 || (family != 0 && family != next)) return 0;
                family = next;
            }
            return family;
        }

        public static uint Classify(Material material)
        {
            if (material == null || material.shader == null) return 0;
            string name = material.shader.name;
            if (name == "Universal Render Pipeline/Lit" || name == "Universal Render Pipeline/Simple Lit" ||
                name == "Universal Render Pipeline/Unlit" || name == VoxelLitShaderName)
            {
                if (material.HasProperty("_Surface") && material.GetFloat("_Surface") != 0) return 0;
                return name == "Universal Render Pipeline/Unlit" ? PartialUnlitBit :
                    name == "Universal Render Pipeline/Simple Lit" ? PartialSimpleLitBit : PartialLitBit;
            }
            if (name == "Ossastra/GroundSurfaces/ThinTop") return PartialThinTopBit;
            if (name == "Ossastra/GroundResearch/MaskWorldTile") return PartialWorldTileBit;
            if (name == "Ossastra/GroundWorkshop/DecorationCutout") return PartialDecorationBit;
            if (name == "Ossastra/MapResearch/DepthPlane") return PartialMapDepthBit;
            if (name == "Ossastra/Characters/UprightDepthPlane") return PartialDepthPlaneBit;
            if (name == "Spine/Skeleton") return PartialSkeletonBit;
            if (name == "Ossastra/Characters/UprightSpinePma") return PartialSpineBit;
            if (name == "Universal Render Pipeline/Spine/Sprite" || name == "Universal Render Pipeline/Spine/Sprite MainBody") return PartialLitSpineBit;
            if (name == "Sprites/Default") return PartialDefaultSpriteBit;
            if (name == "Ossastra/Characters/UprightSpriteDefault") return PartialSpriteBit;
            return 0;
        }
    }
}
