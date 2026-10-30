/* 脚本：GroundSurfaceLabView
 * 功能：本样板的可恢复显示观察：裸缝、线框、拆分副本与原生未裁剪对照。
 * 边界：不改作者格、源Mesh、真实TRS、Collider、人物、相机或TimeScale。
 * 协作：Binding保管显示归属；Editor Window按ObservationRevision只释放自己发起的一轮观察。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundSurfaces
{
    public enum GroundSurfaceLabViewMode { Final, BareSeam, Structure, Exploded, BeforeClip }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GroundSurfaceLabView : MonoBehaviour
    {
        public GroundSurfaceBinding[] surfaces = Array.Empty<GroundSurfaceBinding>();
        public Transform[] decorationRoots = Array.Empty<Transform>();
        public Camera gameplayCamera;
        public GroundSurfaceLabViewMode Mode { get; private set; }
        public string LastMessage { get; private set; }
        public int ObservationRevision { get; private set; }
        public bool IsObserving => Mode != GroundSurfaceLabViewMode.Final;
        public int DiagnosticRendererCount => temporaryRenderers == null ? 0 : temporaryRenderers.Count(r => r != null);

        private sealed class ForceState
        { public bool original, written, hadNativeOwner; public GroundSurfaceBinding nativeOwner; }
        [NonSerialized] private Dictionary<Renderer, ForceState> states;
        [NonSerialized] private List<MeshRenderer> temporaryRenderers;
        [NonSerialized] private List<UnityEngine.Object> resources;
        [NonSerialized] private GameObject temporaryRoot;
        [NonSerialized] private int diagnosticVertices;
        private const int MaximumDiagnosticVertices = 600000;

        public bool SetMode(GroundSurfaceLabViewMode mode)
        {
            Restore();
            if (mode == GroundSurfaceLabViewMode.Final) { LastMessage = "正常显示；本工具观察已释放。"; return true; }
            try
            {
                EnsureBuffers();
                if (!Enum.IsDefined(typeof(GroundSurfaceLabViewMode), mode)) throw new InvalidOperationException("未知观察模式。");
                ValidateInputs();
                if (mode == GroundSurfaceLabViewMode.BareSeam)
                    HideDecorations();
                else
                {
                    foreach (var binding in surfaces)
                    {
                        var cache = RequireCache(binding);
                        if (mode == GroundSurfaceLabViewMode.BeforeClip)
                        {
                            foreach (var chunk in cache.chunks) if (chunk != null) Force(chunk.renderer, true);
                            foreach (var renderer in NativeRenderers(binding)) Force(renderer, false, binding);
                        }
                        else if (mode == GroundSurfaceLabViewMode.Exploded)
                        {
                            HideDecorations();
                            foreach (var chunk in cache.chunks)
                            {
                                if (chunk == null || chunk.filter == null || chunk.renderer == null) continue;
                                var mesh = CopyWorldMesh(chunk.filter.sharedMesh, chunk.filter.transform.localToWorldMatrix, Vector3.up * 1.2f);
                                var output = AddRenderer("顶盖拆分_仅显示", mesh, chunk.renderer.sharedMaterials, chunk.renderer.gameObject.layer);
                                output.sortingLayerID = chunk.renderer.sortingLayerID; output.sortingOrder = chunk.renderer.sortingOrder;
                                var block = new MaterialPropertyBlock(); chunk.renderer.GetPropertyBlock(block); output.SetPropertyBlock(block);
                                Force(chunk.renderer, true);
                            }
                        }
                        else if (mode == GroundSurfaceLabViewMode.Structure)
                        {
                            HideDecorations();
                            foreach (var renderer in NativeRenderers(binding)) Force(renderer, true, binding);
                            foreach (var chunk in cache.chunks)
                            {
                                if (chunk == null || chunk.filter == null) continue;
                                Lines("顶面线框", chunk.filter.sharedMesh, chunk.filter.transform.localToWorldMatrix, new Color(.15f,.9f,1f,1));
                                Force(chunk.renderer, true);
                            }
                            foreach (var wall in binding.sideWalls ?? Array.Empty<MeshFilter>())
                            {
                                if (wall == null) continue;
                                Lines("侧壁线框", wall.sharedMesh, wall.transform.localToWorldMatrix, new Color(1f,.72f,.22f,1));
                                Force(wall.GetComponent<Renderer>(), true);
                            }
                            Rim(binding);
                            if (binding.capCollider != null && binding.capCollider.sharedMesh != null)
                                Lines("站立面Collider", binding.capCollider.sharedMesh, binding.capCollider.transform.localToWorldMatrix, new Color(1f,.3f,.8f,1));
                        }
                    }
                }
                Mode = mode; ObservationRevision++;
                LastMessage = mode == GroundSurfaceLabViewMode.BareSeam ? "裸接缝：仅隐藏明确的装饰显示，墙和顶盖保持真实位置。" :
                    mode == GroundSurfaceLabViewMode.Structure ? "结构：青色顶面、黄色侧壁、绿色连接环、粉色站立Collider；均为临时线框。" :
                    mode == GroundSurfaceLabViewMode.Exploded ? "拆开看：顶盖显示副本上移1.2米；真实顶面、碰撞与人物未移动。" :
                    "裁剪前对照：显示同平台原生Tilemap，隐藏缓存；作者格、enabled与材质未改。";
                return true;
            }
            catch (Exception error)
            {
                Restore(); LastMessage = "观察未启用：" + error.Message; return false;
            }
        }

        public void Restore()
        {
            EnsureBuffers();
            foreach (var item in states)
            {
                if (item.Key == null || item.Key.forceRenderingOff != item.Value.written) continue;
                // Binding已先释放时，不能把BeforeClip前由Binding拥有的隐藏位重新写回。
                if (item.Value.original && !item.Value.written && item.Value.hadNativeOwner &&
                    (item.Value.nativeOwner == null || !item.Value.nativeOwner.isActiveAndEnabled)) continue;
                item.Key.forceRenderingOff = item.Value.original;
            }
            states.Clear();
            if (temporaryRoot != null) { temporaryRoot.SetActive(false); Dispose(temporaryRoot); temporaryRoot = null; }
            temporaryRenderers.Clear();
            foreach (var resource in resources) if (resource != null) Dispose(resource);
            resources.Clear(); diagnosticVertices = 0; Mode = GroundSurfaceLabViewMode.Final; ObservationRevision++;
        }

        private void EnsureBuffers()
        {
            if (states == null) states = new Dictionary<Renderer, ForceState>();
            if (temporaryRenderers == null) temporaryRenderers = new List<MeshRenderer>();
            if (resources == null) resources = new List<UnityEngine.Object>();
        }
        private void ValidateInputs()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded || surfaces == null || surfaces.Length == 0 || surfaces.Length > 16)
                throw new InvalidOperationException("请选择本样板内1至16个已登记平台。");
            var ids = new HashSet<string>();
            foreach (var binding in surfaces)
                if (binding == null || !binding.isActiveAndEnabled || binding.gameObject.scene != gameObject.scene || binding.map == null ||
                    binding.map.gameObject.scene != gameObject.scene || binding.map.surface == null || binding.map.surface.layers == null || binding.map.surface.layers.Length == 0 ||
                    string.IsNullOrEmpty(binding.SurfaceId) || !ids.Add(binding.SurfaceId))
                    throw new InvalidOperationException("平台缺失、跨场、停用或surfaceId重复；未替换真实世界。");
            foreach (var root in decorationRoots ?? Array.Empty<Transform>())
                if (root != null && root.gameObject.scene != gameObject.scene) throw new InvalidOperationException("装饰根跨场；不会隐藏外部物件。");
        }
        private GroundSurfaceMeshCache RequireCache(GroundSurfaceBinding binding)
        {
            var caches = new HashSet<GroundSurfaceMeshCache>(binding.GetComponentsInChildren<GroundSurfaceMeshCache>(true).Where(c => c.binding == binding));
            if (binding.cacheRoot != null) foreach (var cache in binding.cacheRoot.GetComponentsInChildren<GroundSurfaceMeshCache>(true)) if (cache.binding == binding) caches.Add(cache);
            if (caches.Count != 1) throw new InvalidOperationException("平台" + binding.name + "缺少唯一对应的顶面缓存；先重建。");
            var result = caches.Single();
            if (result.chunks == null || !result.chunks.Any(c => c != null && c.filter != null && c.renderer != null && c.filter.sharedMesh != null))
                throw new InvalidOperationException("平台" + binding.name + "没有有效显示块；先重建。");
            return result;
        }
        private static IEnumerable<TilemapRenderer> NativeRenderers(GroundSurfaceBinding binding)
        {
            if (binding.map.surface == null || binding.map.surface.layers == null) yield break;
            foreach (var layer in binding.map.surface.layers)
                if (layer != null && layer.tilemap != null && binding.map.surface.Owns(layer.tilemap))
                { var renderer = layer.tilemap.GetComponent<TilemapRenderer>(); if (renderer != null) yield return renderer; }
        }
        private void HideDecorations()
        { foreach (var root in decorationRoots ?? Array.Empty<Transform>()) if (root != null) foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) Force(renderer, true); }
        private void Force(Renderer renderer, bool value, GroundSurfaceBinding nativeOwner = null)
        {
            if (renderer == null || renderer.forceRenderingOff == value) return;
            if (renderer.gameObject.scene != gameObject.scene) throw new InvalidOperationException("显示引用跨场；观察已恢复。");
            if (!states.TryGetValue(renderer, out var state))
            { state = new ForceState { original = renderer.forceRenderingOff, nativeOwner = nativeOwner, hadNativeOwner = nativeOwner != null }; states.Add(renderer, state); }
            state.written = value; renderer.forceRenderingOff = value;
        }
        private MeshRenderer AddRenderer(string label, Mesh mesh, Material[] materials, int layer = -1)
        {
            if (temporaryRenderers.Count >= 256) throw new InvalidOperationException("观察显示块超过256上限，请缩小样板。");
            if (temporaryRoot == null)
            {
                temporaryRoot = new GameObject("地表观察_临时显示") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(temporaryRoot, gameObject.scene);
            }
            var output = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave };
            output.layer = layer < 0 ? gameObject.layer : layer;
            output.transform.SetParent(temporaryRoot.transform, false);
            output.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = output.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            temporaryRenderers.Add(renderer); return renderer;
        }
        private Mesh NewMesh(Vector3[] vertices, string label)
        {
            diagnosticVertices += vertices.Length;
            if (diagnosticVertices > MaximumDiagnosticVertices) throw new InvalidOperationException("观察副本超过60万顶点上限，请缩小样板。");
            var mesh = new Mesh { name = label, hideFlags = HideFlags.HideAndDontSave, indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            resources.Add(mesh); mesh.vertices = vertices; return mesh;
        }
        private Mesh CopyWorldMesh(Mesh source, Matrix4x4 matrix, Vector3 offset)
        {
            if (source == null || !source.isReadable) throw new InvalidOperationException("诊断需要可读的静态Mesh；未修改源Importer。");
            var vertices = source.vertices; for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]) + offset;
            var mesh = NewMesh(vertices, "顶面显示副本"); mesh.uv = source.uv; mesh.colors = source.colors;
            var normals = source.normals; var normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            mesh.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) mesh.SetIndices(source.GetIndices(i), source.GetTopology(i), i, false);
            mesh.RecalculateBounds(); return mesh;
        }
        private Material LineMaterial(Color color)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) throw new InvalidOperationException("缺少本机线框Shader；未替换为Always或修改管线。");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_Color", color); material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off); material.SetInt("_ZWrite", 0); material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            resources.Add(material); return material;
        }
        private void Lines(string label, Mesh source, Matrix4x4 matrix, Color color)
        {
            if (source == null || !source.isReadable) throw new InvalidOperationException("线框需要可读静态Mesh；源文件保留。");
            var vertices = source.vertices; for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var edges = new HashSet<ulong>(); var indices = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("当前线框仅支持三角静态Mesh。");
                var triangles = source.GetIndices(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                { Edge(triangles[i], triangles[i+1], edges, indices); Edge(triangles[i+1], triangles[i+2], edges, indices); Edge(triangles[i+2], triangles[i], edges, indices); }
            }
            var mesh = NewMesh(vertices, label); mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0); mesh.RecalculateBounds();
            AddRenderer(label, mesh, new[] { LineMaterial(color) });
        }
        private static void Edge(int a, int b, HashSet<ulong> edges, List<int> output)
        { uint lo = (uint)Math.Min(a,b), hi = (uint)Math.Max(a,b); if (edges.Add(((ulong)lo << 32) | hi)) { output.Add(a); output.Add(b); } }
        private void Rim(GroundSurfaceBinding binding)
        {
            if (binding.boundary == null || binding.boundary.rim == null || binding.boundary.rim.Length < 3) throw new InvalidOperationException("平台缺少已读回的连接环。");
            var ring = binding.boundary.rim; var vertices = new Vector3[ring.Length]; var indices = new int[ring.Length * 2];
            for (int i = 0; i < ring.Length; i++)
            { vertices[i] = binding.transform.TransformPoint(new Vector3(ring[i].x, binding.boundary.localTopY, ring[i].y)); indices[i*2] = i; indices[i*2+1] = (i+1)%ring.Length; }
            var mesh = NewMesh(vertices, "模型连接环"); mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
            AddRenderer("模型连接环", mesh, new[] { LineMaterial(new Color(.35f,1f,.3f,1)) });
        }
        private static void Dispose(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
