/* 功能：登记属于一栋建筑的可揭顶视觉成员。
 * 边界：本组件不判断房间或改对象启停；顶盖的碰撞、业务与光照独立保留。
 * 协作：唯一RoomVisibilityModule在同一相机作用域内统一提交本栋所有组，并在End准确归还；标签射线仅排除明确登记的顶盖Collider。
 */
using System;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoofVisibilityGroup : MonoBehaviour
    {
        public string roofGroupId;
        [Tooltip("只登记屋顶/遮盖天花板；禁止混入墙顶、地板、台阶或承载面。原ShadowCaster在揭顶时保留。")]
        public Renderer[] members = Array.Empty<Renderer>();
        [Tooltip("仅供已揭顶时的世界标签可见性射线忽略这些原屋顶Collider；不修改Collider启用、物理碰撞或攻击LOS。禁止登记墙/未知内容Collider。")]
        public Collider[] labelRaycastExclusions = Array.Empty<Collider>();
    }
}
