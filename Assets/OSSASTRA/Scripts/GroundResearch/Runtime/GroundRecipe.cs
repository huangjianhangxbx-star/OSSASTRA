/* 脚本：GroundRecipe
 * 功能：定义底纹、边缘族、世界周期与裁剪参数；assetId区分配方和实际底纹来源。
 * 边界：定义不保存格子，不在OnValidate修改共享材质；应用须经Editor事务创建地图自有实例。
 */
using UnityEngine;
namespace Ossastra.GroundResearch
{
    [CreateAssetMenu(menuName="OSSASTRA/地表/地表配方")]
    public sealed class GroundRecipe : ScriptableObject
    {
        public int schemaVersion = 1;
        public string assetId, revision = "1", displayName, detailAssetId;
        [TextArea(2,5)] public string evidence;
        public Texture2D detailTexture;
        public GroundEdgeFamily edgeFamily;
        [Min(.1f)] public float worldRepeatPeriod = 16f;
        [Range(.01f,.99f)] public float cutoff = .5f;
    }
}
