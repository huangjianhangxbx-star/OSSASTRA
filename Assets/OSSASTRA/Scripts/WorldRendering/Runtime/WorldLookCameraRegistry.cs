/* 功能：按明确 owner 与 Camera 发布环境 Look 快照，供对应相机 Feature 消费。
 * 边界：不查场景、角色、材质或旧预览控制器；一个相机不允许两个控制器互相覆盖。
 * 协作与生命周期：控制器发布和释放，Feature 监听 Released 精确归还该相机资源；保留被销毁相机的托管身份用于清理。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.WorldRendering
{
    public static class WorldLookCameraRegistry
    {
        sealed class Entry
        {
            public object owner;
            public Camera camera;
            public WorldLookSettings settings;
        }

        static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        public static event Action<int> Released;

        public static void Publish(object owner, Camera camera, WorldLookSettings settings)
        {
            if (owner == null || camera == null || settings == null)
                throw new ArgumentException("环境 Look 需要明确 owner、相机与快照。");
            int id = camera.GetInstanceID();
            if (entries.TryGetValue(id, out var entry))
            {
                if (!ReferenceEquals(entry.owner, owner) || entry.camera != camera)
                    throw new InvalidOperationException("同一相机已有另一个环境 Look owner。");
                entry.settings = settings;
            }
            else entries.Add(id, new Entry { owner = owner, camera = camera, settings = settings });
        }

        public static bool TryGet(Camera camera, out WorldLookSettings settings)
        {
            settings = null;
            if (camera == null || !entries.TryGetValue(camera.GetInstanceID(), out var entry) || entry.camera != camera) return false;
            settings = entry.settings;
            return settings != null;
        }

        public static void Release(object owner, Camera camera)
        {
            if (owner == null || ReferenceEquals(camera, null)) return;
            int id = camera.GetInstanceID();
            if (!entries.TryGetValue(id, out var entry) || !ReferenceEquals(entry.owner, owner)) return;
            entries.Remove(id);
            Released?.Invoke(id);
        }
    }
}
