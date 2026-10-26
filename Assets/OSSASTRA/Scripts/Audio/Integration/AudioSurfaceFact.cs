/* 脚本：AudioSurfaceFact
 * 功能：在真实碰撞体所属物体上声明声音使用的地表与接触材质。
 * 边界：只读作者事实，不从贴图、名称或坐标猜材质，不改伤害或碰撞规则。
 * 协作：脚步读取向下射线实际命中的地表；接触适配器读取实际受击目标的材质。
 */
using UnityEngine;

namespace Ossastra.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioSurfaceFact : MonoBehaviour
    {
        [Tooltip("脚步地表：generic / stone / wood / dirt / metal；未标注保留generic。")]
        public string surface = "generic";
        [Tooltip("明确的接触材质，如flesh / metal / stone / wood；留空表示未知，不能推定为肉体。")]
        public string impactMaterial = "";
    }
}
