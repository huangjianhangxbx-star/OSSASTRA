/* 功能：对已核实的当前RoomLab做环境根、预设、uGUI入口与光影观察区的幂等增量装配。
 * 边界：仅Root通过Unity Editor API调用；不新建场景、不保存原场、不改旧Profile/角色/业务/原碰撞。
 * 协作：复用原相机/可视根/地图/太阳与补光；自有样板明确登记到共享承载面及静态历史。
 * 生命周期：完整标记只补自有天空/风叶接线，重复不增殖；半成品拒绝覆盖。场景由调用者保存。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ossastra.Development.Environment;
using Ossastra.MapResearch;
using Ossastra.SceneVisibility;
using Ossastra.SceneVisibility.Rooms;

namespace Ossastra.EnvironmentSimulation.Editor
{
 public static class EnvironmentMainSceneSetup
 {
  public const string MainScenePath="Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity";
  const string RootName="EnvironmentSimulationRoot";
  const string AssetRoot="Assets/OSSASTRA/Environment";
  const string Profiles=AssetRoot+"/Profiles";
  const string Materials=AssetRoot+"/ObservationMaterials";
  const string Geometry=AssetRoot+"/ObservationGeometry";
  const string BladePath=Geometry+"/Blade.asset";
  const string WindMaterialPath=Materials+"/ObservationWindFoliage.mat";

  public static EnvironmentObservationAuthoring Apply(Camera camera,SceneVisibilityRoot visibility,MapResearchRoot map,Light sun,Light fill,Vector3 observationOrigin,TMP_FontAsset font)
  {
   var scene=SceneManager.GetActiveScene();
   if(Application.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating||Application.dataPath!="E:/WORLDCREATOR/new/Project_ANKALUTE/Assets"||scene.path!=MainScenePath)
    throw new InvalidOperationException("环境装配仅允许指定工程的当前RoomLab稳定Edit状态。");
   if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("请先退出Prefab编辑，不能把主场装配写入预制体。");
   if(camera==null||visibility==null||map==null||sun==null||fill==null||visibility.gameplayCamera!=camera||map.gameplayCamera!=camera||visibility.roomModule==null)
    throw new ArgumentException("需要明确的原相机、可视/地图根、太阳与独立补光。");
   if(new Component[]{camera,visibility,map,sun,fill}.Any(c=>c.gameObject.scene!=scene)||sun==fill||sun.type!=LightType.Directional||fill.type!=LightType.Directional)
    throw new ArgumentException("装配引用必须属于同一当前场景，两灯必须为不同方向灯。");
   if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count()!=1)
    throw new InvalidOperationException("原场必须恰有一个EventSystem；装配不擅自替换或补建输入系统。");
   var existing=scene.GetRootGameObjects().Where(g=>g.name==RootName).ToArray();
   if(existing.Length>1)throw new InvalidOperationException("环境作者根重复，拒绝猜选。");
   if(existing.Length==1)
   {
    var marker=existing[0].GetComponent<EnvironmentObservationAuthoring>();
    if(marker==null||marker.configurationVersion!=1||marker.environment==null||marker.panel==null||marker.observationRoot==null||marker.authoredRenderers.Length==0||marker.authoredRenderers.Any(r=>r==null))
     throw new InvalidOperationException("存在同名但非完整自有装配，保留现场并要求显式修复。");
    if(marker.environment.gameplayCamera!=camera||marker.panel.visibility!=visibility||marker.panel.map!=map)
     throw new InvalidOperationException("已有环境根的明确引用与本次请求不同，拒绝覆盖。");
    FinishRendering(marker,visibility);return marker;
   }
   if(!Finite(observationOrigin))throw new ArgumentException("观察区位置不是有限数值。");
   var module=visibility.roomModule;
   if(module.sharedSurfaces==null||module.staticEnvironmentSurfaces==null)throw new InvalidOperationException("原可视模块登记表未初始化，装配未创建新根。");
   if(module.sightBlockers==null||module.sightBlockers.Length+4>64)throw new InvalidOperationException("四个自有窗墙/雨棚遮挡盒超过现有64预算。");
   if(Shader.Find("Universal Render Pipeline/Lit")==null)throw new InvalidOperationException("缺少URP Lit，未创建任何场景根。");
   EnsureFolder(AssetRoot);EnsureFolder(Profiles);EnsureFolder(Materials);
   var day=CreateOnce<DayCycleProfileSO>(Profiles+"/MainDayCycle.asset",p=>{p.maximumSunElevation=60;p.sunAzimuthOffset=-45;p.daySunIntensity=1.35f;p.nightSunIntensity=.12f;p.dayFillIntensity=.10f;p.nightFillIntensity=.008f;});
   var weather=CreateWeatherProfiles();var climates=CreateClimateProfiles(weather);
   var concrete=CreateMaterial("ObservationConcrete",new Color(.43f,.47f,.43f),0,.14f);
   var stone=CreateMaterial("ObservationStone",new Color(.55f,.48f,.36f),0,.17f);
   var rough=CreateMaterial("ObservationRough",new Color(.48f,.57f,.48f),0,.08f);
   var smooth=CreateMaterial("ObservationSmooth",new Color(.58f,.51f,.32f),.12f,.52f);
   var metal=CreateMaterial("ObservationMetal",new Color(.60f,.65f,.69f),1,.72f);
   var root=new GameObject(RootName);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);Undo.RegisterCreatedObjectUndo(root,"Create environment simulation root");
   var author=root.AddComponent<EnvironmentObservationAuthoring>();
   var environment=root.AddComponent<EnvironmentRoot>();var lighting=root.AddComponent<EnvironmentLightingBinding>();var panel=root.AddComponent<EnvironmentSimulationPanel>();
   environment.gameplayCamera=camera;environment.dayCycle=day;environment.initialWeather=weather[0];environment.climate=climates[0];environment.lightingBinding=lighting;environment.previewInEditMode=false;environment.clockRunning=false;environment.timeHours=12;environment.weatherTransitionSeconds=5;
   lighting.root=environment;lighting.sun=sun;lighting.fill=fill;
   panel.environment=environment;panel.visibility=visibility;panel.map=map;panel.weatherPresets=weather;panel.climatePresets=climates;panel.font=font;panel.SetExpanded(false);
   author.environment=environment;author.panel=panel;author.observationOrigin=observationOrigin;
   var observation=new GameObject("EnvironmentObservationRoot");observation.transform.SetParent(root.transform,false);observation.transform.position=observationOrigin;author.observationRoot=observation.transform;
   var renderers=new List<Renderer>();var blockers=new List<BoxCollider>();
   var floor=Cube(observation.transform,"ReceiverGround",new Vector3(0,.025f,0),new Vector3(12,.05f,10),concrete,renderers);floor.shadowCastingMode=ShadowCastingMode.Off;
   for(int i=0;i<3;i++)
   {
    float height=new[]{1.2f,2.4f,4f}[i];var pillar=Primitive(observation.transform,PrimitiveType.Cylinder,"SunPillar"+(i+1),new Vector3(-4+i*2.3f,height*.5f+.05f,-2),new Vector3(.8f,height*.5f,.8f),stone,renderers);
    pillar.shadowCastingMode=ShadowCastingMode.On;
    Cube(observation.transform,"Step"+(i+1),new Vector3(-4+i*1.2f,.18f+i*.14f,1),new Vector3(1.15f,.26f+i*.28f,1.7f),concrete,renderers);
   }
   AddBlocker(Cube(observation.transform,"WindowWallLeft",new Vector3(-2,1.55f,3.5f),new Vector3(4,3,.22f),stone,renderers),blockers);
   AddBlocker(Cube(observation.transform,"WindowWallRight",new Vector3(3,1.55f,3.5f),new Vector3(2,3,.22f),stone,renderers),blockers);
   AddBlocker(Cube(observation.transform,"WindowLintel",new Vector3(1,2.8f,3.5f),new Vector3(2,.5f,.22f),stone,renderers),blockers);
   AddBlocker(Cube(observation.transform,"RainAwning",new Vector3(3,3.1f,.65f),new Vector3(3.5f,.22f,3.6f),concrete,renderers),blockers);
   for(int i=0;i<3;i++)Primitive(observation.transform,PrimitiveType.Sphere,new[]{"MatteSample","SmoothSample","MetalSample"}[i],new Vector3(-3.8f+i*2,.8f,-4),Vector3.one*1.45f,new[]{rough,smooth,metal}[i],renderers);
   author.authoredRenderers=renderers.ToArray();author.sightBlockers=blockers.ToArray();
   Undo.RecordObject(module,"Register own environment observation surfaces");
   module.sharedSurfaces=module.sharedSurfaces.Concat(renderers).Distinct().ToArray();
   module.staticEnvironmentSurfaces=module.staticEnvironmentSurfaces.Concat(renderers).Distinct().ToArray();
   module.sightBlockers=module.sightBlockers.Concat(blockers).Distinct().ToArray();
   EditorUtility.SetDirty(module);EditorUtility.SetDirty(environment);EditorUtility.SetDirty(lighting);EditorUtility.SetDirty(panel);EditorUtility.SetDirty(author);
   FinishRendering(author,visibility);root.SetActive(true);EditorSceneManager.MarkSceneDirty(scene);return author;
  }
  static void FinishRendering(EnvironmentObservationAuthoring marker,SceneVisibilityRoot visibility)
  {
   var scene=marker.gameObject.scene;var module=visibility.roomModule;
   bool changed=false;
   if(marker.environment.gameObject!=marker.gameObject||marker.panel.gameObject!=marker.gameObject||!marker.observationRoot.IsChildOf(marker.transform))
    throw new InvalidOperationException("环境标记越过自有根，拒绝补接天空/风样板。");
   var buildings=module.buildings;
   if(buildings==null||buildings.Length==0||buildings.Any(b=>b==null||b.regions==null))throw new InvalidOperationException("缺少当前可视模块已登记的房间，未猜测室内。");
   var rooms=buildings.SelectMany(b=>b.regions).Distinct().ToArray();
   if(rooms.Length==0||rooms.Any(r=>r==null||r.gameObject.scene!=scene))throw new InvalidOperationException("环境室内引用必须来自同场明确登记的RoomRegion。");
   var points=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).Where(l=>l.type==LightType.Point).ToArray();
   if(points.Length!=2)throw new InvalidOperationException("当前收口要求两盏原室内点灯；数量改变时需明确重新登记，不猜选。");
   var legacyPanels=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Ossastra.Development.SceneVisibility.RoomVisibilityLabPanel>(true)).ToArray();
   if(legacyPanels.Length!=1||legacyPanels[0].visibility!=visibility)throw new InvalidOperationException("需要同场唯一且属于原可视根的旧诊断面板，拒绝猜选。");
   var shelters=marker.sightBlockers.Where(b=>b!=null&&b.name=="RainAwning").ToArray();
   if(shelters.Length!=1)throw new InvalidOperationException("自有观察区必须恰有一个已登记RainAwning。");
   var sky=Shader.Find("Hidden/Ossastra/Environment/Sky");var rain=Shader.Find("Hidden/Ossastra/Environment/Rain");var cube=Shader.Find("Skybox/Cubemap");var rainDepthCopy=Shader.Find("Hidden/Ossastra/WorldRendering/LegalDepthCopy");
   if(sky==null||rain==null||cube==null||rainDepthCopy==null||!sky.isSupported||!rain.isSupported||!cube.isSupported||!rainDepthCopy.isSupported)throw new InvalidOperationException("天空/雨/反射/合法雨深度Shader未就绪，保留原场等待编译。");
   var skyBindings=marker.GetComponents<Ossastra.EnvironmentSimulation.Rendering.EnvironmentSkyBinding>();
   if(skyBindings.Length>1||(skyBindings.Length==1&&skyBindings[0].environment!=null&&skyBindings[0].environment!=marker.environment))throw new InvalidOperationException("天空接收方重复或已属于其他环境根。");
   var windGroups=marker.observationRoot.Cast<Transform>().Where(t=>t.name=="WindFoliageSamples").ToArray();
   if(windGroups.Length>1)throw new InvalidOperationException("自有风样板组重复，拒绝猜选或叠加。");
   Renderer[] leaves=null;
   if(windGroups.Length==1)leaves=ValidateWindGroup(windGroups[0],marker,module);
   EnsureFolder(Geometry);EnsureFolder(Materials);
   var mesh=CreateBladeMesh();var leafMaterial=CreateWindMaterial();
   var binding=skyBindings.Length==1?skyBindings[0]:Undo.AddComponent<Ossastra.EnvironmentSimulation.Rendering.EnvironmentSkyBinding>(marker.gameObject);changed=skyBindings.Length==0;
   if(binding.environment!=marker.environment||binding.previewConsumer!=marker.panel||binding.indoorRegions==null||!binding.indoorRegions.SequenceEqual(rooms)||binding.localLights==null||!binding.localLights.SequenceEqual(points)||
      binding.rainShelters==null||!binding.rainShelters.SequenceEqual(shelters)||binding.skyShader!=sky||binding.rainShader!=rain||binding.reflectionSkyShader!=cube||binding.rainDepthCopyShader!=rainDepthCopy)
   {
    Undo.RecordObject(binding,"Finish environment sky and rain references");binding.environment=marker.environment;binding.previewConsumer=marker.panel;binding.indoorRegions=rooms;binding.localLights=points;binding.rainShelters=shelters;
    binding.skyShader=sky;binding.rainShader=rain;binding.reflectionSkyShader=cube;binding.rainDepthCopyShader=rainDepthCopy;EditorUtility.SetDirty(binding);changed=true;
   }
   if(marker.panel.skyBinding!=binding){Undo.RecordObject(marker.panel,"Connect environment panel sky controls");marker.panel.skyBinding=binding;EditorUtility.SetDirty(marker.panel);changed=true;}
   if(marker.panel.legacyDiagnostics!=legacyPanels[0]){Undo.RecordObject(marker.panel,"Connect overlapping legacy diagnostics lease");marker.panel.legacyDiagnostics=legacyPanels[0];EditorUtility.SetDirty(marker.panel);changed=true;}
   if(marker.panel.observationAnchor!=marker.observationRoot){Undo.RecordObject(marker.panel,"Connect observation debug location");marker.panel.observationAnchor=marker.observationRoot;EditorUtility.SetDirty(marker.panel);changed=true;}
   if(leaves==null)
   {
    var group=new GameObject("WindFoliageSamples");group.SetActive(false);group.transform.SetParent(marker.observationRoot,false);Undo.RegisterCreatedObjectUndo(group,"Create owned wind foliage samples");
    leaves=new Renderer[3];var foliage=new Transform[3];
    for(int i=0;i<3;i++)
    {
     var leaf=new GameObject("WindLeaf"+(i+1),typeof(MeshFilter),typeof(MeshRenderer));leaf.transform.SetParent(group.transform,false);
     leaf.transform.localPosition=new Vector3(4.7f,.05f,-3.5f+i*.9f);leaf.transform.localRotation=Quaternion.Euler(0,15+i*52,0);leaf.transform.localScale=Vector3.one*new[]{1f,.85f,1.12f}[i];
     leaf.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=leaf.GetComponent<MeshRenderer>();renderer.sharedMaterial=leafMaterial;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;leaves[i]=renderer;foliage[i]=leaf.transform;
    }
    var wind=group.AddComponent<EnvironmentVegetationWind>();wind.environment=marker.environment;wind.foliage=foliage;
    Undo.RecordObject(module,"Register own dynamic wind surfaces");Undo.RecordObject(marker,"Append owned wind renderer references");
    module.sharedSurfaces=module.sharedSurfaces.Concat(leaves).Distinct().ToArray();marker.authoredRenderers=marker.authoredRenderers.Concat(leaves).Distinct().ToArray();
    EditorUtility.SetDirty(module);EditorUtility.SetDirty(marker);EditorUtility.SetDirty(wind);group.SetActive(true);changed=true;
   }
   if(changed)EditorSceneManager.MarkSceneDirty(scene);
  }
  static Renderer[] ValidateWindGroup(Transform group,EnvironmentObservationAuthoring marker,RoomVisibilityModule module)
  {
   var wind=group.GetComponent<EnvironmentVegetationWind>();var renderers=group.GetComponentsInChildren<Renderer>(true);
   if(wind==null||wind.environment!=marker.environment||wind.foliage==null||wind.foliage.Length!=3||wind.foliage.Distinct().Count()!=3||group.childCount!=3||renderers.Length!=3||group.GetComponentsInChildren<Collider>(true).Length!=0||group.GetComponentsInChildren<Collider2D>(true).Length!=0)
    throw new InvalidOperationException("已有WindFoliageSamples不是完整自有三叶装配，保留半成品并拒绝重复创建。");
   foreach(var leaf in wind.foliage)
   {
    if(leaf==null||leaf.parent!=group||leaf.GetComponent<MeshFilter>()==null||leaf.GetComponent<MeshRenderer>()==null||AssetDatabase.GetAssetPath(leaf.GetComponent<MeshFilter>().sharedMesh)!=BladePath||AssetDatabase.GetAssetPath(leaf.GetComponent<MeshRenderer>().sharedMaterial)!=WindMaterialPath)
     throw new InvalidOperationException("风叶几何/材质/父级已改变，拒绝吞掉未知半成品。");
   }
   if(module.sharedSurfaces==null||module.staticEnvironmentSurfaces==null||module.staticEnvironmentRoots==null||renderers.Any(r=>!marker.authoredRenderers.Contains(r)||!module.sharedSurfaces.Contains(r)||module.staticEnvironmentSurfaces.Contains(r))||module.staticEnvironmentRoots.Any(r=>r!=null&&group.IsChildOf(r)))
    throw new InvalidOperationException("风叶必须明确共享绘制，且不得登记静态历史；需要显式修复原登记。");
   return renderers;
  }
  static Mesh CreateBladeMesh()
  {
   var existing=AssetDatabase.LoadAssetAtPath<Mesh>(BladePath);if(existing!=null){if(existing.vertexCount==0)throw new InvalidOperationException("自有Blade几何为空。");return existing;}
   if(AssetDatabase.LoadMainAssetAtPath(BladePath)!=null)throw new InvalidOperationException("Blade目标已有非Mesh资产。");
   var vertices=new List<Vector3>(15);var uv=new List<Vector2>(15);var triangles=new List<int>(27);
   for(int blade=0;blade<3;blade++)
   {
    float height=.85f+blade*.12f,width=.13f+blade*.015f;var turn=Quaternion.Euler(0,blade*117,0);int start=vertices.Count;
    var polygon=new[]{Vector3.zero,new Vector3(-width,height*.28f,0),new Vector3(-width*.62f,height*.68f,.03f),new Vector3(0,height,.06f),new Vector3(width,height*.44f,.02f)};
    for(int i=0;i<polygon.Length;i++){vertices.Add(turn*polygon[i]);uv.Add(new Vector2((polygon[i].x/width+1)*.5f,polygon[i].y/height));}
    for(int i=1;i<4;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
   }
   var mesh=new Mesh{name="EnvironmentObservationBlade"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,BladePath);AssetDatabase.SaveAssetIfDirty(mesh);return mesh;
  }
  static Material CreateWindMaterial()
  {
   var existing=AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
   if(existing!=null){if(existing.shader==null||existing.shader.name!="Universal Render Pipeline/Lit"||!existing.HasProperty("_Cull")||existing.GetFloat("_Cull")!=0)throw new InvalidOperationException("自有风叶材质须保持URP Lit双面，未改写既有资产。");return existing;}
   var material=CreateMaterial("ObservationWindFoliage",new Color(.18f,.42f,.12f),0,.12f);material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
  }
  static Renderer Primitive(Transform parent,PrimitiveType type,string name,Vector3 position,Vector3 scale,Material material,List<Renderer> renderers)
  {
   var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
   var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;renderer.shadowCastingMode=ShadowCastingMode.On;renderers.Add(renderer);return renderer;
  }
  static Renderer Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,List<Renderer> renderers)=>Primitive(parent,PrimitiveType.Cube,name,position,scale,material,renderers);
  static void AddBlocker(Renderer renderer,List<BoxCollider> blockers){var box=renderer.GetComponent<BoxCollider>();if(box==null)throw new InvalidOperationException("预期Cube遮挡体缺少BoxCollider。");blockers.Add(box);}
  static bool Finite(Vector3 p)=>!(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z));
  static void EnsureFolder(string path)
  {if(AssetDatabase.IsValidFolder(path))return;int split=path.LastIndexOf('/');string parent=path.Substring(0,split);if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);string guid=AssetDatabase.CreateFolder(parent,path.Substring(split+1));if(string.IsNullOrEmpty(guid))throw new InvalidOperationException("不能创建自有目录："+path);}
  static T CreateOnce<T>(string path,Action<T> initialize) where T:ScriptableObject
  {
   var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null)return existing;
   if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("自有目标位置已有其他类型资产："+path);
   var value=ScriptableObject.CreateInstance<T>();value.name=System.IO.Path.GetFileNameWithoutExtension(path);initialize(value);AssetDatabase.CreateAsset(value,path);AssetDatabase.SaveAssetIfDirty(value);return value;
  }
  static Material CreateMaterial(string name,Color color,float metallic,float smoothness)
  {
   string path=Materials+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
   if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("自有材质目标位置已有其他类型资产："+path);
   var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);material.SetFloat("_ReceiveShadows",1);material.DisableKeyword("_RECEIVE_SHADOWS_OFF");AssetDatabase.CreateAsset(material,path);AssetDatabase.SaveAssetIfDirty(material);return material;
  }
  static WeatherProfileSO[] CreateWeatherProfiles()
  {
   var names=new[]{"晴","多云","阴","薄雾","小雨"};var files=new[]{"Clear","PartlyCloudy","Overcast","Mist","LightRain"};var result=new WeatherProfileSO[5];
   for(int i=0;i<result.Length;i++)
   {
    int index=i;result[i]=CreateOnce<WeatherProfileSO>(Profiles+"/Weather_"+files[i]+".asset",p=>
    {
     p.displayName=names[index];p.kind=(WeatherKind)index;p.sunMultiplier=new[]{1f,.8f,.34f,.64f,.42f}[index];p.ambientMultiplier=new[]{1f,.96f,.84f,.92f,.86f}[index];p.skyMultiplier=new[]{1f,.94f,.72f,.91f,.76f}[index];
     p.cloudCoverage=new[]{.16f,.48f,.9f,.62f,.84f}[index];p.cloudDensity=new[]{.5f,.82f,1.1f,.8f,1.15f}[index];p.cloudSpeed=new[]{1f,1.05f,.75f,.5f,1.25f}[index];p.cloudShadowStrength=index==0?0:index==1?.09f:index==4?.08f:.04f;
     p.windSpeed=new[]{.35f,.65f,.9f,.2f,1.2f}[index];p.windYawDegrees=45;p.fogDensity=index==3?.025f:index==4?.01f:0;p.fogColor=new Color(.54f,.62f,.65f);
     p.rain=index==4?.7f:0;p.wetness=index==4?.22f:0;p.temperature=index==3?12:index==4?15:20;p.humidity=new[]{.35f,.5f,.66f,.86f,.92f}[index];
     p.sunlightTint=index>=2?new Color(.89f,.94f,1):Color.white;p.ambientTint=index>=2?new Color(.9f,.96f,1):Color.white;
    });
   }
   return result;
  }
  static ClimateProfileSO[] CreateClimateProfiles(WeatherProfileSO[] weather)
  {
   var names=new[]{"温和晴朗","湿润多云","冷色干燥"};var files=new[]{"TemperateClear","HumidCloudy","CoolDry"};var result=new ClimateProfileSO[3];
   for(int i=0;i<3;i++){int index=i;result[i]=CreateOnce<ClimateProfileSO>(Profiles+"/Climate_"+files[i]+".asset",p=>{p.displayName=names[index];p.weatherProfiles=weather;p.weatherWeights=index==0?new[]{5f,3f,1f,.4f,.6f}:index==1?new[]{1f,3f,4f,2f,3f}:new[]{4f,2f,1f,.7f,.2f};p.weatherDurationSeconds=new Vector2(40,120);p.seed=12345+index;p.temperatureOffset=index==2?-8:0;p.humidityOffset=index==1?.12f:index==2?-.12f:0;p.windMultiplier=index==2?1.2f:1;p.environmentTint=index==2?new Color(.84f,.92f,1):index==1?new Color(.94f,1,.96f):Color.white;});}
   return result;
  }
 }
}
