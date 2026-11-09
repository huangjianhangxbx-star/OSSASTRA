/* 功能：保存显式表面局部根/法线与固定分布；网格是可重建的显示资源。 */
using UnityEngine;
namespace Ossastra.WorldRendering.Vegetation
{
 [CreateAssetMenu(menuName="OSSASTRA/场景/草层放置数据",fileName="GrassPatchData")]
 public sealed class GrassPatchDataSO:ScriptableObject
 {
  public int seed;public float density,maxDensity;
  public string[] surfaceIds=System.Array.Empty<string>();
  public GrassPlacement[] placements=System.Array.Empty<GrassPlacement>();
 }
 [System.Serializable]
 public sealed class GrassSurfaceBinding
 {
  public string id;
  public MeshCollider collider;
 }
}
