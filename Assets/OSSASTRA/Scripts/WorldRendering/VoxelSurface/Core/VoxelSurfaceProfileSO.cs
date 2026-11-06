/* 功能：可保存/复用的体素表面响应配置；不保存运行材质或比较模式。
 * 生命周期：比较组件只读取此配置，参数覆盖只进入私有材质副本。
 */
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [CreateAssetMenu(menuName = "OSSASTRA/渲染/体素表面配置", fileName = "VoxelSurfaceProfile")]
    public sealed class VoxelSurfaceProfileSO : ScriptableObject
    {
        [Tooltip("只描述体素表面响应；不会修改环境、灯光、角色或业务参数。")]
        public VoxelSurfaceSettings settings = new VoxelSurfaceSettings();

        void OnValidate()
        {
            if (settings == null) settings = new VoxelSurfaceSettings();
            settings.Sanitize();
        }
    }
}
