/* 脚本：MapResearchSpineDepth
 * 功能：仅在研究样地为本物体Spine身体配置片层深度，并在正式生成网格后修正负Z间距的bounds。
 * 边界：不推进骨架/动画/相机，不修改Shader或第三方代码，不创建Mesh/Material；只处理已生成的本实例网格。
 * 协作：外层显式装配本GO的SkeletonRenderer；启用保存原间距并订阅官方事件，禁用精确退订并条件恢复自身写入。
 */
using Spine.Unity;
using UnityEngine;

namespace Ossastra.MapResearchIntegration.Rendering
{
    [DisallowMultipleComponent]
    public sealed class MapResearchSpineDepth : MonoBehaviour
    {
        [Tooltip("必须显式引用本物体的SkeletonRenderer；不扫描/修改其它角色或装备。")]
        public SkeletonRenderer sourceRenderer;
        [Tooltip("研究场片层间距；使用已验证快照候选，真实Play效果仍须回归。")]
        public float layerSpacing = -.0001f;

        private SkeletonRenderer boundRenderer;
        private MeshFilter boundFilter;
        private float previousSpacing, appliedSpacing;
        private bool changedSpacing;
        public bool IsBound => boundRenderer != null;
        public string Status { get; private set; } = "未绑定";

        private void OnEnable()
        {
            Release();
            if (sourceRenderer == null || sourceRenderer.gameObject != gameObject ||
                float.IsNaN(layerSpacing) || float.IsInfinity(layerSpacing))
            { Status = "需要本GO显式Renderer引用与有限间距"; return; }
            var filter = GetComponent<MeshFilter>();
            if (filter == null) { Status = "本GO缺少MeshFilter，未修改间距"; return; }
            boundRenderer = sourceRenderer;
            boundFilter = filter;
            previousSpacing = boundRenderer.zSpacing;
            appliedSpacing = layerSpacing;
            changedSpacing = previousSpacing != appliedSpacing;
            boundRenderer.zSpacing = appliedSpacing;
            boundRenderer.OnMeshAndMaterialsUpdated += MeshReady;
            Status = "已绑定研究深度与正式网格bounds修正";
        }

        private void MeshReady(ISkeletonRenderer renderer)
        {
            if (!ReferenceEquals(renderer, boundRenderer) || boundFilter == null) return;
            RepairMeshBounds(boundFilter.sharedMesh);
        }

        public static void RepairMeshBounds(Mesh mesh)
        {
            if (mesh == null) return;
            // 本机Spine会把未引用缓冲尾部重复为末顶点；按真实顶点重算可修正负extents，不引入任意放大盒。
            mesh.RecalculateBounds();
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        private void Release()
        {
            if (boundRenderer != null)
            {
                boundRenderer.OnMeshAndMaterialsUpdated -= MeshReady;
                // 只在仍保持自身写入值时恢复；若别的写者已改成不同值，不覆盖它。
                if (changedSpacing && boundRenderer.zSpacing == appliedSpacing) boundRenderer.zSpacing = previousSpacing;
            }
            boundRenderer = null;
            boundFilter = null;
            changedSpacing = false;
            Status = "未绑定";
        }
    }
}
