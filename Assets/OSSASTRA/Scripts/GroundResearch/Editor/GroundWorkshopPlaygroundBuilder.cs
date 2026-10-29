/*
 * 脚本：GroundWorkshopPlaygroundBuilder
 * 功能：项目可选的双角色工坊场和当前作者地图试玩装配；纯工坊插件不依赖此文件。
 * 负责：从已保存Frontier另存独立副本、包装七层主题、在临时副本替换作者地表和碰撞。
 * 不负责：角色控制、输入、仲裁、动画、伤害或原Prefab修改；不保存保护旧场。
 * 协作：Core PlayHooks只转发；Maps登记/保存作者数据，Session负责跨Domain返回和临时输出清理。
 * 关键约束：源场只读复制，已有Lab不覆盖；临时场保留原角色/会话/镜头链且作者入口只读。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.MapResearch;
using Ossastra.MapResearchIntegration;
using Ossastra.MapResearchIntegration.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using C = Ossastra.Characters;
using E = Ossastra.Enemies;

namespace Ossastra.GroundResearch.Editor
{
    [InitializeOnLoad]
    public static class GroundWorkshopPlaygroundBuilder
    {
        internal const string LegacyCollisionName="地表碰撞_固定世界尺度";
        private static readonly string[] legacyBoxes={"地表连续碰撞","地表边界_西","地表边界_东","地表边界_南","地表边界_北"};
        static GroundWorkshopPlaygroundBuilder()
        { GroundWorkshopPlayHooks.Register(PlayCurrentMap,OpenReferenceLab); }

        [MenuItem("Tools/OSSASTRA/地表工坊/打开双角色工坊场")]
        public static void OpenReferenceLab()
        {
            RequireCleanContext(null);
            string targetPath=GroundWorkshopOwnership.ReferenceScene;
            if(File.Exists(targetPath))
            {
                var existing=EditorSceneManager.OpenScene(targetPath,OpenSceneMode.Single);
                var map=InScene<GroundWorkshopMap>(existing).SingleOrDefault();
                if(map==null || !GroundWorkshopOwnership.CanEdit(map,out string _))
                    throw new InvalidOperationException("已有双角色工坊场缺有效注册，已保留原文件供检查，不覆盖重建："+targetPath);
                VerifyGameplay(existing,map.surface,false);
                Selection.activeGameObject=map.gameObject;return;
            }
            if(File.Exists(targetPath+".meta"))throw new InvalidOperationException("目标场仅剩meta，保留恢复现场："+targetPath);
            string sourcePath=FrontierGroundSceneBuilder.ScenePath;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(sourcePath)==null)
                throw new InvalidOperationException("缺少已保存的双角色Frontier源场，不重新生成："+sourcePath);
            Scene previous=SceneManager.GetActiveScene(),source=SceneManager.GetSceneByPath(sourcePath),target=default;
            bool opened=!source.IsValid()||!source.isLoaded;
            try
            {
                if(opened)source=EditorSceneManager.OpenScene(sourcePath,OpenSceneMode.Additive);
                var oldSurface=InScene<GroundResearchSurface>(source).SingleOrDefault();
                VerifyGameplay(source,oldSurface);
                if(source.isDirty)throw new InvalidOperationException("保护源场dirty，未保存或复制。");
                if(!EditorSceneManager.SaveScene(source,targetPath,true))throw new IOException("双角色工坊场另存副本失败。");
                target=EditorSceneManager.OpenScene(targetPath,OpenSceneMode.Additive);SceneManager.SetActiveScene(target);
                var surface=InScene<GroundResearchSurface>(target).Single();VerifyGameplay(target,surface);
                using(var ledger=new GroundAssetLedger())
                {
                    var theme=CreateReferenceTheme(surface,ledger);
                    var map=GroundWorkshopMaps.RegisterExisting(surface,theme,targetPath);
                    ledger.Commit();
                    var enemy=InScene<E.EnemyRoot>(target).Single();enemy.AttacksEnabled=false;
                    EditorUtility.SetDirty(enemy);PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                    GroundWorkshopMaps.Save(map);Selection.activeGameObject=map.gameObject;
                }
                CloseOtherCleanScenes(target);
            }
            catch
            {
                Debug.LogError("双角色工坊场未完成；保护源场未保存，已另存的新目标保留为恢复检查点："+targetPath);
                throw;
            }
            finally
            {
                if(opened&&source.IsValid()&&source.isLoaded&&!source.isDirty)EditorSceneManager.CloseScene(source,true);
                if(target.IsValid()&&target.isLoaded)SceneManager.SetActiveScene(target);
                else if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/OSSASTRA/地表工坊/用当前地图进入角色试玩")]
        public static void PlayCurrentMap()
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            GroundWorkshopMap map=null;
            if(Selection.activeGameObject!=null)map=Selection.activeGameObject.GetComponentInParent<GroundWorkshopMap>();
            if(map==null)
            {
                var maps=InScene<GroundWorkshopMap>(SceneManager.GetActiveScene());
                if(maps.Length==1)map=maps[0];
            }
            GroundWorkshopOwnership.Require(map);RequireCleanContext(map.gameObject.scene);
            foreach(var brush in Resources.FindObjectsOfTypeAll<FrontierGroundBrush>())
                if(brush!=null&&brush.Surface==map.surface)brush.FinishStroke(false);
            GroundWorkshopMaps.Save(map);
            GroundWorkshopPlaygroundSession.Begin(map);
        }

        private static GroundTheme CreateReferenceTheme(GroundResearchSurface surface,GroundAssetLedger ledger)
        {
            if(surface.layers==null||surface.layers.Length!=7)throw new InvalidOperationException("参考主题只接受已核实七层源场。");
            var fill=surface.layers[0].paintTile as Tile;
            var rule=surface.layers.Skip(1).Select(l=>l.paintTile).OfType<RuleTile>().FirstOrDefault();
            if(fill==null||fill.sprite==null||rule==null||rule.m_DefaultSprite==null||surface.layers.Skip(1).Any(l=>l.paintTile!=rule))
                throw new InvalidOperationException("源七层的满覆盖Tile/共享RuleTile与已核结构不一致。");
            string folder=GroundWorkshopOwnership.Root+"/LocalReference/ReferenceLab/"+Guid.NewGuid().ToString("N");
            ledger.Folder(folder);
            var family=ScriptableObject.CreateInstance<GroundEdgeFamily>();
            family.assetId="reference.frontier.edge-family";family.displayName="本机开拓参考边缘族";
            family.evidence="SOURCE_VERIFIED：既有源场原生Tile/RuleTile引用；未复制源规则至代码。";
            family.paintTemplate=rule;family.fillTemplate=fill;family.preview=fill.sprite;family.validated=true;
            family=ledger.Create(family,folder+"/EdgeFamily.asset");
            GroundCatalogDocument catalog=null;string catalogPath=EditorPrefs.GetString("Ossastra.GroundWorkshop.CatalogPath","");
            if(!string.IsNullOrEmpty(catalogPath)&&File.Exists(catalogPath))
            {
                try{catalog=GroundWorkshopCatalog.Load(catalogPath);}
                catch(Exception e){Debug.LogWarning("参考图仍可沿真实源ID使用，但目录未对接："+e.Message);}
            }
            var slots=new GroundThemeLayer[surface.layers.Length];
            for(int i=0;i<slots.Length;i++)
            {
                var layer=surface.layers[i];
                if(layer==null||layer.detailTexture==null||layer.paintTile==null||!surface.Owns(layer.tilemap))
                    throw new InvalidOperationException("参考层缺实际纹理/Tilemap："+i);
                string texturePath=AssetDatabase.GetAssetPath(layer.detailTexture);
                var entry=catalog==null?null:catalog.entries.FirstOrDefault(e=>e.kind=="GroundDetail"&&(string)e.raw["unityAssetPath"]==texturePath);
                var recipe=ScriptableObject.CreateInstance<GroundRecipe>();
                recipe.assetId="reference.frontier.recipe."+i;recipe.displayName=layer.displayName;
                recipe.detailAssetId=entry==null?layer.sourceId:entry.assetId;
                recipe.detailTexture=layer.detailTexture;recipe.edgeFamily=family;recipe.worldRepeatPeriod=layer.worldRepeatPeriod;
                recipe.evidence=(layer.evidence??"")+"\n"+(entry==null?"目录未对接，保留旧sourceId；该值不是新图鉴assetId。":"共用图鉴assetId已按实际Unity纹理路径匹配。")+
                    "世界采样与Unlit属于UNITY_ADAPTATION，原精确Shader未知。";
                recipe=ledger.Create(recipe,folder+"/Recipe_"+i+".asset");
                slots[i]=new GroundThemeLayer{slotId="reference-"+i,displayName=layer.displayName,recipe=recipe,
                    purpose=i==0?GroundLayerPurpose.Base:i==slots.Length-1?GroundLayerPurpose.Road:GroundLayerPurpose.Overlay,locked=i==0};
            }
            var theme=ScriptableObject.CreateInstance<GroundTheme>();theme.assetId="reference.frontier.seven-layer";
            theme.displayName="本机开拓参考 · 七层地表";theme.layers=slots;theme.evidence="源七层引用的私有包装，不公开原作素材。";
            return ledger.Create(theme,folder+"/Theme.asset");
        }

        internal static void AssembleTemporary(Scene scene,GameObject authorRoot,string sessionId)
        {
            var oldSurface=InScene<GroundResearchSurface>(scene).Single(s=>s.gameObject!=authorRoot);VerifyGameplay(scene,oldSurface);
            if(oldSurface.GetComponentsInChildren<C.CharacterRoot>(true).Length!=0||oldSurface.GetComponentsInChildren<E.EnemyRoot>(true).Length!=0||
                oldSurface.GetComponentsInChildren<MapResearchRoot>(true).Length!=0||oldSurface.GetComponentsInChildren<MapResearchPartyBridge>(true).Length!=0)
                throw new InvalidOperationException("模板地表根混入角色/会话，拒绝替换。");
            var view=InScene<GroundResearchView>(scene).Single();
            var mapRoot=InScene<MapResearchRoot>(scene).Single();
            var actorRoots=InScene<C.CharacterRoot>(scene).Select(actor=>actor.transform)
                .Concat(InScene<E.EnemyRoot>(scene).Select(actor=>actor.transform)).ToArray();
            Func<Transform,bool> belongsToActor=value=>actorRoots.Any(actor=>BelongsTo(value,actor));
            // 不用旧诊断数组推断物理归属。源场若新增了不属于旧地表/实体/作者快照的碰撞或物件，
            // 保留整个临时检查点报错，不能只清数组后让实际旧地板继续挡住新地图，更不能删除未知对象。
            var foreignColliders=InScene<Collider>(scene).Where(c=>!BelongsTo(c.transform,oldSurface.transform)&&
                !BelongsTo(c.transform,authorRoot.transform)&&!belongsToActor(c.transform)).ToArray();
            var foreignProps=InScene<ScenePropView>(scene).Where(p=>!BelongsTo(p.transform,oldSurface.transform)&&
                !BelongsTo(p.transform,authorRoot.transform)&&!belongsToActor(p.transform)).ToArray();
            if(foreignColliders.Length!=0||foreignProps.Length!=0)
                throw new InvalidOperationException("模板存在未识别的外部Collider/ScenePropView；未替换或删除未知对象："+
                    string.Join(",",foreignColliders.Select(c=>c.name).Concat(foreignProps.Select(p=>p.name))));
            var actorColliders=actorRoots.SelectMany(actor=>actor.GetComponentsInChildren<Collider>(true)).Distinct().ToArray();
            // 旧观察面板挂在将被替换的地表根上；只在临时副本复制其配置，继续引用原模板的两玩家输入。
            if(view.transform.IsChildOf(oldSurface.transform))
            {
                var replacement=mapRoot.gameObject.AddComponent<GroundResearchView>();
                EditorUtility.CopySerialized(view,replacement);view=replacement;
            }
            UnityEngine.Object.DestroyImmediate(oldSurface.gameObject);
            var newSurface=authorRoot.GetComponent<GroundResearchSurface>();
            if(newSurface==null||newSurface.WorldArea<=0)throw new InvalidOperationException("临时作者根缺有效Surface。");
            RemoveKnownLegacyCollisions(newSurface);
            var colliders=CreatePlaygroundCollisions(newSurface,sessionId,out Vector3 center,out Vector3 axisX,out Vector3 axisZ);
            mapRoot.props=actorRoots.SelectMany(actor=>actor.GetComponentsInChildren<ScenePropView>(true))
                .Concat(authorRoot.GetComponentsInChildren<ScenePropView>(true)).Distinct().ToArray();
            mapRoot.collisionVolumes=actorColliders.Where(c=>c!=null).Concat(colliders)
                .Concat(authorRoot.GetComponentsInChildren<Collider>(true)).Distinct().ToArray();
            mapRoot.fixedTarget=center;mapRoot.gridOrigin=newSurface.grid.transform.position;
            mapRoot.gridSize=Vector3.Distance(newSurface.grid.CellToWorld(Vector3Int.zero),newSurface.grid.CellToWorld(Vector3Int.right));
            mapRoot.gridExtent=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(newSurface.EditableCells.size.x,newSurface.EditableCells.size.y)/2f),1,32);
            view.surface=newSurface;view.selectionIndex=0;view.panelExpanded=false;
            var players=InScene<C.CharacterRoot>(scene).OrderBy(c=>c.ActorId,StringComparer.Ordinal).ToArray();
            float separation=Mathf.Min(1f,newSurface.WorldBounds.size.x*.15f),enemyOffset=Mathf.Min(4f,newSurface.WorldBounds.size.z*.25f);
            for(int i=0;i<players.Length;i++)players[i].transform.position=center+axisX*(i==0?-separation:separation)+Vector3.up*.02f;
            var enemy=InScene<E.EnemyRoot>(scene).Single();enemy.transform.position=center+axisZ*enemyOffset+Vector3.up*.02f;enemy.AttacksEnabled=false;
            foreach(var value in new UnityEngine.Object[]{mapRoot,view,enemy}){EditorUtility.SetDirty(value);PrefabUtility.RecordPrefabInstancePropertyModifications(value);}
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Collider[] CreatePlaygroundCollisions(GroundResearchSurface surface,string id,out Vector3 center,out Vector3 axisX,out Vector3 axisZ)
        {
            var b=surface.EditableCells;var grid=surface.grid;
            Vector3 a=grid.CellToWorld(new Vector3Int(b.xMin,b.yMin,0)),x=grid.CellToWorld(new Vector3Int(b.xMax,b.yMin,0)),
                z=grid.CellToWorld(new Vector3Int(b.xMin,b.yMax,0)),opposite=grid.CellToWorld(new Vector3Int(b.xMax,b.yMax,0));
            axisX=(x-a).normalized;axisZ=(z-a).normalized;
            float width=Vector3.Distance(a,x),length=Vector3.Distance(a,z);
            if(width<1||length<1||Mathf.Abs(axisX.y)>.001f||Mathf.Abs(axisZ.y)>.001f||Mathf.Abs(opposite.y-a.y)>.001f||Mathf.Abs(Vector3.Dot(axisX,axisZ))>.001f)
                throw new InvalidOperationException("角色试玩v1只支持水平、非剪切的矩形XZ地表；未修改作者图。");
            center=(a+opposite)*.5f;
            var root=new GameObject("工坊试玩连续碰撞_"+id);SceneManager.MoveGameObjectToScene(root,surface.gameObject.scene);
            root.transform.SetPositionAndRotation(center,Quaternion.LookRotation(axisZ,Vector3.up));
            var boxes=new List<Collider>();
            boxes.Add(Box(root.transform,"连续3D地面",new Vector3(0,-.25f,0),new Vector3(width,.5f,length)));
            boxes.Add(Box(root.transform,"边界_西",new Vector3(-width*.5f-.5f,1,0),new Vector3(1,2,length+2)));
            boxes.Add(Box(root.transform,"边界_东",new Vector3(width*.5f+.5f,1,0),new Vector3(1,2,length+2)));
            boxes.Add(Box(root.transform,"边界_南",new Vector3(0,1,-length*.5f-.5f),new Vector3(width+2,2,1)));
            boxes.Add(Box(root.transform,"边界_北",new Vector3(0,1,length*.5f+.5f),new Vector3(width+2,2,1)));
            return boxes.ToArray();
        }
        private static BoxCollider Box(Transform parent,string name,Vector3 position,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;var box=go.AddComponent<BoxCollider>();box.size=size;return box;}
        private static void RemoveKnownLegacyCollisions(GroundResearchSurface surface)
        {
            var root=surface.transform.Find(LegacyCollisionName);if(root==null)return;
            var children=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i)).ToArray();
            if(root.GetComponents<Component>().Any(c=>!(c is Transform))||children.Length!=5||!new HashSet<string>(children.Select(t=>t.name)).SetEquals(legacyBoxes)||
                children.Any(t=>t.childCount!=0||t.GetComponent<BoxCollider>()==null||t.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is BoxCollider))))
                throw new InvalidOperationException("作者地表中的旧碰撞根含未知内容；保留临时检查点，不批量删除。");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
        internal static void VerifyGameplay(Scene scene,GroundResearchSurface surface,bool requireSevenLayers=true)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("双角色场景尚未有效加载。");
            var roots=InScene<MapResearchRoot>(scene);var bridges=InScene<MapResearchPartyBridge>(scene);
            var players=InScene<C.CharacterRoot>(scene);var enemies=InScene<E.EnemyRoot>(scene);
            if(surface==null||surface.grid==null||surface.layers==null||surface.layers.Length<1||surface.layers.Length>16||
                (requireSevenLayers&&surface.layers.Length!=7)||
                surface.layers.Any(l=>l==null||l.tilemap==null||l.paintTile==null||l.detailTexture==null||!surface.Owns(l.tilemap))||
                players.Length!=2||enemies.Length!=1||roots.Length!=1||bridges.Length!=1||InScene<GroundResearchView>(scene).Length!=1)
                throw new InvalidOperationException((requireSevenLayers?"源双角色模板必须有完整七层地表":"已注册双角色工坊场必须有1–16个完整地表层")+
                    "、两CharacterRoot、一EnemyRoot、唯一MapResearchRoot/会话桥/观察面板。");
            var bridge=bridges[0];
            if(bridge.coordinator==null||bridge.characters==null||bridge.enemies==null||roots[0].sessionComponent!=bridge||
                !new HashSet<C.CharacterRoot>(players).SetEquals(bridge.characters)||!new HashSet<E.EnemyRoot>(enemies).SetEquals(bridge.enemies)||
                roots[0].gameplayCamera==null||roots[0].observerCamera==null||roots[0].gameplayCamera==roots[0].observerCamera||InScene<MapResearchSpineDepth>(scene).Length<1)
                throw new InvalidOperationException("双角色模板的真实会话、镜头或SpineDepth引用不完整，不搭建替代控制器。");
        }
        private static bool BelongsTo(Transform value,Transform root)=>value!=null&&root!=null&&(value==root||value.IsChildOf(root));
        internal static void RequireCleanContext(Scene? allowedDirty)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("当前有未命名Scene，无法可靠恢复，保留现场。");
                if(scene.isDirty&&(!allowedDirty.HasValue||scene!=allowedDirty.Value))throw new InvalidOperationException("非目标场景有未保存变更，保留现场："+scene.path);
            }
        }
        private static void CloseOtherCleanScenes(Scene target)
        {
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {var scene=SceneManager.GetSceneAt(i);if(scene==target)continue;if(scene.isDirty)throw new InvalidOperationException("其他Scene变为dirty，未关闭："+scene.path);EditorSceneManager.CloseScene(scene,true);}
        }
        internal static T[] InScene<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
