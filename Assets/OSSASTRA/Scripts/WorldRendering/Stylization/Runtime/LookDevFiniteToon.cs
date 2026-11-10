/* 功能：同一 Renderer / slot 的有限原版、三档与已验证插件材质对照。
 * 边界：最多24个明确登记的3D不透明槽（约4个多材质物体）；不搜索全场、不改角色/草、不改原材质、不生成天气或许可。
 * 生命周期：仅明确切档时换引用；禁用时只恢复仍由本组件占用的槽；不在Update复制材质或写MPB。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.WorldRendering.Stylization
{
    public enum LookDevToonVariant { Original, ThreeBand, RealToon }

    [Serializable]
    public sealed class LookDevToonBinding
    {
        public MeshRenderer renderer;
        [Min(0)] public int materialSlot;
        public Material original, threeBand, realToon;
        public bool outline;
        public bool smoothOutlineNormalsInUv3;
    }

    [DisallowMultipleComponent]
    public sealed class LookDevFiniteToon : MonoBehaviour
    {
        static readonly Dictionary<Camera, LookDevFiniteToon> owners = new Dictionary<Camera, LookDevFiniteToon>();
        [SerializeField] Camera targetCamera;
        [SerializeField] LookDevToonBinding[] bindings = Array.Empty<LookDevToonBinding>();
        [SerializeField] LookDevToonVariant variant;
        [SerializeField, Tooltip("Only after the current Room resolver executes the actual plugin adapter.")]
        bool realToonRoomBackendValidated = false;
        [SerializeField, Tooltip("Exact local Room wrapper keyword variants only; empty retains the v1.1 Player block.")]
        Material[] realToonPlayerRetention = Array.Empty<Material>();
        bool playerRetentionReady;
        string playerRetentionError = "No exact Room wrapper retention configured.";
        [SerializeField] bool outlineEnabled;
        [SerializeField, Range(.5f, 1.5f)] float outlineOutputPixels = 1;
        [SerializeField] Color outlineDarkSrgb = new Color(8f/255,13f/255,45f/255,1);
        readonly List<Material> slots = new List<Material>(8);

        public IReadOnlyList<LookDevToonBinding> Bindings => bindings;
        public LookDevToonVariant Variant => variant;
        public bool RealToonAvailable => realToonRoomBackendValidated && (Application.isEditor || playerRetentionReady);
        public bool PlayerRetentionReady => playerRetentionReady;
        public string PlayerRetentionError => playerRetentionError;
        public bool OutlineEnabled => isActiveAndEnabled && outlineEnabled && variant != LookDevToonVariant.Original;
        public float OutlineOutputPixels => Mathf.Clamp(outlineOutputPixels, .5f, 1.5f);
        public Color OutlineDarkLinear => outlineDarkSrgb.linear;

        public static bool TryGet(Camera camera, out LookDevFiniteToon controller)
        {
            controller = null;
            if (camera == null || camera.cameraType != CameraType.Game || !owners.TryGetValue(camera,out var candidate) ||
                candidate == null || !candidate.isActiveAndEnabled) return false;
            controller = candidate; return true;
        }
        public void BindCamera(Camera camera)
        {
            ReleaseCamera();
            if (camera == null || camera.gameObject.scene != gameObject.scene)throw new ArgumentException("Finite Toon needs an explicit same-scene camera.");
            if (owners.TryGetValue(camera,out var previous) && previous != null && previous != this)
                throw new InvalidOperationException("Finite Toon camera already has an owner.");
            targetCamera = camera; owners[camera] = this;
        }
        void ReleaseCamera()
        {
            if (targetCamera != null && owners.TryGetValue(targetCamera,out var previous) && previous == this)
                owners.Remove(targetCamera);
        }

        static bool SafeVoxel(Material material)
        {
            return material != null && material.shader != null && material.shader.name == "Ossastra/WorldRendering/VoxelLit" &&
                material.HasProperty("_LookDevToonMain") && material.GetFloat("_Surface") == 0 &&
                (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") < .5f) &&
                !material.IsKeywordEnabled("_ALPHATEST_ON") && material.renderQueue < 2500;
        }

        static bool SafeOriginal(Material material)
        {
            if (material == null || material.shader == null) return false;
            string name = material.shader.name;
            return (name == "Ossastra/WorldRendering/VoxelLit" || name == "Universal Render Pipeline/Lit" ||
                name == "Universal Render Pipeline/Simple Lit") && material.renderQueue < 2500 &&
                (!material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0) &&
                (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") < .5f) &&
                !material.IsKeywordEnabled("_ALPHATEST_ON");
        }

        public Material SelectedMaterial(LookDevToonBinding binding)
        {
            return variant == LookDevToonVariant.ThreeBand ? binding.threeBand :
                variant == LookDevToonVariant.RealToon ? binding.realToon : binding.original;
        }

        public bool TrySetVariant(LookDevToonVariant next, out string error)
        {
            error = "";
            if (next == LookDevToonVariant.RealToon && !Application.isEditor && !RefreshPlayerRetention(out error))
            { error = "Exact Room wrapper retention is not ready; kept the current variant. " + error; return false; }
            if (bindings == null || bindings.Length > 24) { error = "Finite Toon needs 0..24 explicit 3D slots."; return false; }
            if (next == LookDevToonVariant.RealToon && !realToonRoomBackendValidated)
            { error = "RealToon actual Room adapter has not been validated; kept the current variant."; return false; }
            // Validate every target before changing any material reference.
            for (int i = 0; i < bindings.Length; ++i)
            {
                var binding = bindings[i];
                if (binding == null || binding.renderer == null || binding.renderer.GetComponent<MeshFilter>() == null)
                { error = "Missing finite 3D target at " + i; return false; }
                if (!SafeOriginal(binding.original) || !SafeVoxel(binding.threeBand) ||
                    binding.threeBand.GetFloat("_LookDevToonMain") < .5f)
                { error = "Expected original / opt-in Voxel materials at " + i; return false; }
                if (next == LookDevToonVariant.RealToon && (binding.realToon == null || binding.realToon.shader == null ||
                    binding.realToon.shader.name != "Universal Render Pipeline/RealToon/Version 5/Default/Default"))
                { error = "Expected the audited RealToon URP material at " + i; return false; }
                binding.renderer.GetSharedMaterials(slots);
                if (binding.materialSlot < 0 || binding.materialSlot >= slots.Count)
                { error = "Missing material slot at " + i; return false; }
                var current = slots[binding.materialSlot];
                if (current != binding.original && current != binding.threeBand && current != binding.realToon)
                { error = "The finite target was changed by another writer; kept its reference: " + i; return false; }
                for (int j = 0; j < i; ++j)
                    if (bindings[j].renderer == binding.renderer && bindings[j].materialSlot == binding.materialSlot)
                    { error = "Duplicate Renderer / slot at " + i; return false; }
            }
            for (int i = 0; i < bindings.Length; ++i)
            {
                var binding = bindings[i];
                binding.renderer.GetSharedMaterials(slots);
                slots[binding.materialSlot] = next == LookDevToonVariant.ThreeBand ? binding.threeBand :
                    next == LookDevToonVariant.RealToon ? binding.realToon : binding.original;
                binding.renderer.SetSharedMaterials(slots);
            }
            variant = next;
            return true;
        }

        const string VendorShader = "Universal Render Pipeline/RealToon/Version 5/Default/Default";
        const string RoomShader = "Hidden/Ossastra/LookDev/RealToonRoomLocal";
        static string KeywordSignature(Material source, Shader wrapper, bool? outline)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var name in source.shaderKeywords)
                if (wrapper.keywordSpace.FindKeyword(name).isValid) names.Add(name);
            if (outline.HasValue) { names.Remove("N_F_O_ON"); if (outline.Value) names.Add("N_F_O_ON"); }
            return string.Join(";", names);
        }
        bool CheckPlayerRetention(Material[] retained, out string error)
        {
            error = "";
            if (bindings == null || bindings.Length == 0 || bindings.Length > 24 || retained == null || retained.Length == 0 || retained.Length > 48)
            { error = "Expected 1..24 explicit slots and 1..48 exact wrapper variants."; return false; }
            Shader wrapper = retained[0] != null ? retained[0].shader : null;
            if (wrapper == null || !wrapper.isSupported || wrapper.name != RoomShader)
            { error = "Retention must reference the actual local Room shader, not the vendor source."; return false; }
            var required = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in bindings)
            {
                if (b == null || b.renderer == null || b.realToon == null || b.realToon.shader == null ||
                    b.realToon.shader.name != VendorShader || b.realToon.GetTag("LookDevFiniteToon",false,"") != "LOOKDEV-01")
                { error = "Missing audited finite source binding."; return false; }
                required.Add(KeywordSignature(b.realToon,wrapper,false));
                if (b.outline) required.Add(KeywordSignature(b.realToon,wrapper,true));
            }
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in retained)
            {
                // This engine can retain the vendor property cache after CopyPropertiesFromMaterial.
                // Check these project-only declarations on the exact shader; keep pass/keyword checks.
                if (m == null || m.shader != wrapper || m.FindPass("ForwardLit") < 0 || m.FindPass("Outline") < 0 ||
                    m.shader.FindPropertyIndex("_RoomSightRoofMode") < 0 || m.shader.FindPropertyIndex("_EnvironmentFullSurface") < 0)
                {
                    bool hasShader = m != null && m.shader != null;
                    error = "Retention has a missing/mismatched Room shader or actual pass. material=" + (m != null ? m.name : "<null>") +
                        "; shader=" + (hasShader ? m.shader.name : "<null>") +
                        "; Forward=" + (hasShader ? m.FindPass("ForwardLit") : -1) + "; Outline=" + (hasShader ? m.FindPass("Outline") : -1) +
                        "; roofDeclaration=" + (hasShader ? m.shader.FindPropertyIndex("_RoomSightRoofMode") : -1) +
                        "; fullDeclaration=" + (hasShader ? m.shader.FindPropertyIndex("_EnvironmentFullSurface") : -1);
                    return false;
                }
                string key = KeywordSignature(m,wrapper,null);
                if (!required.Contains(key) || !found.Add(key))
                { error = "Retention has an unrelated or duplicate local keyword signature."; return false; }
                bool outline = m.IsKeywordEnabled("N_F_O_ON");
                if (!m.HasProperty("_N_F_O") || (m.GetFloat("_N_F_O") > .5f) != outline ||
                    (outline && !m.GetShaderPassEnabled("SRPDefaultUnlit")))
                { error = "Outline retained property/keyword/pass state does not match."; return false; }
            }
            if (!required.SetEquals(found))
            { error = "A finite Forward or Outline local keyword signature is not retained."; return false; }
            return true;
        }
        // Configuration proof only. Actual Player rendering/costs remain a separate Root acceptance step.
        public bool ConfigurePlayerRetention(Material[] retained, out string error)
        {
            if (!CheckPlayerRetention(retained,out error)) return false;
            realToonPlayerRetention = (Material[])retained.Clone();
            playerRetentionReady = true; playerRetentionError = ""; return true;
        }
        public bool RefreshPlayerRetention(out string error)
        {
            playerRetentionReady = CheckPlayerRetention(realToonPlayerRetention,out error);
            playerRetentionError = error; return playerRetentionReady;
        }
        public string GetRealToonRuntimeIdentity()
        {
            var first = realToonPlayerRetention != null && realToonPlayerRetention.Length > 0 ? realToonPlayerRetention[0] : null;
            var groups = bindings == null ? Array.Empty<string>() : bindings.Where(b=>b!=null && b.realToon!=null && b.realToon.shader!=null)
                .Select(b=>b.realToon.shader.name+" ["+string.Join(";",b.realToon.shaderKeywords.OrderBy(k=>k,StringComparer.Ordinal))+"]").Distinct().ToArray();
            return "variant="+variant+"; retentionPrepared="+playerRetentionReady+"; sourceGroups="+string.Join(" | ",groups)+
                "; expectedRoom="+RoomShader+"; retained="+(realToonPlayerRetention==null?0:realToonPlayerRetention.Length)+
                "; Forward="+(first!=null&&first.shader!=null?first.FindPass("ForwardLit"):-1)+"; Outline="+(first!=null&&first.shader!=null?first.FindPass("Outline"):-1)+
                "; configuration identity only, actual draw/visual acceptance separate; error="+playerRetentionError;
        }
        public void Restore()
        {
            if (bindings == null) return;
            foreach (var binding in bindings)
            {
                if (binding == null || binding.renderer == null || binding.original == null) continue;
                binding.renderer.GetSharedMaterials(slots);
                if (binding.materialSlot < 0 || binding.materialSlot >= slots.Count) continue;
                var current = slots[binding.materialSlot];
                if (current != binding.threeBand && current != binding.realToon) continue;
                slots[binding.materialSlot] = binding.original;
                binding.renderer.SetSharedMaterials(slots);
            }
            variant = LookDevToonVariant.Original;
        }

        public void SetOutline(bool enabled, float outputPixels = 1)
        { outlineEnabled = enabled; outlineOutputPixels = Mathf.Clamp(outputPixels, .5f, 1.5f); }
        public void SetOutlineTheme(Color darkSrgb) => outlineDarkSrgb = darkSrgb;
        void OnEnable() { RefreshPlayerRetention(out _); if(targetCamera!=null)BindCamera(targetCamera); }
        void OnDisable() { Restore(); ReleaseCamera(); }
        void OnDestroy() { Restore(); ReleaseCamera(); }
    }
}
