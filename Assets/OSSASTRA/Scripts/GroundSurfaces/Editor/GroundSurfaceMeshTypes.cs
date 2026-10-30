/*
 * 脚本：GroundSurfaceMeshTypes
 * 功能：声明薄顶面生成器的真实Tilemap输入、按层块纹理输出与有界构建统计。
 * 边界：不保存作者格、不创建资产或材质；每个输出三角形保留对应cell/Sprite，支持实际片段反查。
 * 协作：root桥提供有效预览输入并负责Mesh持久化、Renderer替换和资源生命周期。
 */
#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundSurfaces.Editor
{
    [Serializable]
    public sealed class SurfaceLayerInput
    {
        public Tilemap tilemap;
        public Material material;
        public int layerIndex;
        public SurfaceLayerInput() { }
        public SurfaceLayerInput(Tilemap tilemap, Material material, int layerIndex)
        { this.tilemap=tilemap;this.material=material;this.layerIndex=layerIndex; }
    }

    public sealed class SurfaceMeshChunk
    {
        public Mesh mesh;
        public Texture mainTexture;
        public Material sourceMaterial;
        public int layerIndex;
        public Vector2Int chunk;
        public Tilemap sourceTilemap;
        // 数组与mesh的三角形一一对应，而非仅存去重列表；裁出来的多个片段仍可定位同一个原生作者格。
        public Vector3Int[] cells=Array.Empty<Vector3Int>();
        public Sprite[] sprites=Array.Empty<Sprite>();
    }

    public sealed class SurfaceMeshBuildResult
    {
        public SurfaceMeshChunk[] chunks=Array.Empty<SurfaceMeshChunk>();
        public int inputCells,inputTriangles,outputVertices,outputTriangles;
        public int whollyInsideTriangles,clippedTriangles,discardedTriangles;
        public int materialTextureGroups;
        public double elapsedMilliseconds;
    }
}
#endif
