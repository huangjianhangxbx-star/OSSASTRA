/* 功能：让限定源Shader绘制读取所属空间与屋顶像素权限，交给按原材质分桶的overrideMaterial绘制。
 * 边界：不换原材质，不改原物理/业务；逐Renderer及已有非空材质槽保留原PropertyBlock。
 * 生命周期：本相机绘制前捕获，CameraCleanup或异常撤销；缓存仅保留本帧明确登记对象。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    internal sealed class RoomPositivePropertyScope
    {
        sealed class Slot
        {
            internal readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
            internal readonly MaterialPropertyBlock Working = new MaterialPropertyBlock();
            internal bool Written;
        }
        sealed class Entry
        {
            internal readonly Slot RendererBlock = new Slot();
            internal readonly List<Slot> Slots = new List<Slot>();
        }
        // Only the zero-owner, non-roof terrain path is eligible. Those three
        // Append fields are fixed to zero and neither owner array is uploaded;
        // all seven remaining Append values participate in exact key equality.
        readonly struct Payload : IEquatable<Payload>
        {
            readonly bool exterior, staticEnvironment, environmentFull;
            readonly float role, response, shadow, minimum;
            internal Payload(RoomPositiveSurface surface)
            {
                exterior = surface.OwnerExterior; staticEnvironment = surface.StaticEnvironment;
                environmentFull = surface.EnvironmentFullSurface; role = surface.EnvironmentSurfaceRole;
                response = surface.EnvironmentResponseOverride; shadow = surface.EnvironmentReceiveShadow;
                minimum = surface.EnvironmentMinimumOverride;
            }
            public bool Equals(Payload other) => exterior == other.exterior && staticEnvironment == other.staticEnvironment &&
                environmentFull == other.environmentFull && role.Equals(other.role) && response.Equals(other.response) &&
                shadow.Equals(other.shadow) && minimum.Equals(other.minimum);
            public override bool Equals(object other) => other is Payload value && Equals(value);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = exterior ? 1 : 0;
                    hash = hash * 31 + (staticEnvironment ? 1 : 0); hash = hash * 31 + (environmentFull ? 1 : 0);
                    hash = hash * 31 + role.GetHashCode(); hash = hash * 31 + response.GetHashCode();
                    hash = hash * 31 + shadow.GetHashCode(); return hash * 31 + minimum.GetHashCode();
                }
            }
        }
        sealed class Template
        {
            // Never used as GetPropertyBlock output, Original or merge Working.
            internal readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
            internal bool Used;
        }
        readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        readonly HashSet<Renderer> used = new HashSet<Renderer>();
        readonly List<Renderer> stale = new List<Renderer>();
        readonly Dictionary<Payload, Template> templates = new Dictionary<Payload, Template>();
        readonly List<Payload> staleTemplates = new List<Payload>();
        readonly Matrix4x4[] ownerMatrices = new Matrix4x4[8];
        readonly Vector4[] ownerExtents = new Vector4[8];

        internal int TemplateCount => templates.Count;
        // Per Apply call, including a new template shared by later rows.
        internal int TemplateBuildCount { get; private set; }
        internal int TemplateRendererWrites { get; private set; }
        internal int TemplateFallbackCount { get; private set; }

        internal int BlockCount
        {
            get { int count = templates.Count; foreach (var entry in entries.Values) count += 2 + entry.Slots.Count * 2; return count; }
        }

        internal void Apply(RoomMaskFrame frame) => ApplyCore(frame, null);

        // Consumed only inside the caller's private synchronous preCull prepare
        // interval. It invokes no resolver, publication or other external callback.
        internal void ApplyTerrain(RoomMaskFrame frame, RoomStaticTerrainPreparation terrain) => ApplyCore(frame, terrain);

        void ApplyCore(RoomMaskFrame frame, RoomStaticTerrainPreparation terrain)
        {
            Restore(); used.Clear();
            TemplateBuildCount = TemplateRendererWrites = TemplateFallbackCount = 0;
            foreach (var template in templates.Values) template.Used = false;
            try
            {
                foreach (var surface in frame.PositiveSurfaces)
                {
                    var renderer = surface.Renderer;
                    if (renderer == null || surface.Kind == RoomPositiveSurfaceKind.Full || !used.Add(renderer)) continue;
                    // 手工无光照路线逐draw上传owner，不让原角色/FX写者缓存本模块的临时MPB。
                    if (surface.DrawMask == RoomPositiveRenderingLayers.IndependentBit && RoomPositiveRenderingLayers.RequiresIndependentDraw(surface)) continue;
                    if (!entries.TryGetValue(renderer, out var entry)) { entry = new Entry(); entries.Add(renderer, entry); }

                    Material[] materials = null;
                    bool prepared = terrain != null && surface.Kind == RoomPositiveSurfaceKind.Partial &&
                        (surface.OwnerVolumes == null || surface.OwnerVolumes.Length == 0) &&
                        terrain.TryPrepared(renderer, out materials) && materials != null;
                    if (prepared)
                    {
                        // Every Original is captured anew, including every empty
                        // slot, before choosing a template or writing anything.
                        int slots = materials.Length;
                        while (entry.Slots.Count < slots) entry.Slots.Add(new Slot());
                        renderer.GetPropertyBlock(entry.RendererBlock.Original);
                        bool empty = entry.RendererBlock.Original.isEmpty;
                        for (int i = 0; i < slots; ++i)
                        {
                            renderer.GetPropertyBlock(entry.Slots[i].Original, i);
                            empty &= entry.Slots[i].Original.isEmpty;
                        }
                        if (empty)
                        {
                            var payload = new Payload(surface);
                            if (!templates.TryGetValue(payload, out var template))
                            {
                                template = new Template(); Append(template.Block, frame, surface);
                                templates.Add(payload, template); ++TemplateBuildCount;
                            }
                            template.Used = true;
                            renderer.SetPropertyBlock(template.Block); entry.RendererBlock.Written = true;
                            ++TemplateRendererWrites;
                        }
                        else
                        {
                            ++TemplateFallbackCount;
                            MergeCaptured(renderer, entry, slots, frame, surface);
                        }
                        continue;
                    }

                    // Original public/reflection path is retained. Qualification
                    // failure uses its full fresh source/merge/restore behavior.
                    renderer.GetPropertyBlock(entry.RendererBlock.Original);
                    renderer.GetPropertyBlock(entry.RendererBlock.Working);
                    Append(entry.RendererBlock.Working, frame, surface);
                    renderer.SetPropertyBlock(entry.RendererBlock.Working); entry.RendererBlock.Written = true;
                    int ordinarySlots = renderer.sharedMaterials.Length;
                    while (entry.Slots.Count < ordinarySlots) entry.Slots.Add(new Slot());
                    for (int i = 0; i < ordinarySlots; ++i)
                    {
                        var slot = entry.Slots[i]; renderer.GetPropertyBlock(slot.Original, i);
                        if (slot.Original.isEmpty) continue;
                        renderer.GetPropertyBlock(slot.Working, i); Append(slot.Working, frame, surface);
                        renderer.SetPropertyBlock(slot.Working, i); slot.Written = true;
                    }
                }
            }
            finally
            {
                // At most one key for each active eligible row; no historical
                // payload pool grows when weather/role/author parameters change.
                staleTemplates.Clear();
                foreach (var pair in templates) if (!pair.Value.Used) staleTemplates.Add(pair.Key);
                foreach (var key in staleTemplates) templates.Remove(key);
            }
            stale.Clear(); foreach (var renderer in entries.Keys) if (renderer == null || !used.Contains(renderer)) stale.Add(renderer);
            foreach (var renderer in stale) entries.Remove(renderer);
        }

        void MergeCaptured(Renderer renderer, Entry entry, int slots, RoomMaskFrame frame, RoomPositiveSurface surface)
        {
            renderer.GetPropertyBlock(entry.RendererBlock.Working);
            Append(entry.RendererBlock.Working, frame, surface);
            renderer.SetPropertyBlock(entry.RendererBlock.Working); entry.RendererBlock.Written = true;
            for (int i = 0; i < slots; ++i)
            {
                var slot = entry.Slots[i];
                if (slot.Original.isEmpty) continue;
                renderer.GetPropertyBlock(slot.Working, i); Append(slot.Working, frame, surface);
                renderer.SetPropertyBlock(slot.Working, i); slot.Written = true;
            }
        }

        void Append(MaterialPropertyBlock block, RoomMaskFrame frame, RoomPositiveSurface surface)
        {
            var volumes = surface.OwnerVolumes;
            int count = volumes == null ? 0 : volumes.Length;
            block.SetInteger("_RoomSightOwnerCount", count);
            block.SetInteger("_RoomSightOwnerExterior", surface.OwnerExterior ? 1 : 0);
            block.SetFloat("_RoomSightStaticEnvironment",surface.StaticEnvironment?1:0);
            block.SetFloat("_EnvironmentFullSurface",surface.EnvironmentFullSurface?1:0);
            block.SetFloat("_EnvironmentSurfaceRole",surface.EnvironmentSurfaceRole);
            block.SetFloat("_EnvironmentResponseOverride",surface.EnvironmentResponseOverride);
            block.SetFloat("_EnvironmentReceiveShadow",surface.EnvironmentReceiveShadow);
            block.SetFloat("_EnvironmentMinimumOverride",surface.EnvironmentMinimumOverride);
            for (int i = 0; i < count; ++i) { ownerMatrices[i] = volumes[i].WorldToLocal; ownerExtents[i] = volumes[i].HalfExtents; }
            if (count > 0) { block.SetMatrixArray("_RoomSightOwnerMatrices", ownerMatrices); block.SetVectorArray("_RoomSightOwnerExtents", ownerExtents); }
            bool roof = surface.Kind == RoomPositiveSurfaceKind.Roof;
            block.SetFloat("_RoomSightRoofMode", roof ? 1 : 0);
            block.SetFloat("_RoomSightCutRoof", roof && frame.ExteriorView && frame.PreviewRoofSurfaces != null &&
                Array.IndexOf(frame.PreviewRoofSurfaces, surface.Renderer) >= 0 ? 1 : 0);
        }

        internal void Restore()
        {
            foreach (var pair in entries)
            {
                var renderer = pair.Key; var entry = pair.Value;
                if (renderer != null)
                {
                    if (entry.RendererBlock.Written) renderer.SetPropertyBlock(entry.RendererBlock.Original.isEmpty ? null : entry.RendererBlock.Original);
                    for (int i = 0; i < entry.Slots.Count; ++i)
                        if (entry.Slots[i].Written) renderer.SetPropertyBlock(entry.Slots[i].Original.isEmpty ? null : entry.Slots[i].Original, i);
                }
                entry.RendererBlock.Written = false; foreach (var slot in entry.Slots) slot.Written = false;
            }
        }
        internal void Clear()
        {
            Restore(); entries.Clear(); templates.Clear(); used.Clear(); stale.Clear(); staleTemplates.Clear();
            TemplateBuildCount = TemplateRendererWrites = TemplateFallbackCount = 0;
        }
    }
}
