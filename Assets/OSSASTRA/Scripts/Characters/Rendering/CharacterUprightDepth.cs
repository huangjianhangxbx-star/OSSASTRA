/* 脚本：CharacterUprightDepth
 * 功能：由CharacterRoot显式Prepare身体与左右独立Spine的竖直环境深度；不改变屏幕XY。
 * 边界：无Update/LateUpdate、输入/动作/移动、研究场路径；不写Mesh、alpha、骨架、原Material或MPB。
 * 协作：公开CustomMaterialOverride按原key包住既有effective材质；正式Mesh回执接住同帧新装备。
 * 生命周期：自有override/slot条件归还；外部接管不抢回；普通摄像机Begin/End只切自有uniform。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters.ViewSpace;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.Characters.Rendering
{
    [DisallowMultipleComponent]
    public sealed class CharacterUprightDepth : MonoBehaviour, ICharacterVisualDepth
    {
        [Tooltip("显式本角色身体SkeletonRenderer；不扫描命名对象。")]
        public SkeletonRenderer bodySkeleton;
        public Shader depthPlaneShader;
        public Shader spinePmaShader;
        public Shader spriteDepthShader;
        public Shader opaqueCoverageShader;

        private readonly List<SourceBinding> sources = new List<SourceBinding>(3);
        private readonly List<Camera> renderingCameras = new List<Camera>(2);
        private readonly SpriteUprightDepthMaterialBinding spriteBinding = new SpriteUprightDepthMaterialBinding();
        private Transform actor;
        private CharacterEquipmentSpineView equipment;
        private CharacterEquipmentView spriteEquipment;
        private Camera gameplayCamera;
        private bool prepared, callbacksBound, disposed, statusDirty;
        private float nextStatusTime;
        private static readonly int Foot = Shader.PropertyToID("_VisualDepthFoot");
        private static readonly int Normal = Shader.PropertyToID("_VisualDepthNormal");
        private static readonly int Enabled = Shader.PropertyToID("_VisualDepthEnabled");

        public string LastResult { get; private set; } = "未初始化";
        public int BoundSourceCount => sources.Count;
        public int OwnedVariantCount
        {
            get { int count = spriteBinding.OwnedMaterialCount; for (int i = 0; i < sources.Count; ++i) count += sources[i].entries.Count; return count; }
        }
        public int OwnedCoverageMaterialCount
        { get { int count=0; for(int i=0;i<sources.Count;++i)count+=sources[i].coverage.OwnedMaterialCount;return count; } }
        public Camera PreparedCamera => prepared ? gameplayCamera : null;
        public bool OwnsRenderer(Renderer renderer)
        { for(int i=0;i<sources.Count;++i)if(sources[i].coverage.OwnsRenderer(renderer))return true;return false; }

        public void Initialize(Transform owner)
        {
            Dispose();
            if (owner == null || owner != transform)
                throw new ArgumentException("显示深度模块必须位于其CharacterRoot本体。", nameof(owner));
            actor = owner;
            equipment = owner.GetComponent<CharacterEquipmentSpineView>();
            spriteEquipment = owner.GetComponent<CharacterEquipmentView>();
            disposed = false;
            statusDirty = true;
            LastResult = "已初始化；等待根Prepare";
        }

        public void Prepare(Camera camera)
        {
            if (disposed || actor == null || !isActiveAndEnabled || !actor.gameObject.activeInHierarchy)
            { Suspend(); return; }
            if (camera == null || camera.cameraType != CameraType.Game || !camera.isActiveAndEnabled)
            { Suspend(); LastResult = "没有有效GameplayCamera；原深度"; return; }
            if (bodySkeleton == null || !bodySkeleton.transform.IsChildOf(actor))
            { Suspend(); LastResult = "需要本角色显式bodySkeleton；原深度"; return; }

            // 最多三个正式端口，常态不创建HashSet、临时数组或LINQ枚举器。
            SkeletonRenderer left = equipment == null || equipment.LeftSkeleton == null ? null : equipment.LeftSkeleton.GetComponent<SkeletonRenderer>();
            SkeletonRenderer right = equipment == null || equipment.RightSkeleton == null ? null : equipment.RightSkeleton.GetComponent<SkeletonRenderer>();
            for (int i = sources.Count - 1; i >= 0; --i)
            {
                var source = sources[i].source;
                if (source == null || source != bodySkeleton && source != left && source != right)
                { Release(sources[i]); sources.RemoveAt(i); statusDirty = true; }
            }
            KeepSource(bodySkeleton);
            if (left != null && left != bodySkeleton) KeepSource(left);
            if (right != null && right != bodySkeleton && right != left) KeepSource(right);
            // 唯一正式只读端口；不反射旧私有字段，不搜索名字或其它Sprite。
            spriteBinding.Bind(spriteEquipment == null ? null : spriteEquipment.PresentedRenderer, actor, spriteDepthShader);
            statusDirty |= gameplayCamera != camera || !prepared;
            gameplayCamera = camera;
            prepared = true;
            BindCallbacks();
            SetCameraMode(null);
            RefreshStatus();
        }

        private SourceBinding FindSource(ISkeletonRenderer renderer)
        {
            for (int i = 0; i < sources.Count; ++i)
                if (ReferenceEquals(sources[i].source, renderer)) return sources[i];
            return null;
        }

        private void KeepSource(SkeletonRenderer source)
        {
            if (source == null || !source.transform.IsChildOf(actor) || source.gameObject.scene != actor.gameObject.scene) return;
            var binding = FindSource(source);
            if (binding == null)
            {
                var renderer = source.GetComponent<MeshRenderer>();
                if (renderer == null) return;
                binding = new SourceBinding { source = source, renderer = renderer, overrides = source.CustomMaterialOverride, meshReady = MeshReady };
                sources.Add(binding);
                source.OnMeshAndMaterialsUpdated += binding.meshReady;
                Reconcile(binding);
                statusDirty = true;
            }
            else
            {
                bool changed = CheckOwnership(binding);
                if (changed || binding.data != source.skeletonDataAsset) Reconcile(binding);
            }
            SyncOriginalProperties(binding, false);
            if(binding.coverageShader!=opaqueCoverageShader)
            {
                binding.coverage.Suspend();binding.coverageShader=opaqueCoverageShader;
                if(opaqueCoverageShader!=null)binding.coverage.Bind(binding.renderer,actor,opaqueCoverageShader);
            }
            if(opaqueCoverageShader!=null)binding.coverage.Synchronize();
        }

        private void MeshReady(ISkeletonRenderer renderer)
        {
            if (!prepared || disposed) return;
            var binding = FindSource(renderer);
            if (binding == null || binding.source == null || binding.renderer == null) return;
            bool changed = CheckOwnership(binding) || binding.data != binding.source.skeletonDataAsset;
            // GetSharedMaterials(List)复用容量；仅正式hook/真实材质变化读写slot，不在Prepare每帧分配sharedMaterials数组。
            binding.renderer.GetSharedMaterials(binding.slots);
            for (int i = 0; i < binding.slots.Count && !changed; ++i)
            {
                var material = binding.slots[i];
                if (material == null || IsKnownSlot(binding, material)) continue;
                if (!binding.unsupported.Contains(material)) changed = true;
            }
            if (changed) Reconcile(binding);
            else AssignOwnedSlots(binding, true);
            if(binding.coverageShader!=null)binding.coverage.Synchronize();
            SetCameraMode(renderingCameras.Count == 0 ? null : renderingCameras[renderingCameras.Count - 1]);
        }

        private static bool IsKnownSlot(SourceBinding binding, Material material)
        {
            for (int i = 0; i < binding.entries.Count; ++i)
            {
                var entry = binding.entries[i];
                if (entry.variant == material || entry.effective == material || entry.original == material) return true;
            }
            if (binding.blocked.Contains(material)) return true;
            foreach (var original in binding.blocked)
                if (binding.overrides.TryGetValue(original, out var replacement) && replacement == material) return true;
            return false;
        }

        private bool CheckOwnership(SourceBinding binding)
        {
            bool changed = false;
            for (int i = binding.entries.Count - 1; i >= 0; --i)
            {
                var entry = binding.entries[i];
                bool foreign = !binding.overrides.TryGetValue(entry.original, out var current) || current != entry.variant;
                bool sourceShaderChanged = entry.effective == null || entry.effective.shader != entry.sourceShader;
                if (!foreign && !sourceShaderChanged) continue;
                if (foreign) binding.blocked.Add(entry.original);
                ReleaseEntry(binding, entry);
                binding.entries.RemoveAt(i);
                changed = true;
            }
            statusDirty |= changed;
            return changed;
        }

        private static void AddKey(SourceBinding binding, Material material)
        {
            if (material != null && binding.keySet.Add(material)) binding.keys.Add(material);
        }

        private static void CollectOriginalKeys(SourceBinding binding)
        {
            binding.keys.Clear(); binding.keySet.Clear();
            var data = binding.source.skeletonDataAsset;
            if (data != null)
            {
                for (int i = 0; i < data.atlasAssets.Length; ++i)
                {
                    var atlas = data.atlasAssets[i];
                    if (atlas != null) foreach (var material in atlas.Materials) AddKey(binding, material);
                }
                var blends = data.blendModeMaterials;
                if (blends != null)
                {
                    for (int i = 0; i < blends.additiveMaterials.Count; ++i) AddKey(binding, blends.additiveMaterials[i].material);
                    for (int i = 0; i < blends.multiplyMaterials.Count; ++i) AddKey(binding, blends.multiplyMaterials[i].material);
                    for (int i = 0; i < blends.screenMaterials.Count; ++i) AddKey(binding, blends.screenMaterials[i].material);
                }
            }
            // 先完成持久scratch收集，再改字典；不在字典枚举期间写入。
            foreach (var pair in binding.overrides) AddKey(binding, pair.Key);
        }

        private Shader SelectShader(Material source)
        {
            if (source == null || source.shader == null) return null;
            if (source.shader.name == "Ossastra/MapResearch/DepthPlane")
                return depthPlaneShader != null && depthPlaneShader.isSupported ? depthPlaneShader : null;
            if (source.shader.name == "Spine/Skeleton")
                return spinePmaShader != null && spinePmaShader.isSupported ? spinePmaShader : null;
            return null;
        }

        private static Entry FindEntry(SourceBinding binding, Material original)
        {
            for (int i = 0; i < binding.entries.Count; ++i)
                if (binding.entries[i].original == original) return binding.entries[i];
            return null;
        }

        private void Reconcile(SourceBinding binding)
        {
            CheckOwnership(binding);
            CollectOriginalKeys(binding);
            for (int i = 0; i < binding.keys.Count; ++i)
            {
                var original = binding.keys[i];
                if (binding.blocked.Contains(original) || FindEntry(binding, original) != null) continue;
                bool hadPrior = binding.overrides.TryGetValue(original, out var prior);
                var effective = hadPrior ? prior : original;
                var shader = SelectShader(effective);
                if (shader == null) { binding.unsupported.Add(original); if (effective != null) binding.unsupported.Add(effective); continue; }
                binding.unsupported.Remove(original); binding.unsupported.Remove(effective);
                var variant = new Material(shader) { name = effective.name + " [Actor visual depth]", hideFlags = HideFlags.HideAndDontSave };
                binding.entries.Add(new Entry { original = original, effective = effective, sourceShader = effective.shader, prior = prior, hadPrior = hadPrior, variant = variant });
                binding.overrides[original] = variant;
                var touched = binding.source.CustomMaterialOverride;
            }
            binding.data = binding.source.skeletonDataAsset;
            SyncOriginalProperties(binding, true);
            AssignOwnedSlots(binding, false);
            statusDirty = true;
        }

        private static void SyncOriginalProperties(SourceBinding binding, bool force)
        {
            float now = Time.realtimeSinceStartup;
            if (!force && now < binding.nextPropertySync) return;
            binding.nextPropertySync = now + .25f;
            for (int i = 0; i < binding.entries.Count; ++i)
            {
                var entry = binding.entries[i];
                if (entry.effective == null || entry.variant == null) continue;
                entry.variant.CopyPropertiesFromMaterial(entry.effective);
                entry.variant.renderQueue = entry.effective.renderQueue;
                entry.variant.SetFloat(Enabled, 0);
            }
        }

        private static void AssignOwnedSlots(SourceBinding binding, bool alreadyRead)
        {
            if (binding.renderer == null) return;
            if (!alreadyRead) binding.renderer.GetSharedMaterials(binding.slots);
            bool changed = false;
            for (int i = 0; i < binding.slots.Count; ++i)
                for (int j = 0; j < binding.entries.Count; ++j)
                {
                    var entry = binding.entries[j];
                    if (binding.slots[i] != entry.effective && binding.slots[i] != entry.original) continue;
                    if (!binding.overrides.TryGetValue(entry.original, out var mapped) || mapped != entry.variant) continue;
                    binding.slots[i] = entry.variant; changed = true; break;
                }
            if (changed) binding.renderer.SetSharedMaterials(binding.slots);
        }

        private void RefreshStatus()
        {
            float now = Time.realtimeSinceStartup;
            if (!statusDirty && now < nextStatusTime) return;
            nextStatusTime = now + .25f;
            statusDirty = false;
            int rejected = 0;
            for (int i = 0; i < sources.Count; ++i) rejected += sources[i].unsupported.Count + sources[i].blocked.Count;
            LastResult = "身体/装备源 " + sources.Count + "，自有变体 " + OwnedVariantCount +
                (rejected == 0 ? "；等待GameplayCamera" : "；保留原深度的未知/外部占用材质 " + rejected);
        }

        private void BindCallbacks()
        {
            if (callbacksBound) return;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            callbacksBound = true;
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        { renderingCameras.Add(camera); SetCameraMode(camera); }
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            for(int i=0;i<sources.Count;++i)sources[i].coverage.EndCamera(camera);
            if (renderingCameras.Count > 0 && renderingCameras[renderingCameras.Count - 1] == camera)
                renderingCameras.RemoveAt(renderingCameras.Count - 1);
            else renderingCameras.Clear();
            SetCameraMode(renderingCameras.Count == 0 ? null : renderingCameras[renderingCameras.Count - 1]);
        }
        private void SetCameraMode(Camera camera)
        {
            bool active = prepared && actor != null && camera != null && camera == gameplayCamera;
            var normal = active ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : Vector3.forward;
            active &= normal.sqrMagnitude > .000001f;
            if (active) normal.Normalize();
            var foot = actor == null ? Vector3.zero : actor.position;
            for (int i = 0; i < sources.Count; ++i)
                for (int j = 0; j < sources[i].entries.Count; ++j)
                {
                    var variant = sources[i].entries[j].variant;
                    if (variant == null) continue;
                    variant.SetVector(Foot, foot); variant.SetVector(Normal, normal); variant.SetFloat(Enabled, active ? 1 : 0);
                }
            spriteBinding.SetCameraMode(active,foot,normal);
            for(int i=0;i<sources.Count;++i)
                sources[i].coverage.BeginCamera(camera,active?gameplayCamera:null,foot,normal);
        }

        public void Suspend()
        {
            prepared = false; gameplayCamera = null;
            if (callbacksBound)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                RenderPipelineManager.endCameraRendering -= EndCamera;
                callbacksBound = false;
            }
            renderingCameras.Clear();
            for (int i = sources.Count - 1; i >= 0; --i) Release(sources[i]);
            sources.Clear();
            spriteBinding.Suspend();
            LastResult = "已归还自有显示深度；原材质/原深度";
        }

        private static void ReleaseEntry(SourceBinding binding, Entry entry)
        {
            Material restore = entry.hadPrior ? entry.prior : entry.original;
            bool hasCurrent = binding.overrides.TryGetValue(entry.original, out var current);
            bool owns = entry.variant != null && hasCurrent && current == entry.variant;
            if (owns)
            {
                if (entry.hadPrior) binding.overrides[entry.original] = entry.prior;
                else binding.overrides.Remove(entry.original);
                if (binding.source != null) { var touched = binding.source.CustomMaterialOverride; }
            }
            else restore = hasCurrent ? current : entry.original;
            if (binding.renderer != null)
            {
                binding.renderer.GetSharedMaterials(binding.slots);
                bool changed = false;
                for (int i = 0; i < binding.slots.Count; ++i)
                    if (binding.slots[i] == entry.variant) { binding.slots[i] = restore; changed = true; }
                if (changed)
                {
                    binding.renderer.SetSharedMaterials(binding.slots);
                    if (binding.source != null) { var touched = binding.source.CustomMaterialOverride; }
                }
            }
            if (entry.variant != null)
            {
                entry.variant.SetFloat(Enabled, 0);
                if (Application.isPlaying) Object.Destroy(entry.variant); else Object.DestroyImmediate(entry.variant);
            }
        }
        private static void Release(SourceBinding binding)
        {
            if (!ReferenceEquals(binding.source, null)) binding.source.OnMeshAndMaterialsUpdated -= binding.meshReady;
            binding.coverage.Dispose();
            for (int i = binding.entries.Count - 1; i >= 0; --i) ReleaseEntry(binding, binding.entries[i]);
            binding.entries.Clear();
        }
        public void Dispose() { Suspend(); spriteBinding.Dispose();actor = null; equipment = null; spriteEquipment=null;disposed = true; }
        private void OnDisable() { Suspend(); }
        private void OnDestroy() { Dispose(); }

        private sealed class SourceBinding
        {
            public SkeletonRenderer source;
            public MeshRenderer renderer;
            public SkeletonDataAsset data;
            public Dictionary<Material, Material> overrides;
            public SkeletonRendererDelegate meshReady;
            public float nextPropertySync;
            public Shader coverageShader;
            public readonly CharacterOpaqueCoverage coverage=new CharacterOpaqueCoverage();
            public readonly List<Entry> entries = new List<Entry>(4);
            public readonly List<Material> slots = new List<Material>(4);
            public readonly List<Material> keys = new List<Material>(8);
            public readonly HashSet<Material> keySet = new HashSet<Material>();
            public readonly HashSet<Material> blocked = new HashSet<Material>();
            public readonly HashSet<Material> unsupported = new HashSet<Material>();
        }
        private sealed class Entry
        {
            public Material original, effective, prior, variant;
            public Shader sourceShader;
            public bool hadPrior;
        }
    }
}
