/*
 * 功能：记录有限Renderer的forceRenderingOff临时写入并按条件还原。
 * 边界：不拥有原对象，不修改enabled、材质、层或碰撞体；冲突写入明确拒绝。
 * 生命周期：同一实例可反复使用；Dispose逆序只恢复仍等于本模块写入值的状态。
 */
using System;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rendering
{
    /// <summary>Restores only its own forceRenderingOff writes that remain unchanged. Never owns a source GO.</summary>
    internal sealed class RendererScope : IDisposable
    {
        struct Write
        {
            public Renderer Renderer;
            public bool Before;
            public bool Written;
        }

        readonly Write[] writes;
        int count;

        public RendererScope(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            writes = new Write[capacity];
        }

        public void Set(Renderer renderer, bool forceOff)
        {
            if (renderer == null) return;
            for (int i = 0; i < count; i++)
            {
                if (writes[i].Renderer != renderer) continue;
                if (writes[i].Written != forceOff)
                    throw new InvalidOperationException("A renderer cannot receive conflicting scope writes.");
                return;
            }
            bool before = renderer.forceRenderingOff;
            if (before == forceOff) return;
            if (count == writes.Length) throw new InvalidOperationException("Finite renderer scope capacity exceeded.");
            writes[count++] = new Write { Renderer = renderer, Before = before, Written = forceOff };
            renderer.forceRenderingOff = forceOff;
        }

        public void Dispose()
        {
            while (count > 0)
            {
                var write = writes[--count];
                writes[count] = default;
                if (write.Renderer != null && write.Renderer.forceRenderingOff == write.Written)
                    write.Renderer.forceRenderingOff = write.Before;
            }
        }
    }
}
