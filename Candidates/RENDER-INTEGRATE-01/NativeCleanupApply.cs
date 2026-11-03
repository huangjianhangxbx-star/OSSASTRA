// 功能：精确外部备份和移除已核验历史场景，先全部SHA校验再执行Unity删除，不删除共享素材。
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Funplay.Editor.Tools.Scripting;
public class NativeCleanupApply:IFunplayCommand{
class SceneTarget{public string path,guid;public SceneTarget(string p,string g){path=p;guid=g;}}
static string Hash(string p){using(var h=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
public void Execute(ExecutionContext ctx){
const string project="E:/WORLDCREATOR/new/Project_ANKALUTE";
const string archive="E:/WORLDCREATOR/XingHaiHuiLang/研究/渲染接入/RENDER-INTEGRATE-01/RemovedScenes";
if(Application.isPlaying||Application.dataPath!=project+"/Assets")throw new Exception("Project/mode guard");
var targets=new SceneTarget[]{new SceneTarget("Assets/OSSASTRA/Crafting/PrivateCheckpoints/BeforeCrafting.unity","d6eb5fe121e28f045ae8b318082399bf"),
new SceneTarget("Assets/OSSASTRA/WorldProps/PrivateValidation/SC_LegacyCommerceSmoke.unity","f596c417198f3a14ab0de550c22499b2"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforeAnimeV2.unity","5dda9de6256b800469908a55213ea0e3"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforeDecay.unity","017c49a077bb3d74fa7747158a3600d3"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforePaint_20261005.unity","ebc23a77a1a26d746be3ceeb1cabdef4"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_BeforeRenderLab.unity","510c30320000a0946bf3fda0ba0ea1f7"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_BeforeStructureLight.unity","e8b7e892b33e4654e953e5dc7a1f2ce9"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_Exploration_T61_PreviewSafety.unity","766b98b9dedc7a340a48021d24e7c7f7"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_Exploration_T62_PreviewSafety.unity","5d3713b111e2bb64f83aa1f008b09de1"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_Sunset_T61_ResumeSafety.unity","6ae391c1a6fe97b4d87efa1fc089ea8b"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeBlockLight.unity","7a88853901779854fab60affb1446001"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeHD2D.unity","577a6628b5dd100468c80b284451e5bd"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeRenderRefine.unity","26be8f165616c0b4baa8a05110f1b973"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_Validation_T61_PreviewSafety.unity","5beea02fdea75eb47a5a6bb87c5d7b0d"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_Validation_T62_PreviewSafety.unity","a32caf5b3f6c8c541a5f8c0e6363fe4d"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_VoxelRenderLab_Standard_T61_PreviewSafety.unity","076a7ded23ef410459203662246eef9c"),
new SceneTarget("Assets/Scenes/XingHaiHuiLang/Backups/SC_VoxelRenderLab_Standard_T62_PreviewSafety.unity","9767031a8c57fb043af8698560a21ce4")};
var copied=new List<object>();
foreach(var t in targets){
string full=Path.GetFullPath(Path.Combine(project,t.path));
if(!full.StartsWith(Path.GetFullPath(project)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!t.path.EndsWith(".unity",StringComparison.Ordinal)||AssetDatabase.AssetPathToGUID(t.path)!=t.guid)throw new Exception("Identity guard "+t.path);
if(EditorBuildSettings.scenes.Any(s=>s.path==t.path)||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).path==t.path))throw new Exception("Live scene "+t.path);
foreach(var suffix in new[]{"",".meta"}){
string src=full+suffix;string dst=Path.GetFullPath(Path.Combine(archive,t.path+suffix));
if(!dst.StartsWith(Path.GetFullPath(archive)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Archive guard");
if(!File.Exists(src))throw new Exception("Missing original "+src);
Directory.CreateDirectory(Path.GetDirectoryName(dst));
if(!File.Exists(dst))File.Copy(src,dst);
string sha=Hash(src);if(sha!=Hash(dst))throw new Exception("Backup mismatch "+src);
copied.Add(new{path=t.path+suffix,sha256=sha,bytes=new FileInfo(src).Length,backup=dst});
}}
var results=new List<object>();
foreach(var t in targets){bool deleted=AssetDatabase.DeleteAsset(t.path);results.Add(new{t.path,t.guid,deleted,remaining=File.Exists(Path.Combine(project,t.path))});if(!deleted)throw new Exception("Delete failed "+t.path);}
ctx.ReturnValue=new{deleted=results.Count,backups=copied,results,buildValid=EditorBuildSettings.scenes.All(s=>AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path)!=null)};
}}
