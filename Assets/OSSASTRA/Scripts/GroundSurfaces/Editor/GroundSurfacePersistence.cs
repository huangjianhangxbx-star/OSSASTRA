/*
 * 脚本：GroundSurfacePersistence
 * 功能：把当前派生Mesh/有效材质与封口Collider写入各Map自己的稳定SurfaceCache路径。
 * 边界：不写源纹理/原材质/共享Boundary，不清理历史未知文件，不把预览材质保存成作者配方。
 * 协作：Authoring先同步真实输入；本服务预检全部路径/依赖，再更新自有资产并替换Scene引用。
 * 事务：本批新文件有精确清单，已有缓存资产先备份；失败恢复本批资产/引用，不回滚外部作者历史。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ossastra.GroundResearch;
using Ossastra.GroundResearch.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfacePersistence
    {
        private sealed class ChunkPlan
        {public GroundSurfaceRenderChunk chunk;public string meshPath;public MaterialPlan material;public Mesh mesh,sourceMesh;}
        private sealed class MaterialPlan
        {public Material source,prepared,stored;public string path,signature;}
        private sealed class Plan
        {
            public GroundSurfaceBinding binding;public GroundSurfaceMeshCache cache;public string folder,colliderPath;
            public ChunkPlan[] chunks;public MaterialPlan[] materials;public Mesh collider,sourceCollider;
        }
        public static void Persist(GroundSurfaceBinding binding)=>PersistMany(new[]{binding});

        internal static void PersistMany(GroundSurfaceBinding[] bindings)
        {
            var plans=new List<Plan>();
            try
            {
                foreach(var binding in bindings)plans.Add(Prepare(binding));
                using(var transaction=new CacheAssetTransaction())
                {
                    // 全部预检先于本批资产写入；已有文件只能在其精确所属Map/SurfaceCache中更新。
                    foreach(var plan in plans)
                    {
                        transaction.Folder(plan.folder);
                        foreach(var material in plan.materials)material.stored=transaction.Store(material.prepared,material.path);
                        foreach(var chunk in plan.chunks)chunk.mesh=transaction.Store(chunk.chunk.filter.sharedMesh,chunk.meshPath);
                        plan.collider=transaction.Store(plan.cache.colliderMesh,plan.colliderPath);
                    }
                    foreach(var plan in plans)
                    {
                        foreach(var chunk in plan.chunks)
                        {
                            var filter=chunk.chunk.filter;var renderer=chunk.chunk.renderer;var oldMesh=filter.sharedMesh;var oldMaterial=renderer.sharedMaterial;
                            transaction.OnRollback(()=>{if(filter!=null)filter.sharedMesh=oldMesh;if(renderer!=null)renderer.sharedMaterial=oldMaterial;});
                            filter.sharedMesh=chunk.mesh;renderer.sharedMaterial=chunk.material.stored;EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
                        }
                        var oldCollider=plan.cache.colliderMesh;var oldCap=plan.binding.capCollider.sharedMesh;
                        transaction.OnRollback(()=>{if(plan.cache!=null)plan.cache.colliderMesh=oldCollider;if(plan.binding.capCollider!=null){plan.binding.capCollider.sharedMesh=null;plan.binding.capCollider.sharedMesh=oldCap;}});
                        plan.cache.colliderMesh=plan.collider;plan.binding.capCollider.sharedMesh=null;plan.binding.capCollider.sharedMesh=plan.collider;
                        EditorUtility.SetDirty(plan.cache);EditorUtility.SetDirty(plan.binding.capCollider);EditorUtility.SetDirty(plan.binding);
                    }
                    transaction.Commit();
                }
                // 新引用均已接到稳定资产，释放本缓存原来明确拥有、现已脱离的瞬态对象。
                foreach(var plan in plans)
                {
                    foreach(var source in plan.materials.Select(p=>p.source).Distinct())if(source!=null&&!EditorUtility.IsPersistent(source))Object.DestroyImmediate(source);
                    foreach(var source in plan.chunks.Select(c=>c.sourceMesh).Concat(new[]{plan.sourceCollider}).Distinct())
                        if(source!=null&&!EditorUtility.IsPersistent(source))Object.DestroyImmediate(source);
                    SaveOwnedDefinitions(plan.binding);
                }
            }
            finally{foreach(var plan in plans)foreach(var material in plan.materials??Array.Empty<MaterialPlan>())if(material.prepared!=null&&!EditorUtility.IsPersistent(material.prepared))Object.DestroyImmediate(material.prepared);}
        }

        private static Plan Prepare(GroundSurfaceBinding binding)
        {
            GroundSurfaceAuthoring.RequireForPersistence(binding);
            if(GroundWorkshopAuthoring.ActivePreview!=null&&GroundWorkshopAuthoring.ActivePreview.map==binding.map)
                throw new InvalidOperationException("先取消本平台配方预览并同步重建，不能保存临时输入。");
            if(binding.boundary.sourceFingerprint!=GroundSurfaceModelReader.Fingerprint(binding))throw new InvalidOperationException("模型来源已变化，未保存看似有效的旧连接环。");
            var cache=binding.cacheRoot==null?null:binding.cacheRoot.GetComponent<GroundSurfaceMeshCache>();
            if(cache==null||cache.binding!=binding||cache.colliderMesh==null||binding.capCollider==null||binding.capCollider.sharedMesh!=cache.colliderMesh)
                throw new InvalidOperationException("平台缓存/封口Collider尚未完整同步。");
            string folder=binding.map.registration.assetFolder+"/SurfaceCache";
            var plan=new Plan{binding=binding,cache=cache,folder=folder,colliderPath=folder+"/CapCollider.asset",sourceCollider=cache.colliderMesh};
            var materials=new Dictionary<Material,MaterialPlan>();var chunks=new List<ChunkPlan>();var meshPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var materialPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach(var chunk in cache.chunks)
                {
                    if(chunk==null||chunk.filter==null||chunk.renderer==null||chunk.filter.sharedMesh==null||chunk.renderer.sharedMaterial==null||chunk.sourceTilemap==null)
                        throw new InvalidOperationException("持久化块缺真实Mesh/Renderer/材质/Tilemap。");
                    if(chunk.layerIndex<0||chunk.layerIndex>=binding.map.surface.layers.Length||chunk.sourceTilemap!=binding.map.surface.layers[chunk.layerIndex].tilemap)
                        throw new InvalidOperationException("缓存仍引用预览/未知层，未写入磁盘。");
                    var source=chunk.renderer.sharedMaterial;
                    if(source.shader==null||source.shader.name!="Ossastra/GroundSurfaces/ThinTop")throw new InvalidOperationException("只持久化本后端独立薄顶面材质，不写原Tilemap材质。");
                    var block=new MaterialPropertyBlock();chunk.renderer.GetPropertyBlock(block);
                    var prepared=Object.Instantiate(source);prepared.hideFlags=HideFlags.HideAndDontSave;ApplyBlock(prepared,block);
                    string textureKey=TextureIdentity(prepared.GetTexture("_MainTex"));
                    TextureIdentity(prepared.GetTexture("_SecondTex"));
                    string key="L"+chunk.layerIndex+"_T"+HashText(textureKey).Substring(0,20),signature=MaterialSignature(prepared);
                    if(!materials.TryGetValue(source,out var material))
                    {
                        string path=folder+"/Material_"+key+".mat";
                        if(!materialPaths.Add(path)){Object.DestroyImmediate(prepared);throw new InvalidOperationException("不同来源材质稳定键冲突，未把它们合并成错误绑定。");}
                        material=new MaterialPlan{source=source,prepared=prepared,path=path,signature=signature};materials.Add(source,material);
                    }
                    else
                    {Object.DestroyImmediate(prepared);if(material.signature!=signature)throw new InvalidOperationException("同一材质对象被不同MPB显示参数复用，不能用一个持久材质冒充全部块。");}
                    string meshPath=folder+"/Mesh_"+key+"_C"+Signed(chunk.chunk.x)+"_"+Signed(chunk.chunk.y)+".asset";
                    if(!meshPaths.Add(meshPath))throw new InvalidOperationException("缓存稳定Mesh键冲突，未覆盖不同来源块。");
                    chunks.Add(new ChunkPlan{chunk=chunk,meshPath=meshPath,material=material,sourceMesh=chunk.filter.sharedMesh});
                }
                plan.materials=materials.Values.ToArray();plan.chunks=chunks.ToArray();
                foreach(var material in plan.materials)CheckTarget<Material>(material.path,folder);
                foreach(var chunk in plan.chunks)CheckTarget<Mesh>(chunk.meshPath,folder);
                CheckTarget<Mesh>(plan.colliderPath,folder);
                return plan;
            }
            catch{foreach(var material in materials.Values)if(material.prepared!=null)Object.DestroyImmediate(material.prepared);throw;}
        }
        private static void ApplyBlock(Material material,MaterialPropertyBlock block)
        {
            foreach(string name in new[]{"_MainTex","_SecondTex"})if(block.HasTexture(name))material.SetTexture(name,block.GetTexture(name));
            foreach(string name in new[]{"_WorldPeriod","_Cutoff","_BaseCoverage","_DebugMask","_DepthUnits","_DepthFactor"})if(block.HasFloat(name))material.SetFloat(name,block.GetFloat(name));
            if(block.HasVector("_GroundBounds"))material.SetVector("_GroundBounds",block.GetVector("_GroundBounds"));
            if(block.HasColor("_Color"))material.SetColor("_Color",block.GetColor("_Color"));
        }
        private static string TextureIdentity(Texture texture)
        {
            if(texture==null)throw new InvalidOperationException("派生材质纹理缺失。");
            if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long localId)&&!string.IsNullOrEmpty(guid))return guid+":"+localId;
            if(texture==Texture2D.whiteTexture)return "builtin:white";
            if(texture==Texture2D.blackTexture)return "builtin:black";
            if(texture==Texture2D.grayTexture)return "builtin:gray";
            throw new InvalidOperationException("纹理尚未持久化，不能把临时Texture引用保存进材质："+texture.name);
        }
        private static string MaterialSignature(Material material)
        {
            var text=new StringBuilder(material.shader.name).Append('|').Append(material.renderQueue);
            foreach(string name in new[]{"_MainTex","_SecondTex"})text.Append('|').Append(TextureIdentity(material.GetTexture(name)));
            foreach(string name in new[]{"_WorldPeriod","_Cutoff","_BaseCoverage","_DebugMask","_DepthUnits","_DepthFactor"})text.Append('|').Append(material.GetFloat(name).ToString("R",CultureInfo.InvariantCulture));
            Vector4 b=material.GetVector("_GroundBounds");Color c=material.GetColor("_Color");
            foreach(float value in new[]{b.x,b.y,b.z,b.w,c.r,c.g,c.b,c.a})text.Append('|').Append(value.ToString("R",CultureInfo.InvariantCulture));return text.ToString();
        }
        private static void CheckTarget<T>(string path,string folder)where T:Object
        {
            if(!GroundWorkshopOwnership.IsUnder(path,folder))throw new InvalidOperationException("缓存目标越出本图SurfaceCache。");
            var asset=AssetDatabase.LoadMainAssetAtPath(path);
            if(asset!=null&&!(asset is T)||asset==null&&(File.Exists(path)||File.Exists(path+".meta")))throw new InvalidOperationException("缓存目标已有异类/无法解析资产，保留不覆盖："+path);
        }
        private static void SaveOwnedDefinitions(GroundSurfaceBinding binding)
        {
            string folder=binding.map.registration.assetFolder;
            foreach(var value in new Object[]{binding.boundary,binding.map.theme,binding.map.registration})
            {
                if(value==null||!GroundWorkshopOwnership.IsUnder(AssetDatabase.GetAssetPath(value),folder))throw new InvalidOperationException("拒绝保存共享/外部平台定义。");
                AssetDatabase.SaveAssetIfDirty(value);
            }
        }
        private static string Signed(int value)=>value<0?"n"+(-(long)value):"p"+value;
        private static string HashText(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}

        private sealed class CacheAssetTransaction:IDisposable
        {
            private readonly List<string> created=new List<string>(),folders=new List<string>();
            private readonly Dictionary<Object,Object> backups=new Dictionary<Object,Object>();
            private readonly List<Action> rollback=new List<Action>();private bool committed;
            public void Folder(string folder)
            {
                if(AssetDatabase.IsValidFolder(folder))return;
                if(Directory.Exists(folder)||File.Exists(folder+".meta"))throw new InvalidOperationException("缓存文件夹有未知文件系统现场，未覆盖："+folder);
                string parent=Path.GetDirectoryName(folder).Replace('\\','/');
                if(!AssetDatabase.IsValidFolder(parent))throw new InvalidOperationException("地图资源父目录不存在，未自动创建注册身份。");
                string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));if(string.IsNullOrEmpty(guid))throw new IOException("缓存目录创建失败。");folders.Add(folder);
            }
            public T Store<T>(T source,string path)where T:Object
            {
                var target=AssetDatabase.LoadAssetAtPath<T>(path);
                if(target!=null)
                {
                    if(!backups.ContainsKey(target)){var backup=Object.Instantiate(target);backup.hideFlags=HideFlags.HideAndDontSave;backups.Add(target,backup);}
                    if(target!=source)EditorUtility.CopySerialized(source,target);
                    target.hideFlags=HideFlags.None;target.name=Path.GetFileNameWithoutExtension(path);EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);return target;
                }
                if(File.Exists(path)||File.Exists(path+".meta"))throw new InvalidOperationException("缓存新目标意外存在，未覆盖："+path);
                var copy=Object.Instantiate(source);copy.hideFlags=HideFlags.None;copy.name=Path.GetFileNameWithoutExtension(path);created.Add(path);
                AssetDatabase.CreateAsset(copy,path);AssetDatabase.SaveAssetIfDirty(copy);return copy;
            }
            public void OnRollback(Action action)=>rollback.Add(action);
            public void Commit()=>committed=true;
            public void Dispose()
            {
                if(!committed)
                {
                    foreach(var restore in rollback.AsEnumerable().Reverse())restore();
                    foreach(var row in backups)if(row.Key!=null){EditorUtility.CopySerialized(row.Value,row.Key);row.Key.hideFlags=HideFlags.None;EditorUtility.SetDirty(row.Key);AssetDatabase.SaveAssetIfDirty(row.Key);}
                    foreach(string path in created.AsEnumerable().Reverse())if(File.Exists(path)||File.Exists(path+".meta"))AssetDatabase.DeleteAsset(path);
                    foreach(string folder in folders.AsEnumerable().Reverse())if(AssetDatabase.IsValidFolder(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())AssetDatabase.DeleteAsset(folder);
                }
                foreach(var backup in backups.Values)if(backup!=null)Object.DestroyImmediate(backup);
            }
        }
    }

    public static partial class GroundSurfaceAuthoring
    {
        private static bool savingSurfaceLab;
        static GroundSurfaceAuthoring(){EditorSceneManager.sceneSaving+=SurfaceSceneSaving;}
        internal static void RequireForPersistence(GroundSurfaceBinding binding)=>RequireBinding(binding);

        public static void SaveLab()
        {
            if(savingSurfaceLab||IsRebuilding)throw new InvalidOperationException("平台保存/重建事务正在执行，拒绝重入。");
            var scene=SceneManager.GetActiveScene();RequireLabScene(scene);
            savingSurfaceLab=true;
            try
            {
                PrepareAndPersistLab(scene);
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("悬崖样板保存失败，当前作者场保留。");
            }
            finally{savingSurfaceLab=false;}
        }
        public static void PlayLab()
        {
            SaveLab();var scene=SceneManager.GetActiveScene();RequireLabScene(scene);
            for(int i=0;i<SceneManager.sceneCount;i++){var other=SceneManager.GetSceneAt(i);if(other!=scene&&other.isDirty)throw new InvalidOperationException("其他loaded场有未保存变更，不进入Play："+other.path);}
            var start=EditorSceneManager.playModeStartScene;
            if(start!=null&&AssetDatabase.GetAssetPath(start)!=ScenePath)throw new InvalidOperationException("当前Play起始场指向其他Scene；未篡改用户设置，也未进入错误场景。");
            EditorApplication.isPlaying=true;
        }
        private static void SurfaceSceneSaving(Scene scene,string path)
        {
            if(path!=ScenePath||scene.path!=ScenePath||savingSurfaceLab)return;
            // 首次从保护模板另存/登记的中间快照尚无平台，不把构造流程误当已完成的多平台保存。
            if(!scene.GetRootGameObjects().Any(root=>root.GetComponentsInChildren<GroundSurfaceBinding>(true).Length!=0))return;
            RequireLabScene(scene);savingSurfaceLab=true;
            try{PrepareAndPersistLab(scene);}finally{savingSurfaceLab=false;}
        }
        private static void RequireLabScene(Scene scene)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            if(!scene.IsValid()||!scene.isLoaded||scene.path!=ScenePath||scene!=SceneManager.GetActiveScene()||EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException("只保存当前有效的独立悬崖样板；不切换或保存旧场。");
        }
        private static void PrepareAndPersistLab(Scene scene)
        {
            var maps=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GroundWorkshopMap>(true)).ToArray();
            if(maps.Length==0)throw new InvalidOperationException("新Lab缺已注册作者地图。");
            foreach(var map in maps)GroundWorkshopOwnership.Require(map);
            var bindings=GetBindings();if(bindings.Length==0)throw new InvalidOperationException("新Lab缺平台Binding，不能假称完成保存。");
            foreach(var binding in bindings)
                if(binding.boundary.sourceFingerprint!=GroundSurfaceModelReader.Fingerprint(binding))throw new InvalidOperationException("模型已变化，请明确重取上沿再保存："+binding.SurfaceId);
            RestoreLabViews(scene);
            // 原工坊的遮罩/层眼睛也属于临时观察；先归还各窗口拥有的状态，避免把_DebugMask烘进运行材质。
            foreach(var window in Resources.FindObjectsOfTypeAll<GroundWorkshopWindow>())
                if(window!=null&&window.SelectedMap!=null&&window.SelectedMap.gameObject.scene==scene)window.ReleaseAuthoring();
            GroundWorkshopAuthoring.CancelPreview();
            foreach(var binding in bindings)Rebuild(binding,false);
            GroundSurfacePersistence.PersistMany(bindings);
            foreach(var map in maps)
            {
                foreach(var binding in map.layerBindings){AssetDatabase.SaveAssetIfDirty(binding.ownedTile);AssetDatabase.SaveAssetIfDirty(binding.ownedMaterial);}
                AssetDatabase.SaveAssetIfDirty(map.theme);AssetDatabase.SaveAssetIfDirty(map.registration);
            }
            GroundSurfaceUpdates.ClearPending();
        }
        private static void RestoreLabViews(Scene scene)
        {
            foreach(var view in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).Where(view=>view!=null&&view.GetType().FullName=="Ossastra.GroundSurfaces.GroundSurfaceLabView"))
            {
                var restore=view.GetType().GetMethod("Restore",BindingFlags.Public|BindingFlags.Instance,null,Type.EmptyTypes,null);
                if(restore==null)throw new InvalidOperationException("本Lab观察组件缺少明确Restore入口，未忽略诊断状态保存。");
                try{restore.Invoke(view,null);}catch(TargetInvocationException error){throw new InvalidOperationException("退出本Lab诊断失败，未继续保存。",error.InnerException??error);}
            }
        }
    }
}
#endif
