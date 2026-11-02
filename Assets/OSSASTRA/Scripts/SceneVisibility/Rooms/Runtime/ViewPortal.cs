/* 功能：把真实门窗孔洞、开合状态及内外房间关系登记给许可遮罩。
 * 边界：可观察、可通行是独立作者信息；不修改物理/攻击/拾取，也不自动解锁未知内室。
 * 协作：transform.forward为inside到outside的方向；Module只提交已到达区域朝室外的开放孔洞。
 */
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms
{
    [DisallowMultipleComponent]
    public sealed class ViewPortal : MonoBehaviour
    {
        public string portalId;
        public RoomRegion inside;
        [Tooltip("null表示室外。内部门只登记结构关系，本轮不借它解锁未知房间。")]
        public RoomRegion outside;
        [Tooltip("本地XY平面上的真实开口网格，与实体墙洞吻合；不渲染假窗贴图。")]
        public Mesh apertureMesh;
        public bool open = true;
        public bool canSeeThrough = true;
        public bool canTraverse;
        Mesh cachedMesh;
        Vector3[] cachedVertices;
        int[] cachedTriangles;
        public Vector3 OutwardNormal => transform.forward.normalized;

        /// <summary>世界标签仅作开口几何粗筛；缓存作者三角形，不逐标签读取Mesh数组。</summary>
        public bool ContainsWorldPoint(Vector3 world)
        {
            if (apertureMesh == null || !apertureMesh.isReadable) return false;
            if (cachedMesh != apertureMesh)
            {
                cachedMesh = apertureMesh;
                cachedVertices = cachedMesh.vertices; cachedTriangles = cachedMesh.triangles;
            }
            Vector3 local = transform.InverseTransformPoint(world);
            if (Mathf.Abs(local.z) > .02f) return false;
            Vector2 p = new Vector2(local.x, local.y);
            for (int i = 0; i + 2 < cachedTriangles.Length; i += 3)
            {
                Vector2 a = cachedVertices[cachedTriangles[i]], b = cachedVertices[cachedTriangles[i + 1]], c = cachedVertices[cachedTriangles[i + 2]];
                Vector2 u = b - a, v = c - a, w = p - a;
                float denominator = u.x * v.y - u.y * v.x;
                if (Mathf.Abs(denominator) < 1e-8f) continue;
                float s = (w.x * v.y - w.y * v.x) / denominator;
                float t = (u.x * w.y - u.y * w.x) / denominator;
                if (s >= -.0001f && t >= -.0001f && s + t <= 1.0001f) return true;
            }
            return false;
        }
        void OnDrawGizmosSelected()
        {
            Color old = Gizmos.color; Matrix4x4 before = Gizmos.matrix;
            Gizmos.color = open && canSeeThrough ? Color.green : Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            if (apertureMesh != null) Gizmos.DrawWireMesh(apertureMesh);
            Gizmos.matrix = before;
            Gizmos.DrawLine(transform.position, transform.position + OutwardNormal);
            Gizmos.color = old;
        }
    }
}
