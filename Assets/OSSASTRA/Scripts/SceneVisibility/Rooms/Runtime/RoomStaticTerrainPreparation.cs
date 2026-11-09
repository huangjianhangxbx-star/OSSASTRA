/* Retained metadata for explicitly installed terrain only. No renderer/material writes,
 * GPU state, event subscription, visibility history, or global owner. Fresh component
 * inventories and source/layout captures bound each receipt; no notification assumption.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Ossastra.VoxelTerrain;
using Ossastra.EnvironmentSimulation;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.SceneVisibility.Rooms
{
    internal interface IRoomStaticTerrainOwner
    {
        Renderer[] SharedTerrainSurfaces{get;}
        bool IsRetainedTerrainStatic(Renderer renderer);
    }
    internal sealed class RoomStaticTerrainPreparation
    {
        internal static readonly ProfilingSampler perfCaptureBuild=new ProfilingSampler("RoomVisibility.StaticTerrain.CaptureBuild");
        internal static readonly ProfilingSampler perfCapturePrepare=new ProfilingSampler("RoomVisibility.StaticTerrain.CapturePrepare");
        internal sealed class Row
        {
            internal Renderer Renderer;
            internal VoxelTerrainInstallation Installation;
            internal Material[] Materials;
            internal int[] SourceVersions;
            internal RoomPositiveSourceFacts Facts;
            internal RoomPositiveSurface Surface;
            internal Matrix4x4 BuildMatrix;
            internal int SeenEpoch, BuildEpoch, PrepareEpoch, Generation;
        }
        sealed class Node
        {
            internal readonly List<Component> Components=new List<Component>(8);
            internal int Epoch;
            internal bool Forbidden;
        }
        sealed class Source
        {
            internal int Epoch,Crc,Version;
            internal Shader Shader;
            internal int Queue;
            internal bool Supported,Initialized;
        }
        readonly Dictionary<Renderer,Row> rows=new Dictionary<Renderer,Row>();
        readonly Dictionary<Transform,Node> nodes=new Dictionary<Transform,Node>();
        readonly Dictionary<Material,Source> sources=new Dictionary<Material,Source>();
        readonly List<Material[]> layouts=new List<Material[]>();
        readonly List<Material> slotScratch=new List<Material>(4);
        readonly HashSet<Renderer> roster=new HashSet<Renderer>();
        readonly HashSet<Renderer> prepareRoster=new HashSet<Renderer>(),prepareShared=new HashSet<Renderer>();
        readonly List<Renderer> staleRows=new List<Renderer>();
        readonly List<Transform> staleNodes=new List<Transform>();
        readonly List<Material> staleSources=new List<Material>();
        VoxelTerrainInstallation[] installations=Array.Empty<VoxelTerrainInstallation>();
        Scene scene;
        IRoomStaticTerrainOwner moduleOwner;
        Camera camera;
        int epoch,nodeEpoch,sourceEpoch,prepareEpoch;
        internal bool Enabled=true;
        internal int RowCount=>rows.Count;
        internal int FreshComponentReads{get;private set;}
        internal int FreshBuildSlotReads{get;private set;}
        internal int FreshPrepareSlotReads{get;private set;}
        internal int UniqueSourceReads{get;private set;}
        internal int RowRebuilds{get;private set;}
        internal int PrepareEligibleCount{get;private set;}

        internal void Bind(VoxelTerrainInstallation[] explicitInstallations,Scene ownerScene)
        {
            Clear();scene=ownerScene;
            installations=explicitInstallations==null?Array.Empty<VoxelTerrainInstallation>():(VoxelTerrainInstallation[])explicitInstallations.Clone();
        }
        internal void BindOwned(VoxelTerrainInstallation[] explicitInstallations,Scene ownerScene,IRoomStaticTerrainOwner owner)
        {Bind(explicitInstallations,ownerScene);moduleOwner=owner;}
        internal void CaptureBuild(Camera currentCamera)
        {
            camera=currentCamera;++epoch;++nodeEpoch;++sourceEpoch;prepareEpoch=0;
            FreshComponentReads=FreshBuildSlotReads=FreshPrepareSlotReads=UniqueSourceReads=RowRebuilds=PrepareEligibleCount=0;
            roster.Clear();
            if(!Enabled||camera==null){ClearRecords();return;}
            foreach(var installation in installations)
            {
                if(installation==null||!installation.isActiveAndEnabled||installation.gameObject.scene!=scene||installation.chunks==null)continue;
                // Read the public array's actual elements every time, including same-array edits.
                foreach(var renderer in installation.chunks)
                {
                    if(renderer==null||!roster.Add(renderer)||renderer.gameObject.scene!=scene||!renderer.transform.IsChildOf(installation.transform))continue;
                    if(!rows.TryGetValue(renderer,out var row)){row=new Row{Renderer=renderer};rows.Add(renderer,row);}
                    row.SeenEpoch=epoch;row.BuildEpoch=0;row.PrepareEpoch=0;
                    row.Installation=installation;
                    if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.forceRenderingOff)continue;
                    if(!ReadRole(renderer,out float role,out float response,out float shadow,out float minimum))continue;
                    slotScratch.Clear();renderer.GetSharedMaterials(slotScratch);++FreshBuildSlotReads;
                    if(slotScratch.Count==0||slotScratch.Count>16)continue;
                    bool valid=true;
                    foreach(var material in slotScratch)if(!ReadSource(material).Supported){valid=false;break;}
                    if(!valid)continue;
                    var layout=InternLayout(slotScratch);
                    bool changed=!ReferenceEquals(row.Materials,layout);
                    if(row.SourceVersions==null||row.SourceVersions.Length!=layout.Length){row.SourceVersions=new int[layout.Length];changed=true;}
                    for(int i=0;i<layout.Length;++i)
                    {
                        int version=sources[layout[i]].Version;
                        if(row.SourceVersions[i]!=version){row.SourceVersions[i]=version;changed=true;}
                    }
                    var surface=new RoomPositiveSurface{Renderer=renderer,Kind=RoomPositiveSurfaceKind.Partial,
                        OwnerVolumes=Array.Empty<RoomMaskVolume>(),OwnerExterior=false,IndependentDraw=false,
                        StaticEnvironment=true,WorldLookEnvironment=true,EnvironmentFullSurface=false,
                        EnvironmentSurfaceRole=role,EnvironmentResponseOverride=response,EnvironmentReceiveShadow=shadow,
                        EnvironmentMinimumOverride=minimum,DrawMask=RoomPositiveRenderingLayers.PartialLitBit};
                    if(!SameSemantic(row.Surface,surface))changed=true;
                    row.Materials=layout;row.Surface=surface;
                    row.BuildMatrix=renderer.localToWorldMatrix;
                    row.Facts=new RoomPositiveSourceFacts{Renderer=renderer,Materials=layout,Family=RoomPositiveRenderingLayers.PartialLitBit,Independent=false};
                    row.BuildEpoch=epoch;
                    if(changed){++row.Generation;++RowRebuilds;}
                }
            }
            staleRows.Clear();foreach(var pair in rows)if(pair.Key==null||pair.Value.SeenEpoch!=epoch)staleRows.Add(pair.Key);
            foreach(var renderer in staleRows)rows.Remove(renderer);
            staleNodes.Clear();foreach(var pair in nodes)if(pair.Key==null||pair.Value.Epoch!=nodeEpoch)staleNodes.Add(pair.Key);
            foreach(var transform in staleNodes)nodes.Remove(transform);
            PruneSourcesAndLayouts();
        }
        internal bool TryBuild(Renderer renderer,out RoomPositiveSurface surface,out RoomPositiveSourceFacts facts)
        {
            surface=default;facts=default;
            if(!Enabled||renderer==null||!rows.TryGetValue(renderer,out var row)||row.BuildEpoch!=epoch)return false;
            surface=row.Surface;facts=row.Facts;return true;
        }
        internal void CapturePrepare(Camera currentCamera,RoomMaskFrame frame)
        {
            prepareEpoch=0;FreshPrepareSlotReads=PrepareEligibleCount=0;
            if(!Enabled||!ReferenceEquals(camera,currentCamera)||frame==null||frame.PositiveSurfaces==null)return;
            prepareRoster.Clear();
            foreach(var installation in installations)
                if(installation!=null&&installation.isActiveAndEnabled&&installation.gameObject.scene==scene&&installation.chunks!=null)
                    foreach(var renderer in installation.chunks)if(renderer!=null&&renderer.transform.IsChildOf(installation.transform))prepareRoster.Add(renderer);
            prepareShared.Clear();
            if(moduleOwner!=null&&moduleOwner.SharedTerrainSurfaces!=null)foreach(var renderer in moduleOwner.SharedTerrainSurfaces)prepareShared.Add(renderer);
            ++sourceEpoch;++nodeEpoch;prepareEpoch=sourceEpoch;
            foreach(var surface in frame.PositiveSurfaces)
            {
                var renderer=surface.Renderer;
                if(renderer==null||!rows.TryGetValue(renderer,out var row)||row.BuildEpoch!=epoch||!SameSemantic(row.Surface,surface))continue;
                row.PrepareEpoch=0;
                // Publication is an arbitrary callback boundary; freshly read all actual slots.
                if(!prepareRoster.Contains(renderer)||row.Installation==null||!row.Installation.isActiveAndEnabled||
                   !renderer.transform.IsChildOf(row.Installation.transform)||
                   (moduleOwner!=null&&(!prepareShared.Contains(renderer)||!moduleOwner.IsRetainedTerrainStatic(renderer)))||
                   !renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.forceRenderingOff||
                   !SameMatrix(row.BuildMatrix,renderer.localToWorldMatrix)||
                   !ReadRole(renderer,out float role,out float response,out float shadow,out float minimum)||
                   role!=surface.EnvironmentSurfaceRole||response!=surface.EnvironmentResponseOverride||
                   shadow!=surface.EnvironmentReceiveShadow||minimum!=surface.EnvironmentMinimumOverride)continue;
                slotScratch.Clear();renderer.GetSharedMaterials(slotScratch);++FreshPrepareSlotReads;
                if(!Same(row.Materials,slotScratch))continue;
                bool valid=true;
                for(int i=0;i<row.Materials.Length;++i)
                {
                    var source=ReadSource(row.Materials[i]);
                    if(!source.Supported||source.Version!=row.SourceVersions[i]){valid=false;break;}
                }
                if(valid){row.PrepareEpoch=prepareEpoch;++PrepareEligibleCount;}
            }
        }
        internal bool TryPrepared(Renderer renderer,out Material[] materials)
        {
            materials=null;
            if(!Enabled||prepareEpoch==0||renderer==null||!rows.TryGetValue(renderer,out var row)||row.BuildEpoch!=epoch||row.PrepareEpoch!=prepareEpoch)return false;
            materials=row.Materials;return true;
        }
        internal void EndPrepare(){prepareEpoch=0;}
        internal bool Contains(Renderer renderer)=>renderer!=null&&rows.ContainsKey(renderer);
        internal void Clear(){ClearRecords();installations=Array.Empty<VoxelTerrainInstallation>();moduleOwner=null;camera=null;prepareEpoch=0;}
        void ClearRecords()
        {
            rows.Clear();nodes.Clear();sources.Clear();layouts.Clear();roster.Clear();prepareRoster.Clear();prepareShared.Clear();
            slotScratch.Clear();staleRows.Clear();staleNodes.Clear();staleSources.Clear();
        }

        bool ReadRole(Renderer renderer,out float role,out float response,out float shadow,out float minimum)
        {
            role=1;response=shadow=1;minimum=0;
            EnvironmentMaterialResponse selected=null;bool selectedNative=false;bool mesh=false;
            // Every actual object gets a fresh component inventory this epoch; no negative cache.
            for(var transform=renderer.transform;transform!=null;transform=transform.parent)
            {
                if(!nodes.TryGetValue(transform,out var node)){node=new Node();nodes.Add(transform,node);}
                if(node.Epoch!=nodeEpoch)
                {
                    node.Epoch=nodeEpoch;node.Components.Clear();transform.GetComponents(node.Components);++FreshComponentReads;
                    node.Forbidden=false;
                    foreach(var component in node.Components)
                        if(component is Animator||component is Animation||component is LODGroup||component is Spine.Unity.SkeletonRenderer||
                           component is Ossastra.Characters.CharacterRoot||component is Ossastra.Enemies.EnemyRoot||component is Ossastra.Npcs.NpcRoot||
                           component is Ossastra.WorldProps.WorldPropRoot||component is Ossastra.Characters.Rendering.CharacterUprightDepth)node.Forbidden=true;
                }
                if(node.Forbidden)return false;
                foreach(var component in node.Components)
                {
                    if(transform==renderer.transform&&component is MeshFilter filter&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0)mesh=true;
                    if(selected!=null||selectedNative||!(component is EnvironmentMaterialResponse tag)||!tag.isActiveAndEnabled)continue;
                    bool explicitMember=tag.renderers!=null&&Array.IndexOf(tag.renderers,renderer)>=0;
                    bool own=(tag.renderers==null||tag.renderers.Length==0)&&tag.transform==renderer.transform;
                    if(!explicitMember&&!own)continue;
                    if(tag.role==EnvironmentMaterialRole.NativeLit){selectedNative=true;continue;}
                    selected=tag;
                }
            }
            if(!mesh)return false;
            if(selected!=null)
            {
                role=selected.role==EnvironmentMaterialRole.Ground?0:selected.role==EnvironmentMaterialRole.Decoration?1:2;
                response=float.IsNaN(selected.response)||float.IsInfinity(selected.response)?1:Mathf.Clamp01(selected.response);
                shadow=selected.receiveSunShadow?1:0;
                minimum=float.IsNaN(selected.minimumReadability)||float.IsInfinity(selected.minimumReadability)?0:Mathf.Clamp01(selected.minimumReadability);
            }
            return true;
        }
        Source ReadSource(Material material)
        {
            if(material==null)return Unsupported;
            if(!sources.TryGetValue(material,out var source)){source=new Source();sources.Add(material,source);}
            if(source.Epoch==sourceEpoch)return source;
            source.Epoch=sourceEpoch;++UniqueSourceReads;
            int crc=material.ComputeCRC();var shader=material.shader;int queue=material.renderQueue;
            bool supported=shader!=null&&shader.name==RoomPositiveRenderingLayers.VoxelLitShaderName&&
                (!material.HasProperty("_Surface")||material.GetFloat("_Surface")==0)&&EffectiveQueue(material)<=2500;
            if(!source.Initialized||source.Crc!=crc||source.Shader!=shader||source.Queue!=queue||source.Supported!=supported)++source.Version;
            source.Initialized=true;source.Crc=crc;source.Shader=shader;source.Queue=queue;source.Supported=supported;return source;
        }
        static readonly Source Unsupported=new Source();
        static int EffectiveQueue(Material material)=>material.renderQueue<0&&material.shader!=null?material.shader.renderQueue:material.renderQueue;
        Material[] InternLayout(List<Material> materials)
        {
            foreach(var layout in layouts)if(Same(layout,materials))return layout;
            var result=materials.ToArray();layouts.Add(result);return result;
        }
        void PruneSourcesAndLayouts()
        {
            staleSources.Clear();
            foreach(var pair in sources)
            {
                bool used=false;foreach(var row in rows.Values)if(row.Materials!=null&&Array.IndexOf(row.Materials,pair.Key)>=0){used=true;break;}
                if(pair.Key==null||!used)staleSources.Add(pair.Key);
            }
            foreach(var material in staleSources)sources.Remove(material);
            for(int i=layouts.Count-1;i>=0;--i)
            {bool used=false;foreach(var row in rows.Values)if(ReferenceEquals(row.Materials,layouts[i])){used=true;break;}if(!used)layouts.RemoveAt(i);}
        }
        static bool Same(Material[] expected,List<Material> actual)
        {if(expected==null||expected.Length!=actual.Count)return false;for(int i=0;i<expected.Length;++i)if(!ReferenceEquals(expected[i],actual[i]))return false;return true;}
        static bool SameSemantic(RoomPositiveSurface a,RoomPositiveSurface b)=>ReferenceEquals(a.Renderer,b.Renderer)&&a.Kind==b.Kind&&
            (a.OwnerVolumes==null||a.OwnerVolumes.Length==0)&&(b.OwnerVolumes==null||b.OwnerVolumes.Length==0)&&a.OwnerExterior==b.OwnerExterior&&
            a.IndependentDraw==b.IndependentDraw&&a.StaticEnvironment==b.StaticEnvironment&&a.WorldLookEnvironment==b.WorldLookEnvironment&&
            a.EnvironmentFullSurface==b.EnvironmentFullSurface&&a.EnvironmentSurfaceRole==b.EnvironmentSurfaceRole&&
            a.EnvironmentResponseOverride==b.EnvironmentResponseOverride&&a.EnvironmentReceiveShadow==b.EnvironmentReceiveShadow&&a.EnvironmentMinimumOverride==b.EnvironmentMinimumOverride;
        static bool SameMatrix(Matrix4x4 a,Matrix4x4 b){for(int i=0;i<16;++i)if(Mathf.Abs(a[i]-b[i])>.0001f)return false;return true;}
    }
}
