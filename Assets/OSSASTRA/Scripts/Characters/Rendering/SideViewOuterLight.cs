using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.Characters.Rendering
{
    internal sealed class SideViewOuterLight : IDisposable
    {
        private readonly MeshRenderer renderer;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] corners = new Vector3[4];

        public SideViewOuterLight(Transform actor, Shader shader)
        {
            var child = new GameObject("Character outer light") { hideFlags = HideFlags.HideAndDontSave, layer = actor.gameObject.layer };
            child.transform.SetParent(actor, false);
            mesh = new Mesh { name = "Side view silhouette extents", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.vertices = corners;
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = short.MinValue;
            renderer.forceRenderingOff = true;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            renderer.sharedMaterial = material;
        }

        public void Present(Camera camera, Ossastra.CharacterLighting.Rendering.CharacterCoverageBinding coverage, MaterialPropertyBlock properties)
        {
            var rect = coverage.ScreenRect;
            var size = coverage.ScreenSize;
            float pad = Mathf.Max(2, 4 * size.y / 1080f) + 1;
            var minimum = new Vector2(rect.x - pad * size.z, 1 - (rect.y + rect.w + pad * size.w));
            var maximum = new Vector2(rect.x + rect.z + pad * size.z, 1 - (rect.y - pad * size.w));
            float depth = camera.WorldToViewportPoint(renderer.transform.position).z;
            var transform = renderer.transform;
            corners[0] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(minimum.x, minimum.y, depth)));
            corners[1] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(maximum.x, minimum.y, depth)));
            corners[2] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(minimum.x, maximum.y, depth)));
            corners[3] = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(maximum.x, maximum.y, depth)));
            mesh.vertices = corners;
            mesh.RecalculateBounds();
            renderer.SetPropertyBlock(properties);
            renderer.forceRenderingOff = false;
        }

        public void Hide() { if (renderer != null) renderer.forceRenderingOff = true; }

        public void Dispose()
        {
            Hide();
            if (renderer != null) CoreUtils.Destroy(renderer.gameObject);
            CoreUtils.Destroy(mesh);
            CoreUtils.Destroy(material);
        }
    }
}
