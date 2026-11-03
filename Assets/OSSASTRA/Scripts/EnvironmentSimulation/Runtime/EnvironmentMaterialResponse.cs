/* 功能：给自有环境样板/素材显式标记受光角色与响应系数；组件只描述数据，不写原材质或业务状态。 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public enum EnvironmentMaterialRole{NativeLit,Ground,Decoration,Character}
 [DisallowMultipleComponent]
 public sealed class EnvironmentMaterialResponse:MonoBehaviour
 {
  public EnvironmentMaterialRole role=EnvironmentMaterialRole.Ground;
  [Range(0,1)] public float response=1;
  public bool receiveSunShadow=true;
  [Range(0,1)] public float minimumReadability;
  public Renderer[] renderers=System.Array.Empty<Renderer>();
 }
}
