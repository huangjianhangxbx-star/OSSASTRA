/*
 * 脚本：SideViewCombatEffects
 * 功能：按可配置的横版配方播放释放弧光、命中飞屑与防御轮廓，管理限量世界实例。
 * 边界：只负责效果池和画面，不选攻击目标、不扣血、不播放声音。
 * 协作：CombatFeedbackView提供已冻结的方向和命中点，相机和音频各自消费事实。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.Combat
{
    public enum CombatEffectDirection { Released, Arriving, Surface, Fixed }
    internal enum SideViewEffectShape { Swing, Hit, Guard, Evade, Surface }

    [Serializable]
    public sealed class SideViewEffectRecipe
    {
        public Color tint = Color.white;
        [Min(.01f)] public float size = .8f;
        [Min(.01f)] public float lifetime = .16f;
        [Min(0f)] public float drift = .12f;
        [Range(0f, 1f)] public float shake;
        public CombatEffectDirection direction = CombatEffectDirection.Arriving;
        public Vector2 localOffset;
        public float angleOffset;
    }

    [Serializable]
    public sealed class SideViewCombatEffectSettings
    {
        [Range(1, 64)] public int capacity = 24;
        public int sortingOrder = 2;
        public SideViewEffectRecipe release = new SideViewEffectRecipe
        {
            tint = new Color(1f, .96f, .76f, .65f), size = 1.42f, lifetime = .14f, drift = .20f,
            direction = CombatEffectDirection.Released, localOffset = new Vector2(.38f, 0f)
        };
        public SideViewEffectRecipe hit = new SideViewEffectRecipe
        {
            tint = new Color(1f, .72f, .23f), size = 1.32f, lifetime = .22f, drift = .18f, shake = .7f
        };
        public SideViewEffectRecipe guard = new SideViewEffectRecipe
        {
            tint = new Color(.52f, .83f, 1f), size = 1.06f, lifetime = .21f, drift = .10f, shake = .32f,
            direction = CombatEffectDirection.Surface
        };
        public SideViewEffectRecipe evade = new SideViewEffectRecipe
        {
            tint = new Color(.76f, .86f, 1f, .62f), size = .86f, lifetime = .15f, drift = .20f
        };
        public SideViewEffectRecipe surface = new SideViewEffectRecipe
        {
            tint = new Color(1f, .68f, .42f), size = .92f, lifetime = .16f, drift = .09f, shake = .2f,
            direction = CombatEffectDirection.Surface
        };
    }

    internal sealed class SideViewCombatEffects : IDisposable
    {
        private sealed class Instance
        {
            public Transform transform;
            public MeshFilter mesh;
            public MeshRenderer renderer;
            public Vector3 origin, forward;
            public Color tint;
            public float age, lifetime, size, drift;
        }

        private static readonly int Tint = Shader.PropertyToID("_Color");
        private readonly SideViewCombatEffectSettings settings;
        private readonly Transform owner;
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Dictionary<SideViewEffectShape, Mesh> shapes = new Dictionary<SideViewEffectShape, Mesh>();
        private readonly Stack<Instance> idle = new Stack<Instance>();
        private readonly List<Instance> active = new List<Instance>();
        private int created;

        public int ActiveCount => active.Count;

        public SideViewCombatEffects(Transform owner, SideViewCombatEffectSettings settings)
        {
            this.owner = owner;
            this.settings = settings;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader) { name = "SideViewCombatEffects", hideFlags = HideFlags.DontSave };
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }
        }

        public void Play(SideViewEffectShape shape, SideViewEffectRecipe recipe, Vector3 point,
            Vector3 direction, Color? tint = null)
        {
            if (material == null || recipe == null || recipe.size <= 0 || recipe.lifetime <= 0) return;
            if (idle.Count == 0 && created >= Mathf.Clamp(settings.capacity, 1, 64)) return;
            var item = idle.Count > 0 ? idle.Pop() : Create();
            if (!shapes.TryGetValue(shape, out var mesh))
            {
                mesh = CreateShape(shape);
                shapes.Add(shape, mesh);
            }

            // 美术本地前向为+X，绕Z旋转。保留真实接触的Z，绘制优先级由sortingOrder确定。
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + recipe.angleOffset;
            var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            item.mesh.sharedMesh = mesh;
            item.origin = point + rotation * (Vector3)recipe.localOffset;
            item.forward = rotation * Vector3.right;
            item.age = 0;
            item.lifetime = recipe.lifetime;
            item.tint = tint ?? recipe.tint;
            item.size = recipe.size;
            item.drift = recipe.drift;
            item.transform.SetPositionAndRotation(item.origin, rotation);
            item.renderer.sortingOrder = settings.sortingOrder;
            active.Add(item);
            Render(item, 0f);
            item.renderer.enabled = true;
        }

        public void Tick(float delta)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                item.age += delta;
                if (item.age >= item.lifetime)
                {
                    item.renderer.enabled = false;
                    item.renderer.SetPropertyBlock(null);
                    item.mesh.sharedMesh = null;
                    active.RemoveAt(i);
                    idle.Push(item);
                }
                else Render(item, item.age / item.lifetime);
            }
        }

        private Instance Create()
        {
            created++;
            var obj = new GameObject("SideViewCombatEffect") { hideFlags = HideFlags.DontSave };
            obj.transform.SetParent(owner, false);
            var mesh = obj.AddComponent<MeshFilter>();
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            return new Instance { transform = obj.transform, mesh = mesh, renderer = renderer };
        }

        private void Render(Instance item, float progress)
        {
            var color = item.tint;
            color.a *= (1 - progress) * (1 - progress);
            item.transform.position = item.origin + item.forward * (item.drift * progress);
            item.transform.localScale = Vector3.one * (item.size * Mathf.Lerp(.84f, 1.22f, progress));
            properties.Clear();
            properties.SetColor(Tint, color);
            item.renderer.SetPropertyBlock(properties);
        }

        private static Mesh CreateShape(SideViewEffectShape shape)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            if (shape == SideViewEffectShape.Swing)
            {
                Crescent(.76f, .15f, 67f, .9f);
                Crescent(.96f, .026f, 43f, .45f);
            }
            else if (shape == SideViewEffectShape.Guard)
            {
                Crescent(.47f, .14f, 79f, .92f);
                Crescent(.63f, .032f, 60f, .70f);
                Shard(new Vector2(.38f, .05f), new Vector2(.38f, -.42f), .07f);
            }
            else if (shape == SideViewEffectShape.Evade)
            {
                Shard(new Vector2(-.41f, -.12f), new Vector2(.49f, -.20f), .085f);
                Shard(new Vector2(-.22f, .17f), new Vector2(.62f, .13f), .11f);
            }
            else
            {
                // 爆点、主方向长屑及散开短屑由同一张三角网格绘制。
                Shard(new Vector2(-.37f, .02f), new Vector2(.73f, .04f), .15f);
                Shard(Vector2.zero, new Vector2(.46f, .43f), .10f);
                Shard(new Vector2(.10f, -.06f), new Vector2(.71f, -.35f), .068f);
                Shard(new Vector2(-.08f, .08f), new Vector2(-.08f, .47f), .09f);
                Shard(new Vector2(-.08f, -.05f), new Vector2(-.20f, -.32f), .085f);
                Shard(new Vector2(.27f, .27f), new Vector2(.84f, .41f), .032f);
                Shard(new Vector2(.44f, -.19f), new Vector2(.89f, -.26f), .036f);
            }

            var mesh = new Mesh { name = "SideViewCombat_" + shape, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            return mesh;

            void Triangle(Vector2 a, Vector2 b, Vector2 c, float opacityA, float opacityB, float opacityC)
            {
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(new Color(1, 1, 1, opacityA));
                colors.Add(new Color(1, 1, 1, opacityB));
                colors.Add(new Color(1, 1, 1, opacityC));
            }

            void Shard(Vector2 a, Vector2 b, float width)
            {
                var d = (b - a).normalized;
                var middle = Vector2.Lerp(a, b, .26f);
                var across = new Vector2(-d.y, d.x) * width;
                Triangle(a, middle + across, b, .08f, .9f, 0f);
                Triangle(a, b, middle - across, .08f, 0f, .9f);
            }

            void Crescent(float radius, float width, float limit, float opacity)
            {
                const int steps = 12;
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / steps;
                    float u = (float)(i + 1) / steps;
                    var a = Unit(t);
                    var b = Unit(u);
                    float insetA = width * Mathf.Sin(t * Mathf.PI);
                    float insetB = width * Mathf.Sin(u * Mathf.PI);
                    Triangle(a * radius, a * (radius - insetA), b * (radius - insetB), opacity, 0, 0);
                    Triangle(a * radius, b * (radius - insetB), b * radius, opacity, 0, opacity);
                }
                Vector2 Unit(float t)
                {
                    float angle = Mathf.Lerp(-limit, limit, t) * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in active) DestroyItem(item);
            foreach (var item in idle) DestroyItem(item);
            active.Clear();
            idle.Clear();
            foreach (var mesh in shapes.Values) Object.Destroy(mesh);
            shapes.Clear();
            if (material != null) Object.Destroy(material);
        }

        private static void DestroyItem(Instance item)
        {
            if (item.renderer == null) return;
            item.renderer.enabled = false;
            Object.Destroy(item.renderer.gameObject);
        }
    }
}
