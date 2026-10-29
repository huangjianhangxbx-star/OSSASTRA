/* 脚本：GroundEdgeFamily
 * 功能：引用一套已校验的原生Tile/RuleTile边缘语义与基层Tile；供多份底纹配方复用。
 * 边界：不复制地图格、不从Sprite数量猜规则；原作引用与原创族按assetId/证据区分。
 */
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch
{
    [CreateAssetMenu(menuName="OSSASTRA/地表/边缘族")]
    public sealed class GroundEdgeFamily : ScriptableObject
    {
        public int schemaVersion = 1;
        public string assetId, revision = "1", displayName;
        [TextArea(2,5)] public string evidence;
        public TileBase paintTemplate, fillTemplate;
        public Sprite preview;
        public bool validated;
    }
}
