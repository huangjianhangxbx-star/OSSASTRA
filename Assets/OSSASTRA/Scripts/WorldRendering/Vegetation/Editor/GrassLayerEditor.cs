/* 功能：独立草样区的显式生成、重建与保存入口。
 * 边界：仅自有GrassLab场/配置/数据；不自动OnValidate生成，不保存Main或全资产。
 * 生命周期：生成数据持久化，显示块仍由GrassLayer按保存数据重建；关闭仅恢复原地表显示。
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ossastra.WorldRendering.Vegetation.Editor
{
 [CustomEditor(typeof(GrassLayer))]
 public sealed class GrassLayerEditor : UnityEditor.Editor
 {
  const string AssetRoot="Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/";
  string message="生成与保存只在编辑态显式执行；原图、地形碰撞及主场保持。";
  MessageType messageType=MessageType.Info;
  int bakeResolution=256;

  public override void OnInspectorGUI()
  {
   DrawDefaultInspector();
   var layer=(GrassLayer)target;
   EditorGUILayout.Space();
   EditorGUILayout.LabelField("二维草层作者操作",EditorStyles.boldLabel);
   if(layer.registerRoomVisibility&&!Owned(layer.gameObject.scene.path))
   {
    EditorGUILayout.HelpBox("主场草层已接入房间可见性。当前使用已保存的半幅放置；取消组件勾选可隐藏草层，原地形保留。",MessageType.Info);
    EditorGUILayout.LabelField("当前显示",layer.ClumpCount+" 簇 / "+layer.BlockCount+" 块 / "+layer.TriangleCount+" 三角");
    return;
   }
   EditorGUILayout.HelpBox("独立URP候选；勾选Room登记后由原门户/黑幕控制可见性。",MessageType.Info);
   using(new EditorGUI.DisabledScope(Application.isPlaying))
   {
    if(GUILayout.Button("生成放置数据并预览"))Run(()=>Generate(layer));
    if(GUILayout.Button("从保存数据重建显示块"))Run(()=>{RequireOwned(layer);layer.RebuildFromSaved();return "已从当前保存数据重建显示块；未重新采样或改动原地形。";});
    bakeResolution=EditorGUILayout.IntPopup("地表基础色图尺寸",bakeResolution,new[]{"128","256","512","1024"},new[]{128,256,512,1024});
    if(GUILayout.Button("烘焙当前层基础色"))Run(()=>BakeTerrainColor(layer,bakeResolution));
    if(GUILayout.Button("保存本草场配置、数据与场景"))Run(()=>Save(layer));
   }
   if(GUILayout.Button("关闭草层 / 恢复原地表"))
   {
    if(!Owned(layer.gameObject.scene.path)){message="拒绝操作：只允许本GrassLab草场。";messageType=MessageType.Error;}
    else{layer.SetMode(GrassWindMode.Original,layer.castShadows);message="草层已关闭；原地形、贴图与碰撞未改。";messageType=MessageType.Info;}
   }
   EditorGUILayout.HelpBox(message,messageType);
   EditorGUILayout.LabelField("当前显示",layer.ClumpCount+" 簇 / "+layer.BlockCount+" 块 / "+layer.TriangleCount+" 三角");
  }

  void Run(Func<string> operation)
  {
   try{message=operation();messageType=MessageType.Info;}
   catch(Exception error){message=error.Message;messageType=MessageType.Error;}
  }

  static void RequireOwned(GrassLayer layer)
  {
   if(Application.isPlaying)throw new InvalidOperationException("请先退出此独立草场的Play，再生成或保存。运行时风态切换无需保存。");
   if(layer==null||!layer.gameObject.scene.IsValid()||!Owned(layer.gameObject.scene.path))
    throw new InvalidOperationException("拒绝操作：只允许已保存到GrassLab目录的自有场景，不保存主场或未命名场。");
   if(layer.profile==null||layer.data==null)throw new InvalidOperationException("先指定本草场独立Profile与放置数据资产。");
   if(!Owned(AssetDatabase.GetAssetPath(layer.profile))||!Owned(AssetDatabase.GetAssetPath(layer.data)))
    throw new InvalidOperationException("拒绝写入：Profile与放置数据都必须属于GrassLab目录；源草图可只读引用外部素材。");
  }
  static bool Owned(string path)=>!string.IsNullOrWhiteSpace(path)&&path.StartsWith(AssetRoot,StringComparison.Ordinal)&&path.IndexOf("..",StringComparison.Ordinal)<0;

  static string Generate(GrassLayer layer)
  {
   RequireOwned(layer);
   var placements=layer.Generate(); // 成功完成全部采样后才改写作者数据。
   Undo.RecordObject(layer.data,"生成二维草层放置数据");
   layer.data.placements=placements;
   layer.data.seed=layer.profile.seed;layer.data.density=layer.profile.density;layer.data.maxDensity=layer.profile.maxDensity;
   layer.data.surfaceIds=layer.surfaces.Select(s=>s.id).ToArray();
   EditorUtility.SetDirty(layer.data);layer.RebuildFromSaved();
   return "已生成 "+placements.Length+" 簇；种子 "+layer.data.seed+"。请显式保存本草场后重开读回。";
  }

  static string Save(GrassLayer layer)
  {
   RequireOwned(layer);
   string profilePath=AssetDatabase.GetAssetPath(layer.profile),dataPath=AssetDatabase.GetAssetPath(layer.data),scenePath=layer.gameObject.scene.path;
   string profileJson=EditorJsonUtility.ToJson(layer.profile),dataJson=EditorJsonUtility.ToJson(layer.data);
   AssetDatabase.SaveAssetIfDirty(layer.profile);AssetDatabase.SaveAssetIfDirty(layer.data);
   if(!EditorSceneManager.SaveScene(layer.gameObject.scene,scenePath,false))throw new InvalidOperationException("本草场场景未保存；没有保存任何其它场景。");
   var profile=AssetDatabase.LoadAssetAtPath<GrassLayerProfileSO>(profilePath);var data=AssetDatabase.LoadAssetAtPath<GrassPatchDataSO>(dataPath);
   if(profile==null||data==null||EditorJsonUtility.ToJson(profile)!=profileJson||EditorJsonUtility.ToJson(data)!=dataJson)
    throw new InvalidOperationException("本草场配置/数据读回不一致，请保留现场核查；没有批量保存或回滚其它资产。");
   return "已保存并读回：\n"+profilePath+"\n"+dataPath+"\n"+scenePath;
  }

  // 本按钮只处理一个明确surface，保持已有map/texture GUID；不会通过重新起名无限累积烘焙资产。
  public static string BakeTerrainColor(GrassLayer layer,int resolution=256)
  {
   RequireOwned(layer);
   if(layer.surfaces==null||layer.surfaces.Length!=1||layer.surfaces[0]==null||layer.surfaces[0].collider==null||string.IsNullOrWhiteSpace(layer.surfaces[0].id))
    throw new InvalidOperationException("基础色烘焙首版仅支持一个明确surface；高台和低地请分别保存，不混用全局顶视图。");
   var surface=layer.surfaces[0];var collider=surface.collider;
   if(collider.gameObject.scene!=layer.gameObject.scene)throw new InvalidOperationException("明确surface跨场景，未烘焙。");
   var sources=layer.terrainSources;
   if(sources==null||sources.Length==0)throw new InvalidOperationException("先显式指定本层terrainSources地表Renderer，不自动搜索世界。");
   bool containsGround=false;var allowedBounds=collider.bounds;allowedBounds.Expand(.1f);
   foreach(var source in sources)
   {
    var filter=source==null?null:source.GetComponent<MeshFilter>();
    if(source==null||source.gameObject.scene!=layer.gameObject.scene||filter==null||filter.sharedMesh==null)
     throw new InvalidOperationException("地表源必须为本场景显式MeshRenderer及有效Mesh，不搜索或跨场景采集。");
    if(layer.Renderers.Contains(source))throw new InvalidOperationException("拒绝烘焙草层自身Renderer："+source.name);
    if(source.transform==collider.transform&&filter.sharedMesh==collider.sharedMesh)containsGround=true;
    else
    {
     if(!allowedBounds.Contains(source.bounds.min)||!allowedBounds.Contains(source.bounds.max))
      throw new InvalidOperationException("额外地表层超出本surface世界Bounds及0.05米裕量，未烘焙："+source.name);
     RequireUpwardSurface(source,filter.sharedMesh);
    }
   }
   if(!containsGround)throw new InvalidOperationException("必须登记与本surface MeshCollider同物体、同Mesh的基底Renderer；overlay不能替代surface身份。");
   if(sources.Distinct().Count()!=sources.Length)throw new InvalidOperationException("地表Renderer重复登记，未烘焙。");
   const string shaderPath="Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Rendering/TerrainBaseColorBake.shader";
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);if(shader==null)throw new InvalidOperationException("缺少本模块专用基础色烘焙Shader。");
   var map=layer.terrainColor;
   string dataPath=AssetDatabase.GetAssetPath(layer.data),mapPath=null;
   string stem=(Path.GetDirectoryName(dataPath)+"/"+Path.GetFileNameWithoutExtension(dataPath)).Replace('\\','/');
   if(map!=null)
   {
    mapPath=AssetDatabase.GetAssetPath(map);
    if(!Owned(mapPath))throw new InvalidOperationException("拒绝覆盖非本草场的颜色映射资产。");
    if(!string.IsNullOrWhiteSpace(map.surfaceId)&&map.surfaceId!=surface.id)throw new InvalidOperationException("现有颜色映射属于不同surface；请使用独立数据资产。");
   }
   else
   {
    string canonical=stem+"_TerrainColor.asset";var existing=AssetDatabase.LoadAssetAtPath<GrassTerrainColorSourceSO>(canonical);
    if(existing!=null)
    {
     if(!string.IsNullOrWhiteSpace(existing.surfaceId)&&existing.surfaceId!=surface.id)throw new InvalidOperationException("稳定映射路径已属于另一surface，请先独立另存本层数据。");
     map=existing;mapPath=canonical;
    }
    else mapPath=AssetDatabase.GenerateUniqueAssetPath(canonical);
   }
   var texture=map==null?null:map.texture;
   string texturePath=texture==null?null:AssetDatabase.GetAssetPath(texture);
   if(texture!=null)
   {
    if(!Owned(texturePath)||!texturePath.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)||texture==layer.profile.image||IsSourceTexture(texture,sources))
     throw new InvalidOperationException("拒绝覆盖原图、地表输入纹理或非本草场Native烘焙Texture2D资产。");
   }
   else texturePath=AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(mapPath,null)+"_BaseColor.asset");
   if(map!=null&&map.version==int.MaxValue)throw new InvalidOperationException("颜色映射版本已达上限；未烘焙。");
   int version=map==null?1:Mathf.Max(0,map.version)+1;
   Bounds bounds=sources[0].bounds;for(int i=1;i<sources.Length;i++)bounds.Encapsulate(sources[i].bounds);
   Texture2D temporary=null;
   using(var result=TerrainBaseColorBaker.Bake(surface.id,sources,bounds,resolution,shader,version))
   {
    try
    {
     temporary=result.DetachTexture();
     if(texture==null)
     {
      texture=temporary;AssetDatabase.CreateAsset(texture,texturePath);temporary=null;
     }
     else
     {
      string name=texture.name;Undo.RecordObject(texture,"重烘焙本层基础色");EditorUtility.CopySerialized(temporary,texture);texture.name=name;
     }
     if(map==null){map=ScriptableObject.CreateInstance<GrassTerrainColorSourceSO>();AssetDatabase.CreateAsset(map,mapPath);}
     Undo.RecordObject(map,"保存地表颜色映射");Undo.RecordObject(layer,"绑定地表颜色映射");
     map.surfaceId=result.surfaceId;map.texture=texture;map.worldXZ=result.worldXZ;map.version=result.version;
     map.semantics="UnlitLinearBaseColor; alpha=valid; UV.v=+worldZ";layer.terrainColor=map;
     EditorUtility.SetDirty(texture);EditorUtility.SetDirty(map);EditorUtility.SetDirty(layer);
     AssetDatabase.SaveAssetIfDirty(texture);AssetDatabase.SaveAssetIfDirty(map);
     layer.RebuildFromSaved();
     EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
     if(!EditorSceneManager.SaveScene(layer.gameObject.scene,layer.gameObject.scene.path,false))throw new InvalidOperationException("基础色与映射已保存，但本草场引用未保存；请保留现场处理。");
     var read=AssetDatabase.LoadAssetAtPath<GrassTerrainColorSourceSO>(mapPath);
     if(read==null||read.surfaceId!=surface.id||read.version!=version||read.texture!=texture||read.worldXZ!=result.worldXZ)
      throw new InvalidOperationException("地表颜色映射读回不一致；没有保存其它场景或源图。");
     return "已显式烘焙未受光基础色 v"+version+"；固定路径读回。\n"+texturePath+"\n"+mapPath+"\n不捕获草/角色/阴影/灯光，不每帧更新。";
    }
    finally{if(temporary!=null&&!AssetDatabase.Contains(temporary))UnityEngine.Object.DestroyImmediate(temporary);}
   }
  }
  static bool IsSourceTexture(Texture2D texture,MeshRenderer[] sources)
  {
   foreach(var renderer in sources)foreach(var material in renderer.sharedMaterials)
    if(material!=null&&((material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap")==texture)||(material.HasProperty("_MainTex")&&material.GetTexture("_MainTex")==texture)))return true;
   return false;
  }
  static void RequireUpwardSurface(MeshRenderer source,Mesh mesh)
  {
   if(!mesh.isReadable)throw new InvalidOperationException("额外地表层Mesh须可读以验证朝上表面："+source.name);
   var vertices=mesh.vertices;var normalMatrix=source.localToWorldMatrix.inverse.transpose;var normals=mesh.normals;
   if(normals.Length!=vertices.Length)throw new InvalidOperationException("额外地表层缺少完整朝上法线："+source.name);
   foreach(var normal in normals)
   {
    var world=normalMatrix.MultiplyVector(normal).normalized;
    if(!Finite(world)||world.y<=.0001f)throw new InvalidOperationException("额外地表层含非朝上法线，拒绝墙体、草卡或其它层："+source.name);
   }
   bool hasTriangle=false;var matrix=source.localToWorldMatrix;
   for(int slot=0;slot<mesh.subMeshCount;slot++)
   {
    if(mesh.GetTopology(slot)!=MeshTopology.Triangles)throw new InvalidOperationException("额外地表层必须为三角地表："+source.name);
    var indices=mesh.GetTriangles(slot);
    for(int i=0;i<indices.Length;i+=3)
    {
     var a=matrix.MultiplyPoint3x4(vertices[indices[i]]);var b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
     var normal=Vector3.Cross(b-a,c-a);if(!Finite(normal)||normal.sqrMagnitude<.000000000001f)throw new InvalidOperationException("额外地表层含退化或无效三角形："+source.name);
     if(normal.normalized.y<=.0001f)throw new InvalidOperationException("额外地表层包含非朝上几何面，未烘焙："+source.name);hasTriangle=true;
    }
   }
   if(!hasTriangle)throw new InvalidOperationException("额外地表层没有有效朝上三角形："+source.name);
  }
  static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
 }
}
#endif
