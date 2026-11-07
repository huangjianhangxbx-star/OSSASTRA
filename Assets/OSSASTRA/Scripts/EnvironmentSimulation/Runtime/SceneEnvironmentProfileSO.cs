/* 功能：独立保存场景环境漫射色度权重；启用权仍由现有EnvironmentSkyBinding持有。 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 [CreateAssetMenu(menuName="Ossastra/Environment/Scene Diffuse Chroma")]
 public sealed class SceneEnvironmentProfileSO:ScriptableObject
 {
  public SceneEnvironmentSettings settings=new SceneEnvironmentSettings();
 }
}
