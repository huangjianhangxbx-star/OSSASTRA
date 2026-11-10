/* 功能：只保存场景环境漫射色度候选；不持有灯、房间、角色参数或时钟。
 * 边界：返回规范化副本，不在渲染采样时改写作者配置。 */
using System;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 [Serializable]
 public sealed class SceneEnvironmentSettings
 {
  [Range(0,1)] public float groundStrength=.45f;
  [Range(0,1)] public float decorationStrength=.35f;
  [Range(0,1)] public float voxelStrength=.45f;
  [Range(0,.5f)] public float groundBaseRecolorStrength; // opt-in before lighting; old authored profiles stay zero
  [Range(-30,0)] public float twilightStartElevation=-8;
  [Range(-20,10)] public float twilightFullElevation=-1;
  [Range(0,45)] public float sunsetFullElevation=10;
  [Range(1,89)] public float sunsetEndElevation=30;
  public SceneEnvironmentSettings Clone()=>(SceneEnvironmentSettings)MemberwiseClone();
  public SceneEnvironmentSettings Sanitize()
  {
   var result=Clone();
   result.groundStrength=Bound(groundStrength,0,1,.45f);
   result.decorationStrength=Bound(decorationStrength,0,1,.35f);
   result.voxelStrength=Bound(voxelStrength,0,1,.45f);
   result.groundBaseRecolorStrength=Bound(groundBaseRecolorStrength,0,.5f,0);
   result.twilightStartElevation=Bound(twilightStartElevation,-30,0,-8);
   result.twilightFullElevation=Bound(twilightFullElevation,result.twilightStartElevation+.1f,10,-1);
   result.sunsetFullElevation=Bound(sunsetFullElevation,Math.Max(0,result.twilightFullElevation),45,10);
   result.sunsetEndElevation=Bound(sunsetEndElevation,result.sunsetFullElevation+.1f,89,30);
   return result;
  }
  static float Bound(float value,float min,float max,float fallback)=>Math.Min(max,Math.Max(min,float.IsNaN(value)||float.IsInfinity(value)?fallback:value));
 }
}
