/* 功能：把真实门窗孔洞、开合状态及内外房间关系登记给许可遮罩。
 * 边界：可观察、可通行是独立作者信息；不修改物理/攻击/拾取，也不自动解锁未知内室。
 * 协作：transform.forward为inside到outside的方向；原主控观察点产生单跳双向局部视域。
 */
using System;
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
        [Min(.1f), Tooltip("仅有限策略：观察点到孔洞最大距离；玩家自然视野与相机窗景不使用此限制。")]
        public float observerRange=6f;
        [Min(.1f), Tooltip("仅有限策略：从孔洞法线延伸上限；玩家自然视野与相机窗景不使用此限制。")]
        public float maxSightDistance=8f;

        public Vector3[] GetWorldAperture()
        {
            if(apertureMesh==null||!apertureMesh.isReadable)return Array.Empty<Vector3>();
            EnsureCache();
            if(cachedBoundary==null)cachedBoundary=BuildBoundary(cachedVertices,cachedTriangles);
            var world=new Vector3[cachedBoundary.Length];for(int i=0;i<world.Length;i++)world[i]=transform.TransformPoint(cachedBoundary[i]);return world;
        }
        Mesh cachedMesh;
        Vector3[] cachedVertices;
        int[] cachedTriangles;
        Vector3[] cachedBoundary;
        public Vector3 OutwardNormal => transform.forward.normalized;

        void EnsureCache()
        {
            if(cachedMesh==apertureMesh)return;
            cachedMesh=apertureMesh;cachedVertices=cachedMesh.vertices;cachedTriangles=cachedMesh.triangles;cachedBoundary=null;
        }

        static Vector3[] BuildBoundary(Vector3[] vertices,int[] triangles)
        {
            // 只从真实三角形的边界取环；不把未填充区域补成凸包，也不把中心细分点当窗边。
            var unique=new System.Collections.Generic.List<Vector3>();var map=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){if(Mathf.Abs(vertices[i].z)>.001f)return Array.Empty<Vector3>();int index=unique.FindIndex(v=>(v-vertices[i]).sqrMagnitude<.0000001f);if(index<0){index=unique.Count;unique.Add(vertices[i]);}map[i]=index;}
            var edges=new System.Collections.Generic.Dictionary<long,int>();
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                int a=map[triangles[i]],b=map[triangles[i+1]],c=map[triangles[i+2]];
                if(a==b||b==c||c==a||Vector3.Cross(unique[b]-unique[a],unique[c]-unique[a]).sqrMagnitude<1e-12f)return Array.Empty<Vector3>();
                int[] ids={a,b,c};for(int e=0;e<3;e++){int u=ids[e],v=ids[(e+1)%3];long key=((long)Mathf.Min(u,v)<<32)|(uint)Mathf.Max(u,v);edges.TryGetValue(key,out int count);if(count>=2)return Array.Empty<Vector3>();edges[key]=count+1;}
            }
            var adjacent=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<int>>();int boundaryEdges=0;
            foreach(var pair in edges)if(pair.Value==1){int a=(int)(pair.Key>>32),b=(int)(pair.Key&uint.MaxValue);if(!adjacent.TryGetValue(a,out var aa)){aa=new System.Collections.Generic.List<int>(2);adjacent.Add(a,aa);}if(!adjacent.TryGetValue(b,out var bb)){bb=new System.Collections.Generic.List<int>(2);adjacent.Add(b,bb);}aa.Add(b);bb.Add(a);++boundaryEdges;}
            if(boundaryEdges<3||boundaryEdges>7)return Array.Empty<Vector3>();foreach(var pair in adjacent)if(pair.Value.Count!=2)return Array.Empty<Vector3>();
            int first=-1;foreach(int id in adjacent.Keys)if(first<0||id<first)first=id;
            int current=first,previous=-1;var result=new System.Collections.Generic.List<Vector3>(boundaryEdges);var visited=new System.Collections.Generic.HashSet<int>();
            do{if(!visited.Add(current))return Array.Empty<Vector3>();result.Add(unique[current]);var neighbours=adjacent[current];int next=neighbours[0]==previous?neighbours[1]:neighbours[0];previous=current;current=next;}while(current!=first&&result.Count<=boundaryEdges);
            return current==first&&result.Count==boundaryEdges?result.ToArray():Array.Empty<Vector3>();
        }

        /// <summary>世界标签仅作开口几何粗筛；缓存作者三角形，不逐标签读取Mesh数组。</summary>
        public bool ContainsWorldPoint(Vector3 world)
        {
            if (apertureMesh == null || !apertureMesh.isReadable) return false;
            EnsureCache();
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
