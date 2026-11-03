/* 功能：明确登记主场环境观察区及本轮装配所有者，为幂等组装和检查提供稳定引用。
 * 边界：不是建筑、掉落或玩法实体，不拥有Current、时钟、碰撞策略或可视权限。
 * 生命周期：只保存作者引用；重装配入口只复用同一标记，不清场、不按名称猜测旧内容归属。
 */
using UnityEngine;
using Ossastra.EnvironmentSimulation;
namespace Ossastra.Development.Environment
{
 public sealed class EnvironmentObservationAuthoring:MonoBehaviour
 {
  public int configurationVersion=1;
  public EnvironmentRoot environment;
  public EnvironmentSimulationPanel panel;
  public Transform observationRoot;
  public Renderer[] authoredRenderers=System.Array.Empty<Renderer>();
  public BoxCollider[] sightBlockers=System.Array.Empty<BoxCollider>();
  public Vector3 observationOrigin;
 }
}
