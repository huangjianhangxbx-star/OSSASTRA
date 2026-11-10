/* 功能：在正向合法世界完成后传递同一相机、本帧的许可颜色与真实深度附件。
 * 边界：不生产许可、不持有附件所有权、不读取旧相机深度；失效或跨帧一律拒绝。
 * 生命周期：成功绘制后Publish；绘制开始/结束Invalidate只撤销旧输出，真正释放Revoke才发事件。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.Rendering
{
    public static class WorldLookLegalFrameRegistry
    {
        sealed class Entry
        {
            internal Camera camera;
            internal RTHandle permission, depth, characterGlow, styleCoverage;
            internal int frame;
        }
        static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        // 同帧再次绘制前只撤销数据，不释放消费者资源；保留这个待释放身份直到真正Revoke。
        static readonly HashSet<int> invalidated = new HashSet<int>();
        public static event Action<int> Released;
        public static int RegisteredCameraCount => entries.Count;

        public static void Publish(Camera camera, RTHandle permission, RTHandle depth, int frame, RTHandle characterGlow = null, RTHandle styleCoverage = null)
        {
            if (camera == null) return;
            if (!Valid(permission) || !Valid(depth) || frame != Time.frameCount) { Revoke(camera); return; }
            int id = camera.GetInstanceID();
            invalidated.Remove(id);
            if (!entries.TryGetValue(id, out var entry)) { entry = new Entry(); entries.Add(id, entry); }
            entry.camera = camera; entry.permission = permission; entry.depth = depth; entry.frame = frame;
            // Optional借用只接受本次许可同尺寸的可采样附件；MSAA生产者须先完成显式resolve。
            entry.styleCoverage = SampleableStyle(styleCoverage, permission, depth) ? styleCoverage : null;
            entry.characterGlow = Valid(characterGlow) && characterGlow.rt.width == permission.rt.width && characterGlow.rt.height == permission.rt.height ? characterGlow : null;
        }

        public static bool TryGet(Camera camera, int frame, out RTHandle permission, out RTHandle depth)
        {
            permission = null; depth = null;
            if (camera == null || frame != Time.frameCount || !entries.TryGetValue(camera.GetInstanceID(), out var entry) ||
                entry.camera != camera || entry.frame != frame || !Valid(entry.permission) || !Valid(entry.depth)) return false;
            permission = entry.permission; depth = entry.depth; return true;
        }

        public static void Revoke(Camera camera)
        {
            if (ReferenceEquals(camera, null)) return;
            int id = camera.GetInstanceID();
            bool existed = entries.Remove(id);
            existed |= invalidated.Remove(id);
            if (existed) Released?.Invoke(id);
        }

        // 只传同相机当前已完成的颜色贡献，不授予任何新空间许可，也不接管RT所有权。
        public static bool TryGetCharacterGlow(Camera camera, int frame, out RTHandle glow)
        {
            glow = null;
            if (!TryGet(camera, frame, out _, out _) || !entries.TryGetValue(camera.GetInstanceID(), out var entry) || !Valid(entry.characterGlow)) return false;
            glow = entry.characterGlow; return true;
        }

        public static bool TryGetStyleCoverage(Camera camera, int frame, out RTHandle coverage)
        {
            coverage = null;
            if (!TryGet(camera, frame, out var permission, out var depth) || !entries.TryGetValue(camera.GetInstanceID(), out var entry) ||
                !SampleableStyle(entry.styleCoverage, permission, depth)) return false;
            coverage = entry.styleCoverage; return true;
        }

        static bool SampleableStyle(RTHandle style, RTHandle permission, RTHandle depth) => Valid(style) && Valid(permission) && Valid(depth) &&
            style.rt.width == permission.rt.width && style.rt.height == permission.rt.height &&
            style.rt.width == depth.rt.width && style.rt.height == depth.rt.height &&
            style.rt.antiAliasing == permission.rt.antiAliasing && !style.rt.bindTextureMS;

        /// <summary>相机本次绘制开始或结束时使旧输出失效，保留消费者可复用缓冲；不发资源释放事件。</summary>
        public static void Invalidate(Camera camera)
        {
            if (ReferenceEquals(camera, null)) return;
            int id = camera.GetInstanceID();
            if (entries.Remove(id)) invalidated.Add(id);
        }

        static bool Valid(RTHandle handle) => handle != null && handle.rt != null && handle.rt.IsCreated();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            var ids = new HashSet<int>(entries.Keys); ids.UnionWith(invalidated); entries.Clear(); invalidated.Clear();
            foreach (int id in ids) Released?.Invoke(id);
        }
    }
}
