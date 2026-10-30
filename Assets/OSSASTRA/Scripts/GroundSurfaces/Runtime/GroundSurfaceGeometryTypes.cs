/* 脚本：GroundSurfaceGeometryTypes
 * 功能：定义侧壁快照、平台派生域及三角裁剪的纯数据契约。
 * 边界：不读取资产、不持有Tilemap作者格、不创建Mesh或碰撞；输入坐标统一到表面局部XZ。
 */
using System;
using UnityEngine;

namespace Ossastra.GroundSurfaces
{
    [Serializable]
    public sealed class GeometryOptions
    {
        public float weldTolerance=1e-4f;
        public float planeTolerance=5e-4f;
        public double areaTolerance=1e-9;
        internal GeometryOptions Copy()=>new GeometryOptions{weldTolerance=weldTolerance,planeTolerance=planeTolerance,areaTolerance=areaTolerance};
    }

    [Serializable]
    public sealed class MeshSnapshot
    {
        public Vector3[] vertices=Array.Empty<Vector3>();
        public int[] triangles=Array.Empty<int>();
        public Matrix4x4 meshToSurface=Matrix4x4.identity;
        public string sourceId="";
        public int submesh;
    }

    [Serializable]
    public sealed class GeometryIssue
    {
        public string code="",message="",sourceId="";
        public int elementIndex=-1;
        public GeometryIssue(){}
        public GeometryIssue(string code,string message,string sourceId="",int elementIndex=-1)
        {this.code=code;this.message=message;this.sourceId=sourceId;this.elementIndex=elementIndex;}
    }

    /// <summary>验证后的单一简单外环；triangles对应ring，映射到XZ时法线朝+Y。</summary>
    public sealed class SurfaceDomain
    {
        public Vector2[] ring=Array.Empty<Vector2>();
        public int[] triangles=Array.Empty<int>();
        public Rect bounds;
        public double area;
        internal GeometryOptions tolerances=new GeometryOptions();
        internal Rect[] triangleBounds=Array.Empty<Rect>();
        internal bool validated;
    }

    [Serializable]
    public struct ClipVertex
    {
        public Vector3 position;
        public Vector2 uv;
        public Color color;
        public ClipVertex(Vector3 position,Vector2 uv,Color color)
        {this.position=position;this.uv=uv;this.color=color;}
    }
}
