using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.SideView.TerrainLab
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class NgTerrainPatch : MonoBehaviour
    {
        [Serializable]
        public struct Quad
        {
            public Rect bounds, uv;
            [SerializeField] private bool transformed;
            [SerializeField] private Vector3 lowerLeft, upperLeft, upperRight, lowerRight;
            [SerializeField] private bool flipX, flipY;

            public Quad(Rect bounds, Rect uv) : this() => (this.bounds, this.uv) = (bounds, uv);

            public Quad(Vector3 leftBottom, Vector3 leftTop, Vector3 rightTop, Vector3 rightBottom,
                bool flipX, bool flipY) : this()
            {
                transformed = true;
                lowerLeft = leftBottom;
                upperLeft = leftTop;
                upperRight = rightTop;
                lowerRight = rightBottom;
                this.flipX = flipX;
                this.flipY = flipY;
                uv = new Rect(0f, 0f, 1f, 1f);
            }

            internal void Append(List<Vector3> vertices, List<Vector2> coordinates)
            {
                vertices.Add(transformed ? lowerLeft : new Vector3(bounds.xMin, bounds.yMin));
                vertices.Add(transformed ? upperLeft : new Vector3(bounds.xMin, bounds.yMax));
                vertices.Add(transformed ? upperRight : new Vector3(bounds.xMax, bounds.yMax));
                vertices.Add(transformed ? lowerRight : new Vector3(bounds.xMax, bounds.yMin));
                float west = flipX ? uv.xMax : uv.xMin, east = flipX ? uv.xMin : uv.xMax;
                float south = flipY ? uv.yMax : uv.yMin, north = flipY ? uv.yMin : uv.yMax;
                coordinates.Add(new Vector2(west, south));
                coordinates.Add(new Vector2(west, north));
                coordinates.Add(new Vector2(east, north));
                coordinates.Add(new Vector2(east, south));
            }
        }

        [SerializeField] private Quad[] quads = Array.Empty<Quad>();
        private Mesh generated;

        public void SetQuads(Quad[] value)
        {
            quads = value ?? throw new ArgumentNullException(nameof(value));
            if (isActiveAndEnabled) Rebuild();
        }

        private void OnEnable() => Rebuild();
        private void OnDisable() => Release();

        private void Rebuild()
        {
            Release();
            if (quads.Length == 0) return;
            var vertices = new List<Vector3>(quads.Length * 4);
            var uv = new List<Vector2>(quads.Length * 4);
            var triangles = new List<int>(quads.Length * 6);
            var colors = new List<Color32>(quads.Length * 4);
            foreach (var quad in quads)
            {
                int index = vertices.Count;
                quad.Append(vertices, uv);
                for (int i = 0; i < 4; i++) colors.Add(new Color32(255, 255, 255, 255));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            generated = new Mesh { name = "Terrain Lab Visible Cards", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            generated.SetVertices(vertices);
            generated.SetUVs(0, uv);
            generated.SetColors(colors);
            generated.SetTriangles(triangles, 0);
            GetComponent<MeshFilter>().sharedMesh = generated;
        }

        private void Release()
        {
            if (generated == null) return;
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == generated) filter.sharedMesh = null;
            if (Application.IsPlaying(gameObject)) Destroy(generated);
            else DestroyImmediate(generated);
            generated = null;
        }
    }
}
