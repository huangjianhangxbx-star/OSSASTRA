/* 脚本：GroundDecorationInstance
 * 功能：标记工坊拥有的原生装饰实例、手摆/生成来源与锁；根Transform为唯一脚点作者数据。
 * 边界：不在Update随机、不保存平行JSON位置；visual派生朝向只由地图表现组件更新。
 */
using UnityEngine;
namespace Ossastra.GroundResearch
{
    [DisallowMultipleComponent]
    public sealed class GroundDecorationInstance : MonoBehaviour
    {
        public int schemaVersion = 1;
        public string instanceId, assetId, generationGroup;
        public GroundDecorationDefinition definition;
        public bool isLocked, generated;
        public int seed;
        public Transform visual;
    }
}
