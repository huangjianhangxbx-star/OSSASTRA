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
        readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        readonly HashSet<Renderer> used = new HashSet<Renderer>();
        readonly List<Renderer> stale = new List<Renderer>();
        readonly Matrix4x4[] ownerMatrices = new Matrix4x4[8];
        readonly Vector4[] ownerExtents = new Vector4[8];

        internal int BlockCount
        {
            get { int count = 0; foreach (var entry in entries.Values) count += 2 + entry.Slots.Count * 2; return count; }
        }

        internal void Apply(RoomMaskFrame frame)
        {
            Restore(); used.Clear();
            foreach (var surface in frame.PositiveSurfaces)
            {
                var renderer = surface.Renderer;
                if (renderer == null || surface.Kind == RoomPositiveSurfaceKind.Full || !used.Add(renderer)) continue;
                // 手工无光照路线逐draw上传owner，不让原角色/FX写者缓存本模块的临时MPB。
                if (RoomPositiveRenderingLayers.RequiresIndependentDraw(surface) && surface.DrawMask == RoomPositiveRenderingLayers.IndependentBit) continue;
                if (!entries.TryGetValue(renderer, out var entry)) { entry = new Entry(); entries.Add(renderer, entry); }
                renderer.GetPropertyBlock(entry.RendererBlock.Original);
                renderer.GetPropertyBlock(entry.RendererBlock.Working);
                Append(entry.RendererBlock.Working, frame, surface);
                renderer.SetPropertyBlock(entry.RendererBlock.Working); entry.RendererBlock.Written = true;
                // 原角色可能使用逐材质槽的深度参数；槽级PropertyBlock会覆盖Renderer级，必须同样附加。
                int slots = renderer.sharedMaterials.Length;
                while (entry.Slots.Count < slots) entry.Slots.Add(new Slot());
                for (int i = 0; i < slots; ++i)
                {
                    var slot = entry.Slots[i]; renderer.GetPropertyBlock(slot.Original, i);
                    if (slot.Original.isEmpty) continue;
                    renderer.GetPropertyBlock(slot.Working, i); Append(slot.Working, frame, surface);
                    renderer.SetPropertyBlock(slot.Working, i); slot.Written = true;
                }
            }
            stale.Clear(); foreach (var renderer in entries.Keys) if (renderer == null || !used.Contains(renderer)) stale.Add(renderer);
            foreach (var renderer in stale) entries.Remove(renderer);
        }

        void Append(MaterialPropertyBlock block, RoomMaskFrame frame, RoomPositiveSurface surface)
        {
            var volumes = surface.OwnerVolumes;
            int count = volumes == null ? 0 : volumes.Length;
            block.SetInteger("_RoomSightOwnerCount", count);
            block.SetInteger("_RoomSightOwnerExterior", surface.OwnerExterior ? 1 : 0);
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
        internal void Clear() { Restore(); entries.Clear(); }
    }
}
