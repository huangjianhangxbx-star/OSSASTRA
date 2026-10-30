/*
 * 功能：为明确的主相机捕获最多四个登记遮挡组，保持原Lit颜色，以自有真实深度完成柔边显示。
 * 边界：只读原几何／材质／MPB，不改角色、碰撞层或全局URP；只支持已验证材质与标准投影。
 * 生命周期：调用者安排Begin／End；三个条件恢复作用域与精确Dispose只管理本模块创建的图形资源。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.SceneVisibility.Rendering
{
    /// <summary>
    /// Explicit main-camera backend. Its caller owns callback ordering, detection, supported-scene
    /// policy and reserved-layer validation. It never subscribes, edits a source layer or moves an actor.
    /// </summary>
    public sealed class FiniteGroupCapture : IDisposable
    {
        const int MaxGroups = 4, MaxRenderers = 256;
        sealed class Node { public Transform Source, Copy, Parent; }
        sealed class Source
        {
            public Renderer Renderer;
            public MeshRenderer Copy;
            public MeshFilter Filter;
            public SpriteRenderer SpriteRenderer;
            public Sprite Sprite;
            public Texture2D SpriteTexture;
            public Vector2[] SpriteBaseVertices;
            public Vector3[] SpriteVertices;
            public Color32[] SpriteColors;
            public bool FlipX, FlipY;
            public Color Tint;
            public Mesh SourceMesh, Mesh;
            public Material[] SourceMaterials, Materials, DepthMaterials;
            public MaterialPropertyBlock Global = new MaterialPropertyBlock();
            public MaterialPropertyBlock[] Slots;
            public bool Visible;
        }
        sealed class Group
        {
            public int Id, Version;
            public GameObject Root;
            public Source[] Sources;
            public Node[] Nodes;
            public RenderTexture Color, Depth;
            public RegisteredGroupFrame Frame;
            public bool InFrame, Active;
        }

        readonly Camera main, auxiliary;
        readonly GameObject auxiliaryObject, proxyRoot;
        readonly int captureLayer, displayLayer;
        static readonly int[] ColorIds = Ids("_GroupColor"), DepthIds = Ids("_GroupDepth"), CircleAIds = Ids("_CircleA"), CircleBIds = Ids("_CircleB"), StrengthIds = Ids("_Strength"), SupportIds = Ids("_Support");
        static readonly int ActiveId = Shader.PropertyToID("_GroupActive"), OrderId = Shader.PropertyToID("_GroupOrder"), ScreenId = Shader.PropertyToID("_VisScreen"), SupportStrengthId = Shader.PropertyToID("_SupportStrength");
        static readonly int SpriteMainTexId = Shader.PropertyToID("_MainTex");
        readonly Shader dataShader;
        readonly Group[] slots = new Group[MaxGroups];
        readonly List<Material> sharedMaterialsScratch = new List<Material>(8);
        readonly RendererScope sourceScope = new RendererScope(MaxRenderers);
        readonly RendererScope copyScope = new RendererScope(MaxRenderers);
        readonly RendererScope proxyScope = new RendererScope(MaxGroups + 1);
        readonly Material[] displays = new Material[MaxGroups + 1];
        readonly MeshRenderer[] proxies = new MeshRenderer[MaxGroups + 1];
        readonly CommandBuffer commands;
        readonly Mesh quad;
        bool disposed, inside;
        int width, height;

        public string LastError { get; private set; }
        public int CapturedGroupCount { get; private set; }
        public int OwnedRTCount
        {
            get
            {
                if(disposed)return 0;int count=0;
                for(int i=0;i<MaxGroups;i++)if(slots[i]!=null){if(slots[i].Color!=null)count++;if(slots[i].Depth!=null)count++;}
                return count;
            }
        }
        public int OwnedMaterialCount
        {
            get
            {
                if(disposed)return 0;int count=0;for(int i=0;i<displays.Length;i++)if(displays[i]!=null)count++;
                for(int i=0;i<MaxGroups;i++)if(slots[i]!=null&&slots[i].Sources!=null)for(int s=0;s<slots[i].Sources.Length;s++)
                {
                    var source=slots[i].Sources[s];if(source==null)continue;
                    if(source.Materials!=null)for(int m=0;m<source.Materials.Length;m++)if(source.Materials[m]!=null)count++;
                    if(source.DepthMaterials!=null)for(int m=0;m<source.DepthMaterials.Length;m++)if(source.DepthMaterials[m]!=null)count++;
                }
                return count;
            }
        }
        public int OwnedMeshCount
        {
            get
            {
                if(disposed)return 0;int count=quad!=null?1:0;
                for(int i=0;i<MaxGroups;i++)if(slots[i]!=null&&slots[i].Sources!=null)for(int s=0;s<slots[i].Sources.Length;s++)if(slots[i].Sources[s]!=null&&slots[i].Sources[s].Mesh!=null)count++;
                return count;
            }
        }

        // copyDepthShader is the finite known-material device-depth DATA adapter, not a global-depth copy.
        public FiniteGroupCapture(Camera mainCamera, int captureLayer, Shader opaqueShader, Shader featherShader, Shader copyDepthShader)
        {
            if (mainCamera == null) throw new ArgumentNullException(nameof(mainCamera));
            if (captureLayer < 0 || captureLayer > 31) throw new ArgumentOutOfRangeException(nameof(captureLayer));
            if (opaqueShader == null || featherShader == null || copyDepthShader == null ||
                !opaqueShader.isSupported || !featherShader.isSupported || !copyDepthShader.isSupported)
                throw new InvalidOperationException("All three verified visibility shaders are required.");
            main = mainCamera; this.captureLayer = captureLayer; dataShader = copyDepthShader;
            displayLayer = FirstVisibleLayer(mainCamera.cullingMask & ~(1 << captureLayer));
            try
            {
            commands=new CommandBuffer { name = "SceneVisibility owned depth data" };
            auxiliaryObject = OwnedObject("__VIS_DisabledAuxiliary");
            auxiliary = auxiliaryObject.AddComponent<Camera>(); auxiliary.enabled = false;
            var extra = auxiliaryObject.AddComponent<UniversalAdditionalCameraData>();
            extra.renderType = CameraRenderType.Base; extra.renderPostProcessing = false;
            extra.volumeLayerMask = 0; extra.requiresDepthOption = CameraOverrideOption.Off;
            extra.requiresColorOption = CameraOverrideOption.Off; extra.renderShadows = false;
            proxyRoot = OwnedObject("__VIS_CameraDisplay");
            quad = new Mesh { name = "VIS viewport quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateBounds();
            for (int i = 0; i < displays.Length; i++)
            {
                displays[i] = new Material(i == 0 ? opaqueShader : featherShader)
                { name = "VIS display " + i, hideFlags = HideFlags.HideAndDontSave, renderQueue = i == 0 ? 2000 : 2999 + i };
                if(i>0)displays[i].SetFloat("_Rank", i - 1);
                var go = OwnedObject("__VIS_Display_" + i); go.layer = displayLayer; go.transform.SetParent(proxyRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad; var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = displays[i]; renderer.forceRenderingOff = true;
                if(i>0)renderer.sortingOrder=short.MaxValue;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; proxies[i] = renderer;
            }
            }
            catch { Dispose(); throw; }
        }

        public void BeginCamera(ScriptableRenderContext context, Camera camera, RegisteredGroupFrame[] groups, int groupCount)
        {
            if (disposed || camera != main) return;
            if (inside) { LastError = "Previous main camera scope was not ended; restored without rendering."; RestoreOwnedState(); return; }
            RestoreOwnedState(); CapturedGroupCount = 0;
            try
            {
                if (inside) throw new InvalidOperationException("Visibility backend cannot be reentered.");
                if (groupCount < 0 || groupCount > MaxGroups || (groupCount > 0 && (groups == null || groups.Length < groupCount)))
                    throw new InvalidOperationException("At most four explicit group frames are supported.");
                if ((main.cullingMask & (1 << captureLayer)) != 0)
                    throw new InvalidOperationException("The new-scene gameplay camera must exclude the reserved capture layer.");
                if (main.rect != new Rect(0, 0, 1, 1))
                    throw new InvalidOperationException("This verified backend requires a full camera viewport.");
                ValidateProjection();
                if (UniversalRenderPipeline.asset == null || Mathf.Abs(UniversalRenderPipeline.asset.renderScale - 1) > .0001f)
                    throw new InvalidOperationException("Capture requires the verified URP renderScale=1 contract.");
                width = main.pixelWidth; height = main.pixelHeight;
                if (width < 1 || height < 1 || width > 4096 || height > 4096)
                    throw new InvalidOperationException("Camera dimensions are outside the bounded capture allocation.");
                int total = 0;
                for (int i = 0; i < groupCount; i++)
                {
                    ValidateFrame(groups[i]); total += groups[i].Members.Length;
                    for (int j = 0; j < i; j++) if (groups[i].StableId == groups[j].StableId)
                        throw new InvalidOperationException("Stable group IDs must be unique.");
                    for (int m = 0; m < groups[i].Members.Length; m++)
                    {
                        for (int j = 0; j <= i; j++) for (int n = 0; n < (j == i ? m : groups[j].Members.Length); n++)
                            if (groups[i].Members[m] == groups[j].Members[n]) throw new InvalidOperationException("A source renderer cannot belong to two captures.");
                    }
                }
                if (total > MaxRenderers) throw new InvalidOperationException("Finite source renderer limit exceeded.");
                for (int s = 0; s < MaxGroups; s++) if (slots[s] != null) slots[s].InFrame = false;
                for (int i = 0; i < groupCount; i++)
                {
                    int slot = Find(groups[i].StableId);
                    if (slot >= 0) slots[slot].InFrame = true;
                }
                for (int s = 0; s < MaxGroups; s++) if (slots[s] != null && !slots[s].InFrame) { Release(slots[s]); slots[s] = null; }
                for (int i = 0; i < groupCount; i++)
                {
                    var frame = groups[i]; int slot = Find(frame.StableId);
                    if (slot < 0) { slot = Empty(); slots[slot] = Build(frame); }
                    else if (NeedsRefresh(slots[slot], frame))
                    {
                        var replacement = Build(frame); Release(slots[slot]); slots[slot] = replacement;
                    }
                    var group = slots[slot]; group.Frame = frame; group.InFrame = true;
                    bool any = false;
                    for (int n = 0; n < group.Nodes.Length; n++) CopyNode(group.Nodes[n]);
                    for (int n = 0; n < group.Sources.Length; n++)
                    {
                        var source = group.Sources[n];SyncMaterials(source);if(source.SpriteRenderer!=null)SynchronizeSprite(source);
                        source.Visible = source.Renderer.enabled && source.Renderer.gameObject.activeInHierarchy && !source.Renderer.forceRenderingOff && (main.cullingMask & (1 << source.Renderer.gameObject.layer)) != 0 && (source.SpriteRenderer==null||source.Sprite!=null);
                        any |= source.Visible; SyncBlocks(source);
                    }
                    group.Active = any && (frame.WholeFade > 0 || frame.CircleA.Strength > 0 || frame.CircleB.Strength > 0);
                    if (group.Active) EnsureTargets(group);
                }
                inside = true; ConfigureAuxiliary();
                for (int s = 0; s < MaxGroups; s++)
                {
                    var group = slots[s]; if (group == null || !group.Active) continue;
                    for (int n = 0; n < group.Sources.Length; n++) if (group.Sources[n].Visible) sourceScope.Set(group.Sources[n].Renderer, true);
                    try
                    {
                        for (int n = 0; n < group.Sources.Length; n++) if (group.Sources[n].Visible) copyScope.Set(group.Sources[n].Copy, false);
                        auxiliary.targetTexture = group.Color;
                        #pragma warning disable 0618
                        UniversalRenderPipeline.RenderSingleCamera(context, auxiliary);
                        #pragma warning restore 0618
                    }
                    finally { copyScope.Dispose(); }
                    DrawDepth(context, group); CapturedGroupCount++;
                }
                BindDisplays(); PositionDisplays();
                if (CapturedGroupCount > 0) for (int p = 0; p < proxies.Length; p++) proxyScope.Set(proxies[p], false);
                LastError = null;
            }
            catch (Exception error) { LastError = error.Message; CapturedGroupCount = 0; RestoreOwnedState(); }
        }

        public void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == main) RestoreOwnedState(); }
        public void RestoreOwnedState() { proxyScope.Dispose(); copyScope.Dispose(); sourceScope.Dispose(); inside = false; }

        void ValidateFrame(RegisteredGroupFrame frame)
        {
            if (frame.StableId == 0 || frame.Members == null || frame.Members.Length == 0)
                throw new InvalidOperationException("Each finite group needs a nonzero stable ID and explicit members.");
            ValidateCircle(frame.CircleA); ValidateCircle(frame.CircleB); ValidateCircle(frame.PreserveSupport);
            if (!Finite(frame.WholeFade) || frame.WholeFade < 0 || frame.WholeFade > 1) throw new InvalidOperationException("Whole fade must be finite 0..1.");
            for (int i = 0; i < frame.Members.Length; i++)
            {
                var renderer=frame.Members[i];
                if(renderer==null||renderer.gameObject.layer==captureLayer||renderer.gameObject.scene!=main.gameObject.scene)
                    throw new InvalidOperationException("Only explicit same-scene source renderers outside the private layer are supported.");
                if(renderer is SpriteRenderer sprite)ValidateSprite(sprite);
                else if(!(renderer is MeshRenderer)||renderer.GetComponent<MeshFilter>()==null)
                    throw new InvalidOperationException("Only MeshRenderer and the verified Simple DecorationCutout SpriteRenderer are supported.");
            }
        }
        static void ValidateCircle(CaptureCircle circle)
        {
            if (!Finite(circle.Strength) || circle.Strength < 0 || circle.Strength > 1 ||
                !Finite(circle.CenterViewport.x) || !Finite(circle.CenterViewport.y) ||
                !Finite(circle.InnerPixels) || !Finite(circle.OuterPixels) || circle.InnerPixels < 0 ||
                (circle.Strength > 0 && circle.OuterPixels <= circle.InnerPixels))
                throw new InvalidOperationException("Circle parameters must be finite, ordered and have strength 0..1.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void ValidateProjection()
        {
            var shift=main.lensShift;
            if(main.usePhysicalProperties||shift.x!=0||shift.y!=0)
                throw new InvalidOperationException("Physical/lens-shift camera projections are outside the verified visibility scope.");
            float size=main.orthographicSize,aspect=main.aspect;
            var expected=main.orthographic?Matrix4x4.Ortho(-size*aspect,size*aspect,-size,size,main.nearClipPlane,main.farClipPlane):
                Matrix4x4.Perspective(main.fieldOfView,aspect,main.nearClipPlane,main.farClipPlane);
            var actual=main.projectionMatrix;
            for(int i=0;i<16;i++)if(!Finite(actual[i])||!Finite(expected[i])||Mathf.Abs(actual[i]-expected[i])>1e-5f)
                throw new InvalidOperationException("Off-center/custom projection is outside the verified visibility scope; original rendering retained.");
        }
        int Find(int id) { for (int i = 0; i < MaxGroups; i++) if (slots[i] != null && slots[i].Id == id) return i; return -1; }
        int Empty() { for (int i = 0; i < MaxGroups; i++) if (slots[i] == null) return i; throw new InvalidOperationException("No free finite capture slot."); }
        bool NeedsRefresh(Group group, RegisteredGroupFrame frame)
        {
            if (group.Version != frame.GeometryVersion || group.Sources.Length != frame.Members.Length) return true;
            for (int i = 0; i < group.Sources.Length; i++) if (group.Sources[i].Renderer != frame.Members[i] ||
                group.Sources[i].SpriteRenderer==null&&(group.Sources[i].Filter == null || group.Sources[i].Filter.sharedMesh != group.Sources[i].SourceMesh)) return true;
            for(int i=0;i<group.Sources.Length;i++)
            {
                var source=group.Sources[i];source.Renderer.GetSharedMaterials(sharedMaterialsScratch);
                if(sharedMaterialsScratch.Count!=source.SourceMaterials.Length)return true;
                for(int m=0;m<sharedMaterialsScratch.Count;m++){ValidateMaterial(sharedMaterialsScratch[m]);if(sharedMaterialsScratch[m]!=source.SourceMaterials[m]||sharedMaterialsScratch[m].shader!=source.Materials[m].shader)return true;}
            }
            for (int i = 0; i < group.Nodes.Length; i++) if (group.Nodes[i].Source == null || group.Nodes[i].Source.parent != group.Nodes[i].Parent) return true;
            return false;
        }
        Group Build(RegisteredGroupFrame frame)
        {
            var group = new Group { Id = frame.StableId, Version = frame.GeometryVersion, Root = OwnedObject("__VIS_Group_" + frame.StableId), Frame = frame };
            var nodes = new Dictionary<Transform, Node>(); group.Sources = new Source[frame.Members.Length];
            try
            {
                for (int i = 0; i < frame.Members.Length; i++)
                {
                    var renderer = frame.Members[i]; var sprite=renderer as SpriteRenderer;var filter = sprite==null?renderer.GetComponent<MeshFilter>():null; var materials = renderer.sharedMaterials;
                    if (sprite==null&&(filter.sharedMesh == null || materials.Length != filter.sharedMesh.subMeshCount))
                        throw new InvalidOperationException("Source mesh/submesh/material mapping is invalid.");
                    if(sprite!=null&&(materials.Length!=1||materials[0]==null||materials[0].shader==null||materials[0].shader.name!="Ossastra/GroundWorkshop/DecorationCutout"))
                        throw new InvalidOperationException("Sprite capture requires exactly one verified opaque DecorationCutout material.");
                    for (int m = 0; m < materials.Length; m++) ValidateMaterial(materials[m]);
                    var source = new Source { Renderer = renderer, Filter = filter, SpriteRenderer=sprite,SourceMesh = filter==null?null:filter.sharedMesh, SourceMaterials=materials };
                    group.Sources[i] = source;
                    source.Mesh = sprite==null?Object.Instantiate(filter.sharedMesh):new Mesh{name="VIS Simple Sprite geometry"}; source.Mesh.hideFlags = HideFlags.HideAndDontSave;
                    source.Materials = new Material[materials.Length]; source.DepthMaterials = new Material[materials.Length]; source.Slots = new MaterialPropertyBlock[materials.Length];
                    for (int m = 0; m < materials.Length; m++)
                    {
                        source.Materials[m] = new Material(materials[m]) { hideFlags = HideFlags.HideAndDontSave };
                        var depth = new Material(dataShader) { hideFlags = HideFlags.HideAndDontSave }; source.DepthMaterials[m] = depth;
                        depth.CopyPropertiesFromMaterial(materials[m]); bool thin = materials[m].shader.name == "Ossastra/GroundSurfaces/ThinTop";bool decoration=materials[m].shader.name=="Ossastra/GroundWorkshop/DecorationCutout";
                        depth.SetFloat("_VisDataMode", decoration?3:thin ? 1 : 0); depth.SetFloat("_Cull", thin||decoration ? 0 : materials[m].GetFloat("_Cull"));
                        depth.SetFloat("_VisAlphaClip", materials[m].IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0); source.Slots[m] = new MaterialPropertyBlock();
                    }
                    var transform = Clone(renderer.transform, group, nodes); transform.gameObject.AddComponent<MeshFilter>().sharedMesh = source.Mesh;
                    var copy = transform.gameObject.AddComponent<MeshRenderer>(); source.Copy = copy; copy.sharedMaterials = source.Materials;
                    copy.forceRenderingOff = true; copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
                    copy.renderingLayerMask = renderer.renderingLayerMask; copy.lightProbeUsage = renderer.lightProbeUsage;
                    copy.reflectionProbeUsage = renderer.reflectionProbeUsage; copy.probeAnchor = renderer.probeAnchor;
                    copy.lightmapIndex = renderer.lightmapIndex; copy.lightmapScaleOffset = renderer.lightmapScaleOffset;
                    copy.realtimeLightmapIndex = renderer.realtimeLightmapIndex; copy.realtimeLightmapScaleOffset = renderer.realtimeLightmapScaleOffset;
                    copy.sortingLayerID = renderer.sortingLayerID; copy.sortingOrder = renderer.sortingOrder;
                }
                group.Nodes = new Node[nodes.Count]; nodes.Values.CopyTo(group.Nodes, 0); return group;
            }
            catch { Release(group); throw; }
        }
        static void ValidateMaterial(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported || material.renderQueue >= 2500)
                throw new InvalidOperationException("Only verified opaque/cutout materials may be captured; arbitrary transparency is unsupported.");
            if (material.shader.name == "Ossastra/GroundSurfaces/ThinTop"||material.shader.name=="Ossastra/GroundWorkshop/DecorationCutout") return;
            if (material.shader.name != "Universal Render Pipeline/Lit" || material.GetFloat("_Surface") != 0 ||
                material.GetFloat("_ZWrite") != 1 || material.GetFloat("_SrcBlend") != (float)BlendMode.One || material.GetFloat("_DstBlend") != (float)BlendMode.Zero)
                throw new InvalidOperationException("Unknown shader or blended Lit material has no verified finite depth adapter.");
        }
        static void SyncMaterials(Source source)
        {
            // Material identity can stay stable while authoring/gameplay changes its constants or
            // keywords. Refresh only our existing variants; source materials remain read-only.
            for(int m=0;m<source.SourceMaterials.Length;m++)
            {
                var original=source.SourceMaterials[m];ValidateMaterial(original);
                var color=source.Materials[m];color.CopyPropertiesFromMaterial(original);
                color.renderQueue=original.renderQueue;color.enableInstancing=original.enableInstancing;
                var depth=source.DepthMaterials[m];depth.CopyPropertiesFromMaterial(original);
                bool thin=original.shader.name=="Ossastra/GroundSurfaces/ThinTop";
                bool decoration=original.shader.name=="Ossastra/GroundWorkshop/DecorationCutout";
                // CopyProperties does not replace the owned data shader. Its private adapter mode
                // must follow the original shader/keyword, never an arbitrary source property.
                depth.SetFloat("_VisDataMode",decoration?3:thin?1:0);
                depth.SetFloat("_Cull",thin||decoration?0:original.GetFloat("_Cull"));
                depth.SetFloat("_VisAlphaClip",original.IsKeywordEnabled("_ALPHATEST_ON")?1:0);
            }
        }
        static void ValidateSprite(SpriteRenderer sprite)
        {
            if(sprite.drawMode!=SpriteDrawMode.Simple||sprite.maskInteraction!=SpriteMaskInteraction.None)
                throw new InvalidOperationException("Sprite capture supports only Simple DecorationCutout without SpriteMask; Sliced/Tiled and arbitrary transparency are unsupported.");
            var material=sprite.sharedMaterial;
            if(material==null||material.shader==null||material.shader.name!="Ossastra/GroundWorkshop/DecorationCutout")
                throw new InvalidOperationException("Only the audited opaque DecorationCutout sprite shader is supported.");
            var tint=sprite.color;
            if(!Finite(tint.r)||!Finite(tint.g)||!Finite(tint.b)||!Finite(tint.a))
                throw new InvalidOperationException("Sprite tint must be finite.");
        }
        static void SynchronizeSprite(Source source)
        {
            var sprite=source.SpriteRenderer.sprite;
            if(sprite==null)
            {
                if(source.Sprite!=null)source.Mesh.Clear();source.Sprite=null;source.SpriteTexture=null;return;
            }
            bool geometry=sprite!=source.Sprite||sprite.texture!=source.SpriteTexture;
            var tint=source.SpriteRenderer.color;bool tintChanged=geometry||tint.r!=source.Tint.r||tint.g!=source.Tint.g||tint.b!=source.Tint.b||tint.a!=source.Tint.a;
            bool flip=geometry||source.FlipX!=source.SpriteRenderer.flipX||source.FlipY!=source.SpriteRenderer.flipY;
            if(geometry)
            {
                source.Sprite=sprite;source.SpriteTexture=sprite.texture;source.SpriteBaseVertices=sprite.vertices;
                source.SpriteVertices=new Vector3[source.SpriteBaseVertices.Length];source.SpriteColors=new Color32[source.SpriteBaseVertices.Length];
                var triangles=sprite.triangles;var indices=new int[triangles.Length];for(int i=0;i<indices.Length;i++)indices[i]=triangles[i];
                source.Mesh.Clear();source.Mesh.vertices=source.SpriteVertices;source.Mesh.uv=sprite.uv;source.Mesh.triangles=indices;
                for(int i=0;i<source.Materials.Length;i++){source.Materials[i].SetTexture("_MainTex",source.SpriteTexture);source.DepthMaterials[i].SetTexture("_MainTex",source.SpriteTexture);}
            }
            if(flip)
            {
                source.FlipX=source.SpriteRenderer.flipX;source.FlipY=source.SpriteRenderer.flipY;
                // Native A/A proved this specific shader does not consume SpriteRenderer._Flip.
                // Reproduce its actual unflipped vertex stream, rather than silently adding a feature.
                for(int i=0;i<source.SpriteVertices.Length;i++){var point=source.SpriteBaseVertices[i];source.SpriteVertices[i]=new Vector3(point.x,point.y,0);}
                source.Mesh.vertices=source.SpriteVertices;source.Mesh.RecalculateBounds();
            }
            if(tintChanged)
            {
                // Its native COLOR is white; sr.color lives in a renderer uniform the source shader
                // never reads. Material _Color remains unchanged and still participates normally.
                source.Tint=tint;Color32 vertexTint=new Color32(255,255,255,255);for(int i=0;i<source.SpriteColors.Length;i++)source.SpriteColors[i]=vertexTint;
                source.Mesh.colors32=source.SpriteColors;
            }
        }
        Transform Clone(Transform source, Group group, Dictionary<Transform, Node> nodes)
        {
            if (source == null) return group.Root.transform;
            if (nodes.TryGetValue(source, out var existing)) return existing.Copy;
            var parent = Clone(source.parent, group, nodes); var go = OwnedObject("VIS_" + source.name); go.transform.SetParent(parent, false);
            var node = new Node { Source = source, Copy = go.transform, Parent = source.parent }; nodes.Add(source, node); CopyNode(node); return go.transform;
        }
        static void CopyNode(Node node) { node.Copy.localPosition = node.Source.localPosition; node.Copy.localRotation = node.Source.localRotation; node.Copy.localScale = node.Source.localScale; }
        static void SyncBlocks(Source source)
        {
            if(source.SpriteRenderer!=null)
            {
                // Native Sprite A/A proved this known shader consumes its per-renderer texture but
                // retains material _Color/_Cutoff from UnityPerMaterial. Copying arbitrary Sprite MPB
                // scalars into a MeshRenderer would add overrides the actual source never applies.
                source.Renderer.GetPropertyBlock(source.Global);KeepSpriteTextureBinding(source,source.Global,0);source.Copy.SetPropertyBlock(source.Global);
                for(int i=0;i<source.Slots.Length;i++)
                {
                    source.Renderer.GetPropertyBlock(source.Slots[i],i);bool empty=source.Slots[i].isEmpty;
                    if(!empty)KeepSpriteTextureBinding(source,source.Slots[i],i);
                    source.Copy.SetPropertyBlock(empty?null:source.Slots[i],i);
                }
                return;
            }
            source.Renderer.GetPropertyBlock(source.Global); source.Copy.SetPropertyBlock(source.Global);
            for (int i = 0; i < source.Slots.Length; i++) { source.Renderer.GetPropertyBlock(source.Slots[i], i); source.Copy.SetPropertyBlock(source.Slots[i].isEmpty ? null : source.Slots[i], i); }
        }
        static void KeepSpriteTextureBinding(Source source,MaterialPropertyBlock block,int slot)
        {
            // User SetPropertyBlock replaces the native Sprite _MainTex block. Absence is meaningful:
            // the tested source then uses material/default white, not an invented sprite.texture fallback.
            Texture texture=block.GetTexture(SpriteMainTexId);
            if(texture==null)texture=source.SourceMaterials[slot].GetTexture(SpriteMainTexId);
            if(texture==null)texture=Texture2D.whiteTexture;
            block.Clear();block.SetTexture(SpriteMainTexId,texture);
        }
        void EnsureTargets(Group group)
        {
            if (group.Color != null && group.Depth != null && group.Color.IsCreated() && group.Depth.IsCreated() &&
                group.Color.width == width && group.Color.height == height && group.Depth.width == width && group.Depth.height == height) return;
            var color = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            { name = "VIS group color " + group.Id, antiAliasing = 1, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            color.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            var depth = new RenderTexture(width, height, 32, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            { name = "VIS group device depth " + group.Id, antiAliasing = 1, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            depth.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            if (!color.Create() || !depth.Create()) { ReleaseTarget(color); ReleaseTarget(depth); throw new InvalidOperationException("Finite capture targets could not be created."); }
            ReleaseTarget(group.Color); ReleaseTarget(group.Depth); group.Color = color; group.Depth = depth;
        }
        void ConfigureAuxiliary()
        {
            auxiliary.CopyFrom(main); auxiliary.enabled = false; auxiliary.cullingMask = 1 << captureLayer;
            auxiliary.clearFlags = CameraClearFlags.SolidColor; auxiliary.backgroundColor = Color.clear;
            auxiliary.allowHDR = true; auxiliary.allowMSAA = false; auxiliary.useOcclusionCulling = false;
            auxiliary.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            auxiliary.worldToCameraMatrix = main.worldToCameraMatrix; auxiliary.projectionMatrix = main.projectionMatrix;
        }
        void DrawDepth(ScriptableRenderContext context, Group group)
        {
            commands.Clear(); float clear = SystemInfo.usesReversedZBuffer ? 0 : 1;
            CoreUtils.SetRenderTarget(commands, new RenderTargetIdentifier(group.Depth), ClearFlag.All, new Color(clear, clear, clear, 1)); commands.SetViewport(new Rect(0, 0, width, height));
            var view = main.worldToCameraMatrix; var projection = main.projectionMatrix; var gpu = GL.GetGPUProjectionMatrix(projection, true);
            commands.SetViewProjectionMatrices(view, projection); commands.SetGlobalMatrix("unity_MatrixInvV", view.inverse);
            commands.SetGlobalMatrix("unity_MatrixInvP", gpu.inverse); commands.SetGlobalMatrix("unity_MatrixInvVP", (gpu * view).inverse);
            var worldToCamera = Matrix4x4.Scale(new Vector3(1, 1, -1)) * view;
            commands.SetGlobalMatrix("unity_WorldToCamera", worldToCamera); commands.SetGlobalMatrix("unity_CameraToWorld", worldToCamera.inverse);
            float n = main.nearClipPlane, f = main.farClipPlane, z0 = 1 - f / n, z1 = f / n; var z = new Vector4(z0, z1, z0 / f, z1 / f);
            if (SystemInfo.usesReversedZBuffer) { z.y += z.x; z.x = -z.x; z.w += z.z; z.z = -z.z; }
            commands.SetGlobalVector("_ProjectionParams", new Vector4(SystemInfo.graphicsUVStartsAtTop ? -1 : 1, n, f, 1 / f));
            commands.SetGlobalVector("_ZBufferParams", z); commands.SetGlobalVector("_WorldSpaceCameraPos", main.transform.position);
            commands.SetGlobalVector("_ScreenParams", new Vector4(width, height, 1f + 1f / width, 1f + 1f / height));
            commands.SetGlobalVector("_ScaledScreenParams", new Vector4(width, height, 1f + 1f / width, 1f + 1f / height));
            commands.SetGlobalVector("unity_OrthoParams", new Vector4(main.orthographicSize * main.aspect, main.orthographicSize, 0, main.orthographic ? 1 : 0));
            for (int i = 0; i < group.Sources.Length; i++)
            {
                var source = group.Sources[i]; if (!source.Visible) continue;
                for (int m = 0; m < source.DepthMaterials.Length; m++) commands.DrawMesh(source.Mesh, source.Renderer.localToWorldMatrix,
                    source.DepthMaterials[m], m, 0, source.Slots[m].isEmpty ? source.Global : source.Slots[m]);
            }
            context.ExecuteCommandBuffer(commands); commands.Clear();
        }
        void PositionDisplays()
        {
            float z = main.nearClipPlane + .05f; float h = main.orthographic ? main.orthographicSize * 2 : 2 * z * Mathf.Tan(main.fieldOfView * Mathf.Deg2Rad * .5f);
            proxyRoot.transform.SetPositionAndRotation(main.transform.position + main.transform.forward * z, main.transform.rotation);
            proxyRoot.transform.localScale = new Vector3(h * main.aspect, h, 1);
        }
        void BindDisplays()
        {
            var active = Vector4.zero; var order = Vector4.zero; var supportStrength = Vector4.zero;
            for (int s = 0; s < MaxGroups; s++)
            {
                var group = slots[s]; bool enabled = group != null && group.Active; active[s] = enabled ? 1 : 0;
                if (group != null) for (int t = 0; t < MaxGroups; t++) if (slots[t] != null && slots[t].Id < group.Id) order[s]++;
                for (int m = 0; m < displays.Length; m++)
                {
                    displays[m].SetTexture(ColorIds[s], enabled ? (Texture)group.Color : Texture2D.blackTexture);
                    displays[m].SetTexture(DepthIds[s], enabled ? (Texture)group.Depth : Texture2D.blackTexture);
                    var a = enabled ? group.Frame.CircleA : default; var b = enabled ? group.Frame.CircleB : default;
                    var support = enabled ? group.Frame.PreserveSupport : default; supportStrength[s] = support.Strength;
                    displays[m].SetVector(CircleAIds[s], new Vector4(a.CenterViewport.x, a.CenterViewport.y, a.InnerPixels, a.OuterPixels));
                    displays[m].SetVector(CircleBIds[s], new Vector4(b.CenterViewport.x, b.CenterViewport.y, b.InnerPixels, b.OuterPixels));
                    displays[m].SetVector(SupportIds[s], new Vector4(support.CenterViewport.x, support.CenterViewport.y, support.InnerPixels, support.OuterPixels));
                    displays[m].SetVector(StrengthIds[s], new Vector4(a.Strength, b.Strength, enabled ? group.Frame.WholeFade : 0, 0));
                }
            }
            for (int m = 0; m < displays.Length; m++)
            {
                displays[m].SetVector(ActiveId, active); displays[m].SetVector(OrderId, order); displays[m].SetVector(SupportStrengthId, supportStrength);
                displays[m].SetVector(ScreenId, new Vector4(width, height, 1f / width, 1f / height));
            }
        }
        static int[] Ids(string prefix) { var ids = new int[MaxGroups]; for (int i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID(prefix + i); return ids; }
        static int FirstVisibleLayer(int mask) { for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i; throw new InvalidOperationException("Main camera needs a display layer outside the private capture layer."); }
        GameObject OwnedObject(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = captureLayer };
            if (main.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(go, main.gameObject.scene);
            return go;
        }
        static void ReleaseTarget(RenderTexture target) { if (target == null) return; target.Release(); DestroyOwned(target); }
        static void Release(Group group)
        {
            if (group == null) return;
            if (group.Root != null) { var renderers = group.Root.GetComponentsInChildren<Renderer>(true); for (int i = 0; i < renderers.Length; i++) renderers[i].forceRenderingOff = true; }
            if (group.Sources != null) for (int i = 0; i < group.Sources.Length; i++)
            {
                var source = group.Sources[i]; if (source == null) continue; DestroyOwned(source.Mesh);
                if (source.Materials != null) for (int m = 0; m < source.Materials.Length; m++) DestroyOwned(source.Materials[m]);
                if (source.DepthMaterials != null) for (int m = 0; m < source.DepthMaterials.Length; m++) DestroyOwned(source.DepthMaterials[m]);
            }
            ReleaseTarget(group.Color); ReleaseTarget(group.Depth); DestroyOwned(group.Root);
        }
        static void DestroyOwned(Object value)
        {
            if (value == null) return;
            #if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(value); return; }
            #endif
            Object.Destroy(value);
        }
        public void Dispose()
        {
            if (disposed) return; RestoreOwnedState(); disposed = true; if(auxiliary!=null){auxiliary.enabled = false; auxiliary.targetTexture = null;}
            for (int i = 0; i < MaxGroups; i++) { Release(slots[i]); slots[i] = null; }
            for (int i = 0; i < displays.Length; i++) { if(proxies[i]!=null)proxies[i].forceRenderingOff = true; DestroyOwned(displays[i]); }
            commands?.Release(); DestroyOwned(quad); DestroyOwned(proxyRoot); DestroyOwned(auxiliaryObject);
        }
    }
}
