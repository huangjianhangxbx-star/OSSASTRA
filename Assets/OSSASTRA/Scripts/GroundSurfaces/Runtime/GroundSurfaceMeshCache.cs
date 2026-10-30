/*
 * 脚本：GroundSurfaceMeshCache
 * 功能：保存本平台派生渲染块、Collider网格与生成统计，保留逐三角形来源反查。
 * 边界：不是作者格表；所有格子与手工矩阵仍属于原生Tilemap。无Update或自动重建。
 * 协作：Editor构建服务原子替换这些引用，Binding管理原生/缓存显示的生命周期。
 */
using System;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundSurfaces
{
    [Serializable]
    public sealed class GroundSurfaceRenderChunk
    {
        public MeshFilter filter;
        public MeshRenderer renderer;
        public int layerIndex;
        public Vector2Int chunk;
        public Tilemap sourceTilemap;
        public Vector3Int[] sourceCells = Array.Empty<Vector3Int>();
        public Sprite[] sourceSprites = Array.Empty<Sprite>();
    }
    [DisallowMultipleComponent]
    public sealed class GroundSurfaceMeshCache : MonoBehaviour
    {
        public GroundSurfaceBinding binding;
        public GroundSurfaceRenderChunk[] chunks = Array.Empty<GroundSurfaceRenderChunk>();
        public Mesh colliderMesh;
        public string signature;
        public int rebuildCount;
        public double lastBuildMilliseconds;
        public int outputTriangles;
        public int outputVertices;
        public int inputCells;
    }
}
