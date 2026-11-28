/*
 * 脚本：CombinedVfx
 * 功能：用单一实例时间轴生成笔触网格、贴片和脱离发射器的飞片。
 * 角色事件与调试台负责启动和回收；预览直接调用同一个求值入口。
 * 本组件只写视觉状态，不派发命中、伤害或其他玩法事件。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombinedVfx : MonoBehaviour
    {
        public Texture2D defaultTexture;
        public Material material;
        public Vector2 offset;
        public float rotation;
        public Vector2 scale = Vector2.one;
        public VfxPath sharedPath = VfxPath.Line();
        public List<VfxLayer> layers = new List<VfxLayer>();

        private const int StripSegments = 48;
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private readonly List<LayerMesh> meshes = new List<LayerMesh>();
        private readonly Dictionary<string, Matrix4x4> previousPoses = new Dictionary<string, Matrix4x4>();
        private readonly Dictionary<string, Matrix4x4> currentPoses = new Dictionary<string, Matrix4x4>();
        private readonly Dictionary<(string anchor, float time), Matrix4x4> capturedPoses =
            new Dictionary<(string anchor, float time), Matrix4x4>();
        private Material fallbackMaterial;
        private Func<string, Matrix4x4> poseSource;
        private float previousAge, age;
        private int seed;
        private bool started;

        public float Duration
        {
            get
            {
                float latest = 0f;
                foreach (VfxLayer layer in layers)
                    if (layer != null && layer.visible) latest = Mathf.Max(latest, layer.EndTime);
                return latest;
            }
        }
        public bool IsComplete => started && age >= Duration;
        public float Age => age;

        /// <summary>为一次播放采集初始挂点，空名称代表整套效果的放置基准。</summary>
        public void Begin(Func<string, Matrix4x4> pose, int randomSeed)
        {
            poseSource = pose ?? (_ => transform.localToWorldMatrix);
            seed = randomSeed;
            age = previousAge = 0f;
            started = true;
            currentPoses.Clear();
            previousPoses.Clear();
            capturedPoses.Clear();
            ReadPoses();
            Render(0f, seed, CurrentPose, ScheduledPose, null);
        }

        /// <summary>由所属播放入口推进一次项目时钟，暂停时传入零。</summary>
        public void Advance(float delta)
        {
            if (!started || delta <= 0f || IsComplete) return;
            previousAge = age;
            age = Mathf.Min(Duration, age + delta);
            ReadPoses();
            Render(age, seed, CurrentPose, ScheduledPose, null);
        }

        /// <summary>在指定时间重新求值。挂点运动由调用方提供，倒拖无需积累预览对象。</summary>
        public void Evaluate(float seconds, int randomSeed, Func<string, float, Matrix4x4> poseAt, Predicate<int> visible = null)
        {
            seconds = Mathf.Max(0f, seconds);
            Func<string, float, Matrix4x4> sampler = poseAt ?? ((_, _) => transform.localToWorldMatrix);
            Render(seconds, randomSeed, anchor => sampler(anchor, seconds), sampler, visible);
        }

        private void ReadPoses()
        {
            previousPoses.Clear();
            ReadAnchor(string.Empty);
            foreach (VfxLayer layer in layers)
                if (layer != null && !string.IsNullOrEmpty(layer.anchor)) ReadAnchor(layer.anchor);
        }

        private void ReadAnchor(string anchor)
        {
            if (previousPoses.ContainsKey(anchor)) return;
            Matrix4x4 next = poseSource(anchor);
            previousPoses[anchor] = currentPoses.TryGetValue(anchor, out Matrix4x4 current) ? current : next;
            currentPoses[anchor] = next;
        }

        private Matrix4x4 CurrentPose(string anchor) => currentPoses[anchor];

        private Matrix4x4 ScheduledPose(string anchor, float time)
        {
            var key = (anchor, time);
            if (capturedPoses.TryGetValue(key, out Matrix4x4 captured)) return captured;
            Matrix4x4 before = previousPoses[anchor], after = currentPoses[anchor];
            float fraction = Mathf.InverseLerp(previousAge, age, time);
            // 低帧率跨过发射点时，从两帧挂点插值。转身镜像是离散切换，途中保持出生前的方向。
            Vector3 xBefore = before.GetColumn(0), xAfter = after.GetColumn(0);
            Vector3 yBefore = before.GetColumn(1), yAfter = after.GetColumn(1);
            bool flip = Vector3.Cross(xBefore, yBefore).z * Vector3.Cross(xAfter, yAfter).z < 0f;
            captured = flip && fraction < 1f ? before : after;
            if (!flip)
            {
                for (int column = 0; column < 3; column++)
                    captured.SetColumn(column, Vector4.LerpUnclamped(before.GetColumn(column), after.GetColumn(column), fraction));
            }
            captured.SetColumn(3, Vector4.LerpUnclamped(before.GetColumn(3), after.GetColumn(3), fraction));
            capturedPoses.Add(key, captured);
            return captured;
        }

        private void Render(float time, int randomSeed, Func<string, Matrix4x4> livePose,
            Func<string, float, Matrix4x4> poseAt, Predicate<int> visible)
        {
            while (meshes.Count > layers.Count)
            {
                int last = meshes.Count - 1;
                meshes[last]?.Dispose(this);
                meshes.RemoveAt(last);
            }

            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            Matrix4x4 effectMatrix = Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, rotation), new Vector3(scale.x, scale.y, 1f));
            for (int i = 0; i < layers.Count; i++)
            {
                if (meshes.Count <= i) meshes.Add(null);
                VfxLayer layer = layers[i];
                if (layer == null || !layer.visible || (visible != null && !visible(i))
                    || time < layer.BeginTime || time >= layer.EndTime)
                {
                    meshes[i]?.SetVisible(false);
                    continue;
                }

                Material layerMaterial = ResolveMaterial();
                if (layerMaterial == null) continue;
                Texture texture = layer.texture ? layer.texture : defaultTexture;
                if (texture == null) texture = layerMaterial.mainTexture;
                if (meshes[i] == null) meshes[i] = new LayerMesh(this);
                LayerMesh output = meshes[i];
                Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? layer.color.linear : layer.color;
                float localTime = time - layer.BeginTime;
                string anchor = layer.anchor ?? string.Empty;
                VfxPath path = layer.useSharedPath ? sharedPath : layer.path;
                Matrix4x4 local = effectMatrix * layer.LocalMatrix;
                if (layer.kind == VfxLayerKind.Particles)
                {
                    DrawParticles(output, layer, path, i, tint, localTime, randomSeed, toLocal, local, poseAt);
                }
                else
                {
                    Matrix4x4 anchorPose = layer.anchorMode == VfxAnchorMode.FreezeAtStart
                        ? poseAt(anchor, layer.BeginTime) : livePose(anchor);
                    Matrix4x4 matrix = toLocal * anchorPose * local;
                    tint.a *= Fade(layer, localTime);
                    if (layer.kind == VfxLayerKind.Ribbon)
                        DrawRibbon(output, layer, path, tint, localTime, matrix);
                    else
                    {
                        output.Resize(4, 1);
                        output.Stamp(0, matrix, Vector2.zero, Vector2.right, layer.size, layer.AtlasRect(), tint);
                    }
                }
                output.Commit(layerMaterial, texture, layer.order);
            }
        }

        private static float Fade(VfxLayer layer, float seconds)
        {
            if (!layer.fade.enabled) return 1f;
            float progress = layer.fade.Progress(seconds);
            return Mathf.Clamp01(layer.fadeCurve?.Evaluate(progress) ?? (1f - progress));
        }

        private static void DrawRibbon(LayerMesh output, VfxLayer layer, VfxPath path, Color tint, float time, Matrix4x4 matrix)
        {
            output.Resize((StripSegments + 1) * 2, StripSegments, strip: true);
            Rect atlas = layer.AtlasRect();
            float firstV = layer.flipWidthUv ? atlas.yMax : atlas.yMin;
            float lastV = layer.flipWidthUv ? atlas.yMin : atlas.yMax;
            float reveal = layer.expand.enabled ? layer.expand.Progress(time) : 1f;
            float erase = layer.erase.enabled ? layer.erase.Progress(time) : 0f;
            float softness = Mathf.Clamp01(layer.softness);

            for (int i = 0; i <= StripSegments; i++)
            {
                float u = (float)i / StripSegments;
                Vector2 center = path?.Sample(u) ?? Vector2.zero;
                Vector2 normal = path?.Normal(u) ?? Vector2.up;
                float halfWidth = layer.WidthAt(u) * .5f;
                int v = i * 2;
                Color color = tint;
                color.a *= Gate(u, reveal, softness);
                color.a *= Gate(layer.eraseFromEnd ? u : 1f - u, 1f - erase, softness);
                // UV 始终按原路径排布。边界推进只改顶点透明度，保留剩余笔触在空间中的纹理位置。
                output.Vertex(v, matrix.MultiplyPoint3x4(center + normal * (layer.normalOffset - halfWidth)),
                    new Vector2(atlas.xMin + atlas.width * u, firstV), color);
                output.Vertex(v + 1, matrix.MultiplyPoint3x4(center + normal * (layer.normalOffset + halfWidth)),
                    new Vector2(atlas.xMin + atlas.width * u, lastV), color);
            }
        }

        private static float Gate(float position, float limit, float softness)
        {
            if (limit >= 1f) return 1f;
            if (limit <= 0f) return 0f;
            if (softness < .0001f) return position <= limit ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((limit - position) / softness));
        }

        private static void DrawParticles(LayerMesh output, VfxLayer layer, VfxPath path, int layerIndex, Color tint,
            float time, int randomSeed, Matrix4x4 toLocal, Matrix4x4 local, Func<string, float, Matrix4x4> poseAt)
        {
            int count = Mathf.Clamp(layer.count, 0, 128);
            output.Resize(count * 4, count);
            Rect atlas = layer.AtlasRect();
            string anchor = layer.anchor ?? string.Empty;
            float first = layer.BeginTime;
            float emission = Mathf.Max(0f, layer.emission);

            for (int i = 0; i < count; i++)
            {
                float along = count <= 1 ? .5f : (float)i / (count - 1);
                float birth = along * emission;
                float elapsed = time - birth;
                float life = Mathf.Max(.001f, Mathf.Lerp(layer.particleLife.x, layer.particleLife.y, Sample(randomSeed, layerIndex, i, 0)));
                if (elapsed < 0f || elapsed >= life)
                {
                    output.HideStamp(i);
                    continue;
                }

                Vector2 center = layer.particlesAlongPath && path != null ? path.Sample(along) : Vector2.zero;
                Matrix4x4 matrix = toLocal * poseAt(anchor, first + birth) * local;
                float angle = layer.direction + (Sample(randomSeed, layerIndex, i, 1) - .5f) * layer.spread;
                if (layer.particlesAlongPath && path != null)
                {
                    Vector2 normal = path.Normal(along);
                    angle += Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;
                }
                Vector2 direction = Direction(angle);
                float speed = Mathf.Lerp(layer.speed.x, layer.speed.y, Sample(randomSeed, layerIndex, i, 2));
                center += direction * (speed * elapsed) + Vector2.down * (.5f * layer.gravity * elapsed * elapsed);
                float size = Mathf.Max(0f, Mathf.Lerp(layer.particleSize.x, layer.particleSize.y, Sample(randomSeed, layerIndex, i, 3)));
                Vector2 axis = Direction(angle + layer.spin * elapsed);
                Color color = tint;
                color.a *= Mathf.Clamp01(layer.particleAlpha?.Evaluate(elapsed / life) ?? (1f - elapsed / life));
                output.Stamp(i, matrix, center, axis, layer.size * size, atlas, color);
            }
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float Sample(int seed, int layer, int index, int channel)
        {
            unchecked
            {
                uint bits = (uint)seed ^ (uint)(layer * 0x1e35a7bd) ^ (uint)(index * 0x2c1b3c6d) ^ (uint)(channel * 0x297a2d39);
                bits ^= bits >> 16;
                bits *= 0x7feb352d;
                bits ^= bits >> 15;
                bits *= 0x846ca68b;
                bits ^= bits >> 16;
                return (bits & 0x00ffffff) / 16777216f;
            }
        }

        private Material ResolveMaterial()
        {
            if (material) return material;
            if (fallbackMaterial) return fallbackMaterial;
            Shader shader = Shader.Find("Ossastra/SideView/CombatEffect");
            if (shader == null) return null;
            fallbackMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return fallbackMaterial;
        }

        public void ReleaseVisuals()
        {
            foreach (LayerMesh mesh in meshes) mesh?.Dispose(this);
            meshes.Clear();
            DestroyTemporary(fallbackMaterial);
            fallbackMaterial = null;
            started = false;
            poseSource = null;
            previousPoses.Clear();
            currentPoses.Clear();
            capturedPoses.Clear();
        }

        private void OnDisable() => ReleaseVisuals();

        private void DestroyTemporary(UnityEngine.Object resource)
        {
            if (!resource) return;
            if (Application.IsPlaying(gameObject)) Destroy(resource);
            else DestroyImmediate(resource);
        }

        private sealed class LayerMesh
        {
            private readonly GameObject container;
            private readonly Mesh mesh;
            private readonly MeshRenderer renderer;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private Vector3[] vertices = Array.Empty<Vector3>();
            private Vector2[] uv = Array.Empty<Vector2>();
            private Color[] colors = Array.Empty<Color>();
            private int[] triangles = Array.Empty<int>();
            private bool topologyDirty, strip;
            private int quads;

            public LayerMesh(CombinedVfx parent)
            {
                container = new GameObject("组合特效 · 播放层") { hideFlags = HideFlags.HideAndDontSave, layer = parent.gameObject.layer };
                container.transform.SetParent(parent.transform, false);
                renderer = container.AddComponent<MeshRenderer>();
                if (parent.TryGetComponent(out MeshRenderer reference))
                    renderer.sortingLayerID = reference.sortingLayerID;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mesh = new Mesh { name = container.name, hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                container.AddComponent<MeshFilter>().sharedMesh = mesh;
            }

            public void Resize(int vertexCount, int quadCount, bool strip = false)
            {
                if (vertices.Length == vertexCount && quads == quadCount && this.strip == strip) return;
                vertices = new Vector3[vertexCount];
                uv = new Vector2[vertexCount];
                colors = new Color[vertexCount];
                triangles = new int[quadCount * 6];
                for (int i = 0; i < quadCount; i++)
                {
                    int v = strip ? 2 * i : 4 * i;
                    int start = i * 6;
                    triangles[start] = v;
                    triangles[start + 1] = v + 1;
                    triangles[start + 2] = v + (strip ? 3 : 2);
                    triangles[start + 3] = v;
                    triangles[start + 4] = triangles[start + 2];
                    triangles[start + 5] = v + (strip ? 2 : 3);
                }
                quads = quadCount;
                this.strip = strip;
                topologyDirty = true;
            }

            public void Vertex(int index, Vector3 position, Vector2 texture, Color color)
            {
                vertices[index] = position;
                uv[index] = texture;
                colors[index] = color;
            }

            public void Stamp(int index, Matrix4x4 matrix, Vector2 center, Vector2 axis, Vector2 size, Rect atlas, Color color)
            {
                Vector2 x = axis * (size.x * .5f);
                Vector2 y = new Vector2(-axis.y, axis.x) * (size.y * .5f);
                int v = index * 4;
                Vertex(v, matrix.MultiplyPoint3x4(center - x - y), new Vector2(atlas.xMin, atlas.yMin), color);
                Vertex(v + 1, matrix.MultiplyPoint3x4(center - x + y), new Vector2(atlas.xMin, atlas.yMax), color);
                Vertex(v + 2, matrix.MultiplyPoint3x4(center + x + y), new Vector2(atlas.xMax, atlas.yMax), color);
                Vertex(v + 3, matrix.MultiplyPoint3x4(center + x - y), new Vector2(atlas.xMax, atlas.yMin), color);
            }

            public void HideStamp(int index)
            {
                int start = index * 4;
                for (int i = 0; i < 4; i++) Vertex(start + i, Vector3.zero, Vector2.zero, Color.clear);
            }

            public void Commit(Material material, Texture texture, int order)
            {
                SetVisible(quads > 0);
                if (quads == 0) return;
                if (topologyDirty) mesh.Clear();
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.colors = colors;
                if (topologyDirty)
                {
                    mesh.SetTriangles(triangles, 0, false);
                    topologyDirty = false;
                }
                mesh.RecalculateBounds();
                renderer.sortingOrder = order;
                renderer.sharedMaterial = material;
                if (texture != null) properties.SetTexture(MainTexture, texture);
                else properties.Clear();
                renderer.SetPropertyBlock(properties);
            }

            public void SetVisible(bool value) => renderer.enabled = value;

            public void Dispose(CombinedVfx parent)
            {
                renderer.enabled = false;
                parent.DestroyTemporary(mesh);
                parent.DestroyTemporary(container);
            }
        }
    }
}
