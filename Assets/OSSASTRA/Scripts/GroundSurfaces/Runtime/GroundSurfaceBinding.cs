/*
 * 脚本：GroundSurfaceBinding
 * 功能：把独立模型连接环、唯一Tilemap作者Surface与派生顶盖组织为一块物理平台。
 * 边界：不建格表、不更新角色或相机；只管理显示归属与可选作者域，无Update重建。
 * 协作：Editor服务更新Boundary/缓存；原Brush/装饰通过IGroundSurfaceEditDomain查询真实范围。
 * 生命周期：缓存显示只拥有forceRenderingOff，保留enabled给原配方预览；停用恢复自己的隐藏位。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Ossastra.GroundResearch;
namespace Ossastra.GroundSurfaces
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GroundSurfaceBinding : MonoBehaviour, IGroundSurfaceEditDomain
    {
        public GroundWorkshopMap map;
        public MeshFilter[] sideWalls = Array.Empty<MeshFilter>();
        public GroundSurfaceBoundary boundary;
        public Transform cacheRoot;
        public MeshCollider capCollider;
        [Range(1, 32)] public int chunkSize = 8;
        public bool useCache = true;
        public bool flattenDisplay = true;
        [Min(0f)] public float decorationEdgeInset = .25f;
        [Min(.001f)] public float footpointHeightTolerance = .1f;
        public Mesh[] modelVariants = Array.Empty<Mesh>();
        public int currentModelVariant;
        [TextArea] public string lastError;
        private SurfaceDomain domain;
        private GroundSurfaceBoundary domainSource;
        private int domainRevision = -1;
        private readonly Dictionary<Renderer, bool> previousHidden = new Dictionary<Renderer, bool>();
        public string SurfaceId => map != null && map.registration != null ? map.registration.mapId : "";
        public GroundResearchSurface Surface => map == null ? null : map.surface;
        public float DecorationInset => decorationEdgeInset;

        public SurfaceDomain GetDomain()
        {
            if (boundary == null) throw new InvalidOperationException("平台缺少已校验的模型连接环。");
            if (domain == null || domainSource != boundary || domainRevision != boundary.revision)
            {
                var options = new GeometryOptions { weldTolerance = boundary.weldTolerance, planeTolerance = boundary.planeTolerance };
                if (!GroundSurfaceGeometry.TryBuildDomain(boundary.rim, options, out var next, out var issue))
                    throw new InvalidOperationException("平台连接环失效：" + issue.code + " " + issue.message);
                domain = next; domainSource = boundary; domainRevision = boundary.revision;
            }
            return domain;
        }
        public void InvalidateBoundary() { domain = null; domainSource = null; domainRevision = -1; }

        public bool ContainsWorldPoint(Vector3 position, float inset = 0f)
        {
            var point = transform.InverseTransformPoint(position);
            if (boundary == null || Mathf.Abs(point.y - boundary.localTopY) > footpointHeightTolerance) return false;
            SurfaceDomain current;
            try { current = GetDomain(); } catch (InvalidOperationException) { return false; }
            if (!GroundSurfaceGeometry.ContainsXZ(current, new Vector2(point.x, point.z))) return false;
            if (inset <= 0f) return true;
            // 退让按世界米计算，避免正缩放的平台把配置间距变成另一种单位。
            for (int i = 0; i < current.ring.Length; i++)
            {
                var a = transform.TransformPoint(new Vector3(current.ring[i].x, boundary.localTopY, current.ring[i].y));
                var b = transform.TransformPoint(new Vector3(current.ring[(i + 1) % current.ring.Length].x, boundary.localTopY, current.ring[(i + 1) % current.ring.Length].y));
                a.y = b.y = position.y; var ab = b - a;
                float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / ab.sqrMagnitude) : 0f;
                if (Vector3.Distance(position, a + t * ab) < inset) return false;
            }
            return true;
        }
        public bool IntersectsCell(Tilemap tilemap, Vector3Int cell)
        {
            if (Surface == null || tilemap == null || !Surface.Owns(tilemap)) return false;
            SurfaceDomain current;
            try { current = GetDomain(); } catch (InvalidOperationException) { return false; }
            var a = transform.InverseTransformPoint(tilemap.CellToWorld(cell));
            var b = transform.InverseTransformPoint(tilemap.CellToWorld(cell + Vector3Int.right));
            var c = transform.InverseTransformPoint(tilemap.CellToWorld(cell + Vector3Int.right + Vector3Int.up));
            var d = transform.InverseTransformPoint(tilemap.CellToWorld(cell + Vector3Int.up));
            var vertices = new List<ClipVertex>(); var indices = new List<int>();
            GroundSurfaceGeometry.ClipTriangle(new ClipVertex(a, Vector2.zero, Color.white), new ClipVertex(b, Vector2.zero, Color.white), new ClipVertex(c, Vector2.zero, Color.white), current, vertices, indices);
            if (indices.Count != 0) return true;
            GroundSurfaceGeometry.ClipTriangle(new ClipVertex(a, Vector2.zero, Color.white), new ClipVertex(c, Vector2.zero, Color.white), new ClipVertex(d, Vector2.zero, Color.white), current, vertices, indices);
            return indices.Count != 0;
        }

        public void SetCacheVisible(bool visible)
        {
            if (cacheRoot != null) cacheRoot.gameObject.SetActive(visible);
            if (Surface == null || Surface.layers == null) return;
            foreach (var layer in Surface.layers)
            {
                var renderer = layer == null || layer.tilemap == null ? null : layer.tilemap.GetComponent<TilemapRenderer>();
                if (renderer == null) continue;
                if (visible)
                {
                    if (!previousHidden.ContainsKey(renderer)) previousHidden[renderer] = renderer.forceRenderingOff;
                    renderer.forceRenderingOff = true;
                }
                else if (previousHidden.TryGetValue(renderer, out bool previous))
                {
                    if (renderer.forceRenderingOff) renderer.forceRenderingOff = previous;
                    previousHidden.Remove(renderer);
                }
            }
        }
        private void OnEnable() { if (cacheRoot != null) SetCacheVisible(useCache); }
        private void OnDisable()
        {
            foreach (var item in previousHidden)
                if (item.Key != null && item.Key.forceRenderingOff) item.Key.forceRenderingOff = item.Value;
            previousHidden.Clear();
            if (cacheRoot != null) cacheRoot.gameObject.SetActive(false);
        }
        private void OnValidate() { InvalidateBoundary(); }
    }
}
