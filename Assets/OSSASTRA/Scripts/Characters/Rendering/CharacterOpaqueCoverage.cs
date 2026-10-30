/*
 * 功能：为显式Spine图形源中近乎完全不透明的像素补充真实深度覆盖，保留原颜色、alpha及队列。
 * 边界：不改原Mesh／Material／MPB，不订阅Update或渲染事件；仅支持Spine/Skeleton与本轮竖直PMA变体。
 * 生命周期：由新场模块显式Bind／Synchronize／Begin／End；Suspend与Dispose只释放自己的无物理图形。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.Characters.Rendering
{
    public sealed class CharacterOpaqueCoverage : IDisposable
    {
        const int Queue = 2499;
        const float MinimumAlpha = .999f;
        const string OriginalShader = "Spine/Skeleton";
        const string UprightShader = "Ossastra/Characters/UprightSpinePma";
        static readonly int Foot = Shader.PropertyToID("_VisualDepthFoot");
        static readonly int Normal = Shader.PropertyToID("_VisualDepthNormal");
        static readonly int DepthEnabled = Shader.PropertyToID("_VisualDepthEnabled");
        static readonly int CoverageEnabled = Shader.PropertyToID("_CharacterCoverageEnabled");
        static readonly int CoverageMinimum = Shader.PropertyToID("_CharacterCoverageMinimum");
        readonly List<Material> readSlots = new List<Material>(4);
        readonly List<Material> sourceSlots = new List<Material>(4);
        readonly List<Shader> sourceShaders = new List<Shader>(4);
        readonly List<bool> sourceUpright = new List<bool>(4);
        readonly Dictionary<Shader, byte> sourceKinds = new Dictionary<Shader, byte>(4);
        readonly List<Material> materials = new List<Material>(4);
        readonly List<MaterialPropertyBlock> slotBlocks = new List<MaterialPropertyBlock>(4);
        readonly MaterialPropertyBlock globalBlock = new MaterialPropertyBlock();
        MeshRenderer source, ownedRenderer;
        MeshFilter sourceFilter, ownedFilter;
        Transform actor;
        Shader shader;
        GameObject ownedObject;
        Camera activeCamera;
        Vector3 foot, normal;
        bool synchronized;

        public MeshRenderer OwnedRenderer => ownedRenderer;
        public int OwnedMaterialCount => materials.Count;
        public string LastError { get; private set; }
        public bool OwnsRenderer(Renderer renderer) => !ReferenceEquals(renderer, null) && ReferenceEquals(renderer, ownedRenderer);

        public void Bind(MeshRenderer source, Transform actor, Shader shader)
        {
            if (this.source == source && this.actor == actor && this.shader == shader)
            { Synchronize(); return; }
            Suspend();
            if (shader == null) { LastError = null; return; }
            if (source == null || actor == null || !source.transform.IsChildOf(actor) || source.gameObject.scene != actor.gameObject.scene)
            { LastError = "不透明覆盖需要本角色显式图形源。"; return; }
            if (!shader.isSupported) { LastError = "不透明覆盖shader不受支持；保留原图。"; return; }
            this.source = source; this.actor = actor; this.shader = shader;
            sourceFilter = source.GetComponent<MeshFilter>();
            Synchronize();
        }

        public void Synchronize()
        {
            if (source == null || actor == null || shader == null)
            { ReleaseOwned(); return; }
            if (!source.transform.IsChildOf(actor) || source.gameObject.scene != actor.gameObject.scene)
            { LastError = "图形源已离开所属角色；原图保持不变。"; ReleaseOwned(); return; }
            if(source.sortingLayerID!=0||source.GetComponentInParent<SortingGroup>()!=null)
            { LastError="当前覆盖只验证Default排序层且无SortingGroup；保留原图。";ReleaseOwned();return; }
            try
            {
                if (sourceFilter == null) sourceFilter = source.GetComponent<MeshFilter>();
                Mesh mesh = sourceFilter == null ? null : sourceFilter.sharedMesh;
                source.GetSharedMaterials(readSlots);
                if (mesh == null || readSlots.Count == 0 || readSlots.Count != mesh.subMeshCount)
                {
                    synchronized = false; Hide();
                    if (ownedFilter != null) ownedFilter.sharedMesh = null;
                    LastError = "等待正式Mesh与材质slot一致。"; return;
                }
                for (int i = 0; i < readSlots.Count; i++)
                    if (!Supported(readSlots[i]))
                    { LastError = "原材质不属于已验证Spine PMA范围；没有创建覆盖。"; ReleaseOwned(); return; }
                bool rebuild = ownedRenderer == null || readSlots.Count != sourceSlots.Count;
                for (int i = 0; i < readSlots.Count && !rebuild; i++)
                    rebuild = readSlots[i] != sourceSlots[i] || readSlots[i].shader != sourceShaders[i];
                if (rebuild) Rebuild();
                // Spine can alternate its two generated Mesh objects every frame. Only rebind the
                // shared read-only pointer: never clone/destroy its Mesh or recreate materials for this.
                if (ownedFilter.sharedMesh != mesh) ownedFilter.sharedMesh = mesh;
                ownedObject.layer = source.gameObject.layer;
                ownedObject.transform.localPosition = Vector3.zero;
                ownedObject.transform.localRotation = Quaternion.identity;
                ownedObject.transform.localScale = Vector3.one;
                ownedRenderer.enabled = source.enabled;
                ownedRenderer.sortingLayerID = 0;
                ownedRenderer.sortingOrder = short.MinValue;
                ownedRenderer.renderingLayerMask = source.renderingLayerMask;
                for (int i = 0; i < materials.Count; i++)
                {
                    materials[i].CopyPropertiesFromMaterial(readSlots[i]);
                    materials[i].renderQueue = Queue;
                    materials[i].SetFloat(CoverageMinimum, MinimumAlpha);
                    materials[i].SetFloat(CoverageEnabled, 0);
                }
                synchronized = true; ApplyBlocks(); UpdateVisibility(); LastError = null;
            }
            catch (Exception error)
            {
                LastError = "覆盖初始化失败，已释放自有图形：" + error.Message;
                ReleaseOwned();
            }
        }

        public void BeginCamera(Camera current, Camera gameplayCamera, Vector3 foot, Vector3 normal)
        {
            Hide(); activeCamera = null; DisableOwnedUniforms();
            if (current == null || current != gameplayCamera || current.cameraType != CameraType.Game) return;
            if (!Finite(foot) || !Finite(normal)) { LastError = "覆盖脚点或法线无效；保留原图。"; return; }
            if(Mathf.Abs(normal.y)>1e-5f||Mathf.Abs(normal.sqrMagnitude-1)>1e-4f)
            {LastError="覆盖需要调用者已校验的水平单位法线；保留原图。";return;}
            this.foot = foot;
            // The source color pass already uses this validated normal. Re-normalizing by even
            // one ULP can make equal SV_Depth fail LEqual against our own opaque coverage.
            this.normal = normal;
            activeCamera = current;
            Synchronize();
        }

        public void EndCamera(Camera current)
        {
            // No independently managed camera stack: the caller owns nested-camera ordering.
            activeCamera = null; Hide(); DisableOwnedUniforms();
        }

        public void Suspend()
        {
            activeCamera = null; ReleaseOwned();
            source = null; sourceFilter = null; actor = null; shader = null;
            readSlots.Clear(); sourceKinds.Clear(); LastError = null;
        }
        public void Dispose() { Suspend(); }

        bool Supported(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported) return false;
            var sourceShader=material.shader;
            if(!sourceKinds.TryGetValue(sourceShader,out var kind))
            {
                string name=sourceShader.name;kind=(byte)(name==OriginalShader?1:name==UprightShader?2:0);sourceKinds.Add(sourceShader,kind);
            }
            return kind!=0;
        }
        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        void Rebuild()
        {
            var staged = new List<Material>(readSlots.Count);
            try
            {
                for (int i = 0; i < readSlots.Count; i++)
                {
                    var material = new Material(shader)
                    { name = readSlots[i].name + " [opaque coverage]", hideFlags = HideFlags.HideAndDontSave, renderQueue = Queue };
                    staged.Add(material);
                    material.CopyPropertiesFromMaterial(readSlots[i]); material.renderQueue = Queue;
                    material.SetFloat(CoverageMinimum, MinimumAlpha); material.SetFloat(CoverageEnabled, 0);
                }
                if (ownedObject == null)
                {
                    ownedObject = new GameObject("__CharacterOpaqueCoverage") { hideFlags = HideFlags.HideAndDontSave };
                    ownedObject.transform.SetParent(source.transform, false);
                    ownedFilter = ownedObject.AddComponent<MeshFilter>(); ownedRenderer = ownedObject.AddComponent<MeshRenderer>();
                    ownedRenderer.forceRenderingOff = true;
                    ownedRenderer.shadowCastingMode = ShadowCastingMode.Off; ownedRenderer.receiveShadows = false;
                    ownedRenderer.lightProbeUsage = LightProbeUsage.Off; ownedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                Hide();
                // Clear previous per-slot blocks while their indices still belong to the old layout.
                for (int i = 0; i < materials.Count; i++) ownedRenderer.SetPropertyBlock(null, i);
                ownedRenderer.SetSharedMaterials(staged);
                for (int i = 0; i < materials.Count; i++) DestroyOwned(materials[i]);
                materials.Clear(); materials.AddRange(staged); staged.Clear();
                sourceSlots.Clear(); sourceShaders.Clear();sourceUpright.Clear();
                for (int i = 0; i < readSlots.Count; i++) { sourceSlots.Add(readSlots[i]); sourceShaders.Add(readSlots[i].shader);sourceUpright.Add(sourceKinds[readSlots[i].shader]==2); }
                while (slotBlocks.Count < readSlots.Count) slotBlocks.Add(new MaterialPropertyBlock());
            }
            catch
            {
                for (int i = 0; i < staged.Count; i++) DestroyOwned(staged[i]);
                throw;
            }
        }

        void ApplyBlocks()
        {
            if (source == null || ownedRenderer == null) return;
            bool enabled = activeCamera != null && synchronized;
            source.GetPropertyBlock(globalBlock);
            bool anyUpright=false;for(int i=0;i<sourceUpright.Count;i++)anyUpright|=sourceUpright[i];
            SetOwnedUniforms(globalBlock, enabled, anyUpright); ownedRenderer.SetPropertyBlock(globalBlock);
            for (int i = 0; i < materials.Count; i++)
            {
                var block = slotBlocks[i]; source.GetPropertyBlock(block, i);
                // Unity uses the per-material block instead of the global one. Only an empty original
                // per-slot block inherits the original global block; do not merge nonempty slot blocks.
                if (block.isEmpty) source.GetPropertyBlock(block);
                SetOwnedUniforms(block, enabled, sourceUpright[i]);
                ownedRenderer.SetPropertyBlock(block, i);
            }
        }
        void SetOwnedUniforms(MaterialPropertyBlock block, bool enabled, bool upright)
        {
            block.SetVector(Foot, new Vector4(foot.x, foot.y, foot.z, 1));
            block.SetVector(Normal, new Vector4(normal.x, normal.y, normal.z, 0));
            block.SetFloat(DepthEnabled, enabled && upright && normal.sqrMagnitude > .000001f ? 1 : 0);
            block.SetFloat(CoverageEnabled, enabled ? 1 : 0);
            block.SetFloat(CoverageMinimum, MinimumAlpha);
        }
        void UpdateVisibility()
        {
            if (ownedRenderer == null) return;
            bool visible = synchronized && activeCamera != null && source != null &&
                source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
            ownedRenderer.forceRenderingOff = !visible;
        }
        void Hide() { if (ownedRenderer != null) ownedRenderer.forceRenderingOff = true; }
        void DisableOwnedUniforms()
        {
            if(ownedRenderer==null)return;
            globalBlock.SetFloat(DepthEnabled,0);globalBlock.SetFloat(CoverageEnabled,0);ownedRenderer.SetPropertyBlock(globalBlock);
            for(int i=0;i<materials.Count;i++){slotBlocks[i].SetFloat(DepthEnabled,0);slotBlocks[i].SetFloat(CoverageEnabled,0);ownedRenderer.SetPropertyBlock(slotBlocks[i],i);}
        }
        void ReleaseOwned()
        {
            Hide(); synchronized = false; activeCamera = null;
            if (ownedFilter != null) ownedFilter.sharedMesh = null;
            DestroyOwned(ownedObject); ownedObject = null; ownedRenderer = null; ownedFilter = null;
            for (int i = 0; i < materials.Count; i++) DestroyOwned(materials[i]);
            materials.Clear(); sourceSlots.Clear(); sourceShaders.Clear();sourceUpright.Clear(); globalBlock.Clear();
            for (int i = 0; i < slotBlocks.Count; i++) slotBlocks[i].Clear();
        }
        static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
