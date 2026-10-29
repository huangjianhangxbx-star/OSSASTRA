/* 脚本：GroundWorkshopMaps
 * 功能：创建/另存注册地图、独立主题和每层材质/Tile，保存原生作者数据而不复制第二套格表。
 * 边界：拒绝覆盖已有场景和保护旧场；只保存明确目标，失败清理本次资产并恢复原clean场景设置。
 * 协作：所有权服务校验真实注册；配方只作定义，地图拥有独立生成对象，避免改图串材质。
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopMaps
    {
        public static GroundWorkshopMap CreateMap(GroundTheme theme,string scenePath,Vector2Int size)
        {
            RequireNewPath(scenePath); RequireCleanScenes();
            if(theme==null)throw new InvalidOperationException("先选择已保存的有效主题。");
            GroundWorkshopAuthoring.ValidateSlots(theme.layers);
            string themePath=AssetDatabase.GetAssetPath(theme),themeName=theme.displayName;
            if(string.IsNullOrEmpty(themePath)||EditorUtility.IsDirty(theme))throw new InvalidOperationException("先保存自有主题，再用它新建地图；不会暗存共享定义。");
            if(size.x<2||size.y<2||size.x>128||size.y>128)throw new InvalidOperationException("v1支持每边2—128逻辑格，默认16×12。");
            var previous=EditorSceneManager.GetSceneManagerSetup();Scene scene=default;
            using(var ledger=new GroundAssetLedger())
            {
                try
                {
                    ledger.Reserve(scenePath);
                    scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    // NewScene会卸载未被场景引用的资产，不能继续使用调用栈上的旧Unity对象包装。
                    theme=AssetDatabase.LoadAssetAtPath<GroundTheme>(themePath);
                    if(theme==null)throw new InvalidOperationException("新场中无法重新读取已保存主题："+themePath);
                    var root=new GameObject("星骸地表_"+themeName);root.transform.localScale=new Vector3(4,1,4);
                    var grid=root.AddComponent<Grid>();grid.cellSwizzle=GridLayout.CellSwizzle.XZY;grid.cellSize=Vector3.one;
                    var surface=root.AddComponent<GroundResearchSurface>();surface.grid=grid;
                    surface.editableCells=new BoundsInt(-size.x/2,-size.y/2,0,size.x,size.y,1);
                    var map=root.AddComponent<GroundWorkshopMap>();map.surface=surface;
                    InitializeRegistration(map,theme,scenePath,ledger);
                    surface.layers=new GroundLayer[theme.layers.Length];map.layerBindings=new GroundMapLayerBinding[theme.layers.Length];
                    for(int i=0;i<theme.layers.Length;i++)
                    {
                        var slot=map.theme.layers[i];var go=new GameObject(slot.displayName);go.transform.SetParent(root.transform,false);
                        go.transform.localPosition=new Vector3(0,.006f+i*.002f,0);
                        var tilemap=go.AddComponent<Tilemap>();tilemap.orientation=Tilemap.Orientation.XZ;
                        var renderer=go.AddComponent<TilemapRenderer>();renderer.mode=TilemapRenderer.Mode.Chunk;renderer.sortingOrder=i;
                        var binding=CreateBinding(map,slot,"Layer_"+i,ledger);map.layerBindings[i]=binding;
                        surface.layers[i]=DescribeLayer(slot,tilemap,binding);renderer.sharedMaterial=binding.ownedMaterial;
                    }
                    // 首次创建的满覆盖基层只是初始化；后续打开/刷新没有任何重填路径。
                    foreach(var cell in surface.EditableCells.allPositionsWithin)surface.layers[0].tilemap.SetTile(cell,surface.layers[0].paintTile);
                    var decorationRoot=new GameObject("装饰作者层");decorationRoot.transform.SetParent(root.transform,false);
                    decorationRoot.transform.localScale=new Vector3(.25f,1,.25f);map.decorationRoot=decorationRoot.transform;
                    CreateFloor(surface);CreateCamera(surface);
                    SaveNewMap(map,scenePath);ledger.Commit();Selection.activeGameObject=root;return map;
                }
                catch
                {
                    if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                    if(previous.Length>0)EditorSceneManager.RestoreSceneManagerSetup(previous);
                    throw;
                }
            }
        }
        public static void Save(GroundWorkshopMap map)
        {
            GroundWorkshopOwnership.Require(map);GroundWorkshopAuthoring.CancelPreview();
            GroundWorkshopAuthoring.Rebuild(map);
            foreach(var binding in map.layerBindings){AssetDatabase.SaveAssetIfDirty(binding.ownedMaterial);AssetDatabase.SaveAssetIfDirty(binding.ownedTile);}
            AssetDatabase.SaveAssetIfDirty(map.theme);AssetDatabase.SaveAssetIfDirty(map.registration);
            if(!EditorSceneManager.SaveScene(map.gameObject.scene,map.registration.scenePath))throw new IOException("地图保存失败；当前编辑保留。");
        }
        public static GroundTheme CopyTheme(GroundWorkshopMap map)
        {
            GroundWorkshopOwnership.Require(map);GroundWorkshopAuthoring.CancelPreview();
            using(var ledger=new GroundAssetLedger())
            {
                var copy=UnityEngine.Object.Instantiate(map.theme);copy.assetId="map-theme-"+Guid.NewGuid().ToString("N");
                copy.name=map.theme.name+"_副本";copy.layers=GroundWorkshopAuthoring.CloneSlots(map.theme.layers);
                ledger.Create(copy,map.registration.assetFolder+"/Theme_"+Guid.NewGuid().ToString("N")+".asset");
                Undo.RecordObject(map,"另存当前地图主题副本");map.theme=copy;EditorUtility.SetDirty(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);ledger.Commit();return copy;
            }
        }
        public static GroundWorkshopMap SaveAs(GroundWorkshopMap map,string scenePath)
        {
            GroundWorkshopOwnership.Require(map);RequireNewPath(scenePath);GroundWorkshopAuthoring.CancelPreview();
            for(int i=0;i<SceneManager.sceneCount;i++){var other=SceneManager.GetSceneAt(i);if(other!=map.gameObject.scene&&other.isDirty)
                throw new InvalidOperationException("其他场景有未保存变更，保留现场："+other.path);}
            string oldPath=map.gameObject.scene.path;
            using(var folderLedger=new GroundAssetLedger())
            {folderLedger.Folder(Path.GetDirectoryName(scenePath).Replace('\\','/'));folderLedger.Commit();}
            if(!EditorSceneManager.SaveScene(map.gameObject.scene,scenePath,true))throw new IOException("另存地图副本失败。");
            // 先确切保存副本，再关闭本地图；原文件不被保存，作者新内容已在副本持久化。
            var original=map.gameObject.scene;
            // Unity拒绝关闭最后一个loaded Scene；先加载副本，原内存场景才有明确的接替者。
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            if(!EditorSceneManager.CloseScene(original,true))throw new InvalidOperationException("地图副本已写入，但原内存场景未能关闭。");
            var copy=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GroundWorkshopMap>(true)).Single();
            using(var ledger=new GroundAssetLedger())
            {
                try{RegisterExisting(copy.surface,copy.theme,scenePath,ledger,copy);SaveNewMap(copy,scenePath);ledger.Commit();Selection.activeGameObject=copy.gameObject;return copy;}
                catch
                {
                    // 副本磁盘上仍是完整原生作者快照；回读它，避免内存继续引用将被撤回的新资产。
                    EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
                    Debug.LogError("另存的原生快照已保留，但新注册失败；副本只读供恢复，原文件未改："+oldPath);throw;
                }
            }
        }

        /// <summary>只为已另存的新工坊场登记现有Surface；调用方负责复制源场，绝不对保护源场注册。</summary>
        public static GroundWorkshopMap RegisterExisting(GroundResearchSurface surface,GroundTheme theme,string scenePath)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            if(surface==null||!surface.isActiveAndEnabled||surface.gameObject.scene!=SceneManager.GetActiveScene()||
                !surface.gameObject.scene.isLoaded||surface.gameObject.scene.isDirty||EditorSceneManager.IsPreviewScene(surface.gameObject.scene)||
                surface.gameObject.scene.path!=scenePath||!GroundWorkshopOwnership.IsScenePath(scenePath)||surface.GetComponent<GroundWorkshopMap>()!=null)
                throw new InvalidOperationException("只能登记已另存到工坊白名单的新场Surface。");
            if(theme==null)throw new InvalidOperationException("缺少待注册的已保存主题。");
            GroundWorkshopAuthoring.ValidateSlots(theme.layers);
            if(surface.layers==null||surface.layers.Length!=theme.layers.Length||surface.layers.Any(l=>l==null||!surface.Owns(l.tilemap)))
                throw new InvalidOperationException("待注册层与主题不对应，尚未修改场景。");
            var scene=surface.gameObject.scene;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("登记新工坊地图");
            using(var ledger=new GroundAssetLedger())
            {
                try
                {
                    Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{surface}.Concat(surface.layers.SelectMany(l=>new UnityEngine.Object[]{l.tilemap,l.tilemap.GetComponent<TilemapRenderer>()})).ToArray(),"登记新工坊地图");
                    var component=Undo.AddComponent<GroundWorkshopMap>(surface.gameObject);
                    var map=RegisterExisting(surface,theme,scenePath,ledger,component);
                    SaveNewMap(map,scenePath);ledger.Commit();
                    Undo.FlushUndoRecordObjects();if(Undo.GetCurrentGroup()==group)Undo.CollapseUndoOperations(group);return map;
                }
                catch
                {
                    Undo.FlushUndoRecordObjects();
                    if(Undo.GetCurrentGroup()==group)
                    {
                        Undo.RevertAllDownToGroup(group);
                        // 仅恢复本次新目标，源保护场从未进入注册入口。
                        if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.SaveScene(scene,scenePath);
                    }
                    else ledger.Commit(); // 外部历史可能引用输出，保留恢复现场，不能删出悬空绑定。
                    throw;
                }
                finally{Undo.IncrementCurrentGroup();}
            }
        }
        private static GroundWorkshopMap RegisterExisting(GroundResearchSurface surface,GroundTheme theme,string scenePath,GroundAssetLedger ledger,GroundWorkshopMap existing)
        {
            GroundWorkshopAuthoring.ValidateSlots(theme.layers);
            if(surface.layers==null||surface.layers.Length!=theme.layers.Length||surface.layers.Any(l=>l==null||!surface.Owns(l.tilemap)))
                throw new InvalidOperationException("待登记Surface与主题槽位不对应。");
            var map=existing==null?surface.gameObject.AddComponent<GroundWorkshopMap>():existing;
            string previousMapId=map.registration==null?null:map.registration.mapId;
            var copiedDecorations=map.decorationRoot==null||string.IsNullOrEmpty(previousMapId)?Array.Empty<GroundDecorationInstance>():
                map.decorationRoot.GetComponentsInChildren<GroundDecorationInstance>(true).Where(i=>GroundWorkshopDecorations.Owned(map,i)).ToArray();
            map.surface=surface;InitializeRegistration(map,theme,scenePath,ledger);
            map.layerBindings=new GroundMapLayerBinding[surface.layers.Length];
            for(int i=0;i<surface.layers.Length;i++)
            {
                var layer=surface.layers[i];var oldTile=layer.paintTile;var slot=map.theme.layers[i];
                var attributes=GroundTilemapAttributes.Capture(layer.tilemap,surface.EditableCells);
                var binding=CreateBinding(map,slot,"Layer_"+i,ledger);
                foreach(var cell in surface.EditableCells.allPositionsWithin)
                    if(layer.tilemap.GetTile(cell)==oldTile)ReplaceCell(layer.tilemap,cell,binding.ownedTile);
                attributes.Restore(oldTile,binding.ownedTile);
                map.layerBindings[i]=binding;surface.layers[i]=DescribeLayer(slot,layer.tilemap,binding);
                layer.tilemap.GetComponent<TilemapRenderer>().sharedMaterial=binding.ownedMaterial;
            }
            if(map.decorationRoot==null)
            {
                var root=new GameObject("装饰作者层");root.transform.SetParent(map.transform,false);
                Vector3 s=map.transform.lossyScale;root.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);map.decorationRoot=root.transform;
            }
            if(copiedDecorations.Length>0)RebindCopiedDecorations(map,previousMapId,copiedDecorations,ledger);
            return map;
        }
        private static void RebindCopiedDecorations(GroundWorkshopMap map,string previousMapId,GroundDecorationInstance[] owned,GroundAssetLedger ledger)
        {
            string oldPrefix="Ossastra.GroundWorkshop:"+previousMapId+":",newPrefix="Ossastra.GroundWorkshop:"+map.registration.mapId+":";
            if(owned.Length==0)return;
            var source=owned[0].visual.GetComponent<SpriteRenderer>();
            if(source==null||source.sharedMaterial==null)throw new InvalidOperationException("另存装饰缺少真实Renderer/材质，原图文件未改。");
            var material=UnityEngine.Object.Instantiate(source.sharedMaterial);material.name="Decoration";
            ledger.Create(material,map.registration.assetFolder+"/Decoration.mat");
            foreach(var instance in owned)
            {
                var renderer=instance.visual.GetComponent<SpriteRenderer>();if(renderer==null)throw new InvalidOperationException("另存装饰结构不完整。");
                instance.generationGroup=newPrefix+instance.generationGroup.Substring(oldPrefix.Length);
                instance.instanceId=Guid.NewGuid().ToString("N");renderer.sharedMaterial=material;
            }
            var orientation=map.GetComponent<GroundDecorationOrientation>();
            if(orientation!=null){orientation.map=map;orientation.Refresh();}
        }
        private static void InitializeRegistration(GroundWorkshopMap map,GroundTheme definition,string scenePath,GroundAssetLedger ledger)
        {
            // 先把主题副本交给真实场景组件持有，再创建磁盘资产，避免导入/回收使孤立SO失效。
            var theme=UnityEngine.Object.Instantiate(definition);theme.name=definition.name+"_地图副本";theme.layers=GroundWorkshopAuthoring.CloneSlots(definition.layers);
            map.theme=theme;
            string id=Guid.NewGuid().ToString("N"),folder=GroundWorkshopOwnership.Root+"/Maps/"+id;ledger.Folder(folder);
            var registration=ScriptableObject.CreateInstance<GroundMapRegistration>();registration.mapId=id;registration.scenePath=scenePath;registration.assetFolder=folder;
            map.registration=ledger.Create(registration,folder+"/MapRegistration.asset");
            map.theme=ledger.Create(theme,folder+"/Theme.asset");
        }
        internal static GroundMapLayerBinding CreateBinding(GroundWorkshopMap map,GroundThemeLayer slot,string key,GroundAssetLedger ledger)
        {
            var recipe=slot.recipe;var template=slot.purpose==GroundLayerPurpose.Base?recipe.edgeFamily.fillTemplate:recipe.edgeFamily.paintTemplate;
            var tile=CloneAuthorTile(template);tile.name=slot.displayName+"_地图Tile";
            ledger.Create(tile,map.registration.assetFolder+"/"+key+".asset");
            var shader=Shader.Find(GroundWorkshopOwnership.ShaderName);if(shader==null)throw new InvalidOperationException("缺少工坊URP地表Shader。");
            var material=new Material(shader){name=slot.displayName+"_地图材质"};
            material.SetTexture("_SecondTex",recipe.detailTexture);material.SetFloat("_WorldPeriod",recipe.worldRepeatPeriod);
            material.SetFloat("_Cutoff",recipe.cutoff);material.SetFloat("_BaseCoverage",slot.purpose==GroundLayerPurpose.Base?1:0);
            Bounds b=map.surface.WorldBounds;material.SetVector("_GroundBounds",new Vector4(b.min.x,b.min.z,b.max.x,b.max.z));
            ledger.Create(material,map.registration.assetFolder+"/"+key+".mat");
            return new GroundMapLayerBinding{slotId=slot.slotId,recipe=recipe,ownedTile=tile,ownedMaterial=material};
        }
        private static TileBase CloneAuthorTile(TileBase template)
        {
            // 只适配Unity原生两类定义；已有自定义Tile子类保留自己的实现，不能擅自抹除其匹配行为。
            if(template.GetType()==typeof(RuleTile))
            {
                var copy=ScriptableObject.CreateInstance<GroundAuthorRuleTile>();
                EditorUtility.CopySerializedManagedFieldsOnly(template,copy);return copy;
            }
            if(template.GetType()==typeof(Tile))
            {
                var copy=ScriptableObject.CreateInstance<GroundAuthorTile>();
                EditorUtility.CopySerializedManagedFieldsOnly(template,copy);return copy;
            }
            return UnityEngine.Object.Instantiate(template);
        }
        internal static GroundLayer DescribeLayer(GroundThemeLayer slot,Tilemap tilemap,GroundMapLayerBinding binding)=>new GroundLayer
        {displayName=slot.displayName,isLocked=slot.locked,tilemap=tilemap,paintTile=binding.ownedTile,
         eraseReplacement=slot.purpose==GroundLayerPurpose.Base?binding.ownedTile:null,preview=slot.recipe.edgeFamily.preview,
         sourceId=string.IsNullOrEmpty(slot.recipe.detailAssetId)?slot.recipe.assetId:slot.recipe.detailAssetId,
         sourceContainer=slot.recipe.assetId,sourceObjectId=slot.recipe.edgeFamily.assetId,evidence=slot.recipe.evidence,
         detailTexture=slot.recipe.detailTexture,worldRepeatPeriod=slot.recipe.worldRepeatPeriod};
        internal static void ReplaceCell(Tilemap map,Vector3Int cell,TileBase tile)
        {
            Color color=map.GetColor(cell);TileFlags flags=map.GetTileFlags(cell);Matrix4x4 transform=map.GetTransformMatrix(cell);
            map.SetTile(cell,tile);map.SetTileFlags(cell,TileFlags.None);map.SetColor(cell,color);
            // LockTransform表示规则控制的显示矩阵，不能把旧规则缓存烘焙成新规则的作者输入。
            if((flags&TileFlags.LockTransform)==0)map.SetTransformMatrix(cell,transform);
            map.SetTileFlags(cell,flags);
        }
        private static void SaveNewMap(GroundWorkshopMap map,string path)
        {
            // 已有新目标先完成所有可失败的资源刷新，再一次保存；不会先写新引用再删“临时”资产。
            var dir=Path.GetDirectoryName(path).Replace('\\','/');
            if(!AssetDatabase.IsValidFolder(dir))throw new InvalidOperationException("场景父文件夹尚不存在："+dir);
            if(map.gameObject.scene.path==path) { GroundWorkshopOwnership.Require(map);GroundWorkshopAuthoring.Rebuild(map); }
            else
            {
                // 仅CreateMap的未保存新场走此私有路径；对象/资源均由当前构造函数和ledger创建。
                Bounds b=map.surface.WorldBounds;
                foreach(var binding in map.layerBindings)
                {
                    binding.ownedMaterial.SetVector("_GroundBounds",new Vector4(b.min.x,b.min.z,b.max.x,b.max.z));
                    EditorUtility.SetDirty(binding.ownedMaterial);AssetDatabase.SaveAssetIfDirty(binding.ownedMaterial);
                }
            }
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            if(!EditorSceneManager.SaveScene(map.gameObject.scene,path))throw new IOException("新地图保存失败："+path);
        }
        private static void RequireNewPath(string path)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            if(!GroundWorkshopOwnership.IsScenePath(path)||File.Exists(path)||File.Exists(path+".meta"))
                throw new InvalidOperationException("请选择工坊Maps/Samples内不存在的新.unity；旧研究场和已有文件不会覆盖。");
            // 受控父目录由本次创建账本建立；首次使用不要求用户先手工整理Project文件夹。
        }
        private static void RequireCleanScenes()
        {for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("当前存在未保存场景，保留现场；先保存自己的地图。");}
        private static void CreateFloor(GroundResearchSurface surface)
        {
            var go=new GameObject("连续3D地面_无逐格碰撞");go.transform.SetPositionAndRotation(surface.WorldBounds.center+Vector3.down*.25f,Quaternion.identity);
            var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(surface.WorldBounds.size.x,.5f,surface.WorldBounds.size.z);
        }
        private static void CreateCamera(GroundResearchSurface surface)
        {
            var go=new GameObject("地图预览相机");go.tag="MainCamera";var camera=go.AddComponent<Camera>();camera.orthographic=true;
            camera.orthographicSize=Mathf.Max(surface.WorldBounds.size.x,surface.WorldBounds.size.z)*.65f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.1f,.11f);
            go.transform.rotation=Quaternion.Euler(55,40,0);go.transform.position=surface.WorldBounds.center-go.transform.forward*80f;
            camera.nearClipPlane=.1f;camera.farClipPlane=250f;
        }
    }
}
#endif
