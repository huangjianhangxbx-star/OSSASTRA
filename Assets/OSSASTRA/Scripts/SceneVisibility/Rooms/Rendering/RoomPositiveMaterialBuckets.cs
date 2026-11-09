/* 功能：在相机剔除前仅为静态有限表面建立保留原Shader的自有队列材质，最终按静态桶使用overrideMaterial。
 * 边界：Full与独立动态绘制路线始终保留sharedMaterials引用；角色、敌人、Sprite、Spine及登记表现不由静态桶接管。
 * 不使用overrideShader，不修改原材质或物理；混合/未知源家族保守跳过。
 * 生命周期：Prepare先还原旧范围，预算与Shader先预检；只在数组仍为本范围所写时还原。外部继续引用的自有克隆保留并计数，解除引用后再释放。
 * 缓存：仅进程内比较源与两个自有副本的原生内容CRC；源变化或借用副本被改写即刷新，不把CRC序列化。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    internal delegate Shader RoomPositiveShaderResolver(Material source, uint family, out int pass);

    internal sealed class RoomPositiveMaterialBuckets : IDisposable
    {
        // 有限CPU观测；不增加逐Renderer标记，不改变借用或异常恢复路径。
        internal static readonly ProfilingSampler perfPlanBuild = new ProfilingSampler("RoomPositive.Buckets.PlanBuild");
        internal static readonly ProfilingSampler perfResourceRefresh = new ProfilingSampler("RoomPositive.Buckets.ResourceRefresh");
        internal static readonly ProfilingSampler perfBorrowWrites = new ProfilingSampler("RoomPositive.Buckets.Borrow");
        internal static readonly ProfilingSampler perfRestore = new ProfilingSampler("RoomPositive.Buckets.Restore");
        internal sealed class Bucket
        {
            internal Material Original, Clone, Adapter;
            internal Shader SourceShader;
            internal uint Family;
            internal int Queue, Pass;
            internal bool Transparent;
            internal bool hasCopiedState;
            internal int sourceCrc, cloneCrc, adapterCrc;
        }

        readonly struct Key : IEquatable<Key>
        {
            internal readonly Material Original;
            internal readonly uint Family;
            internal readonly bool Transparent;
            readonly int id;
            internal Key(Material original, uint family, bool transparent)
            { Original = original; Family = family; Transparent = transparent; id = original.GetInstanceID(); }
            public bool Equals(Key other) => ReferenceEquals(Original, other.Original) && Family == other.Family && Transparent == other.Transparent;
            public override bool Equals(object value) => value is Key other && Equals(other);
            public override int GetHashCode() => (id * 397) ^ (int)Family ^ (Transparent ? 1 : 0);
        }

        sealed class Request
        {
            internal Key Key;
            internal Shader Shader;
            internal int Pass, Order;
        }

        sealed class Swap
        {
            internal Renderer Renderer;
            internal Material[] Original, Written;
            internal Key[] Keys;
        }

        const int MaxBuckets = 256;
        readonly Dictionary<Material,uint> planFamilies=new Dictionary<Material,uint>();
        readonly Dictionary<Key, Bucket> cache = new Dictionary<Key, Bucket>();
        readonly Dictionary<Material, Material> originalsByClone = new Dictionary<Material, Material>();
        readonly Dictionary<Bucket, HashSet<Renderer>> pins = new Dictionary<Bucket, HashSet<Renderer>>();
        readonly List<Bucket> active = new List<Bucket>(32);
        readonly List<Swap> swaps = new List<Swap>(128);
        readonly List<Key> staleKeys = new List<Key>();
        readonly List<Bucket> stalePins = new List<Bucket>();
        readonly List<Renderer> staleHolders = new List<Renderer>();
        readonly Dictionary<Renderer,Swap> terrainPlans = new Dictionary<Renderer,Swap>();
        readonly List<Renderer> staleTerrainPlans = new List<Renderer>();
        readonly Dictionary<Material,Shader> callbackShaders = new Dictionary<Material,Shader>();
        readonly Dictionary<Material,int> callbackQueues = new Dictionary<Material,int>();

        internal IReadOnlyList<Bucket> Active => active;
        internal int RetainedConflictCount => pins.Count;
        internal int OwnedMaterialCount
        {
            get
            {
                int count = 0;
                foreach (var bucket in cache.Values)
                { if (bucket.Clone != null) ++count; if (bucket.Adapter != null) ++count; }
                return count;
            }
        }

        internal Material ResolveOriginal(Material candidate)
        {
            if (ReferenceEquals(candidate, null)) return null;
            return originalsByClone.TryGetValue(candidate, out var original) ? original : candidate;
        }

        internal void Prepare(RoomMaskFrame frame, RoomPositiveShaderResolver resolver)
        { PrepareCore(frame,resolver,null,false); }

        // Only the owning PositivePass's private ShaderFor is known callback-free.
        // Unknown delegates retain the complete legacy reread/validation path.
        internal void PrepareTerrain(RoomMaskFrame frame, RoomPositiveShaderResolver resolver,
            RoomStaticTerrainPreparation terrain, bool callbackFreeResolver=false)
        {
            if(!callbackFreeResolver){terrain?.EndPrepare();terrain=null;}
            PrepareCore(frame,resolver,terrain,!callbackFreeResolver);
        }

        void PrepareCore(RoomMaskFrame frame, RoomPositiveShaderResolver resolver,RoomStaticTerrainPreparation terrain,bool verifyCallbackBoundary)
        {
            planFamilies.Clear();
            callbackShaders.Clear();
            callbackQueues.Clear();
            try
            {
            Restore(); active.Clear();
            if (pins.Count != 0)
                throw new InvalidOperationException("房间绘制的自有材质仍被外部修改后的Renderer引用；已保留该引用，拒绝叠加新的换材质范围。");
            if (frame == null || frame.PositiveSurfaces == null) throw new ArgumentNullException(nameof(frame));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            List<Request> requests;
            HashSet<Key> requestKeys;
            List<Swap> plans;
            using (new ProfilingScope(null, perfPlanBuild))
            {
            requests = new List<Request>();
            requestKeys = new HashSet<Key>();
            plans = new List<Swap>();
            var seen = new HashSet<Renderer>();
            var fullMembers = new HashSet<Renderer>();
            foreach (var surface in frame.PositiveSurfaces)
                if (surface.Kind == RoomPositiveSurfaceKind.Full && surface.Renderer != null) fullMembers.Add(surface.Renderer);

            // 先完成全部计划与预算检查；此阶段不写任何Renderer材质数组。
            foreach (var surface in frame.PositiveSurfaces)
            {
                var renderer = surface.Renderer;
                if (renderer == null || fullMembers.Contains(renderer) || !seen.Add(renderer)) continue;
                Material[] originals=null;
                bool retained=terrain!=null&&terrain.TryPrepared(renderer,out originals);
                if(!retained){originals=renderer.sharedMaterials;terrainPlans.Remove(renderer);}
                if (!retained&&RoomPositiveRenderingLayers.RequiresIndependentDraw(surface,originals,planFamilies)) continue;
                if (renderer is SpriteRenderer sprite &&
                    (sprite.drawMode != SpriteDrawMode.Simple || sprite.maskInteraction != SpriteMaskInteraction.None)) continue;
                if (originals.Length > 16) throw new InvalidOperationException("单个房间表面超过16个材质槽，拒绝换材质。");
                uint sourceFamily = retained?RoomPositiveRenderingLayers.PartialLitBit:0; bool supported = originals.Length != 0;
                for (int i = 0; !retained&&i < originals.Length; ++i)
                {
                    originals[i] = ResolveOriginal(originals[i]);
                    uint next = RoomPositiveRenderingLayers.Classify(originals[i],planFamilies);
                    if (next == 0 || (sourceFamily != 0 && sourceFamily != next)) { supported = false; break; }
                    sourceFamily = next;
                }
                if (!supported) continue;
                uint family = surface.Kind == RoomPositiveSurfaceKind.Roof ? RoomPositiveRenderingLayers.RoofBit : sourceFamily;
                Swap plan=null;
                if(retained&&terrainPlans.TryGetValue(renderer,out var saved)&&SameReferences(saved.Original,originals))plan=saved;
                var keys = plan==null?new Key[originals.Length]:plan.Keys;
                for (int i = 0; i < originals.Length; ++i)
                {
                    var original = originals[i];
                    if(verifyCallbackBoundary&&!callbackShaders.ContainsKey(original))
                    {callbackShaders.Add(original,original.shader);callbackQueues.Add(original,EffectiveQueue(original));}
                    var key = plan==null?new Key(original, family, IsTransparent(original)):keys[i];
                    if(plan==null)keys[i] = key;
                    if (!requestKeys.Add(key)) continue;
                    if (requestKeys.Count > MaxBuckets)
                        throw new InvalidOperationException("房间正向绘制超过256个材质桶，拒绝部分换材质。");
                    Shader shader;int pass;
                    try { shader=resolver(original,family,out pass); }
                    finally { planFamilies.Clear(); }
                    if (shader == null || !shader.isSupported || pass < 0)
                        throw new InvalidOperationException("房间材质桶缺少有效源家族Shader或Pass：" + family);
                    requests.Add(new Request { Key = key, Shader = shader, Pass = pass, Order = requests.Count });
                }
                if(plan==null)
                {
                    plan=new Swap { Renderer = renderer, Original = originals, Written = new Material[originals.Length], Keys = keys };
                    if(retained)terrainPlans[renderer]=plan;
                }
                plans.Add(plan);
            }
            staleTerrainPlans.Clear();
            foreach(var pair in terrainPlans)if(pair.Key==null||!seen.Contains(pair.Key)||terrain==null||!terrain.TryPrepared(pair.Key,out _))staleTerrainPlans.Add(pair.Key);
            foreach(var renderer in staleTerrainPlans)terrainPlans.Remove(renderer);
            if(verifyCallbackBoundary)
            {
                // Arbitrary resolver delegates are a capture boundary, not a trusted receipt.
                // Re-read every completed layout and unique source after the last callback,
                // before any resource/Renderer write. Mutation refuses this preparation.
                foreach(var plan in plans)
                    if(plan.Renderer==null||!SameReferences(plan.Renderer.sharedMaterials,plan.Original))
                        throw new InvalidOperationException("Resolver callback changed a captured material layout; refuse before borrowing");
                foreach(var plan in plans)foreach(var key in plan.Keys)
                {
                    var material=key.Original;
                    uint family=RoomPositiveRenderingLayers.Classify(material);
                    uint expected=key.Family==RoomPositiveRenderingLayers.RoofBit?RoomPositiveRenderingLayers.PartialLitBit:key.Family;
                    if(material==null||material.shader!=callbackShaders[material]||EffectiveQueue(material)!=callbackQueues[material]||family!=expected||IsTransparent(material)!=key.Transparent)
                        throw new InvalidOperationException("Resolver callback changed a captured source family; refuse before borrowing");
                }
            }

            // 薄地面的共面层依赖原队列先后，不能把遍历顺序误当作者层序。
            requests.Sort((a, b) =>
            {
                int queueOrder = EffectiveQueue(a.Key.Original).CompareTo(EffectiveQueue(b.Key.Original));
                return queueOrder != 0 ? queueOrder : a.Order.CompareTo(b.Order);
            });
            }
            Prune(requestKeys);
            try
            {
                using (new ProfilingScope(null, perfResourceRefresh))
                {
                for (int i = 0; i < requests.Count; ++i)
                {
                    var request = requests[i];
                    if (!cache.TryGetValue(request.Key, out var bucket))
                    {
                        bucket = new Bucket { Original = request.Key.Original, Family = request.Key.Family, Transparent = request.Key.Transparent };
                        cache.Add(request.Key, bucket);
                    }
                    bucket.SourceShader = bucket.Original.shader;
                    bucket.Pass = request.Pass;
                    bucket.Queue = (bucket.Transparent ? 3100 : 2100) + i;
                    if (bucket.Clone == null)
                    {
                        bucket.Clone = new Material(bucket.Original) { name = bucket.Original.name + " [Room visibility source queue]", hideFlags = HideFlags.HideAndDontSave };
                        bucket.hasCopiedState = false;
                        originalsByClone.Add(bucket.Clone, bucket.Original);
                    }
                    if (bucket.Adapter == null)
                    {
                        bucket.Adapter = new Material(request.Shader) { name = bucket.Original.name + " [Room visibility draw adapter]", hideFlags = HideFlags.HideAndDontSave };
                        bucket.hasCopiedState = false;
                    }
                    else if (bucket.Adapter.shader != request.Shader)
                    { bucket.Adapter.shader = request.Shader; bucket.hasCopiedState = false; }
                    Refresh(bucket);
                    if (bucket.Pass >= bucket.Adapter.passCount)
                        throw new InvalidOperationException("房间材质桶Pass超出Shader实际范围：" + bucket.Pass);
                    active.Add(bucket);
                }
                }
                using (new ProfilingScope(null, perfBorrowWrites))
                {
                foreach (var plan in plans)
                {
                    for (int i = 0; i < plan.Keys.Length; ++i) plan.Written[i] = cache[plan.Keys[i]].Clone;
                    // 先登记再写入；中途异常仍能撤销此前真正写入的精确范围。
                    swaps.Add(plan);
                    plan.Renderer.sharedMaterials = plan.Written;
                }
                }
            }
            catch
            {
                Restore(); active.Clear();
                throw;
            }
                    }
            finally { planFamilies.Clear();callbackShaders.Clear();callbackQueues.Clear(); }
        }

        internal void RefreshAdapters()
        {
            foreach (var bucket in active)
            {
                if (bucket.Original == null || bucket.Clone == null || bucket.Adapter == null)
                    throw new InvalidOperationException("本帧材质桶的源或自有材质已被销毁，拒绝继续绘制。");
                if (bucket.Original.shader != bucket.SourceShader)
                    throw new InvalidOperationException("相机剔除后源材质改变了Shader身份；等待下一帧重新建立材质桶。");
                if (IsTransparent(bucket.Original) != bucket.Transparent)
                    throw new InvalidOperationException("相机剔除后源材质改变了队列类别；等待下一帧重新建立材质桶。");
            }
            // 所有源先通过检查，避免后续桶失效时前面的桶已改成另一份布局。
            foreach (var bucket in active) Refresh(bucket);
        }

        static bool IsTransparent(Material material) => EffectiveQueue(material) > 2500;

        static int EffectiveQueue(Material material)
        {
            int queue = material.renderQueue;
            if (queue < 0 && material.shader != null) queue = material.shader.renderQueue;
            return queue;
        }

        static void Refresh(Bucket bucket)
        {
            // Clone继续使用原Shader，故原光照Caster仍读取原合同；仅颜色终绘才使用Adapter。
            int sourceCrc = bucket.Original.ComputeCRC();
            // Clone会临时借给Renderer；只看源CRC会漏掉回调对自有副本的同引用改写。
            bool refresh = !bucket.hasCopiedState || sourceCrc != bucket.sourceCrc ||
                bucket.Clone.ComputeCRC() != bucket.cloneCrc || bucket.Adapter.ComputeCRC() != bucket.adapterCrc;
            if (refresh)
            {
                if (bucket.Clone.shader != bucket.Original.shader) bucket.Clone.shader = bucket.Original.shader;
                bucket.Clone.CopyPropertiesFromMaterial(bucket.Original);
                bucket.Adapter.CopyPropertiesFromMaterial(bucket.Original);
            }
            bool cloneQueueChanged = bucket.Clone.renderQueue != bucket.Queue;
            bool adapterQueueChanged = bucket.Adapter.renderQueue != bucket.Queue;
            if (cloneQueueChanged) bucket.Clone.renderQueue = bucket.Queue;
            if (adapterQueueChanged) bucket.Adapter.renderQueue = bucket.Queue;
            if (refresh || cloneQueueChanged) bucket.cloneCrc = bucket.Clone.ComputeCRC();
            if (refresh || adapterQueueChanged) bucket.adapterCrc = bucket.Adapter.ComputeCRC();
            bucket.sourceCrc = sourceCrc; bucket.hasCopiedState = true;
        }

        internal void Restore()
        {
            using (new ProfilingScope(null, perfRestore))
            {
            foreach (var swap in swaps)
            {
                var renderer = swap.Renderer;
                if (renderer == null) continue;
                var current = renderer.sharedMaterials;
                if (SameReferences(current, swap.Written)) renderer.sharedMaterials = swap.Original;
                else PinStillReferenced(renderer, current);
            }
            swaps.Clear(); ReapPins();
            }
        }

        static bool SameReferences(Material[] actual, Material[] expected)
        {
            if (actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; ++i) if (!ReferenceEquals(actual[i], expected[i])) return false;
            return true;
        }

        void PinStillReferenced(Renderer renderer, Material[] current)
        {
            foreach (var bucket in cache.Values)
            {
                if (bucket.Clone == null || !ContainsReference(current, bucket.Clone)) continue;
                if (!pins.TryGetValue(bucket, out var holders)) { holders = new HashSet<Renderer>(); pins.Add(bucket, holders); }
                holders.Add(renderer);
            }
        }

        static bool ContainsReference(Material[] values, Material material)
        {
            foreach (var value in values) if (ReferenceEquals(value, material)) return true;
            return false;
        }

        void ReapPins()
        {
            stalePins.Clear();
            foreach (var pin in pins)
            {
                staleHolders.Clear();
                foreach (var renderer in pin.Value)
                    if (renderer == null || !ContainsReference(renderer.sharedMaterials, pin.Key.Clone)) staleHolders.Add(renderer);
                foreach (var renderer in staleHolders) pin.Value.Remove(renderer);
                if (pin.Value.Count == 0) stalePins.Add(pin.Key);
            }
            foreach (var bucket in stalePins) pins.Remove(bucket);
        }

        void Prune(HashSet<Key> wanted)
        {
            staleKeys.Clear();
            foreach (var pair in cache)
                if (!wanted.Contains(pair.Key) && !pins.ContainsKey(pair.Value)) staleKeys.Add(pair.Key);
            foreach (var key in staleKeys) { DestroyBucket(cache[key]); cache.Remove(key); }
        }

        void DestroyBucket(Bucket bucket)
        {
            if (!ReferenceEquals(bucket.Clone, null)) originalsByClone.Remove(bucket.Clone);
            DestroyOwned(bucket.Clone); bucket.Clone = null;
            DestroyOwned(bucket.Adapter); bucket.Adapter = null;
        }

        static void DestroyOwned(Object value)
        { if (value != null) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }

        public void Dispose()
        {
            terrainPlans.Clear();staleTerrainPlans.Clear();
            Restore(); active.Clear(); staleKeys.Clear();
            foreach (var pair in cache)
            {
                if (pins.ContainsKey(pair.Value))
                {
                    DestroyOwned(pair.Value.Adapter); pair.Value.Adapter = null;
                }
                else staleKeys.Add(pair.Key);
            }
            foreach (var key in staleKeys) { DestroyBucket(cache[key]); cache.Remove(key); }
        }
    }
}
