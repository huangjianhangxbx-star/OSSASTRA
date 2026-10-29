/* 脚本：GroundSourceImporter
 * 功能：校验本机白名单，导入原mask/底纹，按原14条规则建立原生RuleTile与独立材质。
 * 边界：不解析/复制完整游戏，不保存旧场，不公开派生规则，不还原原作Shader；所有源依赖和派生产物仅本机。
 * 协作：Builder只通过CreateTilemaps接入原生作者数据，窗口和规则共享同一Tilemap。
 */
#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
using Newtonsoft.Json.Linq;using UnityEditor;using UnityEngine;using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch.Editor
{
 public static class GroundSourceImporter
 {
  public const string ReferenceRoot="Assets/OSSASTRA/Art/FrontierTaleReferenceLocal";
  public const string LocalRoot="Assets/OSSASTRA/GroundResearch/LocalGenerated";
  public const string ManifestPath=ReferenceRoot+"/manifest.json";
  private const string ShaderName="Ossastra/GroundResearch/MaskWorldTile";
  private static string TexturePath(JToken t)=>ReferenceRoot+"/"+(string)t["texturePath"];
  private static string MaterialPath(JToken t)=>LocalRoot+"/Ground_"+(string)t["id"]+".mat";
  private static string RulePath=>LocalRoot+"/OriginalNeighborRules.asset";
  private static string BasePath=>LocalRoot+"/FullCoverageBase.asset";
  public static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');if(i<0)throw new InvalidOperationException("Not an Assets directory: "+path);Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
  private static JObject ReadManifest(string path){if(!File.Exists(path))throw new FileNotFoundException("缺少本机白名单manifest",path);var m=JObject.Parse(File.ReadAllText(path));if((string)m["format"]!="KT-GROUND-01-local-assets-v1"||!(bool)m["localOnly"]||m["layers"].Count()!=7||m["sprites"].Count()!=15||m["ruleTile"]["rules"].Count()!=14)throw new InvalidOperationException("来源身份/依赖数量不符，停止导入。");return m;}
  private static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  private static void CopyVerified(string root,string relative,string sha,string target){string full=Path.GetFullPath(Path.Combine(root,relative));if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||Hash(full)!=sha.ToLowerInvariant())throw new InvalidOperationException("白名单路径/指纹不符："+relative);Folder(Path.GetDirectoryName(target).Replace('\\','/'));if(File.Exists(target)){if(Hash(target)!=sha.ToLowerInvariant())throw new InvalidOperationException("本机目标已改，保留并停止："+target);}else File.Copy(full,target);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);}
  [MenuItem("Tools/OSSASTRA/地表研究/1 导入已提取白名单")]
  public static void ImportMenu(){string file=EditorUtility.OpenFilePanel("选择本机03-converted/manifest.json","","json");if(!string.IsNullOrEmpty(file))Debug.Log(ImportSelected(file));}
  public static string ImportSelected(string manifestPath)
  {
   if(Application.isPlaying)throw new InvalidOperationException("仅Edit导入本轮资源");var m=ReadManifest(manifestPath);string root=Path.GetDirectoryName(Path.GetFullPath(manifestPath));Folder(ReferenceRoot);Folder(LocalRoot);
   foreach(JObject t in m["layers"]){string path=TexturePath(t);CopyVerified(root,(string)t["texturePath"],(string)t["textureSha256"],path);ConfigureTexture(path,t["import"],false,0,Vector2.zero);}
   var atlas=m["maskAtlas"];if(atlas==null)throw new InvalidOperationException("缺少经核对的原生遮罩图集，停止导入");
   string atlasPath=ReferenceRoot+"/"+(string)atlas["path"];CopyVerified(root,(string)atlas["path"],(string)atlas["sha256"],atlasPath);ConfigureMaskAtlas(atlasPath,m);
   File.Copy(manifestPath,ManifestPath,true);AssetDatabase.ImportAsset(ManifestPath,ImportAssetOptions.ForceSynchronousImport);
   var atlasSprites=AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToDictionary(s=>s.name);var sprites=m["sprites"].Select(s=>atlasSprites["mask_"+(int)s["index"]]).ToArray();if(sprites.Length!=15||sprites.Any(s=>s==null)||sprites.Select(s=>s.texture).Distinct().Count()!=1)throw new InvalidOperationException("原图集Sprite导入失败，保留现场");
   var rule=AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);if(rule==null){rule=ScriptableObject.CreateInstance<RuleTile>();AssetDatabase.CreateAsset(rule,RulePath);}rule.m_DefaultSprite=sprites[(int)m["ruleTile"]["defaultSpriteIndex"]];rule.m_DefaultColliderType=Tile.ColliderType.None;rule.m_DefaultGameObject=null;rule.m_TilingRules=new List<RuleTile.TilingRule>();
   foreach(JObject r in m["ruleTile"]["rules"]){var v=new RuleTile.TilingRule{m_Id=(int)r["id"],m_Sprites=r["spriteIndices"].Select(i=>sprites[(int)i]).ToArray(),m_Neighbors=r["neighbors"].Select(n=>(int)n["value"]).ToList(),m_NeighborPositions=r["neighbors"].Select(n=>new Vector3Int((int)n["x"],(int)n["y"],0)).ToList(),m_RuleTransform=(RuleTile.TilingRuleOutput.Transform)(int)r["transform"],m_Output=(RuleTile.TilingRuleOutput.OutputSprite)(int)r["output"],m_RandomTransform=(RuleTile.TilingRuleOutput.Transform)(int)r["randomTransform"],m_PerlinScale=(float)r["perlinScale"],m_MinAnimationSpeed=(float)r["minAnimationSpeed"],m_MaxAnimationSpeed=(float)r["maxAnimationSpeed"],m_ColliderType=Tile.ColliderType.None};rule.m_TilingRules.Add(v);}rule.UpdateNeighborPositions();EditorUtility.SetDirty(rule);AssetDatabase.SaveAssetIfDirty(rule);
   var fill=AssetDatabase.LoadAssetAtPath<Tile>(BasePath);if(fill==null){fill=ScriptableObject.CreateInstance<Tile>();AssetDatabase.CreateAsset(fill,BasePath);}fill.sprite=sprites[0];fill.colliderType=Tile.ColliderType.None;fill.color=Color.white;EditorUtility.SetDirty(fill);AssetDatabase.SaveAssetIfDirty(fill);
   var shader=Shader.Find(ShaderName);if(shader==null)throw new InvalidOperationException("Missing own shader "+ShaderName);int index=0;
   foreach(JObject t in m["layers"]){string path=MaterialPath(t);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.SetTexture("_SecondTex",AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(t)));mat.SetFloat("_WorldPeriod",16);mat.SetFloat("_Cutoff",(float)t["sourceCutoff"]);mat.SetFloat("_BaseCoverage",index++==0?1:0);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);}
   return "已验证并导入7底纹/15边缘Sprite/14原邻接规则；16米世界底纹采样是自有候选，非原Shader公式还原。";
  }
  private static void ConfigureMaskAtlas(string path,JObject manifest)
  {
   var t=AssetImporter.GetAtPath(path)as TextureImporter;if(t==null)throw new InvalidOperationException("缺少原图集Importer");
   t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Multiple;t.spritePixelsPerUnit=99;t.sRGBTexture=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=true;t.isReadable=true;
   var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);
   t.spritesheet=manifest["sprites"].Select(s=>new SpriteMetaData{name="mask_"+(int)s["index"],alignment=(int)SpriteAlignment.Custom,pivot=new Vector2((float)s["pivot"]["x"],(float)s["pivot"]["y"]),rect=new Rect((float)s["packedRect"]["x"],(float)s["packedRect"]["y"],(float)s["packedRect"]["width"],(float)s["packedRect"]["height"])}).ToArray();t.SaveAndReimport();
  }
  private static void ConfigureTexture(string path,JToken info,bool sprite,float ppu,Vector2 pivot){var t=AssetImporter.GetAtPath(path) as TextureImporter;if(t==null)throw new InvalidOperationException("缺少TextureImporter "+path);t.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;t.sRGBTexture=(bool)info["sRGB"];t.wrapMode=(string)info["wrap"]=="Repeat"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.mipmapEnabled=(bool)info["mipmap"];t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=sprite;t.isReadable=true;if(sprite){t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=ppu;t.spritePivot=pivot;var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);}t.SaveAndReimport();}
  public static bool RequiredAssetsReady(out string detail)
  {
   try
   {
    var m=ReadManifest(ManifestPath);var atlas=m["maskAtlas"];
    if(atlas==null)throw new InvalidOperationException("缺少本机原图集身份");
    string atlasPath=ReferenceRoot+"/"+(string)atlas["path"];
    var sprites=AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
    if(sprites.Length!=15||sprites.Any(sprite=>sprite.texture==null))throw new InvalidOperationException("缺少本机原图集或15个边缘Sprite");
    foreach(var t in m["layers"])
    {
     var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(t));
     if(AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(t))==null||mat==null||mat.shader==null||mat.shader.name!=ShaderName||mat.GetTexture("_SecondTex")==null)
      throw new InvalidOperationException("缺少本机底纹/自有Shader材质 "+t["id"]);
    }
    var rule=AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);var fill=AssetDatabase.LoadAssetAtPath<Tile>(BasePath);
    if(rule==null||fill==null||rule.m_DefaultSprite==null||fill.sprite==null||rule.m_TilingRules==null||rule.m_TilingRules.Count!=14||
       rule.m_TilingRules.Any(r=>r.m_Sprites==null||r.m_Sprites.Length==0||r.m_Sprites.Any(sprite=>sprite==null||sprite.texture==null)))
     throw new InvalidOperationException("缺少本机规则配置或所引用的Sprite；保留现场，不重新生成");
    detail="ready";return true;
   }
   catch(Exception e){detail=e.Message;return false;}
  }
  public static GroundLayer[] CreateTilemaps(Transform parent)
  {
   if(parent==null||parent.GetComponent<Grid>()==null)throw new InvalidOperationException("CreateTilemaps需要显式Grid父节点");if(!RequiredAssetsReady(out string detail))throw new InvalidOperationException(detail);var m=ReadManifest(ManifestPath);var rules=AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);var fill=AssetDatabase.LoadAssetAtPath<Tile>(BasePath);var result=new List<GroundLayer>();int index=0;
   foreach(var d in m["layers"]){var go=new GameObject("地表_"+d["id"]+"_"+d["displayName"]);go.transform.SetParent(parent,false);int depthRank=m["layers"].Count(other=>(int)other["sortingOrder"]<(int)d["sortingOrder"]);go.transform.localPosition=new Vector3(0,.006f+depthRank*.002f,0);var map=go.AddComponent<Tilemap>();map.orientation=Tilemap.Orientation.XZ;var renderer=go.AddComponent<TilemapRenderer>();renderer.mode=TilemapRenderer.Mode.Chunk;renderer.sortingOrder=(int)d["sortingOrder"];renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(d));result.Add(new GroundLayer{displayName=(string)d["displayName"],tilemap=map,paintTile=index==0?(TileBase)fill:rules,eraseReplacement=index==0?fill:null,preview=fill.sprite,sourceId=(string)d["id"],sourceContainer=(string)m["source"]["bundle"],sourceObjectId=(string)d["sourceTextureId"],evidence="SOURCE_VERIFIED Tilemap/原Sprite/原规则/底纹；UNITY_ADAPTATION 世界周期16m、不复原原Lit/Ramp",detailTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(d)),worldRepeatPeriod=16});index++;}return result.ToArray();
  }
  public static void UpdateMaterialBounds(GroundResearchSurface surface)
  {
   if(surface==null||surface.layers==null||surface.WorldArea<=0)throw new ArgumentException("缺少有效地表范围");
   var owned=new List<KeyValuePair<Material,GroundLayer>>();
   foreach(var layer in surface.layers)
   {
    if(layer==null||!surface.Owns(layer.tilemap))throw new InvalidOperationException("拒绝更新外部图层材质");
    var renderer=layer.tilemap.GetComponent<TilemapRenderer>();var mat=renderer==null?null:renderer.sharedMaterial;
    string path=mat==null?"":AssetDatabase.GetAssetPath(mat);
    if(mat==null||mat.shader==null||mat.shader.name!=ShaderName||!path.StartsWith(LocalRoot+"/",StringComparison.Ordinal))
     throw new InvalidOperationException("只更新本任务LocalGenerated内的独立地表材质，保留外部材质");
    if(float.IsNaN(layer.worldRepeatPeriod)||float.IsInfinity(layer.worldRepeatPeriod)||layer.worldRepeatPeriod<=0)
     throw new InvalidOperationException("底纹世界周期必须是有限正数");
    owned.Add(new KeyValuePair<Material,GroundLayer>(mat,layer));
   }
   var b=surface.WorldBounds;
   foreach(var pair in owned)
   {
    pair.Key.SetVector("_GroundBounds",new Vector4(b.min.x,b.min.z,b.max.x,b.max.z));
    pair.Key.SetFloat("_WorldPeriod",pair.Value.worldRepeatPeriod);
    EditorUtility.SetDirty(pair.Key);AssetDatabase.SaveAssetIfDirty(pair.Key);
   }
  }
 }
}
#endif
