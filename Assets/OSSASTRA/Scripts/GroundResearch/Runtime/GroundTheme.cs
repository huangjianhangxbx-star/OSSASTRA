/* 脚本：GroundTheme
 * 功能：保存有序地表槽位与装饰默认定义，供新地图复用；每地图使用自己的主题副本。
 * 边界：不是第二份世界布局；改主题不会自动重撒花草或迁移任何已刷格。
 */
using System;
using UnityEngine;
namespace Ossastra.GroundResearch
{
    [Serializable]
    public sealed class GroundThemeLayer
    {
        public string slotId, displayName;
        public GroundRecipe recipe;
        public GroundLayerPurpose purpose;
        public bool locked;
    }
    [CreateAssetMenu(menuName="OSSASTRA/地表/场景主题")]
    public sealed class GroundTheme : ScriptableObject
    {
        public int schemaVersion = 1;
        public string assetId, revision = "1", displayName;
        [TextArea(2,5)] public string evidence;
        public GroundThemeLayer[] layers = Array.Empty<GroundThemeLayer>();
        public GroundDecorationDefinition[] decorations = Array.Empty<GroundDecorationDefinition>();
        [Range(0,50)] public float densityPer100 = 4f;
        [Min(.05f)] public float minimumSpacing = .7f;
        [Range(0,3)] public int roadExclusionCells;
        public int seed = 123;
    }
}
