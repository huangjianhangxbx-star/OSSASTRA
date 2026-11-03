// 功能：对精确历史场景做Native GUID/构建/反向依赖核验；不删除或改变项目资产。
using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Funplay.Editor.Tools.Scripting;
public class NativeCleanupAudit:IFunplayCommand
{
 public void Execute(ExecutionContext ctx)
 {
  if(Application.dataPath!="E:/WORLDCREATOR/new/Project_ANKALUTE/Assets"||Application.isPlaying)throw new Exception("Project/mode guard");
  var candidates=AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes/XingHaiHuiLang/Backups"}).Select(AssetDatabase.GUIDToAssetPath).Concat(new[]{"Assets/OSSASTRA/Crafting/PrivateCheckpoints/BeforeCrafting.unity","Assets/OSSASTRA/WorldProps/PrivateValidation/SC_LegacyCommerceSmoke.unity"}).OrderBy(p=>p).ToArray();
  if(candidates.Length!=17)throw new Exception("Expected exact17; inspect before deleting");
  var set=new HashSet<string>(candidates);var build=EditorBuildSettings.scenes.Select(s=>s.path).ToArray();
  var open=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).path).ToArray();
  var paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&!p.StartsWith("Assets/Script/",StringComparison.Ordinal)&&!p.StartsWith("Assets/Scripts/",StringComparison.Ordinal)&&!set.Contains(p)&&new[]{".unity",".prefab",".asset",".controller",".overrideController",".playable"}.Contains(Path.GetExtension(p))).ToArray();
  var hits=new HashSet<string>(AssetDatabase.GetDependencies(paths,true).Where(set.Contains));
  var rows=candidates.Select(p=>new{path=p,guid=AssetDatabase.AssetPathToGUID(p),roundtrip=AssetDatabase.GUIDToAssetPath(AssetDatabase.AssetPathToGUID(p))==p,build=build.Contains(p),open=open.Contains(p),dependents=hits.Contains(p)?paths.Where(s=>AssetDatabase.GetDependencies(s,true).Contains(p)).ToArray():new string[0]}).ToArray();
  ctx.ReturnValue=new{pass=rows.All(r=>r.guid.Length>0&&r.roundtrip&&!r.build&&!r.open&&r.dependents.Length==0),inspectedAssets=paths.Length,rows};
 }
}
