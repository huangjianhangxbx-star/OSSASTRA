/* 功能：在单个相机作用域内撤销顶盖/未知内容的颜色与遮挡深度，并保留原投影状态。
 * 边界：不设置Renderer.enabled、不停用GameObject、不改材质/物理/灯；不接管外部生灭。
 * 生命周期：逆序只恢复仍等于本scope写入值的属性；重入先归还，不会复活已禁用/破坏对象。
 */
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.SceneVisibility.Rooms
{
    internal sealed class RoomRendererScope : IDisposable
    {
        struct Write
        {
            public Renderer Renderer;
            public bool ForceOff;
            public ShadowCastingMode ShadowBefore;
        }
        readonly Write[] writes = new Write[512];
        int count;
        public int Count => count;
        public void Suppress(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) return;
            for (int i = 0; i < count; ++i) if (writes[i].Renderer == renderer) return;
            var mode = renderer.shadowCastingMode;
            if (mode == ShadowCastingMode.ShadowsOnly) return;
            if (count == writes.Length) throw new InvalidOperationException("房间相机显示scope超过512成员预算");
            bool forceOff = mode == ShadowCastingMode.Off;
            writes[count++] = new Write { Renderer = renderer, ForceOff = forceOff, ShadowBefore = mode };
            // 原来不投影的成员不增添新阴影；原来投影的成员只退出颜色/可见深度，仍作为Caster。
            if (forceOff) renderer.forceRenderingOff = true;
            else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        public void Dispose()
        {
            while (count > 0)
            {
                var entry = writes[--count]; writes[count] = default;
                if (entry.Renderer == null) continue;
                if (entry.ForceOff)
                {
                    if (entry.Renderer.forceRenderingOff) entry.Renderer.forceRenderingOff = false;
                }
                else if (entry.Renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                    entry.Renderer.shadowCastingMode = entry.ShadowBefore;
            }
        }
    }
}
