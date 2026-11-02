/* 功能：集中登记一栋建筑的有效范围、全部顶盖、永久结构与房间/门窗。
 * 边界：这是相机呈现作者入口，不是建筑玩法根；门窗连通不会扩大Building身份。
 * 协作：RoomVisibilityModule按主控脚点解析本栋；全栋所有roofGroups消费同一揭顶状态。
 */
using System;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms
{
    [DisallowMultipleComponent]
    public sealed class BuildingVisibilityRoot : MonoBehaviour
    {
        [Header("明确建筑身份与有效内部范围")]
        public string buildingId;
        [Tooltip("相对本Transform的实际内部范围并集；必须明确登记，不按Portal或屋顶包围盒推断。")]
        public RoomLocalBox[] occupancyVolumes = Array.Empty<RoomLocalBox>();
        [Min(0f)] public float enterInset = .02f;
        [Min(0f)] public float exitMargin = .12f;
        public RoomRegion[] regions = Array.Empty<RoomRegion>();
        [Tooltip("本栋全部顶盖统一揭开；房间无独立顶盖开关。")]
        public RoofVisibilityGroup[] roofGroups = Array.Empty<RoofVisibilityGroup>();
        public ViewPortal[] portals = Array.Empty<ViewPortal>();
        [Tooltip("墙、压顶、柱、门框等永久结构；保留真实深度，并拒绝角色局部透视。")]
        public Renderer[] structuralMembers = Array.Empty<Renderer>();

        public bool Contains(Vector3 world, float expansion = 0f)
            => RoomRegion.ContainsBoxes(transform, occupancyVolumes, world, expansion);
        void OnDrawGizmosSelected() => RoomRegion.DrawBoxes(transform, occupancyVolumes, new Color(1f, .7f, .2f, .9f));
    }
}
