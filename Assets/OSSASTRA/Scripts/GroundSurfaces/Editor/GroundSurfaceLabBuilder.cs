/*
 * 脚本：GroundSurfaceLabBuilder
 * 功能：首次创建复用现有地表/角色/相机的独立高台方案B样板。
 * 边界：拒绝覆盖；先原生另存源场再改变副本。模型生成器只造侧壁，封口从已保存Mesh读回。
 * 协作：原Maps复制独立作者资源，Authoring生成薄顶面和真实Collider；不创建第二个角色运动入口。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Ossastra.GroundResearch;
using Ossastra.GroundResearch.Editor;
using Ossastra.MapResearch;
using C = Ossastra.Characters;
using E = Ossastra.Enemies;
namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfaceLabBuilder
    {
        [MenuItem("Tools/OSSASTRA/高台样板/首次创建方案B样板")]
        public static void Create()
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            if(AssetDatabase.LoadMainAssetAtPath(GroundSurfaceAuthoring.ScenePath)!=null)
                throw new InvalidOperationException("样板已存在，拒绝覆盖；请打开它继续编辑。");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("保留未保存场景，请先完成当前编辑。");
            var original=SceneManager.GetActiveScene();
            if(original.path!=GroundWorkshopOwnership.ReferenceScene)throw new InvalidOperationException("首次装配请先打开已保存的参考工坊Lab。");
            var source=InScene<GroundWorkshopMap>(original).Single();
            var sourceView=InScene<MapResearchRoot>(original).Single();
            if(Mathf.Abs(sourceView.pitch-35f)>.001f)throw new InvalidOperationException("请保留已确认35度镜头，不从旧Builder恢复旧角度。");
            GroundSurfaceTechnicalModels.EnsureFolder(GroundSurfaceTechnicalModels.Root);
            var low=GroundWorkshopMaps.SaveAs(source,GroundSurfaceAuthoring.ScenePath);
            var scene=SceneManager.GetActiveScene();
            low.name="低地_原生Tilemap作者与显示";
            // 仅移除新副本中的旧场地阻挡，保留三个原实体的实际Controller与命中组件。
            foreach(var collider in InScene<Collider>(scene))
                if(collider.GetComponentInParent<C.CharacterRoot>()==null&&collider.GetComponentInParent<E.EnemyRoot>()==null)
                    UnityEngine.Object.DestroyImmediate(collider);
            foreach(var decoration in low.decorationRoot.GetComponentsInChildren<GroundDecorationInstance>(true))
                if(decoration.transform.position.x>4&&decoration.transform.position.x<21&&decoration.transform.position.z>-17&&decoration.transform.position.z<14)
                    UnityEngine.Object.DestroyImmediate(decoration.gameObject);
            var models=GroundSurfaceTechnicalModels.CreateModels();
            var wallMaterials=CreateRockMaterials();
            ConfigureWallFill();
            var a=CreatePlatform(low,"高台A_三米_凹L与非整格缺口",new Vector3(12,3,5),models[0],new[]{models[0],models[1]},wallMaterials);
            var b=CreatePlatform(low,"高台B_五米_斜切边缘",new Vector3(-13,5,9),models[2],new[]{models[2]},wallMaterials);
            CreateLowFloor(low.surface);
            CreateRamp(low,new Vector3(12,0,-14),new Vector3(12,3,-4),new Vector3(12,3,-2),4f);
            var actors=InScene<C.CharacterRoot>(scene).OrderBy(actor=>actor.ActorId,StringComparer.Ordinal).ToArray();
            if(actors.Length!=2)throw new InvalidOperationException("复制场必须保留两个现有角色，不创建替代控制器。");
            actors[0].transform.position=new Vector3(12,.02f,-18);actors[1].transform.position=new Vector3(10,.02f,-18);
            foreach(var enemy in InScene<E.EnemyRoot>(scene)){enemy.transform.position=new Vector3(17,.02f,-13);enemy.AttacksEnabled=false;}
            var view=InScene<MapResearchRoot>(scene).Single();view.pitch=35;
            view.collisionVolumes=InScene<Collider>(scene);view.props=InScene<ScenePropView>(scene);
            var diagnostic=new GameObject("高台接合观察_只改显示").AddComponent<GroundSurfaceLabView>();
            diagnostic.surfaces=new[]{a,b};diagnostic.decorationRoots=new[]{low.decorationRoot,a.map.decorationRoot,b.map.decorationRoot};diagnostic.gameplayCamera=view.gameplayCamera;
            a.map.GetComponent<GroundDecorationOrientation>()?.Refresh();b.map.GetComponent<GroundDecorationOrientation>()?.Refresh();
            GroundSurfaceAuthoring.SaveLab();Selection.activeGameObject=a.gameObject;
        }
        private static GroundSurfaceBinding CreatePlatform(GroundWorkshopMap low,string name,Vector3 position,Mesh mesh,Mesh[] variants,Material[] wallMaterials)
        {
            var parent=new GameObject(name);parent.transform.position=position;
            var map=GroundWorkshopMaps.CopyIntoOpenScene(low,parent.transform,new BoundsInt(-2,-2,0,4,4,1));
            map.name="作者Grid_仍使用原地表工坊";
            foreach(var collider in map.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(Transform child in map.decorationRoot.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            var binding=parent.AddComponent<GroundSurfaceBinding>();binding.map=map;binding.modelVariants=variants;binding.chunkSize=4;
            var side=new GameObject("独立三维侧壁_已保存源Mesh");side.transform.SetParent(parent.transform,false);
            var filter=side.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            side.AddComponent<MeshRenderer>().sharedMaterials=wallMaterials;
            var wallCollider=side.AddComponent<MeshCollider>();wallCollider.sharedMesh=mesh;wallCollider.convex=false;
            binding.sideWalls=new[]{filter};
            var boundary=ScriptableObject.CreateInstance<GroundSurfaceBoundary>();boundary.name="侧壁读回连接环";boundary.localTopY=0;
            AssetDatabase.CreateAsset(boundary,map.registration.assetFolder+"/SurfaceBoundary.asset");binding.boundary=boundary;
            GroundSurfaceAuthoring.Rebuild(binding,true);
            var definitions=map.theme.decorations.Where(d=>d!=null).ToArray();
            if(definitions.Length>0)GroundWorkshopDecorations.Scatter(map,definitions,map.surface.EditableCells,17,4f,1.4f,true);
            return binding;
        }
        private static Material[] CreateRockMaterials()
        {
            GroundSurfaceTechnicalModels.EnsureFolder(GroundSurfaceTechnicalModels.Root+"/Materials");
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("缺URP Lit");
            var result=new Material[2];
            for(int i=0;i<2;i++)
            {
                result[i]=new Material(shader){name=i==0?"自有岩壁暖灰":"自有岩壁暗面"};
                result[i].SetColor("_BaseColor",i==0?new Color(.36f,.32f,.25f):new Color(.26f,.25f,.20f));
                result[i].SetFloat("_Smoothness",.08f);
                result[i].EnableKeyword("_EMISSION");result[i].SetColor("_EmissionColor",new Color(.045f,.039f,.03f));
                AssetDatabase.CreateAsset(result[i],GroundSurfaceTechnicalModels.Root+"/Materials/Wall_"+i+".mat");
            }
            return result;
        }
        public static void ConfigureWallFill()
        {
            if(SceneManager.GetActiveScene().path!=GroundSurfaceAuthoring.ScenePath)throw new InvalidOperationException("只配置新样板的局部照明。");
            var existing=GameObject.Find("岩壁阅读补光_不投影");
            var light=existing==null?new GameObject("岩壁阅读补光_不投影").AddComponent<Light>():existing.GetComponent<Light>();
            if(light==null)throw new InvalidOperationException("本轮补光节点缺Light，未覆盖未知对象。");
            light.type=LightType.Directional;light.intensity=.65f;light.color=new Color(.8f,.86f,1f);light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(35,140,0);
        }
        private static void CreateLowFloor(GroundResearchSurface surface)
        {
            var floor=new GameObject("低地连续碰撞_非高台AABB");floor.transform.SetParent(surface.transform,true);
            var b=surface.WorldBounds;floor.transform.position=new Vector3(b.center.x,-.5f,b.center.z);
            var collider=floor.AddComponent<BoxCollider>();collider.size=new Vector3(b.size.x,1,b.size.z);
        }
        private static void CreateRamp(GroundWorkshopMap low,Vector3 start,Vector3 crest,Vector3 end,float width)
        {
            var go=new GameObject("连续缓坡_真实MeshCollider_带两米平台过渡");
            var vertices=new[]{start+Vector3.left*width*.5f,start+Vector3.right*width*.5f,crest+Vector3.left*width*.5f,crest+Vector3.right*width*.5f,end+Vector3.left*width*.5f,end+Vector3.right*width*.5f};
            var mesh=new Mesh{name="自有缓坡连接面"};mesh.vertices=vertices;mesh.uv=vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();mesh.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,GroundSurfaceTechnicalModels.Root+"/Models/Ramp.asset");
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshCollider>().sharedMesh=mesh;
            var material=new Material(low.layerBindings[0].ownedMaterial){name="缓坡_复用原基层底纹"};material.SetTexture("_MainTex",Texture2D.whiteTexture);material.SetFloat("_BaseCoverage",1);
            AssetDatabase.CreateAsset(material,GroundSurfaceTechnicalModels.Root+"/Materials/Ramp.mat");go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static T[] InScene<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
