using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.VoxelTerrain
{
    /// <summary>只读接收原生 greedy quads；调用者负责创建 Mesh 资产与碰撞器。</summary>
    public static class VoxelTerrainGeometry
    {
        public static VoxelTerrainMeshData[] Build(VoxelTerrainSource source, bool linearVertexColors = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new List<VoxelTerrainMeshData>();
            foreach (var group in source.Quads.GroupBy(q => q.Chunk).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                var uv = new List<Vector2>(); var colors = new List<Color>();
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                foreach (var quad in group)
                {
                    int start = vertices.Count;
                    var normal = quad.Axis == 0 ? new Vector3(quad.Sign, 0, 0) : quad.Axis == 1 ? new Vector3(0, 0, quad.Sign) : new Vector3(0, quad.Sign, 0);
                    Color color = linearVertexColors ? source.Palette[quad.Color].linear : source.Palette[quad.Color];
                    var points = new[] { quad.PositionAt(quad.A, quad.B), quad.PositionAt(quad.A + quad.Width, quad.B), quad.PositionAt(quad.A + quad.Width, quad.B + quad.Height), quad.PositionAt(quad.A, quad.B + quad.Height) };
                    foreach (var point in points)
                    {
                        Vector3 world = point * source.VoxelSize;
                        vertices.Add(world); normals.Add(normal); uv.Add(new Vector2(world.x / 16f, world.z / 16f)); colors.Add(color);
                    }
                    // x/z/y 的基底交换反转手性，按实际外法线修正绕序。
                    bool reverse = Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0;
                    int[] order = reverse ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                    foreach (int index in order) triangles[source.MaterialRoles[quad.Color]].Add(start + index);
                }
                result.Add(new VoxelTerrainMeshData(group.Key, vertices.ToArray(), normals.ToArray(), uv.ToArray(), colors.ToArray(), triangles.Select(t => t.ToArray()).ToArray()));
            }
            return result.ToArray();
        }
    }

    /// <summary>单个原生 16³ chunk 的 Mesh 数据；grass / soil / basin 三槽始终存在。</summary>
    public sealed class VoxelTerrainMeshData
    {
        public string ChunkKey { get; }
        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uv { get; }
        public Color[] Colors { get; }
        public int[][] SubmeshTriangles { get; }
        internal VoxelTerrainMeshData(string key, Vector3[] vertices, Vector3[] normals, Vector2[] uv, Color[] colors, int[][] triangles)
        {
            ChunkKey = key; Vertices = vertices; Normals = normals; Uv = uv; Colors = colors; SubmeshTriangles = triangles;
        }
    }
}
