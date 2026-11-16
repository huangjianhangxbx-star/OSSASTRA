/* 脚本：SideViewSliceAnchor；一个稳定保存点对应双角色的安全世界脚点。 */
using UnityEngine;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SideViewSliceAnchor : MonoBehaviour
    {
        public string anchorId;
        public Vector3 firstActorPosition;
        public Vector3 secondActorPosition;
        public int facingX = 1;
    }
}
