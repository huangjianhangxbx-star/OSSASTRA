/* 功能：仅独立草场Player的显式命令行成本测量；普通启动不自动采样。
 * 边界：不修改作者资产/主场/质量；密度档为进程内副本。结束归还进程限帧并退出。
 */
using System;using System.IO;using System.Linq;using System.Collections;using UnityEngine;
namespace Ossastra.WorldRendering.Vegetation.Development
{
 public sealed class GrassLabPlayerRun:MonoBehaviour
 {
  public GrassLabCostSampler sampler;
  IEnumerator Start()
  {
#if !UNITY_EDITOR
   var args=Environment.GetCommandLineArgs();if(!args.Contains("--grass-benchmark"))yield break;
   if(sampler==null||sampler.controls==null||!gameObject.scene.path.StartsWith("Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/",StringComparison.Ordinal)){Application.Quit(3);yield break;}
   yield return null;yield return null;
   int vsync=QualitySettings.vSyncCount,target=Application.targetFrameRate;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
   string densityArg=args.FirstOrDefault(a=>a.StartsWith("--grass-density=",StringComparison.Ordinal));float density=-1;
   if(densityArg!=null&&!float.TryParse(densityArg.Substring(16),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out density)){Application.Quit(4);yield break;}
   if(density>0){foreach(var layer in sampler.layers){if(density>layer.profile.maxDensity){Application.Quit(5);yield break;}var data=Instantiate(layer.data);data.hideFlags=HideFlags.DontSave;data.placements=layer.Generate(density);data.density=density;layer.data=data;layer.profile.density=density;layer.RebuildFromSaved();}}
   string name="player-"+(density>0?density.ToString("0",System.Globalization.CultureInfo.InvariantCulture):"recommended")+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json";
   string output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"artifacts/VEGETATION-01A",name);
   if(!sampler.Begin(output,3,10)){Debug.LogError(sampler.Status);Application.Quit(6);yield break;}
   while(sampler.IsRunning)yield return null;
   QualitySettings.vSyncCount=vsync;Application.targetFrameRate=target;Debug.Log("GRASS_BENCHMARK_RESULT "+sampler.ResultPath);Application.Quit(File.Exists(output)?0:7);
#else
   yield break;
#endif
  }
 }
}
