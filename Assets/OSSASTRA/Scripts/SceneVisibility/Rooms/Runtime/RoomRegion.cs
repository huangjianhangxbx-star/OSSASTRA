/* 功能：登记房间/走廊的稳定身份、真实脚点体积与内容成员。
 * 边界：不拥有屋顶开关、Current、角色运动或存档；体积仅作只读空间判定，不创建物理触发器。
 * 协作：RoomVisibilityModule消费本地定向盒并集；高度独立于水平入退滞回。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.SceneVisibility.Rooms
{
    [Serializable]
    public struct RoomLocalBox
    {
        public Vector3 center;
        public Vector3 size;
    }

    [DisallowMultipleComponent]
    public sealed class RoomRegion : MonoBehaviour
    {
        [Header("稳定房间身份；不控制本房屋顶")]
        public string roomId;
        public string floorId = "Ground";
        public int priority;
        [Tooltip("相对本组件Transform的定向盒并集；Y尺寸就是楼层有效高度。")]
        public RoomLocalBox[] volumes = Array.Empty<RoomLocalBox>();
        [Tooltip("本区域内容和地板；墙/压顶归Building.structuralMembers，屋顶归Building.roofGroups。成员不可跨未知/已知房间混用。")]
        public Renderer[] members = Array.Empty<Renderer>();
        [Min(0f), Tooltip("进入只收缩水平门槛，不能把贴地脚点从Y底边排除。")]
        public float enterInset = .02f;
        [Min(0f)] public float exitMargin = .12f;

        public bool Contains(Vector3 world, float expansion = 0f)
            => ContainsBoxes(transform, volumes, world, expansion);

        public void AppendMaskVolumes(List<RoomMaskVolume> destination)
            => AppendBoxes(transform, volumes, destination);

        internal static bool ContainsBoxes(Transform basis, RoomLocalBox[] boxes, Vector3 world, float expansion)
        {
            if (basis == null || boxes == null || !Finite(world) || !Finite(expansion)) return false;
            Vector3 local = basis.InverseTransformPoint(world);
            Vector3 scale = basis.lossyScale;
            float xMargin = expansion / Mathf.Max(.0001f, Mathf.Abs(scale.x));
            float zMargin = expansion / Mathf.Max(.0001f, Mathf.Abs(scale.z));
            foreach (var box in boxes)
            {
                Vector3 half = box.size * .5f;
                Vector3 p = local - box.center;
                if (half.x + xMargin < 0f || half.z + zMargin < 0f) continue;
                // Y仅检查真实高度；否则enterInset会把始终贴地的脚点永久挡在室外。
                if (Mathf.Abs(p.x) <= half.x + xMargin && Mathf.Abs(p.z) <= half.z + zMargin &&
                    Mathf.Abs(p.y) <= half.y + .001f) return true;
            }
            return false;
        }

        internal static void AppendBoxes(Transform basis, RoomLocalBox[] boxes, List<RoomMaskVolume> destination)
        {
            if (basis == null || boxes == null) return;
            foreach (var box in boxes) destination.Add(new RoomMaskVolume
            {
                WorldToLocal = Matrix4x4.Translate(-box.center) * basis.worldToLocalMatrix,
                HalfExtents = box.size * .5f
            });
        }

        internal static bool ValidateBoxes(Transform basis, RoomLocalBox[] boxes, out string reason)
        {
            reason = null;
            if (basis == null || boxes == null || boxes.Length == 0) reason = "缺少明确体积";
            else if (!Finite(basis.lossyScale) || Mathf.Abs(basis.lossyScale.x) < .0001f ||
                Mathf.Abs(basis.lossyScale.y) < .0001f || Mathf.Abs(basis.lossyScale.z) < .0001f) reason = "体积Transform缩放无效";
            else foreach (var box in boxes)
                if (!Finite(box.center) || !Finite(box.size) || box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f)
                { reason = "体积中心/尺寸无效"; break; }
            return reason == null;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        internal static void DrawBoxes(Transform basis, RoomLocalBox[] boxes, Color color)
        {
            if (basis == null || boxes == null) return;
            Matrix4x4 before = Gizmos.matrix; Color old = Gizmos.color;
            Gizmos.matrix = basis.localToWorldMatrix; Gizmos.color = color;
            foreach (var box in boxes) Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = before; Gizmos.color = old;
        }
        void OnDrawGizmosSelected() => DrawBoxes(transform, volumes, new Color(.2f, .9f, .9f, .8f));
    }
}
