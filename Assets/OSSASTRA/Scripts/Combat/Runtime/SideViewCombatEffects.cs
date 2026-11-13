/*
 * 脚本：SideViewCombatEffects
 * 功能：管理横版释放刀光与接触反馈的分层配方、世界锚点和有限效果池。
 * 边界：只读释放/接触事实，既不参与伤害、音频、震屏，也不参与角色身体 Coverage。
 * 协作：跟随层读取现有插值后的身体位置；世界层停留在回执记录的空间位置。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.Combat
{
    public enum CombatEffectDirection { Released, Arriving, Surface, Fixed }
    public enum SideViewEffectShape { Swing, Hit, Guard, Evade, Surface }
    public enum SideViewEffectLayerShape
    {
        Crescent, SlashAccent, ContactFlash, DirectionalStrips, DirectionalMotes,
        GuardRim, EvadeStreaks, SurfaceChips
    }

    public enum SideViewEffectOrientation { Rotate, MirrorHorizontal }

    public enum SideViewEffectAdmission
    {
        Accepted, MissingMaterial, InvalidRecipe, PoolFull
    }

    [Serializable]
    public sealed class SideViewEffectLayer
    {
        public SideViewEffectLayerShape shape;
        public SideViewEffectOrientation orientation;
        [Tooltip("朝向空间中的平移。每层素材的原点及自然前向为 +X。")]
        public Vector2 offset;
        public Color tint = Color.white;
        [Tooltip("本机原作帧图集。每帧大小一致，横向顺排。")]
        public Texture2D frameStrip;
        [Min(1)] public int frameCount = 1;
        [Min(1)] public float framesPerSecond = 20f;
        public Vector2 framePivot = new Vector2(.5f, .5f);
        public Vector2 frameWorldSize = Vector2.one;
        public float angleOffset;
        [Range(0f, .99f)] public float delay;
        [Range(.01f, 1f)] public float duration = 1f;
        [Min(.01f)] public float size = 1f;
        [Min(.01f)] public float endScale = 1f;
        [Tooltip("效果随时间沿自然前向移动的距离，以配方尺寸为单位。")]
        public float travel;
        [Tooltip("使用发起者当前的显示位置；释放方向永远取最初快照。")]
        public bool followsBody;

        internal bool IsValid => delay >= 0f && duration > 0f && size > 0f && endScale > 0f &&
            (frameStrip == null || (frameCount > 0 && framesPerSecond > 0f &&
                frameStrip.width % frameCount == 0 && frameWorldSize.x > 0f && frameWorldSize.y > 0f)) &&
            Enum.IsDefined(typeof(SideViewEffectLayerShape), shape);
    }

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
        [Tooltip("时间以配方寿命为单位。接触配方使用回执的世界锚点，释放层可以跟随角色显示位置。")]
        public SideViewEffectLayer[] layers;
    }

    [Serializable]
    public sealed class SideViewCombatEffectSettings
    {
        public Shader shader;
        [Range(1, 64)] public int capacity = 24;
        [Tooltip("横版角色层为 1–2，前景为 3。保持效果位于前景后方。")]
        public int sortingOrder = 2;
        [Tooltip("仅正式横版普通攻击。原作帧图集与绑定保存在本机。")]
        public SideViewEffectRecipe originalRelease, originalAlternateRelease, originalHit;
        public SideViewEffectRecipe release = new SideViewEffectRecipe
        {
            tint = new Color(1f, .96f, .82f, .94f), size = 1.42f, lifetime = .16f, drift = .08f,
            direction = CombatEffectDirection.Released, localOffset = new Vector2(.38f, 0f),
            layers = SideViewEffectDefaults.For(SideViewEffectShape.Swing)
        };
        public SideViewEffectRecipe hit = new SideViewEffectRecipe
        {
            tint = new Color(1f, .83f, .48f), size = 1.32f, lifetime = .22f, drift = .08f, shake = .7f,
            layers = SideViewEffectDefaults.For(SideViewEffectShape.Hit)
        };
        public SideViewEffectRecipe guard = new SideViewEffectRecipe
        {
            tint = new Color(.52f, .83f, 1f), size = 1.06f, lifetime = .21f, drift = .10f, shake = .32f,
            direction = CombatEffectDirection.Surface, layers = SideViewEffectDefaults.For(SideViewEffectShape.Guard)
        };
        public SideViewEffectRecipe evade = new SideViewEffectRecipe
        {
            tint = new Color(.76f, .86f, 1f, .62f), size = .86f, lifetime = .15f, drift = .20f,
            layers = SideViewEffectDefaults.For(SideViewEffectShape.Evade)
        };
        public SideViewEffectRecipe surface = new SideViewEffectRecipe
        {
            tint = new Color(1f, .68f, .42f), size = .92f, lifetime = .16f, drift = .09f, shake = .2f,
            direction = CombatEffectDirection.Surface, layers = SideViewEffectDefaults.For(SideViewEffectShape.Surface)
        };
    }

    internal static class SideViewEffectDefaults
    {
        // 每层采用独立时序。短暂接触闪光之后，两侧尖条的展开和较慢的细屑仍清楚可见。
        internal static SideViewEffectLayer[] For(SideViewEffectShape effect)
        {
            var white = new Color(1f, 1f, .97f);
            switch (effect)
            {
                case SideViewEffectShape.Swing:
                    return new[]
                    {
                        Layer(SideViewEffectLayerShape.Crescent, 0, 1f, 1f, 1.10f, 0f, white, true),
                        Layer(SideViewEffectLayerShape.SlashAccent, .1f, .65f, .75f, 1.15f, .03f, white, true)
                    };
                case SideViewEffectShape.Hit:
                    return new[]
                    {
                        Layer(SideViewEffectLayerShape.ContactFlash, 0, .46f, .57f, 1.30f, 0, white),
                        Layer(SideViewEffectLayerShape.DirectionalStrips, .08f, .77f, 1.08f, .42f, .12f, white),
                        Layer(SideViewEffectLayerShape.DirectionalMotes, .19f, .81f, .84f, 1.45f, .30f,
                            new Color(1f, .88f, .62f, .93f))
                    };
                case SideViewEffectShape.Guard:
                    return new[]
                    {
                        Layer(SideViewEffectLayerShape.GuardRim, 0, 1f, 1f, 1.27f, .04f, white),
                        Layer(SideViewEffectLayerShape.ContactFlash, 0, .27f, .45f, 1.05f, 0, white)
                    };
                case SideViewEffectShape.Evade:
                    return new[]
                    {
                        Layer(SideViewEffectLayerShape.EvadeStreaks, 0, 1f, 1f, 1.11f, .22f, white)
                    };
                default:
                    return new[]
                    {
                        Layer(SideViewEffectLayerShape.ContactFlash, 0, .32f, .37f, 1.13f, 0, white),
                        Layer(SideViewEffectLayerShape.SurfaceChips, .1f, .9f, 1f, 1.33f, .20f,
                            new Color(1f, .83f, .60f))
                    };
            }
        }

        private static SideViewEffectLayer Layer(SideViewEffectLayerShape shape, float delay, float duration,
            float size, float endScale, float travel, Color tint, bool followsBody = false)
        {
            return new SideViewEffectLayer
            {
                shape = shape, delay = delay, duration = duration, size = size, endScale = endScale,
                travel = travel, tint = tint, followsBody = followsBody,
                orientation = followsBody ? SideViewEffectOrientation.MirrorHorizontal : SideViewEffectOrientation.Rotate
            };
        }
    }

    internal sealed class SideViewCombatEffects : IDisposable
    {
        private sealed class LayerInstance
        {
            public Transform transform;
            public MeshFilter mesh;
            public MeshRenderer renderer;
            public SideViewEffectLayer recipe;
            public Vector3 localOffset, forward, scale;
        }

        private sealed class Instance
        {
            public Transform transform;
            public SortingGroup sorting;
            public readonly List<LayerInstance> layers = new List<LayerInstance>(3);
            public CharacterMovement body;
            public Vector3 origin, anchorOffset, bodyRelativeOrigin;
            public Color tint;
            public float age, lifetime, size, drift;
            public int layerCount, frameStarted;
        }

        private const int MaxLayers = 8;
        internal const string ShaderName = "Ossastra/SideView/CombatEffect";
        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int FrameUv = Shader.PropertyToID("_FrameUv");
        private readonly SideViewCombatEffectSettings settings;
        private readonly Transform owner;
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Dictionary<SideViewEffectLayerShape, Mesh> meshes = new Dictionary<SideViewEffectLayerShape, Mesh>();
        private readonly Dictionary<SideViewEffectLayer, Mesh> stripMeshes = new Dictionary<SideViewEffectLayer, Mesh>();
        private readonly Dictionary<SideViewEffectShape, SideViewEffectLayer[]> defaults = new Dictionary<SideViewEffectShape, SideViewEffectLayer[]>();
        private readonly Stack<Instance> idle = new Stack<Instance>();
        private readonly List<Instance> active = new List<Instance>();

        public int ActiveCount => active.Count;

        public SideViewCombatEffects(Transform owner, SideViewCombatEffectSettings settings)
        {
            this.owner = owner;
            this.settings = settings;
            var shader = settings.shader != null ? settings.shader : Shader.Find(ShaderName);
            if (shader != null && shader.isSupported)
                material = new Material(shader) { name = "SideViewCombatEffects", hideFlags = HideFlags.DontSave };
        }

        internal SideViewEffectAdmission Play(SideViewEffectShape shape, SideViewEffectRecipe recipe, Vector3 point,
            Vector3 direction, CharacterMovement body = null, Color? tint = null, Vector3 anchorOffset = default)
        {
            if (material == null) return SideViewEffectAdmission.MissingMaterial;
            if (recipe == null || !(recipe.size > 0) || !(recipe.lifetime > 0) ||
                !Finite(recipe.size) || !Finite(recipe.lifetime))
                return SideViewEffectAdmission.InvalidRecipe;
            var layers = GetLayers(shape, recipe);
            if (layers.Length > MaxLayers || Array.Exists(layers, layer => layer == null || !layer.IsValid))
                return SideViewEffectAdmission.InvalidRecipe;
            if (active.Count >= Mathf.Clamp(settings.capacity, 1, 64)) return SideViewEffectAdmission.PoolFull;

            var item = idle.Count > 0 ? idle.Pop() : Create();
            item.origin = point;
            item.anchorOffset = anchorOffset;
            item.bodyRelativeOrigin = body == null ? Vector3.zero : point - body.transform.position;
            item.body = body;
            item.age = 0f;
            item.frameStarted = Time.frameCount;
            item.lifetime = recipe.lifetime;
            item.tint = tint ?? recipe.tint;
            item.size = recipe.size;
            item.drift = recipe.drift;
            item.layerCount = layers.Length;
            item.sorting.sortingOrder = settings.sortingOrder;
            item.transform.position = point;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = i < item.layers.Count ? item.layers[i] : AddLayer(item, i);
                PrepareLayer(layer, layers[i], recipe, direction);
            }
            active.Add(item);
            Render(item);
            return SideViewEffectAdmission.Accepted;
        }

        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);

        private SideViewEffectLayer[] GetLayers(SideViewEffectShape shape, SideViewEffectRecipe recipe)
        {
            if (recipe.layers != null && recipe.layers.Length > 0) return recipe.layers;
            // 早期场景没有序列化子层时，沿现有主配方设置补齐子层。
            if (!defaults.TryGetValue(shape, out var layers))
            {
                layers = SideViewEffectDefaults.For(shape);
                defaults.Add(shape, layers);
            }
            return layers;
        }

        private void PrepareLayer(LayerInstance layer, SideViewEffectLayer settings, SideViewEffectRecipe parent,
            Vector3 direction)
        {
            layer.recipe = settings;
            layer.mesh.sharedMesh = GetMesh(settings);
            bool mirror = settings.orientation == SideViewEffectOrientation.MirrorHorizontal && direction.x < 0f;
            float angle = Mathf.Atan2(direction.y, mirror ? -direction.x : direction.x) * Mathf.Rad2Deg;
            angle += parent.angleOffset + settings.angleOffset;
            var rotate = Quaternion.AngleAxis(angle, Vector3.forward);
            var flip = new Vector3(mirror ? -1 : 1, 1, 1);
            // 先做右向造型，再沿世界 X 镜像。斜向左上时，镜像仍朝左上。
            layer.localOffset = Vector3.Scale(rotate * (Vector3)(parent.localOffset + settings.offset), flip);
            layer.forward = Vector3.Scale(rotate * Vector3.right, flip);
            layer.scale = flip;
            layer.transform.rotation = mirror ? Quaternion.AngleAxis(-angle, Vector3.forward) : rotate;
        }

        private Mesh GetMesh(SideViewEffectLayer settings)
        {
            if (settings.frameStrip != null)
            {
                if (!stripMeshes.TryGetValue(settings, out var stripMesh))
                {
                    stripMesh = BuildFrameQuad(settings);
                    stripMeshes.Add(settings, stripMesh);
                }
                return stripMesh;
            }
            if (!meshes.TryGetValue(settings.shape, out var mesh))
            {
                mesh = SideViewEffectMesh.Build(settings.shape);
                meshes.Add(settings.shape, mesh);
            }
            return mesh;
        }

        private static Mesh BuildFrameQuad(SideViewEffectLayer layer)
        {
            var min = -Vector2.Scale(layer.framePivot, layer.frameWorldSize);
            var max = min + layer.frameWorldSize;
            var mesh = new Mesh { name = "SideViewCombat_" + layer.frameStrip.name, hideFlags = HideFlags.DontSave };
            mesh.vertices = new Vector3[]
            {
                min, new Vector3(max.x, min.y, 0f), max, new Vector3(min.x, max.y, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return mesh;
        }

        internal void Tick(float delta)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                // 保留释放帧，避免一次长帧直接跳过极短的爆点。
                if (item.frameStarted != Time.frameCount) item.age += delta;
                if (item.age < item.lifetime)
                {
                    Render(item);
                    continue;
                }
                foreach (var layer in item.layers)
                {
                    layer.renderer.enabled = false;
                    layer.renderer.SetPropertyBlock(null);
                    layer.mesh.sharedMesh = null;
                    layer.recipe = null;
                }
                item.body = null;
                item.layerCount = 0;
                active.RemoveAt(i);
                idle.Push(item);
            }
        }

        private Instance Create()
        {
            var obj = new GameObject("SideViewCombatEffect") { hideFlags = HideFlags.DontSave };
            obj.layer = owner.gameObject.layer;
            obj.transform.SetParent(owner, false);
            var sorting = obj.AddComponent<SortingGroup>();
            return new Instance { transform = obj.transform, sorting = sorting };
        }

        private LayerInstance AddLayer(Instance item, int index)
        {
            var obj = new GameObject("EffectLayer") { hideFlags = HideFlags.DontSave };
            obj.layer = owner.gameObject.layer;
            obj.transform.SetParent(item.transform, false);
            var mesh = obj.AddComponent<MeshFilter>();
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = index;
            var layer = new LayerInstance { transform = obj.transform, mesh = mesh, renderer = renderer };
            item.layers.Add(layer);
            return layer;
        }

        private void Render(Instance item)
        {
            float age = item.age / item.lifetime;
            bool hasBody = item.body != null;
            var bodyPosition = hasBody ? item.body.PresentedPosition + item.bodyRelativeOrigin : item.origin;
            item.transform.position = bodyPosition;
            for (int i = 0; i < item.layerCount; i++)
            {
                var layer = item.layers[i];
                var recipe = layer.recipe;
                bool animated = recipe.frameStrip != null;
                float localAge = item.age - recipe.delay * item.lifetime;
                float progress = (age - recipe.delay) / recipe.duration;
                if (animated) progress = localAge * recipe.framesPerSecond / recipe.frameCount;
                if (progress < 0f || progress >= 1f)
                {
                    layer.renderer.enabled = false;
                    continue;
                }
                var color = recipe.tint;
                // 爆点和条带保留明亮核心，配方色主要进入柔边及较慢的碎屑。
                if (recipe.shape == SideViewEffectLayerShape.ContactFlash ||
                    recipe.shape == SideViewEffectLayerShape.DirectionalStrips)
                    color *= Color.Lerp(item.tint, Color.white, .82f);
                else color *= item.tint;
                float fade = animated ? 1f : 1f - Mathf.SmoothStep(0, 1, progress);
                color.a = recipe.tint.a * item.tint.a * fade;
                var anchor = recipe.followsBody ? bodyPosition : item.origin;
                layer.transform.position = anchor + item.anchorOffset + layer.localOffset + layer.forward *
                    ((item.drift + recipe.travel * item.size) * progress);
                // 条带先快速打开，再由尖端向接触点收短，避免最大外廓一直持续到淡出。
                float scale = animated ? 1f : recipe.shape == SideViewEffectLayerShape.DirectionalStrips
                    ? StripScale(progress, recipe.endScale)
                    : Mathf.Lerp(1f, recipe.endScale, progress);
                layer.transform.localScale = layer.scale * (item.size * recipe.size * scale);
                properties.Clear();
                properties.SetColor(Tint, color);
                if (animated)
                {
                    int frame = Mathf.FloorToInt(localAge * recipe.framesPerSecond);
                    float width = 1f / recipe.frameCount;
                    properties.SetTexture(MainTexture, recipe.frameStrip);
                    properties.SetVector(FrameUv, new Vector4(width, 1f, frame * width, 0f));
                }
                layer.renderer.SetPropertyBlock(properties);
                layer.renderer.enabled = true;
            }
        }

        private static float StripScale(float progress, float endScale)
        {
            const float peakTime = .21f;
            return progress < peakTime ? Mathf.Lerp(.74f, 1.18f, progress / peakTime) :
                Mathf.Lerp(1.18f, endScale, (progress - peakTime) / (1f - peakTime));
        }

        public void Dispose()
        {
            foreach (var item in active) DestroyItem(item);
            foreach (var item in idle) DestroyItem(item);
            active.Clear();
            idle.Clear();
            foreach (var mesh in meshes.Values) Object.Destroy(mesh);
            meshes.Clear();
            foreach (var mesh in stripMeshes.Values) Object.Destroy(mesh);
            stripMeshes.Clear();
            if (material != null) Object.Destroy(material);
        }

        private static void DestroyItem(Instance item)
        {
            if (item.transform == null) return;
            item.transform.gameObject.SetActive(false);
            Object.Destroy(item.transform.gameObject);
        }
    }
}

