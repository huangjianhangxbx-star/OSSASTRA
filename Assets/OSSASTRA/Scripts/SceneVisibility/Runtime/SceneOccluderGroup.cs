/* 功能：显式登记一件环境模型的显示成员、未淡出检测代理及支撑身份。
 * 边界：不查全场、不改物理或地图作者，不用世界根自动收纳所有模型。
 * 协作：Root消费本组；高台缓存重建后由Binding.cacheRoot重新解析成员，旧Renderer不保留。
 */
using System;
using System.Collections.Generic;
using Ossastra.GroundSurfaces;
using Ossastra.GroundResearch;
using UnityEngine;

namespace Ossastra.SceneVisibility
{
    [DisallowMultipleComponent]
    public sealed class SceneOccluderGroup : MonoBehaviour
    {
        [Header("明确的一件模型（不能指定整个世界）")]
        public string groupLabel = "环境模型";
        public Renderer[] members = Array.Empty<Renderer>();
        [Tooltip("只做显示检测；始终查询未淡出的原Collider，透明不会关碰撞。显式Trigger也可使用。")]
        public Collider[] volumes = Array.Empty<Collider>();
        [Header("可选高台登记与承载保护")]
        public GroundSurfaceBinding platform;
        [Tooltip("只在平台缓存重建/作者Map身份改变时重取该Map显式装饰；不逐帧扫描场景。")]
        public bool includePlatformDecorations = true;
        [Tooltip("主控或被保护伙伴站立时必须保留的表面；高台缓存顶盖自动属于此类。")]
        public Renderer[] supportRenderers = Array.Empty<Renderer>();
        public Collider[] supportColliders = Array.Empty<Collider>();
        [Header("整件淡出只对有限小模型显式开放")]
        public bool allowWholeFade;
        [Range(0, 1)] public float minimumOpacity;
        readonly List<Renderer> resolved = new List<Renderer>();
        Renderer[] cached = Array.Empty<Renderer>();
        int signature;
        int volumeSignature;
        Collider[] queryVolumes = Array.Empty<Collider>();
        Collider[] transientProxies = Array.Empty<Collider>();
        SpriteRenderer[] platformSprites = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] spriteMembers = Array.Empty<SpriteRenderer>();
        GroundSurfaceMeshCache lastCache;
        int lastRebuild = -1, lastMapId;
        bool lastIncludeDecorations;
        GroundDecorationOrientation decorationOrientation;
        public int GeometryVersion { get; private set; }
        public bool SupportProtected { get; private set; }
        public string Status { get; private set; } = "未登记";
        public Renderer[] Resolved => cached;
        public SpriteRenderer[] SpriteMembers => spriteMembers;
        // 保存/刷图会替换capCollider；检测不保存这个短命实例的序列化快照。
        public Collider[] QueryVolumes
        {
            get
            {
                var cap = platform == null ? null : platform.capCollider;
                int next = cap == null ? 17 : cap.GetInstanceID();
                unchecked { if (volumes != null) foreach (var volume in volumes) next = next * 31 + (volume == null ? 0 : volume.GetInstanceID()); }
                unchecked { foreach (var volume in transientProxies) next = next * 31 + (volume == null ? 0 : volume.GetInstanceID()); }
                if (next != volumeSignature)
                {
                    var list = new List<Collider>();
                    if (volumes != null) foreach (var volume in volumes) if (volume != null && !list.Contains(volume)) list.Add(volume);
                    if (cap != null && !list.Contains(cap)) list.Add(cap);
                    foreach (var volume in transientProxies) if (volume != null && !list.Contains(volume)) list.Add(volume);
                    queryVolumes = list.ToArray(); volumeSignature = next;
                }
                return queryVolumes;
            }
        }
        // 这些只属于独立查询PhysicsScene，绝不作为主世界碰撞体登记/序列化。
        public void SetTransientProxies(Collider[] proxies) { transientProxies = proxies ?? Array.Empty<Collider>(); }
        public void ClearTransientProxies(Collider[] expected)
        { if (ReferenceEquals(transientProxies, expected)) transientProxies = Array.Empty<Collider>(); }

        // SceneView可能先于主相机刷新派生视觉；捕获前调用同一个既有写者，不能复制姿态算法。
        public void PrepareForCamera(Camera camera)
        { if (decorationOrientation != null) decorationOrientation.RefreshForCamera(camera); }

        public bool Supports(Vector3 foot)
        {
            if (platform != null && platform.isActiveAndEnabled && platform.ContainsWorldPoint(foot)) return true;
            Ray ray = new Ray(foot + Vector3.up * .15f, Vector3.down);
            if (supportColliders != null)
                foreach (var collider in supportColliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                        collider.Raycast(ray, out RaycastHit hit, .3f) && hit.normal.y > .5f) return true;
            return false;
        }

        public bool Resolve(bool protectSupport)
        {
            SupportProtected = protectSupport;
            resolved.Clear();
            if (members != null) foreach (var member in members) Add(member, protectSupport);
            var cache = platform == null || platform.cacheRoot == null ? null : platform.cacheRoot.GetComponent<GroundSurfaceMeshCache>();
            int mapId = platform == null || platform.map == null ? 0 : platform.map.GetInstanceID();
            int rebuild = cache == null ? -1 : cache.rebuildCount;
            if (lastCache != cache || lastRebuild != rebuild || lastMapId != mapId || lastIncludeDecorations != includePlatformDecorations)
            {
                platformSprites = includePlatformDecorations && mapId != 0 ? platform.map.GetComponentsInChildren<SpriteRenderer>(true) : Array.Empty<SpriteRenderer>();
                decorationOrientation = mapId == 0 ? null : platform.map.GetComponent<GroundDecorationOrientation>();
                lastCache = cache; lastRebuild = rebuild; lastMapId = mapId; lastIncludeDecorations = includePlatformDecorations;
            }
            if (!protectSupport && cache != null && cache.chunks != null)
                foreach (var chunk in cache.chunks) if (chunk != null) Add(chunk.renderer, false);
            foreach (var sprite in platformSprites) Add(sprite, false);
            int next = protectSupport ? 73 : 17;
            unchecked
            {
                foreach (var member in resolved)
                {
                    next = next * 31 + member.GetInstanceID();
                    var filter = member.GetComponent<MeshFilter>();
                    next = next * 31 + (filter == null || filter.sharedMesh == null ? 0 : filter.sharedMesh.GetInstanceID());
                }
                next = next * 31 + (cache == null ? 0 : cache.rebuildCount);
            }
            if (next != signature)
            {
                cached = resolved.ToArray(); signature = next; ++GeometryVersion;
                var sprites = new List<SpriteRenderer>();
                foreach (var member in cached) if (member is SpriteRenderer sprite) sprites.Add(sprite);
                spriteMembers = sprites.ToArray();
                Status = cached.Length == 0 ? "无可绘制成员" : protectSupport ? "承载面保留；只处理前景成员，整组淡出禁用" : "已登记 " + cached.Length + " 个显示成员";
            }
            return cached.Length > 0;
        }

        void Add(Renderer member, bool protectSupport)
        {
            if (member == null || resolved.Contains(member)) return;
            if (protectSupport && supportRenderers != null && Array.IndexOf(supportRenderers, member) >= 0) return;
            resolved.Add(member);
        }
    }
}
