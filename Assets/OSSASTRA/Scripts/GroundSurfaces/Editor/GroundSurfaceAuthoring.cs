/* 脚本：GroundSurfaceAuthoring
 * 功能：在独立悬崖样板中校验平台归属，消费真实原生/配方预览层，原子构建派生顶盖与站立碰撞。
 * 边界：不保存资产/场景、不改作者Tile/原材质/角色、不每帧重建；持久化与调度由独立服务管理。
 * 协作：Reader只读模型；Geometry/Generator只造派生数据；原地表工坊继续拥有画笔和作者Undo。
 * 事务：先完成新域和缓存再替换旧引用；失败释放本次产物，重取环及换模型记录自有Undo。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ossastra.GroundResearch;
using Ossastra.GroundResearch.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Ossastra.GroundSurfaces.Editor
{
    public static partial class GroundSurfaceAuthoring
    {
        public const string ScenePath="Assets/OSSASTRA/GroundWorkshop/Samples/KT-GROUND-03/SC_CliffEdgeLab.unity";
        public static bool IsRebuilding {get;private set;}
        const string ThinShader="Ossastra/GroundSurfaces/ThinTop";
        sealed class PreparedCache
        {
            public GameObject root;
            public GameObject colliderObject;
            public GroundSurfaceMeshCache cache;
            public MeshCollider collider;
            public readonly List<Mesh> meshes=new List<Mesh>();
            public readonly List<Material> materials=new List<Material>();
        }
        struct SurfaceMaterialKey : IEquatable<SurfaceMaterialKey>
        {
            public int layer,material,texture;
            public bool Equals(SurfaceMaterialKey other)=>layer==other.layer&&material==other.material&&texture==other.texture;
            public override bool Equals(object value)=>value is SurfaceMaterialKey other&&Equals(other);
            public override int GetHashCode()=>unchecked((layer*397^material)*397^texture);
        }

        static void RequireBinding(GroundSurfaceBinding binding)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            var active=SceneManager.GetActiveScene();
            if(binding==null||!binding.isActiveAndEnabled||EditorUtility.IsPersistent(binding)||!GroundSurfaceSceneAccess.IsSupported(active.path)||binding.gameObject.scene!=active||EditorSceneManager.IsPreviewScene(active))
                throw new InvalidOperationException("仅当前已保存悬崖样板中的有效平台Binding可执行此操作。");
            GroundWorkshopOwnership.Require(binding.map);
            if(binding.map==null||binding.map.registration==null||binding.map.gameObject.scene!=active||binding.map.registration.scenePath!=active.path||binding.boundary==null||
               !GroundWorkshopOwnership.IsUnder(AssetDatabase.GetAssetPath(binding.boundary),binding.map.registration.assetFolder))
                throw new InvalidOperationException("平台缺少属于本地图assetFolder的独立Boundary，未写共享环或外部资产。");
            ValidateExistingCache(binding);
        }
        public static GroundSurfaceBinding[] GetBindings()
        {
            var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid()||!scene.isLoaded||!GroundSurfaceSceneAccess.IsSupported(scene.path))return Array.Empty<GroundSurfaceBinding>();
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            var bindings=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundSurfaceBinding>(true)).ToArray();
            var maps=new HashSet<GroundWorkshopMap>();
            foreach(var binding in bindings){RequireBinding(binding);if(!maps.Add(binding.map))throw new InvalidOperationException("同一作者Map被多个平台Binding登记，拒绝歧义操作。");}
            return bindings;
        }

        /// <summary>真实显示输入：Existing替换一层，Future保留旧层并在最后追加临时展示层。</summary>
        public static SurfaceLayerInput[] EffectiveInputs(GroundSurfaceBinding binding)
        {
            RequireBinding(binding);var map=binding.map;var layers=map.surface.layers;
            var inputs=new List<SurfaceLayerInput>(layers.Length+1);
            for(int i=0;i<layers.Length;i++)inputs.Add(new SurfaceLayerInput(layers[i].tilemap,map.layerBindings[i].ownedMaterial,i));
            if(GroundWorkshopAuthoring.TryGetPreviewTilemap(map,out var preview,out var material,out int layer,out var scope))
            {
                if(layer<0||layer>=layers.Length||preview==null||material==null||preview.gameObject.scene!=map.gameObject.scene||preview.layoutGrid!=map.surface.grid)
                    throw new InvalidOperationException("当前配方预览不属于平台实际Grid/图层，未沿旧层假装预览成功。");
                if(scope==GroundReplacementScope.ExistingAndFuture)inputs[layer]=new SurfaceLayerInput(preview,material,layer);
                else if(scope==GroundReplacementScope.FutureOnly)inputs.Add(new SurfaceLayerInput(preview,material,layers.Length));
                else throw new InvalidOperationException("未知配方替换范围，未生成近似缓存。");
            }
            return inputs.ToArray();
        }

        public static void Rebuild(GroundSurfaceBinding binding,bool retakeRim=false)
        {
            RequireBinding(binding);
            if(IsRebuilding)throw new InvalidOperationException("平台缓存事务正在执行，拒绝重入。");
            IsRebuilding=true;PreparedCache prepared=null;
            try
            {
                string fingerprint=GroundSurfaceModelReader.Fingerprint(binding);
                if(!retakeRim&&binding.boundary.sourceFingerprint!=fingerprint)
                    throw new InvalidOperationException("侧壁来源或变换已变化；旧连接环保留，请使用重取上沿/重建顶盖。");
                SurfaceDomain domain=retakeRim?GroundSurfaceModelReader.ReadDomain(binding):binding.GetDomain();
                var inputs=EffectiveInputs(binding);var built=GroundSurfaceMeshGenerator.Build(binding,domain,inputs,true);
                prepared=PrepareCache(binding,domain,built,fingerprint);
                var oldRoot=binding.cacheRoot;var oldCollider=binding.capCollider;var oldCache=oldRoot==null?null:oldRoot.GetComponent<GroundSurfaceMeshCache>();
                prepared.cache.rebuildCount=(oldCache==null?0:oldCache.rebuildCount)+1;
                if(retakeRim)
                {
                    // 所有可失败的几何/材质/碰撞准备先完成，再记录并提交唯一模型环证据。
                    Undo.RecordObject(binding.boundary,"重取平台模型上沿");
                    binding.boundary.rim=(Vector2[])domain.ring.Clone();binding.boundary.sourceFingerprint=fingerprint;
                    binding.boundary.sourceMeshGuids=binding.sideWalls.Select(m=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m.sharedMesh))).Distinct().ToArray();
                    binding.boundary.revision++;EditorUtility.SetDirty(binding.boundary);binding.InvalidateBoundary();
                }
                binding.cacheRoot=prepared.root.transform;binding.capCollider=prepared.collider;binding.lastError="";
                prepared.colliderObject.SetActive(true);prepared.root.SetActive(binding.useCache);binding.SetCacheVisible(binding.useCache);
                HideNativePreview(binding);EditorUtility.SetDirty(binding);EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
                prepared=null;
                if(oldRoot!=null)DestroyOwnedCache(oldRoot,oldCache,oldCollider);
            }
            finally
            {
                if(prepared!=null)ReleasePrepared(prepared);
                IsRebuilding=false;
            }
        }
        static void HideNativePreview(GroundSurfaceBinding binding)
        {
            if(GroundWorkshopAuthoring.TryGetPreviewTilemap(binding.map,out var tilemap,out _,out _,out _))
            {var renderer=tilemap.GetComponent<TilemapRenderer>();if(renderer!=null)renderer.forceRenderingOff=binding.useCache;}
        }
        static PreparedCache PrepareCache(GroundSurfaceBinding binding,SurfaceDomain domain,SurfaceMeshBuildResult built,string fingerprint)
        {
            var prepared=new PreparedCache();
            if(built.chunks!=null)foreach(var chunk in built.chunks)if(chunk!=null&&chunk.mesh!=null)prepared.meshes.Add(chunk.mesh);
            try
            {
                var shader=Shader.Find(ThinShader);if(shader==null)throw new InvalidOperationException("缺少独立薄顶面Shader："+ThinShader);
                prepared.root=new GameObject("顶盖缓存_"+binding.SurfaceId);prepared.root.SetActive(false);
                SceneManager.MoveGameObjectToScene(prepared.root,binding.gameObject.scene);prepared.root.transform.SetParent(binding.transform,false);
                var cache=prepared.root.AddComponent<GroundSurfaceMeshCache>();prepared.cache=cache;cache.binding=binding;
                var chunks=new List<GroundSurfaceRenderChunk>();var materials=new Dictionary<SurfaceMaterialKey,Material>();
                foreach(var chunk in built.chunks)
                {
                    if(chunk==null||chunk.mesh==null||chunk.sourceMaterial==null||chunk.mainTexture==null||chunk.sourceTilemap==null)
                        throw new InvalidOperationException("生成器返回不完整的块/纹理/材质/来源，旧缓存未替换。");
                    var sourceRenderer=chunk.sourceTilemap.GetComponent<TilemapRenderer>();if(sourceRenderer==null)throw new InvalidOperationException("真实输入层缺TilemapRenderer。");
                    var key=new SurfaceMaterialKey{layer=chunk.layerIndex,material=chunk.sourceMaterial.GetInstanceID(),texture=chunk.mainTexture.GetInstanceID()};
                    if(!materials.TryGetValue(key,out var material))
                    {
                        material=new Material(shader){name="ThinTop_"+binding.SurfaceId+"_Layer"+chunk.layerIndex};prepared.materials.Add(material);
                        material.CopyPropertiesFromMaterial(chunk.sourceMaterial);material.shader=shader;
                        material.SetTexture("_MainTex",chunk.mainTexture);material.SetFloat("_DepthFactor",0);material.SetFloat("_DepthUnits",-chunk.layerIndex);
                        material.renderQueue=2450+Mathf.Clamp(chunk.layerIndex,0,31);materials.Add(key,material);
                    }
                    var go=new GameObject("外观层"+chunk.layerIndex+"_块"+chunk.chunk.x+"_"+chunk.chunk.y);go.layer=chunk.sourceTilemap.gameObject.layer;go.transform.SetParent(prepared.root.transform,false);
                    var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=chunk.mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.enabled=sourceRenderer.enabled;renderer.sortingLayerID=sourceRenderer.sortingLayerID;renderer.sortingOrder=sourceRenderer.sortingOrder;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    var block=new MaterialPropertyBlock();sourceRenderer.GetPropertyBlock(block);
                    block.SetTexture("_MainTex",chunk.mainTexture);block.SetFloat("_DepthFactor",0);block.SetFloat("_DepthUnits",-chunk.layerIndex);renderer.SetPropertyBlock(block);
                    // 原工坊的层眼睛只控制SceneView；复制此状态，保留GameView的enabled与forceRenderingOff语义。
                    if(SceneVisibilityManager.instance.IsHidden(chunk.sourceTilemap.gameObject))SceneVisibilityManager.instance.Hide(go,false);
                    chunks.Add(new GroundSurfaceRenderChunk{filter=filter,renderer=renderer,layerIndex=chunk.layerIndex,chunk=chunk.chunk,sourceTilemap=chunk.sourceTilemap,
                        sourceCells=(Vector3Int[])chunk.cells.Clone(),sourceSprites=(Sprite[])chunk.sprites.Clone()});
                }
                var cap=new Mesh{name="PhysicalCap_"+binding.SurfaceId};prepared.meshes.Add(cap);
                cap.vertices=domain.ring.Select(p=>new Vector3(p.x,binding.boundary.localTopY,p.y)).ToArray();cap.triangles=(int[])domain.triangles.Clone();cap.RecalculateBounds();
                // 显示A/B会停用cacheRoot，物理站立面必须独立，不能跟着显示后端开关消失。
                prepared.colliderObject=new GameObject("平台站立面_"+binding.SurfaceId);prepared.colliderObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(prepared.colliderObject,binding.gameObject.scene);prepared.colliderObject.transform.SetParent(binding.transform,false);
                var collider=prepared.colliderObject.AddComponent<MeshCollider>();collider.sharedMesh=cap;collider.convex=false;prepared.collider=collider;
                cache.chunks=chunks.ToArray();cache.colliderMesh=cap;cache.signature=CacheSignature(fingerprint,built);
                cache.lastBuildMilliseconds=built.elapsedMilliseconds;cache.outputTriangles=built.outputTriangles;cache.outputVertices=built.outputVertices;cache.inputCells=built.inputCells;
                return prepared;
            }
            catch{ReleasePrepared(prepared);throw;}
        }
        static string CacheSignature(string fingerprint,SurfaceMeshBuildResult built)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write("ThinTop/1");writer.Write(fingerprint);writer.Write(built.chunks.Length);
                    foreach(var chunk in built.chunks)
                    {
                        writer.Write(chunk.layerIndex);writer.Write(chunk.chunk.x);writer.Write(chunk.chunk.y);
                        var mesh=chunk.mesh;var vertices=mesh.vertices;var uv=mesh.uv;var colors=mesh.colors32;var indices=mesh.triangles;
                        writer.Write(vertices.Length);foreach(var p in vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                        writer.Write(uv.Length);foreach(var p in uv){writer.Write(p.x);writer.Write(p.y);}
                        writer.Write(colors.Length);foreach(var color in colors){writer.Write(color.r);writer.Write(color.g);writer.Write(color.b);writer.Write(color.a);}
                        writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);
                        writer.Write(chunk.cells.Length);foreach(var cell in chunk.cells){writer.Write(cell.x);writer.Write(cell.y);writer.Write(cell.z);}
                        writer.Write(AssetDatabase.GetAssetPath(chunk.mainTexture));writer.Write(AssetDatabase.GetAssetPath(chunk.sourceMaterial));
                    }
                }
                stream.Position=0;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }
        }
        static void ValidateExistingCache(GroundSurfaceBinding binding)
        {
            if(binding.cacheRoot==null)
            {if(binding.capCollider!=null)throw new InvalidOperationException("平台Collider没有对应的自有缓存根，未删除未知碰撞。");return;}
            var root=binding.cacheRoot;var cache=root.GetComponent<GroundSurfaceMeshCache>();
            if(root.parent!=binding.transform||root.gameObject.scene!=binding.gameObject.scene||EditorUtility.IsPersistent(root)||cache==null||cache.binding!=binding||cache.chunks==null||
               binding.capCollider==null||binding.capCollider.sharedMesh!=cache.colliderMesh||root.childCount!=cache.chunks.Length||
               root.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is GroundSurfaceMeshCache)&&!(c is MeshCollider)))
                throw new InvalidOperationException("现有缓存根/Collider归属不完整或含未知内容，未替换或删除。");
            var cap=binding.capCollider;
            // 兼容首版显示根上的Collider；重建后统一迁移为直属平台的独立物理对象。
            if(cap.transform!=root&&(cap.transform.parent!=binding.transform||cap.gameObject.scene!=binding.gameObject.scene||cap.transform.childCount!=0||
                cap.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is MeshCollider))||cap.GetComponents<MeshCollider>().Length!=1))
                throw new InvalidOperationException("平台站立碰撞包含外部内容或不属于当前平台，未替换。");
            if(root.GetComponents<MeshCollider>().Any(c=>c!=cap))throw new InvalidOperationException("显示缓存根含未知额外碰撞，未删除。");
            var seen=new HashSet<GameObject>();
            foreach(var chunk in cache.chunks)
                if(chunk==null||chunk.filter==null||chunk.renderer==null||chunk.filter.gameObject!=chunk.renderer.gameObject||chunk.filter.transform.parent!=root||
                   chunk.filter.transform.childCount!=0||!seen.Add(chunk.filter.gameObject)||chunk.filter.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)))
                    throw new InvalidOperationException("旧缓存块包含未知对象/组件或失效引用，未删除现场。");
        }
        static void DestroyOwnedCache(Transform root,GroundSurfaceMeshCache cache,MeshCollider collider)
        {
            var assets=new HashSet<Object>();
            if(cache!=null)
            {
                if(cache.colliderMesh!=null)assets.Add(cache.colliderMesh);
                foreach(var chunk in cache.chunks)
                {if(chunk.filter!=null&&chunk.filter.sharedMesh!=null)assets.Add(chunk.filter.sharedMesh);if(chunk.renderer!=null)foreach(var material in chunk.renderer.sharedMaterials)if(material!=null)assets.Add(material);}
            }
            Object.DestroyImmediate(root.gameObject);
            if(collider!=null)Object.DestroyImmediate(collider.gameObject);
            foreach(var asset in assets)if(asset!=null&&!EditorUtility.IsPersistent(asset))Object.DestroyImmediate(asset);
        }
        static void ReleasePrepared(PreparedCache prepared)
        {
            if(prepared.root!=null)Object.DestroyImmediate(prepared.root);
            if(prepared.colliderObject!=null)Object.DestroyImmediate(prepared.colliderObject);
            foreach(var material in prepared.materials)if(material!=null&&!EditorUtility.IsPersistent(material))Object.DestroyImmediate(material);
            foreach(var mesh in prepared.meshes.Distinct())if(mesh!=null&&!EditorUtility.IsPersistent(mesh))Object.DestroyImmediate(mesh);
        }

        public static void SelectForPainting(GroundSurfaceBinding binding)
        {
            RequireBinding(binding);Selection.activeGameObject=binding.map.gameObject;
            GroundWorkshopWindow.Open();EditorWindow.GetWindow<GroundWorkshopWindow>().SelectSurface(binding.map.surface);
        }
        public static void UseModelVariant(GroundSurfaceBinding binding,int index)
        {
            RequireBinding(binding);if(IsRebuilding)throw new InvalidOperationException("平台缓存事务正在执行，拒绝换模型重入。");
            if(binding.modelVariants==null||index<0||index>=binding.modelVariants.Length||binding.modelVariants[index]==null||binding.sideWalls==null||binding.sideWalls.Length==0||binding.sideWalls[0]==null)
                throw new ArgumentException("模型版本或首侧壁MeshFilter无效。");
            var target=binding.modelVariants[index];var domain=GroundSurfaceModelReader.ReadDomain(binding,target);
            GroundSurfaceModelReader.Fingerprint(binding,target);
            // 在任何作者Undo/指针写入前预检真实作者层，避免几何合法但当前Tile不支持时半换模型。
            var preflight=GroundSurfaceMeshGenerator.Build(binding,domain,EffectiveInputs(binding),true);
            foreach(var chunk in preflight.chunks)if(chunk.mesh!=null)Object.DestroyImmediate(chunk.mesh);
            var filter=binding.sideWalls[0];var previousMesh=filter.sharedMesh;var collider=filter.GetComponent<MeshCollider>();
            bool replaceCollider=collider!=null&&collider.sharedMesh==previousMesh;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("切换平台侧壁模型版本");
            Undo.RecordObject(filter,"切换平台侧壁模型版本");Undo.RecordObject(binding,"切换平台侧壁模型版本");Undo.RecordObject(binding.boundary,"切换平台侧壁模型版本");
            if(replaceCollider)Undo.RecordObject(collider,"切换平台侧壁模型碰撞");
            try
            {
                filter.sharedMesh=target;if(replaceCollider)collider.sharedMesh=target;binding.currentModelVariant=index;
                // 封口本次作者字段差异；派生缓存指针随后替换，不能随作者Undo回到已销毁的缓存。
                Undo.FlushUndoRecordObjects();
                Rebuild(binding,true);EditorUtility.SetDirty(filter);if(replaceCollider)EditorUtility.SetDirty(collider);
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            }
            catch
            {
                IsRebuilding=true;try{Undo.RevertAllDownToGroup(group);binding.InvalidateBoundary();}finally{IsRebuilding=false;}throw;
            }
        }
        public static string DescribeSource(GroundSurfaceBinding binding,int layer,int triangle=0)
        {
            RequireBinding(binding);var cache=binding.cacheRoot==null?null:binding.cacheRoot.GetComponent<GroundSurfaceMeshCache>();
            if(cache==null)return "尚未生成顶盖缓存。";
            if(layer<0||triangle<0)throw new ArgumentOutOfRangeException("来源层号和三角序号必须非负。");
            int remaining=triangle;
            foreach(var chunk in cache.chunks.Where(c=>c.layerIndex==layer))
            {
                if(remaining>=chunk.sourceCells.Length){remaining-=chunk.sourceCells.Length;continue;}
                if(chunk.sourceTilemap==null)throw new InvalidOperationException("临时预览已释放，等待有效缓存刷新后再定位来源。");
                var sprite=chunk.sourceSprites[remaining];var map=binding.map;
                int sourceLayer=Array.FindIndex(map.surface.layers,l=>l.tilemap==chunk.sourceTilemap);GroundRecipe recipe=null;
                var preview=GroundWorkshopAuthoring.ActivePreview;
                if(preview!=null&&preview.map==map&&GroundWorkshopAuthoring.TryGetPreviewTilemap(map,out var previewTilemap,out _,out _,out _)&&previewTilemap==chunk.sourceTilemap)
                    recipe=preview.recipe;
                else if(sourceLayer>=0)recipe=map.layerBindings[sourceLayer].recipe;
                if(recipe==null)throw new InvalidOperationException("缓存片段不能映射到实际作者层或有效配方预览。");
                var block=new MaterialPropertyBlock();chunk.renderer.GetPropertyBlock(block);var detail=block.GetTexture("_SecondTex");
                if(detail==null)detail=chunk.renderer.sharedMaterial.GetTexture("_SecondTex");
                return "平台="+binding.SurfaceId+"；层="+layer+"；Tilemap="+chunk.sourceTilemap.name+"；格="+chunk.sourceCells[remaining]+
                    "；Sprite="+(sprite==null?"缺失":sprite.name)+" ["+AssetDatabase.GetAssetPath(sprite)+"]；mask="+(sprite==null?"缺失":AssetDatabase.GetAssetPath(sprite.texture))+"；配方="+recipe.displayName+
                    "；底纹="+AssetDatabase.GetAssetPath(detail)+"；侧壁="+
                    string.Join(",",binding.sideWalls.Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)))+"；环指纹="+binding.boundary.sourceFingerprint;
            }
            throw new ArgumentOutOfRangeException(nameof(triangle),"该外观层没有指定的真实三角来源。");
        }
    }
}
#endif
