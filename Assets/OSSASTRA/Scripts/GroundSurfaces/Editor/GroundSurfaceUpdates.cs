/*
 * 脚本：GroundSurfaceUpdates
 * 功能：把真实Tilemap/预览、Undo和新Lab打开事件合并为一次延迟平台重建。
 * 边界：不轮询相机/角色、不每帧三角化、不保存Scene，不自动重取失效模型上沿。
 * 协作：原工坊DisplayEvents发布有效显示改变；Authoring保留旧结果并抛错；本服务记录lastError。
 * 生命周期：编译/Play暂停派生更新；重载保留仍被场景引用的原生资源，关新Lab才释放明确自有瞬态资源。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.GroundResearch;
using Ossastra.GroundResearch.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundSurfaces.Editor
{
    [InitializeOnLoad]
    public static class GroundSurfaceUpdates
    {
        private static readonly HashSet<GroundSurfaceBinding> pending=new HashSet<GroundSurfaceBinding>();
        private static bool scheduled,draining,waiting;
        private static double reloadRetryUntil;
        static GroundSurfaceUpdates()
        {
            Tilemap.tilemapTileChanged+=TilesChanged;
            GroundWorkshopDisplayEvents.Changed+=QueueMap;
            Undo.undoRedoPerformed+=QueueAll;
            Undo.postprocessModifications+=Modified;
            EditorSceneManager.sceneOpened+=SceneOpened;
            EditorSceneManager.sceneClosing+=SceneClosing;
            EditorApplication.playModeStateChanged+=ModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            ScheduleReloadedBindings();
        }
        public static void Queue(GroundSurfaceBinding binding)
        {
            if(!Eligible(binding)||!binding.isActiveAndEnabled||GroundSurfaceAuthoring.IsRebuilding||EditorApplication.isPlayingOrWillChangePlaymode)return;
            pending.Add(binding);Schedule();
        }
        public static void QueueMap(GroundWorkshopMap map)
        {
            if(map==null||map.gameObject.scene.path!=GroundSurfaceAuthoring.ScenePath)return;
            foreach(var binding in AllBindings())if(binding.map==map)Queue(binding);
        }
        public static void ClearPending()
        {
            pending.Clear();EditorApplication.delayCall-=Drain;EditorApplication.update-=Drain;scheduled=false;
            EditorApplication.update-=QueueReloadedBindings;
            EditorApplication.update-=WaitReady;waiting=false;
        }
        private static void TilesChanged(Tilemap map,Tilemap.SyncTile[] tiles)
        {
            if(map==null)return;
            // 临时预览Tilemap不在layerBindings，但仍挂在其原Map祖先下；不按名字或First猜归属。
            var owner=map.GetComponentInParent<GroundWorkshopMap>();if(owner!=null)QueueMap(owner);
        }
        private static void QueueAll(){foreach(var binding in AllBindings())Queue(binding);}
        private static void Schedule()
        {
            if(scheduled||pending.Count==0)return;scheduled=true;EditorApplication.update+=Drain;
        }
        private static void Drain()
        {
            // 只在变更入队后订阅一次，进入即退订；不依赖Inspector重绘才执行的delayCall。
            EditorApplication.update-=Drain;EditorApplication.delayCall-=Drain;
            scheduled=false;if(pending.Count==0||draining)return;
            if(!Ready())
            {
                if(!waiting){waiting=true;EditorApplication.update+=WaitReady;}
                return;
            }
            var active=SceneManager.GetActiveScene();
            var todo=pending.Where(Eligible).Where(b=>b.gameObject.scene==active).ToArray();
            foreach(var binding in todo)pending.Remove(binding);
            draining=true;
            try
            {
                foreach(var binding in todo)
                {
                    try{GroundSurfaceAuthoring.Rebuild(binding,false);}
                    catch(Exception error)
                    {
                        string text="派生显示未更新；作者格及旧缓存保留。"+error.Message;
                        bool changed=binding.lastError!=text;binding.lastError=text;EditorUtility.SetDirty(binding);
                        if(changed)Debug.LogWarning(text,binding);
                    }
                }
            }
            finally{draining=false;}
            // 不为其他活动场循环delay；切回新Lab时sceneOpened/明确显示操作再投递。
        }
        private static bool Ready()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!GroundSurfaceAuthoring.IsRebuilding;
        private static void WaitReady()
        {
            if(!Ready())return;
            EditorApplication.update-=WaitReady;waiting=false;Schedule();
        }
        private static void SceneOpened(Scene scene,OpenSceneMode mode){if(scene.path==GroundSurfaceAuthoring.ScenePath)QueueAll();}
        private static void ModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode){ClearPending();return;}
            if(state==PlayModeStateChange.EnteredEditMode)QueueAll();
        }
        private static UndoPropertyModification[] Modified(UndoPropertyModification[] modifications)
        {
            if(modifications==null||GroundSurfaceAuthoring.IsRebuilding)return modifications;
            foreach(var binding in AllBindings())
            {
                foreach(var modification in modifications)
                {
                    var target=modification.currentValue.target;
                    bool affects=target==binding||target==binding.boundary||target==binding.transform||target==binding.map||target==binding.Surface;
                    if(!affects&&target is MeshFilter filter)affects=(binding.sideWalls??Array.Empty<MeshFilter>()).Contains(filter);
                    if(!affects&&target is Transform transform)
                        affects=(binding.sideWalls??Array.Empty<MeshFilter>()).Any(wall=>wall!=null&&(transform==wall.transform||wall.transform.IsChildOf(transform)))||
                            (binding.Surface.layers??Array.Empty<GroundLayer>()).Any(layer=>layer!=null&&layer.tilemap!=null&&transform==layer.tilemap.transform);
                    if(!affects&&target is Tilemap tilemap)affects=tilemap.GetComponentInParent<GroundWorkshopMap>()==binding.map;
                    if(affects){Queue(binding);break;}
                }
            }
            return modifications;
        }
        private static bool Eligible(GroundSurfaceBinding binding)=>binding!=null&&binding.map!=null&&binding.Surface!=null&&binding.gameObject.scene.IsValid()&&
            binding.gameObject.scene.isLoaded&&binding.gameObject.scene.path==GroundSurfaceAuthoring.ScenePath;
        private static GroundSurfaceBinding[] AllBindings()=>Resources.FindObjectsOfTypeAll<GroundSurfaceBinding>().Where(Eligible).ToArray();
        private static void BeforeReload(){ClearPending();}
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ScriptsReloaded(){ScheduleReloadedBindings();}
        private static void ScheduleReloadedBindings()
        {
            reloadRetryUntil=EditorApplication.timeSinceStartup+10;
            EditorApplication.update-=QueueReloadedBindings;EditorApplication.update+=QueueReloadedBindings;
        }
        private static void QueueReloadedBindings()
        {
            EditorApplication.update-=QueueReloadedBindings;
            // InitializeOnLoad可能早于场景反序列化；有界等待就绪，不驻帧扫描或重建。
            if(!Ready())
            {if(EditorApplication.timeSinceStartup<reloadRetryUntil)EditorApplication.update+=QueueReloadedBindings;return;}
            var bindings=AllBindings();foreach(var binding in bindings)Queue(binding);
            if(SceneManager.GetActiveScene().path==GroundSurfaceAuthoring.ScenePath&&bindings.Length==0&&EditorApplication.timeSinceStartup<reloadRetryUntil)
                EditorApplication.update+=QueueReloadedBindings;
        }
        private static void SceneClosing(Scene scene,bool removing)
        {
            if(scene.path!=GroundSurfaceAuthoring.ScenePath)return;
            foreach(var binding in AllBindings().Where(b=>b.gameObject.scene==scene)){pending.Remove(binding);ReleaseTransient(binding);}
        }
        private static void ReleaseTransient(GroundSurfaceBinding binding)
        {
            if(binding.cacheRoot==null)return;
            var cache=binding.cacheRoot.GetComponent<GroundSurfaceMeshCache>();if(cache==null||cache.binding!=binding)return;
            var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();
            foreach(var chunk in cache.chunks??Array.Empty<GroundSurfaceRenderChunk>())
            {
                if(chunk==null)continue;
                if(chunk.filter!=null&&chunk.filter.transform.IsChildOf(binding.cacheRoot)&&chunk.filter.sharedMesh!=null)meshes.Add(chunk.filter.sharedMesh);
                if(chunk.renderer!=null&&chunk.renderer.transform.IsChildOf(binding.cacheRoot)&&chunk.renderer.sharedMaterial!=null&&
                    chunk.renderer.sharedMaterial.shader!=null&&chunk.renderer.sharedMaterial.shader.name=="Ossastra/GroundSurfaces/ThinTop")materials.Add(chunk.renderer.sharedMaterial);
            }
            if(cache.colliderMesh!=null)meshes.Add(cache.colliderMesh);
            foreach(var mesh in meshes)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
            foreach(var material in materials)if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
        }
    }

    // 只观察当前Lab明确的模型来源重导入，不把保存自己的缓存资产反过来排成无限更新。
    internal sealed class GroundSurfaceSourceImportUpdates : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            var changed=new HashSet<string>((imported??Array.Empty<string>()).Concat(deleted??Array.Empty<string>()).Concat(moved??Array.Empty<string>()).Concat(movedFrom??Array.Empty<string>()),StringComparer.Ordinal);
            foreach(var binding in Resources.FindObjectsOfTypeAll<GroundSurfaceBinding>())
                if(binding!=null&&binding.gameObject.scene.IsValid()&&binding.gameObject.scene.path==GroundSurfaceAuthoring.ScenePath&&
                    binding.sideWalls!=null&&binding.sideWalls.Any(wall=>wall!=null&&wall.sharedMesh!=null&&changed.Contains(AssetDatabase.GetAssetPath(wall.sharedMesh))))GroundSurfaceUpdates.Queue(binding);
        }
    }
}
#endif
