/* 脚本：GroundAuthorRuleTile
 * 功能：保留原生Tilemap上已解锁的作者矩阵/flags，同时继续使用RuleTile的邻接与自动转角。
 * 边界：不存第二套格子数据；新格没有缓存Sprite，完全使用原生规则初始化。
 * 协作：工坊为地图克隆定义时使用；Tile Palette在Undo/Redo刷新目标也不会抹掉已解锁作者值。
 */
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch
{
    public sealed class GroundAuthorRuleTile : RuleTile
    {
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            // Getter读取Tilemap原生缓存。只有已有Sprite才代表已初始化格，不能把新格默认flags=0误认手摆。
            bool initialized = tilemap.GetSprite(position) != null;
            TileFlags flags = tilemap.GetTileFlags(position);
            bool manualTransform = initialized && (flags & TileFlags.LockTransform) == 0;
            Matrix4x4 authored = manualTransform ? tilemap.GetTransformMatrix(position) : Matrix4x4.identity;
            base.GetTileData(position, tilemap, ref tileData);
            if (initialized) tileData.flags = flags;
            if (manualTransform) tileData.transform = authored;
        }
    }
}
